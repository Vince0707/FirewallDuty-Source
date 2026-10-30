using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The menu part of the game: login, main menu, missions and the rest of the screens that used to be in the Android app.
// Lives in the MainMenu scene. Screens are built from code (see UiKit) the first time they are shown.
public class MenuApp : MonoBehaviour
{
    public const string MenuSceneName = "MainMenu_Scene";

    public static MenuApp Instance { get; private set; }

    // Set before loading the menu scene to open a particular screen first (for example "MissionOutcome" after a mission).
    public static string StartScreen;

    private readonly Dictionary<string, MenuScreen> screens = new Dictionary<string, MenuScreen>();
    private readonly Stack<string> history = new Stack<string>();

    private RectTransform screensRoot;
    private CanvasGroup busyOverlay;
    private TMP_Text busyLabel;
    private RectTransform toastBox;
    private TMP_Text toastText;
    private LayoutElement toastLayout;
    private Coroutine toastRoutine;
    private Coroutine fadeRoutine;

    private MenuScreen current;
    private string currentId;

    public RectTransform Root { get; private set; }

    private void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        GameSettings.Apply();
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        EnsureAudioListener();
        EnsureEventSystem();
        BuildCanvas();
        MenuMusic.Create(gameObject);   // the background music of the menu
    }

    private void Start()
    {
        string first = StartScreen;
        StartScreen = null;

        if (string.IsNullOrEmpty(first))
            first = PlayerSession.IsSignedIn ? "GameMenu" : "Login";

        Show(first, null, false);

        // A save that failed while the mission scene was ending.
        if (!string.IsNullOrEmpty(PlayerSession.SaveError))
            Toast("Could not save your progress online - " + PlayerSession.SaveError);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // The phone's Back button arrives as Escape.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && current != null)
            current.OnBack();
    }

    // ---- Screens ----------------------------------------------------------------------------------------------

    public void Show(string id, object arg = null, bool remember = true)
    {
        if (!screens.TryGetValue(id, out MenuScreen screen))
        {
            screen = MenuScreens.Create(id);
            if (screen == null)
            {
                UnityEngine.Debug.LogError("Menu screen '" + id + "' does not exist.");
                return;
            }

            screen.Init(this, screensRoot, id);
            screens[id] = screen;
        }

        if (remember && currentId != null && currentId != id)
            history.Push(currentId);

        MenuScreen previous = current;
        current = screen;
        currentId = id;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(Transition(previous, screen, arg));
    }

    // Goes to the screen before this one, or to the fallback if there is none.
    public void Back(string fallback = "GameMenu")
    {
        if (history.Count > 0)
            Show(history.Pop(), null, false);
        else
            Show(fallback, null, false);
    }

    public void ClearHistory()
    {
        history.Clear();
    }

    private IEnumerator Transition(MenuScreen from, MenuScreen to, object arg)
    {
        const float duration = 0.18f;

        if (from != null && from != to)
        {
            from.OnHide();
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                from.Group.alpha = 1f - t / duration;
                yield return null;
            }

            from.Root.gameObject.SetActive(false);
        }

        to.Root.gameObject.SetActive(true);
        to.Group.alpha = 0f;
        to.OnShow(arg);

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            to.Group.alpha = t / duration;
            yield return null;
        }

        to.Group.alpha = 1f;
    }

    // ---- Messages and busy overlay ---------------------------------------------------------------------------

    // Like an Android Toast: a short message at the bottom of the screen.
    public void Toast(string message)
    {
        if (toastRoutine != null)
            StopCoroutine(toastRoutine);

        toastRoutine = StartCoroutine(ShowToast(message));
    }

    private IEnumerator ShowToast(string message)
    {
        toastText.text = message;
        toastLayout.preferredWidth = Mathf.Min(toastText.GetPreferredValues(message).x + 2f, 560f);
        toastBox.gameObject.SetActive(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(toastBox);

        CanvasGroup group = toastBox.GetComponent<CanvasGroup>();
        group.alpha = 1f;

        yield return new WaitForSecondsRealtime(2.4f);

        for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.3f;
            yield return null;
        }

        toastBox.gameObject.SetActive(false);
    }

    // Blocks touches and shows a message while something is loading (login, saving ...).
    public void SetBusy(bool busy, string message = "Please wait...")
    {
        busyLabel.text = message;
        busyOverlay.alpha = busy ? 1f : 0f;
        busyOverlay.blocksRaycasts = busy;
        busyOverlay.interactable = busy;
    }

    // ---- Scenes -----------------------------------------------------------------------------------------------

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // ---- Building the canvas ----------------------------------------------------------------------------------

    // Sound needs an AudioListener, and the menu scene was made with a camera that has none: without one the menu is silent (no music, no click sounds).
    private static void EnsureAudioListener()
    {
        if (FindAnyObjectByType<AudioListener>() != null)
            return;

        Camera camera = FindAnyObjectByType<Camera>();
        GameObject host = camera != null ? camera.gameObject : new GameObject("AudioListener");
        host.AddComponent<AudioListener>();
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();   // without this a module made in code has no pointer actions and touches do nothing
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        // 800 x 360 units, as the Android layouts were drawn. Wider or taller screens show more of the backgrounds; the content
        // area is always fully visible.
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 360f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100f;

        Root = (RectTransform)canvasObject.transform;
        screensRoot = UiKit.New(Root, "Screens");
        UiKit.Stretch(screensRoot);

        BuildToast();
        BuildBusyOverlay();
    }

    private void BuildToast()
    {
        toastBox = UiKit.New(Root, "Toast");
        UiKit.Place(toastBox, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 0f));

        Image bg = toastBox.gameObject.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.12f, 0.92f);
        bg.raycastTarget = false;

        HorizontalLayoutGroup layout = toastBox.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = toastBox.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        toastBox.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

        TextMeshProUGUI text = UiKit.Text(toastBox, "", 14f, Color.white);
        text.textWrappingMode = TextWrappingModes.Normal;
        toastText = text;

        // The width is set per message (see ShowToast) so a long message wraps inside the screen.
        toastLayout = text.gameObject.AddComponent<LayoutElement>();
        toastBox.gameObject.SetActive(false);
    }

    private void BuildBusyOverlay()
    {
        RectTransform overlay = UiKit.New(Root, "Busy");
        UiKit.Stretch(overlay);

        busyOverlay = overlay.gameObject.AddComponent<CanvasGroup>();
        busyOverlay.alpha = 0f;
        busyOverlay.blocksRaycasts = false;
        busyOverlay.interactable = false;

        Image dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        busyLabel = UiKit.Text(overlay, "Please wait...", 18f, Color.white, FontStyles.Bold);
        UiKit.Center(busyLabel.rectTransform, 400f, 40f);
    }
}

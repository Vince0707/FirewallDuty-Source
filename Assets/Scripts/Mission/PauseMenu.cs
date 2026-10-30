using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The PAUSE button at the top left of the screen while a mission is played, and what it opens: Resume, Settings and Quit, in the gold-and-navy design of
// the main menu (cut-corner panels, gold accents, the menu fonts).
// Everything is made in code (the scenes are not changed): MissionScenarioController calls PauseMenu.Create() when the mission starts.
// While paused the game is frozen (Time.timeScale = 0, so the timer stops too). The phone's Back button (Escape) pauses, goes back one step, or resumes.
public class PauseMenu : MonoBehaviour
{
    private static PauseMenu instance;

    private static readonly Color RedFill = new Color32(0x7A, 0x1F, 0x2B, 238);
    private static readonly Color RedEdge = new Color32(0xE0, 0x6A, 0x72, 255);

    private GameObject overlay, mainPanel, settingsPanel, confirmPanel, pauseButtonRoot;
    private readonly Image[] graphicsFills = new Image[3];
    private readonly TMP_Text[] graphicsLabels = new TMP_Text[3];
    private bool paused;

    public static void Create()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("PauseMenu");
        instance = go.AddComponent<PauseMenu>();
    }

    private void Awake()
    {
        Build();
    }

    private void Update()
    {
        // While the tablet is open (it fills the screen, with its own BACK button in the same corner) the PAUSE button is hidden.
        if (pauseButtonRoot != null)
        {
            bool show = paused || !TabletOverlay.IsOpen;
            if (pauseButtonRoot.activeSelf != show)
                pauseButtonRoot.SetActive(show);
        }

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (!paused)
            Pause();
        else if (settingsPanel.activeSelf || confirmPanel.activeSelf)
            ShowPanel(mainPanel);
        else
            Resume();
    }

    private void OnDestroy()
    {
        if (paused)
            Time.timeScale = 1f;

        if (instance == this)
            instance = null;
    }

    // ---- What the buttons do ------------------------------------------------------------------------------------------------

    private void Pause()
    {
        if (paused)
            return;

        paused = true;
        Time.timeScale = 0f;
        overlay.SetActive(true);
        ShowPanel(mainPanel);
    }

    private void Resume()
    {
        paused = false;
        Time.timeScale = 1f;
        overlay.SetActive(false);
    }

    private void Quit()
    {
        // Back to the main menu; the mission stays as the "continue" mission there (it starts again from the beginning).
        paused = false;
        Time.timeScale = 1f;
        MissionFlow.Current = null;
        SceneManager.LoadScene(MenuApp.MenuSceneName);
    }

    private void ShowPanel(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        confirmPanel.SetActive(panel == confirmPanel);

        if (panel == settingsPanel)
            RefreshGraphics();
    }

    // ---- Building the screens -----------------------------------------------------------------------------------------------

    private void Build()
    {
        // The button, always on screen: a small cut-corner plate with gold edges.
        RectTransform buttonCanvas = MissionOverlay.CreateCanvas("PauseButtonCanvas", 120);
        buttonCanvas.SetParent(transform, false);
        pauseButtonRoot = buttonCanvas.gameObject;

        RectTransform pause = MenuStyle.Chamfer(buttonCanvas, "PauseButton", 14f, 12f, 120f, 40f, 9f, 0f, 0f, 9f, MenuStyle.PanelDark, MenuStyle.Gold);
        MenuStyle.MakeButton(pause, Pause);
        TextMeshProUGUI pauseLabel = MenuStyle.Text(pause, "II  PAUSE", 16f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Center, "Label");
        UiKit.Stretch(pauseLabel.rectTransform);
        UiLayout.Spaced(pauseLabel, 0.08f);

        // The pause screens, over everything.
        RectTransform menuCanvas = MissionOverlay.CreateCanvas("PauseMenuCanvas", 130);
        menuCanvas.SetParent(transform, false);

        // Everything that belongs to the pause screens is inside this one object, so switching it off hides all of it (the dim layer and every panel).
        RectTransform screens = UiKit.New(menuCanvas, "Screens");
        UiKit.Stretch(screens);
        overlay = screens.gameObject;

        Image dim = UiKit.Solid(screens, "Dim", new Color(0.02f, 0.06f, 0.14f, 0.72f));
        dim.raycastTarget = true;   // it also stops touches reaching the game while paused
        UiKit.Stretch(dim.rectTransform);

        RectTransform frame = UiLayout.Frame(screens);

        mainPanel = BuildMain(frame);
        settingsPanel = BuildSettings(frame);
        confirmPanel = BuildConfirm(frame);

        overlay.SetActive(false);
    }

    // A panel in the middle of the screen.
    private static RectTransform Card(RectTransform frame, string name, float width, float height)
    {
        RectTransform card = MenuStyle.Chamfer(frame, name, (UiLayout.DesignWidth - width) * 0.5f, (UiLayout.DesignHeight - height) * 0.5f, width, height,
                                               20f, 0f, 0f, 20f, MenuStyle.PanelDark, MenuStyle.Edge);
        card.GetComponent<Image>().raycastTarget = true;   // a touch on the panel must not reach the game behind it
        MenuStyle.Diagonal(card, MenuStyle.Corner.TopLeft, 20f, MenuStyle.Gold);
        MenuStyle.Diagonal(card, MenuStyle.Corner.BottomRight, 20f, MenuStyle.Gold);
        return card;
    }

    // The heading of a panel with a gold line under it.
    private static void Heading(RectTransform card, string text, float width, float y)
    {
        TextMeshProUGUI heading = MenuStyle.Text(card, text, 28f, MenuStyle.Cream, MenuFonts.Title, TextAlignmentOptions.Center, "Heading");
        UiLayout.TopLeft(heading.rectTransform, 20f, y, width - 40f, 36f);
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        UiLayout.Spaced(heading, 0.06f);

        Image line = UiKit.Solid(card, "HeadingLine", new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, 0.7f));
        UiLayout.TopLeft(line.rectTransform, width * 0.5f - 60f, y + 40f, 120f, 1.6f);
    }

    // A wide button: a gold one for the main choice, a navy one for the others, a red one for the leaving ones.
    private static RectTransform Button(RectTransform card, string name, string label, float x, float y, float width, float height, Color fill, Color outline, Color textColor, Action action)
    {
        RectTransform button = MenuStyle.Rounded(card, name, x, y, width, height, fill, outline);
        MenuStyle.MakeButton(button, () => action());

        TextMeshProUGUI text = MenuStyle.Text(button, label, 20f, textColor, MenuFonts.Label, TextAlignmentOptions.Center, "Label");
        UiKit.Stretch(text.rectTransform);
        UiLayout.Spaced(text, 0.08f);
        return button;
    }

    private GameObject BuildMain(RectTransform frame)
    {
        const float width = 340f;
        RectTransform card = Card(frame, "MainPanel", width, 292f);
        Heading(card, "PAUSED", width, 24f);

        float x = (width - 250f) * 0.5f;
        Button(card, "Resume", "RESUME", x, 88f, 250f, 46f, MenuStyle.Gold, Color.clear, MenuStyle.Navy, Resume);
        Button(card, "Settings", "SETTINGS", x, 146f, 250f, 46f, MenuStyle.Panel, MenuStyle.Edge, MenuStyle.Cream, () => ShowPanel(settingsPanel));
        Button(card, "Quit", "QUIT", x, 204f, 250f, 46f, RedFill, RedEdge, MenuStyle.Cream, () => ShowPanel(confirmPanel));
        return card.gameObject;
    }

    private GameObject BuildConfirm(RectTransform frame)
    {
        const float width = 470f;
        RectTransform card = Card(frame, "ConfirmPanel", width, 228f);
        Heading(card, "QUIT THIS MISSION?", width, 22f);

        TextMeshProUGUI text = MenuStyle.Text(card, "Your progress in this mission will be lost.\nYou can start it again from the main menu.", 12f, MenuStyle.Soft, MenuFonts.Body,
                                              TextAlignmentOptions.Top, "Message");
        UiLayout.TopLeft(text.rectTransform, 30f, 84f, width - 60f, 40f);

        Button(card, "ConfirmQuit", "QUIT", 40f, 150f, 190f, 46f, RedFill, RedEdge, MenuStyle.Cream, Quit);
        Button(card, "ConfirmCancel", "CANCEL", width - 40f - 190f, 150f, 190f, 46f, MenuStyle.Panel, MenuStyle.Edge, MenuStyle.Cream, () => ShowPanel(mainPanel));
        return card.gameObject;
    }

    private GameObject BuildSettings(RectTransform frame)
    {
        const float width = 540f;
        RectTransform card = Card(frame, "SettingsPanel", width, 300f);
        Heading(card, "SETTINGS", width, 18f);

        // Graphics: Low / Medium / High.
        TextMeshProUGUI graphics = MenuStyle.Text(card, "Graphics", 15f, MenuStyle.Cream, MenuFonts.Label, TextAlignmentOptions.Left, "GraphicsLabel");
        UiLayout.TopLeft(graphics.rectTransform, 30f, 76f, 160f, 24f);

        string[] names = { "Low", "Medium", "High" };
        for (int i = 0; i < 3; i++)
        {
            int level = i;
            RectTransform button = MenuStyle.Rounded(card, "Graphics_" + names[i], 214f + i * 100f, 72f, 92f, 30f, MenuStyle.PlateFillColor, MenuStyle.Edge);
            MenuStyle.MakeButton(button, () =>
            {
                GameSettings.GraphicsQuality = level;
                GameSettings.Apply();
                RefreshGraphics();
            });

            TextMeshProUGUI label = MenuStyle.Text(button, names[i], 15f, MenuStyle.Cream, MenuFonts.Label, TextAlignmentOptions.Center, "Label");
            UiKit.Stretch(label.rectTransform);

            graphicsFills[i] = button.GetComponent<Image>();
            graphicsLabels[i] = label;
        }

        // The three sliders, with the same names and saved values as the Settings screen of the main menu.
        AddSliderRow(card, "Sensitivity", 118f, GameSettings.Sensitivity, value => GameSettings.Sensitivity = value, false);
        AddSliderRow(card, "Master Volume", 160f, GameSettings.MasterVolume, value => { GameSettings.MasterVolume = value; GameSettings.Apply(); }, true);
        AddSliderRow(card, "Sound Effects", 202f, GameSettings.SfxVolume, value => GameSettings.SfxVolume = value, true);

        Button(card, "Back", "BACK", (width - 180f) * 0.5f, 250f, 180f, 38f, MenuStyle.Panel, MenuStyle.Gold, MenuStyle.Gold, () => ShowPanel(mainPanel));
        return card.gameObject;
    }

    private void RefreshGraphics()
    {
        for (int i = 0; i < graphicsFills.Length; i++)
        {
            if (graphicsFills[i] == null)
                continue;

            bool selected = i == GameSettings.GraphicsQuality;
            graphicsFills[i].color = selected ? MenuStyle.Gold : MenuStyle.PlateFillColor;
            graphicsLabels[i].color = selected ? MenuStyle.Navy : MenuStyle.Cream;
        }
    }

    private void AddSliderRow(RectTransform card, string title, float y, int value, Action<int> changed, bool preview)
    {
        TextMeshProUGUI label = MenuStyle.Text(card, title + ": " + value + "%", 15f, MenuStyle.Cream, MenuFonts.Label, TextAlignmentOptions.Left, "SliderLabel");
        UiLayout.TopLeft(label.rectTransform, 30f, y + 4f, 190f, 24f);

        Slider slider = MakeSlider(card, 270f, 34f, value);
        UiLayout.TopLeft((RectTransform)slider.transform, 224f, y - 1f, 270f, 34f);

        slider.onValueChanged.AddListener(v =>
        {
            int whole = (int)v;
            label.text = title + ": " + whole + "%";
            changed(whole);

            if (preview)
                GameAudio.PlayPreview();
        });
    }

    // ---- A slider in the same look as the sliders of the main menu's Settings, in gold ------------------------------------------------

    private static Slider MakeSlider(RectTransform parent, float width, float height, int value)
    {
        RectTransform root = UiKit.New(parent, "Slider");
        root.sizeDelta = new Vector2(width, height);

        // The invisible picture over the whole row makes the row easy to touch.
        Image touch = UiKit.Solid(root, "Touch", new Color(1f, 1f, 1f, 0f));
        touch.raycastTarget = true;
        UiKit.Stretch(touch.rectTransform);

        Image track = UiKit.Solid(root, "Track", new Color32(0x2F, 0x55, 0x8C, 0xFF));
        track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(-18f, 4f);

        // A Slider stretches its fill over the full height of the fill area and its handle over the full height of the handle area, so the fill area
        // is exactly as tall as the track (4) and the handle area exactly as tall as the thumb (18).
        RectTransform fillArea = UiKit.New(root, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.pivot = new Vector2(0.5f, 0.5f);
        fillArea.anchoredPosition = Vector2.zero;
        fillArea.sizeDelta = new Vector2(-18f, 4f);

        Image fill = UiKit.Solid(fillArea, "Fill", MenuStyle.Gold);
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleArea = UiKit.New(root, "Handle Slide Area");
        handleArea.anchorMin = new Vector2(0f, 0.5f);
        handleArea.anchorMax = new Vector2(1f, 0.5f);
        handleArea.pivot = new Vector2(0.5f, 0.5f);
        handleArea.anchoredPosition = Vector2.zero;
        handleArea.sizeDelta = new Vector2(-18f, 18f);

        Image handle = UiKit.Pic(handleArea, "Handle", UiKit.CircleSprite(), MenuStyle.Cream, true);
        handle.type = Image.Type.Simple;
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(18f, 0f);   // 18 wide, as tall as the area (18)

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(value);
        return slider;
    }
}

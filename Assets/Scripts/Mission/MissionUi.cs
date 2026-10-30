using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The pieces of screen the mission draws over the game: the overlay canvas, the hint at the top, the conversation box, the meeting
// subtitles and the marker over whoever or whatever the player should go to.

// Screens drawn over the game by the mission: a canvas, and the EventSystem it needs.
public static class MissionOverlay
{
    public static RectTransform CreateCanvas(string name, int sortingOrder)
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 360f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        return (RectTransform)go.transform;
    }
}

// A short message at the top of the screen. Touches pass through it.
public class MissionBanner : MonoBehaviour
{
    private static GameObject current;

    public static void Show(string title, string line, float seconds)
    {
        if (current != null)
            Destroy(current);

        RectTransform root = MissionOverlay.CreateCanvas("MissionBanner", 62);
        current = root.gameObject;

        // In the upper left, under the objective plate (which is 14 from the left, 60 from the top and 68 high): a small navy plate with a gold bar down
        // its left edge, so it no longer covers the middle of the view.
        RectTransform plate = MenuStyle.Chamfer(root, "Back", 14f, 134f, 318f, 62f, 0f, 0f, 0f, 14f, new Color32(0x0A, 0x20, 0x45, 0xEE), new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, 0.85f));

        Image bar = UiKit.Solid(plate, "Accent", MenuStyle.Gold);
        bar.raycastTarget = false;
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        bar.rectTransform.sizeDelta = new Vector2(4f, 0f);

        TextMeshProUGUI heading = MenuStyle.Text(plate, title, 12.5f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Left, "Heading");
        UiLayout.TopLeft(heading.rectTransform, 16f, 6f, 292f, 18f);
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.overflowMode = TextOverflowModes.Ellipsis;
        UiLayout.Spaced(heading, 0.05f);

        TextMeshProUGUI hint = MenuStyle.Text(plate, line, 11f, MenuStyle.Cream, MenuFonts.Body, TextAlignmentOptions.TopLeft, "Line");
        UiLayout.TopLeft(hint.rectTransform, 16f, 25f, 292f, 34f);
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.overflowMode = TextOverflowModes.Ellipsis;

        current.AddComponent<MissionBanner>().StartCoroutine(Hide(current, seconds));
    }

    private static IEnumerator Hide(GameObject target, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        if (target != null)
            Destroy(target);
    }

    private static GameObject currentSpeech;

    // Someone speaks (the taxi or bus driver): the same box at the bottom of the screen as when the player talks to a character, so it is not in the way
    // at the top of the view. It goes by itself after "seconds"; touches pass through it and the player can keep looking around.
    public static void Say(string speaker, string line, float seconds)
    {
        GameSounds.Talk();

        if (currentSpeech != null)
            Destroy(currentSpeech);

        RectTransform root = MissionOverlay.CreateCanvas("MissionSpeech", 62);
        currentSpeech = root.gameObject;

        Image box = UiKit.Solid(root, "Box", UiKit.Hex("#F0114A73"));
        box.raycastTarget = false;
        box.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        box.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        box.rectTransform.pivot = new Vector2(0.5f, 0f);
        box.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        box.rectTransform.sizeDelta = new Vector2(470f, 78f);   // narrow enough to stay clear of the joystick and the hand button

        TextMeshProUGUI name = UiKit.Text(box.transform, speaker.ToUpperInvariant(), 12f, UiKit.Hex("#FFD600"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(400f, 18f));

        TextMeshProUGUI text = UiKit.Text(box.transform, line, 13f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Stretch(text.rectTransform, 16f, 8f, 16f, 30f);

        currentSpeech.AddComponent<MissionBanner>().StartCoroutine(Hide(currentSpeech, seconds));
    }
}

// The hint at the top left of the screen, under the PAUSE button: what the player is doing now. It stays until that step is done, then shows the next one.
// A navy plate with a gold bar down its left edge, the heading in gold and the instruction below it.
public class MissionHintHud : MonoBehaviour
{
    private GameObject canvas;
    private TMP_Text heading;
    private TMP_Text hint;
    private string shownHeading, shownHint;

    private void Awake()
    {
        RectTransform root = MissionOverlay.CreateCanvas("MissionHint", 60);
        canvas = root.gameObject;

        // Under the PAUSE button (which is 14 from the left, 12 from the top and 40 high), with a little gap.
        RectTransform plate = MenuStyle.Chamfer(root, "Back", 14f, 60f, 262f, 70f, 0f, 0f, 0f, 14f, new Color32(0x0A, 0x20, 0x45, 0x80), new Color(MenuStyle.Edge.r, MenuStyle.Edge.g, MenuStyle.Edge.b, 0.5f));

        // The gold bar down the left edge.
        Image bar = UiKit.Solid(plate, "Accent", MenuStyle.Gold);
        bar.raycastTarget = false;
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        bar.rectTransform.sizeDelta = new Vector2(4f, 0f);

        heading = MenuStyle.Text(plate, "", 12.5f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Left, "Heading");
        UiLayout.TopLeft(heading.rectTransform, 14f, 6f, 240f, 18f);
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.overflowMode = TextOverflowModes.Ellipsis;
        UiLayout.Spaced((TextMeshProUGUI)heading, 0.05f);

        hint = MenuStyle.Text(plate, "", 11f, MenuStyle.Cream, MenuFonts.Body, TextAlignmentOptions.TopLeft, "Hint");
        UiLayout.TopLeft(hint.rectTransform, 14f, 26f, 240f, 42f);
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void Update()
    {
        MissionSession session = MissionSession.Instance;
        MissionScenarioController director = MissionScenarioController.Instance;
        if (session == null || director == null || canvas == null)
            return;

        if (session.HasEnded)
        {
            canvas.SetActive(false);
            return;
        }

        bool hidden = director.HintHidden;
        if (canvas.activeSelf == hidden)
            canvas.SetActive(!hidden);

        director.GetHint(out string newHeading, out string newHint);

        if (newHeading != shownHeading || newHint != shownHint)
        {
            shownHeading = newHeading;
            shownHint = newHint;
            heading.text = newHeading;
            hint.text = newHint;
        }
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas);
    }
}

// A conversation box at the bottom of the screen: who is talking, what they say, and NEXT.
public class MissionDialogue : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private string[] lines;
    private int index;
    private System.Action done;
    private TMP_Text text;
    private TMP_Text speakerLabel;
    private string speaker;
    private TMP_Text nextLabel;
    private PlayerMobileController lockedPlayer;
    private bool previousLock;

    public static void Open(string speaker, string[] lines, System.Action done)
    {
        if (IsOpen || lines == null || lines.Length == 0)
            return;

        RectTransform root = MissionOverlay.CreateCanvas("MissionDialogue", 95);
        root.gameObject.AddComponent<MissionDialogue>().Begin(root, speaker, lines, done);
    }

    private void Begin(RectTransform root, string speaker, string[] lines, System.Action done)
    {
        this.lines = lines;
        this.done = done;
        this.speaker = speaker;
        IsOpen = true;

        lockedPlayer = FindAnyObjectByType<PlayerMobileController>();
        if (lockedPlayer != null)
        {
            previousLock = lockedPlayer.MovementLocked;
            lockedPlayer.MovementLocked = true;
        }

        Image box = UiKit.Solid(root, "Box", UiKit.Hex("#F0114A73"));
        box.raycastTarget = true;
        box.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        box.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        box.rectTransform.pivot = new Vector2(0.5f, 0f);
        box.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        box.rectTransform.sizeDelta = new Vector2(470f, 100f);   // narrow enough to stay clear of the joystick and the hand button

        TextMeshProUGUI name = UiKit.Text(box.transform, speaker.ToUpperInvariant(), 12f, UiKit.Hex("#FFD600"), FontStyles.Bold, TextAlignmentOptions.Left);
        speakerLabel = name;
        UiKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(400f, 18f));

        text = UiKit.Text(box.transform, "", 13f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Stretch(text.rectTransform, 16f, 30f, 16f, 28f);

        Button next = UiKit.Btn(box.transform, "Next", null, Color.clear, "NEXT", 13f, UiKit.Hex("#FFD600"), true, -1f, -1f);
        UiKit.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 4f), new Vector2(80f, 26f));
        next.targetGraphic.raycastTarget = true;
        next.onClick.AddListener(Next);
        nextLabel = next.GetComponentInChildren<TMP_Text>();

        Show();
    }

    private void Show()
    {
        GameSounds.Talk();

        // A line can name its own speaker: "Speaker||text".
        string line = lines[index];
        int bar = line.IndexOf("||");
        speakerLabel.text = (bar > 0 ? line.Substring(0, bar) : speaker).ToUpperInvariant();
        text.text = bar > 0 ? line.Substring(bar + 2) : line;
        nextLabel.text = index < lines.Length - 1 ? "NEXT" : "OK";
    }

    private void Next()
    {
        index++;

        if (index < lines.Length)
        {
            Show();
            return;
        }

        System.Action finished = done;
        Destroy(gameObject);
        finished?.Invoke();
    }

    private void OnDestroy()
    {
        IsOpen = false;

        if (lockedPlayer != null)
            lockedPlayer.MovementLocked = previousLock;
    }
}

// What is said while the player is not stuck in a conversation (the meeting): a strip at the bottom with who speaks, the line, and a
// progress bar. Touches pass through it and the player can keep looking around.
public class MissionSubtitle : MonoBehaviour
{
    private static MissionSubtitle current;

    private GameObject canvas;
    private TMP_Text speakerText;
    private TMP_Text lineText;
    private TMP_Text progressText;
    private Image progressFill;

    public static void Show(string speaker, string line, string progressLabel, float progress)
    {
        if (current == null)
        {
            RectTransform root = MissionOverlay.CreateCanvas("MissionSubtitle", 58);
            current = root.gameObject.AddComponent<MissionSubtitle>();
            current.Build(root);
        }

        current.canvas.SetActive(true);
        current.speakerText.text = speaker.ToUpperInvariant();
        current.lineText.text = line;
        current.progressText.text = progressLabel;
        current.progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }

    public static void Hide()
    {
        if (current != null)
            current.canvas.SetActive(false);
    }

    private void Build(RectTransform root)
    {
        canvas = root.gameObject;

        Image box = UiKit.Solid(root, "Box", UiKit.Hex("#E6114A73"));
        box.raycastTarget = false;
        box.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        box.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        box.rectTransform.pivot = new Vector2(0.5f, 0f);
        box.rectTransform.anchoredPosition = new Vector2(0f, 16f);
        box.rectTransform.sizeDelta = new Vector2(470f, 84f);   // narrow enough to stay clear of the joystick and the hand button

        speakerText = UiKit.Text(box.transform, "", 11f, UiKit.Hex("#FFD600"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(300f, 16f));

        progressText = UiKit.Text(box.transform, "", 11f, Color.white, FontStyles.Bold, TextAlignmentOptions.Right);
        UiKit.Place(progressText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -6f), new Vector2(200f, 16f));

        lineText = UiKit.Text(box.transform, "", 13f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Stretch(lineText.rectTransform, 14f, 14f, 14f, 24f);

        Image track = UiKit.Solid(box.transform, "Track", UiKit.Hex("#35536A"));
        track.rectTransform.anchorMin = new Vector2(0f, 0f);
        track.rectTransform.anchorMax = new Vector2(1f, 0f);
        track.rectTransform.pivot = new Vector2(0.5f, 0f);
        track.rectTransform.offsetMin = new Vector2(0f, 0f);
        track.rectTransform.offsetMax = new Vector2(0f, 6f);
        track.gameObject.AddComponent<RectMask2D>();

        progressFill = UiKit.Solid(track.transform, "Fill", UiKit.Hex("#FFD600"));
        progressFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        progressFill.rectTransform.offsetMin = Vector2.zero;
        progressFill.rectTransform.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas);

        if (current == this)
            current = null;
    }
}

// An arrow over whoever or whatever the player should go to now, with the distance in metres. It is drawn on top of the game, so it
// can be followed through walls; when the target is off screen it sits on the screen edge and points the way.
public class MissionMarker : MonoBehaviour
{
    private RectTransform canvasRect;
    private RectTransform marker;
    private RectTransform arrow;
    private TMP_Text title;
    private TMP_Text distanceText;
    private Camera viewer;
    private bool visible;
    private FloorGuide guide;   // the arrows on the floor that lead to the same target
    private Transform playerRoot;   // the player (its position is at the feet)

    private void Awake()
    {
        canvasRect = MissionOverlay.CreateCanvas("MissionMarker", 55);
        guide = gameObject.AddComponent<FloorGuide>();

        marker = UiKit.New(canvasRect, "Marker");
        marker.sizeDelta = new Vector2(120f, 70f);

        title = UiKit.Text(marker, "", 11f, UiKit.Hex("#FFD600"), FontStyles.Bold);
        UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(120f, 16f));

        Image triangle = UiKit.Pic(marker, "Arrow", UiKit.DownTriangleSprite(), UiKit.Hex("#FFD600"));
        triangle.type = Image.Type.Simple;
        arrow = triangle.rectTransform;
        UiKit.Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(26f, 26f));

        distanceText = UiKit.Text(marker, "", 12f, Color.white, FontStyles.Bold);
        UiKit.Place(distanceText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(120f, 18f));

        canvasRect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (canvasRect != null)
            Destroy(canvasRect.gameObject);
    }

    public void AddPocket(Bounds region, Vector3 outside, Vector3 inside, bool onlyWhenAsked)
    {
        if (guide != null)
            guide.AddPocket(region, outside, inside, onlyWhenAsked);
    }

    public void Hide()
    {
        if (guide != null)
            guide.Hide();

        if (visible)
        {
            visible = false;
            canvasRect.gameObject.SetActive(false);
        }
    }

    public void Track(Vector3 worldPoint, string label, Vector3 facing = default, Vector3[] via = null)
    {
        // The player's own camera (the one the player looks through), not whatever scene camera happens to be tagged MainCamera.
        if (viewer == null || !viewer.isActiveAndEnabled)
        {
            viewer = null;

            PlayerMobileController player = FindAnyObjectByType<PlayerMobileController>();
            if (player != null)
            {
                playerRoot = player.transform;
                foreach (Camera candidate in player.GetComponentsInChildren<Camera>())
                {
                    if (candidate.isActiveAndEnabled)
                    {
                        viewer = candidate;
                        break;
                    }
                }
            }

            if (viewer == null)
                viewer = Camera.main;

            if (viewer == null)
                return;
        }

        if (!visible)
        {
            visible = true;
            canvasRect.gameObject.SetActive(true);
        }

        if (guide != null)
            guide.Show(playerRoot != null ? playerRoot.position : viewer.transform.position - Vector3.up * 1.6f, worldPoint, facing, via);

        title.text = label;
        distanceText.text = Vector3.Distance(viewer.transform.position, worldPoint).ToString("0.0") + " m";

        Vector3 screen = viewer.WorldToScreenPoint(worldPoint);
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 point = new Vector2(screen.x, screen.y);

        // Behind the camera: mirror it so that it ends up on the right side of the edge.
        if (screen.z < 0f)
            point = center - (point - center);

        float margin = 60f;
        bool offScreen = screen.z < 0f || point.x < margin || point.x > Screen.width - margin || point.y < margin || point.y > Screen.height - margin;

        if (offScreen)
        {
            Vector2 direction = (point - center).normalized;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.up;

            // Where the line from the middle of the screen in that direction leaves the screen.
            float scaleX = direction.x != 0f ? (Screen.width * 0.5f - margin) / Mathf.Abs(direction.x) : float.MaxValue;
            float scaleY = direction.y != 0f ? (Screen.height * 0.5f - margin) / Mathf.Abs(direction.y) : float.MaxValue;
            point = center + direction * Mathf.Min(scaleX, scaleY);

            // The arrow points the way (its picture points down at rotation 0).
            arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
        }
        else
        {
            arrow.localRotation = Quaternion.identity;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, null, out Vector2 local);
        marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.anchoredPosition = local;
    }
}

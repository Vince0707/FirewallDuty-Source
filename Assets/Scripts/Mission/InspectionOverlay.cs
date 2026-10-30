using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// What the player sees when inspecting a place or machine in the scene (an Inspect step): a window with what it shows, and two decisions,
// FLAG AS EVIDENCE or LOOKS NORMAL. The mission controller judges the decision (the right one is kept, the wrong one costs time) and the
// answer is shown here. Same frame as the other mission screens (914 x 411).
public struct InspectionOutcome
{
    public string message;   // what to tell the player
    public bool close;       // true when the decision was right: the window closes after a moment
}

public class InspectionOverlay : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private EmailScenario.Station station;
    private bool alreadyChecked;
    private Func<bool, InspectionOutcome> decide;
    private MissionSession session;
    private RectTransform frame;
    private RectTransform content;
    private TMP_Text timeText;
    private string message = "";
    private string messageColor = "#FFD75E";   // red for a wrong decision, green for a right one, yellow otherwise
    private bool finishing;
    private PlayerMobileController lockedPlayer;
    private bool previousLock;

    public static void Open(EmailScenario.Station station, bool alreadyChecked, Func<bool, InspectionOutcome> decide)
    {
        if (IsOpen || MissionSession.Instance == null)
            return;

        RectTransform root = MissionOverlay.CreateCanvas("Inspection", 91);
        InspectionOverlay overlay = root.gameObject.AddComponent<InspectionOverlay>();
        overlay.Begin(root, station, alreadyChecked, decide);
    }

    private void Begin(RectTransform root, EmailScenario.Station station, bool alreadyChecked, Func<bool, InspectionOutcome> decide)
    {
        this.station = station;
        this.alreadyChecked = alreadyChecked;
        this.decide = decide;
        session = MissionSession.Instance;
        IsOpen = true;

        lockedPlayer = FindAnyObjectByType<PlayerMobileController>();
        if (lockedPlayer != null)
        {
            previousLock = lockedPlayer.MovementLocked;   // already locked when seated: give it back as it was
            lockedPlayer.MovementLocked = true;
        }

        Image dim = UiKit.Solid(root, "Dim", UiKit.Hex("#F2071019"));
        UiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        frame = UiLayout.Frame(root);
        content = UiKit.New(frame, "Content");
        UiKit.Stretch(content);

        if (alreadyChecked)
            message = "You already checked this one.";
            messageColor = "#FFD75E";

        Rebuild();
    }

    private void OnDestroy()
    {
        IsOpen = false;

        if (lockedPlayer != null)
            lockedPlayer.MovementLocked = previousLock;
    }

    private void Update()
    {
        if (timeText != null && session != null)
            timeText.text = session.FormatTime();
    }

    private void Close()
    {
        Destroy(gameObject);
    }

    private void Rebuild()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        TextMeshProUGUI heading = UiKit.Text(content, "FIREWALL DUTY  //  " + session.MissionTitle.ToUpperInvariant() + "  //  INSPECTION", 12f, UiKit.Hex("#58DFFF"),
                                             FontStyles.Bold, TextAlignmentOptions.Left);
        UiLayout.TopLeft(heading.rectTransform, 100f, 14f, 600f, 18f);

        timeText = UiKit.Text(content, session.FormatTime(), 14f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Right);
        UiLayout.TopRight(timeText.rectTransform, 100f, 13f, 80f, 20f);

        // The window of the machine.
        Image titleBar = UiKit.Solid(content, "WindowTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 100f, 40f, 714f, 24f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI title = UiKit.Text(titleBar.transform, station.title, 11f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(title.rectTransform);

        Image body = UiKit.Solid(content, "WindowBody", UiKit.Hex("#0F2236"));
        UiLayout.TopLeft(body.rectTransform, 100f, 64f, 714f, 200f);

        for (int i = 0; station.lines != null && i < station.lines.Length; i++)
        {
            TextMeshProUGUI line = UiKit.Text(body.transform, station.lines[i], 14f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -16f - i * 38f), new Vector2(676f, 34f));
        }

        // What to do, and the answer to the last decision.
        TextMeshProUGUI question = UiKit.Text(content, "Does this match one of the 3 pieces of evidence on your TABLET?", 12f, UiKit.Hex("#55DFFF"),
                                              FontStyles.Bold, TextAlignmentOptions.Left);
        UiLayout.TopLeft(question.rectTransform, 100f, 272f, 714f, 18f);

        if (!string.IsNullOrEmpty(message))
        {
            TextMeshProUGUI feedback = UiKit.Text(content, message, 11f, UiKit.Hex(messageColor), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiLayout.TopLeft(feedback.rectTransform, 100f, 292f, 714f, 32f);
        }

        if (!alreadyChecked)
        {
            Button flag = UiKit.Btn(content, "Flag", UiKit.Shape("briefing_panel_bg"), new Color(1f, 0.6f, 0.6f), "FLAG AS EVIDENCE", 12f, Color.white, true, -1f, -1f);
            UiLayout.TopLeft((RectTransform)flag.transform, 100f, 332f, 210f, 32f);
            flag.onClick.AddListener(() => Decide(true));

            Button normal = UiKit.Btn(content, "Normal", UiKit.Shape("briefing_panel_bg"), new Color(0.6f, 0.85f, 1f), "LOOKS NORMAL", 12f, Color.white, true, -1f, -1f);
            UiLayout.TopLeft((RectTransform)normal.transform, 318f, 332f, 210f, 32f);
            normal.onClick.AddListener(() => Decide(false));
        }

        Button tablet = UiKit.Btn(content, "Tablet", UiKit.Shape("briefing_panel_bg"), Color.white, "TABLET", 11f, UiKit.Hex("#F2D58A"), true, -1f, -1f);
        UiLayout.TopLeft((RectTransform)tablet.transform, 536f, 332f, 130f, 32f);
        tablet.onClick.AddListener(TabletOverlay.Open);

        Button close = UiKit.Btn(content, "Close", UiKit.Shape("briefing_panel_bg"), Color.white, "CLOSE", 11f, UiKit.Hex("#F2D58A"), true, -1f, -1f);
        UiLayout.TopLeft((RectTransform)close.transform, 674f, 332f, 140f, 32f);
        close.onClick.AddListener(Close);
    }

    private void Decide(bool flagged)
    {
        if (finishing)
            return;

        InspectionOutcome outcome = decide(flagged);
        message = outcome.message;
        messageColor = outcome.close ? "#5BE08A" : "#FF5A5A";

        if (outcome.close)
        {
            finishing = true;
            alreadyChecked = true;   // no more buttons while it closes
            StartCoroutine(CloseSoon());
        }

        Rebuild();
    }

    // A moment to read the answer, then the window closes so the player can walk on.
    private IEnumerator CloseSoon()
    {
        yield return new WaitForSecondsRealtime(1.8f);
        Close();
    }
}

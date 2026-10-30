using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Questioning a person (an Interview step): what he answers on top, the questions to choose from below. The mission controller judges each
// question: one that probes what the tablet lists as evidence records it, any other costs time. Same frame as the other mission screens
// (914 x 411).
public struct InterviewOutcome
{
    public string answer;      // what the person says
    public string note;        // what it meant for the investigation
    public bool finished;      // all the evidence is in: the window closes after a moment
    public bool error;         // a question that found nothing (it cost time): the note is red
    public bool good;          // a question that found evidence: the note is green
}

public class InterviewOverlay : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private string speaker;
    private EmailScenario.Question[] questions;
    private Func<int, bool> isAsked;
    private Func<int, InterviewOutcome> ask;
    private MissionSession session;
    private RectTransform content;
    private TMP_Text timeText;
    private string answer;
    private string note = "";
    private string noteColor = "#FFD75E";
    private bool finishing;
    private PlayerMobileController lockedPlayer;
    private bool previousLock;

    public static void Open(string speaker, string opening, EmailScenario.Question[] questions, Func<int, bool> isAsked, Func<int, InterviewOutcome> ask)
    {
        if (IsOpen || MissionSession.Instance == null || questions == null || questions.Length == 0)
            return;

        RectTransform root = MissionOverlay.CreateCanvas("Interview", 91);
        InterviewOverlay overlay = root.gameObject.AddComponent<InterviewOverlay>();
        overlay.Begin(root, speaker, opening, questions, isAsked, ask);
    }

    private void Begin(RectTransform root, string speaker, string opening, EmailScenario.Question[] questions, Func<int, bool> isAsked, Func<int, InterviewOutcome> ask)
    {
        this.speaker = speaker;
        this.questions = questions;
        this.isAsked = isAsked;
        this.ask = ask;
        answer = opening;
        session = MissionSession.Instance;
        GameSounds.Talk();
        IsOpen = true;

        lockedPlayer = FindAnyObjectByType<PlayerMobileController>();
        if (lockedPlayer != null)
        {
            previousLock = lockedPlayer.MovementLocked;
            lockedPlayer.MovementLocked = true;
        }

        Image dim = UiKit.Solid(root, "Dim", UiKit.Hex("#F2071019"));
        UiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        RectTransform frame = UiLayout.Frame(root);
        content = UiKit.New(frame, "Content");
        UiKit.Stretch(content);

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

        TextMeshProUGUI heading = UiKit.Text(content, "FIREWALL DUTY  //  " + session.MissionTitle.ToUpperInvariant() + "  //  QUESTIONING", 12f, UiKit.Hex("#58DFFF"),
                                             FontStyles.Bold, TextAlignmentOptions.Left);
        UiLayout.TopLeft(heading.rectTransform, 100f, 10f, 600f, 18f);

        timeText = UiKit.Text(content, session.FormatTime(), 14f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Right);
        UiLayout.TopRight(timeText.rectTransform, 100f, 9f, 80f, 20f);

        // His answer, in a window.
        Image titleBar = UiKit.Solid(content, "WindowTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 100f, 34f, 714f, 24f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI title = UiKit.Text(titleBar.transform, "Questioning  -  " + speaker, 11f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(title.rectTransform);

        Image body = UiKit.Solid(content, "WindowBody", UiKit.Hex("#0F2236"));
        UiLayout.TopLeft(body.rectTransform, 100f, 58f, 714f, 96f);

        TextMeshProUGUI who = UiKit.Text(body.transform, speaker.ToUpperInvariant(), 11f, UiKit.Hex("#FFD600"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(who.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(400f, 16f));

        TextMeshProUGUI said = UiKit.Text(body.transform, answer, 14f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Place(said.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -28f), new Vector2(680f, 62f));

        TextMeshProUGUI question = UiKit.Text(content, "What do you ask?  Your TABLET lists what to look for in Social Engineering. A question that finds nothing costs time.", 11f,
                                              UiKit.Hex("#55DFFF"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiLayout.TopLeft(question.rectTransform, 100f, 160f, 714f, 18f);

        // The questions, two columns.
        for (int i = 0; i < questions.Length; i++)
        {
            bool asked = isAsked(i);
            int index = i;

            Color tint = asked ? new Color(0.55f, 0.6f, 0.65f) : Color.white;
            Button button = UiKit.Btn(content, "Question" + i, UiKit.Shape("trivia_answer"), tint, (asked ? "(asked)  " : "") + questions[i].text, 11f, Color.white, false, -1f, -1f);
            UiLayout.TopLeft((RectTransform)button.transform, 100f + (i % 2) * 360f, 182f + (i / 2) * 32f, 354f, 28f);
            button.interactable = !asked && !finishing;
            button.onClick.AddListener(() => Ask(index));
        }

        int rows = (questions.Length + 1) / 2;
        float after = 182f + rows * 32f + 2f;

        if (!string.IsNullOrEmpty(note))
        {
            TextMeshProUGUI feedback = UiKit.Text(content, note, 11f, UiKit.Hex(noteColor), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiLayout.TopLeft(feedback.rectTransform, 100f, after, 714f, 32f);
        }

        Button tablet = UiKit.Btn(content, "Tablet", UiKit.Shape("briefing_panel_bg"), Color.white, "TABLET", 11f, UiKit.Hex("#F2D58A"), true, -1f, -1f);
        UiLayout.TopLeft((RectTransform)tablet.transform, 536f, 372f, 130f, 30f);
        tablet.onClick.AddListener(TabletOverlay.Open);

        Button close = UiKit.Btn(content, "Close", UiKit.Shape("briefing_panel_bg"), Color.white, "CLOSE", 11f, UiKit.Hex("#F2D58A"), true, -1f, -1f);
        UiLayout.TopLeft((RectTransform)close.transform, 674f, 372f, 140f, 30f);
        close.onClick.AddListener(Close);
    }

    private void Ask(int index)
    {
        if (finishing)
            return;

        InterviewOutcome outcome = ask(index);
        answer = outcome.answer;
        GameSounds.Talk();
        note = outcome.note;
        noteColor = outcome.error ? "#FF5A5A" : outcome.good ? "#5BE08A" : "#FFD75E";

        if (outcome.finished)
        {
            finishing = true;
            StartCoroutine(CloseSoon());
        }

        Rebuild();
    }

    // A moment to read the last answer, then the window closes.
    private IEnumerator CloseSoon()
    {
        yield return new WaitForSecondsRealtime(2.2f);
        Close();
    }
}

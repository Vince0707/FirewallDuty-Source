using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The player's computer in an email mission: the objectives on the left, the email on the right, and below it what to do next
// (find the clues, say whether it is legitimate, choose the response). What has been done is kept by MissionSession (its
// objectives), so closing and reopening the computer loses nothing. Designed on the same 914 x 411 frame as the menu screens.
public class EmailInvestigationUI : MonoBehaviour
{
    private const int VerdictObjective = 4;
    private const int ResponseObjective = 5;

    public static bool IsOpen { get; private set; }
    public static bool EverOpened { get; private set; }   // the hint changes once the computer has been used

    public static void ResetState()
    {
        EverOpened = false;
        IsOpen = false;
    }

    private EmailScenario scenario;
    private MissionSession session;
    private RectTransform frame;
    private RectTransform dynamicRoot;
    private TMP_Text timeText;
    private string message = "";
    private string messageColor = "#FFD75E";   // red for a wrong choice or a wasted tap, green for a right one, yellow for anything else
    private const string Red = "#FF5A5A", Green = "#5BE08A", Yellow = "#FFD75E";
    private bool finishing;
    private PlayerMobileController lockedPlayer;
    private bool previousLock;
    private int[] verdictOrder;
    private int[] responseOrder;
    private int[] liveSlots;       // Live: the record in each row (index into the cards, -1 = empty)
    private int liveNext;          // Live: the row that receives the next record
    private int liveNewest = -1;   // Live: the row that received the last one
    private int liveCursor;        // Live: where we are in the stream
    private float liveTimer;

    public static void Open(EmailScenario scenario)
    {
        if (IsOpen || MissionSession.Instance == null)
            return;

        GameSounds.Laptop();
        RectTransform root = MissionOverlay.CreateCanvas("EmailInvestigation", 90);
        EmailInvestigationUI ui = root.gameObject.AddComponent<EmailInvestigationUI>();
        ui.Begin(root, scenario);
    }

    private void Begin(RectTransform root, EmailScenario scenario)
    {
        this.scenario = scenario;
        session = MissionSession.Instance;
        IsOpen = true;
        EverOpened = true;

        lockedPlayer = FindAnyObjectByType<PlayerMobileController>();
        if (lockedPlayer != null)
        {
            previousLock = lockedPlayer.MovementLocked;   // already locked when seated: give it back as it was
            lockedPlayer.MovementLocked = true;
        }

        // The responses are shown in a different order each time.
        responseOrder = new int[scenario.responseOptions.Length];
        for (int i = 0; i < responseOrder.Length; i++)
            responseOrder[i] = i;
        for (int i = responseOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int swap = responseOrder[i];
            responseOrder[i] = responseOrder[j];
            responseOrder[j] = swap;
        }

        verdictOrder = new int[scenario.verdictOptions.Length];
        for (int i = 0; i < verdictOrder.Length; i++)
            verdictOrder[i] = i;
        for (int i = verdictOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int swap = verdictOrder[i];
            verdictOrder[i] = verdictOrder[j];
            verdictOrder[j] = swap;
        }

        Image dim = UiKit.Solid(root, "Dim", UiKit.Hex("#F2071019"));
        UiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        frame = UiLayout.Frame(root);
        dynamicRoot = UiKit.New(frame, "Content");
        UiKit.Stretch(dynamicRoot);

        if (scenario.analysis == AnalysisKind.Live)
            StartLive();

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

        // The stream goes on until all the evidence is blocked.
        if (scenario.analysis == AnalysisKind.Live && !finishing && !CluesAllFound())
        {
            liveTimer += Time.unscaledDeltaTime;
            if (liveTimer >= Mathf.Max(0.5f, scenario.liveInterval))
            {
                liveTimer = 0f;
                AddLiveEvent();
                Rebuild();
            }
        }
    }

    private void Close()
    {
        Destroy(gameObject);
    }

    // ---- Drawing ----------------------------------------------------------------------------------------------

    private void Rebuild()
    {
        for (int i = dynamicRoot.childCount - 1; i >= 0; i--)
            Destroy(dynamicRoot.GetChild(i).gameObject);

        BuildHeader();
        BuildObjectives();

        if (scenario.analysis == AnalysisKind.Board)
            BuildBoard();
        else if (scenario.analysis == AnalysisKind.Table)
            BuildTable();
        else if (scenario.analysis == AnalysisKind.Summary)
            BuildSummary();
        else if (scenario.analysis == AnalysisKind.Live)
            BuildLive();
        else if (scenario.analysis == AnalysisKind.Filter)
            BuildFilter();
        else if (scenario.analysis == AnalysisKind.Compare)
            BuildCompare();
        else
            BuildEmail();

        BuildStage();
    }

    private void BuildHeader()
    {
        TextMeshProUGUI title = UiKit.Text(dynamicRoot, "FIREWALL DUTY  //  " + session.MissionTitle.ToUpperInvariant(), 12f, UiKit.Hex("#58DFFF"),
                                           FontStyles.Bold, TextAlignmentOptions.Left);
        UiLayout.TopLeft(title.rectTransform, 24f, 8f, 600f, 18f);

        timeText = UiKit.Text(dynamicRoot, session.FormatTime(), 14f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Right);
        UiLayout.TopRight(timeText.rectTransform, 200f, 7f, 80f, 20f);

        // The tablet can be opened from here (the one on the game screen is behind this screen).
        Button tablet = UiKit.Btn(dynamicRoot, "Tablet", UiKit.Shape("briefing_panel_bg"), Color.white, "TABLET", 11f, UiKit.Hex("#F2D58A"), true, -1f, -1f);
        UiLayout.TopRight((RectTransform)tablet.transform, 108f, 5f, 80f, 24f);
        tablet.onClick.AddListener(TabletOverlay.Open);

        Button close = UiKit.Btn(dynamicRoot, "Close", UiKit.Shape("briefing_panel_bg"), Color.white, "CLOSE", 11f, UiKit.Hex("#F2D58A"), true, -1f, -1f);
        UiLayout.TopRight((RectTransform)close.transform, 20f, 5f, 80f, 24f);
        close.onClick.AddListener(Close);
    }

    // Left column: the six objectives of this mission, ticked as they are done.
    private void BuildObjectives()
    {
        Image panel = UiKit.Pic(dynamicRoot, "ObjectivesPanel", UiKit.Shape("briefing_panel_bg"));
        UiLayout.TopLeft(panel.rectTransform, 20f, 34f, 225f, 357f);

        TextMeshProUGUI heading = UiKit.Text(panel.transform, "MISSION OBJECTIVES", 11f, UiKit.Hex("#55DFFF"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(200f, 16f));

        string[] objectives = MissionData.Objectives(session.ThreatType);
        for (int i = 0; i < objectives.Length; i++)
        {
            bool done = session.IsObjectiveDone(i);
            float y = -34f - i * 52f;

            Image badge = UiKit.Pic(panel.transform, "Badge" + i, UiKit.Shape("profile_avatar_bg"), done ? UiKit.Hex("#35D07F") : UiKit.Hex("#35536A"));
            badge.type = Image.Type.Simple;
            UiKit.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, y), new Vector2(20f, 20f));
            UiKit.Stretch(UiKit.Text(badge.transform, done ? "OK" : (i + 1).ToString(), 8f, done ? Color.black : Color.white, FontStyles.Bold).rectTransform);

            TextMeshProUGUI label = UiKit.Text(panel.transform, objectives[i], 11f, done ? UiKit.Hex("#8BE9B2") : UiKit.Hex("#E4F6FF"),
                                               FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y + 2f), new Vector2(172f, 44f));
        }
    }

    // A third kind: a table of records (who did what, when), to compare with the rules shown above it.
    private void BuildTable()
    {
        Image titleBar = UiKit.Solid(dynamicRoot, "TableTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 260f, 34f, 634f, 22f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI tableTitle = UiKit.Text(titleBar.transform, scenario.documentTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(tableTitle.rectTransform);

        // The rules of the file.
        Image rules = UiKit.Solid(dynamicRoot, "Rules", UiKit.Hex("#17334A"));
        UiLayout.TopLeft(rules.rectTransform, 260f, 58f, 634f, 46f);

        TextMeshProUGUI rulesHeading = UiKit.Text(rules.transform, scenario.rosterTitle, 10f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(rulesHeading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -4f), new Vector2(610f, 14f));

        for (int i = 0; scenario.rosterLines != null && i < scenario.rosterLines.Length && i < 2; i++)
        {
            TextMeshProUGUI rulesLine = UiKit.Text(rules.transform, scenario.rosterLines[i], 10f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            rulesLine.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Place(rulesLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -18f - i * 13f), new Vector2(614f, 13f));
        }

        // The table: a header and one row for each record.
        float[] widths = scenario.tableWidths != null ? scenario.tableWidths : new[] { 105f, 100f, 85f, 125f, 135f, 84f };   // the columns, then the flag
        Image header = UiKit.Solid(dynamicRoot, "Header", UiKit.Hex("#2A4A66"));
        UiLayout.TopLeft(header.rectTransform, 260f, 108f, 634f, 20f);
        FillRow(header.rectTransform, scenario.tableHeader, widths, UiKit.Hex("#FFD75E"), FontStyles.Bold, false);

        for (int i = 0; i < scenario.cards.Count; i++)
            BuildTableRow(scenario.cards[i], 130f + i * 25f, widths, i % 2 == 0);
    }

    private void BuildTableRow(EmailScenario.Card record, float y, float[] widths, bool shaded)
    {
        bool found = record.objective >= 0 && session.IsObjectiveDone(record.objective);

        Color background = found ? UiKit.Hex("#66FF3030") : shaded ? UiKit.Hex("#1F3D57") : UiKit.Hex("#183650");
        Image row = UiKit.Solid(dynamicRoot, "Row", background);
        row.raycastTarget = true;
        UiLayout.TopLeft(row.rectTransform, 260f, y, 634f, 23f);

        Button button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => TapItem(record.objective, record.callout, record.hint));

        FillRow(row.rectTransform, record.fields, widths, found ? UiKit.Hex("#FFB4B4") : Color.white, FontStyles.Normal, found);
    }

    // The cells of a row of the table; a found row gets a red flag in the last column.
    private void FillRow(RectTransform row, string[] cells, float[] widths, Color color, FontStyles style, bool flagged)
    {
        float x = 6f;

        for (int i = 0; cells != null && i < cells.Length && i < widths.Length; i++)
        {
            TextMeshProUGUI cell = UiKit.Text(row, cells[i], 11.5f, color, style, TextAlignmentOptions.Left);
            cell.textWrappingMode = TextWrappingModes.NoWrap;
            cell.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Place(cell.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(widths[i] - 4f, 20f));
            x += widths[i];
        }

        if (flagged)
        {
            Image flag = UiKit.Pic(row, "Flag", UiKit.Shape("example_callout"));
            UiKit.Place(flag.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(68f, 16f));
            TextMeshProUGUI mark = UiKit.Text(flag.transform, "FLAGGED", 9f, Color.white, FontStyles.Bold);
            UiKit.Stretch(mark.rectTransform);
        }
    }

    // ---- Live monitor (AnalysisKind.Live) ---------------------------------------------------------------------------------------

    private const int LiveSlotCount = 6;

    private void StartLive()
    {
        liveSlots = new int[LiveSlotCount];
        for (int i = 0; i < liveSlots.Length; i++)
            liveSlots[i] = -1;

        // Start in a different place of the stream each time, so the evidence does not always come in the same order.
        liveCursor = scenario.cards.Count > 0 ? Random.Range(0, scenario.cards.Count) : 0;
        liveNext = 0;

        for (int i = 0; i < LiveSlotCount; i++)
            AddLiveEvent();
    }

    private bool CluesAllFound()
    {
        for (int i = 0; i < scenario.clueCount; i++)
        {
            if (!session.IsObjectiveDone(i))
                return false;
        }

        return true;
    }

    // The next record of the stream goes into the oldest slot (a ring), so a row never moves under the player's finger. Evidence that was
    // already blocked is not shown again.
    private void AddLiveEvent()
    {
        int count = scenario.cards.Count;
        if (count == 0 || liveSlots == null)
            return;

        for (int tries = 0; tries < count; tries++)
        {
            int index = liveCursor % count;
            liveCursor++;

            EmailScenario.Card card = scenario.cards[index];
            if (card.objective >= 0 && session.IsObjectiveDone(card.objective))
                continue;

            liveSlots[liveNext] = index;
            liveNewest = liveNext;
            liveNext = (liveNext + 1) % liveSlots.Length;
            return;
        }
    }

    // The account's profile on top, then the stream of logins: the player blocks the ones that match the evidence on the tablet.
    private void BuildLive()
    {
        Image titleBar = UiKit.Solid(dynamicRoot, "LiveTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 260f, 34f, 634f, 22f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI liveTitle = UiKit.Text(titleBar.transform, scenario.documentTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(liveTitle.rectTransform);

        Image roster = UiKit.Solid(dynamicRoot, "Roster", UiKit.Hex("#17334A"));
        UiLayout.TopLeft(roster.rectTransform, 260f, 58f, 634f, 46f);

        TextMeshProUGUI rosterHeading = UiKit.Text(roster.transform, scenario.rosterTitle, 10f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(rosterHeading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -4f), new Vector2(610f, 14f));

        for (int i = 0; scenario.rosterLines != null && i < scenario.rosterLines.Length && i < 2; i++)
        {
            TextMeshProUGUI rosterLine = UiKit.Text(roster.transform, scenario.rosterLines[i], 10f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            rosterLine.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Place(rosterLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -18f - i * 13f), new Vector2(614f, 13f));
        }

        // The stream: six rows that fill up in turn.
        for (int slot = 0; slot < LiveSlotCount; slot++)
        {
            float y = 108f + slot * 29f;
            int index = liveSlots != null ? liveSlots[slot] : -1;

            Image row = UiKit.Solid(dynamicRoot, "Row" + slot, slot % 2 == 0 ? UiKit.Hex("#102A40") : UiKit.Hex("#143049"));
            UiLayout.TopLeft(row.rectTransform, 260f, y, 634f, 27f);

            if (index < 0)
                continue;

            EmailScenario.Card card = scenario.cards[index];
            bool blocked = card.objective >= 0 && session.IsObjectiveDone(card.objective);
            if (blocked)
                row.color = UiKit.Hex("#8E2530");
            else if (slot == liveNewest)
                row.color = UiKit.Hex("#1D4A6E");   // the one that has just arrived

            float[] columns = { 10f, 120f, 275f };
            float[] widths = { 105f, 150f, 175f };
            for (int i = 0; i < 3 && i < card.fields.Length; i++)
            {
                TextMeshProUGUI field = UiKit.Text(row.transform, card.fields[i], 11.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
                field.textWrappingMode = TextWrappingModes.NoWrap;
                UiKit.Place(field.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(columns[i], 0f), new Vector2(widths[i], 20f));
            }

            if (blocked)
            {
                Image callout = UiKit.Pic(row.transform, "Callout", UiKit.Shape("example_callout"));
                UiKit.Place(callout.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(455f, 0f), new Vector2(170f, 18f));
                TextMeshProUGUI label = UiKit.Text(callout.transform, "BLOCKED: " + card.callout, 8.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                UiKit.Stretch(label.rectTransform, 3f, 0f, 3f, 0f);
            }
            else
            {
                Button block = UiKit.Btn(row.transform, "Block", UiKit.Shape("briefing_panel_bg"), new Color(1f, 0.55f, 0.55f), "BLOCK", 10.5f, Color.white, true, -1f, -1f);
                UiKit.Place((RectTransform)block.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(455f, 0f), new Vector2(84f, 23f));
                block.onClick.AddListener(() => TapItem(card.objective, card.callout, card.hint));
            }
        }
    }

    // ---- Spam filter (AnalysisKind.Filter) --------------------------------------------------------------------------------------

    // The inbox is flooded and the player sets up the spam filter: six rules, each a switch. The cards are the rules (title = the rule, fields = what
    // it catches). The three rules that match the tablet's evidence work when switched ON (the inbox empties as they do); the others would block real
    // mail, so they cost time and switch themselves off again.
    private void BuildFilter()
    {
        Image titleBar = UiKit.Solid(dynamicRoot, "FilterTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 260f, 34f, 634f, 22f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI filterTitle = UiKit.Text(titleBar.transform, scenario.documentTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(filterTitle.rectTransform);

        // What the filter does to the inbox so far.
        int blocked = 0;
        foreach (EmailScenario.Card card in scenario.cards)
        {
            if (card.objective >= 0 && session.IsObjectiveDone(card.objective))
                blocked += scenario.filterPerRule;
        }

        Image status = UiKit.Solid(dynamicRoot, "FilterStatus", UiKit.Hex("#17334A"));
        UiLayout.TopLeft(status.rectTransform, 260f, 58f, 634f, 40f);

        TextMeshProUGUI statusTitle = UiKit.Text(status.transform, scenario.rosterTitle, 10f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(statusTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -4f), new Vector2(610f, 14f));

        int left = Mathf.Max(0, scenario.filterTotal - blocked);
        TextMeshProUGUI counts = UiKit.Text(status.transform, "INBOX: " + scenario.filterTotal + " new messages      BLOCKED: " + blocked + "      STILL IN THE INBOX: " + left,
                                            12f, left == 0 || blocked == scenario.filterTotal ? UiKit.Hex("#8BE9B2") : Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        counts.textWrappingMode = TextWrappingModes.NoWrap;
        UiKit.Place(counts.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -21f), new Vector2(614f, 16f));

        // The rules.
        for (int i = 0; i < scenario.cards.Count; i++)
        {
            EmailScenario.Card card = scenario.cards[i];
            bool on = card.objective >= 0 && session.IsObjectiveDone(card.objective);
            float y = 102f + i * 30f;

            Image row = UiKit.Solid(dynamicRoot, "Rule" + i, on ? UiKit.Hex("#14503A") : (i % 2 == 0 ? UiKit.Hex("#102A40") : UiKit.Hex("#143049")));
            UiLayout.TopLeft(row.rectTransform, 260f, y, 634f, 28f);

            Button toggle = UiKit.Btn(row.transform, "Switch", UiKit.Shape("briefing_panel_bg"), on ? new Color(0.5f, 1f, 0.7f) : new Color(0.75f, 0.78f, 0.82f),
                                      on ? "ON" : "OFF", 10.5f, on ? Color.black : Color.white, true, -1f, -1f);
            UiKit.Place((RectTransform)toggle.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(54f, 22f));
            toggle.onClick.AddListener(() => TapItem(card.objective, card.callout, card.hint));

            TextMeshProUGUI rule = UiKit.Text(row.transform, card.title, 11f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            rule.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Place(rule.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 5f), new Vector2(556f, 15f));

            if (card.fields != null && card.fields.Length > 0)
            {
                TextMeshProUGUI catches = UiKit.Text(row.transform, card.fields[0], 9.5f, UiKit.Hex("#9FB4C7"), FontStyles.Normal, TextAlignmentOptions.Left);
                catches.textWrappingMode = TextWrappingModes.NoWrap;
                UiKit.Place(catches.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, -7f), new Vector2(556f, 13f));
            }

            if (on)
            {
                Image callout = UiKit.Pic(row.transform, "Callout", UiKit.Shape("example_callout"));
                UiKit.Place(callout.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(150f, 16f));
                TextMeshProUGUI label = UiKit.Text(callout.transform, card.callout, 8.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                UiKit.Stretch(label.rectTransform, 3f, 0f, 3f, 0f);
            }
        }
    }

    // ---- Compare (AnalysisKind.Compare) -----------------------------------------------------------------------------------------

    // Two emails side by side: on the left the bank's REAL notice, on the right the email the customers received. They are the same except for three
    // differences, which are exactly the three pieces of evidence on the tablet. The player taps the lines of the received email that differ. Lines are
    // EmailScenario.Line: text = the received version, reference = the real version (null: the same in both).
    private void BuildCompare()
    {
        Image leftBar = UiKit.Solid(dynamicRoot, "RealTitle", UiKit.Hex("#2F6F4F"));
        UiLayout.TopLeft(leftBar.rectTransform, 260f, 34f, 311f, 22f);
        TextMeshProUGUI leftTitle = UiKit.Text(leftBar.transform, scenario.rosterTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(leftTitle.rectTransform);

        Image rightBar = UiKit.Solid(dynamicRoot, "ReceivedTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(rightBar.rectTransform, 583f, 34f, 311f, 22f);
        TextMeshProUGUI rightTitle = UiKit.Text(rightBar.transform, scenario.documentTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(rightTitle.rectTransform);

        Image leftBody = UiKit.Solid(dynamicRoot, "RealBody", UiKit.Hex("#E6F2EA"));
        UiLayout.TopLeft(leftBody.rectTransform, 260f, 56f, 311f, 226f);

        Image rightBody = UiKit.Solid(dynamicRoot, "ReceivedBody", Color.white);
        UiLayout.TopLeft(rightBody.rectTransform, 583f, 56f, 311f, 226f);

        float y = 8f;
        foreach (EmailScenario.Line line in scenario.lines)
        {
            if (line.spaceBefore)
                y += 6f;

            bool differs = line.reference != null;
            bool found = line.objective >= 0 && session.IsObjectiveDone(line.objective);

            // The real version.
            TextMeshProUGUI real = UiKit.Text(leftBody.transform, differs ? line.reference : line.text, 10f, differs ? UiKit.Hex("#1B5E20") : UiKit.Hex("#222222"),
                                              differs ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Left);
            real.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Place(real.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -y), new Vector2(298f, 22f));

            // The received version: a row to tap.
            Image row = UiKit.Pic(rightBody.transform, "Line", UiKit.Shape("briefing_panel_bg"), found ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 1f, 1f, 0f), true);
            UiKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -(y - 1f)), new Vector2(307f, 22f));

            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row;
            button.transition = Selectable.Transition.None;
            EmailScenario.Line captured = line;
            button.onClick.AddListener(() => Tap(captured));

            TextMeshProUGUI received = UiKit.Text(row.transform, line.text, 10f, found ? Color.white : UiKit.Hex("#222222"), FontStyles.Normal, TextAlignmentOptions.Left);
            received.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Place(received.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(found ? 195f : 298f, 20f));

            if (found)
            {
                Image callout = UiKit.Pic(row.transform, "Callout", UiKit.Shape("example_callout"));
                UiKit.Place(callout.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(104f, 19f));
                TextMeshProUGUI label = UiKit.Text(callout.transform, line.callout, 7.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                UiKit.Stretch(label.rectTransform, 3f, 0f, 3f, 0f);
            }

            y += 26f;
        }
    }

    // After an Inspect step: the evidence the player found by walking around the scene, written up as a short incident report. Nothing to tap here:
    // the player only names the threat and chooses the response below it.
    private void BuildSummary()
    {
        Image titleBar = UiKit.Solid(dynamicRoot, "ReportTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 260f, 34f, 634f, 22f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI reportTitle = UiKit.Text(titleBar.transform, scenario.documentTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(reportTitle.rectTransform);

        Image body = UiKit.Solid(dynamicRoot, "ReportBody", UiKit.Hex("#0F2236"));
        UiLayout.TopLeft(body.rectTransform, 260f, 56f, 634f, 226f);

        TextMeshProUGUI heading = UiKit.Text(body.transform, scenario.summaryHeading, 11f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(600f, 16f));

        // The evidence of the mission, found by inspecting places or by asking questions.
        List<KeyValuePair<int, string[]>> evidence = new List<KeyValuePair<int, string[]>>();
        foreach (EmailScenario.Station station in scenario.stations)
        {
            if (station.objective >= 0)
                evidence.Add(new KeyValuePair<int, string[]>(station.objective, new[] { station.callout, station.summary }));
        }

        foreach (EmailScenario.Question q in scenario.questions)
        {
            if (q.objective >= 0)
                evidence.Add(new KeyValuePair<int, string[]>(q.objective, new[] { q.callout, q.summary }));
        }

        int row = 0;
        foreach (KeyValuePair<int, string[]> item in evidence)
        {
            bool found = session.IsObjectiveDone(item.Key);
            float y = -36f - row * 58f;

            if (found)
            {
                // The red label of the example screens, then what was seen.
                Image callout = UiKit.Pic(body.transform, "Callout", UiKit.Shape("example_callout"));
                UiKit.Place(callout.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, y), new Vector2(190f, 18f));
                TextMeshProUGUI label = UiKit.Text(callout.transform, item.Value[0], 10.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                UiKit.Stretch(label.rectTransform, 4f, 0f, 4f, 0f);

                TextMeshProUGUI seen = UiKit.Text(body.transform, item.Value[1], 12.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                UiKit.Place(seen.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, y - 22f), new Vector2(600f, 30f));
            }
            else
            {
                TextMeshProUGUI missing = UiKit.Text(body.transform, "?   Evidence not found yet.", 12f, UiKit.Hex("#9FB4C7"), FontStyles.Normal, TextAlignmentOptions.TopLeft);
                UiKit.Place(missing.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, y), new Vector2(600f, 30f));
            }

            row++;
        }
    }

    // The other kind of investigation: the logins of the access log as cards, to compare with the authorized profile above them.
    private void BuildBoard()
    {
        Image titleBar = UiKit.Solid(dynamicRoot, "BoardTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 260f, 34f, 634f, 22f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI boardTitle = UiKit.Text(titleBar.transform, scenario.documentTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(boardTitle.rectTransform);

        // The profile of the account: what a normal login looks like.
        Image roster = UiKit.Solid(dynamicRoot, "Roster", UiKit.Hex("#17334A"));
        UiLayout.TopLeft(roster.rectTransform, 260f, 58f, 634f, 46f);

        TextMeshProUGUI rosterHeading = UiKit.Text(roster.transform, scenario.rosterTitle, 10f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(rosterHeading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -4f), new Vector2(610f, 14f));

        for (int i = 0; scenario.rosterLines != null && i < scenario.rosterLines.Length && i < 2; i++)
        {
            TextMeshProUGUI rosterLine = UiKit.Text(roster.transform, scenario.rosterLines[i], 10f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            rosterLine.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Place(rosterLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -18f - i * 13f), new Vector2(614f, 13f));
        }

        // The login cards, three to a row.
        for (int i = 0; i < scenario.cards.Count; i++)
        {
            float x = 260f + (i % 3) * (207f + 6.5f);
            float y = 110f + (i / 3) * (84f + 6f);
            BuildCard(scenario.cards[i], x, y);
        }
    }

    private void BuildCard(EmailScenario.Card card, float x, float y)
    {
        bool found = card.objective >= 0 && session.IsObjectiveDone(card.objective);

        Image background = UiKit.Pic(dynamicRoot, "Card", UiKit.Shape("briefing_panel_bg"), found ? new Color(1f, 0.55f, 0.55f) : Color.white, true);
        UiLayout.TopLeft(background.rectTransform, x, y, 207f, 84f);

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => TapItem(card.objective, card.callout, card.hint));

        TextMeshProUGUI title = UiKit.Text(background.transform, card.title, 11.5f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(80f, 16f));

        for (int i = 0; i < card.fields.Length && i < 4; i++)
        {
            TextMeshProUGUI field = UiKit.Text(background.transform, card.fields[i], 11.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            field.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Place(field.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -26f - i * 17f), new Vector2(190f, 15f));
        }

        if (found)
        {
            // The red label of the example screens, in the corner of the card.
            Image callout = UiKit.Pic(background.transform, "Callout", UiKit.Shape("example_callout"));
            UiKit.Place(callout.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -5f), new Vector2(124f, 15f));
            TextMeshProUGUI label = UiKit.Text(callout.transform, card.callout, 8.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Stretch(label.rectTransform, 3f, 0f, 3f, 0f);
        }
    }

    // Right: the email. Each line can be tapped; the ones that matter turn red and get a label.
    private void BuildEmail()
    {
        Image titleBar = UiKit.Solid(dynamicRoot, "MailTitle", UiKit.Hex("#707070"));
        UiLayout.TopLeft(titleBar.rectTransform, 260f, 34f, 634f, 22f);
        for (int i = 0; i < 3; i++)
        {
            Image dot = UiKit.Pic(titleBar.transform, "Dot" + i, UiKit.Shape("example_dot"), Color.white);
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 11f, 0f), new Vector2(7f, 7f));
        }

        TextMeshProUGUI mailTitle = UiKit.Text(titleBar.transform, scenario.documentTitle, 10f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        UiKit.Stretch(mailTitle.rectTransform);

        Image body = UiKit.Solid(dynamicRoot, "MailBody", scenario.bubbles ? UiKit.Hex("#0F2236") : Color.white);
        UiLayout.TopLeft(body.rectTransform, 260f, 56f, 634f, 226f);

        float y = 8f;
        foreach (EmailScenario.Line line in scenario.lines)
        {
            if (line.spaceBefore)
                y += 6f;

            if (scenario.bubbles)
            {
                BuildBubble(body.rectTransform, line, y);
                y += 28f;
            }
            else
            {
                BuildLine(body.rectTransform, line, y);
                y += 22f;
            }
        }
    }

    // The conversation style: what the other side says on the left, the player's side on the right, the call info across the top.
    private void BuildBubble(RectTransform body, EmailScenario.Line line, float y)
    {
        bool found = line.objective >= 0 && session.IsObjectiveDone(line.objective);

        float width = line.header ? 614f : line.mine ? 300f : 470f;
        float x = line.mine ? 634f - 300f - 10f : 10f;
        Color color = found ? UiKit.Hex("#B3262E") : line.mine ? UiKit.Hex("#1565C0") : line.header ? UiKit.Hex("#3A4A5C") : UiKit.Hex("#2A3F55");

        Image bubble = UiKit.Solid(body, "Bubble", color);
        bubble.raycastTarget = true;
        UiLayout.TopLeft(bubble.rectTransform, x, y, width, 22f);

        Button button = bubble.gameObject.AddComponent<Button>();
        button.targetGraphic = bubble;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Tap(line));

        TextMeshProUGUI text = UiKit.Text(bubble.transform, line.text, 11.5f, Color.white, FontStyles.Normal, line.header ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        UiKit.Stretch(text.rectTransform, 8f, 0f, 8f, 0f);

        if (found && !line.header)
        {
            Image callout = UiKit.Pic(body, "Callout", UiKit.Shape("example_callout"));
            UiLayout.TopLeft(callout.rectTransform, x + width + 6f, y + 2f, 634f - (x + width + 6f) - 6f, 18f);
            TextMeshProUGUI label = UiKit.Text(callout.transform, line.callout, 9f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.Stretch(label.rectTransform, 3f, 0f, 3f, 0f);
        }
    }

    private void BuildLine(RectTransform body, EmailScenario.Line line, float y)
    {
        bool found = line.objective >= 0 && session.IsObjectiveDone(line.objective);

        Image row = UiKit.Solid(body, "Line", found ? UiKit.Hex("#33FF3030") : new Color(1f, 1f, 1f, 0f));
        row.raycastTarget = true;
        UiLayout.TopLeft(row.rectTransform, 6f, y, 622f, 20f);

        Button button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = row;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Tap(line));

        Color textColor = line.link ? UiKit.Hex("#1976D2") : Color.black;
        TextMeshProUGUI text = UiKit.Text(row.transform, line.text, 12.5f, textColor, FontStyles.Normal, TextAlignmentOptions.Left);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        UiKit.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(line.objective >= 0 && found ? 380f : 610f, 20f));

        if (found)
        {
            // The arrow and the red label of the example screens.
            TextMeshProUGUI arrow = UiKit.Text(row.transform, "<", 16f, UiKit.Hex("#FF3030"), FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Place(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-232f, 0f), new Vector2(18f, 18f));

            Image callout = UiKit.Pic(row.transform, "Callout", UiKit.Shape("example_callout"));
            UiKit.Place(callout.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(222f, 18f));
            TextMeshProUGUI label = UiKit.Text(callout.transform, line.callout, 10.5f, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            UiKit.Stretch(label.rectTransform, 4f, 0f, 4f, 0f);
        }
    }

    private void Tap(EmailScenario.Line line)
    {
        TapItem(line.objective, line.callout, line.hint);
    }

    // Something in the investigation was tapped: a line, or a card. A clue completes its objective; anything else costs time, so
    // guessing is not free.
    private void TapItem(int objective, string callout, string hint)
    {
        if (finishing)
            return;

        if (objective < 0)
        {
            session.AddTimePenalty(scenario.wrongTapPenalty);
            message = (hint ?? "Nothing suspicious here.") + "  (-" + (int)scenario.wrongTapPenalty + " seconds)";
            messageColor = Red;
        }
        else if (!session.IsObjectiveDone(objective))
        {
            session.CompleteObjective(objective);
            message = "Found: " + callout + ".";
            messageColor = Green;

            bool all = true;
            for (int i = 0; i < scenario.clueCount; i++)
                all &= session.IsObjectiveDone(i);

            if (all)
            {
                session.CompleteObjective(scenario.summaryObjective);
                message = "Found: " + callout + ". That is all the evidence.";
                messageColor = Green;
            }
        }
        else
        {
            message = callout + " - already flagged.";
            messageColor = Yellow;
        }

        Rebuild();
    }

    // Below the email: the next thing to do.
    private void BuildStage()
    {
        Image panel = UiKit.Pic(dynamicRoot, "StagePanel", UiKit.Shape("briefing_panel_bg"));
        UiLayout.TopLeft(panel.rectTransform, 260f, 285f, 634f, 106f);

        int found = 0;
        for (int i = 0; i < scenario.clueCount; i++)
        {
            if (session.IsObjectiveDone(i))
                found++;
        }

        bool cluesDone = found >= scenario.clueCount;
        bool verdictDone = session.IsObjectiveDone(VerdictObjective);
        bool responseDone = session.IsObjectiveDone(ResponseObjective) || MissionStory.ResponseChosen;

        string question;
        if (session.IsObjectiveDone(ResponseObjective)) question = "MISSION COMPLETE";
        else if (responseDone) question = "Good choice. Now go and report it: follow the arrow and the hint at the top.";
        else if (verdictDone) question = scenario.responseQuestion;
        else if (cluesDone) question = scenario.verdictQuestion;
        else question = scenario.investigateQuestion + "  (" + found + " / " + scenario.clueCount + " found)";

        TextMeshProUGUI heading = UiKit.Text(panel.transform, question, 12f, UiKit.Hex("#55DFFF"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(610f, 18f));

        if (responseDone)
        {
            // nothing more to choose
        }
        else if (verdictDone)
        {
            // Two columns of response buttons.
            for (int slot = 0; slot < responseOrder.Length; slot++)
            {
                int option = responseOrder[slot];
                Button button = UiKit.Btn(panel.transform, "Response" + slot, UiKit.Shape("trivia_answer"), Color.white,
                                          scenario.responseOptions[option], 10.5f, Color.white, false, -1f, -1f);
                float x = 12f + (slot % 2) * 310f;
                float y = -27f - (slot / 2) * 25f;
                UiKit.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(300f, 22f));
                button.onClick.AddListener(() => ChooseResponse(option));
            }
        }
        else if (cluesDone)
        {
            // The kinds of threat in a grid, four to a row, in a different order each time.
            for (int slot = 0; slot < verdictOrder.Length; slot++)
            {
                int option = verdictOrder[slot];
                Button button = UiKit.Btn(panel.transform, "Verdict" + slot, UiKit.Shape("trivia_answer"), Color.white,
                                          scenario.verdictOptions[option], 10.5f, Color.white, true, -1f, -1f);
                float x = 12f + (slot % 4) * 155f;
                float y = -27f - (slot / 4) * 25f;
                UiKit.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(148f, 22f));
                button.onClick.AddListener(() => ChooseVerdict(option));
            }
        }

        if (!string.IsNullOrEmpty(message))
        {
            TextMeshProUGUI feedback = UiKit.Text(panel.transform, message, 10f, UiKit.Hex(messageColor), FontStyles.Normal, TextAlignmentOptions.BottomLeft);
            UiKit.Place(feedback.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 3f), new Vector2(610f, 26f));
        }
    }

    // ---- Choosing ---------------------------------------------------------------------------------------------

    private void ChooseVerdict(int option)
    {
        if (finishing)
            return;

        if (option == scenario.verdictCorrect)
        {
            session.CompleteObjective(VerdictObjective);
            message = scenario.verdictRight;
            messageColor = Green;
        }
        else
        {
            message = scenario.verdictFeedback != null && option < scenario.verdictFeedback.Length ? scenario.verdictFeedback[option] : scenario.verdictWrong;
            messageColor = Red;
        }

        Rebuild();
    }

    private void ChooseResponse(int option)
    {
        if (finishing)
            return;

        message = scenario.responseFeedback[option];
        messageColor = option == scenario.responseCorrect ? Green : Red;

        if (option != scenario.responseCorrect)
        {
            // A wrong response in the last objective costs experience and points (once per wrong option).
            int lost = session.RegisterWrongResponse(option);
            if (lost > 0)
                message += "  (-" + lost + " EXP and points)";
        }

        if (option == scenario.responseCorrect)
        {
            // The right choice is to report it: the player now has to go and tell an IT expert (the mission controller takes over).
            finishing = true;
            MissionScenarioController.Instance?.OnResponseChosen();
            StartCoroutine(CloseSoon());
        }

        Rebuild();
    }

    // A moment to read the result, then the computer closes so the player can walk to the IT experts.
    private IEnumerator CloseSoon()
    {
        yield return new WaitForSecondsRealtime(2.2f);
        Close();
    }
}

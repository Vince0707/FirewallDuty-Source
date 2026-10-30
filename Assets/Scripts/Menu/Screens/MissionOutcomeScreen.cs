using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Mission Outcome screen (designed from the picture; there is no Android version): what the mission was, the time left,
// the experience earned, and which of the six objectives were completed.
public class MissionOutcomeScreen : MenuScreen
{
    private TMP_Text missionTitle;
    private TMP_Text environment;
    private TMP_Text threat;
    private TMP_Text time;
    private TMP_Text exp;
    private RectTransform objectiveList;
    private Button restartButton;
    private Button returnButton;

    protected override void Build(RectTransform root)
    {
        UiLayout.Backdrop(root, "operations_hub_bg", "#B8041424", 0.55f);
        RectTransform frame = UiLayout.Frame(root);

        Image header = UiKit.Pic(frame, "Header", UiKit.Shape("leaderboard_title_bg"));
        UiLayout.TopCenter(header.rectTransform, 24f, 420f, 48f);
        TextMeshProUGUI headerText = UiKit.Text(header.transform, "MISSION OUTCOME", 21f, Color.white, FontStyles.Bold);
        UiLayout.Spaced(headerText, 0.10f);
        UiKit.Stretch(headerText.rectTransform);

        // Left card: the facts.
        Image left = UiKit.Pic(frame, "Facts", UiKit.Shape("briefing_panel_bg"));
        UiLayout.TopLeft(left.rectTransform, 40f, 92f, 410f, 230f);
        RectTransform facts = UiKit.VBox(left.transform, "Rows", 0f, TextAnchor.UpperLeft, false, false, 16);
        UiKit.Stretch(facts);
        facts.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

        missionTitle = Row(facts, "MISSION TITLE", "#FFD75E", 17f);
        environment = Row(facts, "ENVIRONMENT", "#FFFFFF", 14f);
        threat = Row(facts, "THREAT TYPE", "#FFFFFF", 14f);
        time = Row(facts, "TIME REMAINING", "#FFFFFF", 14f);
        exp = Row(facts, "EXP GAINED", "#FFD75E", 17f);

        // Right card: the objectives.
        Image right = UiKit.Pic(frame, "Objectives", UiKit.Shape("briefing_panel_bg"));
        UiLayout.TopLeft(right.rectTransform, 464f, 92f, 410f, 230f);

        TextMeshProUGUI heading = UiKit.Text(right.transform, "OBJECTIVES", 14f, UiKit.Hex("#55DFFF"), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(300f, 18f));
        Image rule = UiKit.Solid(right.transform, "Rule", UiKit.Hex("#28678A"));
        UiKit.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -38f), new Vector2(378f, 1f));

        objectiveList = UiKit.VBox(right.transform, "List", 8f, TextAnchor.UpperLeft, false, false);
        UiKit.Stretch(objectiveList, 16f, 10f, 16f, 48f);
        objectiveList.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

        returnButton = UiKit.Btn(frame, "Return", UiKit.Shape("briefing_start_button"), Color.white, "RETURN TO OPERATIONS HUB", 15f, Color.white, true, -1f, -1f);
        UiLayout.BottomCenter((RectTransform)returnButton.transform, 22f, 340f, 50f);
        UiLayout.Spaced(returnButton.GetComponentInChildren<TextMeshProUGUI>(), 0.06f);
        returnButton.onClick.AddListener(() =>
        {
            App.ClearHistory();
            App.Show("OperationsHub", null, false);
        });

        // RESTART: only when the time ran out. It plays the same mission again from the start.
        restartButton = UiKit.Btn(frame, "Restart", UiKit.Shape("briefing_start_button"), Color.white, "RESTART MISSION", 15f, Color.white, true, -1f, -1f);
        UiLayout.BottomCenter((RectTransform)restartButton.transform, 22f, 300f, 50f);
        UiLayout.Spaced(restartButton.GetComponentInChildren<TextMeshProUGUI>(), 0.06f);
        restartButton.onClick.AddListener(RestartMission);
        restartButton.gameObject.SetActive(false);
    }

    // When the time ran out there are two buttons side by side (RESTART MISSION and RETURN TO OPERATIONS HUB); otherwise just the return button, in the middle.
    private void ShowButtons(bool timedOut)
    {
        restartButton.gameObject.SetActive(timedOut);

        RectTransform back = (RectTransform)returnButton.transform;
        RectTransform restart = (RectTransform)restartButton.transform;

        if (timedOut)
        {
            UiKit.Place(restart, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-165f, 22f), new Vector2(300f, 50f));
            UiKit.Place(back, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(165f, 22f), new Vector2(300f, 50f));
            returnButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13f;
        }
        else
        {
            UiKit.Place(back, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(340f, 50f));
            returnButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 15f;
        }
    }

    // Plays the same mission again, from the start (the same as START MISSION on its briefing).
    private void RestartMission()
    {
        MissionResult result = MissionFlow.LastResult;
        if (result == null)
            return;

        string scene = MissionData.SceneName(result.environment);
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            App.Toast("The " + result.environment + " scene is not in the build list yet.");
            return;
        }

        MissionRequest request = new MissionRequest
        {
            environment = result.environment,
            sceneNumber = result.sceneNumber,
            missionTitle = result.missionTitle,
            threatType = result.threatType
        };

        MissionFlow.Current = request;
        PlayerSession.SavePendingMission(request);
        SceneManager.LoadScene(scene);
    }

    // A small grey label with the value under it.
    private static TMP_Text Row(RectTransform parent, string label, string valueColor, float valueSize)
    {
        UiKit.Text(parent, label, 10f, UiKit.Hex("#83AFC6"), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        TextMeshProUGUI value = UiKit.Text(parent, "", valueSize, UiKit.Hex(valueColor), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Space(parent, 7f);
        return value;
    }

    public override void OnShow(object arg)
    {
        MissionResult result = MissionFlow.LastResult ?? Sample();

        missionTitle.text = result.missionTitle;
        environment.text = result.environment.ToUpperInvariant();
        threat.text = result.threatType.ToUpperInvariant();
        time.text = Clock(result.timeRemainingSeconds) + "  (out of " + Clock(result.timeLimitSeconds) + ")";
        exp.text = result.alreadyCompleted ? "+0 EXP  (already completed)" : "+" + result.expGained + " EXP";

        ShowButtons(result.timedOut);

        for (int i = objectiveList.childCount - 1; i >= 0; i--)
            Object.Destroy(objectiveList.GetChild(i).gameObject);

        string[] names = MissionData.Objectives(result.threatType);
        for (int i = 0; i < names.Length; i++)
        {
            bool done = result.objectives != null && i < result.objectives.Length && result.objectives[i];
            bool last = i == names.Length - 1;
            AddObjective(names[i], done, last);
        }
    }

    private void AddObjective(string text, bool done, bool last)
    {
        RectTransform row = UiKit.HBox(objectiveList, "Objective", 10f, TextAnchor.MiddleLeft, false, false);

        // Green tick when done (teal for the final response), red cross when not.
        Color color = done ? (last ? UiKit.Hex("#2EE6C8") : UiKit.Hex("#35D07F")) : UiKit.Hex("#E5484D");
        Image badge = UiKit.Pic(row, "Badge", UiKit.Shape("profile_avatar_bg"), color);
        UiKit.Size(badge, 20f, 20f);
        TextMeshProUGUI mark = UiKit.Text(badge.transform, done ? "OK" : "X", 8f, Color.black, FontStyles.Bold);
        UiKit.Stretch(mark.rectTransform);

        TextMeshProUGUI label = UiKit.Text(row, text, 13f, UiKit.Hex("#E4F6FF"), FontStyles.Normal, TextAlignmentOptions.Left);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        UiKit.Size(label, 10f, 20f);
        UiKit.Flexible(label, 1f, 0f);
    }

    private static string Clock(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    // Shown when this screen is opened without a mission having been played.
    private static MissionResult Sample()
    {
        return new MissionResult
        {
            environment = "office",
            sceneNumber = 1,
            missionTitle = "Payroll Information Update",
            threatType = "Phishing Email",
            timeRemainingSeconds = 154,
            timeLimitSeconds = 360,
            expGained = 50,
            objectives = new[] { true, true, false, true, false, true }
        };
    }
}

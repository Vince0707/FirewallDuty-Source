using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// activity_mission_briefing.xml / MissionBriefingActivity.java: mission details, the six objectives, and START MISSION,
// which (new in Unity) remembers the mission for "Continue" and loads the 3D scene.
public class MissionBriefingScreen : MenuScreen
{
    private TMP_Text title;
    private TMP_Text environmentText;
    private TMP_Text threatText;
    private TMP_Text objectivesText;
    private MissionRequest request;

    protected override void Build(RectTransform root)
    {
        UiLayout.Backdrop(root, "operations_hub_bg", "#C9041424", 0.22f);
        RectTransform frame = UiLayout.Frame(root);

        UiLayout.BackButton(frame, 12f, 24f, 120f, 56f, 15f, () => App.Back("SceneSelection"));

        TextMeshProUGUI system = UiKit.Text(frame, "FIREWALL DUTY  //  OPERATIONS", 10f, UiKit.Hex("#58DFFF"), FontStyles.Bold, TextAlignmentOptions.Right);
        UiLayout.Spaced(system, 0.06f);
        UiLayout.TopRight(system.rectTransform, 18f, 44f, 300f, 16f);

        Image header = UiKit.Pic(frame, "Header", UiKit.Shape("leaderboard_title_bg"));
        UiLayout.TopLeft(header.rectTransform, 135f, 88f, UiLayout.DesignWidth - 270f, 48f);
        TextMeshProUGUI headerText = UiKit.Text(header.transform, "MISSION BRIEFING", 21f, Color.white, FontStyles.Bold);
        UiLayout.Spaced(headerText, 0.10f);
        UiKit.Stretch(headerText.rectTransform);

        TextMeshProUGUI subtitle = UiKit.Text(frame, "REVIEW YOUR ASSIGNMENT BEFORE DEPLOYMENT", 10f, UiKit.Hex("#7BBBD6"));
        UiLayout.Spaced(subtitle, 0.05f);
        UiLayout.TopCenter(subtitle.rectTransform, 141f, 500f, 14f);

        // Left panel: details.
        RectTransform details = Panel(frame, 24f, "01  //  MISSION DETAILS");
        RectTransform detailsContent = PanelScroll(details);
        detailsContent.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

        title = UiKit.Text(detailsContent, "", 19f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Space(detailsContent, 8f);
        SmallLabel(detailsContent, "ENVIRONMENT");
        environmentText = UiKit.Text(detailsContent, "", 14f, UiKit.Hex("#E4F6FF"), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Space(detailsContent, 10f);
        SmallLabel(detailsContent, "THREAT CLASSIFICATION");
        threatText = UiKit.Text(detailsContent, "", 14f, UiKit.Hex("#FFD75E"), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Space(detailsContent, 12f);
        Image rule = UiKit.Solid(detailsContent, "Rule", UiKit.Hex("#28678A"));
        UiKit.Size(rule, -1f, 1f);
        UiKit.Space(detailsContent, 10f);
        SmallLabel(detailsContent, "MISSION TIME LIMIT");
        UiKit.Space(detailsContent, 6f);
        Image timeBox = UiKit.Solid(detailsContent, "TimeLimit", UiKit.Hex("#17334A"));
        UiKit.Size(timeBox, -1f, 52f);
        TextMeshProUGUI limit = UiKit.Text(timeBox.transform, "06:00", 22f, UiKit.Hex("#FFD75E"), FontStyles.Bold);
        UiKit.Stretch(limit.rectTransform);

        // Right panel: objectives.
        RectTransform objectives = Panel(frame, 465f, "02  //  MISSION OBJECTIVES");
        RectTransform objectivesContent = PanelScroll(objectives);
        objectivesContent.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
        objectivesText = UiKit.Text(objectivesContent, "", 14f, UiKit.Hex("#E4F6FF"), FontStyles.Normal, TextAlignmentOptions.TopLeft);

        Button start = UiKit.Btn(frame, "Start", UiKit.Shape("briefing_start_button"), Color.white, "START MISSION", 15f, Color.white, true, -1f, -1f);
        UiLayout.BottomCenter((RectTransform)start.transform, 14f, 260f, 50f);
        UiLayout.Spaced(start.GetComponentInChildren<TextMeshProUGUI>(), 0.06f);
        start.onClick.AddListener(StartMission);
    }

    private static void SmallLabel(RectTransform parent, string text)
    {
        UiKit.Text(parent, text, 10f, UiKit.Hex("#83AFC6"), FontStyles.Bold, TextAlignmentOptions.TopLeft);
    }

    // A 425 x 174 panel with its numbered heading and rule.
    private RectTransform Panel(RectTransform frame, float x, string heading)
    {
        Image panel = UiKit.Pic(frame, "Panel", UiKit.Shape("briefing_panel_bg"));
        UiLayout.TopLeft(panel.rectTransform, x, 165f, 425f, 174f);

        TextMeshProUGUI text = UiKit.Text(panel.transform, heading, 13f, UiKit.Hex("#55DFFF"), FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(400f, 16f));

        Image rule = UiKit.Solid(panel.transform, "Rule", UiKit.Hex("#28678A"));
        UiKit.Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -35f), new Vector2(401f, 1f));

        return panel.rectTransform;
    }

    private RectTransform PanelScroll(RectTransform panel)
    {
        RectTransform content = UiKit.Scroll(panel, "Scroll", 0f, 0);
        UiKit.Stretch(UiLayout.ScrollRoot(content), 12f, 12f, 12f, 44f);
        return content;
    }

    public override void OnShow(object arg)
    {
        request = arg as MissionRequest ?? request ?? new MissionRequest { environment = "office", sceneNumber = 1, missionTitle = "Mission", threatType = "Cybersecurity Threat" };

        title.text = "SCENE " + request.sceneNumber + "\n" + request.missionTitle;
        environmentText.text = "ENVIRONMENT: " + request.environment.ToUpperInvariant();
        threatText.text = "THREAT TYPE: " + request.threatType;

        string[] objectives = MissionData.Objectives(request.threatType);
        string text = "";
        for (int i = 0; i < objectives.Length; i++)
            text += (i > 0 ? "\n\n" : "") + (i + 1) + ". " + objectives[i];
        objectivesText.text = text;
    }

    private void StartMission()
    {
        string scene = MissionData.SceneName(request.environment);

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            App.Toast("The " + request.environment + " scene is not in the build list yet.");
            return;
        }

        MissionFlow.Current = request;
        PlayerSession.SavePendingMission(request);
        SceneManager.LoadScene(scene);
    }
}

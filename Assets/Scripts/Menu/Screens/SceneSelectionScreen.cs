using TMPro;
using UnityEngine;
using UnityEngine.UI;

// activity_scene_selection.xml / SceneSelectionActivity.java: the ten missions of the chosen environment, two rows of five.
public class SceneSelectionScreen : MenuScreen
{
    private TMP_Text environmentTitle;
    private RectTransform list;
    private string environment = "office";

    protected override void Build(RectTransform root)
    {
        UiLayout.Backdrop(root, "operations_hub_bg", "#B8041424", 0.38f);
        RectTransform frame = UiLayout.Frame(root);

        // The gold-and-navy header: the environment's name as the title, BACK at the right.
        environmentTitle = MenuStyle.Header(frame, "OFFICE ENVIRONMENT", "MISSION SELECT  //  CHOOSE YOUR OPERATION", () => App.Back("OperationsHub"), out TextMeshProUGUI subtitle);

        list = UiKit.Scroll(frame, "Scroll", 10f, 0);
        UiKit.Stretch(UiLayout.ScrollRoot(list), 20f, 10f, 20f, 76f);
    }

    public override void OnShow(object arg)
    {
        if (arg is string chosen)
            environment = MissionData.IsBank(chosen) ? "bank" : "office";

        environmentTitle.text = MissionData.IsBank(environment) ? "BANK ENVIRONMENT" : "OFFICE ENVIRONMENT";

        for (int i = list.childCount - 1; i >= 0; i--)
            Object.Destroy(list.GetChild(i).gameObject);

        string[] titles = MissionData.IsBank(environment) ? MissionData.BankTitles : MissionData.OfficeTitles;
        string[] threats = MissionData.IsBank(environment) ? MissionData.BankThreats : MissionData.OfficeThreats;

        for (int rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            RectTransform row = UiKit.HBox(list, "Row", 10f, TextAnchor.MiddleCenter, false, false);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            UiKit.Size(row, -1f, 184f);
            UiKit.Flexible(row, 1f, 0f);

            for (int column = 0; column < 5; column++)
            {
                int index = rowIndex * 5 + column;
                if (index < titles.Length)
                    BuildCard(row, index + 1, titles[index], threats[index]);
            }
        }
    }

    private void BuildCard(RectTransform row, int sceneNumber, string missionTitle, string threatType)
    {
        // Each card is coloured for the threat of its mission (phishing blue, malware red, pop-ups amber, spam purple, social engineering
        // teal, unauthorized access indigo) and shows that threat's picture.
        Image card = UiKit.Pic(row, "Mission" + sceneNumber, CartoonArt.ThreatCard(threatType), Color.white, true);
        UiKit.Flexible(card, 1f, 0f);
        UiKit.Size(card, 10f, 184f);

        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        card.gameObject.AddComponent<ButtonFeedback>();

        MissionRequest request = new MissionRequest
        {
            environment = environment,
            sceneNumber = sceneNumber,
            missionTitle = missionTitle,
            threatType = threatType
        };
        bool locked = !PlayerSession.IsSceneUnlocked(environment, sceneNumber);
        button.onClick.AddListener(() =>
        {
            if (locked)
                App.Toast("Finish mission " + (sceneNumber - 1) + " first to unlock this one.");
            else
                Open(request);
        });

        // "MISSION // 01"
        TextMeshProUGUI number = MenuStyle.Text(card.transform, "MISSION // " + sceneNumber.ToString("00"), 11f, Color.white, MenuFonts.Label, TextAlignmentOptions.Center);
        UiLayout.Spaced(number, 0.1f);
        UiKit.Place(number.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(0f, 20f));
        number.rectTransform.anchorMax = new Vector2(1f, 1f);
        number.rectTransform.sizeDelta = new Vector2(0f, 20f);

        // The threat's picture, on a soft glow.
        Image glow = UiKit.Pic(card.transform, "Glow", CartoonArt.Glow(), Color.white);
        glow.type = Image.Type.Simple;
        UiKit.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(64f, 64f));

        Image emblem = UiKit.Pic(card.transform, "Emblem", CartoonArt.ThreatIcon(threatType));
        emblem.type = Image.Type.Simple;
        emblem.preserveAspect = true;
        UiKit.Place(emblem.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(46f, 46f));

        TextMeshProUGUI title = MenuStyle.Text(card.transform, missionTitle, 13.5f, Color.white, MenuFonts.Label, TextAlignmentOptions.Center);
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(8f, 49f);
        title.rectTransform.offsetMax = new Vector2(-8f, -70f);

        Image separator = UiKit.Solid(card.transform, "Separator", new Color(1f, 1f, 1f, 0.35f));
        UiKit.Place(separator.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 43f), new Vector2(0f, 1f));
        separator.rectTransform.anchorMax = new Vector2(1f, 0f);
        separator.rectTransform.sizeDelta = new Vector2(-16f, 1f);

        if (locked)
            AddLockCover(card.transform);

        TextMeshProUGUI threat = MenuStyle.Text(card.transform, threatType.ToUpperInvariant(), 9.5f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Center);
        threat.overflowMode = TextOverflowModes.Ellipsis;
        threat.rectTransform.anchorMin = Vector2.zero;
        threat.rectTransform.anchorMax = new Vector2(1f, 0f);
        threat.rectTransform.pivot = new Vector2(0.5f, 0f);
        threat.rectTransform.offsetMin = new Vector2(8f, 8f);
        threat.rectTransform.offsetMax = new Vector2(-8f, 38f);

        // A thin outline round the card, like the panels of the rest of the menu.
        Image outline = UiKit.Pic(card.transform, "Outline", CartoonArt.MenuPanel(true), new Color(MenuStyle.Edge.r, MenuStyle.Edge.g, MenuStyle.Edge.b, 0.85f));
        UiKit.Stretch(outline.rectTransform);
    }

    // A locked mission is dimmed and says so.
    private static void AddLockCover(Transform card)
    {
        Image cover = UiKit.Solid(card, "Locked", UiKit.Hex("#B3041424"));
        UiKit.Stretch(cover.rectTransform);
        cover.raycastTarget = false;

        TextMeshProUGUI label = MenuStyle.Text(cover.transform, "LOCKED", 13f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Center);
        UiKit.Stretch(label.rectTransform);
    }

    // Mission 1 goes through the loading screen and the story; the others go straight to the briefing.
    private void Open(MissionRequest request)
    {
        App.Show(request.sceneNumber == 1 ? "Loading" : "MissionBriefing", request);
    }
}

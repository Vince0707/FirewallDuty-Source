using TMPro;
using UnityEngine;
using UnityEngine.UI;

// MissionSelectionActivity.java: choose Office or Bank, in the gold-and-navy design. The cards open the same screens as before.
public class MissionSelectionScreen : MenuScreen
{
    protected override void Build(RectTransform root)
    {
        // The same picture as before, a little darker.
        UiLayout.Backdrop(root, "operations_hub_bg", "#55041022");
        RectTransform frame = UiLayout.Frame(root);

        MenuStyle.Header(frame, "MISSION SELECTION", "CHOOSE YOUR ENVIRONMENT", () => App.Back("OperationsHub"), out TextMeshProUGUI subtitle);

        BuildCard(frame, "Office", 40f, "office_environment", "OFFICE ENVIRONMENT", "Office Scenarios:",
                  "Phishing Email Attack", "Unauthorized Access Attempt", "office");
        BuildCard(frame, "Bank", 470f, "bank_environment", "BANK ENVIRONMENT", "Bank Scenarios:",
                  "Fake Pop-up Warning Scam", "Malware File Attack", "bank");

        MenuStyle.Footer(frame);
    }

    private void BuildCard(RectTransform parent, string name, float x, string picture, string heading, string subheading,
                           string line1, string line2, string environment)
    {
        // The card: the environment's picture, a dark fade at the bottom for the words, a blue outline and gold brackets.
        Image card = UiKit.Solid(parent, name, Color.black);
        UiLayout.TopLeft(card.rectTransform, x, 80f, 404f, 268f);
        card.raycastTarget = true;
        card.gameObject.AddComponent<RectMask2D>();

        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        card.gameObject.AddComponent<ButtonFeedback>();
        button.onClick.AddListener(() => App.Show("SceneSelection", environment));

        UiKit.Cover(card.transform, "Picture", UiKit.Picture(picture));

        Image fade = UiKit.Pic(card.transform, "Fade", CartoonArt.MenuChamfer(0f, 0f, 0f, 0f, false), new Color(0.03f, 0.1f, 0.22f, 0.9f));
        UiKit.Place(fade.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 124f));
        fade.rectTransform.anchorMax = new Vector2(1f, 0f);
        fade.rectTransform.sizeDelta = new Vector2(0f, 124f);

        RectTransform outline = UiKit.New(card.transform, "Outline");
        UiKit.Stretch(outline);
        Image edge = UiKit.Pic(outline, "Edge", CartoonArt.MenuChamfer(0f, 0f, 0f, 0f, true), MenuStyle.Edge);
        UiKit.Stretch(edge.rectTransform);
        MenuStyle.Brackets(outline, 22f, MenuStyle.Gold);

        // The words, on the fade.
        TextMeshProUGUI headingText = MenuStyle.Text(card.transform, heading, 21f, MenuStyle.Cream, MenuFonts.Label, TextAlignmentOptions.Left, "Heading");
        UiKit.Place(headingText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 90f), new Vector2(360f, 26f));
        UiLayout.Spaced(headingText, 0.06f);

        TextMeshProUGUI subText = MenuStyle.Text(card.transform, subheading, 13f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Left, "Subheading");
        UiKit.Place(subText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 66f), new Vector2(360f, 20f));
        UiLayout.Spaced(subText, 0.08f);

        TextMeshProUGUI first = MenuStyle.Text(card.transform, "•  " + line1, 10.5f, MenuStyle.Soft, MenuFonts.Body, TextAlignmentOptions.Left, "Line1");
        UiKit.Place(first.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 40f), new Vector2(360f, 16f));

        TextMeshProUGUI second = MenuStyle.Text(card.transform, "•  " + line2, 10.5f, MenuStyle.Soft, MenuFonts.Body, TextAlignmentOptions.Left, "Line2");
        UiKit.Place(second.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(360f, 16f));
    }
}

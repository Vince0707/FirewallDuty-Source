using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Operations Hub (OperationsHubActivity.java): MISSION SELECTION and CYBER TABLET, in the gold-and-navy design.
// The cards open the same screens as before; BACK goes back as before.
public class OperationsHubScreen : MenuScreen
{
    protected override void Build(RectTransform root)
    {
        // The same picture as before, a little darker.
        UiLayout.Backdrop(root, "operations_hub_bg", "#66041022");
        RectTransform frame = UiLayout.Frame(root);

        MenuStyle.Header(frame, "OPERATIONS HUB", "SELECT YOUR OPERATION", () => App.Back(), out TextMeshProUGUI subtitle);

        // The two big cards, side by side.
        BuildCard(frame, "MissionSelection", 56f, "ic_mission", 74f, 74f, "MISSION SELECTION", "Choose your mission", () => App.Show("MissionSelection"));
        BuildCard(frame, "CyberTablet", 480f, "ic_cyber_table", 80f, 74f, "CYBER TABLET", "Investigation Tool", () => App.Show("Tablet"));

        MenuStyle.Footer(frame);
    }

    private void BuildCard(RectTransform parent, string name, float x, string icon, float iconWidth, float iconHeight, string heading, string sub, UnityEngine.Events.UnityAction action)
    {
        RectTransform card = MenuStyle.Chamfer(parent, name, x, 106f, 378f, 214f, 22f, 0f, 0f, 22f, MenuStyle.PanelDark, MenuStyle.Edge);
        MenuStyle.Diagonal(card, MenuStyle.Corner.TopLeft, 22f, MenuStyle.Gold);
        MenuStyle.Diagonal(card, MenuStyle.Corner.BottomRight, 22f, MenuStyle.Gold);
        MenuStyle.MakeButton(card, action);

        // The picture on a dark plate.
        RectTransform plate = MenuStyle.Rounded(card, "Plate", 28f, 34f, 112f, 112f, MenuStyle.Plate, new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, 0.55f));
        MenuStyle.Icon(plate, UiKit.Icon(icon), iconWidth, iconHeight);

        TextMeshProUGUI headingText = MenuStyle.Text(card, heading, 25f, MenuStyle.Cream, MenuFonts.Label, TextAlignmentOptions.Left, "Heading");
        UiLayout.TopLeft(headingText.rectTransform, 160f, 52f, 200f, 60f);
        headingText.textWrappingMode = TextWrappingModes.Normal;
        UiLayout.Spaced(headingText, 0.04f);

        TextMeshProUGUI subText = MenuStyle.Text(card, sub, 11.5f, MenuStyle.Soft, MenuFonts.Body, TextAlignmentOptions.Left, "Subtitle");
        UiLayout.TopLeft(subText.rectTransform, 160f, 118f, 200f, 18f);

        // A gold "enter" line along the bottom of the card.
        Image line = UiKit.Solid(card, "EnterLine", new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, 0.6f));
        UiLayout.TopLeft(line.rectTransform, 28f, 168f, 250f, 1.4f);

        TextMeshProUGUI enter = MenuStyle.Text(card, "OPEN", 11f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Left, "Open");
        UiLayout.TopLeft(enter.rectTransform, 28f, 176f, 160f, 16f);
        UiLayout.Spaced(enter, 0.12f);
    }
}

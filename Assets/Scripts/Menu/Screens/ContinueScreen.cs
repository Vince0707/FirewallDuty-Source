using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The CONTINUE page, in the gold-and-navy design of the main menu. Every mission that was started and not finished is listed (newest first), each with the
// date and time it was started, its environment and scene number, and the threat. Tapping one opens its briefing to play it again. A finished mission leaves
// the list. "No unfinished missions yet." when there are none.
public class ContinueScreen : MenuScreen
{
    private RectTransform list;
    private TextMeshProUGUI message;

    protected override void Build(RectTransform root)
    {
        // The same picture as before.
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex("#70000000"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);

        MenuStyle.Header(frame, "CONTINUE", "YOUR UNFINISHED MISSIONS", () => App.Back(), out TextMeshProUGUI subtitle);

        message = MenuStyle.Text(frame, "", 12f, MenuStyle.Soft, MenuFonts.Body, TextAlignmentOptions.Center, "Message");
        UiLayout.TopCenter(message.rectTransform, 76f, 700f, 18f);

        list = UiKit.Scroll(frame, "Scroll", 8f, 0);
        UiKit.Stretch(UiLayout.ScrollRoot(list), 40f, 66f, 40f, 100f);

        // OPERATIONS HUB, as before, at the bottom.
        RectTransform hub = MenuStyle.Chamfer(frame, "OperationsHub", (UiLayout.DesignWidth - 270f) * 0.5f, 354f, 270f, 40f, 10f, 0f, 0f, 10f, MenuStyle.PanelDark, MenuStyle.Gold);
        MenuStyle.MakeButton(hub, () => App.Show("OperationsHub"));
        TextMeshProUGUI hubLabel = MenuStyle.Text(hub, "OPERATIONS HUB", 15f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Center, "Label");
        UiKit.Stretch(hubLabel.rectTransform);
        UiLayout.Spaced(hubLabel, 0.1f);
    }

    public override void OnShow(object arg)
    {
        for (int i = list.childCount - 1; i >= 0; i--)
            Object.Destroy(list.GetChild(i).gameObject);

        System.Collections.Generic.List<MissionRequest> pending = PlayerSession.PendingMissions;

        if (pending.Count == 0)
        {
            message.text = "No unfinished missions yet.";
            return;
        }

        message.text = pending.Count == 1 ? "You left 1 mission unfinished. Tap it to continue." : "You left " + pending.Count + " missions unfinished. Tap one to continue it.";

        foreach (MissionRequest request in pending)
            BuildCard(request);
    }

    private void BuildCard(MissionRequest request)
    {
        MissionRequest mission = request;

        RectTransform card = MenuStyle.Chamfer(list, "Mission", 0f, 0f, 100f, 72f, 14f, 0f, 0f, 14f, MenuStyle.PanelDark, MenuStyle.Edge);
        UiKit.Size(card, -1f, 72f);
        UiKit.Flexible(card, 1f, 0f);
        MenuStyle.Diagonal(card, MenuStyle.Corner.TopLeft, 14f, MenuStyle.Gold);
        MenuStyle.Diagonal(card, MenuStyle.Corner.BottomRight, 14f, MenuStyle.Gold);
        MenuStyle.MakeButton(card, () => App.Show("MissionBriefing", mission));

        // The threat's picture on a dark plate.
        RectTransform plate = MenuStyle.Rounded(card, "Plate", 16f, 10f, 52f, 52f, MenuStyle.Plate, new Color(MenuStyle.Gold.r, MenuStyle.Gold.g, MenuStyle.Gold.b, 0.5f));
        Image emblem = UiKit.Pic(plate, "Emblem", CartoonArt.ThreatIcon(mission.threatType));
        emblem.type = Image.Type.Simple;
        emblem.preserveAspect = true;
        UiKit.Place(emblem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));

        // The mission and where it is: the title, then the environment, the scene number and the threat.
        TextMeshProUGUI title = MenuStyle.Text(card, mission.missionTitle, 18f, MenuStyle.Cream, MenuFonts.Label, TextAlignmentOptions.Left, "Title");
        UiLayout.TopLeft(title.rectTransform, 82f, 11f, 460f, 24f);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;

        string environment = MissionData.IsBank(mission.environment) ? "BANK" : "OFFICE";
        TextMeshProUGUI details = MenuStyle.Text(card, environment + "   •   SCENE " + mission.sceneNumber + "   •   " + (mission.threatType ?? "").ToUpperInvariant(), 9.6f, MenuStyle.Sky,
                                                 MenuFonts.Body, TextAlignmentOptions.Left, "Details");
        UiLayout.TopLeft(details.rectTransform, 82f, 41f, 480f, 14f);
        details.textWrappingMode = TextWrappingModes.NoWrap;
        details.overflowMode = TextOverflowModes.Ellipsis;

        // The date and the time it was started, and the way in.
        FormatTime(mission.startedAt, out string date, out string time);

        TextMeshProUGUI dateText = MenuStyle.Text(card, date, 13.5f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Right, "Date");
        UiLayout.TopRight(dateText.rectTransform, 20f, 10f, 170f, 18f);
        UiLayout.Spaced(dateText, 0.06f);

        TextMeshProUGUI timeText = MenuStyle.Text(card, time, 10.5f, MenuStyle.Soft, MenuFonts.Body, TextAlignmentOptions.Right, "Time");
        UiLayout.TopRight(timeText.rectTransform, 20f, 29f, 170f, 14f);

        TextMeshProUGUI go = MenuStyle.Text(card, "CONTINUE", 11.5f, MenuStyle.Cream, MenuFonts.Label, TextAlignmentOptions.Right, "Go");
        UiLayout.TopRight(go.rectTransform, 20f, 47f, 170f, 16f);
        UiLayout.Spaced(go, 0.1f);
    }

    // "2026-10-27 15:45" -> "27 OCT 2026" and "03:45 PM". A mission saved before dates were kept has none.
    private static void FormatTime(string startedAt, out string date, out string time)
    {
        if (!string.IsNullOrEmpty(startedAt) &&
            System.DateTime.TryParseExact(startedAt, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out System.DateTime when))
        {
            date = when.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
            time = when.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            return;
        }

        date = "DATE UNKNOWN";
        time = "";
    }
}

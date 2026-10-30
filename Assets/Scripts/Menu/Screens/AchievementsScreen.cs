using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// activity_achievements.xml / AchievementActivity.java: seven certificates, LOCKED until they are in the player's
// "unlockedAchievements" list in Firestore.
public class AchievementsScreen : MenuScreen
{
    private struct Certificate
    {
        public string id;
        public string title;
        public string badge;     // the Android card used an emoji; the game font has none, so a short mark stands in
        public Color color;
        public float titleSize;
    }

    private static readonly Certificate[] All =
    {
        new Certificate { id = "phishing_detection", title = "Phishing Detection Certificate", badge = "PH", color = new Color(0.25f, 0.62f, 0.95f), titleSize = 14f },
        new Certificate { id = "malware_detection", title = "Malware Detection Certificate", badge = "MW", color = new Color(0.85f, 0.3f, 0.3f), titleSize = 14f },
        new Certificate { id = "popup_protection", title = "Pop-Up Protection Certificate", badge = "PU", color = new Color(0.98f, 0.75f, 0.2f), titleSize = 14f },
        new Certificate { id = "spam_mail_analyst", title = "Spam Mail Analyst Certificate", badge = "SP", color = new Color(0.4f, 0.75f, 0.95f), titleSize = 14f },
        new Certificate { id = "social_engineering_awareness", title = "Social Engineering Awareness Certificate", badge = "SE", color = new Color(0.65f, 0.5f, 0.9f), titleSize = 13f },
        new Certificate { id = "access_control_defender", title = "Access Control Defender Certificate", badge = "AC", color = new Color(0.3f, 0.8f, 0.55f), titleSize = 14f },
        new Certificate { id = "trivia_master", title = "Trivia Master Certificate", badge = "TM", color = new Color(1f, 0.82f, 0.2f), titleSize = 14f },
    };

    private readonly Dictionary<string, TMP_Text> statuses = new Dictionary<string, TMP_Text>();
    private readonly Dictionary<string, Image> icons = new Dictionary<string, Image>();
    private int visit;

    protected override void Build(RectTransform root)
    {
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex("#66051C31"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);

        UiLayout.BackButton(frame, 1f, 40f, 110f, 45f, 15f, () => App.Back());

        Image title = UiKit.Pic(frame, "Title", UiKit.Shape("leaderboard_title_bg"));
        UiLayout.TopCenter(title.rectTransform, 20f, 370f, 52f);
        TextMeshProUGUI titleText = UiKit.Text(title.transform, "ACHIEVEMENTS", 23f, Color.white, FontStyles.Bold);
        UiLayout.Spaced(titleText, 0.16f);
        UiKit.Stretch(titleText.rectTransform);

        // Panel: left 55, right 55, from 20 below the title to 20 above the bottom.
        Image panel = UiKit.Pic(frame, "Panel", UiKit.Shape("achievements_panel_bg"));
        UiKit.Stretch(panel.rectTransform, 55f, 20f, 55f, 92f);

        RectTransform content = UiKit.Scroll(panel.transform, "Scroll", 0f, 0);
        UiKit.Stretch(UiLayout.ScrollRoot(content), 8f, 8f, 8f, 8f);
        content.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;

        for (int start = 0; start < All.Length; start += 3)
        {
            RectTransform row = UiKit.HBox(content, "Row", 0f, TextAnchor.UpperLeft, false, false);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            UiKit.Size(row, -1f, 160f);

            for (int slot = 0; slot < 3; slot++)
            {
                int index = start + slot;
                if (index < All.Length)
                    BuildCard(row, All[index]);
                else
                    Spacer(row);
            }
        }
    }

    private static void Spacer(RectTransform row)
    {
        RectTransform space = UiKit.New(row, "Empty");
        UiKit.Size(space, 10f, 150f);
        UiKit.Flexible(space, 1f, 0f);
    }

    private void BuildCard(RectTransform row, Certificate certificate)
    {
        // Each card has a 5 margin: a transparent holder with the card inside.
        RectTransform holder = UiKit.New(row, certificate.id);
        UiKit.Size(holder, 10f, 160f);
        UiKit.Flexible(holder, 1f, 0f);

        Image card = UiKit.Pic(holder, "Card", UiKit.Shape("achievement_card_bg"));
        UiKit.Stretch(card.rectTransform, 5f, 5f, 5f, 5f);

        TextMeshProUGUI title = UiKit.Text(card.transform, certificate.title, certificate.titleSize, Color.white, FontStyles.Bold, TextAlignmentOptions.Top);
        UiLayout.Spaced(title, 0.07f);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.offsetMin = new Vector2(8f, -50f);
        title.rectTransform.offsetMax = new Vector2(-8f, -6f);

        // A soft glow in the certificate's colour, with its cartoon picture on top (grey while locked).
        Image glow = UiKit.Pic(card.transform, "Glow", CartoonArt.Glow(), certificate.color);
        glow.type = Image.Type.Simple;
        UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(76f, 76f));

        Image icon = UiKit.Pic(card.transform, "Icon", CartoonArt.CertificateIcon(certificate.id));
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        UiKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(56f, 56f));
        icons[certificate.id] = icon;

        TextMeshProUGUI status = UiKit.Text(card.transform, "LOCKED", 13f, UiKit.Hex("#AFC5D9"), FontStyles.Bold);
        UiLayout.Spaced(status, 0.12f);
        UiKit.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(180f, 20f));
        statuses[certificate.id] = status;
    }

    public override void OnShow(object arg)
    {
        // What this phone already knows, until the fresh record arrives.
        ShowStatuses(PlayerSession.IsSignedIn ? new HashSet<string>(PlayerSession.Player.unlockedAchievements) : new HashSet<string>());
        visit++;
        int myVisit = visit;

        if (!PlayerSession.IsSignedIn)
        {
            App.Toast("Please log in to view your achievements.");
            return;
        }

        FirebaseRest.GetPlayer(PlayerSession.Player.uid, (result, player) =>
        {
            if (myVisit != visit)
                return;

            if (!result.ok)
            {
                App.Toast("Couldn't load achievements. Please try again.");
                return;
            }

            if (player != null)
            {
                // SignIn works the certificates out again from the missions finished and the trivia score.
                PlayerSession.SignIn(player);
                ShowStatuses(new HashSet<string>(PlayerSession.Player.unlockedAchievements));
            }
        });
    }

    private void ShowStatuses(HashSet<string> unlocked)
    {
        foreach (KeyValuePair<string, TMP_Text> entry in statuses)
        {
            bool done = unlocked.Contains(entry.Key);
            entry.Value.text = done ? "COMPLETED" : "LOCKED";
            entry.Value.color = done ? new Color32(113, 255, 126, 255) : new Color32(175, 197, 217, 255);

            if (icons.TryGetValue(entry.Key, out Image picture))
                picture.color = done ? Color.white : new Color(0.38f, 0.43f, 0.5f, 0.85f);
        }
    }
}

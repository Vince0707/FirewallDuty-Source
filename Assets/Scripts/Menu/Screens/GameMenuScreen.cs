using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// The main menu: the gold-and-navy design (cut-corner panels, header plate, gold accents). The buttons do exactly what they always did:
//   NEW GAME -> Operations Hub          MISSIONS -> Continue          TRIVIA -> Trivia Challenge
//   RANKINGS -> Leaderboards            PROFILE (and the profile chip) -> Profile
//   SETTINGS -> Settings                EXIT GAME -> quit
// The player's name and level are shown in the chip at the top, refreshed the same way as before. The picture behind it is the same one as before.
// Fonts (see MenuFonts): the title is Exo 2 Bold, buttons and labels are Rajdhani SemiBold, descriptions are Inter Regular.
public class GameMenuScreen : MenuScreen
{
    private TMP_Text usernameText;
    private TMP_Text levelText;
    private TMP_Text avatarLetter;
    private RawImage avatarPhoto;       // the player's photo in the avatar circle (when there is one)
    private Texture2D shownPhoto;
    private string shownPhotoUri;

    private static readonly Color Cream = new Color32(0xF6, 0xE6, 0xBC, 255);
    private static readonly Color Gold = new Color32(0xF3, 0xD7, 0x93, 255);
    private static readonly Color Navy = new Color32(0x0B, 0x25, 0x4F, 255);
    private static readonly Color Panel = new Color32(0x14, 0x3C, 0x78, 232);       // the fill is lighter at the top (the sprite shades it)
    private static readonly Color PanelDark = new Color32(0x0F, 0x2E, 0x62, 238);
    private static readonly Color Plate = new Color32(0x0A, 0x20, 0x45, 242);
    private static readonly Color Edge = new Color32(0x4F, 0x93, 0xE6, 255);
    private static readonly Color Sky = new Color32(0xA9, 0xC8, 0xF2, 255);
    private static readonly Color Soft = new Color32(0xDC, 0xE8, 0xFA, 255);

    protected override void Build(RectTransform root)
    {
        // The same picture and shade as before.
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Pic(root, "Overlay", UiKit.Shape("menu_overlay"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);

        // A thin cut-corner border round the whole screen.
        RectTransform border = Chamfer(frame, "ScreenBorder", 6f, 4f, 902f, 403f, 14f, 14f, 14f, 14f, Color.clear, new Color(Edge.r, Edge.g, Edge.b, 0.4f));

        BuildHeader(frame);
        BuildCard(frame);
        BuildTiles(frame);
        BuildFooter(frame);
    }

    // ---- Header: the plate with the emblem and title, the player chip and the gear ---------------------------------------------

    private void BuildHeader(RectTransform frame)
    {
        // The dark plate behind the title, cut at its lower right, with slanted stripes at the end.
        Chamfer(frame, "HeaderPlate", 6f, 4f, 456f, 58f, 0f, 0f, 0f, 30f, Plate, new Color(Edge.r, Edge.g, Edge.b, 0.75f));
        Stripe(frame, 398f, 14f, 36f, Gold);
        Stripe(frame, 410f, 14f, 36f, Sky);
        Stripe(frame, 420f, 14f, 36f, new Color(Sky.r, Sky.g, Sky.b, 0.6f));

        Image logo = UiKit.Pic(frame, "Emblem", CartoonArt.MenuLogo());
        UiLayout.TopLeft(logo.rectTransform, 26f, 7f, 48f, 48f);
        logo.preserveAspect = true;

        TextMeshProUGUI title = MenuFonts.Use(UiKit.Text(frame, "FIREWALL DUTY", 35f, Cream, FontStyles.Normal, TextAlignmentOptions.Left, "Title"), MenuFonts.Title);
        UiLayout.TopLeft(title.rectTransform, 88f, 5f, 360f, 38f);
        UiLayout.Spaced(title, 0.05f);

        TextMeshProUGUI subtitle = MenuFonts.Use(UiKit.Text(frame, "CYBERSECURITY SIMULATION", 7.8f, Sky, FontStyles.Normal, TextAlignmentOptions.Left, "Subtitle"), MenuFonts.Body);
        UiLayout.TopLeft(subtitle.rectTransform, 90f, 42f, 330f, 12f);
        UiLayout.Spaced(subtitle, 0.42f);

        // The player chip (cut at the ends): avatar, name and level. Tapping it opens the profile, like the old profile card.
        RectTransform chip = Chamfer(frame, "ProfileChip", 0f, 0f, 146f, 38f, 9f, 9f, 9f, 9f, PanelDark, Edge);
        UiLayout.TopRight(chip, 22f, 10f, 146f, 38f);
        MakeButton(chip, () => App.Show("Profile"));

        Image avatar = UiKit.Pic(chip, "Avatar", UiKit.CircleSprite(), Gold);
        UiKit.Place(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(11f, 0f), new Vector2(26f, 26f));
        // The photo is clipped to the circle and fills it.
        UiKit.Ensure<Mask>(avatar.gameObject).showMaskGraphic = true;
        RectTransform photoRect = UiKit.New(avatar.transform, "Photo");
        UiKit.Stretch(photoRect);
        avatarPhoto = photoRect.gameObject.AddComponent<RawImage>();
        avatarPhoto.raycastTarget = false;
        avatarPhoto.enabled = false;

        avatarLetter = MenuFonts.Use(UiKit.Text(avatar.transform, "P", 14f, Navy, FontStyles.Normal), MenuFonts.Label);
        UiKit.Stretch(avatarLetter.rectTransform);

        usernameText = MenuFonts.Use(UiKit.Text(chip, "Username", 11f, Cream, FontStyles.Normal, TextAlignmentOptions.Left, "Username"), MenuFonts.Label);
        UiKit.Place(usernameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(45f, 7f), new Vector2(92f, 15f));
        usernameText.overflowMode = TextOverflowModes.Ellipsis;
        usernameText.textWrappingMode = TextWrappingModes.NoWrap;

        levelText = MenuFonts.Use(UiKit.Text(chip, "Level 1", 8.8f, Sky, FontStyles.Normal, TextAlignmentOptions.Left, "Level"), MenuFonts.Body);
        UiKit.Place(levelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(45f, -8f), new Vector2(92f, 12f));

        // The gold line under the header. (There is no gear at the top right any more: SETTINGS is one of the tiles.)
        Image line = UiKit.Solid(frame, "HeaderLine", new Color(Gold.r, Gold.g, Gold.b, 0.85f));
        UiLayout.TopLeft(line.rectTransform, 6f, 61f, 902f, 1.6f);
    }

    // ---- The big card: "Ready for duty?" and NEW GAME ---------------------------------------------------------------------------------

    private void BuildCard(RectTransform frame)
    {
        RectTransform card = Chamfer(frame, "AssignmentCard", 38f, 106f, 468f, 222f, 18f, 0f, 0f, 18f, PanelDark, Edge);
        Diagonal(card, Corner.TopLeft, 18f, Gold);
        Diagonal(card, Corner.BottomRight, 18f, Gold);

        // The tag (cut at its right end).
        RectTransform tag = Chamfer(card, "Tag", 26f, 22f, 160f, 18f, 0f, 7f, 0f, 7f, Gold, Color.clear);
        TextMeshProUGUI tagText = MenuFonts.Use(UiKit.Text(tag, "YOUR NEXT ASSIGNMENT", 8.8f, Navy, FontStyles.Normal), MenuFonts.Label);
        UiKit.Stretch(tagText.rectTransform, 0f, 0f, 6f, 0f);
        UiLayout.Spaced(tagText, 0.06f);

        TextMeshProUGUI headline = MenuFonts.Use(UiKit.Text(card, "Ready for duty?", 38f, Gold, FontStyles.Normal, TextAlignmentOptions.Left, "Headline"), MenuFonts.Label);
        UiLayout.TopLeft(headline.rectTransform, 26f, 46f, 400f, 46f);

        TextMeshProUGUI text = MenuFonts.Use(UiKit.Text(card, "Step into the field and keep data\nsafe from cyber threats.", 11f, Soft, FontStyles.Normal, TextAlignmentOptions.TopLeft, "Description"), MenuFonts.Body);
        UiLayout.TopLeft(text.rectTransform, 26f, 98f, 380f, 36f);

        // NEW GAME: the big gold button.
        RectTransform newGame = Rounded(card, "NewGame", 26f, 150f, 270f, 52f, Gold, Color.clear);
        MakeButton(newGame, () => App.Show("OperationsHub"));

        Image play = UiKit.Pic(newGame, "Play", CartoonArt.MenuPlayIcon());
        UiKit.Place(play.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-66f, 0f), new Vector2(26f, 26f));
        play.preserveAspect = true;

        TextMeshProUGUI label = MenuFonts.Use(UiKit.Text(newGame, "NEW GAME", 21f, Navy, FontStyles.Normal, TextAlignmentOptions.Center, "Label"), MenuFonts.Label);
        UiKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(190f, 30f));
        UiLayout.Spaced(label, 0.05f);
    }

    // ---- The tiles on the right ----------------------------------------------------------------------------------------------------

    private void BuildTiles(RectTransform frame)
    {
        // MISSIONS (the old CONTINUE): the wide tile, cut at its right corners with a gold accent.
        RectTransform missions = Chamfer(frame, "MissionsTile", 530f, 106f, 344f, 78f, 0f, 14f, 0f, 14f, Panel, Edge);
        MakeButton(missions, () => App.Show("Continue"));
        Diagonal(missions, Corner.TopRight, 14f, Gold);
        Diagonal(missions, Corner.BottomRight, 14f, new Color(Edge.r, Edge.g, Edge.b, 0.9f));

        RectTransform plate = Rounded(missions, "Plate", 12f, 12f, 54f, 54f, Plate, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
        Icon(plate, CartoonArt.MenuMissionIcon(), 38f);

        TextMeshProUGUI title = MenuFonts.Use(UiKit.Text(missions, "MISSIONS", 19f, Cream, FontStyles.Normal, TextAlignmentOptions.Left, "Title"), MenuFonts.Label);
        UiLayout.TopLeft(title.rectTransform, 80f, 16f, 220f, 24f);
        UiLayout.Spaced(title, 0.05f);

        TextMeshProUGUI sub = MenuFonts.Use(UiKit.Text(missions, "Continue your assignments", 9.5f, Soft, FontStyles.Normal, TextAlignmentOptions.Left, "Subtitle"), MenuFonts.Body);
        UiLayout.TopLeft(sub.rectTransform, 80f, 43f, 230f, 14f);

        // The four small tiles.
        Tile(frame, "TriviaTile", 530f, 194f, 169f, 74f, CartoonArt.MenuBrainIcon(), "TRIVIA", "Test your knowledge", () => App.Show("Trivia"));
        Tile(frame, "RankingsTile", 705f, 194f, 169f, 74f, CartoonArt.TrophyIcon(), "RANKINGS", "See your progress", () => App.Show("Leaderboard"));
        Tile(frame, "ProfileTile", 530f, 274f, 169f, 74f, CartoonArt.MenuPersonIcon(), "PROFILE", "Your stats & achievements", () => App.Show("Profile"));
        Tile(frame, "SettingsTile", 705f, 274f, 169f, 74f, CartoonArt.MenuGearIcon(), "SETTINGS", "Adjust preferences", () => App.Show("Settings"));
    }

    private void Tile(RectTransform frame, string name, float x, float y, float width, float height, Sprite icon, string title, string subtitle, UnityAction action)
    {
        RectTransform tile = Rounded(frame, name, x, y, width, height, Panel, Edge);
        MakeButton(tile, action);

        Image picture = UiKit.Pic(tile, "Icon", icon);
        UiLayout.TopLeft(picture.rectTransform, 13f, 8f, 28f, 28f);
        picture.preserveAspect = true;

        TextMeshProUGUI t = MenuFonts.Use(UiKit.Text(tile, title, 13f, Cream, FontStyles.Normal, TextAlignmentOptions.Left, "Title"), MenuFonts.Label);
        UiLayout.TopLeft(t.rectTransform, 13f, 39f, width - 30f, 16f);
        UiLayout.Spaced(t, 0.05f);

        TextMeshProUGUI s = MenuFonts.Use(UiKit.Text(tile, subtitle, 8f, Sky, FontStyles.Normal, TextAlignmentOptions.Left, "Subtitle"), MenuFonts.Body);
        UiLayout.TopLeft(s.rectTransform, 13f, 56f, width - 20f, 12f);
    }

    // ---- Footer: the reminders and EXIT GAME ---------------------------------------------------------------------------------------------

    private void BuildFooter(RectTransform frame)
    {
        // The left plate with the reminders (cut at its upper right).
        Chamfer(frame, "FooterPlate", 6f, 362f, 560f, 42f, 0f, 22f, 0f, 0f, Plate, new Color(Gold.r, Gold.g, Gold.b, 0.6f));

        TextMeshProUGUI reminders = MenuFonts.Use(UiKit.Text(frame, "STAY ALERT   •   THINK BEFORE YOU CLICK   •   KEEP DATA SAFE", 8.4f, Sky, FontStyles.Normal,
                                                             TextAlignmentOptions.Left, "Reminders"), MenuFonts.Body);
        UiLayout.TopLeft(reminders.rectTransform, 34f, 377f, 520f, 13f);
        UiLayout.Spaced(reminders, 0.12f);

        // EXIT GAME on its own plate (cut at its upper left): the whole plate is the button.
        RectTransform exit = Chamfer(frame, "ExitGame", 0f, 0f, 212f, 42f, 22f, 0f, 0f, 0f, Plate, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        UiLayout.TopRight(exit, 6f, 362f, 212f, 42f);
        MakeButton(exit, QuitGame);

        Image icon = UiKit.Pic(exit, "Icon", CartoonArt.MenuExitIcon());
        UiKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-34f, -2f), new Vector2(19f, 19f));
        icon.preserveAspect = true;

        TextMeshProUGUI label = MenuFonts.Use(UiKit.Text(exit, "EXIT GAME", 11.5f, Cream, FontStyles.Normal, TextAlignmentOptions.Left, "Label"), MenuFonts.Label);
        UiKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-18f, -2f), new Vector2(100f, 16f));
        UiLayout.Spaced(label, 0.1f);
    }

    // ---- Building blocks -------------------------------------------------------------------------------------------------------------------

    private enum Corner { TopLeft, TopRight, BottomRight }

    // A cut-corner panel placed from the top left of its parent; the cut sizes are top left, top right, bottom left, bottom right.
    private static RectTransform Chamfer(Transform parent, string name, float x, float y, float width, float height,
                                         float topLeft, float topRight, float bottomLeft, float bottomRight, Color fill, Color outline)
    {
        RectTransform rect = UiKit.New(parent, name);
        UiLayout.TopLeft(rect, x, y, width, height);

        Image body = rect.gameObject.AddComponent<Image>();
        body.sprite = CartoonArt.MenuChamfer(topLeft, topRight, bottomLeft, bottomRight, false);
        body.type = Image.Type.Sliced;
        body.color = fill;
        body.raycastTarget = false;

        if (outline.a > 0f)
        {
            Image edge = UiKit.Pic(rect, "Outline", CartoonArt.MenuChamfer(topLeft, topRight, bottomLeft, bottomRight, true), outline);
            UiKit.Stretch(edge.rectTransform);
        }

        return rect;
    }

    // A rounded panel placed from the top left of its parent.
    private static RectTransform Rounded(Transform parent, string name, float x, float y, float width, float height, Color fill, Color outline)
    {
        RectTransform rect = UiKit.New(parent, name);
        UiLayout.TopLeft(rect, x, y, width, height);

        Image body = rect.gameObject.AddComponent<Image>();
        body.sprite = CartoonArt.MenuPanel(false);
        body.type = Image.Type.Sliced;
        body.color = fill;
        body.raycastTarget = false;

        if (outline.a > 0f)
        {
            Image edge = UiKit.Pic(rect, "Outline", CartoonArt.MenuPanel(true), outline);
            UiKit.Stretch(edge.rectTransform);
        }

        return rect;
    }

    // The gold (or blue) line along a cut corner.
    private static void Diagonal(RectTransform panel, Corner corner, float cut, Color color)
    {
        Image line = UiKit.Solid(panel, "Accent", color);
        float length = cut * 1.4142f + 1f;

        switch (corner)
        {
            case Corner.TopLeft:
                UiKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cut * 0.5f, -cut * 0.5f), new Vector2(length, 2.4f));
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                break;
            case Corner.TopRight:
                UiKit.Place(line.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-cut * 0.5f, -cut * 0.5f), new Vector2(length, 2.4f));
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                break;
            default:
                UiKit.Place(line.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-cut * 0.5f, cut * 0.5f), new Vector2(length, 2.4f));
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                break;
        }
    }

    // A slanted bar (the stripes at the end of the header plate).
    private static void Stripe(RectTransform parent, float x, float y, float height, Color color)
    {
        Image bar = UiKit.Solid(parent, "Stripe", color);
        UiLayout.TopLeft(bar.rectTransform, x, y, 4f, height);
        bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
    }

    // Makes a panel tappable: the same colour tint, press feedback and click sound as every other menu button.
    private static void MakeButton(RectTransform rect, UnityAction action)
    {
        Graphic graphic = rect.GetComponent<Image>();
        graphic.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        rect.gameObject.AddComponent<ButtonFeedback>();
        button.onClick.AddListener(action);
    }

    // A picture in the middle of a panel.
    private static void Icon(RectTransform parent, Sprite sprite, float size)
    {
        Image picture = UiKit.Pic(parent, "Icon", sprite);
        UiKit.Place(picture.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        picture.preserveAspect = true;
    }

    // ---- The player ------------------------------------------------------------------------------------------------------------------------

    public override void OnShow(object arg)
    {
        ShowProfile(PlayerSession.Player);

        // Refresh from Firestore (the level may have changed after a mission).
        if (PlayerSession.IsSignedIn)
        {
            FirebaseRest.GetPlayer(PlayerSession.Player.uid, (result, player) =>
            {
                if (result.ok && player != null)
                {
                    PlayerSession.SignIn(player);
                    ShowProfile(PlayerSession.Player);
                }
            });
        }
    }

    private void ShowProfile(PlayerData player)
    {
        string name = player != null && !string.IsNullOrWhiteSpace(player.username) ? player.username
                    : player != null && !string.IsNullOrWhiteSpace(player.fullName) ? player.fullName
                    : "PLAYER";

        usernameText.text = player != null ? name : "Username";
        levelText.text = "Level " + (player != null ? player.level : 1);
        avatarLetter.text = name.Substring(0, 1).ToUpperInvariant();

        ShowPhoto(player != null ? player.photoUri : null);
    }

    // The photo from the profile page in the avatar circle; the first letter of the name when there is no photo or it cannot be read on this phone.
    private void ShowPhoto(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            ClearPhoto();
            return;
        }

        // Read again only when the photo is a different one (the profile page puts the new address in the player when a photo is chosen).
        if (uri != shownPhotoUri || shownPhoto == null)
        {
            Texture2D texture = NativeBridge.LoadPhoto(uri);

            if (shownPhoto != null)
                Object.Destroy(shownPhoto);

            shownPhoto = texture;
            shownPhotoUri = texture != null ? uri : null;
        }

        if (shownPhoto == null)
        {
            avatarPhoto.enabled = false;
            avatarLetter.gameObject.SetActive(true);
            return;
        }

        avatarPhoto.texture = shownPhoto;
        avatarPhoto.enabled = true;

        CoverFit fit = avatarPhoto.GetComponent<CoverFit>();
        if (fit == null)
            fit = avatarPhoto.gameObject.AddComponent<CoverFit>();
        fit.aspect = (float)shownPhoto.width / shownPhoto.height;

        avatarLetter.gameObject.SetActive(false);
    }

    private void ClearPhoto()
    {
        if (shownPhoto != null)
            Object.Destroy(shownPhoto);

        shownPhoto = null;
        shownPhotoUri = null;
        avatarPhoto.enabled = false;
        avatarLetter.gameObject.SetActive(true);
    }

    // The menu is the home screen: Back does nothing here.
    public override void OnBack() { }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

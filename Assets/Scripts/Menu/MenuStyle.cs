using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// The pieces of the gold-and-navy menu design, shared by the screens that use it (Operations Hub, Mission Selection, the mission list and the tablet):
// cut-corner panels, the header plate with the emblem and title, the BACK button, the footer plate, and the fonts. The main menu draws the same shapes.
// Fonts (see MenuFonts): titles Exo 2 Bold, buttons and mission labels Rajdhani SemiBold, descriptions Inter Regular.
public static class MenuStyle
{
    public static readonly Color Cream = new Color32(0xF6, 0xE6, 0xBC, 255);
    public static readonly Color Gold = new Color32(0xF3, 0xD7, 0x93, 255);
    public static readonly Color Navy = new Color32(0x0B, 0x25, 0x4F, 255);
    public static readonly Color Panel = new Color32(0x14, 0x3C, 0x78, 232);
    public static readonly Color PanelDark = new Color32(0x0F, 0x2E, 0x62, 238);
    public static readonly Color Plate = new Color32(0x0A, 0x20, 0x45, 242);
    public static readonly Color PlateFillColor = new Color32(0x0A, 0x20, 0x45, 242);   // the dark fill of small unselected buttons
    public static readonly Color Edge = new Color32(0x4F, 0x93, 0xE6, 255);
    public static readonly Color Sky = new Color32(0xA9, 0xC8, 0xF2, 255);
    public static readonly Color Soft = new Color32(0xDC, 0xE8, 0xFA, 255);

    public enum Corner { TopLeft, TopRight, BottomRight }

    // ---- Panels ---------------------------------------------------------------------------------------------------------------------

    // A cut-corner panel placed from the top left of its parent; the cut sizes are top left, top right, bottom left, bottom right.
    public static RectTransform Chamfer(Transform parent, string name, float x, float y, float width, float height,
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
    public static RectTransform Rounded(Transform parent, string name, float x, float y, float width, float height, Color fill, Color outline)
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
    public static void Diagonal(RectTransform panel, Corner corner, float cut, Color color)
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

    // Gold brackets in two corners of a picture (top left and bottom right), for cards that are a plain rectangle.
    public static void Brackets(RectTransform card, float size, Color color)
    {
        const float thickness = 2.6f;

        Bar(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(size * 0.5f, -thickness * 0.5f), new Vector2(size, thickness), color);
        Bar(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(thickness * 0.5f, -size * 0.5f), new Vector2(thickness, size), color);
        Bar(card, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-size * 0.5f, thickness * 0.5f), new Vector2(size, thickness), color);
        Bar(card, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-thickness * 0.5f, size * 0.5f), new Vector2(thickness, size), color);
    }

    private static void Bar(RectTransform parent, Vector2 anchor, Vector2 pivotUnused, Vector2 offset, Vector2 size, Color color)
    {
        Image bar = UiKit.Solid(parent, "Bracket", color);
        UiKit.Place(bar.rectTransform, anchor, new Vector2(0.5f, 0.5f), offset, size);
    }

    // A slanted bar (the stripes at the end of the header plate).
    public static void Stripe(RectTransform parent, float x, float y, float height, Color color)
    {
        Image bar = UiKit.Solid(parent, "Stripe", color);
        UiLayout.TopLeft(bar.rectTransform, x, y, 4f, height);
        bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
    }

    // ---- Buttons and small pieces ---------------------------------------------------------------------------------------------------

    // Makes a panel tappable: the same colour tint, press feedback and click sound as every other menu button.
    public static void MakeButton(RectTransform rect, UnityAction action)
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

    // A text in one of the menu fonts.
    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align, string name = "Text")
    {
        return MenuFonts.Use(UiKit.Text(parent, text, size, color, FontStyles.Normal, align, name), font);
    }

    // A picture in the middle of a panel.
    public static void Icon(RectTransform parent, Sprite sprite, float width, float height)
    {
        Image picture = UiKit.Pic(parent, "Icon", sprite);
        UiKit.Place(picture.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        picture.preserveAspect = true;
    }

    // ---- Header and footer ------------------------------------------------------------------------------------------------------------

    // The header of an inner screen: the border, the plate with the emblem, the title and a line of small text, the gold line, and BACK at the right.
    // Returns the title text (so a screen can change it).
    public static TextMeshProUGUI Header(RectTransform frame, string title, string subtitle, UnityAction onBack, out TextMeshProUGUI subtitleText)
    {
        Chamfer(frame, "ScreenBorder", 6f, 4f, 902f, 403f, 14f, 14f, 14f, 14f, Color.clear, new Color(Edge.r, Edge.g, Edge.b, 0.4f));
        Chamfer(frame, "HeaderPlate", 6f, 4f, 470f, 58f, 0f, 0f, 0f, 30f, Plate, new Color(Edge.r, Edge.g, Edge.b, 0.75f));
        Stripe(frame, 412f, 14f, 36f, Gold);
        Stripe(frame, 424f, 14f, 36f, Sky);
        Stripe(frame, 434f, 14f, 36f, new Color(Sky.r, Sky.g, Sky.b, 0.6f));

        Image logo = UiKit.Pic(frame, "Emblem", CartoonArt.MenuLogo());
        UiLayout.TopLeft(logo.rectTransform, 26f, 7f, 48f, 48f);
        logo.preserveAspect = true;

        TextMeshProUGUI titleText = Text(frame, title, 27f, Cream, MenuFonts.Title, TextAlignmentOptions.Left, "Title");
        UiLayout.TopLeft(titleText.rectTransform, 88f, 8f, 390f, 34f);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        UiLayout.Spaced(titleText, 0.05f);

        subtitleText = Text(frame, subtitle, 7.8f, Sky, MenuFonts.Body, TextAlignmentOptions.Left, "Subtitle");
        UiLayout.TopLeft(subtitleText.rectTransform, 90f, 43f, 380f, 12f);
        UiLayout.Spaced(subtitleText, 0.36f);

        Image line = UiKit.Solid(frame, "HeaderLine", new Color(Gold.r, Gold.g, Gold.b, 0.85f));
        UiLayout.TopLeft(line.rectTransform, 6f, 61f, 902f, 1.6f);

        if (onBack != null)
        {
            RectTransform back = Chamfer(frame, "Back", 0f, 0f, 96f, 38f, 9f, 9f, 9f, 9f, PanelDark, Gold);
            UiLayout.TopRight(back, 22f, 10f, 96f, 38f);
            MakeButton(back, onBack);

            TextMeshProUGUI label = Text(back, "BACK", 14f, Gold, MenuFonts.Label, TextAlignmentOptions.Center, "Label");
            UiKit.Stretch(label.rectTransform);
            UiLayout.Spaced(label, 0.08f);
        }

        return titleText;
    }

    // The footer: the plate with the reminders (cut at its upper right), the same as on the main menu.
    public static void Footer(RectTransform frame)
    {
        Chamfer(frame, "FooterPlate", 6f, 362f, 560f, 42f, 0f, 22f, 0f, 0f, Plate, new Color(Gold.r, Gold.g, Gold.b, 0.6f));

        TextMeshProUGUI reminders = Text(frame, "STAY ALERT   •   THINK BEFORE YOU CLICK   •   KEEP DATA SAFE", 8.4f, Sky, MenuFonts.Body, TextAlignmentOptions.Left, "Reminders");
        UiLayout.TopLeft(reminders.rectTransform, 34f, 377f, 520f, 13f);
        UiLayout.Spaced(reminders, 0.12f);
    }

    // ---- Fonts for screens made from the Android layouts (the tablet) --------------------------------------------------------------------

    // Bold texts (titles, buttons, labels) get Rajdhani SemiBold; the others (descriptions) get Inter Regular. Texts in the example pictures of the tablet
    // are left alone: they imitate other programs (an email, a pop-up, a login monitor).
    public static void ApplyFonts(Transform root, params Transform[] skip)
    {
        TMP_FontAsset label = MenuFonts.Label;
        TMP_FontAsset body = MenuFonts.Body;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (IsUnderAny(text.transform, skip))
                continue;

            bool bold = (text.fontStyle & FontStyles.Bold) != 0;

            if (bold && label != null)
            {
                text.font = label;
                text.fontStyle &= ~FontStyles.Bold;   // the font is already semi-bold
            }
            else if (!bold && body != null)
            {
                text.font = body;
            }
        }
    }

    private static bool IsUnderAny(Transform t, Transform[] roots)
    {
        if (roots == null)
            return false;

        for (Transform p = t; p != null; p = p.parent)
        {
            foreach (Transform skipped in roots)
            {
                if (skipped != null && p == skipped)
                    return true;
            }
        }

        return false;
    }
}

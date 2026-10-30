using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builders for the menu screens. Sizes are in "dp": the menu canvas is 800 x 360 units, like the Android layouts, so a number
// copied from an Android XML (width 310dp, textSize 14sp ...) is used as it is.
public static class UiKit
{
    // ---- Assets converted from the Android project ----------------------------------------------------------

    public static Sprite Shape(string name) { return Resources.Load<Sprite>("MenuUI/Shapes/" + name); }
    public static Sprite Picture(string name) { return Resources.Load<Sprite>("MenuUI/Images/" + name); }
    public static Sprite Icon(string name) { return Resources.Load<Sprite>("MenuUI/Icons/" + name); }

    // "#RRGGBB" or Android's "#AARRGGBB" (in Android colours the alpha comes first).
    public static Color Hex(string hex)
    {
        if (hex.Length == 9)
            hex = "#" + hex.Substring(3) + hex.Substring(1, 2);
        else if (hex.Length == 4)   // #RGB
            hex = "#" + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3];
        else if (hex.Length == 5)   // #ARGB
            hex = "#" + hex[2] + hex[2] + hex[3] + hex[3] + hex[4] + hex[4] + hex[1] + hex[1];

        return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }

    // GetComponent returns Unity's "fake null", so `??` cannot be used to add a missing component.
    public static T Ensure<T>(GameObject go) where T : Component
    {
        T found = go.GetComponent<T>();
        return found != null ? found : go.AddComponent<T>();
    }

    // ---- Rectangles -------------------------------------------------------------------------------------------

    public static RectTransform New(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Fill the parent, with optional insets (left, bottom, right, top).
    public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    // Anchor to a point of the parent (0..1), set the pivot, then the offset and size from that anchor.
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Center(RectTransform rt, float width, float height, float x = 0f, float y = 0f)
    {
        return Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));
    }

    // Fixed size inside a layout group.
    public static LayoutElement Size(Component c, float width, float height)
    {
        RectTransform rt = c.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(width, height);

        LayoutElement le = UiKit.Ensure<LayoutElement>(c.gameObject);
        if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
        if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
        return le;
    }

    public static void Flexible(Component c, float width, float height)
    {
        LayoutElement le = UiKit.Ensure<LayoutElement>(c.gameObject);
        le.flexibleWidth = width;
        le.flexibleHeight = height;
    }

    // ---- Pictures ---------------------------------------------------------------------------------------------

    public static Image Pic(Transform parent, string name, Sprite sprite, Color? color = null, bool raycast = false)
    {
        RectTransform rt = New(parent, name);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color ?? Color.white;
        image.raycastTarget = raycast;
        image.preserveAspect = false;
        return image;
    }

    public static Image Solid(Transform parent, string name, Color color)
    {
        return Pic(parent, name, null, color);
    }

    // A picture that fills its parent and crops to cover it, like scaleType="centerCrop".
    public static RawImage Cover(Transform parent, string name, Sprite sprite)
    {
        RectTransform rt = New(parent, name);
        Stretch(rt);
        RawImage raw = rt.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;

        if (sprite == null)
            Debug.LogWarning("[UiKit] Background picture for '" + name + "' was not found in Resources/MenuUI/Images. Run Tools > Firewall Duty > Menu UI > Import Android Assets.");

        if (sprite != null)
        {
            raw.texture = sprite.texture;
            rt.gameObject.AddComponent<CoverFit>().aspect = (float)sprite.texture.width / sprite.texture.height;
        }

        return raw;
    }

    // ---- Text -------------------------------------------------------------------------------------------------

    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
                                       FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.Center, string name = "Text")
    {
        RectTransform rt = New(parent, name);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    // ---- Buttons ----------------------------------------------------------------------------------------------

    public static Button Btn(Transform parent, string name, Sprite background, Color tint, string label, float labelSize, Color labelColor,
                             bool bold = true, float width = -1f, float height = -1f)
    {
        RectTransform rt = New(parent, name);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = background;
        image.type = background != null && background.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = tint;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        rt.gameObject.AddComponent<ButtonFeedback>();

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = Text(rt, label, labelSize, labelColor, bold ? FontStyles.Bold : FontStyles.Normal, TextAlignmentOptions.Center, "Label");
            Stretch(text.rectTransform, 4f, 0f, 4f, 0f);
        }

        if (width >= 0f || height >= 0f)
            Size(rt, width, height);

        return button;
    }

    // ---- Text input -------------------------------------------------------------------------------------------

    public static TMP_InputField Field(Transform parent, string name, string placeholder, bool password, Color background, Color textColor,
                                       float textSize = 14f, float width = -1f, float height = -1f, Sprite backgroundSprite = null)
    {
        RectTransform rt = New(parent, name);
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = backgroundSprite;
        image.type = backgroundSprite != null && backgroundSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = background;

        RectTransform viewport = New(rt, "Viewport");
        Stretch(viewport, 8f, 2f, 8f, 2f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI hint = Text(viewport, placeholder, textSize, new Color(textColor.r, textColor.g, textColor.b, 0.5f), FontStyles.Normal,
                                    TextAlignmentOptions.Left, "Placeholder");
        Stretch(hint.rectTransform);
        hint.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI text = Text(viewport, "", textSize, textColor, FontStyles.Normal, TextAlignmentOptions.Left, "Text");
        Stretch(text.rectTransform);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_InputField input = rt.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = hint;
        input.targetGraphic = image;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        input.caretColor = textColor;
        input.customCaretColor = true;
        input.selectionColor = new Color(0.25f, 0.5f, 0.9f, 0.4f);

        if (width >= 0f || height >= 0f)
            Size(rt, width, height);

        return input;
    }

    // ---- Check box --------------------------------------------------------------------------------------------

    public static Toggle Check(Transform parent, string name, string label, float textSize, Color textColor)
    {
        RectTransform rt = New(parent, name);
        HorizontalLayoutGroup row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 6f;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        // The whole row (box and words) can be tapped, like an Android CheckBox.
        Image hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);
        hit.raycastTarget = true;

        Image box = Pic(rt, "Box", null, Color.white, true);
        Size(box, 18f, 18f);

        TextMeshProUGUI text = Text(rt, label, textSize, textColor, FontStyles.Normal, TextAlignmentOptions.Left, "Label");

        Toggle toggle = rt.gameObject.AddComponent<Toggle>();
        toggle.isOn = false;

        // Android Material check box (theme colorPrimary = holo_blue_dark): a white outline; ticked, it fills with that blue and shows a white tick.
        StyleCheck(toggle, box, Color.white, Hex("#0099CC"));
        return toggle;
    }

    // A round check box: a ring; when ticked it fills with a colour and shows a white tick. Used by the login screen and by the
    // tablet's evidence list (where the colour is the layout's buttonTint).
    public static void StyleCheck(Toggle toggle, Image box, Color outline, Color fill)
    {
        box.sprite = RingSprite();
        box.type = Image.Type.Simple;
        box.color = outline;
        box.raycastTarget = true;

        // The fill and the tick live in one object that is shown exactly when the toggle is on. ToggleVisual follows isOn every
        // frame, so the picture is right even when the value is set from code without a click (the tablet does that when you
        // change threat).
        RectTransform mark = New(box.transform, "Mark");
        Stretch(mark);

        Image filled = Pic(mark, "Fill", CircleSprite(), fill);
        Stretch(filled.rectTransform);
        filled.type = Image.Type.Simple;

        Image tick = Pic(mark, "Tick", TickSprite(), Color.white);
        Stretch(tick.rectTransform, 2f, 2f, 2f, 2f);
        tick.type = Image.Type.Simple;

        toggle.targetGraphic = box;
        toggle.transition = Selectable.Transition.None;
        toggle.graphic = null;

        ToggleVisual visual = toggle.gameObject.AddComponent<ToggleVisual>();
        visual.toggle = toggle;
        visual.mark = mark.gameObject;
        visual.Refresh();
    }

    private static Sprite circleSprite, ringSprite, tickSprite;

    public static Sprite CircleSprite()
    {
        return circleSprite != null ? circleSprite : circleSprite = DrawSprite(64, (x, y) =>
            Mathf.Clamp01(31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f)) + 0.5f));
    }

    private static Sprite downTriangleSprite;

    // A triangle pointing down (the mission marker above a target).
    public static Sprite DownTriangleSprite()
    {
        return downTriangleSprite != null ? downTriangleSprite : downTriangleSprite = DrawSprite(64, (x, y) =>
        {
            // tip at the bottom middle (32, 4), top edge at y = 52 from x = 6 to x = 58
            float t = (y - 4f) / 48f;
            float half = 26f * Mathf.Clamp01(t);
            float inside = Mathf.Min(half - Mathf.Abs(x - 32f), Mathf.Min(y - 4f, 52f - y));
            return Mathf.Clamp01(inside + 0.5f);
        });
    }

    public static Sprite RingSprite()
    {
        return ringSprite != null ? ringSprite : ringSprite = DrawSprite(64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
            return Mathf.Clamp01(Mathf.Min(31.5f - d, d - 24f) + 0.5f);
        });
    }

    // A check mark: a short stroke down to the right, then a long one up to the right.
    public static Sprite TickSprite()
    {
        return tickSprite != null ? tickSprite : tickSprite = DrawSprite(64, (x, y) =>
        {
            Vector2 p = new Vector2(x, y);
            float d = Mathf.Min(DistanceToSegment(p, new Vector2(16f, 34f), new Vector2(27f, 22f)),
                                DistanceToSegment(p, new Vector2(27f, 22f), new Vector2(49f, 46f)));
            return Mathf.Clamp01(4f - d + 0.5f);
        });
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    // A white square picture whose transparency is given by coverage(x, y) (0..1), to be tinted.
    private static Sprite DrawSprite(int size, System.Func<float, float, float> coverage)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1f, 1f, 1f, coverage(x + 0.5f, y + 0.5f));
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---- Layout boxes -----------------------------------------------------------------------------------------

    public static RectTransform VBox(Transform parent, string name, float spacing = 0f, TextAnchor align = TextAnchor.UpperCenter,
                                     bool fitWidth = false, bool fitHeight = true, int padding = 0)
    {
        RectTransform rt = New(parent, name);
        VerticalLayoutGroup group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        group.spacing = spacing;
        group.childAlignment = align;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.padding = new RectOffset(padding, padding, padding, padding);

        ContentSizeFitter fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitWidth ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = fitHeight ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        return rt;
    }

    public static RectTransform HBox(Transform parent, string name, float spacing = 0f, TextAnchor align = TextAnchor.MiddleCenter,
                                     bool fitWidth = true, bool fitHeight = true, int padding = 0)
    {
        RectTransform rt = New(parent, name);
        HorizontalLayoutGroup group = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = spacing;
        group.childAlignment = align;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.padding = new RectOffset(padding, padding, padding, padding);

        ContentSizeFitter fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitWidth ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = fitHeight ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        return rt;
    }

    public static void Space(Transform parent, float height)
    {
        RectTransform rt = New(parent, "Space");
        Size(rt, 0f, height);
    }

    // A vertical scroll area. Returns the content to add to (it grows to fit what is added).
    public static RectTransform Scroll(Transform parent, string name, float spacing = 6f, int padding = 0, TextAnchor align = TextAnchor.UpperCenter)
    {
        RectTransform root = New(parent, name);
        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 20f;

        RectTransform viewport = New(root, "Viewport");
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image catcher = viewport.gameObject.AddComponent<Image>();
        catcher.color = new Color(0, 0, 0, 0);   // lets a drag that starts on empty space still scroll

        RectTransform content = VBox(viewport, "Content", spacing, align, false, true, padding);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, 0f);

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }
}

// A small press effect on buttons (like Android's button_click animation).
public class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Vector3 normalScale = Vector3.one;
    private bool captured;

    public void OnPointerDown(PointerEventData eventData)
    {
        GameAudio.PlayClick();

        if (!captured)
        {
            normalScale = transform.localScale;
            captured = true;
        }

        transform.localScale = normalScale * 0.96f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Restore();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Restore();
    }

    private void Restore()
    {
        if (captured)
            transform.localScale = normalScale;
    }

    private void OnDisable()
    {
        Restore();
    }
}

// Makes a picture cover its parent completely (crop, no stretch), like scaleType="centerCrop". It sizes itself from the parent's
// current size every frame, so it is right on the first frame and after the screen changes shape.
public class CoverFit : MonoBehaviour
{
    public float aspect = 1f;

    private void OnEnable()
    {
        Fit();
    }

    private void LateUpdate()
    {
        Fit();
    }

    private void Fit()
    {
        RectTransform self = (RectTransform)transform;
        RectTransform parent = transform.parent as RectTransform;
        if (parent == null || aspect <= 0f)
            return;

        float width = parent.rect.width, height = parent.rect.height;
        if (width <= 0f || height <= 0f)
            return;

        float w = width, h = width / aspect;
        if (h < height)
        {
            h = height;
            w = height * aspect;
        }

        self.anchorMin = self.anchorMax = new Vector2(0.5f, 0.5f);
        self.pivot = new Vector2(0.5f, 0.5f);
        self.anchoredPosition = Vector2.zero;
        self.sizeDelta = new Vector2(w, h);
    }
}

// Shows or hides the ticked look of a check box to match Toggle.isOn at all times.
public class ToggleVisual : MonoBehaviour
{
    public Toggle toggle;
    public GameObject mark;

    private void OnEnable()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (toggle != null && mark != null && mark.activeSelf != toggle.isOn)
            mark.SetActive(toggle.isOn);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reads an Android layout XML (copied from the Android Studio project by "Import Android Assets") and builds it as Unity UI,
// laying it out the way Android does (LinearLayout with weights and gravity, FrameLayout, a useful part of ConstraintLayout,
// wrap_content text, margins, padding, translationX/Y). The Cyber Tablet and its six example screens are built this way, so they are
// the Android layouts themselves rather than a copy by eye.
//
//     AndroidLayout.View root = AndroidLayout.Inflate(parentRectTransform, "activity_tablet");
//     root.Find("txtThreatTitle").SetText("2. MALWARE FILES");
//     root.Relayout();
public static class AndroidLayout
{
    private const float Unbounded = 100000f;

    private enum Mode { Exact, AtMost, Unspecified }

    private struct Spec
    {
        public Mode mode;
        public float size;
        public Spec(Mode mode, float size) { this.mode = mode; this.size = size; }
    }

    [Flags]
    private enum Gravity { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8, CenterH = 16, CenterV = 32 }

    public static View Inflate(RectTransform parent, string layoutName)
    {
        TextAsset asset = Resources.Load<TextAsset>("MenuUI/Layouts/" + layoutName);
        if (asset == null)
        {
            Debug.LogError("[AndroidLayout] Layout '" + layoutName + "' is missing. Run Tools > Firewall Duty > Menu UI > Import Android Assets.");
            return null;
        }

        XmlDocument document = new XmlDocument();
        document.LoadXml(asset.text);

        View root = Parse(document.DocumentElement, null);
        root.Create(parent);
        return root;
    }

    // Inflates another layout and adds it inside a container of an existing layout (LayoutInflater.inflate + addView).
    public static View InflateInto(View container, string layoutName)
    {
        TextAsset asset = Resources.Load<TextAsset>("MenuUI/Layouts/" + layoutName);
        if (asset == null)
        {
            Debug.LogError("[AndroidLayout] Layout '" + layoutName + "' is missing.");
            return null;
        }

        XmlDocument document = new XmlDocument();
        document.LoadXml(asset.text);

        View view = Parse(document.DocumentElement, container);
        container.children.Add(view);
        view.Create(container.rect);
        return view;
    }

    // ---- Reading the XML ----------------------------------------------------------------------------------------

    private static View Parse(XmlElement element, View parent)
    {
        string name = element.LocalName;
        View view = new View { tag = name.Substring(name.LastIndexOf('.') + 1), parent = parent };
        if (view.tag == "ImageButton")
            view.tag = "ImageView";

        foreach (XmlAttribute attribute in element.Attributes)
        {
            if (attribute.Prefix == "xmlns" || attribute.Name == "xmlns")
                continue;

            view.attributes[attribute.LocalName] = attribute.Value;
        }

        if (view.attributes.TryGetValue("id", out string id))
            view.id = id.Substring(id.LastIndexOf('/') + 1);

        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is XmlElement childElement)
                view.children.Add(Parse(childElement, view));
        }

        return view;
    }

    // ---- A view -------------------------------------------------------------------------------------------------

    public class View
    {
        public string tag;
        public string id;
        public View parent;
        public readonly Dictionary<string, string> attributes = new Dictionary<string, string>();
        public readonly List<View> children = new List<View>();

        public RectTransform rect;
        public TextMeshProUGUI text;
        public Image background;
        public Toggle toggle;
        public Slider slider;             // a SeekBar
        private Image icon;               // an ImageView with a background keeps its picture in a child
        private RectTransform iconRect;
        private Image progressFill;

        private bool gone;
        private string currentBackground;
        private float measuredWidth, measuredHeight;
        private float left, top, right, bottom;      // after layout, inside the parent's box
        private bool resolved, resolving;
        private float contentHeight;                 // ScrollView: height of its content

        // ---- Public helpers used by screens ----

        public View Find(string wanted)
        {
            if (id == wanted)
                return this;

            foreach (View child in children)
            {
                View found = child.Find(wanted);
                if (found != null)
                    return found;
            }

            return null;
        }

        public void SetText(string value)
        {
            if (text != null)
                text.text = Sanitize(text, value);
        }

        public void SetBackgroundColor(Color color)
        {
            if (background != null)
                background.color = color;
        }

        // 0..1 for a ProgressBar.
        public void SetProgress(float fraction)
        {
            if (progressFill != null)
                progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        public void ScrollToTop()
        {
            ScrollRect scroll = rect != null ? rect.GetComponent<ScrollRect>() : null;
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        // Adds a view to the end of this one while the screen is running (for things the Android layout does not have), e.g. a button.
        public View AddView(string viewTag, params string[] keysAndValues)
        {
            View view = new View { tag = viewTag, parent = this };

            for (int i = 0; i + 1 < keysAndValues.Length; i += 2)
                view.attributes[keysAndValues[i]] = keysAndValues[i + 1];

            children.Add(view);
            view.BuildObjects(tag == "ScrollView" ? scrollContent : rect);
            return view;
        }

        public void SetFontSize(float size)
        {
            if (text != null)
                text.fontSize = size;
        }

        public void SetTextColor(Color color)
        {
            if (text != null)
                text.color = color;
        }

        public void SetVisible(bool visible)
        {
            gone = !visible;
            ApplyVisibility();
        }

        public bool IsVisible => !gone;

        // Hidden views stay active (text must be alive to be measured) but are invisible and ignore touches.
        private void ApplyVisibility()
        {
            if (rect == null)
                return;

            CanvasGroup group = UiKit.Ensure<CanvasGroup>(rect.gameObject);
            group.alpha = gone ? 0f : 1f;
            group.blocksRaycasts = !gone;
            group.interactable = !gone;
        }

        public void SetBackground(string drawableOrColor)
        {
            currentBackground = drawableOrColor;
            ApplyBackground();
        }

        public void OnClick(UnityEngine.Events.UnityAction action)
        {
            Image hit = rect.GetComponent<Image>();
            if (hit == null)
            {
                hit = rect.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                background = hit;
            }

            hit.raycastTarget = true;
            Button button = UiKit.Ensure<Button>(rect.gameObject);
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.onClick.AddListener(GameAudio.PlayClick);
        }

        // Positions everything again, after text or visibility changed.
        public void Relayout()
        {
            View root = this;
            while (root.parent != null)
                root = root.parent;

            RectTransform host = (RectTransform)root.rect.parent;
            root.DoLayout(host.rect.width, host.rect.height);
            root.Apply();
        }

        // ---- Building the Unity objects ----

        internal void Create(RectTransform host)
        {
            BuildObjects(host);

            // The size of the host: the layout fills it, like setContentView.
            Canvas.ForceUpdateCanvases();
            DoLayout(host.rect.width, host.rect.height);
            Apply();
        }

        private void BuildObjects(RectTransform host)
        {
            GameObject go = new GameObject(tag + (id != null ? "_" + id : ""), typeof(RectTransform));
            rect = (RectTransform)go.transform;
            rect.SetParent(host, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            string visibility = Attr("visibility");
            if (visibility == "gone" || visibility == "invisible")
            {
                gone = true;
                ApplyVisibility();
            }

            currentBackground = Attr("background");
            if (tag == "CardView")
                currentBackground = null;
            ApplyBackground();

            if (tag == "CardView")
            {
                float radius = Dp(Attr("cardCornerRadius"));
                string color = Attr("cardBackgroundColor") ?? "#FFFFFF";
                Image card = rect.gameObject.AddComponent<Image>();
                card.sprite = RoundedSprite(Mathf.Max(1, Mathf.RoundToInt(radius)));
                card.type = Image.Type.Sliced;
                card.color = ParseColor(color);
                card.raycastTarget = false;
                background = card;
            }

            switch (tag)
            {
                case "TextView":
                case "Button":
                    if (!BuildArrow())
                        BuildText();
                    break;
                case "CheckBox":
                    BuildCheckBox();
                    break;
                case "ImageView":
                    BuildImage();
                    break;
                case "ProgressBar":
                    BuildProgress();
                    break;
                case "SeekBar":
                    BuildSeekBar();
                    break;
                case "ScrollView":
                    BuildScroll();
                    break;
            }

            foreach (View child in children)
                child.BuildObjects(tag == "ScrollView" ? ScrollContent : rect);
        }

        private RectTransform scrollContent;
        private RectTransform ScrollContent => scrollContent;

        // A TextView that only holds an arrow character (the red pointers in the example screens) is drawn as a real arrow:
        // the game font has no arrow glyphs, and a drawn one keeps its size and colour.
        private bool BuildArrow()
        {
            string value = Attr("text");
            if (string.IsNullOrEmpty(value) || value.Trim().Length != 1)
                return false;

            float rotation;
            bool ring = false;
            Sprite glyph = null;   // a drawn picture for characters the font cannot show (house, list, info)
            switch (value.Trim()[0])
            {
                case '⌂': rotation = 0f; glyph = CartoonArt.HouseIcon(); break;   // the tablet's HOME tab
                case '▤': rotation = 0f; glyph = CartoonArt.ListIcon(); break;    // the tablet's EXAMPLES tab
                case 'ⓘ': rotation = 0f; glyph = CartoonArt.InfoIcon(); break;    // the "i" beside each piece of evidence
                case '○': rotation = 0f; ring = true; break;       // the tablet's round home button
                case '←': case '⟵': rotation = 0f; break;     // left
                case '→': case '⟶': rotation = 180f; break;   // right
                case '↑': rotation = -90f; break;                  // up
                case '↓': rotation = 90f; break;                   // down
                default: return false;
            }

            GameObject go = new GameObject("Arrow", typeof(RectTransform));
            RectTransform tr = (RectTransform)go.transform;
            tr.SetParent(rect, false);
            // An arrow character sits lower in its text box than the box's middle, and the layouts raise it by about 10 (translationY)
            // to line it up with its label. Drawn from the middle of the box, so it is lowered by the same amount.
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = ring ? new Vector2(20f, 20f) : glyph != null ? new Vector2(15f, 15f) : new Vector2(30f, 15f);
            tr.anchoredPosition = ring || glyph != null || Mathf.Abs(rotation) > 1f && Mathf.Abs(rotation) < 179f ? Vector2.zero : new Vector2(0f, -10f);
            tr.localRotation = Quaternion.Euler(0f, 0f, rotation);

            Image arrow = go.AddComponent<Image>();
            arrow.sprite = ring ? RingSprite() : glyph != null ? glyph : ArrowSprite();
            arrow.preserveAspect = false;
            arrow.raycastTarget = false;
            arrow.color = ParseColor(Attr("textColor") ?? "#FF3030");
            return true;
        }

        private void BuildText()
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            RectTransform tr = (RectTransform)go.transform;
            tr.SetParent(rect, false);
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.fontSize = Dp(Attr("textSize") ?? "14");
            text.color = ParseColor(Attr("textColor") ?? "#FFFFFF");

            string style = Attr("textStyle");
            FontStyles fontStyle = FontStyles.Normal;
            if (style != null && style.Contains("bold")) fontStyle |= FontStyles.Bold;
            if (style != null && style.Contains("italic")) fontStyle |= FontStyles.Italic;
            // Android's Button defaults to capital letters; the tablet's own text is already capitals where wanted.
            text.fontStyle = fontStyle;

            float spacing = Float(Attr("letterSpacing"));
            if (spacing > 0f)
                text.characterSpacing = spacing * 100f;

            float extra = Dp(Attr("lineSpacingExtra"));
            if (extra > 0f)
                text.lineSpacing = extra * 100f / Mathf.Max(1f, text.fontSize);

            string lines = Attr("maxLines");
            if (lines != null && int.TryParse(lines, out int max))
            {
                text.maxVisibleLines = max;
                if (Attr("ellipsize") == "end")
                    text.overflowMode = TextOverflowModes.Ellipsis;
                if (max == 1)
                    text.textWrappingMode = TextWrappingModes.NoWrap;
            }

            text.alignment = TextAlignment(ParseGravity(Attr("gravity"), tag == "Button" ? Gravity.CenterH | Gravity.CenterV : Gravity.Left | Gravity.Top));
            text.text = Sanitize(text, Attr("text") ?? "");
        }

        private void BuildCheckBox()
        {
            // Round, like the login check box; the layout's buttonTint is the colour of the ring and, when ticked, of the fill.
            Image box = UiKit.Ensure<Image>(rect.gameObject);
            background = box;

            toggle = UiKit.Ensure<Toggle>(rect.gameObject);
            toggle.isOn = false;

            Color tint = ParseColor(Attr("buttonTint") ?? "#061F34");
            UiKit.StyleCheck(toggle, box, tint, tint);
        }

        // A SeekBar: a track, a fill in progressTint and a round thumb in thumbTint (UiWidgets.SeekBar), over the whole view.
        private void BuildSeekBar()
        {
            int max = (int)Attributes("max", 100f);
            int progress = (int)Attributes("progress", 0f);

            slider = UiWidgets.SeekBar(rect, "SeekBar", 0, max, progress,
                                       ParseColor("#4A6B85"), ParseColor(Attr("progressTint") ?? "#42A5F5"),
                                       ParseColor(Attr("thumbTint") ?? "#90CAF9"), 40f);

            RectTransform sliderRect = (RectTransform)slider.transform;
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
        }

        // A horizontal progress bar: a track and a fill that grows from the left (SetProgress).
        private void BuildProgress()
        {
            Image track = UiKit.Ensure<Image>(rect.gameObject);
            track.color = ParseColor(Attr("progressBackgroundTint") ?? "#35536A");
            track.raycastTarget = false;
            background = track;
            rect.gameObject.AddComponent<RectMask2D>();

            GameObject go = new GameObject("Fill", typeof(RectTransform));
            RectTransform tr = (RectTransform)go.transform;
            tr.SetParent(rect, false);
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(0f, 1f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            progressFill = go.AddComponent<Image>();
            progressFill.color = ParseColor(Attr("progressTint") ?? "#29B6F6");
            progressFill.raycastTarget = false;
        }

        private void BuildImage()
        {
            if (Attr("background") != null)
            {
                // Background on this view (made by ApplyBackground), the picture on top of it, inset by the padding.
                Sprite picture = FindSprite(Attr("src"));
                if (picture != null)
                {
                    GameObject go = new GameObject("Icon", typeof(RectTransform));
                    iconRect = (RectTransform)go.transform;
                    iconRect.SetParent(rect, false);
                    iconRect.anchorMin = Vector2.zero;
                    iconRect.anchorMax = Vector2.one;
                    icon = go.AddComponent<Image>();
                    icon.sprite = picture;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    icon.color = Attr("tint") != null ? ParseColor(Attr("tint")) : Color.white;
                }

                return;
            }

            Image image = UiKit.Ensure<Image>(rect.gameObject);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.sprite = FindSprite(Attr("src"));
            image.color = Attr("tint") != null ? ParseColor(Attr("tint")) : Color.white;
            image.enabled = image.sprite != null;
            background = image;
        }

        private void BuildScroll()
        {
            ScrollRect scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 20f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            RectTransform vr = (RectTransform)viewport.transform;
            vr.SetParent(rect, false);
            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero;
            vr.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            GameObject content = new GameObject("Content", typeof(RectTransform));
            scrollContent = (RectTransform)content.transform;
            scrollContent.SetParent(vr, false);
            scrollContent.anchorMin = new Vector2(0f, 1f);
            scrollContent.anchorMax = new Vector2(1f, 1f);
            scrollContent.pivot = new Vector2(0f, 1f);

            scroll.viewport = vr;
            scroll.content = scrollContent;
        }

        private void ApplyBackground()
        {
            string value = currentBackground;
            if (value == null || tag == "CardView" || tag == "CheckBox")
                return;

            Sprite sprite = null;
            Color color = Color.white;
            bool isColor = value.StartsWith("#") || value == "@color/transparent";

            if (value == "@color/transparent")
                color = Color.clear;
            else if (value.StartsWith("#"))
                color = ParseColor(value);
            else
                sprite = FindSprite(value);

            if (background == null)
            {
                // Behind everything else in this view.
                background = rect.GetComponent<Image>();
                if (background == null)
                    background = rect.gameObject.AddComponent<Image>();
            }

            background.sprite = isColor ? null : sprite;
            background.type = !isColor && sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            background.color = color;
            background.raycastTarget = rect.GetComponent<Button>() != null;   // a clickable view keeps catching touches
        }

        // ---- Reading attributes ----

        private string Attr(string name)
        {
            return attributes.TryGetValue(name, out string value) ? value.Trim() : null;
        }

        // ---- Layout --------------------------------------------------------------------------------------------

        // The layout fills the screen it is given.
        private void DoLayout(float width, float height)
        {
            ResetResolved();
            Measure(new Spec(Mode.Exact, width), new Spec(Mode.Exact, height));
            Place(0f, 0f, measuredWidth, measuredHeight);
        }

        private void ResetResolved()
        {
            resolved = false;
            resolving = false;
            foreach (View child in children)
                child.ResetResolved();
        }

        private bool IsGone => gone;

        private float MarginStart => Dp(Attr("layout_marginStart") ?? Attr("layout_marginLeft") ?? Attr("layout_margin"));
        private float MarginEnd => Dp(Attr("layout_marginEnd") ?? Attr("layout_marginRight") ?? Attr("layout_margin"));
        private float MarginTop => Dp(Attr("layout_marginTop") ?? Attr("layout_margin"));
        private float MarginBottom => Dp(Attr("layout_marginBottom") ?? Attr("layout_margin"));

        private float PadStart => Dp(Attr("paddingStart") ?? Attr("paddingLeft") ?? Attr("padding"));
        private float PadEnd => Dp(Attr("paddingEnd") ?? Attr("paddingRight") ?? Attr("padding"));
        private float PadTop => Dp(Attr("paddingTop") ?? Attr("padding"));
        private float PadBottom => Dp(Attr("paddingBottom") ?? Attr("padding"));

        private string Dimension(bool horizontal)
        {
            return Attr(horizontal ? "layout_width" : "layout_height") ?? "wrap_content";
        }

        private bool IsMatch(bool horizontal) { return Dimension(horizontal) == "match_parent" || Dimension(horizontal) == "fill_parent"; }
        private bool IsWrap(bool horizontal) { return Dimension(horizontal) == "wrap_content"; }
        private float Weight => Float(Attr("layout_weight"));

        // The spec a child gets from its parent's spec.
        private static Spec ChildSpec(Spec parent, float used, string dimension)
        {
            float available = Mathf.Max(0f, parent.size - used);

            if (dimension == "match_parent" || dimension == "fill_parent")
                return parent.mode == Mode.Unspecified ? new Spec(Mode.Unspecified, 0f) : new Spec(parent.mode == Mode.Exact ? Mode.Exact : Mode.AtMost, available);

            if (dimension == "wrap_content")
                return parent.mode == Mode.Unspecified ? new Spec(Mode.Unspecified, 0f) : new Spec(Mode.AtMost, available);

            return new Spec(Mode.Exact, Dp(dimension));
        }

        private static float Resolve(Spec spec, float content)
        {
            switch (spec.mode)
            {
                case Mode.Exact: return spec.size;
                case Mode.AtMost: return Mathf.Min(content, spec.size);
                default: return content;
            }
        }

        private void Measure(Spec widthSpec, Spec heightSpec)
        {
            if (tag == "Guideline")
            {
                measuredWidth = 0f;
                measuredHeight = 0f;
                return;
            }

            float contentWidth = 0f, contentHeight = 0f;
            float padW = PadStart + PadEnd, padH = PadTop + PadBottom;

            switch (tag)
            {
                case "TextView":
                case "Button":
                case "CheckBox":
                {
                    if (text != null)
                    {
                        float room = widthSpec.mode == Mode.Unspecified ? Unbounded : Mathf.Max(1f, widthSpec.size - padW);
                        Vector2 preferred = text.GetPreferredValues(text.text, room, Unbounded);
                        contentWidth = Mathf.Min(preferred.x, room);
                        contentHeight = preferred.y;

                        float minHeight = Dp(Attr("minHeight"));
                        contentHeight = Mathf.Max(contentHeight, minHeight - padH);
                    }

                    break;
                }

                case "ImageView":
                {
                    Sprite sprite = icon != null ? icon.sprite : background != null ? background.sprite : null;
                    if (sprite != null)
                    {
                        contentWidth = sprite.rect.width;
                        contentHeight = sprite.rect.height;
                    }

                    break;
                }

                case "LinearLayout":
                    MeasureLinear(widthSpec, heightSpec, out contentWidth, out contentHeight);
                    break;

                case "ScrollView":
                    MeasureScroll(widthSpec, heightSpec, out contentWidth, out contentHeight);
                    break;

                default:
                    // FrameLayout, CardView, ConstraintLayout and anything else that holds views.
                    if (tag.EndsWith("ConstraintLayout"))
                        MeasureConstraint(widthSpec, heightSpec, out contentWidth, out contentHeight);
                    else
                        MeasureFrame(widthSpec, heightSpec, out contentWidth, out contentHeight);
                    break;
            }

            measuredWidth = Resolve(widthSpec, contentWidth + padW);
            measuredHeight = Resolve(heightSpec, contentHeight + padH);

            float maxW = Dp(Attr("maxWidth"));
            if (maxW > 0f)
                measuredWidth = Mathf.Min(measuredWidth, maxW);

            float minW = Dp(Attr("minWidth"));
            float minH = Dp(Attr("minHeight"));
            if (widthSpec.mode != Mode.Exact) measuredWidth = Mathf.Max(measuredWidth, minW);
            if (heightSpec.mode != Mode.Exact) measuredHeight = Mathf.Max(measuredHeight, minH);
        }

        // ---- LinearLayout

        private bool Vertical => Attr("orientation") == "vertical";

        private void MeasureLinear(Spec widthSpec, Spec heightSpec, out float contentWidth, out float contentHeight)
        {
            bool vertical = Vertical;
            float padW = PadStart + PadEnd, padH = PadTop + PadBottom;
            Spec inner = new Spec(widthSpec.mode, widthSpec.size - padW);
            Spec innerH = new Spec(heightSpec.mode, heightSpec.size - padH);

            float totalWeight = 0f, used = 0f, cross = 0f;
            List<View> weighted = new List<View>();

            foreach (View child in children)
            {
                if (child.IsGone) continue;

                float marginMain = vertical ? child.MarginTop + child.MarginBottom : child.MarginStart + child.MarginEnd;
                float marginCross = vertical ? child.MarginStart + child.MarginEnd : child.MarginTop + child.MarginBottom;
                bool childWeighted = child.Weight > 0f && child.Dimension(!vertical) == "0dp";

                if (childWeighted && (vertical ? innerH.mode : inner.mode) != Mode.Unspecified)
                {
                    totalWeight += child.Weight;
                    used += marginMain;
                    weighted.Add(child);

                    // Measured for its cross size now; its main size comes later.
                    if (vertical)
                        child.Measure(ChildSpec(inner, child.MarginStart + child.MarginEnd, child.Dimension(true)), new Spec(Mode.Unspecified, 0f));
                    else
                        child.Measure(new Spec(Mode.Unspecified, 0f), ChildSpec(innerH, child.MarginTop + child.MarginBottom, child.Dimension(false)));

                    cross = Mathf.Max(cross, (vertical ? child.measuredWidth : child.measuredHeight) + marginCross);
                    continue;
                }

                Spec childWidth = vertical
                    ? ChildSpec(inner, child.MarginStart + child.MarginEnd, child.Dimension(true))
                    : ChildSpec(new Spec(inner.mode, inner.size), 0f, child.Dimension(true));
                Spec childHeight = vertical
                    ? ChildSpec(new Spec(innerH.mode, innerH.size), 0f, child.Dimension(false))
                    : ChildSpec(innerH, child.MarginTop + child.MarginBottom, child.Dimension(false));

                // Along the main axis a child may use what is left.
                if (vertical)
                    childHeight = ChildSpec(new Spec(innerH.mode, innerH.size), used + child.MarginTop + child.MarginBottom, child.Dimension(false));
                else
                    childWidth = ChildSpec(new Spec(inner.mode, inner.size), used + child.MarginStart + child.MarginEnd, child.Dimension(true));

                child.Measure(childWidth, childHeight);

                used += (vertical ? child.measuredHeight : child.measuredWidth) + marginMain;
                cross = Mathf.Max(cross, (vertical ? child.measuredWidth : child.measuredHeight) + marginCross);
            }

            // Share what is left among the weighted children.
            if (weighted.Count > 0)
            {
                float shareOf = Attributes("weightSum", 0f) > 0f ? Attributes("weightSum", 0f) : totalWeight;
                Spec mainSpec = vertical ? innerH : inner;
                float left = Mathf.Max(0f, mainSpec.size - used);

                foreach (View child in weighted)
                {
                    float length = left * child.Weight / shareOf;
                    float marginCross = vertical ? child.MarginStart + child.MarginEnd : child.MarginTop + child.MarginBottom;

                    if (vertical)
                        child.Measure(ChildSpec(inner, child.MarginStart + child.MarginEnd, child.Dimension(true)), new Spec(Mode.Exact, length));
                    else
                        child.Measure(new Spec(Mode.Exact, length), ChildSpec(innerH, child.MarginTop + child.MarginBottom, child.Dimension(false)));

                    cross = Mathf.Max(cross, (vertical ? child.measuredWidth : child.measuredHeight) + marginCross);
                }

                used = mainSpec.size;
            }

            contentWidth = vertical ? cross : used;
            contentHeight = vertical ? used : cross;
        }

        // ---- FrameLayout and CardView

        private void MeasureFrame(Spec widthSpec, Spec heightSpec, out float contentWidth, out float contentHeight)
        {
            float padW = PadStart + PadEnd, padH = PadTop + PadBottom;
            Spec inner = new Spec(widthSpec.mode, widthSpec.size - padW);
            Spec innerH = new Spec(heightSpec.mode, heightSpec.size - padH);

            contentWidth = 0f;
            contentHeight = 0f;

            foreach (View child in children)
            {
                if (child.IsGone) continue;

                child.Measure(
                    ChildSpec(inner, child.MarginStart + child.MarginEnd, child.Dimension(true)),
                    ChildSpec(innerH, child.MarginTop + child.MarginBottom, child.Dimension(false)));

                contentWidth = Mathf.Max(contentWidth, child.measuredWidth + child.MarginStart + child.MarginEnd);
                contentHeight = Mathf.Max(contentHeight, child.measuredHeight + child.MarginTop + child.MarginBottom);
            }
        }

        // ---- ScrollView

        private void MeasureScroll(Spec widthSpec, Spec heightSpec, out float contentWidth, out float contentHeight)
        {
            contentWidth = 0f;
            contentHeight = 0f;
            this.contentHeight = 0f;

            foreach (View child in children)
            {
                if (child.IsGone) continue;

                child.Measure(
                    ChildSpec(new Spec(widthSpec.mode, widthSpec.size - PadStart - PadEnd), child.MarginStart + child.MarginEnd, child.Dimension(true)),
                    new Spec(Mode.Unspecified, 0f));

                contentWidth = Mathf.Max(contentWidth, child.measuredWidth + child.MarginStart + child.MarginEnd);
                contentHeight = child.measuredHeight + child.MarginTop + child.MarginBottom;
                this.contentHeight = contentHeight + PadTop + PadBottom;
            }
        }

        // ---- ConstraintLayout (what the Android layouts use: parent / view / guideline anchors, 0dp between them)

        private void MeasureConstraint(Spec widthSpec, Spec heightSpec, out float contentWidth, out float contentHeight)
        {
            // These layouts are always match_parent or 0dp, so the size is decided by the parent; only wrap_content needs a guess.
            contentWidth = widthSpec.mode == Mode.Unspecified ? 0f : widthSpec.size;
            contentHeight = heightSpec.mode == Mode.Unspecified ? 0f : heightSpec.size;
        }

        private void PlaceConstraint(float width, float height)
        {
            foreach (View child in children)
            {
                child.resolved = false;
                child.resolving = false;
            }

            foreach (View child in children)
                child.ResolveConstraints(this, width, height);

            foreach (View child in children)
            {
                if (!child.IsGone)
                    child.Place(child.left, child.top, child.measuredWidth, child.measuredHeight);
            }
        }

        private void ResolveConstraints(View layout, float parentWidth, float parentHeight)
        {
            if (resolved || resolving)
                return;

            resolving = true;

            if (IsGone)
            {
                left = right = top = bottom = 0f;
                resolved = true;
                resolving = false;
                return;
            }

            if (tag == "Guideline")
            {
                float percent = Float(Attr("layout_constraintGuide_percent"));
                bool verticalLine = Attr("orientation") == "vertical";
                left = right = verticalLine ? percent * parentWidth : 0f;
                top = bottom = verticalLine ? 0f : percent * parentHeight;
                resolved = true;
                resolving = false;
                return;
            }

            // Horizontal
            bool hasStart = TryAnchor(layout, parentWidth, parentHeight, true, true, out float startAnchor);
            bool hasEnd = TryAnchor(layout, parentWidth, parentHeight, true, false, out float endAnchor);

            float x, w;
            if (Dimension(true) == "0dp" && hasStart && hasEnd)
            {
                x = startAnchor + MarginStart;
                w = Mathf.Max(0f, endAnchor - MarginEnd - x);
            }
            else
            {
                Measure(ConstraintSpec(Dimension(true), hasStart && hasEnd ? endAnchor - startAnchor - MarginStart - MarginEnd : parentWidth),
                        new Spec(Mode.Unspecified, 0f));
                w = measuredWidth;

                if (hasStart && hasEnd) x = startAnchor + MarginStart + (endAnchor - MarginEnd - (startAnchor + MarginStart) - w) * 0.5f;
                else if (hasStart) x = startAnchor + MarginStart;
                else if (hasEnd) x = endAnchor - MarginEnd - w;
                else x = MarginStart;
            }

            // Vertical (the width is known now, so wrapped text has its true height)
            bool hasTop = TryAnchor(layout, parentWidth, parentHeight, false, true, out float topAnchor);
            bool hasBottom = TryAnchor(layout, parentWidth, parentHeight, false, false, out float bottomAnchor);

            float y, h;
            if (Dimension(false) == "0dp" && hasTop && hasBottom)
            {
                y = topAnchor + MarginTop;
                h = Mathf.Max(0f, bottomAnchor - MarginBottom - y);
            }
            else
            {
                Measure(new Spec(Mode.Exact, w), ConstraintSpec(Dimension(false), hasTop && hasBottom ? bottomAnchor - topAnchor - MarginTop - MarginBottom : parentHeight));
                h = measuredHeight;

                float bias = Attributes("layout_constraintVertical_bias", 0.5f);
                if (hasTop && hasBottom) y = topAnchor + MarginTop + (bottomAnchor - MarginBottom - (topAnchor + MarginTop) - h) * bias;
                else if (hasTop) y = topAnchor + MarginTop;
                else if (hasBottom) y = bottomAnchor - MarginBottom - h;
                else y = MarginTop;
            }

            // Measure again at the final size so children know their box.
            Measure(new Spec(Mode.Exact, w), new Spec(Mode.Exact, h));

            left = x;
            top = y;
            right = x + w;
            bottom = y + h;
            measuredWidth = w;
            measuredHeight = h;
            resolved = true;
            resolving = false;
        }

        private float Attributes(string name, float fallback)
        {
            string value = Attr(name);
            return value != null && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;
        }

        private static Spec ConstraintSpec(string dimension, float available)
        {
            if (dimension == "match_parent") return new Spec(Mode.Exact, available);
            if (dimension == "wrap_content" || dimension == "0dp") return new Spec(Mode.AtMost, Mathf.Max(0f, available));
            return new Spec(Mode.Exact, Dp(dimension));
        }

        // Where this view's edge is pinned to. The constraint names are "layout_constraint<Side>_to<Side>Of".
        private bool TryAnchor(View layout, float parentWidth, float parentHeight, bool horizontal, bool first, out float position)
        {
            string[] sides = horizontal ? (first ? new[] { "Start", "Left" } : new[] { "End", "Right" })
                                        : (first ? new[] { "Top" } : new[] { "Bottom" });
            string[] targets = horizontal ? new[] { "Start", "End", "Left", "Right" } : new[] { "Top", "Bottom" };

            foreach (string side in sides)
            {
                foreach (string target in targets)
                {
                    string reference = Attr("layout_constraint" + side + "_to" + target + "Of");
                    if (reference == null)
                        continue;

                    float value;
                    bool targetIsFirstEdge = target == "Start" || target == "Left" || target == "Top";

                    if (reference == "parent")
                    {
                        float size = horizontal ? parentWidth : parentHeight;
                        value = targetIsFirstEdge ? 0f : size;
                    }
                    else
                    {
                        View other = layout.Find(reference.Substring(reference.LastIndexOf('/') + 1));
                        if (other == null)
                            continue;

                        other.ResolveConstraints(layout, parentWidth, parentHeight);
                        if (!other.resolved)
                            continue;

                        value = horizontal ? (targetIsFirstEdge ? other.left : other.right)
                                           : (targetIsFirstEdge ? other.top : other.bottom);
                    }

                    position = value;
                    return true;
                }
            }

            position = 0f;
            return false;
        }

        // ---- Placing the children (after measuring) ----

        private void Place(float l, float t, float width, float height)
        {
            left = l;
            top = t;
            right = l + width;
            bottom = t + height;
            measuredWidth = width;
            measuredHeight = height;

            float innerLeft = PadStart, innerTop = PadTop;
            float innerWidth = width - PadStart - PadEnd, innerHeight = height - PadTop - PadBottom;

            if (tag.EndsWith("ConstraintLayout"))
            {
                PlaceConstraint(width, height);
                return;
            }

            if (tag == "LinearLayout")
            {
                PlaceLinear(innerLeft, innerTop, innerWidth, innerHeight);
                return;
            }

            if (tag == "ScrollView")
            {
                foreach (View child in children)
                {
                    if (child.IsGone) continue;
                    // x_center="true" (added here, not in Android): content shorter than the view sits in the middle of it.
                    float spare = Attr("x_center") == "true" ? Mathf.Max(0f, (height - contentHeight) * 0.5f) : 0f;
                    child.Place(PadStart + child.MarginStart, PadTop + child.MarginTop + spare, child.measuredWidth, child.measuredHeight);
                }

                return;
            }

            // FrameLayout, CardView: each child by its layout_gravity.
            foreach (View child in children)
            {
                if (child.IsGone) continue;

                Gravity g = ParseGravity(child.Attr("layout_gravity"), Gravity.Left | Gravity.Top);
                float x = AlignX(g, innerLeft, innerWidth, child);
                float y = AlignY(g, innerTop, innerHeight, child);
                child.Place(x, y, child.measuredWidth, child.measuredHeight);
            }
        }

        private static float AlignX(Gravity g, float start, float room, View child)
        {
            float size = child.measuredWidth;
            if ((g & Gravity.CenterH) != 0) return start + (room - size) * 0.5f + child.MarginStart - child.MarginEnd;
            if ((g & Gravity.Right) != 0) return start + room - size - child.MarginEnd;
            return start + child.MarginStart;
        }

        private static float AlignY(Gravity g, float start, float room, View child)
        {
            float size = child.measuredHeight;
            if ((g & Gravity.CenterV) != 0) return start + (room - size) * 0.5f + child.MarginTop - child.MarginBottom;
            if ((g & Gravity.Bottom) != 0) return start + room - size - child.MarginBottom;
            return start + child.MarginTop;
        }

        private void PlaceLinear(float innerLeft, float innerTop, float innerWidth, float innerHeight)
        {
            bool vertical = Vertical;
            Gravity own = ParseGravity(Attr("gravity"), Gravity.Left | Gravity.Top);

            // How much room the children take along the main axis, to centre or push them by the container's gravity.
            float used = 0f;
            foreach (View child in children)
            {
                if (child.IsGone) continue;
                used += vertical ? child.measuredHeight + child.MarginTop + child.MarginBottom
                                 : child.measuredWidth + child.MarginStart + child.MarginEnd;
            }

            float room = vertical ? innerHeight : innerWidth;
            float cursor = vertical ? innerTop : innerLeft;

            if (vertical)
            {
                if ((own & Gravity.CenterV) != 0) cursor += (room - used) * 0.5f;
                else if ((own & Gravity.Bottom) != 0) cursor += room - used;
            }
            else
            {
                if ((own & Gravity.CenterH) != 0) cursor += (room - used) * 0.5f;
                else if ((own & Gravity.Right) != 0) cursor += room - used;
            }

            foreach (View child in children)
            {
                if (child.IsGone) continue;

                Gravity g = ParseGravity(child.Attr("layout_gravity"), Gravity.None);
                if (vertical)
                {
                    // Across: the child's own gravity, else the container's.
                    Gravity across = (g & (Gravity.Left | Gravity.Right | Gravity.CenterH)) != 0 ? g : own;
                    float x = AlignX(across, innerLeft, innerWidth, child);
                    cursor += child.MarginTop;
                    child.Place(x, cursor, child.measuredWidth, child.measuredHeight);
                    cursor += child.measuredHeight + child.MarginBottom;
                }
                else
                {
                    Gravity across = (g & (Gravity.Top | Gravity.Bottom | Gravity.CenterV)) != 0 ? g : own;
                    float y = AlignY(across, innerTop, innerHeight, child);
                    cursor += child.MarginStart;
                    child.Place(cursor, y, child.measuredWidth, child.measuredHeight);
                    cursor += child.measuredWidth + child.MarginEnd;
                }
            }
        }

        // ---- Putting the numbers on the Unity objects ----

        private void Apply()
        {
            if (rect == null)
                return;

            rect.anchoredPosition = new Vector2(left + Dp(Attr("translationX")), -(top + Dp(Attr("translationY"))));
            rect.sizeDelta = new Vector2(measuredWidth, measuredHeight);

            if (text != null)
            {
                RectTransform tr = text.rectTransform;
                tr.offsetMin = new Vector2(PadStart, PadBottom);
                tr.offsetMax = new Vector2(-PadEnd, -PadTop);
            }

            if (iconRect != null)
            {
                iconRect.offsetMin = new Vector2(PadStart, PadBottom);
                iconRect.offsetMax = new Vector2(-PadEnd, -PadTop);
            }

            if (tag == "ScrollView" && scrollContent != null)
                scrollContent.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, measuredHeight));

            foreach (View child in children)
                child.Apply();
        }
    }

    // ---- Small helpers ------------------------------------------------------------------------------------------

    private static float Dp(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0f;

        value = value.Trim();
        foreach (string unit in new[] { "dp", "dip", "sp", "px" })
        {
            if (value.EndsWith(unit))
            {
                value = value.Substring(0, value.Length - unit.Length);
                break;
            }
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
    }

    private static float Float(string value)
    {
        return !string.IsNullOrEmpty(value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
    }

    private static Color ParseColor(string value)
    {
        if (value == "@color/transparent")
            return Color.clear;

        return UiKit.Hex(value.Trim());
    }

    private static Gravity ParseGravity(string value, Gravity fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        Gravity g = Gravity.None;
        foreach (string part in value.Split('|'))
        {
            switch (part.Trim())
            {
                case "left": case "start": g |= Gravity.Left; break;
                case "right": case "end": g |= Gravity.Right; break;
                case "top": g |= Gravity.Top; break;
                case "bottom": g |= Gravity.Bottom; break;
                case "center_horizontal": g |= Gravity.CenterH; break;
                case "center_vertical": g |= Gravity.CenterV; break;
                case "center": g |= Gravity.CenterH | Gravity.CenterV; break;
            }
        }

        // Anything not named defaults to the start / top.
        return g;
    }

    private static TextAlignmentOptions TextAlignment(Gravity g)
    {
        bool centerH = (g & Gravity.CenterH) != 0, right = (g & Gravity.Right) != 0;
        bool centerV = (g & Gravity.CenterV) != 0, bottom = (g & Gravity.Bottom) != 0;

        if (centerV) return centerH ? TextAlignmentOptions.Center : right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        if (bottom) return centerH ? TextAlignmentOptions.Bottom : right ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
        return centerH ? TextAlignmentOptions.Top : right ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
    }

    // The game font has no emoji or odd symbols: keep what it can draw, swap a few for plain look-alikes, drop the rest.
    private static string Sanitize(TextMeshProUGUI target, string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (c < 128 || c == '\n')
            {
                builder.Append(c);
                continue;
            }

            if (char.IsSurrogate(c) || c == '️' || c == '‍')
                continue;   // emoji

            switch (c)
            {
                case '←': continue;   // the arrow of "← BACK" is dropped: the button just says BACK
                case 'ⓘ': builder.Append('i'); continue;   // circled i
                case '○': builder.Append('o'); continue;   // circle
                case '•': builder.Append('•'); continue;
            }

            if (target.font != null && target.font.HasCharacter(c, true, true))
                builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    private static Sprite FindSprite(string reference)
    {
        if (string.IsNullOrEmpty(reference))
            return null;

        string name = reference.Substring(reference.LastIndexOf('/') + 1);
        Sprite found = UiKit.Icon(name) ?? UiKit.Picture(name) ?? UiKit.Shape(name);
        if (found != null)
            return found;

        // Android's own pencil and camera icons are not in the project; draw them.
        if (name == "ic_menu_edit") return PencilSprite();
        if (name == "ic_menu_camera") return CameraSprite();
        return null;
    }

    private static Sprite pencilSprite, cameraSprite;

    private static Sprite PencilSprite()
    {
        if (pencilSprite != null)
            return pencilSprite;

        // A pencil lying at 45 degrees, tip at the bottom left: lead, sharpened wood, body, metal band, eraser. The parts are
        // separated by thin gaps so they read as parts even in one colour.
        const int size = 64;
        Color[] pixels = new Color[size * size];
        Vector2 tip = new Vector2(6f, 6f);
        Vector2 along = new Vector2(1f, 1f).normalized;
        Vector2 across = new Vector2(-1f, 1f).normalized;
        const float half = 7f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - tip;
                float u = Vector2.Dot(p, along);    // distance from the tip along the pencil
                float v = Mathf.Abs(Vector2.Dot(p, across));

                // The width of the pencil at this distance: a cone for the tip, a rounded end for the eraser.
                float width = half;
                if (u < 14f) width = half * Mathf.Clamp01(u / 14f);
                if (u > 66f) width = half * Mathf.Sqrt(Mathf.Clamp01(1f - (u - 66f) / 6f));

                bool inside = u >= 0f && u <= 72f;
                bool gap = (u > 9f && u < 10.6f) || (u > 53.5f && u < 55.3f) || (u > 61.5f && u < 63.3f);

                float coverage = inside && !gap ? Mathf.Clamp01(width - v + 0.5f) : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        pencilSprite = MakeSprite(pixels, size, size);
        return pencilSprite;
    }

    private static Sprite CameraSprite()
    {
        if (cameraSprite != null)
            return cameraSprite;

        const int size = 64;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool body = x >= 6 && x <= 58 && y >= 12 && y <= 44;
                bool bump = x >= 22 && x <= 42 && y > 44 && y <= 52;
                float lens = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 28f));
                bool hole = lens <= 11f && lens > 7f;
                pixels[y * size + x] = (body || bump) && !hole ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }

        cameraSprite = MakeSprite(pixels, size, size);
        return cameraSprite;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(negative && positive);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static Sprite MakeSprite(Color[] pixels, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite arrowSprite;

    // A left-pointing arrow (shaft and head), white, to be tinted.
    private static Sprite ArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        const int width = 128, height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dy = Mathf.Abs(y + 0.5f - 32f);
                bool shaft = x >= 20 && dy <= 6f;
                bool head = x <= 56 && dy <= x * (30f / 56f);
                pixels[y * width + x] = shaft || head ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        arrowSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        return arrowSprite;
    }

    private static Sprite ringSprite;

    // A thin circle, white, to be tinted.
    private static Sprite RingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
                float alpha = Mathf.Clamp01(Mathf.Min(distance - 24f, 31f - distance) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    // A rounded rectangle that can be stretched (9-sliced), for cards.
    private static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();

    private static Sprite RoundedSprite(int radius)
    {
        if (rounded.TryGetValue(radius, out Sprite cached))
            return cached;

        int size = radius * 2 + 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 0.5f));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                      new Vector4(radius, radius, radius, radius));
        rounded[radius] = sprite;
        return sprite;
    }
}

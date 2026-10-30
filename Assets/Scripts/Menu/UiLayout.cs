using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Helpers for screens that were laid out with fixed margins on a wide phone (about 914 x 411 dp).
// Everything inside the frame uses the Android numbers as they are; the frame is scaled to fit whatever the canvas is.
public static class UiLayout
{
    public const float DesignWidth = 914f;
    public const float DesignHeight = 411f;

    public static RectTransform Frame(RectTransform root, float width = DesignWidth, float height = DesignHeight)
    {
        RectTransform frame = UiKit.New(root, "Frame");
        UiKit.Center(frame, width, height);
        frame.gameObject.AddComponent<FitFrame>();
        return frame;
    }

    // Offsets measured from the top-left corner of the frame, like layout_marginStart / layout_marginTop.
    public static RectTransform TopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        return UiKit.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
    }

    public static RectTransform TopCenter(RectTransform rt, float y, float width, float height, float x = 0f)
    {
        return UiKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -y), new Vector2(width, height));
    }

    public static RectTransform TopRight(RectTransform rt, float right, float y, float width, float height)
    {
        return UiKit.Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-right, -y), new Vector2(width, height));
    }

    public static RectTransform BottomCenter(RectTransform rt, float bottom, float width, float height)
    {
        return UiKit.Place(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom), new Vector2(width, height));
    }

    // Android letterSpacing is in em; TextMeshPro's characterSpacing is in hundredths of an em.
    public static TextMeshProUGUI Spaced(TextMeshProUGUI text, float em)
    {
        text.characterSpacing = em * 100f;
        return text;
    }

    // The "BACK" button used on the mission screens.
    public static Button BackButton(RectTransform parent, float x, float y, float width, float height, float textSize, UnityEngine.Events.UnityAction onClick)
    {
        Button back = UiKit.Btn(parent, "Back", UiKit.Shape("briefing_panel_bg"), Color.white, "BACK", textSize, UiKit.Hex("#F2D58A"), true, -1f, -1f);
        TopLeft((RectTransform)back.transform, x, y, width, height);
        back.onClick.AddListener(onClick);
        return back;
    }

    // A dark picture behind the screen: the picture, then a colour laid over it.
    public static void Backdrop(RectTransform root, string picture, string overlayColor, float pictureAlpha = 1f)
    {
        Image floor = UiKit.Solid(root, "Floor", Color.black);
        UiKit.Stretch(floor.rectTransform);

        RawImage image = UiKit.Cover(root, "Picture", UiKit.Picture(picture));
        image.color = new Color(1f, 1f, 1f, pictureAlpha);

        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex(overlayColor));
        UiKit.Stretch(overlay.rectTransform);
    }

    // A text with a fixed place in the frame (anchored top-left, no layout group).
    public static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, FontStyles style,
                                        TextAlignmentOptions align, float x, float y, float width, float height)
    {
        TextMeshProUGUI tmp = UiKit.Text(parent, text, size, color, style, align);
        TopLeft(tmp.rectTransform, x, y, width, height);
        return tmp;
    }

    // The scroll area made by UiKit.Scroll: its root is two levels above the content.
    public static RectTransform ScrollRoot(RectTransform content)
    {
        return (RectTransform)content.parent.parent;
    }
}

// Scales the frame so the whole of it is visible, whatever the shape of the screen.
public class FitFrame : MonoBehaviour
{
    private void LateUpdate()
    {
        RectTransform self = (RectTransform)transform;
        RectTransform parent = (RectTransform)transform.parent;

        float scale = Mathf.Min(parent.rect.width / self.rect.width, parent.rect.height / self.rect.height);
        if (scale > 0f)
            transform.localScale = new Vector3(scale, scale, 1f);
    }
}

// A moving bar for "indeterminate" progress, like the Android ProgressBar on the loading screen.
public class IndeterminateBar : MonoBehaviour
{
    public RectTransform bar;
    public float speed = 1.1f;

    private void Update()
    {
        RectTransform track = (RectTransform)transform;
        float width = track.rect.width;
        float length = width * 0.35f;
        float t = Mathf.Repeat(Time.unscaledTime * speed, 1f);

        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0f, 0.5f);
        bar.sizeDelta = new Vector2(length, 0f);
        bar.anchoredPosition = new Vector2(Mathf.Lerp(-length, width, t), 0f);
    }
}

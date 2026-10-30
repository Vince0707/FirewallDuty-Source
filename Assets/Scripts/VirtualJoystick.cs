using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Size (players found it too small and hard to drag)")]
    [Tooltip("The joystick is made at least this big (in canvas units) when the game starts. The scenes had 180.")]
    [SerializeField, Min(0f)] private float minSize = 270f;
    [Tooltip("The round handle is this fraction of the joystick.")]
    [SerializeField, Range(0.2f, 0.7f)] private float handleRatio = 0.42f;
    [Tooltip("Distance of the joystick from the left and bottom edges of the screen.")]
    [SerializeField, Min(0f)] private Vector2 edgeMargin = new Vector2(60f, 40f);

    [Header("Floating (players wanted a free joystick)")]
    [Tooltip("The joystick appears where the thumb touches the left part of the screen and the thumb can drag from there, instead of having to start on the fixed circle.")]
    [SerializeField] private bool floating = true;
    [Tooltip("How much of the screen, from the left edge, takes the touch that starts the joystick.")]
    [SerializeField, Range(0.25f, 0.6f)] private float zoneWidth = 0.5f;
    [Tooltip("How see-through the joystick is while it is not being used.")]
    [SerializeField, Range(0.1f, 1f)] private float idleAlpha = 0.55f;

    public Vector2 Direction { get; private set; }

    private RectTransform zone;
    private Vector2 homePosition;
    private CanvasGroup fade;

    private void Awake()
    {
        if (background == null)
            background = GetComponent<RectTransform>();

        if (handle == null && transform.childCount > 0)
            handle = transform.GetChild(0) as RectTransform;

        MakeBigger();

        if (floating)
            SetUpFloating();
    }

    // Bigger base and handle, kept in the bottom left corner. Done here so every scene gets it without being edited; a joystick that is already
    // bigger than minSize is left alone.
    private void MakeBigger()
    {
        if (background == null)
            return;

        Vector2 size = background.sizeDelta;
        if (size.x >= minSize && size.y >= minSize)
            return;

        float side = Mathf.Max(minSize, Mathf.Max(size.x, size.y));
        background.sizeDelta = new Vector2(side, side);

        // Only moved when it sits in the bottom left corner (anchored there); the margin keeps it off the edges.
        bool bottomLeft = background.anchorMin == Vector2.zero && background.anchorMax == Vector2.zero;
        if (bottomLeft)
        {
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = new Vector2(edgeMargin.x + side * 0.5f, edgeMargin.y + side * 0.5f);
        }

        if (handle != null)
        {
            float handleSize = side * handleRatio;
            handle.sizeDelta = new Vector2(handleSize, handleSize);
        }
    }

    // A see-through area over the left part of the screen (just behind the joystick, in front of the look area) that passes touches to the joystick, which then
    // moves to where the thumb went down. Made here so every scene gets it without being edited.
    private void SetUpFloating()
    {
        RectTransform parent = background != null ? background.parent as RectTransform : null;
        if (parent == null)
            return;

        homePosition = background.anchoredPosition;

        fade = background.GetComponent<CanvasGroup>();
        if (fade == null)
            fade = background.gameObject.AddComponent<CanvasGroup>();

        fade.alpha = idleAlpha;

        GameObject area = new GameObject("JoystickZone", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        area.layer = background.gameObject.layer;
        zone = (RectTransform)area.transform;
        zone.SetParent(parent, false);
        zone.anchorMin = Vector2.zero;
        zone.anchorMax = new Vector2(zoneWidth, 1f);
        zone.offsetMin = Vector2.zero;
        zone.offsetMax = Vector2.zero;
        zone.SetSiblingIndex(background.GetSiblingIndex());

        Image catcher = area.GetComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;

        area.AddComponent<JoystickZone>().joystick = this;
    }

    // The thumb went down in the zone: the joystick moves there (kept fully on the screen) and takes the touch.
    public void BeginFloating(PointerEventData eventData)
    {
        RectTransform parent = background != null ? background.parent as RectTransform : null;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            Vector2 half = Vector2.Scale(background.rect.size, background.localScale) * 0.5f;
            Rect area = parent.rect;
            local.x = Mathf.Clamp(local.x, area.xMin + half.x, area.xMax - half.x);
            local.y = Mathf.Clamp(local.y, area.yMin + half.y, area.yMax - half.y);
            background.position = parent.TransformPoint(local);
        }

        if (fade != null)
            fade.alpha = 1f;

        OnDrag(eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (fade != null)
            fade.alpha = 1f;

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
            return;

        Vector2 halfSize = background.rect.size * 0.5f;

        if (halfSize.x <= 0 || halfSize.y <= 0)
            return;

        Vector2 normalized = new Vector2(
            localPoint.x / halfSize.x,
            localPoint.y / halfSize.y
        );

        Direction = Vector2.ClampMagnitude(normalized, 1f);

        handle.anchoredPosition = new Vector2(
            Direction.x * halfSize.x,
            Direction.y * halfSize.y
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Direction = Vector2.zero;

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;

        ReturnHome();
    }

    // Back to the corner, see-through, ready for the next touch.
    private void ReturnHome()
    {
        if (!floating || background == null || zone == null)
            return;

        background.anchoredPosition = homePosition;

        if (fade != null)
            fade.alpha = idleAlpha;
    }

    private void OnEnable()
    {
        if (zone != null)
            zone.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        Direction = Vector2.zero;

        if (handle != null)
            handle.anchoredPosition = Vector2.zero;

        ReturnHome();

        if (zone != null)
            zone.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (zone != null)
            Destroy(zone.gameObject);
    }
}

// The see-through area that starts a floating joystick: it passes the touch on to the joystick.
public class JoystickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public VirtualJoystick joystick;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (joystick != null)
            joystick.BeginFloating(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (joystick != null)
            joystick.OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (joystick != null)
            joystick.OnPointerUp(eventData);
    }
}

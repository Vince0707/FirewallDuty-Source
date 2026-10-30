using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Put this on the fixed hand interaction Button. The button is always on screen (so it never moves or
// pops in); it just glows brighter while the crosshair is on something interactable. Tapping it calls
// PlayerInteraction.Interact, which does nothing when nothing is targeted.
// (Class name kept so existing scene references keep working.)
[RequireComponent(typeof(Button))]
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private Button button;
    [Tooltip("Optional text label. Hidden unless Show Label is on.")]
    [SerializeField] private TMP_Text tmpLabel;
    [SerializeField] private Text legacyLabel;
    [SerializeField] private bool showLabel = false;

    [Header("Feedback")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.5f;
    [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
    [SerializeField, Min(1f)] private float activeScale = 1.1f;
    [SerializeField, Min(0f)] private float feedbackSpeed = 12f;

    [Header("Size (players found it too small)")]
    [Tooltip("The button is made at least this big (in canvas units) when the game starts. The scenes had 180.")]
    [SerializeField, Min(0f)] private float minSize = 250f;
    [Tooltip("Distance of the button from the right and bottom edges of the screen.")]
    [SerializeField, Min(0f)] private Vector2 edgeMargin = new Vector2(60f, 40f);

    private CanvasGroup canvasGroup;
    private string shownText;
    private float blend;

    private void Awake()
    {
        MakeBigger();

        if (button == null)
            button = GetComponent<Button>();

        if (tmpLabel == null)
            tmpLabel = GetComponentInChildren<TMP_Text>(true);

        if (tmpLabel == null && legacyLabel == null)
            legacyLabel = GetComponentInChildren<Text>(true);

        // The button must always block raycasts so a tap on it never leaks through to the look area or joystick.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        ApplyFeedback(0f);

        if (!showLabel)
        {
            if (tmpLabel != null) tmpLabel.gameObject.SetActive(false);
            if (legacyLabel != null) legacyLabel.gameObject.SetActive(false);
        }
    }

    // A bigger button (and hand picture), kept in the bottom right corner like the joystick on the left. Done here so every scene gets it without being
    // edited; a button that is already bigger than minSize is left alone.
    private void MakeBigger()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
            return;

        Vector2 size = rect.sizeDelta;
        if (size.x >= minSize && size.y >= minSize)
            return;

        float oldSide = Mathf.Max(1f, Mathf.Max(size.x, size.y));
        float side = Mathf.Max(minSize, oldSide);
        rect.sizeDelta = new Vector2(side, side);

        bool bottomRight = rect.anchorMin == new Vector2(1f, 0f) && rect.anchorMax == new Vector2(1f, 0f);
        if (bottomRight)
        {
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-(edgeMargin.x + side * 0.5f), edgeMargin.y + side * 0.5f);
        }

        // The picture (and label) inside keep their share of the button.
        float factor = side / oldSide;
        foreach (Transform child in transform)
        {
            RectTransform childRect = child as RectTransform;
            if (childRect != null && childRect.anchorMin == childRect.anchorMax)
                childRect.sizeDelta *= factor;
        }
    }

    private void Start()
    {
        if (playerInteraction == null)
            playerInteraction = FindAnyObjectByType<PlayerInteraction>();

        // Don't register twice if the scene already wires OnClick to PlayerInteraction.Interact.
        if (button != null && button.onClick.GetPersistentEventCount() == 0)
            button.onClick.AddListener(OnTapped);
    }

    private void LateUpdate()
    {
        Interactable target = playerInteraction != null ? playerInteraction.CurrentInteractable : null;

        // Glow when there is something to do: act on the target, or stand up while seated.
        bool hasAction = playerInteraction != null && playerInteraction.HasAction;
        blend = Mathf.MoveTowards(blend, hasAction ? 1f : 0f, feedbackSpeed * Time.unscaledDeltaTime);
        ApplyFeedback(blend);

        if (showLabel && target != null)
            SetLabel(target.PromptText);
    }

    private void OnTapped()
    {
        if (playerInteraction != null)
            playerInteraction.Interact();
    }

    private void ApplyFeedback(float t)
    {
        canvasGroup.alpha = Mathf.Lerp(idleAlpha, activeAlpha, t);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, activeScale, t);
    }

    private void SetLabel(string text)
    {
        if (text == shownText)
            return;

        shownText = text;

        if (tmpLabel != null)
            tmpLabel.text = text;
        else if (legacyLabel != null)
            legacyLabel.text = text;
    }
}

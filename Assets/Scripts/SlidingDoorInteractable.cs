using UnityEngine;

// A door that slides sideways instead of swinging. Works with the hand button like any Interactable, and can also open
// by itself when the player walks up to it (automatic sliding doors).
//  - One leaf:  slides in "Slide Direction".
//  - Two leaves (a bi-parting entrance door): the first slides in Slide Direction, the second the opposite way, so the
//    two halves meet in the middle when closed and split apart when open.
// Put this on a parent object that holds the leaves, or directly on a single leaf. The leaves must not be Static.
public class SlidingDoorInteractable : Interactable
{
    [Header("Sliding")]
    [Tooltip("The first moving leaf. Leave empty to move this object.")]
    [SerializeField] private Transform leaf;
    [Tooltip("Optional second leaf. It slides the opposite way to the first, so the two halves split apart.")]
    [SerializeField] private Transform secondLeaf;
    [Tooltip("Direction the first leaf slides, in its parent's space. Right = (1,0,0). The second leaf goes the other way.")]
    [SerializeField] private Vector3 slideDirection = Vector3.right;
    [Tooltip("How far each leaf slides, in metres. About the width of one leaf, so the doorway is clear.")]
    [SerializeField, Min(0.05f)] private float slideDistance = 1.4f;
    [Tooltip("Sliding speed in metres per second.")]
    [SerializeField, Min(0.05f)] private float slideSpeed = 1.2f;

    [Header("Automatic opening")]
    [Tooltip("Open by itself when the player comes near, and close again after the player has left.")]
    [SerializeField] private bool automatic = true;
    [SerializeField, Min(0.5f)] private float autoOpenDistance = 2.2f;
    [SerializeField, Min(0f)] private float autoCloseDelay = 1.5f;

    [Header("Prompt Text")]
    [SerializeField] private string openText = "OPEN";
    [SerializeField] private string closeText = "CLOSE";

    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    private Vector3 secondClosedLocalPosition;
    private Vector3 secondOpenLocalPosition;
    private Vector3 centerWorld;
    private bool isOpen;
    private float openAmount;       // 0 = closed, 1 = fully open
    private float leftNearTimer;
    private Transform player;

    public bool IsOpen => isOpen;
    public Transform Leaf => leaf != null ? leaf : transform;
    public override string PromptText => isOpen ? closeText : openText;

    private void Awake()
    {
        Vector3 direction = slideDirection.sqrMagnitude > 0.0001f ? slideDirection.normalized : Vector3.right;

        closedLocalPosition = Leaf.localPosition;
        openLocalPosition = closedLocalPosition + direction * slideDistance;

        if (secondLeaf != null)
        {
            secondClosedLocalPosition = secondLeaf.localPosition;
            secondOpenLocalPosition = secondClosedLocalPosition - direction * slideDistance;
        }

        // Where the doorway is, measured while the door is closed.
        centerWorld = Leaf.position;
        bool any = false;
        Bounds bounds = new Bounds(centerWorld, Vector3.zero);

        foreach (Transform t in new[] { Leaf, secondLeaf })
        {
            if (t == null)
                continue;

            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);

                if (r.isPartOfStaticBatch)
                {
                    UnityEngine.Debug.LogWarning(t.name + " is Batching Static, so it will not visibly slide. Untick Static on the door.", this);
                }
            }
        }

        if (any)
            centerWorld = bounds.center;
    }

    private void Start()
    {
        PlayerMobileController controller = FindAnyObjectByType<PlayerMobileController>();
        if (controller != null)
            player = controller.transform;
        else if (automatic)
            UnityEngine.Debug.LogWarning(name + ": no player found, so automatic opening is off. The hand button still works.", this);
    }

    private void Update()
    {
        if (automatic && player != null)
            UpdateAutomatic();

        float target = isOpen ? 1f : 0f;
        if (openAmount == target)
            return;

        openAmount = Mathf.MoveTowards(openAmount, target, slideSpeed / slideDistance * Time.deltaTime);
        float eased = Mathf.SmoothStep(0f, 1f, openAmount);

        Leaf.localPosition = Vector3.Lerp(closedLocalPosition, openLocalPosition, eased);

        if (secondLeaf != null)
            secondLeaf.localPosition = Vector3.Lerp(secondClosedLocalPosition, secondOpenLocalPosition, eased);
    }

    private void UpdateAutomatic()
    {
        Vector3 offset = player.position - centerWorld;
        offset.y = 0f;
        bool near = offset.sqrMagnitude <= autoOpenDistance * autoOpenDistance;

        if (near)
        {
            leftNearTimer = 0f;
            SetOpen(true);
        }
        else if (isOpen)
        {
            leftNearTimer += Time.deltaTime;
            if (leftNearTimer >= autoCloseDelay)
                SetOpen(false);
        }
    }

    private void SetOpen(bool open)
    {
        if (open == isOpen)
            return;

        isOpen = open;
        GameSounds.SlidingDoor(centerWorld);
    }

    // The hand button: open it, or close it again.
    public override void Interact()
    {
        SetOpen(!isOpen);
        leftNearTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 direction = slideDirection.sqrMagnitude > 0.0001f ? slideDirection.normalized : Vector3.right;

        DrawSlide(Leaf, direction);
        if (secondLeaf != null)
            DrawSlide(secondLeaf, -direction);

        if (automatic)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(Application.isPlaying ? centerWorld : Leaf.position, autoOpenDistance);
        }
    }

    private void DrawSlide(Transform t, Vector3 localDirection)
    {
        Transform parent = t.parent;
        Vector3 world = parent != null ? parent.TransformDirection(localDirection) : localDirection;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(t.position, t.position + world * slideDistance);
        Gizmos.DrawWireSphere(t.position + world * slideDistance, 0.08f);
    }
}

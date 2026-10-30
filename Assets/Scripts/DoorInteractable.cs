using UnityEngine;

public class DoorInteractable : Interactable
{
    public enum SwingDirection
    {
        AwayFromPlayer,
        Positive,
        Negative
    }

    [Header("Door Settings")]
    [Tooltip("The hinge. This transform is rotated around its own origin, so its origin must sit on the hinge edge of the door. Leave empty to rotate this object itself.")]
    [SerializeField] private Transform doorPivot;
    [Tooltip("How far the door swings open, in degrees.")]
    [SerializeField, Range(1f, 170f)] private float openAngle = 90f;
    [Tooltip("Swing speed in degrees per second.")]
    [SerializeField, Min(1f)] private float rotationSpeed = 120f;
    [Tooltip("AwayFromPlayer: always swings away from whoever opens it. Positive/Negative: fixed direction (positive turns clockwise seen from above).")]
    [SerializeField] private SwingDirection swingDirection = SwingDirection.AwayFromPlayer;

    [Header("Prompt Text")]
    [SerializeField] private string openText = "OPEN";
    [SerializeField] private string closeText = "CLOSE";

    private Quaternion closedLocalRotation = Quaternion.identity;
    private Vector3 leafCenterInPivotSpace;
    private bool poseCached;
    private bool isOpen;
    private float openAmount;   // 0 = closed, 1 = fully open
    private float swingSign = 1f;

    public bool IsOpen => isOpen;
    public float OpenAmount => openAmount;
    public Transform DoorPivot => doorPivot != null ? doorPivot : transform;
    public override string PromptText => isOpen ? closeText : openText;

    private void Awake()
    {
        CacheClosedPose();
    }

    private void Start()
    {
        foreach (Renderer r in DoorPivot.GetComponentsInChildren<Renderer>())
        {
            if (r.isPartOfStaticBatch)
            {
                UnityEngine.Debug.LogWarning(
                    name + " is Batching Static, so its mesh will not visibly rotate. " +
                    "Run Tools > Firewall Duty > Set Up Doors In Open Scenes, or untick Static on the door.",
                    this);
                break;
            }
        }
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    // Records the closed pose and where the door leaf sits relative to the hinge.
    // Must run while the door is closed (Awake does this).
    public void CacheClosedPose()
    {
        Transform pivot = DoorPivot;
        closedLocalRotation = pivot.localRotation;
        leafCenterInPivotSpace = pivot.InverseTransformPoint(CalculateLeafCenter(pivot));
        poseCached = true;
    }

    public override void Interact()
    {
        SetOpen(!isOpen, null);
    }

    public override void Interact(Transform interactor)
    {
        SetOpen(!isOpen, interactor);
    }

    public void SetOpen(bool open, Transform interactor)
    {
        if (open == isOpen)
            return;

        if (!poseCached)
            CacheClosedPose();

        // Only pick a swing side while (almost) closed, so reversing mid-swing never flips the door.
        if (open && openAmount < 0.01f)
            swingSign = ResolveSwingSign(interactor);

        isOpen = open;
        GameSounds.Door(DoorPivot.position, open);
    }

    // Advances the swing animation. Update calls this every frame.
    public void Tick(float deltaTime)
    {
        float target = isOpen ? 1f : 0f;
        if (openAmount == target)
            return;

        float step = rotationSpeed / Mathf.Max(openAngle, 1f) * deltaTime;
        openAmount = Mathf.MoveTowards(openAmount, target, step);
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        float angle = swingSign * openAngle * Mathf.SmoothStep(0f, 1f, openAmount);
        DoorPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.up) * closedLocalRotation;
    }

    private float ResolveSwingSign(Transform interactor)
    {
        switch (swingDirection)
        {
            case SwingDirection.Positive: return 1f;
            case SwingDirection.Negative: return -1f;
        }

        if (interactor == null)
            return swingSign;

        Transform pivot = DoorPivot;
        Quaternion parentRotation = pivot.parent != null ? pivot.parent.rotation : Quaternion.identity;

        // Direction the closed door leaf extends from the hinge.
        Vector3 leaf = parentRotation * closedLocalRotation *
                       Vector3.Scale(leafCenterInPivotSpace, pivot.localScale);
        leaf.y = 0f;

        Vector3 toPlayer = interactor.position - pivot.position;
        toPlayer.y = 0f;

        if (leaf.sqrMagnitude < 0.0001f || toPlayer.sqrMagnitude < 0.0001f)
            return swingSign;

        // Y component of cross(leaf, toPlayer): positive means a positive rotation would swing toward the player.
        float side = leaf.z * toPlayer.x - leaf.x * toPlayer.z;
        return side > 0f ? -1f : 1f;
    }

    private static Vector3 CalculateLeafCenter(Transform pivot)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(pivot.position, Vector3.zero);

        foreach (Renderer r in pivot.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!hasBounds)
        {
            foreach (Collider c in pivot.GetComponentsInChildren<Collider>())
            {
                if (!hasBounds) { bounds = c.bounds; hasBounds = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }

        return hasBounds ? bounds.center : pivot.position + pivot.right;
    }

    private void OnDrawGizmosSelected()
    {
        Transform pivot = DoorPivot;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pivot.position, 0.08f);
        Gizmos.DrawLine(pivot.position, pivot.position + Vector3.up * 2f);
    }
}

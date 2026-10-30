using UnityEngine;

// A chair the player can sit on with the hand button. Put it on the chair (the object that has the chair's collider).
// The tool Tools > Firewall Duty > Set Up Sittable Chairs adds it to every free chair and fills in the values.
public class SeatInteractable : Interactable
{
    [Header("Seat")]
    [Tooltip("Where the player's feet-origin goes while seated, relative to this chair (the middle of the seat, at floor level).")]
    [SerializeField] private Vector3 seatLocalPosition = Vector3.zero;
    [Tooltip("Direction the player faces when sitting down, as a world angle in degrees (0 = +Z, 90 = +X). Usually toward the desk.")]
    [SerializeField] private float sitYaw = 0f;
    [Tooltip("Eye height above the floor while seated, in metres.")]
    [SerializeField, Min(0.5f)] private float seatedEyeHeight = 1.12f;

    // Written by the setup tool so a re-run can tell its own value from one you set by hand.
    [SerializeField, HideInInspector] private float autoEyeHeight;

    [Header("Prompt Text")]
    [SerializeField] private string sitText = "SIT";

    public bool Occupied { get; private set; }

    // Floor-level point to stand the player on while seated.
    public Vector3 SeatPosition => transform.TransformPoint(seatLocalPosition);
    public float SitYaw => sitYaw;
    public float SeatedEyeHeight => seatedEyeHeight;

    public override string PromptText => sitText;
    public override bool CanInteract => !Occupied;

    public override void Interact(Transform interactor)
    {
        PlayerSeatController seatController = interactor != null ? interactor.GetComponent<PlayerSeatController>() : null;

        if (seatController == null)
        {
            UnityEngine.Debug.LogWarning("The player has no PlayerSeatController, so it cannot sit. Run Tools > Firewall Duty > Set Up Sittable Chairs.", this);
            return;
        }

        seatController.Sit(this);
    }

    // Called by PlayerSeatController.
    public void SetOccupied(bool occupied)
    {
        Occupied = occupied;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 p = SeatPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(p + Vector3.up * seatedEyeHeight, 0.08f);
        Gizmos.DrawLine(p, p + Vector3.up * seatedEyeHeight);

        Vector3 forward = Quaternion.Euler(0f, sitYaw, 0f) * Vector3.forward;
        Gizmos.DrawLine(p + Vector3.up * seatedEyeHeight, p + Vector3.up * seatedEyeHeight + forward * 0.6f);
    }
}

using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    private const int MaxHits = 16;
    private const int MaxOverlaps = 32;

    [SerializeField] private Camera playerCamera;
    [Tooltip("Maximum crosshair ray length in meters. Each Interactable also has its own Interaction Range; the smaller of the two applies.")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [Tooltip("Minimum seconds between two interactions, so one tap can never trigger twice.")]
    [SerializeField, Min(0f)] private float interactCooldown = 0.2f;

    [Header("Optional: target without aiming")]
    [Tooltip("Off = only the crosshair selects a target. On = also target the nearest Interactable around the player.")]
    [SerializeField] private bool allowProximityTargeting = false;
    [SerializeField] private float proximityRadius = 3f;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxHits];
    private readonly Collider[] overlapBuffer = new Collider[MaxOverlaps];
    private readonly bool[] hitUsed = new bool[MaxHits];
    private const float PriorityDepth = 2f;   // how far behind the first thing the crosshair hits a more important target is still picked
    private Interactable currentInteractable;
    private int lastInteractFrame = -1;
    private float lastInteractTime = float.NegativeInfinity;
    private PlayerSeatController seatController;

    // The object the hand button would act on right now (null = nothing targeted).
    public Interactable CurrentInteractable => currentInteractable;

    // True when the hand button would do something: act on the targeted object, or stand up from a seat.
    public bool HasAction => currentInteractable != null || (seatController != null && seatController.IsSeated);

    private void Awake()
    {
        seatController = GetComponent<PlayerSeatController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        RefreshTarget();
    }

    private void OnDisable()
    {
        SetTarget(null);
    }

    public void RefreshTarget()
    {
        // While seated nothing else is targeted, except the mission computer in front of the chair: the hand button otherwise
        // means "stand up".
        if (seatController != null && seatController.IsSeated)
        {
            Interactable seatedTarget = FindInteractable();
            SetTarget(seatedTarget is MissionAction action && action.AllowWhileSeated ? seatedTarget : null);
            return;
        }

        SetTarget(FindInteractable());
    }

    // Hooked to the on-screen hand button's OnClick.
    public void Interact()
    {
        // One tap = one interaction, even if the event arrives twice or from two input paths.
        if (Time.frameCount == lastInteractFrame || Time.unscaledTime - lastInteractTime < interactCooldown)
            return;

        // Seated: the hand button uses the mission computer if the crosshair is on it, otherwise it stands the player up
        // (once the sit-down has finished).
        if (seatController != null && seatController.IsSeated)
        {
            RefreshTarget();

            if (currentInteractable != null && currentInteractable.CanInteract)
            {
                lastInteractFrame = Time.frameCount;
                lastInteractTime = Time.unscaledTime;
                GameSounds.Interact();
                currentInteractable.Interact(transform);
                return;
            }

            if (!seatController.IsBusy)
            {
                lastInteractFrame = Time.frameCount;
                lastInteractTime = Time.unscaledTime;
                seatController.StandUp();
            }

            return;
        }

        // Re-evaluate right now so the object acted on is the one under the crosshair at tap time.
        RefreshTarget();

        if (currentInteractable == null || !currentInteractable.CanInteract)
            return;

        lastInteractFrame = Time.frameCount;
        lastInteractTime = Time.unscaledTime;
        GameSounds.Interact();
        currentInteractable.Interact(transform);
    }

    // Lets editor self-tests tap repeatedly without waiting for a new frame or the cooldown.
    public void ResetTapGuard()
    {
        lastInteractFrame = -1;
        lastInteractTime = float.NegativeInfinity;
    }

    private void SetTarget(Interactable target)
    {
        if (target == currentInteractable)
            return;

        if (currentInteractable != null)
            currentInteractable.SetHighlighted(false);

        currentInteractable = target;

        if (currentInteractable != null)
            currentInteractable.SetHighlighted(true);
    }

    private Interactable FindInteractable()
    {
        if (playerCamera == null)
            return null;

        // Ray through the centre of the screen, where the crosshair is drawn.
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Interactable target = RaycastInteractable(ray);

        if (target == null && allowProximityTargeting)
            target = FindNearbyInteractable(ray.origin);

        return target != null && target.CanInteract ? target : null;
    }

    private Interactable RaycastInteractable(Ray ray)
    {
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore);
        System.Array.Clear(hitUsed, 0, hitUsed.Length);

        // The hits from the nearest outwards (not the player's own body, and not furniture that is looked past). The first one decides the target, like always;
        // but a more important target just behind it (the computer a person is sitting at, behind the person) wins, so the person cannot hide the computer.
        Interactable best = null;
        float firstDistance = -1f;

        while (true)
        {
            float nearest = float.MaxValue;
            int nearestIndex = -1;

            for (int i = 0; i < count; i++)
            {
                if (hitUsed[i])
                    continue;

                RaycastHit hit = hitBuffer[i];
                if (hit.collider.GetComponent<InteractionPassThrough>() != null || hit.collider.transform.IsChildOf(transform))
                {
                    hitUsed[i] = true;
                    continue;
                }

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0)
                break;

            hitUsed[nearestIndex] = true;
            RaycastHit current = hitBuffer[nearestIndex];
            Interactable target = current.collider.GetComponentInParent<Interactable>();

            if (firstDistance < 0f)
            {
                firstDistance = current.distance;

                if (target == null || current.distance > target.InteractionRange)
                    return null;

                best = target;
                continue;
            }

            if (current.distance > firstDistance + PriorityDepth || target == null)
                break;   // too far behind, or something solid is in the way

            if (target != best && current.distance <= target.InteractionRange && target.CanInteract && (!best.CanInteract || target.Priority > best.Priority))
                best = target;
        }

        return best;
    }

    private Interactable FindNearbyInteractable(Vector3 origin)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, proximityRadius, overlapBuffer, interactionMask, QueryTriggerInteraction.Ignore);

        Interactable best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            Interactable candidate = col.GetComponentInParent<Interactable>();
            if (candidate == null || !candidate.CanInteract)
                continue;

            Vector3 closest = col.bounds.ClosestPoint(origin);
            float distance = Vector3.Distance(origin, closest);

            if (distance > candidate.InteractionRange)
                continue;

            // The nearest wins, unless something more important (a computer to inspect) is also within reach.
            bool better = best == null || candidate.Priority > best.Priority || (candidate.Priority == best.Priority && distance < bestDistance);
            if (!better)
                continue;

            if (!HasLineOfSight(origin, closest, candidate))
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 point, Interactable candidate)
    {
        Vector3 toPoint = point - origin;
        float distance = toPoint.magnitude;

        if (distance < 0.01f)
            return true;

        if (Physics.Raycast(origin, toPoint / distance, out RaycastHit hit, distance + 0.05f, interactionMask, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<Interactable>() == candidate;

        return true;
    }
}

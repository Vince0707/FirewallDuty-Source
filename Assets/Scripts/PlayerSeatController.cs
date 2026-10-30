using UnityEngine;

// Lets the player sit on a SeatInteractable and stand up again, using the existing hand button.
//  - Sit: the player glides to the seat, the camera drops to seated eye height, walking is switched off and the view can
//    turn only so far left and right (the joystick and the look area keep working for looking around).
//  - Stand up: the hand button again. The player glides back to where they stood before sitting.
// Sits on the Player object next to PlayerMobileController (the setup tool adds it).
[RequireComponent(typeof(PlayerMobileController))]
public class PlayerSeatController : MonoBehaviour
{
    private enum State
    {
        Standing,
        SittingDown,
        Seated,
        StandingUp
    }

    [Tooltip("The first-person camera. Leave empty to find the camera under this object.")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField, Min(0.1f)] private float transitionSeconds = 0.6f;
    [Tooltip("How far left and right the view can turn while seated, in degrees from straight ahead.")]
    [SerializeField, Range(20f, 180f)] private float lookRange = 85f;

    private PlayerMobileController controller;
    private CharacterController characterController;
    private SeatInteractable seat;
    private State state = State.Standing;

    private float progress;
    private Vector3 fromPosition;
    private Vector3 toPosition;
    private float fromYaw;
    private float toYaw;
    private float fromCameraY;
    private float toCameraY;

    private Vector3 returnPosition;
    private float returnYaw;
    private float standingCameraY;

    public bool IsSeated => state == State.Seated || state == State.SittingDown;
    public bool IsBusy => state == State.SittingDown || state == State.StandingUp;

    private void Awake()
    {
        controller = GetComponent<PlayerMobileController>();
        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
        }

        if (cameraTransform == null)
            UnityEngine.Debug.LogWarning(name + ": PlayerSeatController found no camera, so the view will not lower when seated.", this);
        else
            standingCameraY = cameraTransform.localPosition.y;
    }

    public void Sit(SeatInteractable target)
    {
        if (target == null || state != State.Standing || !target.CanInteract)
            return;

        seat = target;
        seat.SetOccupied(true);

        returnPosition = transform.position;
        returnYaw = transform.eulerAngles.y;
        if (cameraTransform != null)
            standingCameraY = cameraTransform.localPosition.y;

        // Walking is off and the capsule must not push against the chair while the player glides into it.
        controller.MovementLocked = true;
        characterController.enabled = false;

        Begin(State.SittingDown, seat.SeatPosition, seat.SitYaw, seat.SeatedEyeHeight);
    }

    public void StandUp()
    {
        if (state != State.Seated)
            return;

        controller.ClearYawLimit();
        Begin(State.StandingUp, returnPosition, returnYaw, standingCameraY);
    }

    private void Begin(State next, Vector3 position, float yaw, float cameraY)
    {
        state = next;
        progress = 0f;
        fromPosition = transform.position;
        toPosition = position;
        fromYaw = transform.eulerAngles.y;
        toYaw = yaw;
        fromCameraY = cameraTransform != null ? cameraTransform.localPosition.y : 0f;
        toCameraY = cameraY;
    }

    private void Update()
    {
        if (state != State.SittingDown && state != State.StandingUp)
            return;

        progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / transitionSeconds);
        float t = Mathf.SmoothStep(0f, 1f, progress);

        transform.position = Vector3.Lerp(fromPosition, toPosition, t);
        transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(fromYaw, toYaw, t), 0f);

        if (cameraTransform != null)
        {
            Vector3 local = cameraTransform.localPosition;
            local.y = Mathf.Lerp(fromCameraY, toCameraY, t);
            cameraTransform.localPosition = local;
        }

        if (progress < 1f)
            return;

        if (state == State.SittingDown)
        {
            state = State.Seated;
            controller.LimitYaw(toYaw, lookRange);
        }
        else
        {
            // Back on the floor: hand control back to normal walking.
            state = State.Standing;
            characterController.enabled = true;
            controller.MovementLocked = false;

            if (seat != null)
                seat.SetOccupied(false);

            seat = null;
        }
    }

    private void OnDisable()
    {
        // Never leave the player stuck if this is switched off while seated.
        if (state != State.Standing)
        {
            state = State.Standing;

            if (characterController != null)
                characterController.enabled = true;

            if (controller != null)
            {
                controller.MovementLocked = false;
                controller.ClearYawLimit();
            }

            if (seat != null)
                seat.SetOccupied(false);
        }
    }
}

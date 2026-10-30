
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMobileController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private VirtualJoystick joystick;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Camera")]
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float minLookAngle = -80f;
    [SerializeField] private float maxLookAngle = 80f;

    private float verticalVelocity;
    private float cameraPitch;

    // While seated (PlayerSeatController) walking is off: the joystick does nothing and gravity is not applied.
    public bool MovementLocked { get; set; }

    private bool yawLimited;
    private float yawCenter;
    private float yawRange;

    private void Awake()
    {
        GameSettings.Apply();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (playerCamera != null)
        {
            cameraPitch = playerCamera.localEulerAngles.x;

            if (cameraPitch > 180f)
                cameraPitch -= 360f;
        }

        HideOwnBody();
    }

    // The player looks out of their own head, so their body must not be drawn for their own camera: looking up showed the eyebrows and glasses, looking down
    // showed the inside of the neck and body. The body meshes only cast shadows now (so the player still has a shadow on the floor).
    private void HideOwnBody()
    {
        foreach (Renderer body in GetComponentsInChildren<Renderer>(true))
        {
            if (!(body is MeshRenderer) && !(body is SkinnedMeshRenderer))
                continue;

            // Anything carried on the camera itself is not the body.
            if (playerCamera != null && body.transform.IsChildOf(playerCamera))
                continue;

            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }

    private void Update()
    {
        MovePlayer();
    }

    private const float StrideLength = 1.0f;   // metres walked per footstep
    private float strideWalked;

    private void MovePlayer()
    {
        if (MovementLocked)
        {
            verticalVelocity = 0f;
            return;
        }

        Vector2 input = joystick != null
            ? Vector2.ClampMagnitude(joystick.Direction, 1f)
            : Vector2.zero;

        Vector3 moveDirection =
            transform.right * input.x +
            transform.forward * input.y;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity =
            moveDirection * moveSpeed +
            Vector3.up * verticalVelocity;

        Vector3 before = transform.position;
        characterController.Move(velocity * Time.deltaTime);

        // A footstep for every stride walked on the ground.
        Vector3 moved = transform.position - before;
        moved.y = 0f;

        if (characterController.isGrounded && input.sqrMagnitude > 0.01f)
        {
            strideWalked += moved.magnitude;
            if (strideWalked >= StrideLength)
            {
                strideWalked = 0f;
                GameSounds.Footstep();
            }
        }
        else if (input.sqrMagnitude <= 0.01f)
        {
            strideWalked = StrideLength * 0.6f;   // the first step comes soon after starting to walk
        }
    }

    // Limits how far the player can turn left and right (used while seated). Angles are world yaw in degrees.
    public void LimitYaw(float centerDegrees, float rangeDegrees)
    {
        yawLimited = true;
        yawCenter = centerDegrees;
        yawRange = rangeDegrees;
    }

    public void ClearYawLimit()
    {
        yawLimited = false;
    }

    public void ApplyLookDelta(Vector2 delta)
    {
        if (playerCamera == null)
            return;

        float yaw = delta.x * lookSensitivity * GameSettings.LookMultiplier;
        float pitch = delta.y * lookSensitivity * GameSettings.LookMultiplier;

        if (yawLimited)
        {
            float relative = Mathf.DeltaAngle(yawCenter, transform.eulerAngles.y);
            float limited = Mathf.Clamp(relative + yaw, -yawRange, yawRange);
            yaw = limited - relative;
        }

        transform.Rotate(0f, yaw, 0f);

        cameraPitch -= pitch;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            minLookAngle,
            maxLookAngle
        );

        playerCamera.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}

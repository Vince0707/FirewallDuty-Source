using System;
using UnityEngine;

// A "working" activity for an NPC at a spot: sitting at a computer, or standing at a machine. The Seat transform
// says where the NPC's feet-origin goes and which way it faces.
//  - On its own (Start Seated on) the NPC goes to its spot as soon as the scene starts: a stationary worker.
//  - With NpcWalker the walker calls SitDown / StandUp, so this component never moves the NPC by itself.
// While working the NavMeshAgent (if any) must be off: this component is then the only thing moving the Transform.
public class NpcWorker : MonoBehaviour
{
    private enum Phase
    {
        Away,
        SittingDown,
        Seated,
        StandingUp
    }

    [Header("Seat")]
    [Tooltip("Empty object at the chair. Its position is where the NPC's feet-origin goes, its blue (forward) axis is the way the NPC faces.")]
    [SerializeField] private Transform seat;
    [Tooltip("Extra offset in the seat's local space, to nudge the NPC forward/back/up relative to the chair.")]
    [SerializeField] private Vector3 seatOffset = Vector3.zero;

    [Header("Fit to chair (sitting only)")]
    [Tooltip("Once seated, measure where the character's hips really are and move the character so they land on Hip Target. " +
             "Fixes characters that float above or sink into the chair because the sitting clip was made for a different body.")]
    [SerializeField] private bool fitToSeat = false;
    [Tooltip("Where the hips should end up, in the Seat object's local space (x = right, y = up, z = forward).")]
    [SerializeField] private Vector3 hipTarget = new Vector3(0f, 0.55f, 0f);

    [Header("Feet (low seats)")]
    [Tooltip("Stop the feet sinking into the floor when the hips are low (beanbag): the legs bend instead.")]
    [SerializeField] private bool keepFeetAboveFloor = false;
    [Tooltip("Height of the ankle joint above the floor when the foot rests on it.")]
    [SerializeField, Min(0f)] private float ankleHeight = 0.09f;
    [Tooltip("Lie back with the legs stretched out in front, feet resting on the floor (beanbag).")]
    [SerializeField] private bool straightenLegs = false;
    [Tooltip("How far the legs reach out, as a fraction of full leg length. 1 = completely straight.")]
    [SerializeField, Range(0.6f, 1f)] private float legExtension = 0.96f;

    [Header("Hands on the desk (sitting)")]
    [Tooltip("Rest both hands on the desk while seated (reception counter, desk). Needs the two targets below.")]
    [SerializeField] private bool restHandsOnDesk = false;
    [Tooltip("Where the left hand rests. A child of the seat marker, placed on the desk surface.")]
    [SerializeField] private Transform leftHandRest;
    [Tooltip("Where the right hand rests.")]
    [SerializeField] private Transform rightHandRest;

    // Written by the setup tools so a re-run can tell your manual tuning from its own earlier values.
    [SerializeField, HideInInspector] private bool toolConfigured;
    [SerializeField, HideInInspector] private Vector3 autoHipTarget;

    [Header("Behaviour")]
    [Tooltip("Go to the spot and work as soon as the scene starts. Turn off when an NpcWalker sends this NPC there later.")]
    [SerializeField] private bool startSeated = true;
    [SerializeField, Min(0.05f)] private float transitionSeconds = 0.7f;

    [Header("Animation")]
    [Tooltip("Leave empty to find the Animator on this object or its children.")]
    [SerializeField] private Animator animator;
    [Tooltip("Bool parameter on the Animator Controller: true while working.")]
    [SerializeField] private string workingParameter = "IsWorking";
    [Tooltip("Name of the working state, used to start each NPC at a random point of the clip so they don't move in unison.")]
    [SerializeField] private string workingStateName = "Work";

    private const float FitSettleSeconds = 0.5f;
    private const float FitSampleSeconds = 0.6f;
    private const int FitSettleFrames = 20;
    private const int FitSampleFrames = 20;
    private const float FitMoveSeconds = 0.4f;

    private Phase phase = Phase.Away;
    private int workingHash;
    private int workingStateHash;
    private bool hasWorkingParameter;
    private bool hasWorkingState;

    private Vector3 fromPosition;
    private Quaternion fromRotation;
    private Vector3 toPosition;
    private Quaternion toRotation;
    private float progress;
    private Action onFinished;

    // Foot planting
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform leftKnee;
    private Transform leftUpperLeg;
    private Transform rightUpperLeg;
    private float leftLegLength;
    private float rightLegLength;
    private Transform rightKnee;
    private float feetWeight;

    // Hands on the desk
    private Transform leftUpperArm;
    private Transform rightUpperArm;
    private float leftArmLength;
    private float rightArmLength;
    private float handsWeight;

    // Fixed placement (Fit To Seat off): where the character was last put, to notice later changes
    private Vector3 appliedOffset;
    private Vector3 appliedSeatPosition;
    private Quaternion appliedSeatRotation;

    // Chair fitting
    private Transform hips;
    private bool fitted;
    private Vector3 fitLocal;          // correction found by fitting, in the seat's local space
    private float fitTimer;
    private Vector3 hipsOffsetSum;
    private int fitSamples;
    private int fitFrames;
    private float standingHipsHeight;
    private bool fitMoving;
    private float fitProgress;
    private Vector3 fitFrom;
    private Vector3 fittedHipTarget;
    private Vector3 fittedSeatOffset;
    private Vector3 fittedSeatPosition;
    private Quaternion fittedSeatRotation;
    private Vector3 fitTo;

    public Transform Seat => seat;
    public Vector3 SeatPosition => seat != null ? seat.TransformPoint(seatOffset) : transform.position;
    public bool IsSeated => phase == Phase.Seated;
    public bool IsBusy => phase == Phase.SittingDown || phase == Phase.StandingUp;

    // Where the NPC's feet-origin should stand, including any correction found by fitting to the chair.
    private Vector3 RootTarget => SeatPosition + (fitted && seat != null ? seat.TransformVector(fitLocal) : Vector3.zero);

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            UnityEngine.Debug.LogWarning(name + ": no Animator found. The NPC will work without animation.", this);
            return;
        }

        workingHash = Animator.StringToHash(workingParameter);
        workingStateHash = Animator.StringToHash(workingStateName);

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == workingHash && p.type == AnimatorControllerParameterType.Bool)
                hasWorkingParameter = true;
        }

        hasWorkingState = animator.HasState(0, workingStateHash);

        if (!hasWorkingParameter)
            UnityEngine.Debug.LogWarning(name + ": Animator has no bool parameter '" + workingParameter + "' (re-run the NPC setup tools).", this);

        if (animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);

            // Hips height in the unposed (standing) skeleton: used to recognise a pose that has not been applied yet.
            if (hips != null)
                standingHipsHeight = hips.position.y - transform.position.y;
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);

            if (leftUpperLeg != null && leftKnee != null && leftFoot != null)
                leftLegLength = Vector3.Distance(leftUpperLeg.position, leftKnee.position) + Vector3.Distance(leftKnee.position, leftFoot.position);

            if (rightUpperLeg != null && rightKnee != null && rightFoot != null)
                rightLegLength = Vector3.Distance(rightUpperLeg.position, rightKnee.position) + Vector3.Distance(rightKnee.position, rightFoot.position);

            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (leftUpperArm != null && leftLowerArm != null && leftHand != null)
                leftArmLength = Vector3.Distance(leftUpperArm.position, leftLowerArm.position) + Vector3.Distance(leftLowerArm.position, leftHand.position);

            if (rightUpperArm != null && rightLowerArm != null && rightHand != null)
                rightArmLength = Vector3.Distance(rightUpperArm.position, rightLowerArm.position) + Vector3.Distance(rightLowerArm.position, rightHand.position);
        }

        animator.applyRootMotion = false;
    }

    private void Start()
    {
        if (seat == null)
        {
            // A warning, not an error: with Error Pause on in the Console, an error here would freeze the whole game.
            UnityEngine.Debug.LogWarning(name + ": NpcWorker has no Seat assigned, so this NPC stays where it is. Run Tools > Firewall Duty > NPC > Set Up Cubicle Workers / Set Up Printer Worker.", this);
            enabled = false;
            return;
        }

        if (fitToSeat && hips == null)
        {
            UnityEngine.Debug.LogWarning(name + ": 'Fit To Seat' needs a Humanoid Animator; fitting is off.", this);
            fitToSeat = false;
        }

        if (!startSeated)
            return;

        if (GetComponent<NpcWalker>() != null)
        {
            UnityEngine.Debug.LogWarning(name + ": has both NpcWalker and 'Start Seated'. Turn Start Seated off; the walker decides when to sit.", this);
            return;
        }

        SnapToSeat();
    }

    // Instantly places the NPC at the spot, already working (stationary workers use this at scene start).
    public void SnapToSeat()
    {
        transform.SetPositionAndRotation(RootTarget, FlatRotation(seat.rotation));
        EnterSeated();
        SetWorking(true);

        // Start each worker at a different point of the animation loop.
        if (animator != null && hasWorkingState)
            animator.Play(workingStateHash, 0, UnityEngine.Random.value);
    }

    // Smoothly moves from where the NPC stands to the spot and starts the working animation.
    public void SitDown(Action finished)
    {
        if (seat == null || IsBusy)
            return;

        Begin(Phase.SittingDown, RootTarget, FlatRotation(seat.rotation), finished);
        SetWorking(true);
    }

    // Smoothly leaves the spot toward 'exitPosition', facing it, and ends the working animation.
    public void StandUp(Vector3 exitPosition, Action finished)
    {
        if (IsBusy)
            return;

        Vector3 away = exitPosition - transform.position;
        away.y = 0f;
        Quaternion facing = away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away) : transform.rotation;

        fitMoving = false;
        Begin(Phase.StandingUp, exitPosition, facing, finished);
        SetWorking(false);
    }

    private void Begin(Phase next, Vector3 position, Quaternion rotation, Action finished)
    {
        phase = next;
        fromPosition = transform.position;
        fromRotation = transform.rotation;
        toPosition = position;
        toRotation = rotation;
        progress = 0f;
        onFinished = finished;
    }

    private void RecordApplied()
    {
        appliedOffset = seatOffset;

        if (seat != null)
        {
            appliedSeatPosition = seat.position;
            appliedSeatRotation = seat.rotation;
        }
    }

    private void EnterSeated()
    {
        phase = Phase.Seated;
        RecordApplied();
        fitTimer = 0f;
        fitSamples = 0;
        fitFrames = 0;
        hipsOffsetSum = Vector3.zero;
        fitMoving = false;
    }

    private void Update()
    {
        if (!IsBusy)
            return;

        progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / transitionSeconds);
        float t = Mathf.SmoothStep(0f, 1f, progress);
        transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, t), Quaternion.Slerp(fromRotation, toRotation, t));

        if (progress < 1f)
            return;

        bool sat = phase == Phase.SittingDown;
        if (sat)
            EnterSeated();
        else
            phase = Phase.Away;

        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }

    // Runs after the Animator has posed the skeleton, so the hip position measured here is the real one.
    private void LateUpdate()
    {
        // Fixed placement: with Fit To Seat off the character stands exactly at Seat + Seat Offset. If the offset or
        // the seat marker is changed (Inspector or Scene view) the character follows. Moving the character itself
        // is not tracked, so you can still drag it around in Play Mode to look at things.
        if (!fitToSeat && phase == Phase.Seated && seat != null
            && (seatOffset != appliedOffset
                || (seat.position - appliedSeatPosition).sqrMagnitude > 1e-6f
                || Quaternion.Angle(seat.rotation, appliedSeatRotation) > 0.1f))
        {
            transform.SetPositionAndRotation(RootTarget, FlatRotation(seat.rotation));
            RecordApplied();
        }

        // Live tuning: if Hip Target, the seat marker or the seat offset is changed (in the Inspector or by moving the
        // marker), fit again so the character follows. Moving the character itself is not tracked; the limbs are
        // pinned by IK, so tune with these values instead.
        if (fitToSeat && fitted && phase == Phase.Seated && seat != null
            && (hipTarget != fittedHipTarget || seatOffset != fittedSeatOffset
                || (seat.position - fittedSeatPosition).sqrMagnitude > 1e-6f
                || Quaternion.Angle(seat.rotation, fittedSeatRotation) > 0.1f))
        {
            fitted = false;
            fitMoving = false;
            fitTimer = FitSettleSeconds;   // the pose is already settled, go straight to measuring
            fitFrames = FitSettleFrames;
            fitSamples = 0;
            hipsOffsetSum = Vector3.zero;
        }

        if (!fitToSeat || fitted || phase != Phase.Seated || hips == null || seat == null)
            return;

        if (fitMoving)
        {
            fitProgress = Mathf.MoveTowards(fitProgress, 1f, Time.deltaTime / FitMoveSeconds);
            transform.position = Vector3.Lerp(fitFrom, fitTo, Mathf.SmoothStep(0f, 1f, fitProgress));

            if (fitProgress >= 1f)
            {
                fitLocal = seat.InverseTransformVector(fitTo - SeatPosition);
                fitted = true;
                fitMoving = false;
                fittedHipTarget = hipTarget;
                fittedSeatOffset = seatOffset;
                fittedSeatPosition = seat.position;
                fittedSeatRotation = seat.rotation;
            }

            return;
        }

        // Measure only once the working clip is really being shown. On the first frames of Play the Animator may not
        // have posed the skeleton yet (the hips are then at standing height), and a slow first frame must not be
        // allowed to use up the waiting time, so time is counted per frame with a cap and frames are counted too.
        fitTimer += Mathf.Min(Time.deltaTime, 0.05f);
        fitFrames++;

        if (fitTimer < FitSettleSeconds || fitFrames < FitSettleFrames)
            return;

        if (hasWorkingState && (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != workingStateHash))
        {
            RestartFitSampling();   // still blending into the working pose, not ready
            return;
        }

        // A seated pose has the hips well below standing height. If they are not, the pose is not applied yet.
        float hipsHeight = hips.position.y - transform.position.y;
        if (standingHipsHeight > 0.1f && hipsHeight > standingHipsHeight * 0.92f)
        {
            RestartFitSampling();
            return;
        }

        // Average the hips' offset from the feet-origin over a short window (the clip sways a little).
        hipsOffsetSum += hips.position - transform.position;
        fitSamples++;

        if (fitTimer < FitSettleSeconds + FitSampleSeconds || fitSamples < FitSampleFrames)
            return;

        Vector3 averageOffset = hipsOffsetSum / fitSamples;
        fitFrom = transform.position;
        fitTo = seat.TransformPoint(hipTarget) - averageOffset;
        fitProgress = 0f;
        fitMoving = true;
    }

    // With the hips lowered into a beanbag, the sitting clip would push the feet below the floor. Pin any foot that
    // would end up under the floor to floor level; the legs then bend at the knee instead of sinking.
    // (Runs inside the Animator update, which is where IK goals must be set. Needs IK Pass on the Base Layer.)
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || seat == null)
            return;

        UpdateFeetIK();
        UpdateHandsIK();
    }

    // Both hands rest on the desk surface while seated; the elbows stay out and down.
    private void UpdateHandsIK()
    {
        if (!restHandsOnDesk || leftHandRest == null || rightHandRest == null || leftUpperArm == null || rightUpperArm == null)
            return;

        handsWeight = Mathf.MoveTowards(handsWeight, phase == Phase.Seated ? 1f : 0f, Time.deltaTime * 3f);

        RestHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftHandRest, leftUpperArm, leftArmLength, -1f);
        RestHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightHandRest, rightUpperArm, rightArmLength, 1f);
    }

    private void RestHand(AvatarIKGoal goal, AvatarIKHint elbowHint, Transform rest, Transform shoulder, float armLength, float side)
    {
        // The arm can only stretch so far; never ask the IK for a point beyond it.
        Vector3 offset = rest.position - shoulder.position;
        float max = armLength * 0.97f;
        Vector3 target = armLength > 0f && offset.sqrMagnitude > max * max ? shoulder.position + offset.normalized * max : rest.position;

        animator.SetIKPositionWeight(goal, handsWeight);
        animator.SetIKPosition(goal, target);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        animator.SetIKHintPositionWeight(elbowHint, handsWeight * 0.6f);
        animator.SetIKHintPosition(elbowHint, shoulder.position + right * (0.25f * side) + Vector3.down * 0.25f - forward * 0.05f);
    }

    private void UpdateFeetIK()
    {
        if (!(keepFeetAboveFloor || straightenLegs) || leftFoot == null || rightFoot == null)
            return;

        float target = phase == Phase.Seated ? 1f : 0f;
        feetWeight = Mathf.MoveTowards(feetWeight, target, Time.deltaTime * 4f);

        float minAnkleY = seat.position.y + ankleHeight;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if (straightenLegs && leftUpperLeg != null && rightUpperLeg != null && leftLegLength > 0f && rightLegLength > 0f)
        {
            ExtendLeg(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftUpperLeg, leftKnee, leftLegLength, minAnkleY, forward);
            ExtendLeg(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightUpperLeg, rightKnee, rightLegLength, minAnkleY, forward);
            return;
        }

        PlantFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftFoot, leftKnee, minAnkleY, forward);
        PlantFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightFoot, rightKnee, minAnkleY, forward);
    }

    // Puts the foot on the floor straight out in front of the hip, almost at full leg reach, so the leg is nearly
    // straight. The reach is limited by leg length: a hip that is h above the ankle height leaves sqrt(L^2 - h^2)
    // of forward distance.
    private void ExtendLeg(AvatarIKGoal goal, AvatarIKHint kneeHint, Transform upperLeg, Transform knee,
                           float legLength, float ankleY, Vector3 forward)
    {
        float reach = legLength * legExtension;
        float drop = Mathf.Max(0f, upperLeg.position.y - ankleY);
        float forwardDistance = Mathf.Sqrt(Mathf.Max(0f, reach * reach - drop * drop));

        Vector3 hip = upperLeg.position;
        Vector3 ankle = new Vector3(hip.x, ankleY, hip.z) + forward * forwardDistance;

        animator.SetIKPositionWeight(goal, feetWeight);
        animator.SetIKPosition(goal, ankle);
        animator.SetIKRotationWeight(goal, 0f);

        // Let the knee lift slightly, never fold sideways.
        if (knee != null)
        {
            animator.SetIKHintPositionWeight(kneeHint, feetWeight * 0.4f);
            animator.SetIKHintPosition(kneeHint, Vector3.Lerp(hip, ankle, 0.5f) + Vector3.up * 0.3f);
        }
    }

    private void PlantFoot(AvatarIKGoal goal, AvatarIKHint kneeHint, Transform foot, Transform knee, float minAnkleY, Vector3 forward)
    {
        Vector3 position = foot.position;
        if (position.y < minAnkleY)
            position.y = minAnkleY;

        animator.SetIKPositionWeight(goal, feetWeight);
        animator.SetIKPosition(goal, position);
        animator.SetIKRotationWeight(goal, 0f);

        // Bend the knee forward and up, not sideways.
        if (knee != null)
        {
            animator.SetIKHintPositionWeight(kneeHint, feetWeight * 0.6f);
            animator.SetIKHintPosition(kneeHint, knee.position + forward * 0.4f + Vector3.up * 0.15f);
        }
    }

    // Throw away the samples so far and start measuring again (the pose was not ready yet).
    private void RestartFitSampling()
    {
        fitSamples = 0;
        hipsOffsetSum = Vector3.zero;
        fitTimer = FitSettleSeconds;
        fitFrames = FitSettleFrames;
    }

    private void SetWorking(bool working)
    {
        if (hasWorkingParameter && animator != null)
            animator.SetBool(workingHash, working);
    }

    private static Quaternion FlatRotation(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
    }

    private void OnDrawGizmosSelected()
    {
        if (seat == null)
            return;

        Vector3 p = SeatPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(p + Vector3.up * 0.5f, new Vector3(0.5f, 1f, 0.5f));
        Gizmos.DrawLine(p + Vector3.up * 0.5f, p + Vector3.up * 0.5f + seat.forward * 0.8f);

        if (fitToSeat)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(seat.TransformPoint(hipTarget), 0.1f);
        }
    }
}

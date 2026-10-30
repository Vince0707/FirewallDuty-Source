using UnityEngine;

// A seated NPC who is waiting and drums his fingers on his legs. There is no such clip in the project, so while the sitting clip plays, both
// hands are moved with Humanoid hand IK onto the tops of the thighs and tap them one after the other (like a "tap-tap-tap, what is taking so
// long" wait), for a few seconds; then the hands go back to the sitting pose, he rests, and it starts again.
// Needs a Humanoid Animator on this GameObject, with "IK Pass" ticked on its Base Layer (the setup tool does this).
[RequireComponent(typeof(Animator))]
public class NpcLapTapper : MonoBehaviour
{
    private enum Step
    {
        Rest,
        Tap
    }

    [Header("Timing (seconds)")]
    [Tooltip("One tap of one hand; the other hand taps in between, so the two together make a steady beat.")]
    [SerializeField, Min(0.15f)] private float tapSeconds = 0.55f;
    [SerializeField, Min(1)] private int tapsMin = 6;
    [SerializeField, Min(1)] private int tapsMax = 12;
    [SerializeField, Min(0f)] private float restMin = 2f;
    [SerializeField, Min(0f)] private float restMax = 5f;
    [Tooltip("Random wait (0 to this value) before the first tapping, so several NPCs don't start together.")]
    [SerializeField, Min(0f)] private float maxStartDelay = 3f;

    [Header("Movement")]
    [Tooltip("How high a hand lifts above the thigh between taps, in metres.")]
    [SerializeField, Range(0.01f, 0.15f)] private float lift = 0.06f;
    [Tooltip("How far along the thigh the hand rests: 0 = at the hip, 1 = at the knee.")]
    [SerializeField, Range(0.2f, 0.9f)] private float alongThigh = 0.6f;
    [Tooltip("How far above the thigh's centre line the skin is, in metres (the hand rests on top).")]
    [SerializeField, Range(0f, 0.2f)] private float thighSurface = 0.075f;

    private Animator animator;
    private Transform leftUpperLeg;
    private Transform leftKnee;
    private Transform rightUpperLeg;
    private Transform rightKnee;
    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform leftHand;
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform rightHand;
    private float leftArmLength;
    private float rightArmLength;

    private Step step = Step.Rest;
    private float timer;
    private float duration;
    private float weight;
    private float tapClock;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (animator == null || !animator.isHuman)
        {
            UnityEngine.Debug.LogWarning(name + ": NpcLapTapper needs a Humanoid Animator.", this);
            enabled = false;
            return;
        }

        leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (leftUpperLeg == null || leftKnee == null || rightUpperLeg == null || rightKnee == null
            || leftUpperArm == null || leftLowerArm == null || leftHand == null
            || rightUpperArm == null || rightLowerArm == null || rightHand == null)
        {
            UnityEngine.Debug.LogWarning(name + ": the Humanoid avatar is missing arm or leg bones, so it cannot tap its legs.", this);
            enabled = false;
            return;
        }

        leftArmLength = Vector3.Distance(leftUpperArm.position, leftLowerArm.position) + Vector3.Distance(leftLowerArm.position, leftHand.position);
        rightArmLength = Vector3.Distance(rightUpperArm.position, rightLowerArm.position) + Vector3.Distance(rightLowerArm.position, rightHand.position);
    }

    private void Start()
    {
        StartStep(Step.Rest, Random.Range(0f, maxStartDelay));
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (step == Step.Rest)
        {
            weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime * 2.5f);
            if (timer >= duration)
            {
                tapClock = 0f;
                int taps = Random.Range(tapsMin, Mathf.Max(tapsMin, tapsMax) + 1);
                StartStep(Step.Tap, taps * tapSeconds);
            }
        }
        else
        {
            weight = Mathf.MoveTowards(weight, 1f, Time.deltaTime * 2.5f);
            tapClock += Time.deltaTime;

            if (timer >= duration)
                StartStep(Step.Rest, Random.Range(restMin, Mathf.Max(restMin, restMax)));
        }
    }

    private void StartStep(Step next, float seconds)
    {
        step = next;
        timer = 0f;
        duration = seconds;
    }

    // The top of a thigh: part of the way from the hip joint to the knee, on the skin above the bone.
    private Vector3 ThighPoint(Transform upperLeg, Transform knee)
    {
        return Vector3.Lerp(upperLeg.position, knee.position, alongThigh) + Vector3.up * (thighSurface * Mathf.Abs(transform.lossyScale.y));
    }

    // 0 while the hand rests on the thigh, 1 at the top of its lift; the two hands are half a beat apart.
    private float Lift(float offset)
    {
        float phase = Mathf.Repeat(tapClock / tapSeconds + offset, 1f);
        return 1f - Mathf.Sin(phase * Mathf.PI);
    }

    // Runs inside the Animator update, which is where IK goals must be set.
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || animator == null || leftUpperLeg == null)
            return;

        Vector3 up = Vector3.up * lift * Mathf.Abs(transform.lossyScale.y);

        Drive(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftUpperArm, leftArmLength, ThighPoint(leftUpperLeg, leftKnee) + up * Lift(0f), -1f);
        Drive(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightUpperArm, rightArmLength, ThighPoint(rightUpperLeg, rightKnee) + up * Lift(0.5f), 1f);
    }

    private void Drive(AvatarIKGoal goal, AvatarIKHint hint, Transform upperArm, float armLength, Vector3 target, float side)
    {
        // The arm can only stretch so far; never ask the IK for a point beyond it.
        Vector3 fromShoulder = target - upperArm.position;
        float max = armLength * 0.97f;
        if (fromShoulder.sqrMagnitude > max * max)
            target = upperArm.position + fromShoulder.normalized * max;

        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKPosition(goal, target);

        // Elbows out a little and down, so the arms do not fold through the body.
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        animator.SetIKHintPositionWeight(hint, weight * 0.6f);
        animator.SetIKHintPosition(hint, upperArm.position + right * (0.2f * side) + Vector3.down * 0.25f - forward * 0.05f);
    }
}

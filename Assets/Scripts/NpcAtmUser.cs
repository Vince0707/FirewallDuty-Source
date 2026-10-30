using UnityEngine;

// A customer using a cash machine. There is no ATM clip in the project, so the NPC stands in front of the machine (NpcWorker) and this
// moves one hand to the keypad with Humanoid hand IK on top of the standing clip: it reaches out, taps a few different keys, pulls the hand
// back, rests for a few seconds and does it again, while the head looks at the screen.
// Needs a Humanoid Animator on this GameObject, with "IK Pass" ticked on its Base Layer (the setup tool does this).
[RequireComponent(typeof(Animator))]
public class NpcAtmUser : MonoBehaviour
{
    private enum Step
    {
        Rest,
        Reach,
        Tap
    }

    [Tooltip("A marker on the machine's keypad. Its blue (forward) axis points INTO the machine; the hand touches it.")]
    [SerializeField] private Transform keypad;
    [SerializeField] private bool useRightHand = true;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.1f)] private float reachSeconds = 0.8f;
    [SerializeField, Min(0.1f)] private float tapSeconds = 0.45f;
    [SerializeField, Min(1)] private int tapsMin = 4;
    [SerializeField, Min(1)] private int tapsMax = 8;
    [SerializeField, Min(0f)] private float restMin = 2f;
    [SerializeField, Min(0f)] private float restMax = 5f;
    [Tooltip("Random wait (0 to this value) before the first reach, so customers don't all start together.")]
    [SerializeField, Min(0f)] private float maxStartDelay = 3f;

    [Header("Movement")]
    [Tooltip("How far the hand pulls back from the keypad between taps, in metres.")]
    [SerializeField, Range(0.01f, 0.15f)] private float tapLift = 0.05f;
    [Tooltip("How far across the keypad the hand wanders between keys (right/left, up/down), in metres.")]
    [SerializeField] private Vector2 keySpread = new Vector2(0.07f, 0.05f);
    [Tooltip("Where the eyes look, relative to the keypad marker (up = the screen above the keys).")]
    [SerializeField] private Vector3 screenOffset = new Vector3(0f, 0.28f, 0f);
    [SerializeField, Range(0f, 1f)] private float lookWeight = 0.7f;

    private Animator animator;
    private Transform upperArm;
    private Transform lowerArm;
    private Transform handBone;
    private AvatarIKGoal goal;
    private AvatarIKHint hint;
    private float armLength;

    private Step step = Step.Rest;
    private float timer;
    private float duration;
    private int tapsLeft;
    private float handWeight;
    private Vector3 handStart;
    private Vector3 handGoal;
    private Vector2 keyOffset;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (keypad == null)
        {
            UnityEngine.Debug.LogWarning(name + ": NpcAtmUser has no keypad marker assigned, so it does nothing.", this);
            enabled = false;
            return;
        }

        if (animator == null || !animator.isHuman)
        {
            UnityEngine.Debug.LogWarning(name + ": NpcAtmUser needs a Humanoid Animator.", this);
            enabled = false;
            return;
        }

        upperArm = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        lowerArm = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        handBone = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        goal = useRightHand ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
        hint = useRightHand ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;

        if (upperArm == null || lowerArm == null || handBone == null)
        {
            UnityEngine.Debug.LogWarning(name + ": the Humanoid avatar is missing arm bones, so it cannot use the machine.", this);
            enabled = false;
            return;
        }

        armLength = Vector3.Distance(upperArm.position, lowerArm.position) + Vector3.Distance(lowerArm.position, handBone.position);
    }

    private void Start()
    {
        StartStep(Step.Rest, Random.Range(0f, maxStartDelay));
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(timer / duration) : 1f;

        switch (step)
        {
            case Step.Rest:
                handWeight = Mathf.MoveTowards(handWeight, 0f, Time.deltaTime * 2.5f);
                if (timer >= duration)
                {
                    handStart = handBone.position;
                    keyOffset = RandomKey();
                    StartStep(Step.Reach, reachSeconds);
                }

                break;

            case Step.Reach:
                handWeight = Mathf.MoveTowards(handWeight, 1f, Time.deltaTime * 2.5f);
                handGoal = Vector3.Lerp(handStart, KeyPoint(keyOffset, tapLift), Mathf.SmoothStep(0f, 1f, t));
                if (timer >= duration)
                {
                    tapsLeft = Random.Range(tapsMin, Mathf.Max(tapsMin, tapsMax) + 1);
                    StartStep(Step.Tap, tapSeconds);
                }

                break;

            case Step.Tap:
                handWeight = Mathf.MoveTowards(handWeight, 1f, Time.deltaTime * 2.5f);

                // Pulled back at the start and end of a tap, touching the key in the middle.
                float pull = tapLift * (1f - Mathf.Sin(t * Mathf.PI));
                handGoal = KeyPoint(keyOffset, pull);

                if (timer >= duration)
                {
                    tapsLeft--;
                    if (tapsLeft <= 0)
                    {
                        StartStep(Step.Rest, Random.Range(restMin, Mathf.Max(restMin, restMax)));
                    }
                    else
                    {
                        keyOffset = RandomKey();
                        StartStep(Step.Tap, tapSeconds);
                    }
                }

                break;
        }
    }

    private void StartStep(Step next, float seconds)
    {
        step = next;
        timer = 0f;
        duration = seconds;
    }

    private Vector2 RandomKey()
    {
        return new Vector2(Random.Range(-keySpread.x, keySpread.x), Random.Range(-keySpread.y, keySpread.y));
    }

    // A point on the keypad (a key) pulled back from the surface by 'pull' metres.
    private Vector3 KeyPoint(Vector2 key, float pull)
    {
        return keypad.position + keypad.right * key.x + keypad.up * key.y - keypad.forward * pull;
    }

    // The arm can only stretch so far; never ask the IK for a point beyond it.
    private Vector3 ClampToReach(Vector3 target)
    {
        Vector3 fromShoulder = target - upperArm.position;
        float max = armLength * 0.97f;
        return fromShoulder.sqrMagnitude > max * max ? upperArm.position + fromShoulder.normalized * max : target;
    }

    // Runs inside the Animator update, which is where IK goals must be set.
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || animator == null || upperArm == null)
            return;

        animator.SetIKPositionWeight(goal, handWeight);
        animator.SetIKPosition(goal, ClampToReach(handGoal));

        // Keep the elbow out and down so the arm does not fold through the body.
        float side = useRightHand ? 1f : -1f;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 elbow = upperArm.position + right * (0.25f * side) + Vector3.down * 0.25f - forward * 0.05f;
        animator.SetIKHintPositionWeight(hint, handWeight * 0.6f);
        animator.SetIKHintPosition(hint, elbow);

        // The eyes stay on the screen.
        animator.SetLookAtWeight(lookWeight, 0.15f, 0.6f, 0.1f, 0.5f);
        animator.SetLookAtPosition(keypad.position + keypad.right * screenOffset.x + keypad.up * screenOffset.y);
    }
}

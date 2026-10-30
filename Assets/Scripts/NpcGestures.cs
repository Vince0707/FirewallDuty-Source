using UnityEngine;

// Hand gestures for a standing NPC who is explaining something. There is no gesturing clip in the project, so the
// hands are moved with Humanoid hand IK on top of whatever clip is playing: every few seconds one of a handful of
// gestures (open palms, pointing, counting, a wide spread, a chop for emphasis) is picked, the hands move to it,
// hold with a little wobble, then relax back into the animation.
// Needs a Humanoid Animator on this GameObject, with "IK Pass" ticked on its Base Layer (the setup tool does this).
[RequireComponent(typeof(Animator))]
public class NpcGestures : MonoBehaviour
{
    // Hand targets are given relative to the chest, as (right, up, forward) in metres, in the character's own frame.
    private struct Gesture
    {
        public bool useLeft;
        public Vector3 left;
        public bool useRight;
        public Vector3 right;
        public float wobbleSpeed;

        public Gesture(bool useLeft, Vector3 left, bool useRight, Vector3 right, float wobbleSpeed)
        {
            this.useLeft = useLeft;
            this.left = left;
            this.useRight = useRight;
            this.right = right;
            this.wobbleSpeed = wobbleSpeed;
        }
    }

    private static readonly Gesture[] Gestures =
    {
        // Open palms, "so here is the situation"
        new Gesture(true, new Vector3(-0.28f, -0.08f, 0.38f), true, new Vector3(0.28f, -0.08f, 0.38f), 1.2f),
        // Wide spread, "the whole picture"
        new Gesture(true, new Vector3(-0.50f, 0.00f, 0.30f), true, new Vector3(0.50f, 0.00f, 0.30f), 0.9f),
        // One hand out and raised, making a point
        new Gesture(false, Vector3.zero, true, new Vector3(0.26f, 0.10f, 0.46f), 1.6f),
        // Counting off, right hand raised with small bobs
        new Gesture(false, Vector3.zero, true, new Vector3(0.20f, 0.18f, 0.34f), 3.0f),
        // Both hands together in front, "let me explain"
        new Gesture(true, new Vector3(-0.10f, -0.05f, 0.34f), true, new Vector3(0.10f, -0.05f, 0.34f), 1.0f),
        // Left hand only, a lighter gesture
        new Gesture(true, new Vector3(-0.28f, 0.02f, 0.40f), false, Vector3.zero, 1.4f),
        // Chop for emphasis
        new Gesture(false, Vector3.zero, true, new Vector3(0.16f, -0.02f, 0.42f), 3.6f)
    };

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.2f)] private float restMin = 1.5f;
    [SerializeField, Min(0.2f)] private float restMax = 3.5f;
    [SerializeField, Min(0.2f)] private float holdMin = 1.6f;
    [SerializeField, Min(0.2f)] private float holdMax = 3.2f;
    [Tooltip("How long the hands take to move into and out of a gesture.")]
    [SerializeField, Min(0.1f)] private float moveSeconds = 0.5f;
    [Tooltip("Random wait (0 to this value) before the first gesture, so NPCs don't all start together.")]
    [SerializeField, Min(0f)] private float maxStartDelay = 3f;

    [Header("Size")]
    [Tooltip("Scales every gesture: 1 = as designed, 0.7 = smaller and calmer, 1.3 = bigger and more animated.")]
    [SerializeField, Range(0.4f, 1.6f)] private float gestureSize = 1f;
    [Tooltip("How much the hands drift while a gesture is held, in metres.")]
    [SerializeField, Range(0f, 0.08f)] private float wobble = 0.025f;

    private enum Phase
    {
        Rest,
        Gesturing
    }

    private Animator animator;
    private Transform chest;
    private readonly Transform[] upperArm = new Transform[2];   // 0 = left, 1 = right
    private readonly float[] armLength = new float[2];
    private readonly Transform[] handBone = new Transform[2];
    private readonly Vector3[] goalPosition = new Vector3[2];
    private readonly Vector3[] goalVelocity = new Vector3[2];
    private readonly float[] weight = new float[2];
    private readonly bool[] active = new bool[2];
    private readonly Vector3[] targetOffset = new Vector3[2];

    private Phase phase = Phase.Rest;
    private float timer;
    private float wobbleSpeed;
    private float seed;
    private int lastGesture = -1;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        seed = Random.value * 100f;

        if (animator == null || !animator.isHuman)
        {
            UnityEngine.Debug.LogWarning(name + ": NpcGestures needs a Humanoid Animator.", this);
            enabled = false;
            return;
        }

        chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null)
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null)
            chest = animator.GetBoneTransform(HumanBodyBones.Spine);

        upperArm[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        upperArm[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        handBone[0] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        handBone[1] = animator.GetBoneTransform(HumanBodyBones.RightHand);

        Transform[] lowerArm =
        {
            animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
            animator.GetBoneTransform(HumanBodyBones.RightLowerArm)
        };

        for (int i = 0; i < 2; i++)
        {
            if (upperArm[i] == null || lowerArm[i] == null || handBone[i] == null)
            {
                UnityEngine.Debug.LogWarning(name + ": the Humanoid avatar is missing arm bones, so it cannot gesture.", this);
                enabled = false;
                return;
            }

            armLength[i] = Vector3.Distance(upperArm[i].position, lowerArm[i].position) + Vector3.Distance(lowerArm[i].position, handBone[i].position);
        }

        if (chest == null)
        {
            UnityEngine.Debug.LogWarning(name + ": the Humanoid avatar has no chest bone, so it cannot gesture.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        StartRest(Random.Range(0f, maxStartDelay));
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            if (phase == Phase.Rest)
                StartGesture();
            else
                StartRest(Random.Range(restMin, Mathf.Max(restMin, restMax)));
        }

        float step = Time.deltaTime / moveSeconds;

        for (int i = 0; i < 2; i++)
        {
            bool on = phase == Phase.Gesturing && active[i];
            weight[i] = Mathf.MoveTowards(weight[i], on ? 1f : 0f, step);

            if (weight[i] < 0.01f)
            {
                // Not gesturing: follow the real hand, so the next gesture starts from where the hand is.
                goalPosition[i] = handBone[i].position;
                goalVelocity[i] = Vector3.zero;
            }
            else if (on)
            {
                goalPosition[i] = Vector3.SmoothDamp(goalPosition[i], TargetFor(i), ref goalVelocity[i], moveSeconds * 0.6f);
            }
        }
    }

    private void StartRest(float seconds)
    {
        phase = Phase.Rest;
        timer = seconds;
    }

    private void StartGesture()
    {
        // Pick one at random, but never the same one twice in a row.
        int index;
        do
        {
            index = Random.Range(0, Gestures.Length);
        }
        while (index == lastGesture && Gestures.Length > 1);

        lastGesture = index;
        Gesture g = Gestures[index];

        active[0] = g.useLeft;
        active[1] = g.useRight;
        targetOffset[0] = g.left;
        targetOffset[1] = g.right;
        wobbleSpeed = g.wobbleSpeed;

        phase = Phase.Gesturing;
        timer = Random.Range(holdMin, Mathf.Max(holdMin, holdMax));
    }

    private Vector3 Forward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    private Vector3 TargetFor(int hand)
    {
        Vector3 forward = Forward();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 o = targetOffset[hand] * gestureSize;

        // Small drift so a held gesture is never frozen; each hand wobbles out of step with the other.
        float t = Time.time * wobbleSpeed + seed + hand * 1.7f;
        Vector3 drift = right * (Mathf.Sin(t) * wobble) + Vector3.up * (Mathf.Sin(t * 1.3f + 0.8f) * wobble) + forward * (Mathf.Sin(t * 0.7f) * wobble * 0.5f);

        return chest.position + right * o.x + Vector3.up * o.y + forward * o.z + drift;
    }

    // Runs inside the Animator update, which is where IK goals must be set.
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || chest == null)
            return;

        Vector3 forward = Forward();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        for (int i = 0; i < 2; i++)
        {
            AvatarIKGoal goal = i == 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            AvatarIKHint hint = i == 0 ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
            float side = i == 0 ? -1f : 1f;

            // The arm can only stretch so far; never ask the IK for a point beyond it.
            Vector3 fromShoulder = goalPosition[i] - upperArm[i].position;
            float max = armLength[i] * 0.97f;
            Vector3 target = fromShoulder.sqrMagnitude > max * max ? upperArm[i].position + fromShoulder.normalized * max : goalPosition[i];

            animator.SetIKPositionWeight(goal, Mathf.SmoothStep(0f, 1f, weight[i]));
            animator.SetIKPosition(goal, target);

            // Elbows out and down, so the arms never fold through the body.
            animator.SetIKHintPositionWeight(hint, weight[i] * 0.6f);
            animator.SetIKHintPosition(hint, upperArm[i].position + right * (0.25f * side) + Vector3.down * 0.25f - forward * 0.05f);
        }
    }
}

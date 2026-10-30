using UnityEngine;

// Procedural coffee drinking for an NPC standing at a table. There is no coffee-drinking clip in the project, so
// the arm is moved with Humanoid hand IK on top of whatever clip is playing (a standing idle):
//   reach to the cup -> grab -> lift to the mouth -> sip -> lower -> (sip again) -> put the cup back -> rest -> repeat.
// The cup is an existing object in the scene; it follows the hand while held and is put back exactly where it was.
// Needs: a Humanoid Animator on this GameObject, with "IK Pass" ticked on its Base Layer (the setup tool does this).
[RequireComponent(typeof(Animator))]
public class NpcCoffeeDrinker : MonoBehaviour
{
    private enum Step
    {
        Rest,
        Reach,
        Grab,
        LiftToMouth,
        Sip,
        LowerToChest,
        HoldAtChest,
        PutBack,
        Release
    }

    [Header("Cup")]
    [Tooltip("The cup on the table this NPC drinks from. It must not be Static, or it cannot move.")]
    [SerializeField] private Transform cup;
    [SerializeField] private bool useRightHand = true;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.1f)] private float reachSeconds = 1.1f;
    [SerializeField, Min(0f)] private float grabSeconds = 0.35f;
    [SerializeField, Min(0.1f)] private float liftSeconds = 1.3f;
    [SerializeField, Min(0.1f)] private float sipSeconds = 2.2f;
    [SerializeField, Min(0.1f)] private float lowerSeconds = 1.2f;
    [SerializeField, Min(0f)] private float holdAtChestSeconds = 1.6f;
    [SerializeField, Min(0.1f)] private float putBackSeconds = 1.3f;
    [SerializeField, Min(1)] private int sipsPerCup = 2;
    [SerializeField, Min(0f)] private float restMin = 6f;
    [SerializeField, Min(0f)] private float restMax = 14f;
    [Tooltip("Random wait (0 to this value) before the first reach, so NPCs don't all start together.")]
    [SerializeField, Min(0f)] private float maxStartDelay = 4f;

    [Header("Pose tuning (metres, relative to the character)")]
    [Tooltip("Cup centre when drinking: right / up / forward of the head bone. The head bone is near the middle of the head, so Y is negative (the mouth is below it). Raise Z if the cup goes into the face; lower Y if the cup is above the mouth. The yellow/cyan spheres in the Scene view show the aim.")]
    [SerializeField] private Vector3 mouthOffset = new Vector3(0f, -0.10f, 0.17f);
    [Tooltip("Cup centre when resting in the hand at the chest: right / up / forward of the head bone.")]
    [SerializeField] private Vector3 chestOffset = new Vector3(0f, -0.45f, 0.28f);
    [Tooltip("Where the wrist sits relative to the cup centre: right / up / forward of the character.")]
    [SerializeField] private Vector3 wristFromCup = new Vector3(0f, 0.03f, -0.06f);

    private Animator animator;
    private Transform head;
    private Transform upperArm;
    private Transform lowerArm;
    private Transform handBone;
    private AvatarIKGoal goal;
    private AvatarIKHint hint;
    private float armLength;

    private Vector3 cupHomePosition;
    private Quaternion cupHomeRotation;
    private bool holding;

    private Step step = Step.Rest;
    private float timer;
    private float duration;
    private int sipsLeft;
    private Vector3 handStart;
    private Vector3 cupStart;
    private Vector3 handGoal;
    private Vector3 cupCentre;
    private float handWeight;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (cup == null)
        {
            UnityEngine.Debug.LogWarning(name + ": NpcCoffeeDrinker has no cup assigned, so it does nothing.", this);
            enabled = false;
            return;
        }

        if (animator == null || !animator.isHuman)
        {
            UnityEngine.Debug.LogWarning(name + ": NpcCoffeeDrinker needs a Humanoid Animator.", this);
            enabled = false;
            return;
        }

        head = animator.GetBoneTransform(HumanBodyBones.Head);
        upperArm = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        lowerArm = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        handBone = animator.GetBoneTransform(useRightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        goal = useRightHand ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
        hint = useRightHand ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;

        if (head == null || upperArm == null || lowerArm == null || handBone == null)
        {
            UnityEngine.Debug.LogWarning(name + ": the Humanoid avatar is missing head or arm bones, so it cannot drink.", this);
            enabled = false;
            return;
        }

        armLength = Vector3.Distance(upperArm.position, lowerArm.position) + Vector3.Distance(lowerArm.position, handBone.position);

        foreach (Renderer r in cup.GetComponentsInChildren<Renderer>())
        {
            if (r.isPartOfStaticBatch)
            {
                UnityEngine.Debug.LogWarning(cup.name + " is Batching Static, so it will not visibly move. Untick Static on the cup.", cup);
                break;
            }
        }
    }

    private void Start()
    {
        // NpcWorker may have moved the NPC to its spot in Start; read the cup's home pose from the scene as placed.
        cupHomePosition = cup.position;
        cupHomeRotation = cup.rotation;
        cupCentre = cupHomePosition;
        StartStep(Step.Rest, Random.Range(0f, maxStartDelay));
    }

    private void OnDisable()
    {
        // Leave the cup where it belongs.
        if (cup != null && Application.isPlaying)
            cup.SetPositionAndRotation(cupHomePosition, cupHomeRotation);

        holding = false;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(timer / duration) : 1f;
        float eased = Mathf.SmoothStep(0f, 1f, t);

        switch (step)
        {
            case Step.Rest:
                handWeight = Mathf.MoveTowards(handWeight, 0f, Time.deltaTime * 2.5f);
                break;

            case Step.Reach:
                handWeight = Mathf.MoveTowards(handWeight, 1f, Time.deltaTime * 2.5f);
                handGoal = Vector3.Lerp(handStart, WristFor(cupHomePosition), eased);
                break;

            case Step.Grab:
                handGoal = WristFor(cupHomePosition);
                break;

            case Step.LiftToMouth:
                MoveCup(cupStart, MouthPoint(), eased);
                break;

            case Step.Sip:
                // A small tip of the cup toward the mouth: rise slightly and draw in.
                cupCentre = MouthPoint() + Vector3.up * (0.015f * Mathf.Sin(t * Mathf.PI)) + FlatForward() * (0.015f * Mathf.Sin(t * Mathf.PI));
                handGoal = WristFor(cupCentre);
                break;

            case Step.LowerToChest:
                MoveCup(cupStart, ChestPoint(), eased);
                break;

            case Step.HoldAtChest:
                cupCentre = ChestPoint();
                handGoal = WristFor(cupCentre);
                break;

            case Step.PutBack:
                MoveCup(cupStart, cupHomePosition, eased);
                break;

            case Step.Release:
                handWeight = Mathf.MoveTowards(handWeight, 0f, Time.deltaTime * 2f);
                handGoal = WristFor(cupHomePosition) + Vector3.up * (0.15f * eased) - FlatForward() * (0.1f * eased);
                break;
        }

        if (timer >= duration)
            Advance();
    }

    private void Advance()
    {
        switch (step)
        {
            case Step.Rest:
                handStart = handBone.position;
                handGoal = handStart;
                StartStep(Step.Reach, reachSeconds);
                break;

            case Step.Reach:
                StartStep(Step.Grab, grabSeconds);
                break;

            case Step.Grab:
                holding = true;
                sipsLeft = sipsPerCup;
                cupStart = cupHomePosition;
                StartStep(Step.LiftToMouth, liftSeconds);
                break;

            case Step.LiftToMouth:
                StartStep(Step.Sip, sipSeconds);
                break;

            case Step.Sip:
                sipsLeft--;
                cupStart = cupCentre;
                StartStep(Step.LowerToChest, lowerSeconds);
                break;

            case Step.LowerToChest:
                StartStep(Step.HoldAtChest, holdAtChestSeconds);
                break;

            case Step.HoldAtChest:
                cupStart = cupCentre;
                if (sipsLeft > 0)
                    StartStep(Step.LiftToMouth, liftSeconds);
                else
                    StartStep(Step.PutBack, putBackSeconds);
                break;

            case Step.PutBack:
                holding = false;
                cup.SetPositionAndRotation(cupHomePosition, cupHomeRotation);
                StartStep(Step.Release, 0.7f);
                break;

            case Step.Release:
                StartStep(Step.Rest, Random.Range(restMin, Mathf.Max(restMin, restMax)));
                break;
        }
    }

    private void StartStep(Step next, float seconds)
    {
        step = next;
        timer = 0f;
        duration = seconds;
    }

    private void MoveCup(Vector3 from, Vector3 to, float eased)
    {
        cupCentre = Vector3.Lerp(from, to, eased);
        handGoal = WristFor(cupCentre);
    }

    private Vector3 FlatForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    private Vector3 FlatRight()
    {
        return Vector3.Cross(Vector3.up, FlatForward());
    }

    private Vector3 HeadRelative(Vector3 offset)
    {
        return head.position + FlatRight() * offset.x + Vector3.up * offset.y + FlatForward() * offset.z;
    }

    private Vector3 MouthPoint() => HeadRelative(mouthOffset);
    private Vector3 ChestPoint() => HeadRelative(chestOffset);

    private Vector3 WristFor(Vector3 cupPosition)
    {
        return cupPosition + FlatRight() * wristFromCup.x + Vector3.up * wristFromCup.y + FlatForward() * wristFromCup.z;
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
        if (layerIndex != 0 || animator == null || head == null)
            return;

        animator.SetIKPositionWeight(goal, handWeight);
        animator.SetIKPosition(goal, ClampToReach(handGoal));

        // Keep the elbow out and down so the arm does not fold through the body.
        float side = useRightHand ? 1f : -1f;
        Vector3 elbow = upperArm.position + FlatRight() * (0.25f * side) + Vector3.down * 0.25f - FlatForward() * 0.05f;
        animator.SetIKHintPositionWeight(hint, handWeight * 0.6f);
        animator.SetIKHintPosition(hint, elbow);

        // Glance at the cup while it is in hand.
        animator.SetLookAtWeight(handWeight * 0.5f, 0.1f, 0.5f, 0f, 0.5f);
        animator.SetLookAtPosition(cupCentre);
    }

    private void LateUpdate()
    {
        if (holding)
            cup.position = cupCentre;
    }

    private void OnDrawGizmosSelected()
    {
        if (cup == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Application.isPlaying ? cupHomePosition : cup.position, 0.06f);

        // Where the cup is aimed while sipping (cyan) and while held at the chest (blue). Visible in Play Mode.
        if (Application.isPlaying && head != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(MouthPoint(), 0.045f);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(ChestPoint(), 0.045f);
        }
    }
}

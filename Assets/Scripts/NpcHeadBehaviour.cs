using UnityEngine;

// Head behaviour layered on top of whatever clip is playing: look toward another character, and either nod
// (a listener) or make small speaking movements (a talker). There is no talking or nodding clip in the project,
// so the head bone is moved directly after the Animator has posed the skeleton.
// Needs a Humanoid Animator on this GameObject, with "IK Pass" ticked on its Base Layer (the setup tool does this).
[RequireComponent(typeof(Animator))]
public class NpcHeadBehaviour : MonoBehaviour
{
    public enum Mode
    {
        Nod,
        Talk
    }

    [Header("Look at")]
    [Tooltip("The character (or any object) to look toward.")]
    [SerializeField] private Transform lookAtTarget;
    [Tooltip("Height above the target's origin to look at, in metres: about 1.5 for a standing person, 1.15 for a seated one.")]
    [SerializeField, Min(0f)] private float lookAtHeight = 1.4f;
    [SerializeField, Range(0f, 1f)] private float lookAtWeight = 0.85f;

    [Header("Behaviour")]
    [SerializeField] private Mode mode = Mode.Nod;

    [Header("Nodding (Mode = Nod)")]
    [Tooltip("How far the head tips forward on each nod, in degrees.")]
    [SerializeField, Range(1f, 30f)] private float nodAngle = 12f;
    [SerializeField, Min(0.2f)] private float secondsPerNod = 0.8f;
    [SerializeField, Min(1)] private int nodsPerBurstMin = 2;
    [SerializeField, Min(1)] private int nodsPerBurstMax = 3;
    [SerializeField, Min(0.5f)] private float pauseMin = 3f;
    [SerializeField, Min(0.5f)] private float pauseMax = 8f;

    [Header("Speaking (Mode = Talk)")]
    [Tooltip("Size of the small head movements while talking, in degrees.")]
    [SerializeField, Range(0.5f, 12f)] private float talkAmplitude = 4f;
    [SerializeField, Min(0.1f)] private float talkSpeed = 1.4f;

    private Animator animator;
    private Transform head;
    private float timer;
    private int nodsLeft;
    private float nodPhase;
    private float seed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        seed = Random.value * 100f;

        if (animator != null && animator.isHuman)
            head = animator.GetBoneTransform(HumanBodyBones.Head);

        if (head == null)
        {
            UnityEngine.Debug.LogWarning(name + ": NpcHeadBehaviour needs a Humanoid Animator with a head bone.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        timer = Random.Range(pauseMin * 0.3f, pauseMax);
    }

    // Look at something else from now on (used when a character is moved to another place).
    public void LookAt(Transform target, float height)
    {
        lookAtTarget = target;
        lookAtHeight = height;
    }

    // Eyes and head turn toward the target; runs inside the Animator update.
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || lookAtTarget == null)
            return;

        animator.SetLookAtWeight(lookAtWeight, 0.15f, 0.8f, 0.3f, 0.6f);
        animator.SetLookAtPosition(lookAtTarget.position + Vector3.up * lookAtHeight);
    }

    // After the Animator has posed the skeleton: add the nod / speaking movement on top.
    private void LateUpdate()
    {
        float pitch = 0f;
        float yaw = 0f;

        if (mode == Mode.Nod)
            pitch = NodPitch();
        else
            TalkMotion(out pitch, out yaw);

        if (Mathf.Approximately(pitch, 0f) && Mathf.Approximately(yaw, 0f))
            return;

        // Tip about the character's right axis (positive = nose down) and turn about the vertical axis.
        head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, transform.right) * head.rotation;
    }

    private float NodPitch()
    {
        if (nodsLeft <= 0)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                nodsLeft = Random.Range(nodsPerBurstMin, Mathf.Max(nodsPerBurstMin, nodsPerBurstMax) + 1);
                nodPhase = 0f;
            }

            return 0f;
        }

        nodPhase += Time.deltaTime / secondsPerNod;
        if (nodPhase >= 1f)
        {
            nodPhase -= 1f;
            nodsLeft--;

            if (nodsLeft <= 0)
            {
                timer = Random.Range(pauseMin, Mathf.Max(pauseMin, pauseMax));
                return 0f;
            }
        }

        // One smooth dip down and back up per nod.
        return nodAngle * Mathf.Sin(nodPhase * Mathf.PI);
    }

    // Slow overlapping sway, with a small emphasis nod now and then, so the head never looks frozen while talking.
    private void TalkMotion(out float pitch, out float yaw)
    {
        float t = Time.time * talkSpeed + seed;
        float emphasis = Mathf.Max(0f, Mathf.Sin(t * 1.7f)) * Mathf.Max(0f, Mathf.Sin(t * 0.45f));

        pitch = talkAmplitude * (0.5f * Mathf.Sin(t * 1.3f) + emphasis);
        yaw = talkAmplitude * 0.6f * Mathf.Sin(t * 0.8f + 1.1f);
    }
}

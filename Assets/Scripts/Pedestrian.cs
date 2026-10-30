using UnityEngine;

// A person walking up and down a pavement, for the street outside the office (made by Tools > Firewall Duty > Office > Build Entrance Street). The
// street has no NavMesh, so this simply walks a straight line between two points: when it reaches one end it stops for a moment, turns around
// and walks back. The walking animation is the office's own (the Animator's "IsWalking" switch).
[RequireComponent(typeof(Animator))]
public class Pedestrian : MonoBehaviour
{
    [SerializeField] private Vector3 pointA;
    [SerializeField] private Vector3 pointB;
    [SerializeField, Min(0.2f)] private float speed = 1.3f;
    [Tooltip("Where on the way from A to B this person is when the scene starts (0 = at A, 1 = at B).")]
    [SerializeField, Range(0f, 1f)] private float startAt = 0f;
    [SerializeField, Min(0f)] private float pauseMin = 0.5f;
    [SerializeField, Min(0f)] private float pauseMax = 2.5f;
    [SerializeField] private string walkingParameter = "IsWalking";

    private Animator animator;
    private bool hasParameter;
    private bool goingToB = true;
    private float pause;

    public void Setup(Vector3 a, Vector3 b, float walkSpeed, float start)
    {
        pointA = a;
        pointB = b;
        speed = walkSpeed;
        startAt = start;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == walkingParameter && p.type == AnimatorControllerParameterType.Bool)
                    hasParameter = true;
            }
        }
    }

    private void Start()
    {
        transform.position = Vector3.Lerp(pointA, pointB, startAt);
        goingToB = Random.value < 0.5f;
        Face(goingToB ? pointB : pointA);
        SetWalking(true);
    }

    private void Update()
    {
        if (pause > 0f)
        {
            pause -= Time.deltaTime;
            if (pause <= 0f)
            {
                goingToB = !goingToB;
                SetWalking(true);
            }

            return;
        }

        Vector3 target = goingToB ? pointB : pointA;
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        float step = speed * Time.deltaTime;
        if (toTarget.magnitude <= step)
        {
            // At the end of the walk: stop for a moment, then turn around.
            transform.position = new Vector3(target.x, transform.position.y, target.z);
            pause = Random.Range(pauseMin, Mathf.Max(pauseMin, pauseMax));
            SetWalking(false);
            return;
        }

        transform.position += toTarget.normalized * step;

        Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 8f * Time.deltaTime);
    }

    private void Face(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void SetWalking(bool walking)
    {
        if (animator != null && hasParameter)
            animator.SetBool(walkingParameter, walking);
    }
}

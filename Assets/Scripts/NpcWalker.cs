using UnityEngine;
using UnityEngine.AI;

// Waypoint walking for an NPC. Add it only to NPCs that should wander; stationary NPCs simply don't have it.
// The NavMeshAgent does all movement and turning, so nothing else may move this Transform.
[RequireComponent(typeof(NavMeshAgent))]
public class NpcWalker : MonoBehaviour
{
    public enum RouteMode
    {
        Sequential,
        PingPong,
        Random
    }

    private enum State
    {
        Waiting,
        Walking,
        GoingToWork,
        Working,
        StandingUp
    }

    [Header("Route")]
    [Tooltip("Places to walk to. Each must be on the baked NavMesh and inside this NPC's area.")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private RouteMode routeMode = RouteMode.Sequential;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.2f;
    [Tooltip("How fast the NPC turns toward its walking direction, degrees per second.")]
    [SerializeField, Min(10f)] private float turnSpeed = 240f;

    [Header("Pauses")]
    [SerializeField, Min(0f)] private float minPause = 3f;
    [SerializeField, Min(0f)] private float maxPause = 8f;
    [Tooltip("Random wait (0 to this value) before the first walk, so NPCs don't all set off together.")]
    [SerializeField, Min(0f)] private float maxStartDelay = 6f;

    [Header("Area limit")]
    [Tooltip("Ignore waypoints, and routes that detour, outside Area Radius of where this NPC starts.")]
    [SerializeField] private bool restrictToArea = true;
    [SerializeField, Min(0.5f)] private float areaRadius = 4f;

    [Header("Work after walking (optional)")]
    [Tooltip("If set, the NPC walks the route, then sits down here and works. Leave empty to walk forever.")]
    [SerializeField] private NpcWorker workstation;
    [Tooltip("How many times the whole route is walked before going to work.")]
    [SerializeField, Min(1)] private int routePassesBeforeWork = 1;
    [Tooltip("Seconds spent working before getting up and walking again. 0 = keep working.")]
    [SerializeField, Min(0f)] private float workSeconds = 0f;

    [Header("Animation")]
    [Tooltip("Leave empty to find the Animator on this object or its children.")]
    [SerializeField] private Animator animator;
    [Tooltip("Bool parameter on the Animator Controller: true while walking, false while idle.")]
    [SerializeField] private string walkingParameter = "IsWalking";
    [SerializeField, Min(0.1f)] private float walkAnimationSpeed = 1f;

    private const float StuckSeconds = 3f;

    private NavMeshAgent agent;
    private State state = State.Waiting;
    private Vector3 areaCenter;
    private float timer;
    private int nextIndex;
    private int pingPongStep = 1;
    private float stuckTimer;
    private int walkingHash;
    private bool hasWalkingParameter;
    private bool animatorWalking;
    private bool warnedNoRoute;

    public bool IsWalking => state == State.Walking;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.angularSpeed = turnSpeed;
        agent.acceleration = 4f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
        agent.updateRotation = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            walkingHash = Animator.StringToHash(walkingParameter);
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == walkingHash && p.type == AnimatorControllerParameterType.Bool)
                    hasWalkingParameter = true;
            }

            if (!hasWalkingParameter)
                UnityEngine.Debug.LogWarning(name + ": Animator has no bool parameter '" + walkingParameter + "' (check its Animator Controller). The NPC will slide without walking animation.", this);

            animator.applyRootMotion = false;
            animator.speed = walkAnimationSpeed;
        }
        else
        {
            UnityEngine.Debug.LogWarning(name + ": no Animator found. The NPC will move without animation.", this);
        }
    }

    private void Start()
    {
        areaCenter = transform.position;

        if (!agent.isOnNavMesh)
        {
            // Try to snap onto the mesh in case the NPC was placed slightly above or beside it.
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                UnityEngine.Debug.LogWarning(name + " is not on a NavMesh, so it cannot walk. Bake the NavMesh (Tools > Firewall Duty > NPC > Set Up NPC Walking) and make sure the NPC stands on it.", this);
                enabled = false;
                return;
            }
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            UnityEngine.Debug.LogWarning(name + ": no waypoints assigned, so it will stand still.", this);
            enabled = false;
            return;
        }

        // Stagger starts and pauses so several walkers never move in lockstep.
        timer = Random.Range(0f, maxStartDelay);
        nextIndex = Random.Range(0, waypoints.Length);
    }

    private void OnDisable()
    {
        if (agent != null && agent.isOnNavMesh)
            agent.ResetPath();

        SetAnimatorWalking(false);
    }

    private void Update()
    {
        bool moving = false;

        switch (state)
        {
            case State.Waiting:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                    BeginNextWalk();
                break;

            case State.Walking:
                UpdateWalking();
                moving = state == State.Walking && agent.velocity.sqrMagnitude > 0.01f;
                break;

            case State.GoingToWork:
                UpdateGoingToWork();
                moving = state == State.GoingToWork && agent.velocity.sqrMagnitude > 0.01f;
                break;

            case State.Working:
                // The NpcWorker owns the Transform now; the agent is switched off.
                if (workSeconds > 0f)
                {
                    timer -= Time.deltaTime;
                    if (timer <= 0f)
                        BeginStandUp();
                }
                break;
        }

        SetAnimatorWalking(moving);
    }

    // ---- Going to work ---------------------------------------------------------------------------------------

    private int arrivals;
    private Vector3 workApproachPoint;

    private bool ShouldGoToWork()
    {
        return workstation != null && arrivals >= routePassesBeforeWork * waypoints.Length;
    }

    private bool TryGoToWork()
    {
        if (workstation.Seat == null)
        {
            UnityEngine.Debug.LogWarning(name + ": workstation has no Seat, so it keeps walking.", this);
            workstation = null;
            return false;
        }

        // Walk to open floor beside or behind the chair (never from the desk side), then slide into the seat.
        // The side/behind spots are tried first so she doesn't cut through the desk; the nearest floor is the fallback.
        Vector3 seatPos = workstation.SeatPosition;
        Transform seat = workstation.Seat;
        Vector3 flatForward = Vector3.ProjectOnPlane(seat.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(seat.right, Vector3.up).normalized;

        Vector3[] candidates =
        {
            seatPos + flatRight * 0.85f,
            seatPos - flatRight * 0.85f,
            seatPos - flatForward * 0.85f
        };

        NavMeshPath path = new NavMeshPath();
        bool found = false;
        NavMeshHit hit = default;

        foreach (Vector3 candidate in candidates)
        {
            if (NavMesh.SamplePosition(candidate, out hit, 0.45f, NavMesh.AllAreas)
                && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                found = true;
                break;
            }
        }

        if (!found && NavMesh.SamplePosition(seatPos, out hit, 2f, NavMesh.AllAreas)
            && agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            found = true;
        }

        if (!found || !agent.SetPath(path))
        {
            UnityEngine.Debug.LogWarning(name + ": cannot find a walkable way to the seat at " + seatPos.ToString("F2") + ". Move the seat marker closer to open floor or rebake the NavMesh. It keeps walking.", this);
            workstation = null;
            return false;
        }

        workApproachPoint = hit.position;
        state = State.GoingToWork;
        stuckTimer = 0f;
        return true;
    }

    private void UpdateGoingToWork()
    {
        if (agent.pathPending)
            return;

        bool arrived = !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.05f;

        stuckTimer = agent.velocity.sqrMagnitude < 0.01f ? stuckTimer + Time.deltaTime : 0f;
        if (!arrived && stuckTimer <= StuckSeconds * 2f)
            return;

        // Hand the Transform over to NpcWorker: switch the agent off, then sit.
        agent.ResetPath();
        agent.enabled = false;
        state = State.Working;
        timer = workSeconds;
        workstation.SitDown(null);
    }

    private void BeginStandUp()
    {
        state = State.StandingUp;
        workstation.StandUp(workApproachPoint, FinishStandUp);
    }

    private void FinishStandUp()
    {
        agent.enabled = true;
        agent.Warp(workApproachPoint);
        arrivals = 0;
        StartWaiting();
    }

    private void BeginNextWalk()
    {
        failureReason = "";

        for (int attempt = 0; attempt < waypoints.Length; attempt++)
        {
            int index = PickIndex();
            if (TryStartPath(waypoints[index]))
            {
                state = State.Walking;
                stuckTimer = 0f;
                return;
            }
        }

        if (!warnedNoRoute)
        {
            UnityEngine.Debug.LogWarning(name + " (at " + transform.position.ToString("F2") + ", area center " + areaCenter.ToString("F2") +
                                         "): no waypoint is reachable.", this);
            UnityEngine.Debug.LogWarning(name + " waypoint problems:" + failureReason, this);
            warnedNoRoute = true;
        }

        timer = Mathf.Max(1f, maxPause);
    }

    private void UpdateWalking()
    {
        if (agent.pathPending)
            return;

        bool arrived = !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.05f;
        if (arrived)
        {
            arrivals++;

            // After the route has been walked, go and sit down at the computer.
            if (ShouldGoToWork() && TryGoToWork())
                return;

            StartWaiting();
            return;
        }

        // Give up on a walk that has stopped making progress (blocked by another NPC, for example).
        stuckTimer = agent.velocity.sqrMagnitude < 0.01f ? stuckTimer + Time.deltaTime : 0f;
        if (stuckTimer > StuckSeconds)
        {
            agent.ResetPath();
            StartWaiting();
        }
    }

    private void StartWaiting()
    {
        state = State.Waiting;
        timer = Random.Range(minPause, Mathf.Max(minPause, maxPause));
    }

    private int PickIndex()
    {
        int count = waypoints.Length;
        int index = nextIndex;

        switch (routeMode)
        {
            case RouteMode.Sequential:
                nextIndex = (nextIndex + 1) % count;
                break;

            case RouteMode.PingPong:
                if (count > 1)
                {
                    if (nextIndex + pingPongStep < 0 || nextIndex + pingPongStep >= count)
                        pingPongStep = -pingPongStep;
                    nextIndex += pingPongStep;
                }
                break;

            case RouteMode.Random:
                nextIndex = count > 1 ? (index + Random.Range(1, count)) % count : 0;
                break;
        }

        return index;
    }

    // Why the last TryStartPath failed; shown in the warning so a bad setup can be diagnosed from the Console.
    private string failureReason = "";

    private bool Fail(string waypointName, string reason)
    {
        failureReason += "\n  " + waypointName + ": " + reason;
        return false;
    }

    private bool TryStartPath(Transform waypoint)
    {
        if (waypoint == null)
            return Fail("(empty slot)", "waypoint reference is missing");

        string label = waypoint.name + " at " + waypoint.position.ToString("F2");

        if (!NavMesh.SamplePosition(waypoint.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            return Fail(label, "no NavMesh within 1 m (move it onto the baked floor)");

        float distance = FlatDistance(hit.position, areaCenter);
        if (restrictToArea && distance > areaRadius)
            return Fail(label, "is " + distance.ToString("F1") + " m from where the NPC started, outside Area Radius " + areaRadius.ToString("F1"));

        NavMeshPath path = new NavMeshPath();
        bool calculated = agent.CalculatePath(hit.position, path);
        if (!calculated || path.status != NavMeshPathStatus.PathComplete)
            return Fail(label, "no complete path from the NPC at " + transform.position.ToString("F2") +
                               " (calculated=" + calculated + ", status=" + path.status + "). The NPC may stand on a separate NavMesh island");

        // A path that leaves the area on the way (through a door, say) is rejected too.
        if (restrictToArea)
        {
            foreach (Vector3 corner in path.corners)
            {
                if (FlatDistance(corner, areaCenter) > areaRadius + 0.75f)
                    return Fail(label, "the path detours outside the area (corner at " + corner.ToString("F2") + ")");
            }
        }

        if (!agent.SetPath(path))
            return Fail(label, "agent.SetPath failed");

        return true;
    }

    private void SetAnimatorWalking(bool walking)
    {
        if (!hasWalkingParameter || animator == null || walking == animatorWalking)
            return;

        animatorWalking = walking;
        animator.SetBool(walkingHash, walking);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? areaCenter : transform.position;

        if (restrictToArea)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(center, areaRadius);
        }

        if (waypoints == null)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null)
                continue;

            Gizmos.DrawWireSphere(waypoints[i].position, 0.15f);

            Transform next = waypoints[(i + 1) % waypoints.Length];
            if (next != null)
                Gizmos.DrawLine(waypoints[i].position, next.position);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A trail of arrows on the floor that leads the player to the objective (the same place the marker over the target points at). The trail follows the
// walkable path (the baked NavMesh) around walls and furniture, so it goes through the doors; where there is no NavMesh (the street outside) it is a straight line.
// The arrows glow in a wave that runs toward the target, the ones under the player's feet disappear, and the last stretch before the target is left clear.
// MissionMarker owns it: it shows the trail whenever the marker is tracking something and hides it when the marker hides.
public class FloorGuide : MonoBehaviour
{
    private const int MaxArrows = 40;
    private const float Spacing = 1.5f;        // distance between arrows along the path
    private const float EndGap = 1.1f;         // the clear stretch before the target
    private const float StartClear = 1.3f;     // arrows closer than this to the player (along the path) are not shown
    private const float Lift = 0.025f;         // how far above the floor the arrows lie
    private const float Refresh = 0.3f;        // how often the path is worked out again (seconds)

    private static Sprite arrowSprite;

    private readonly List<SpriteRenderer> arrows = new List<SpriteRenderer>();
    private readonly List<float> phases = new List<float>();
    private readonly List<Vector3> corners = new List<Vector3>();
    private NavMeshPath path;

    private Vector3 playerPoint;
    private Vector3 targetPoint;
    private float lastShown = -10f;
    private float nextRefresh;
    private int shownCount;

    // Called every frame by the marker while it tracks something.
    // "facing" (optional, flat) is the side of the target where it is used from: for a computer, the way its screen faces (toward the chair); the trail then
    // ends on that side, e.g. inside the teller area, not across the counter. Vector3.zero = any side.
    public void Show(Vector3 feetPosition, Vector3 target, Vector3 facing = default, Vector3[] via = null)
    {
        sideVia = via != null && via.Length == 3 ? via : null;
        playerPoint = feetPosition;
        targetPoint = target;
        facing.y = 0f;
        useSide = facing.sqrMagnitude > 0.01f;
        sideFacing = useSide ? facing.normalized : Vector3.zero;
        lastShown = Time.time;
    }

    private Vector3[] sideVia;   // optional: where the way in to that side goes (the door seen from outside, the door seen from inside, the place to end)
    private bool useSide;
    private Vector3 sideFacing;
    private Vector3 sideFailedTarget;
    private float sideFailedUntil = -1f;

    public void Hide()
    {
        lastShown = -10f;
        HideAll();
    }

    private void LateUpdate()
    {
        // The marker stops calling Show when its target is gone: the trail goes with it.
        if (Time.time - lastShown > 0.35f)
        {
            if (shownCount > 0)
                HideAll();

            return;
        }

        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + Refresh;
            Rebuild();
        }

        // The glow runs along the trail toward the target (arrow 0 is the one nearest the target).
        for (int i = 0; i < shownCount; i++)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f + i * 0.9f);
            Color color = new Color(1f, 0.84f, 0.25f, 0.35f + 0.65f * wave * wave);
            arrows[i].color = color;
        }
    }

    // Works out the path and puts the arrows on it.
    private void Rebuild()
    {
        float floorY = FloorHeight(playerPoint);
        Vector3 start = new Vector3(playerPoint.x, floorY, playerPoint.z);
        Vector3 goal = new Vector3(targetPoint.x, floorY, targetPoint.z);

        // No route that can be trusted: no arrows (better than arrows that lead into a wall).
        if (!BuildRoute(start, goal))
        {
            HideAll();
            return;
        }

        // Length of the trail.
        float total = 0f;
        for (int i = 1; i < corners.Count; i++)
            total += Flat(corners[i] - corners[i - 1]).magnitude;

        // Close to the target there is nothing to follow.
        if (total < EndGap + StartClear + 0.5f)
        {
            HideAll();
            return;
        }

        // Arrows are placed from the target backwards, so they stay where they are while the player walks over them.
        int count = 0;
        for (float fromEnd = EndGap; count < MaxArrows; fromEnd += Spacing)
        {
            float along = total - fromEnd;   // distance from the player
            if (along < StartClear)
                break;

            PointOnPath(along, out Vector3 position, out Vector3 direction);

            SpriteRenderer arrow = GetArrow(count);
            arrow.transform.position = new Vector3(position.x, Mathf.Clamp(position.y, floorY - 0.1f, floorY + 0.3f) + Lift, position.z);

            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            arrow.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            arrow.gameObject.SetActive(true);
            count++;
        }

        for (int i = count; i < arrows.Count; i++)
            arrows[i].gameObject.SetActive(false);

        shownCount = count;
    }

    // ---- The route ------------------------------------------------------------------------------------------------------------------------

    // A way through a door: a walkable spot on each side of it. The walkable surface (the NavMesh) was baked with the doors shut, so it has no way through them;
    // the guide knows the doors and goes through them itself.
    private class Door
    {
        public Vector3 a, b;
    }

    // A piece of route between two nodes: its length and its corners.
    private class Edge
    {
        public float length;
        public Vector3[] corners;
    }

    private List<Door> doors;                                            // found once, with the doors shut (null = not looked for yet)
    private readonly Dictionary<int, Edge> doorEdges = new Dictionary<int, Edge>();   // routes between door sides, worked out once (null = none)

    // Works out the route from start to goal and puts its corners in "corners". Returns false when there is no route to trust.
    private bool BuildRoute(Vector3 start, Vector3 goal)
    {
        corners.Clear();

        if (hasPocket && PocketRoute(start, goal))
            return true;

        corners.Clear();

        bool startOn = NavMesh.SamplePosition(start, out NavMeshHit from, 2.5f, NavMesh.AllAreas);
        bool goalOn = NavMesh.SamplePosition(goal, out NavMeshHit to, 4f, NavMesh.AllAreas);

        // Not on the walkable surface at all (the street outside): a straight line, there is nothing in the way to go round.
        if (!startOn || !goalOn)
        {
            corners.Add(start);
            corners.Add(goal);
            return true;
        }

        if (path == null)
            path = new NavMeshPath();

        // Where the trail should end: spots on the walkable floor round the target (the spot nearest to it, and rings of spots further out), because the
        // nearest spot may be on the wrong side of a wall (the back of a cubicle partition) or in a pocket that cannot be reached (someone at a desk).
        List<Vector3> goals = new List<Vector3> { to.position };
        float[] radii = { 0.6f, 1.1f, 1.7f, 2.4f, 3.0f };
        foreach (float radius in radii)
        {
            for (int k = 0; k < 8; k++)
            {
                float angle = k * 45f * Mathf.Deg2Rad;
                Vector3 around = goal + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (NavMesh.SamplePosition(around, out NavMeshHit hit, 0.6f, NavMesh.AllAreas) && Vector3.Distance(hit.position, to.position) > 0.25f)
                    goals.Add(hit.position);
            }
        }

        // The best place to end: reachable, with the target in sight (nothing solid between a person standing there and it), and as close to it as possible.
        // So for a computer in a cubicle the trail goes in through the cubicle's opening and ends at the desk, not at the back of the wall.
        Vector3[] best = null;
        float bestScore = float.MaxValue;
        Vector3 chosenGoal = Vector3.zero;

        // First only the spots on the side the target is used from (inside the teller area for a teller's computer, in the cubicle for a desk): walking there
        // directly, or, when a door is in the way (the walkable surface was baked with the doors shut), through the door. Only when that is not possible at
        // all, the spots on any side.
        for (int pass = useSide ? 0 : 1; pass < 2 && best == null; pass++)
        {
            foreach (Vector3 candidate in goals)
            {
                if (pass == 0 && Vector3.Dot(Flat(candidate - targetPoint), sideFacing) < 0.15f)
                    continue;

                if (!HasSight(candidate))
                    continue;

                if (!NavMesh.CalculatePath(from.position, candidate, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
                    continue;

                Vector3[] route = path.corners;
                float length = 0f;
                for (int i = 1; i < route.Length; i++)
                    length += Vector3.Distance(route[i - 1], route[i]);

                // Spots close enough to deal with the target (within ReachDistance of it) are all good: the one that is the shortest way for the player wins, so the
                // trail goes straight to the front of a counter or a desk and not round to the far side. Spots further away only count when there is nothing closer.
                float away = Flat(candidate - targetPoint).magnitude;
                float score = away <= ReachDistance ? length : 1000f + away * 10f + length * 0.1f;

                // The spot chosen last time is kept unless another is clearly better, so the trail does not jump about while the player walks.
                if (hasLastGoal && Vector3.Distance(candidate, lastGoal) < 0.3f)
                    score -= 1.0f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = route;
                    chosenGoal = candidate;
                }
            }

            // Nothing on the right side can be walked to directly: the way through the doors, to the spot on that side nearest to the target.
            bool sideFailedBefore = Time.time < sideFailedUntil && Vector3.Distance(sideFailedTarget, targetPoint) < 0.5f;
            if (pass == 0 && best == null && sideVia != null && ViaRoute(from.position, start))
                return true;

            if (pass == 0 && best == null && !sideFailedBefore)
            {
                if (doors == null)
                    FindDoors();

                if (doors.Count > 0)
                {
                    List<Vector3> sideSpots = new List<Vector3>();
                    foreach (Vector3 candidate in goals)
                    {
                        if (Vector3.Dot(Flat(candidate - targetPoint), sideFacing) >= 0.15f && HasSight(candidate))
                            sideSpots.Add(candidate);
                    }

                    sideSpots.Sort((x, y) => Flat(x - targetPoint).sqrMagnitude.CompareTo(Flat(y - targetPoint).sqrMagnitude));

                    for (int i = 0; i < sideSpots.Count && i < 4; i++)
                    {
                        if (DoorRoute(from.position, sideSpots[i], start))
                        {
                            lastGoal = sideSpots[i];
                            hasLastGoal = true;
                            return true;
                        }
                    }
                }

                // No luck: do not try again for this target for a while (and do not jump between the two kinds of trail).
                sideFailedTarget = targetPoint;
                sideFailedUntil = Time.time + 20f;
            }
        }

        if (best != null)
        {
            lastGoal = chosenGoal;
            hasLastGoal = true;
            corners.Add(start);
            corners.AddRange(best);
            return true;
        }

        // Nothing with the target in sight can be reached: the direct way to the nearest spot, when there is one.
        bool found = NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
        if (found && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
        {
            corners.Add(start);
            corners.AddRange(path.corners);
            return true;
        }

        // How far the direct attempt got, to fall back on.
        Vector3[] partial = found && path.corners.Length >= 2 ? path.corners : null;

        for (int i = 1; i < goals.Count; i++)   // (the first one was tried above)
        {
            if (NavMesh.CalculatePath(from.position, goals[i], NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
            {
                corners.Add(start);
                corners.AddRange(path.corners);
                return true;
            }
        }

        // Otherwise through doors (the walkable surface was baked with them shut).
        if (doors == null)
            FindDoors();

        if (doors.Count > 0 && Time.time >= nextDoorSearch)
        {
            bool routed = false;
            for (int i = 0; i < goals.Count && i < 4 && !routed; i++)
                routed = DoorRoute(from.position, goals[i], start);

            if (routed)
                return true;

            nextDoorSearch = Time.time + 1.5f;   // nothing found: do not search again at once
        }

        // No complete route: the part of the way that exists, or failing that a straight line. The trail never just vanishes.
        corners.Clear();
        corners.Add(start);

        if (partial != null)
            corners.AddRange(partial);
        else
            corners.Add(goal);

        return true;
    }

    // The way in to a side that the walkable surface does not reach (the teller area: its floor is not part of it, and its door was shut when it was made).
    // The mission gives three points: the door seen from outside, the door seen from inside, and the place to end. The trail walks to the outside of the door the
    // normal way, then goes through the door and straight on to the end. A player who is already inside goes straight to the end.
    private bool ViaRoute(Vector3 startNav, Vector3 start)
    {
        Vector3 outside = new Vector3(sideVia[0].x, start.y, sideVia[0].z);
        Vector3 inside = new Vector3(sideVia[1].x, start.y, sideVia[1].z);
        Vector3 end = new Vector3(sideVia[2].x, start.y, sideVia[2].z);

        if (path == null)
            path = new NavMeshPath();

        corners.Clear();

        // Already in the area: it is on the same piece of floor as the inside of the door.
        if (NavMesh.SamplePosition(inside, out NavMeshHit insideHit, 1.5f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(startNav, insideHit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
        {
            corners.Add(start);
            corners.AddRange(path.corners);
            corners.Add(end);
            return true;
        }

        // To the outside of the door.
        Vector3 outsideNav = NavMesh.SamplePosition(outside, out NavMeshHit outsideHit, 1.5f, NavMesh.AllAreas) ? outsideHit.position : outside;

        if (NavMesh.CalculatePath(startNav, outsideNav, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
        {
            corners.Add(start);
            corners.AddRange(path.corners);
        }
        else
        {
            if (doors == null)
                FindDoors();

            if (!(doors.Count > 0 && DoorRoute(startNav, outsideNav, start)))
            {
                corners.Clear();
                corners.Add(start);
                corners.Add(outside);
            }
        }

        corners.Add(outside);
        corners.Add(inside);
        corners.Add(end);
        return true;
    }

    // ---- Closed-off areas (the teller area behind the counters, the manager's room) -----------------------------------------------------

    // These are behind walls, their floor is not (all) part of the walkable surface, and the only way in is a door. A trail across them would run through the walls,
    // so the guide knows each area: its region, and its door seen from outside and from inside.
    private class Pocket
    {
        public Bounds region;
        public Vector3 outside, inside;
        public bool onlyWhenAsked;   // the trail goes in only when the target asks for that side (the teller area: customers are met at the counter, from the lobby)
    }

    private readonly List<Pocket> pockets = new List<Pocket>();
    private bool hasPocket => pockets.Count > 0;

    public void AddPocket(Bounds region, Vector3 outside, Vector3 inside, bool onlyWhenAsked)
    {
        pockets.Add(new Pocket { region = region, outside = outside, inside = inside, onlyWhenAsked = onlyWhenAsked });
    }

    private Pocket PocketOf(Vector3 p)
    {
        foreach (Pocket pocket in pockets)
        {
            if (p.x >= pocket.region.min.x && p.x <= pocket.region.max.x && p.z >= pocket.region.min.z && p.z <= pocket.region.max.z)
                return pocket;
        }

        return null;
    }

    // The trail when the start or the goal is in a closed-off area. Returns false when neither is.
    // Going out is always through the door. Going into the teller area is only when the target asked for that side (facing given): a customer at the counter is met from the lobby.
    private bool PocketRoute(Vector3 start, Vector3 goal)
    {
        Pocket from = PocketOf(start);
        Pocket into = PocketOf(goal);

        if (into != null && into != from && into.onlyWhenAsked && !useSide)
            into = null;

        if (from == null && into == null)
            return false;

        corners.Clear();

        // Both in the same area: nothing in the way that the walkable surface knows about, so straight on.
        if (from != null && from == into)
        {
            corners.Add(start);
            corners.Add(goal);
            return true;
        }

        // Inside, going out: to the door, through it, and on from there the normal way (into the next area, if the goal is in one).
        if (from != null)
        {
            Vector3 outside = new Vector3(from.outside.x, start.y, from.outside.z);
            Vector3 inside = new Vector3(from.inside.x, start.y, from.inside.z);

            if (!BuildRoute(outside, goal))
                return false;

            corners.Insert(0, inside);
            corners.Insert(0, start);
            return true;
        }

        // Outside, going in: the normal way to the door, through it, and straight on to the goal.
        Vector3[] kept = sideVia;
        sideVia = new[] { into.outside, into.inside, goal };
        Vector3 startNav = NavMesh.SamplePosition(start, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) ? hit.position : start;
        bool routed = ViaRoute(startNav, start);
        sideVia = kept;
        return routed;
    }

    private float nextDoorSearch;
    private Vector3 lastGoal;
    private bool hasLastGoal;
    private const float ReachDistance = 2.8f;   // how close to the target the end of the trail may be (the hand button reaches about 3 m)

    private readonly RaycastHit[] sightHits = new RaycastHit[24];

    // Can a person standing at this spot see the target? Only things that are tall (a wall, a cubicle partition, a closed door: 2.2 m or more) block the view;
    // furniture, a chair or the desk the computer stands on do not. So a spot on the other side of a partition is rejected, and a spot inside the cubicle,
    // even with the chair in between, is accepted.
    private bool HasSight(Vector3 standingSpot)
    {
        Vector3 eye = standingSpot + Vector3.up * 1.5f;
        Vector3 toTarget = targetPoint - eye;
        float distance = toTarget.magnitude;

        if (distance < 0.05f)
            return true;

        int count = Physics.RaycastNonAlloc(eye, toTarget / distance, sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = sightHits[i].collider;
            if (collider != null && collider.bounds.size.y >= 2.2f && sightHits[i].distance < distance - 0.3f)
                return false;
        }

        return true;
    }

    // The shortest way over the walkable pieces and the doors between them. False when there is none.
    private bool DoorRoute(Vector3 startNav, Vector3 goalNav, Vector3 start)
    {
        corners.Clear();

        int n = 2 + doors.Count * 2;   // node 0 = player, 1 = target, then the two sides of each door
        Vector3[] position = new Vector3[n];
        position[0] = startNav;
        position[1] = goalNav;
        for (int i = 0; i < doors.Count; i++)
        {
            position[2 + i * 2] = doors[i].a;
            position[3 + i * 2] = doors[i].b;
        }

        float[] distance = new float[n];
        int[] previous = new int[n];
        Edge[] viaEdge = new Edge[n];
        bool[] done = new bool[n];
        Dictionary<int, Edge> fresh = new Dictionary<int, Edge>();

        for (int i = 0; i < n; i++)
        {
            distance[i] = float.MaxValue;
            previous[i] = -1;
        }

        distance[0] = 0f;

        while (true)
        {
            int u = -1;
            for (int i = 0; i < n; i++)
            {
                if (!done[i] && distance[i] < float.MaxValue && (u < 0 || distance[i] < distance[u]))
                    u = i;
            }

            if (u < 0 || u == 1)
                break;

            done[u] = true;

            for (int v = 1; v < n; v++)
            {
                if (done[v] || v == u || (u == 0 && v == 1))
                    continue;

                Edge edge = GetEdge(u, v, position, fresh);
                if (edge == null || distance[u] + edge.length >= distance[v])
                    continue;

                distance[v] = distance[u] + edge.length;
                previous[v] = u;
                viaEdge[v] = edge;
            }
        }

        if (previous[1] < 0)
            return false;

        // The pieces from the target back to the player, then put in order.
        List<Edge> pieces = new List<Edge>();
        List<bool> reversed = new List<bool>();
        for (int node = 1; node != 0; node = previous[node])
        {
            pieces.Add(viaEdge[node]);
            reversed.Add(false);
        }

        corners.Add(start);
        for (int i = pieces.Count - 1; i >= 0; i--)
            corners.AddRange(pieces[i].corners);

        return corners.Count >= 2;
    }

    // The route between two nodes, going from node u to node v (null when there is none). Door sides of the same door are joined straight through the door.
    private Edge GetEdge(int u, int v, Vector3[] position, Dictionary<int, Edge> fresh)
    {
        // Through a door.
        if (u >= 2 && v >= 2 && (u - 2) / 2 == (v - 2) / 2)
            return new Edge { length = Vector3.Distance(position[u], position[v]), corners = new[] { position[u], position[v] } };

        bool betweenDoors = u >= 2 && v >= 2;
        int low = Mathf.Min(u, v), high = Mathf.Max(u, v);
        int key = low * 1000 + high;

        Dictionary<int, Edge> cache = betweenDoors ? doorEdges : fresh;
        Edge edge;

        if (!cache.TryGetValue(key, out edge))
        {
            // Worked out from the lower node to the higher one, and kept; used the other way round it is turned round below.
            edge = null;
            if (NavMesh.CalculatePath(position[low], position[high], NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
            {
                Vector3[] cornersCopy = path.corners;
                float length = 0f;
                for (int i = 1; i < cornersCopy.Length; i++)
                    length += Vector3.Distance(cornersCopy[i - 1], cornersCopy[i]);

                edge = new Edge { length = length, corners = cornersCopy };
            }

            cache[key] = edge;
        }

        if (edge == null)
            return null;

        if (u < v)
            return edge;

        Vector3[] turned = (Vector3[])edge.corners.Clone();
        System.Array.Reverse(turned);
        return new Edge { length = edge.length, corners = turned };
    }

    // Finds the doors (hinged and sliding), each with a walkable spot on both sides. Done once, when the doors are still shut: a door that is open later
    // turns its leaf, and its thin side (the way through) would be mistaken.
    private void FindDoors()
    {
        doors = new List<Door>();

        foreach (Interactable interactable in FindObjectsByType<Interactable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!(interactable is DoorInteractable) && !(interactable is SlidingDoorInteractable))
                continue;

            Renderer[] parts = interactable.GetComponentsInChildren<Renderer>();
            if (parts.Length == 0)
                continue;

            Bounds bounds = parts[0].bounds;
            foreach (Renderer part in parts)
                bounds.Encapsulate(part.bounds);

            // The door is thin across the wall: that is the way through it.
            Vector3 through = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
            Vector3 middle = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            if (!NavMesh.SamplePosition(middle + through * 1.0f, out NavMeshHit sideA, 1.2f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(middle - through * 1.0f, out NavMeshHit sideB, 1.2f, NavMesh.AllAreas))
                continue;

            // The same door can carry two components (the leaf and its hinge): count it once.
            bool duplicate = false;
            foreach (Door known in doors)
            {
                if (Vector3.Distance((known.a + known.b) * 0.5f, (sideA.position + sideB.position) * 0.5f) < 0.7f)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                doors.Add(new Door { a = sideA.position, b = sideB.position });
        }
    }

    // The point at "distance" along the trail from the player, and the direction the trail is going there (flat).
    private void PointOnPath(float distance, out Vector3 position, out Vector3 direction)
    {
        float walked = 0f;
        direction = Vector3.forward;
        position = corners[0];

        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 segment = corners[i] - corners[i - 1];
            float length = Flat(segment).magnitude;

            if (length > 0.001f)
                direction = Flat(segment).normalized;

            if (walked + length >= distance)
            {
                float t = length > 0.0001f ? (distance - walked) / length : 0f;
                position = Vector3.Lerp(corners[i - 1], corners[i], Mathf.Clamp01(t));
                return;
            }

            walked += length;
            position = corners[i];
        }
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    // The height of the floor under the player's feet: the walkable surface right there if there is one (a small radius, so a walkable surface high up,
    // such as the top of the ceiling, can never be picked), else what a ray straight down from just above the feet hits.
    private static float FloorHeight(Vector3 feet)
    {
        if (NavMesh.SamplePosition(feet, out NavMeshHit hit, 1.2f, NavMesh.AllAreas))
            return hit.position.y;

        if (Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out RaycastHit ray, 3f, ~0, QueryTriggerInteraction.Ignore))
            return ray.point.y;

        return feet.y;
    }

    private void HideAll()
    {
        foreach (SpriteRenderer arrow in arrows)
            arrow.gameObject.SetActive(false);

        shownCount = 0;
    }

    private SpriteRenderer GetArrow(int index)
    {
        while (arrows.Count <= index)
        {
            GameObject go = new GameObject("FloorArrow");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 1.1f;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ArrowSprite();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 5;

            arrows.Add(renderer);
            phases.Add(0f);
        }

        return arrows[index];
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer arrow in arrows)
        {
            if (arrow != null)
                Destroy(arrow.gameObject);
        }
    }

    // ---- The arrow picture: a gold double chevron with a dark edge, pointing up (its up is the way the trail goes on the floor) -------------------

    private static Sprite ArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        // Two chevrons, one behind the other: each is two thick lines from the tip down to the sides.
        Vector2[][] lines =
        {
            new[] { new Vector2(64f, 118f), new Vector2(18f, 70f) },
            new[] { new Vector2(64f, 118f), new Vector2(110f, 70f) },
            new[] { new Vector2(64f, 76f), new Vector2(18f, 28f) },
            new[] { new Vector2(64f, 76f), new Vector2(110f, 28f) }
        };

        Color gold = new Color(1f, 0.84f, 0.25f, 1f);
        Color edge = new Color(0.04f, 0.1f, 0.2f, 1f);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = float.MaxValue;
                foreach (Vector2[] line in lines)
                    d = Mathf.Min(d, SegmentDistance(p, line[0], line[1]));

                float fill = Mathf.Clamp01(0.5f - (d - 9f));
                float outline = Mathf.Clamp01(0.5f - (d - 13f));

                // Gold over the dark edge.
                Color c = Color.Lerp(edge, gold, fill);
                c.a = Mathf.Max(outline, fill);
                pixels[y * size + x] = c;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        arrowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);   // 1 metre wide at scale 1
        return arrowSprite;
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
}

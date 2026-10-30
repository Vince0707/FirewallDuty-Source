using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > NPC > Set Up Cubicle Workers
// Stage 4: the cubicle employees.
//  - mesh_office_f_intern01 walks her route, sits at the computer for a minute, then walks again (NpcWalker + NpcWorker).
//  - mesh_office_m_chara01 is seated and working from the start (NpcWorker only).
// Tools > Firewall Duty > NPC > Set Up Printer Worker
//  - mesh_office_m_intern01 stands at the nearest printer and works there (NpcWorker only).
// Each NPC gets the nearest free chair / printer. Markers live under NPC_Seats and are never moved by a re-run,
// so nudge them in the Scene view if a character stands or sits a little off.
public static class NpcCubicleSetupTool
{
    private const string WalkingWorker = "mesh_office_f_intern01";
    private const string SeatRootName = "NPC_Seats";
    private const float MaxChairDistance = 10f;
    private const float MaxComputerDistance = 3f;
    private const string PrinterWorker = "mesh_office_m_intern01";
    private const float WalkerWorkSeconds = 60f;
    private const float PrinterStandGap = 0.5f;
    private const string Receptionist = "mesh_office_f_recepcionist01";
    internal const string MeetingListener = "mesh_office_m_accountant01";
    // The Head Manager, who sits behind the manager desk in the Head Office (the other boss is the meeting-room vice president).
    private const string HeadBoss = "mesh_office_m_boss01";
    // The two IT experts: "rival01" is the male model (the player uses the same model but is excluded), "it01" the female.
    private static readonly string[] ItExperts = { "mesh_office_m_rival01", "mesh_office_f_it01" };

    private static readonly string[] CubicleNpcs =
    {
        "mesh_office_f_intern01",
        "mesh_office_m_chara01"
    };

    internal struct Pairing
    {
        public Transform npc;
        public Transform chair;
        public float distance;
    }

    [MenuItem("Tools/Firewall Duty/NPC/Set Up Cubicle Workers")]
    public static void SetUp()
    {
        SetUpSeated(CubicleNpcs);
    }

    [MenuItem("Tools/Firewall Duty/NPC/Set Up Head Manager")]
    public static void SetUpHeadBoss()
    {
        SetUpSeated(new[] { HeadBoss });
    }

    [MenuItem("Tools/Firewall Duty/NPC/Set Up IT Experts")]
    public static void SetUpItExperts()
    {
        SetUpSeated(ItExperts);
    }

    [MenuItem("Tools/Firewall Duty/NPC/Set Up Receptionist")]
    public static void SetUpReceptionist()
    {
        RaiseReceptionChair();
        SetUpSeated(new[] { Receptionist });
    }

    // The reception counter is higher than a normal desk, so her chair is stretched upward from its base (like a
    // raised gas lift: the wheels stay on the floor). The scale is set, not multiplied, so re-running changes nothing.
    // To change the height, edit ReceptionChairHeightScale; to undo, set the chair's Scale Y back to 1.
    private const float ReceptionChairHeightScale = 1.35f;

    private static void RaiseReceptionChair()
    {
        List<Transform> found = FindNpcs(new[] { Receptionist }, new StringBuilder());
        if (found.Count == 0)
            return;

        Transform chair = Nearest(FindByName(IsChair), found[0].position, MaxChairDistance);
        if (chair == null)
        {
            UnityEngine.Debug.LogWarning("Receptionist: no chair found to raise.");
            return;
        }

        Undo.RecordObject(chair, "Raise Reception Chair");
        Vector3 scale = chair.localScale;
        scale.y = ReceptionChairHeightScale;
        chair.localScale = scale;
        EditorUtility.SetDirty(chair);

        UnityEngine.Debug.Log("Raised '" + chair.name + "' for the receptionist: its height is now x" + ReceptionChairHeightScale + ".");
    }

    // Seats each named NPC at its nearest free chair, facing its desk or computer, and fits it to the chair.
    internal static void SetUpSeated(string[] names)
    {
        StringBuilder report = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        NpcSetupTool.CleanUpWaypointRoot(report);

        if (NpcSetupTool.EnsureSurface(scene, false, report) == null)
        {
            UnityEngine.Debug.LogError(report.ToString());
            return;
        }

        AnimatorController controller = NpcSetupTool.EnsureLocomotionController(report);
        if (controller == null)
        {
            UnityEngine.Debug.LogError(report.ToString());
            return;
        }

        List<Transform> npcs = FindNpcs(names, report);
        if (npcs.Count == 0)
        {
            UnityEngine.Debug.LogError(report.ToString());
            return;
        }

        List<Transform> chairs = FindByName(IsChair);
        List<Transform> computers = FindByName(IsComputer);
        report.AppendLine("Found " + chairs.Count + " chair(s) and " + computers.Count + " computer(s) in the scene.");

        foreach (Pairing pairing in AssignChairs(npcs, chairs))
            report.AppendLine("[" + pairing.npc.name + "] " + ConfigureWorker(pairing, computers, controller));

        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S), press Play, then check each character sits correctly at the chair and faces the computer. " +
                          "If not, move that NPC's marker under " + SeatRootName + " in the Scene view.");
        UnityEngine.Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Firewall Duty/NPC/Set Up Printer Worker")]
    public static void SetUpPrinterWorker()
    {
        StringBuilder report = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        NpcSetupTool.CleanUpWaypointRoot(report);

        AnimatorController controller = NpcSetupTool.EnsureLocomotionController(report);
        if (controller == null)
        {
            UnityEngine.Debug.LogError(report.ToString());
            return;
        }

        Transform npc = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name == PrinterWorker && NpcSetupTool.IsNpc(t))
            {
                npc = t;
                break;
            }
        }

        if (npc == null)
        {
            UnityEngine.Debug.LogError("Could not find '" + PrinterWorker + "' in the open scene.");
            return;
        }

        // The real printer machine: a printer, but not the 3D printer and not a small desktop printer.
        Transform printer = Nearest(FindByName(lower => lower.Contains("printer") && !lower.Contains("3d") && !lower.Contains("desk")),
                                    npc.position, MaxChairDistance);
        if (printer == null)
        {
            UnityEngine.Debug.LogError(npc.name + ": no floor-standing printer found within " + MaxChairDistance + " m.");
            return;
        }

        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        GameObject rootObject = GameObject.Find(SeatRootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(SeatRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Seat Root");
        }

        // The old chair marker from the first cubicle setup is no longer used.
        Transform oldSeat = rootObject.transform.Find(npc.name + "_Seat");
        if (oldSeat != null)
        {
            Undo.DestroyObjectImmediate(oldSeat.gameObject);
            notes.Add("removed his old chair marker");
        }

        // The marker name records which printer it was made for, so choosing a different printer replaces it,
        // while a marker you nudged by hand is kept as long as the printer stays the same.
        string spotName = npc.name + "_PrinterSpot (" + printer.name + ")";
        for (int i = rootObject.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = rootObject.transform.GetChild(i);
            if (child.name.StartsWith(npc.name + "_PrinterSpot") && child.name != spotName)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                notes.Add("removed marker for the previously chosen machine");
            }
        }

        Transform spot = rootObject.transform.Find(spotName);
        if (spot == null)
        {
            // Stand on the printer's front: the printer's own axis that points most toward where he is now.
            Bounds bounds = GetBounds(printer);
            Vector3 toNpc = npc.position - bounds.center;
            toNpc.y = 0f;

            Vector3 front = Vector3.forward;
            float best = float.NegativeInfinity;
            foreach (Vector3 axis in new[] { printer.forward, -printer.forward, printer.right, -printer.right })
            {
                Vector3 flat = new Vector3(axis.x, 0f, axis.z).normalized;
                float dot = Vector3.Dot(flat, toNpc.normalized);
                if (dot > best)
                {
                    best = dot;
                    front = flat;
                }
            }

            float halfDepth = Mathf.Abs(front.x) * bounds.extents.x + Mathf.Abs(front.z) * bounds.extents.z;
            Vector3 standAt = new Vector3(bounds.center.x, npc.position.y, bounds.center.z) + front * (halfDepth + PrinterStandGap);

            GameObject marker = new GameObject(spotName);
            Undo.RegisterCreatedObjectUndo(marker, "Create Printer Spot");
            marker.transform.SetParent(rootObject.transform, false);
            marker.transform.position = standAt;
            marker.transform.rotation = Quaternion.LookRotation(-front);
            spot = marker.transform;
            notes.Add("printer machine '" + printer.name + "' " + printer.position.ToString("F2") + ", standing " + PrinterStandGap + " m in front of it");
        }
        else
        {
            notes.Add("kept existing printer spot (not moved)");
        }

        NpcWorker worker = npc.GetComponent<NpcWorker>();
        if (worker == null)
        {
            worker = Undo.AddComponent<NpcWorker>(npc.gameObject);
            notes.Add("added NpcWorker");
        }

        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = spot;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("workingParameter").stringValue = NpcSetupTool.StandWorkingParameter;
        so.FindProperty("workingStateName").stringValue = NpcSetupTool.StandWorkingState;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine("[" + npc.name + "] " + string.Join("; ", notes));
        report.AppendLine("NOTE: the project has no printer-using animation, so he plays a standing look-around idle at the printer.");
        report.AppendLine("Save the scene (Ctrl+S) and press Play.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // ---- Finding things --------------------------------------------------------------------------------------

    private static List<Transform> FindNpcs(string[] names, StringBuilder report)
    {
        List<Transform> found = new List<Transform>();

        foreach (string wanted in names)
        {
            Transform match = null;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name == wanted && NpcSetupTool.IsNpc(t))
                {
                    match = t;
                    break;
                }
            }

            if (match != null)
                found.Add(match);
            else
                report.AppendLine("WARNING: could not find cubicle NPC '" + wanted + "' in the open scene.");
        }

        return found;
    }

    private static bool IsChair(string lower)
    {
        return lower.Contains("chair") && !lower.Contains("lounge");
    }

    internal static bool IsComputer(string lower)
    {
        // "(Prb)PC (4)" and "mesh_pc_01" are computers; "(Prb)PCCase" and "pCube233" are not.
        bool isPc = (lower.StartsWith("(prb)pc") || lower.StartsWith("mesh_pc") || lower == "pc") && !lower.Contains("case");

        return isPc
               || lower.Contains("laptop") || lower.Contains("monitor")
               || lower.Contains("screen") || lower.Contains("computer");
    }

    // Objects whose name matches and that have visible geometry; nested duplicates of the same prop are skipped.
    internal static List<Transform> FindByName(System.Func<string, bool> matches)
    {
        List<Transform> result = new List<Transform>();

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!matches(t.name.ToLowerInvariant()) || t.GetComponentInChildren<Renderer>() == null)
                continue;

            if (t.parent != null && matches(t.parent.name.ToLowerInvariant()))
                continue;

            result.Add(t);
        }

        return result;
    }

    // Closest-first matching so every NPC gets a different chair.
    private static List<Pairing> AssignChairs(List<Transform> npcs, List<Transform> chairs)
    {
        List<Pairing> all = new List<Pairing>();
        foreach (Transform npc in npcs)
        {
            foreach (Transform chair in chairs)
            {
                float d = FlatDistance(npc.position, chair.position);
                if (d <= MaxChairDistance)
                    all.Add(new Pairing { npc = npc, chair = chair, distance = d });
            }
        }

        all.Sort((a, b) => a.distance.CompareTo(b.distance));

        List<Pairing> chosen = new List<Pairing>();
        HashSet<Transform> usedNpcs = new HashSet<Transform>();
        HashSet<Transform> usedChairs = new HashSet<Transform>();

        foreach (Pairing p in all)
        {
            if (usedNpcs.Contains(p.npc) || usedChairs.Contains(p.chair))
                continue;

            chosen.Add(p);
            usedNpcs.Add(p.npc);
            usedChairs.Add(p.chair);
        }

        foreach (Transform npc in npcs)
        {
            if (!usedNpcs.Contains(npc))
                UnityEngine.Debug.LogWarning(npc.name + ": no free chair within " + MaxChairDistance + " m, so it was not set up.");
        }

        return chosen;
    }

    // ---- Per-NPC setup ---------------------------------------------------------------------------------------

    internal static string ConfigureWorker(Pairing pairing, List<Transform> computers, AnimatorController controller)
    {
        Transform npc = pairing.npc;
        bool walks = npc.name == WalkingWorker;
        List<string> notes = new List<string>();

        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        // The walker gets a seat fitted to her chair; the NPCs that already look right keep their existing marker.
        Transform seat = EnsureSeat(npc, pairing.chair, computers, walks, out Vector3 hipTarget, notes);

        bool workerIsNew = npc.GetComponent<NpcWorker>() == null;
        NpcWorker worker = npc.GetComponent<NpcWorker>();
        if (worker == null)
        {
            worker = Undo.AddComponent<NpcWorker>(npc.gameObject);
            notes.Add("added NpcWorker");
        }

        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = seat;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = !walks;

        // Every seated worker is fitted to its chair so nobody floats or sinks into the cushion.
        ApplyFitSettings(so, workerIsNew, hipTarget, false, false, notes);

        if (npc.name == Receptionist || npc.name == MeetingListener || npc.name == HeadBoss || System.Array.IndexOf(ItExperts, npc.name) >= 0 || System.Array.IndexOf(CubicleNpcs, npc.name) >= 0)
            EnsureHandRests(so, seat, pairing.chair, computers, notes);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        if (walks)
        {
            // Walk the route (waypoints are created by the walking setup), then sit down at the seat.
            notes.Add(NpcSetupTool.ConfigureWalker(npc, controller));

            NpcWalker walker = npc.GetComponent<NpcWalker>();
            if (walker != null)
            {
                SerializedObject walkerSo = new SerializedObject(walker);
                walkerSo.FindProperty("workstation").objectReferenceValue = worker;
                walkerSo.FindProperty("routePassesBeforeWork").intValue = 1;

                // Only fill in the default; a value you typed into the Inspector is kept.
                SerializedProperty workSeconds = walkerSo.FindProperty("workSeconds");
                if (workSeconds.floatValue <= 0f)
                    workSeconds.floatValue = WalkerWorkSeconds;

                walkerSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(walker);
                notes.Add("walks the route once, sits and works for " + workSeconds.floatValue + " s, then walks again");
            }
            else
            {
                notes.Add("WARNING: walking setup failed, so she will only sit");
                SerializedObject fix = new SerializedObject(worker);
                fix.FindProperty("startSeated").boolValue = true;
                fix.ApplyModifiedProperties();
            }
        }
        else
        {
            notes.Add("sits and works from the start");
        }

        return string.Join("; ", notes);
    }

    // Applies the chair-fit settings WITHOUT overwriting what you tuned by hand in the Inspector:
    //  - a brand-new worker gets the defaults;
    //  - a worker made by an older version of the tool keeps its current values (they may be your fix);
    //  - after that, the hip target is only refreshed while it still equals the value the tool wrote last time.
    internal static void ApplyFitSettings(SerializedObject so, bool workerIsNew, Vector3 computedHip,
                                          bool keepFeetAboveFloor, bool straightenLegs, List<string> notes)
    {
        SerializedProperty configured = so.FindProperty("toolConfigured");
        SerializedProperty hip = so.FindProperty("hipTarget");
        SerializedProperty autoHip = so.FindProperty("autoHipTarget");

        if (workerIsNew)
        {
            so.FindProperty("fitToSeat").boolValue = true;
            so.FindProperty("keepFeetAboveFloor").boolValue = keepFeetAboveFloor;
            so.FindProperty("straightenLegs").boolValue = straightenLegs;
            hip.vector3Value = computedHip;
            autoHip.vector3Value = computedHip;
            configured.boolValue = true;
            return;
        }

        if (!configured.boolValue)
        {
            autoHip.vector3Value = hip.vector3Value;
            configured.boolValue = true;
            notes.Add("kept your current Npc Worker settings");
            return;
        }

        bool tunedByHand = (hip.vector3Value - autoHip.vector3Value).sqrMagnitude > 1e-6f;
        if (tunedByHand)
        {
            notes.Add("kept your hand-tuned Hip Target");
            return;
        }

        hip.vector3Value = computedHip;
        autoHip.vector3Value = computedHip;
    }

    // Two small markers on the desk surface, in front of the seat, where the hands rest. They are children of the
    // seat marker (so they follow it) and are never moved by a re-run.
    internal static void EnsureHandRests(SerializedObject workerSo, Transform seat, Transform chair, List<Transform> computers, List<string> notes)
    {
        Transform left = seat.Find(seat.name + "_LeftHand");
        Transform right = seat.Find(seat.name + "_RightHand");
        bool created = left == null || right == null;

        if (created)
        {
            // The desk surface is where the computer stands; without a computer, assume a 1 m high desk.
            Bounds chairBounds = GetBounds(chair);
            Vector3 from = chairBounds.size.sqrMagnitude > 0f ? chairBounds.center : chair.position;
            Transform computer = Nearest(computers, from, MaxComputerDistance);

            float deskTop = computer != null ? GetBounds(computer).min.y : seat.position.y + 1f;
            float reachForward = computer != null
                ? Mathf.Clamp(Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(computer.position.x, 0f, computer.position.z)) - 0.30f, 0.30f, 0.55f)
                : 0.45f;

            // No computer (a meeting table): rest the hands on the table top, just past its near edge.
            Transform table = null;
            if (computer == null)
            {
                table = Nearest(FindByName(IsDesk), from, 3.5f);
                if (table != null)
                {
                    Bounds tableBounds = GetBounds(table);
                    deskTop = tableBounds.max.y;

                    Vector3 edge = tableBounds.ClosestPoint(from);
                    float toEdge = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(edge.x, 0f, edge.z));
                    reachForward = Mathf.Clamp(toEdge + 0.15f, 0.30f, 0.55f);
                }
            }

            Vector3 flatSeat = new Vector3(seat.position.x, deskTop + 0.02f, seat.position.z);
            float localY = seat.InverseTransformPoint(flatSeat).y;

            left = left != null ? left : CreateHandMarker(seat, seat.name + "_LeftHand", new Vector3(-0.2f, localY, reachForward));
            right = right != null ? right : CreateHandMarker(seat, seat.name + "_RightHand", new Vector3(0.2f, localY, reachForward));
            notes.Add("hands rest on the desk at height " + (deskTop + 0.02f).ToString("F2") + (computer != null ? " (computer '" + computer.name + "')" : table != null ? " (table '" + table.name + "')" : " (assumed)"));

            workerSo.FindProperty("restHandsOnDesk").boolValue = true;
        }
        else
        {
            notes.Add("kept existing hand markers (not moved)");
        }

        workerSo.FindProperty("leftHandRest").objectReferenceValue = left;
        workerSo.FindProperty("rightHandRest").objectReferenceValue = right;
    }

    private static Transform CreateHandMarker(Transform seat, string markerName, Vector3 localPosition)
    {
        GameObject marker = new GameObject(markerName);
        Undo.RegisterCreatedObjectUndo(marker, "Create Hand Marker");
        marker.transform.SetParent(seat, false);
        marker.transform.localPosition = localPosition;
        return marker.transform;
    }

    internal static bool IsDesk(string lower)
    {
        string[] notDesks = { "light", "plant", "printer", "holder", "organizer", "lamp", "pen", "pc", "stapler", "drawer" };
        if (!(lower.Contains("desk") || lower.Contains("table")))
            return false;

        // The desk prefabs themselves are "(Prb)Desk1", "(Prb)Desk2"; accessories on them are not desks.
        if (lower.StartsWith("(prb)desk1") || lower.StartsWith("(prb)desk2"))
            return true;

        foreach (string word in notDesks)
        {
            if (lower.Contains(word))
                return false;
        }

        return true;
    }

    internal static Bounds GetBounds(Transform t)
    {
        Bounds bounds = new Bounds(t.position, Vector3.zero);
        bool any = false;

        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return bounds;
    }

    private static readonly Vector3 DefaultHipTarget = new Vector3(0f, 0.55f, 0f);

    internal static Transform EnsureSeat(Transform npc, Transform chair, List<Transform> computers, bool fitted,
                                         out Vector3 hipTarget, List<string> notes, bool soft = false)
    {
        hipTarget = DefaultHipTarget;

        GameObject rootObject = GameObject.Find(SeatRootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(SeatRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Seat Root");
        }

        string legacyName = npc.name + "_Seat";
        string seatName = fitted ? npc.name + "_Seat (fitted)" : legacyName;

        Transform existing = rootObject.transform.Find(seatName);
        if (existing != null)
        {
            notes.Add("kept existing seat marker (not moved)");
            hipTarget = ComputeHipTarget(chair, existing, notes, soft);
            return existing;
        }

        // The first version of the walker's marker was only a guess; replace it with a fitted one.
        if (fitted)
        {
            Transform legacy = rootObject.transform.Find(legacyName);
            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy.gameObject);
                notes.Add("replaced her old seat marker");
            }
        }

        // Face the furniture the chair belongs to: the nearest desk, then the nearest computer, then the chair's forward.
        Bounds chairBounds = GetBounds(chair);
        Vector3 chairCenter = chairBounds.size.sqrMagnitude > 0f ? chairBounds.center : chair.position;

        Transform target = Nearest(FindByName(IsDesk), chairCenter, 3.5f);
        string targetKind = "desk";
        if (target == null)
        {
            target = Nearest(computers, chairCenter, MaxComputerDistance);
            targetKind = "computer";
        }

        Vector3 facing = chair.forward;
        string facingName = "the chair's own forward";

        if (target != null)
        {
            // Face the nearest point of the furniture (a long table is faced head-on, not at its middle).
            Bounds targetBounds = GetBounds(target);
            Vector3 toTarget = targetBounds.ClosestPoint(chairCenter) - chairCenter;
            if (toTarget.sqrMagnitude < 0.01f)
                toTarget = targetBounds.center - chairCenter;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
            {
                facing = toTarget;
                facingName = targetKind + " '" + target.name + "'";
            }
        }

        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            facing = Vector3.forward;
        facing.Normalize();

        // The marker sits at the middle of the chair (its bounds), not at the chair's pivot, which may be off-centre.
        GameObject seat = new GameObject(seatName);
        Undo.RegisterCreatedObjectUndo(seat, "Create Seat Marker");
        seat.transform.SetParent(rootObject.transform, false);
        seat.transform.position = new Vector3(chairCenter.x, npc.position.y, chairCenter.z);
        seat.transform.rotation = Quaternion.LookRotation(facing);

        hipTarget = ComputeHipTarget(chair, seat.transform, notes, soft);

        notes.Add("seat at '" + chair.name + "' " + chairCenter.ToString("F2") + ", facing " + facingName);
        return seat.transform;
    }

    // How far the hip joint sits above the cushion when someone is seated.
    private const float HipHeightAboveSeat = 0.14f;

    // Hip height above the floor for someone sunk into a beanbag.
    private const float SoftSeatHipHeight = 0.30f;

    // Where the hips should end up, in the marker's local space: over the middle of the chair, just above the
    // measured seat surface. Recomputed on every run, so it always follows the chair.
    internal static Vector3 ComputeHipTarget(Transform chair, Transform marker, List<string> notes, bool soft = false)
    {
        Bounds bounds = GetBounds(chair);
        Vector3 center = bounds.size.sqrMagnitude > 0f ? bounds.center : chair.position;

        float seatTop = FindSeatTop(chair, center, bounds.min.y, bounds.size.y, soft, out bool measured);

        Vector3 facing = Vector3.ProjectOnPlane(marker.forward, Vector3.up).normalized;
        // A beanbag is soft: a person sinks into it, so the hips sit well BELOW its highest point, at a real beanbag
        // seat height above the floor (never above the measured top). Hard chairs sit just above the cushion.
        float hipY = soft
            ? Mathf.Min(seatTop - 0.10f, bounds.min.y + SoftSeatHipHeight)
            : seatTop + HipHeightAboveSeat;
        Vector3 hipWorld = new Vector3(center.x, hipY, center.z) - facing * 0.04f;

        notes.Add("hips fitted to y=" + hipWorld.y.ToString("F2") + " (seat surface " + seatTop.ToString("F2") +
                  (measured ? " measured from the chair mesh" : " estimated from the chair's height") + ")");

        return marker.InverseTransformPoint(hipWorld);
    }

    // The seat surface is the highest part of the chair's mesh in a small disc around its middle, below
    // backrest height. (The backrest sits behind the disc, the gas-lift column is lower than the cushion.)
    private static float FindSeatTop(Transform chair, Vector3 center, float floorY, float height, bool soft, out bool measured)
    {
        // A chair built by hand (the bank's tall teller chairs) says where its seat is with a child marker named "SeatTop".
        Transform seatMarker = chair.Find("SeatTop");
        if (seatMarker != null)
        {
            measured = true;
            return seatMarker.position.y;
        }

        const float discRadius = 0.18f;
        // A beanbag is low and domed: look from just above the floor up to well over its top.
        float low = floorY + (soft ? 0.05f : 0.25f);
        float high = floorY + (soft ? 1.20f : 0.70f);
        float top = float.NegativeInfinity;

        foreach (MeshFilter filter in chair.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;

            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                if (world.y < low || world.y > high)
                    continue;

                float dx = world.x - center.x;
                float dz = world.z - center.z;
                if (dx * dx + dz * dz <= discRadius * discRadius && world.y > top)
                    top = world.y;
            }
        }

        measured = !float.IsNegativeInfinity(top);
        return measured ? top : floorY + Mathf.Clamp(height * 0.47f, 0.40f, 0.55f);
    }

    internal static Transform Nearest(List<Transform> candidates, Vector3 from, float maxDistance)
    {
        Transform best = null;
        float bestDistance = maxDistance;

        foreach (Transform t in candidates)
        {
            float d = FlatDistance(from, t.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = t;
            }
        }

        return best;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }
}

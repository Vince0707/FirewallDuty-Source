using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > NPC > Set Up Meeting Room
//  - mesh_office_m_accountant01 sits on "(Prb)OfficeChair (8)" facing the table, hands on the table top,
//    looking at the boss and nodding now and then (NpcWorker + NpcHeadBehaviour in Nod mode).
//  - mesh_office_f_boss01 stands at the east end of the table, facing along it toward him, looking at him and making
//    small speaking head movements (NpcWorker for the spot + NpcHeadBehaviour in Talk mode).
// Markers live under NPC_Seats and are never moved by a re-run.
public static class NpcMeetingSetupTool
{
    private const string Boss = "mesh_office_f_boss01";
    private const string Listener = NpcCubicleSetupTool.MeetingListener;
    private const string ListenerChair = "(Prb)OfficeChair (8)";
    private const string Table = "(Prb)ConferenceTable";
    private const string SeatRootName = "NPC_Seats";
    private const float BossGapFromTableEnd = 0.6f;

    [MenuItem("Tools/Firewall Duty/NPC/Set Up Meeting Room")]
    public static void SetUp()
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

        Transform boss = FindNpc(Boss);
        Transform listener = FindNpc(Listener);
        Transform chair = FindObject(ListenerChair);
        Transform table = FindObject(Table);

        if (boss == null) report.AppendLine("WARNING: could not find '" + Boss + "'.");
        if (listener == null) report.AppendLine("WARNING: could not find '" + Listener + "'.");
        if (chair == null) report.AppendLine("WARNING: could not find the chair '" + ListenerChair + "'.");
        if (table == null) report.AppendLine("WARNING: could not find the table '" + Table + "'.");

        if (listener != null && chair != null)
            report.AppendLine("[" + listener.name + "] " + ConfigureListener(listener, chair, boss, controller));

        if (boss != null && table != null)
            report.AppendLine("[" + boss.name + "] " + ConfigureBoss(boss, listener, chair, table, controller));

        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine();
        report.AppendLine("NOTE: there is no talking, gesturing or nodding clip in the project. The boss plays a calm standing idle with " +
                          "head movements and hand gestures added in code (IK); the listener uses the seated clip with a nod added in code.");
        report.AppendLine("Save the scene (Ctrl+S) and press Play.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // Tools > Firewall Duty > NPC > Move Vice President Beside TV   (run it with Company_scene open)
    // The Vice President (mesh_office_f_boss01) gave her talk standing at the east end of the conference table. She now stands beside the TV on the west
    // wall of the meeting room (on the side nearer the listener), facing the listener. Only her standing spot is changed: she is still the speaker, the
    // listener (the accountant on his chair) still looks at her - he follows her wherever she stands - and nods, and she still looks at him and gestures.
    // It does nothing a second time. Ctrl+Z takes it back.
    [MenuItem("Tools/Firewall Duty/NPC/Move Vice President Beside TV")]
    public static void MoveBesideTv()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Move Vice President", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        Transform seatRoot = GetSeatRoot();
        Transform spot = seatRoot.Find(Boss + "_TableHeadSpot");
        Transform boss = FindNpc(Boss);

        Transform tv = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("TV LED"))
            {
                tv = t;
                break;
            }
        }

        if (spot == null || tv == null)
        {
            UnityEngine.Debug.LogError("Move Vice President: " + (spot == null ? "her standing spot '" + Boss + "_TableHeadSpot' (run NPC > Set Up Meeting Room first)" : "the TV") + " was not found, so nothing was changed.");
            return;
        }

        Renderer[] parts = tv.GetComponentsInChildren<Renderer>();
        Bounds tvBounds = parts.Length > 0 ? parts[0].bounds : new Bounds(tv.position, Vector3.one * 0.5f);
        foreach (Renderer part in parts)
            tvBounds.Encapsulate(part.bounds);

        // Beside the TV on its north side (the side the listener's chair is turned toward), a step out from the wall.
        Vector3 standAt = new Vector3(tvBounds.max.x + 0.85f, spot.position.y, tvBounds.max.z + 0.7f);

        if ((standAt - spot.position).sqrMagnitude < 0.0025f)
        {
            UnityEngine.Debug.Log("Move Vice President: she already stands beside the TV, so nothing was changed.");
            return;
        }

        // She faces the listener (his seat marker is where he sits; without it, the middle of the table).
        Transform listenerSeat = seatRoot.Find(Listener + "_Seat");
        Transform table = FindObject(Table);
        Vector3 lookAt = listenerSeat != null ? listenerSeat.position : table != null ? NpcCubicleSetupTool.GetBounds(table).center : standAt + Vector3.right;
        Vector3 facing = lookAt - standAt;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            facing = Vector3.right;

        Quaternion rotation = Quaternion.LookRotation(facing.normalized);

        Undo.RecordObject(spot, "Move Vice President");
        spot.SetPositionAndRotation(standAt, rotation);
        EditorUtility.SetDirty(spot);

        // Her body in the editor view follows (at run time her worker puts her on the spot anyway).
        if (boss != null)
        {
            Undo.RecordObject(boss, "Move Vice President");
            boss.SetPositionAndRotation(standAt, rotation);
            EditorUtility.SetDirty(boss);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Move Vice President: she now stands beside the TV at " + standAt.ToString("F2") + ", facing the listener. The listener still looks at her and nods. " +
                              "Save the scene (Ctrl+S). Use Edit > Undo to take it back.");
    }

    // ---- The listener (seated, nodding) ----------------------------------------------------------------------

    private static string ConfigureListener(Transform npc, Transform chair, Transform boss, AnimatorController controller)
    {
        // Same seating as the other workers: nearest-table facing, hips fitted to the chair, hands on the table top.
        NpcCubicleSetupTool.Pairing pairing = new NpcCubicleSetupTool.Pairing { npc = npc, chair = chair, distance = 0f };
        string result = NpcCubicleSetupTool.ConfigureWorker(pairing, new List<Transform>(), controller);

        // A meeting is not typing: use the relaxed seated clip instead of the computer-work clip.
        NpcWorker worker = npc.GetComponent<NpcWorker>();
        if (worker != null)
        {
            SerializedObject so = new SerializedObject(worker);
            so.FindProperty("workingParameter").stringValue = NpcSetupTool.LoungeParameter;
            so.FindProperty("workingStateName").stringValue = NpcSetupTool.LoungeState;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(worker);
        }

        List<string> notes = new List<string>();
        NpcHeadBehaviour head = GetOrAdd<NpcHeadBehaviour>(npc, notes);
        SerializedObject headSo = new SerializedObject(head);
        headSo.FindProperty("mode").enumValueIndex = (int)NpcHeadBehaviour.Mode.Nod;
        headSo.FindProperty("lookAtTarget").objectReferenceValue = boss;
        headSo.FindProperty("lookAtHeight").floatValue = 1.5f;   // the boss is standing
        headSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(head);

        notes.Add("sits on '" + chair.name + "', looks at the boss and nods");
        return result + "; " + string.Join("; ", notes);
    }

    // ---- The boss (standing at the head of the table, talking) -----------------------------------------------

    private static string ConfigureBoss(Transform npc, Transform listener, Transform listenerChair, Transform table, AnimatorController controller)
    {
        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        Transform seatRoot = GetSeatRoot();

        // Stand just past the end of the table that she is nearest to, in line with the table's long axis.
        Bounds bounds = NpcCubicleSetupTool.GetBounds(table);
        Vector3 fromCenter = npc.position - bounds.center;
        fromCenter.y = 0f;

        bool alongX = Mathf.Abs(fromCenter.x) >= Mathf.Abs(fromCenter.z);
        Vector3 endDirection = alongX ? new Vector3(Mathf.Sign(fromCenter.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(fromCenter.z));
        float halfLength = alongX ? bounds.extents.x : bounds.extents.z;

        Vector3 standAt = new Vector3(bounds.center.x, npc.position.y, bounds.center.z) + endDirection * (halfLength + BossGapFromTableEnd);

        string spotName = npc.name + "_TableHeadSpot";
        Transform spot = seatRoot.Find(spotName);
        if (spot == null)
        {
            // Face back along the table, toward the other character if there is one.
            // The listener will be sitting on his chair by then, not standing where he is placed in the editor.
            Vector3 lookAt = listenerChair != null ? listenerChair.position : listener != null ? listener.position : bounds.center;
            Vector3 facing = lookAt - standAt;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
                facing = -endDirection;

            GameObject marker = new GameObject(spotName);
            Undo.RegisterCreatedObjectUndo(marker, "Create Table Head Spot");
            marker.transform.SetParent(seatRoot, false);
            marker.transform.position = standAt;
            marker.transform.rotation = Quaternion.LookRotation(facing.normalized);
            spot = marker.transform;
            notes.Add("stands at the " + (alongX ? (endDirection.x > 0 ? "east" : "west") : (endDirection.z > 0 ? "north" : "south")) +
                      " end of the table " + standAt.ToString("F2") + ", facing the other character");
        }
        else
        {
            notes.Add("kept existing table-head spot (not moved)");
        }

        NpcWorker worker = GetOrAdd<NpcWorker>(npc, notes);
        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = spot;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("fitToSeat").boolValue = false;
        so.FindProperty("workingParameter").stringValue = NpcSetupTool.StandIdleParameter;
        so.FindProperty("workingStateName").stringValue = NpcSetupTool.StandIdleState;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        NpcHeadBehaviour head = GetOrAdd<NpcHeadBehaviour>(npc, notes);
        SerializedObject headSo = new SerializedObject(head);
        headSo.FindProperty("mode").enumValueIndex = (int)NpcHeadBehaviour.Mode.Talk;
        headSo.FindProperty("lookAtTarget").objectReferenceValue = listener;
        headSo.FindProperty("lookAtHeight").floatValue = 1.15f;   // the listener is seated
        headSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(head);

        notes.Add("looks at the listener and makes small speaking head movements");

        // Hands: open palms, pointing, counting off, a wide spread... moved with IK, picked at random.
        GetOrAdd<NpcGestures>(npc, notes);
        notes.Add("explains with hand gestures");
        return string.Join("; ", notes);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------

    private static Transform GetSeatRoot()
    {
        GameObject rootObject = GameObject.Find(SeatRootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(SeatRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Seat Root");
        }

        return rootObject.transform;
    }

    private static T GetOrAdd<T>(Transform npc, List<string> notes) where T : Component
    {
        T component = npc.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(npc.gameObject);
            notes.Add("added " + typeof(T).Name);
        }

        return component;
    }

    private static Transform FindNpc(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name == name && NpcSetupTool.IsNpc(t))
                return t;
        }

        return null;
    }

    private static Transform FindObject(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }
}

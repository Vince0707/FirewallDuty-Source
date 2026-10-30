using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > NPC > Set Up Kitchen NPCs
//  - mesh_office_m_secretary01 stands at the cafe table, picks up a cup, sips (hand IK, no coffee clip exists),
//    puts it back, rests, and repeats (NpcWorker for the spot + NpcCoffeeDrinker for the arm).
//  - mesh_office_m_suspenders01 sits relaxed on the beanbag "(Prb)Fatboy (3)" (NpcWorker, fitted to the beanbag).
// Markers live under NPC_Seats and are never moved by a re-run.
public static class NpcKitchenSetupTool
{
    private const string CoffeeDrinker = "mesh_office_m_secretary01";
    private const string Lounger = "mesh_office_m_suspenders01";
    private const string Beanbag = "(Prb)Fatboy (3)";
    private const string SeatRootName = "NPC_Seats";
    private const float StandDistanceFromCup = 0.55f;

    [MenuItem("Tools/Firewall Duty/NPC/Set Up Kitchen NPCs")]
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

        Transform drinker = FindNpc(CoffeeDrinker);
        Transform lounger = FindNpc(Lounger);
        Transform beanbag = FindObject(Beanbag);

        if (drinker == null) report.AppendLine("WARNING: could not find '" + CoffeeDrinker + "'.");
        if (lounger == null) report.AppendLine("WARNING: could not find '" + Lounger + "'.");
        if (beanbag == null) report.AppendLine("WARNING: could not find the beanbag '" + Beanbag + "'.");

        GameObject rootObject = GameObject.Find(SeatRootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(SeatRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Seat Root");
        }

        if (drinker != null)
            report.AppendLine("[" + drinker.name + "] " + ConfigureDrinker(drinker, controller, rootObject.transform));

        if (lounger != null && beanbag != null)
            report.AppendLine("[" + lounger.name + "] " + ConfigureLounger(lounger, beanbag, controller));

        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine();
        report.AppendLine("NOTE: the project has no coffee-drinking or lounging clip. The drinker plays a standing idle with her/his arm moved by IK; " +
                          "the beanbag sitter uses the casual sitting clip.");
        report.AppendLine("Save the scene (Ctrl+S) and press Play.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // ---- The coffee drinker ----------------------------------------------------------------------------------

    private static string ConfigureDrinker(Transform npc, AnimatorController controller, Transform seatRoot)
    {
        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        // The cup on the table nearest to this NPC.
        Transform cup = NpcCubicleSetupTool.Nearest(NpcCubicleSetupTool.FindByName(IsTableCup), npc.position, 8f);
        if (cup == null)
            return string.Join("; ", notes) + "; SKIPPED: no cup found on a cafe table (looked for 'Cafe_Cup_*').";

        notes.Add("cup '" + cup.name + "' " + cup.position.ToString("F2"));

        // A static (batched) cup cannot move, so it could never be lifted.
        foreach (Transform t in cup.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0)
            {
                Undo.RecordObject(t.gameObject, "Clear Cup Static Flags");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                notes.Add("cleared Static on the cup so it can move");
                break;
            }
        }

        // Stand at the cup, on the side he is already on, facing it.
        string spotName = npc.name + "_CoffeeSpot (" + cup.name + ")";
        RemoveOtherSpots(seatRoot, npc.name + "_CoffeeSpot", spotName, notes);

        Transform spot = seatRoot.Find(spotName);
        if (spot == null)
        {
            Vector3 away = npc.position - cup.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;

            GameObject marker = new GameObject(spotName);
            Undo.RegisterCreatedObjectUndo(marker, "Create Coffee Spot");
            marker.transform.SetParent(seatRoot, false);
            marker.transform.position = new Vector3(cup.position.x, npc.position.y, cup.position.z) + away * StandDistanceFromCup;
            marker.transform.rotation = Quaternion.LookRotation(-away);
            spot = marker.transform;
            notes.Add("standing spot " + StandDistanceFromCup + " m from the cup");
        }
        else
        {
            notes.Add("kept existing coffee spot (not moved)");
        }

        NpcWorker worker = GetOrAdd<NpcWorker>(npc, notes);
        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = spot;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("fitToSeat").boolValue = false;
        so.FindProperty("workingParameter").stringValue = NpcSetupTool.StandWorkingParameter;
        so.FindProperty("workingStateName").stringValue = NpcSetupTool.StandWorkingState;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        NpcCoffeeDrinker coffee = GetOrAdd<NpcCoffeeDrinker>(npc, notes);
        SerializedObject coffeeSo = new SerializedObject(coffee);
        coffeeSo.FindProperty("cup").objectReferenceValue = cup;

        // Earlier versions put the sip point at the wrong height or inside the face. Move it to mouth level,
        // but only if the value is still one of the old defaults, so a value tuned by hand is kept.
        SerializedProperty mouth = coffeeSo.FindProperty("mouthOffset");
        Vector3 oldMouth = mouth.vector3Value;
        bool oldDefault = (oldMouth - new Vector3(0f, 0f, 0.13f)).sqrMagnitude < 1e-6f
                          || (oldMouth - new Vector3(0f, -0.07f, 0.13f)).sqrMagnitude < 1e-6f
                          || (oldMouth - new Vector3(0f, 0f, 0.2f)).sqrMagnitude < 1e-6f;
        if (oldDefault)
        {
            // The head bone is near the middle of the head, so the mouth is BELOW it (negative Y).
            mouth.vector3Value = new Vector3(0f, -0.10f, 0.17f);
            notes.Add("moved the sip position down to mouth level");
        }

        coffeeSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(coffee);

        notes.Add("stands at the table and drinks from the cup");
        return string.Join("; ", notes);
    }

    // ---- The beanbag sitter ----------------------------------------------------------------------------------

    private static string ConfigureLounger(Transform npc, Transform beanbag, AnimatorController controller)
    {
        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        // Marker at the beanbag, facing the nearest low table; hips fitted to the beanbag's measured top.
        Transform seat = NpcCubicleSetupTool.EnsureSeat(npc, beanbag, new List<Transform>(), false,
                                                        out Vector3 hipTarget, notes, true);

        bool workerIsNew = npc.GetComponent<NpcWorker>() == null;
        NpcWorker worker = GetOrAdd<NpcWorker>(npc, notes);
        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = seat;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("workingParameter").stringValue = NpcSetupTool.LoungeParameter;
        so.FindProperty("workingStateName").stringValue = NpcSetupTool.LoungeState;

        // First run: fit to the beanbag, feet on the floor, legs out. Re-runs keep whatever you tuned by hand
        // (Hip Target, Leg Extension, Straighten Legs, ...).
        NpcCubicleSetupTool.ApplyFitSettings(so, workerIsNew, hipTarget, true, true, notes);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        notes.Add("sits relaxed on '" + beanbag.name + "'");
        return string.Join("; ", notes);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------

    // "Cafe_Cup_4" and "Cafe_Cup_5" are the cups on the table; the to-go cups and cup stand are by the coffee machine.
    private static bool IsTableCup(string lower)
    {
        return lower.StartsWith("cafe_cup_") && !lower.Contains("stand") && !lower.Contains("to_go");
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

    // A marker is named after the cup it was made for; picking a different cup replaces it, while a marker you
    // nudged by hand is kept as long as the cup stays the same.
    private static void RemoveOtherSpots(Transform seatRoot, string prefix, string keepName, List<string> notes)
    {
        for (int i = seatRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = seatRoot.GetChild(i);
            if (child.name.StartsWith(prefix) && child.name != keepName)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                notes.Add("removed marker for a previously chosen cup");
            }
        }
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

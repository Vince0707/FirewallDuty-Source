using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > NPC > ...
// Stage 2: bakes a NavMesh for the office and makes the selected NPC(s) walk between waypoints.
// With nothing selected it targets the cubicle intern (mesh_office_f_intern01). Safe to run repeatedly.
public static class NpcSetupTool
{
    private const string DefaultWalker = "mesh_office_f_intern01";
    private const string ControllerPath = "Assets/Animations/NPC/NPC_Locomotion.controller";
    private const string WalkingParameter = "IsWalking";
    private const string WorkingParameter = "IsWorking";
    internal const string StandWorkingParameter = "IsStandingWork";
    internal const string StandWorkingState = "StandWork";
    internal const string LoungeParameter = "IsLounging";
    internal const string LoungeState = "Lounge";
    internal const string StandIdleParameter = "IsStandingIdle";
    internal const string StandIdleState = "StandIdle";
    private const string WaypointRootName = "NPC_Waypoints";

    [MenuItem("Tools/Firewall Duty/NPC/Set Up NPC Walking (Selected Or Intern)")]
    public static void SetUpWalking()
    {
        StringBuilder report = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        CleanUpWaypointRoot(report);

        NavMeshSurface surface = EnsureSurface(scene, false, report);
        if (surface == null)
            return;

        AnimatorController controller = EnsureLocomotionController(report);
        if (controller == null)
        {
            UnityEngine.Debug.LogError(report.ToString());
            return;
        }

        List<Transform> npcs = FindTargets();
        if (npcs.Count == 0)
        {
            report.AppendLine("No NPC found. Select an NPC in the Hierarchy (or open Company_scene, which contains " + DefaultWalker + ").");
            UnityEngine.Debug.LogWarning(report.ToString());
            return;
        }

        foreach (Transform npc in npcs)
            report.AppendLine("[" + npc.name + "] " + ConfigureWalker(npc, controller));

        EditorSceneManager.MarkSceneDirty(scene);
        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S), then press Play and watch the NPC.");
        UnityEngine.Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Firewall Duty/NPC/Rebake NavMesh")]
    public static void Rebake()
    {
        StringBuilder report = new StringBuilder();
        EnsureSurface(SceneManager.GetActiveScene(), true, report);
        UnityEngine.Debug.Log(report.ToString());
    }

    // ---- NavMesh ---------------------------------------------------------------------------------------------

    internal static NavMeshSurface EnsureSurface(Scene scene, bool forceBake, StringBuilder report)
    {
        NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
        bool created = false;

        if (surface == null)
        {
            GameObject go = new GameObject("NavMeshSurface");
            Undo.RegisterCreatedObjectUndo(go, "Create NavMeshSurface");
            SceneManager.MoveGameObjectToScene(go, scene);
            surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            created = true;
            report.AppendLine("Created NavMeshSurface.");
        }

        // Bake from what is VISIBLE (render meshes), not from colliders: props such as the whiteboard may have no
        // collider at all, and the NavMesh must still keep NPCs out of them. The characters are excluded below.
        bool geometryChanged = surface.useGeometry != NavMeshCollectGeometry.RenderMeshes;
        if (geometryChanged)
        {
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            report.AppendLine("NavMeshSurface now bakes from render meshes (was colliders), so every visible object blocks NPCs.");
        }

        if (forceBake || created || geometryChanged || surface.navMeshData == null)
        {
            int excluded = ExcludeCharactersFromBake();
            if (excluded > 0)
                report.AppendLine("Excluded " + excluded + " character(s) from the NavMesh bake so they don't leave holes.");

            string oldPath = surface.navMeshData != null ? AssetDatabase.GetAssetPath(surface.navMeshData) : "";

            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                report.AppendLine("NavMesh bake produced no data. Does the scene have a floor?");
                return null;
            }

            // Save the baked data as an asset so it survives scene reloads and builds (replacing the previous bake).
            if (!AssetDatabase.Contains(surface.navMeshData))
            {
                string path = string.IsNullOrEmpty(oldPath) ? "Assets/Scenes/" + scene.name + "_NavMesh.asset" : oldPath;
                if (!string.IsNullOrEmpty(oldPath))
                    AssetDatabase.DeleteAsset(oldPath);

                AssetDatabase.CreateAsset(surface.navMeshData, AssetDatabase.GenerateUniqueAssetPath(path));
                AssetDatabase.SaveAssets();
            }

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("Baked NavMesh -> " + AssetDatabase.GetAssetPath(surface.navMeshData));
        }
        else
        {
            report.AppendLine("Using existing baked NavMesh (use 'Rebake NavMesh' after moving furniture or walls).");
        }

        return surface;
    }

    // Character meshes must not become obstacles in the NavMesh (they move). Mark every NPC and the player.
    private static int ExcludeCharactersFromBake()
    {
        int count = 0;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool isPlayer = t.GetComponent<PlayerMobileController>() != null;
            if (!isPlayer && !IsNpc(t))
                continue;

            NavMeshModifier modifier = t.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = Undo.AddComponent<NavMeshModifier>(t.gameObject);

            if (!modifier.ignoreFromBuild || !modifier.applyToChildren)
            {
                Undo.RecordObject(modifier, "Exclude Character From NavMesh");
                modifier.ignoreFromBuild = true;
                modifier.applyToChildren = true;
                EditorUtility.SetDirty(modifier);
                count++;
            }
        }

        return count;
    }
    // ---- Animation -------------------------------------------------------------------------------------------

    // States: Idle, Walk ('IsWalking') and Work ('IsWorking'). Uses the Humanoid clips already in the project.
    // Creates the controller, or adds whatever an older version of it is missing.
    internal static AnimatorController EnsureLocomotionController(StringBuilder report)
    {
        AnimationClip idle = FindClip("Idle");
        AnimationClip walk = FindClip("Adult_Walk");
        AnimationClip work = FindClip("Adult_ComputerWork");
        // No printer-using clip exists in the project; a standing look-around idle is the stand-in for standing work.
        AnimationClip standWork = FindClip("Idle_LookAround") ?? idle;
        // Seated pose for the beanbag. There is no dedicated "lounging" clip. A normal seated clip (knees bent, feet down)
        // fits a beanbag whose top is about seat height; the talking clip has straight legs and left them floating in the air.
        AnimationClip lounge = FindClip("Adult_Slot_Sitting") ?? FindClip("Adult_ComputerWork") ?? idle;

        if (idle == null || walk == null || work == null)
        {
            report.AppendLine("Could not find the 'Idle', 'Adult_Walk' and 'Adult_ComputerWork' clips in Assets/ithappy/Casino_Free/Animations.");
            return null;
        }

        foreach (AnimationClip clip in new[] { idle, walk, work, standWork, lounge })
        {
            if (!clip.humanMotion)
            {
                report.AppendLine("Clip '" + clip.name + "' is not a Humanoid clip, so it cannot drive these Humanoid characters.");
                return null;
            }
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Directory.GetCurrentDirectory(), ControllerPath)));
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            report.AppendLine("Created " + ControllerPath + ".");
        }

        EnsureParameter(controller, WalkingParameter);
        EnsureParameter(controller, WorkingParameter);
        EnsureParameter(controller, StandWorkingParameter);
        EnsureParameter(controller, LoungeParameter);
        EnsureParameter(controller, StandIdleParameter);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = EnsureState(machine, "Idle", idle);
        AnimatorState walkState = EnsureState(machine, "Walk", walk);
        AnimatorState workState = EnsureState(machine, "Work", work);
        AnimatorState standWorkState = EnsureState(machine, StandWorkingState, standWork);
        AnimatorState loungeState = EnsureState(machine, LoungeState, lounge);
        // A calm standing idle (no head wandering), for someone who is standing and talking.
        AnimatorState standIdleState = EnsureState(machine, StandIdleState, idle);
        machine.defaultState = idleState;

        EnsureTransition(idleState, walkState, WalkingParameter, true);
        EnsureTransition(walkState, idleState, WalkingParameter, false);
        EnsureTransition(idleState, workState, WorkingParameter, true);
        EnsureTransition(walkState, workState, WorkingParameter, true);
        EnsureTransition(workState, idleState, WorkingParameter, false);
        EnsureTransition(idleState, standWorkState, StandWorkingParameter, true);
        EnsureTransition(walkState, standWorkState, StandWorkingParameter, true);
        EnsureTransition(standWorkState, idleState, StandWorkingParameter, false);
        EnsureTransition(idleState, loungeState, LoungeParameter, true);
        EnsureTransition(walkState, loungeState, LoungeParameter, true);
        EnsureTransition(loungeState, idleState, LoungeParameter, false);
        EnsureTransition(idleState, standIdleState, StandIdleParameter, true);
        EnsureTransition(walkState, standIdleState, StandIdleParameter, true);
        EnsureTransition(standIdleState, idleState, StandIdleParameter, false);

        // The IK pass lets NpcCoffeeDrinker move a hand to the cup on top of whatever clip is playing.
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void EnsureParameter(AnimatorController controller, string parameter)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == parameter)
                return;
        }

        controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
    }

    private static AnimatorState EnsureState(AnimatorStateMachine machine, string stateName, AnimationClip clip)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == stateName)
            {
                child.state.motion = clip;
                return child.state;
            }
        }

        AnimatorState state = machine.AddState(stateName);
        state.motion = clip;
        return state;
    }

    private static void EnsureTransition(AnimatorState from, AnimatorState to, string parameter, bool whenTrue)
    {
        foreach (AnimatorStateTransition existing in from.transitions)
        {
            if (existing.destinationState == to)
                return;
        }

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.25f;
        transition.AddCondition(whenTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    // Makes sure the NPC has an Animator with the locomotion controller and the model's Humanoid Avatar.
    internal static Animator ConfigureAnimator(Transform npc, AnimatorController controller, List<string> notes)
    {
        Animator animator = npc.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(npc.gameObject);
            notes.Add("added Animator");
        }

        Undo.RecordObject(animator, "Set NPC Animator");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        if (animator.avatar == null)
        {
            Avatar avatar = FindAvatar(npc);
            if (avatar != null)
            {
                animator.avatar = avatar;
                notes.Add("assigned Avatar '" + avatar.name + "'");
            }
            else
            {
                notes.Add("WARNING: could not find a Humanoid Avatar for this model, so the Humanoid clips cannot play");
            }
        }

        return animator;
    }

    private static AnimationClip FindClip(string clipName)
    {
        foreach (string guid in AssetDatabase.FindAssets(clipName + " t:AnimationClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Casino_Free/Animations") && Path.GetFileNameWithoutExtension(path) == clipName)
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        return null;
    }

    // ---- Per-NPC setup ---------------------------------------------------------------------------------------

    // A real NPC is the ROOT of a character model: named mesh_office_*, with a skinned mesh somewhere below it.
    // The FBX also has a child mesh object with the same name; that child must never be treated as the NPC,
    // and neither must the player or a waypoint helper.
    internal static bool IsNpc(Transform t)
    {
        return t != null
               && t.name.StartsWith("mesh_office_")
               && (t.parent == null || !t.parent.name.StartsWith("mesh_office_"))
               && t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
               && t.GetComponentInParent<PlayerMobileController>() == null
               && !IsUnderWaypointRoot(t);
    }

    private static bool IsUnderWaypointRoot(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.name == WaypointRootName)
                return true;
        }

        return false;
    }

    private static List<Transform> FindTargets()
    {
        List<Transform> result = new List<Transform>();

        foreach (Transform t in Selection.transforms)
        {
            // Accept a selected child (e.g. a bone) by climbing to the NPC root.
            Transform root = t;
            while (root != null && !IsNpc(root))
                root = root.parent;

            if (root != null && !result.Contains(root))
                result.Add(root);
        }

        if (result.Count == 0)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t.name == DefaultWalker && IsNpc(t))
                {
                    result.Add(t);
                    break;
                }
            }
        }

        return result;
    }

    // Earlier versions of this tool sometimes added NPC components to a character's child mesh object (same name as
    // the character) instead of the root. Remove those strays. Only components added as overrides are removed, so
    // anything that is part of the model asset itself is never touched.
    internal static int CleanUpStrayNpcComponents()
    {
        int removed = 0;

        foreach (Transform root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsNpc(root))
                continue;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root)
                    continue;

                // Order matters: NpcWalker needs the NavMeshAgent, so it goes first.
                removed += RemoveAdded<NpcWorker>(child);
                removed += RemoveAdded<NpcWalker>(child);
                removed += RemoveAdded<NavMeshAgent>(child);
                removed += RemoveAdded<Animator>(child);
            }
        }

        return removed;
    }

    private static int RemoveAdded<T>(Transform t) where T : Component
    {
        int count = 0;

        foreach (T component in t.GetComponents<T>())
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(component) || PrefabUtility.IsAddedComponentOverride(component))
            {
                Undo.DestroyObjectImmediate(component);
                count++;
            }
        }

        return count;
    }

    // Earlier versions of this tool could configure a waypoint group by mistake. Strip anything that is not a
    // plain waypoint from NPC_Waypoints, then delete groups that no NpcWalker uses any more.
    internal static void CleanUpWaypointRoot(StringBuilder report)
    {
        int strays = CleanUpStrayNpcComponents();
        if (strays > 0)
            report.AppendLine("Removed " + strays + " NPC component(s) that an earlier run had put on a character's child mesh instead of its root.");

        GameObject rootObject = GameObject.Find(WaypointRootName);
        if (rootObject == null)
            return;

        int removed = 0;

        foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
        {
            NpcWalker walker = t.GetComponent<NpcWalker>();
            if (walker != null) { Undo.DestroyObjectImmediate(walker); removed++; }

            NavMeshAgent agent = t.GetComponent<NavMeshAgent>();
            if (agent != null) { Undo.DestroyObjectImmediate(agent); removed++; }

            Animator animator = t.GetComponent<Animator>();
            if (animator != null) { Undo.DestroyObjectImmediate(animator); removed++; }
        }

        HashSet<Transform> used = new HashSet<Transform>();
        foreach (NpcWalker walker in Object.FindObjectsByType<NpcWalker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedProperty list = new SerializedObject(walker).FindProperty("waypoints");
            for (int i = 0; i < list.arraySize; i++)
            {
                Transform wp = list.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (wp != null && wp.parent != null)
                    used.Add(wp.parent);
            }
        }

        for (int i = rootObject.transform.childCount - 1; i >= 0; i--)
        {
            Transform group = rootObject.transform.GetChild(i);
            if (!used.Contains(group))
            {
                Undo.DestroyObjectImmediate(group.gameObject);
                removed++;
            }
        }

        if (removed > 0)
            report.AppendLine("Cleaned up " + removed + " stray object(s)/component(s) left under " + WaypointRootName + " by an earlier run.");
    }

    // The model's Avatar lives inside the FBX; the Humanoid clips only play if the Animator uses it.
    private static Avatar FindAvatar(Transform npc)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(npc.gameObject);
        if (source == null)
            return null;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(source)))
        {
            if (asset is Avatar avatar && avatar.isValid && avatar.isHuman)
                return avatar;
        }

        return null;
    }

    internal static string ConfigureWalker(Transform npc, AnimatorController controller)
    {
        List<string> notes = new List<string>();
        Vector3 start = npc.position;

        if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 1f, NavMesh.AllAreas))
            return "SKIPPED: not standing on the NavMesh (inside furniture, or the bake missed the floor). Move it slightly and run again.";

        Animator animator = ConfigureAnimator(npc, controller, notes);

        NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = Undo.AddComponent<NavMeshAgent>(npc.gameObject);
            agent.radius = 0.3f;
            agent.height = 1.8f;
            notes.Add("added NavMeshAgent");
        }

        NpcWalker walker = npc.GetComponent<NpcWalker>();
        if (walker == null)
        {
            walker = Undo.AddComponent<NpcWalker>(npc.gameObject);
            notes.Add("added NpcWalker");
        }

        SerializedObject so = new SerializedObject(walker);
        SerializedProperty waypointProperty = so.FindProperty("waypoints");

        // Waypoints from older versions of this tool were only checked against the NPC, not against each other
        // (one leg could cross a whiteboard), so they are replaced once. Waypoints of the current version are kept,
        // so positions you moved by hand survive.
        if (waypointProperty.arraySize > 0 && !WaypointsAreCurrent(waypointProperty))
        {
            DestroyWaypointGroups(waypointProperty);
            waypointProperty.arraySize = 0;
            notes.Add("replaced old waypoints");
        }

        if (waypointProperty.arraySize == 0)
        {
            List<Transform> waypoints = CreateWaypoints(npc, startHit.position);
            if (waypoints.Count < 2)
            {
                so.ApplyModifiedProperties();
                return string.Join("; ", notes) + "; only " + waypoints.Count + " direct waypoint(s) found nearby. Add waypoints by hand (see Inspector) or clear furniture around the NPC.";
            }

            waypointProperty.arraySize = waypoints.Count;
            for (int i = 0; i < waypoints.Count; i++)
                waypointProperty.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];

            notes.Add("created " + waypoints.Count + " waypoints under " + WaypointRootName + "/" + npc.name);
        }

        // Per-NPC variation so walkers never look identical (stable per name).
        System.Random rng = new System.Random(npc.name.GetHashCode());
        so.FindProperty("moveSpeed").floatValue = 1f + (float)rng.NextDouble() * 0.4f;
        so.FindProperty("minPause").floatValue = 3f + (float)rng.NextDouble() * 3f;
        so.FindProperty("maxPause").floatValue = 8f + (float)rng.NextDouble() * 6f;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(walker);
        return notes.Count > 0 ? string.Join("; ", notes) : "already configured";
    }

    // Looks for a small loop (3 spots, or 2) around the NPC where EVERY leg is a short, direct walk on the NavMesh:
    // from the NPC to each spot, and between the spots, including the leg that closes the loop. A leg that needs a
    // detour means something (a whiteboard, a desk) sits in the way, so that combination is rejected.
    private static List<Transform> CreateWaypoints(Transform npc, Vector3 start)
    {
        const float directness = 1.25f;   // walked distance may be at most this times the straight-line distance
        const float minSpacing = 1.5f;

        // Candidate spots on rings around the NPC that can be reached directly from where it stands.
        List<Vector3> spots = new List<Vector3>();
        NavMeshPath path = new NavMeshPath();

        foreach (float radius in new[] { 2.5f, 2f, 1.5f })
        {
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 wanted = start + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
                    continue;

                if (IsDirectWalk(start, hit.position, directness, path))
                    spots.Add(hit.position);
            }
        }

        int n = spots.Count;
        bool[,] direct = new bool[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                bool ok = Vector3.Distance(spots[i], spots[j]) >= minSpacing && IsDirectWalk(spots[i], spots[j], directness, path);
                direct[i, j] = direct[j, i] = ok;
            }
        }

        // Prefer a closed loop of three; otherwise a pair to walk back and forth between.
        int[] best = null;
        float bestPerimeter = 0f;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (!direct[i, j])
                    continue;

                for (int k = j + 1; k < n; k++)
                {
                    if (!direct[j, k] || !direct[i, k])
                        continue;

                    float perimeter = Vector3.Distance(spots[i], spots[j]) + Vector3.Distance(spots[j], spots[k]) + Vector3.Distance(spots[k], spots[i]);
                    if (perimeter > bestPerimeter)
                    {
                        bestPerimeter = perimeter;
                        best = new[] { i, j, k };
                    }
                }
            }
        }

        if (best == null)
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    float d = Vector3.Distance(spots[i], spots[j]);
                    if (direct[i, j] && d > bestPerimeter)
                    {
                        bestPerimeter = d;
                        best = new[] { i, j };
                    }
                }
            }
        }

        List<Vector3> chosen = new List<Vector3>();
        if (best != null)
        {
            foreach (int index in best)
                chosen.Add(spots[index]);
        }

        GameObject root = GameObject.Find(WaypointRootName);
        if (root == null)
        {
            root = new GameObject(WaypointRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Waypoint Root");
        }

        // Never name the group exactly like the NPC, or name lookups can find the group instead of the character.
        GameObject group = new GameObject(npc.name + "_Waypoints" + CurrentWaypointSuffix);
        Undo.RegisterCreatedObjectUndo(group, "Create Waypoints");
        group.transform.SetParent(root.transform, false);

        List<Transform> result = new List<Transform>();
        for (int i = 0; i < chosen.Count; i++)
        {
            GameObject wp = new GameObject("WP_" + (i + 1));
            wp.transform.SetParent(group.transform, false);
            wp.transform.position = chosen[i];
            result.Add(wp.transform);
        }

        return result;
    }

    // Waypoint groups made by the current logic carry this suffix; older groups do not and get replaced once.
    private const string CurrentWaypointSuffix = " v2";

    private static bool WaypointsAreCurrent(SerializedProperty waypointProperty)
    {
        for (int i = 0; i < waypointProperty.arraySize; i++)
        {
            Transform wp = waypointProperty.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (wp == null || wp.parent == null || !wp.parent.name.EndsWith(CurrentWaypointSuffix))
                return false;
        }

        return true;
    }

    private static void DestroyWaypointGroups(SerializedProperty waypointProperty)
    {
        HashSet<GameObject> groups = new HashSet<GameObject>();

        for (int i = 0; i < waypointProperty.arraySize; i++)
        {
            Transform wp = waypointProperty.GetArrayElementAtIndex(i).objectReferenceValue as Transform;

            // Only delete a group that sits under NPC_Waypoints, never anything the user parented elsewhere.
            if (wp != null && wp.parent != null && wp.parent.parent != null && wp.parent.parent.name == WaypointRootName)
                groups.Add(wp.parent.gameObject);
        }

        foreach (GameObject group in groups)
            Undo.DestroyObjectImmediate(group);
    }

    private static bool IsDirectWalk(Vector3 from, Vector3 to, float directness, NavMeshPath path)
    {
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        return PathLength(path) <= Vector3.Distance(from, to) * directness + 0.15f;
    }

    private static float PathLength(NavMeshPath path)
    {
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }
}

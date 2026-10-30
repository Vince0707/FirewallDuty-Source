using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Firewall Duty > Set Up Doors In Open Scenes
// Safe to run repeatedly: doors that are already configured are left as they are.
public static class DoorSetupTool
{
    private static readonly string[] ExcludedNameParts = { "frame", "pivot", "hinge", "safe", "vault", "cabinet", "drawer", "furniture", "sign", "label" };

    [MenuItem("Tools/Firewall Duty/Set Up Doors In Open Scenes")]
    public static void SetUpDoorsInOpenScenes()
    {
        StringBuilder report = new StringBuilder();
        int doorCount = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            report.AppendLine("Scene: " + scene.name);

            foreach (Transform leaf in FindDoorLeaves(scene))
            {
                string result = ConfigureDoor(leaf);
                report.AppendLine("  [door] " + GetPath(leaf) + " -> " + result);
                doorCount++;
            }

            string promptResult = SetUpPrompt(scene);
            if (promptResult != null)
                report.AppendLine("  [prompt] " + promptResult);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        report.AppendLine();
        report.AppendLine(doorCount + " door(s) processed. Save the scene(s) (Ctrl+S) to keep the changes.");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static List<Transform> FindDoorLeaves(Scene scene)
    {
        List<Transform> leaves = new List<Transform>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (IsDoorLeaf(t))
                    leaves.Add(t);
            }
        }

        return leaves;
    }

    // A door leaf is the moving part: an object named "...door..." that has its own mesh or collider.
    private static bool IsDoorLeaf(Transform t)
    {
        // The vehicles, the street and the people outside (the group "Outside_Ground") have parts called "DoorLeaf" and "DoorMat": they are not doors.
        for (Transform ancestor = t; ancestor != null; ancestor = ancestor.parent)
        {
            if (ancestor.name == "Outside_Ground")
                return false;
        }

        // A sliding door is set up by SlidingDoorSetupTool; never turn it back into a swinging one.
        if (t.GetComponentInParent<SlidingDoorInteractable>() != null)
            return false;

        if (t.GetComponent<DoorInteractable>() != null)
            return true;

        if (t.GetComponentInParent<DoorInteractable>() != null)
            return false;

        string lower = t.name.ToLowerInvariant();
        if (!lower.Contains("door"))
            return false;

        foreach (string excluded in ExcludedNameParts)
        {
            if (lower.Contains(excluded))
                return false;
        }

        return t.GetComponent<Renderer>() != null || t.GetComponent<Collider>() != null;
    }

    private static string ConfigureDoor(Transform leaf)
    {
        List<string> notes = new List<string>();

        DoorInteractable door = leaf.GetComponent<DoorInteractable>();
        if (door == null)
        {
            door = Undo.AddComponent<DoorInteractable>(leaf.gameObject);
            notes.Add("added DoorInteractable");
        }

        SerializedObject so = new SerializedObject(door);
        SerializedProperty pivotProperty = so.FindProperty("doorPivot");
        Transform pivot = pivotProperty.objectReferenceValue as Transform;

        if (pivot == null)
        {
            pivot = ResolvePivot(leaf, notes);
            if (pivot == null)
            {
                // Without a hinge the door would spin around its own centre, so don't leave it half set up.
                Undo.DestroyObjectImmediate(door);
                return "SKIPPED, DoorInteractable removed (" + string.Join(", ", notes) + ")";
            }

            pivotProperty.objectReferenceValue = pivot;
            so.ApplyModifiedProperties();
        }

        if (ClearStaticFlags(pivot))
            notes.Add("cleared Static flags so the mesh can move");

        if (EnsureLeafCollider(leaf))
            notes.Add("added missing collider");

        int slabs = FixFrameColliders(pivot);
        if (slabs > 0)
            notes.Add("opened doorway: replaced " + slabs + " full-width frame collider(s) with a header");

        EditorUtility.SetDirty(door);
        return notes.Count > 0 ? string.Join("; ", notes) : "already configured";
    }

    private static Transform ResolvePivot(Transform leaf, List<string> notes)
    {
        Transform parent = leaf.parent;
        if (parent != null)
        {
            string parentName = parent.name.ToLowerInvariant();
            if (parentName.Contains("pivot") || parentName.Contains("hinge"))
            {
                notes.Add("using parent '" + parent.name + "' as hinge");
                return parent;
            }
        }

        // The Office Essentials separator door mesh already has its origin on the hinge edge.
        if (leaf.name.IndexOf("separatorDoor_body", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            notes.Add("mesh origin is the hinge");
            return leaf;
        }

        return CreateHinge(leaf, notes);
    }

    // For a door whose origin is at its centre (e.g. a scaled cube): insert a hinge object on its edge.
    private static Transform CreateHinge(Transform leaf, List<string> notes)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(leaf))
        {
            notes.Add("hinge needed but door is part of a prefab instance - unpack it (or assign Door Pivot by hand)");
            return null;
        }

        if (!TryGetLocalBounds(leaf, out Bounds local))
        {
            notes.Add("no mesh or box collider to measure - assign Door Pivot by hand");
            return null;
        }

        Vector3 scale = leaf.lossyScale;
        bool widthAlongX = local.size.x * Mathf.Abs(scale.x) >= local.size.z * Mathf.Abs(scale.z);
        Vector3 hingeLocal = widthAlongX
            ? local.center - Vector3.right * local.extents.x
            : local.center - Vector3.forward * local.extents.z;

        GameObject hinge = new GameObject(leaf.name + "_Hinge");
        Undo.RegisterCreatedObjectUndo(hinge, "Create Door Hinge");
        SceneManager.MoveGameObjectToScene(hinge, leaf.gameObject.scene);
        hinge.transform.SetParent(leaf.parent, false);
        hinge.transform.position = leaf.TransformPoint(hingeLocal);
        hinge.transform.rotation = Quaternion.Euler(0f, leaf.eulerAngles.y, 0f);

        Undo.SetTransformParent(leaf, hinge.transform, "Parent Door To Hinge");

        notes.Add("created hinge '" + hinge.name + "' on the " + (widthAlongX ? "-X" : "-Z") + " edge");
        return hinge.transform;
    }

    private static bool TryGetLocalBounds(Transform leaf, out Bounds bounds)
    {
        BoxCollider box = leaf.GetComponent<BoxCollider>();
        if (box != null)
        {
            bounds = new Bounds(box.center, box.size);
            return true;
        }

        MeshFilter filter = leaf.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            bounds = filter.sharedMesh.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    // Static-batched meshes are merged at load time and ignore transform changes, so a door must not be static.
    private static bool ClearStaticFlags(Transform pivot)
    {
        bool changed = false;

        foreach (Transform t in pivot.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0)
                continue;

            Undo.RecordObject(t.gameObject, "Clear Door Static Flags");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            changed = true;
        }

        return changed;
    }

    private static bool EnsureLeafCollider(Transform leaf)
    {
        if (leaf.GetComponentInChildren<Collider>() != null)
            return false;

        if (!TryGetLocalBounds(leaf, out Bounds local))
            return false;

        BoxCollider box = Undo.AddComponent<BoxCollider>(leaf.gameObject);
        box.center = local.center;
        box.size = local.size;
        return true;
    }

    // Door frame prefabs often ship one collider covering the whole doorway, which keeps the player
    // blocked after the door opens. Replace it with a header collider above the door.
    private static int FixFrameColliders(Transform pivot)
    {
        // The frame can sit at different depths (it is a sibling of the door group in some scenes),
        // so search from the highest ancestor that is still part of the door, not just the direct parent.
        Transform assembly = pivot.parent;
        while (assembly != null && assembly.parent != null
               && assembly.parent.name.ToLowerInvariant().Contains("door"))
            assembly = assembly.parent;

        if (assembly == null)
            return 0;

        bool hasLeafBounds = false;
        Bounds leafBounds = new Bounds(pivot.position, Vector3.zero);
        foreach (Collider c in pivot.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger)
                continue;

            if (!hasLeafBounds) { leafBounds = c.bounds; hasLeafBounds = true; }
            else leafBounds.Encapsulate(c.bounds);
        }

        if (!hasLeafBounds)
            return 0;

        int fixedCount = 0;

        foreach (BoxCollider slab in assembly.GetComponentsInChildren<BoxCollider>())
        {
            if (!slab.enabled || slab.isTrigger || slab.transform.IsChildOf(pivot))
                continue;

            string slabName = slab.name.ToLowerInvariant();
            if (!slabName.Contains("frame") && !slabName.Contains("door"))
                continue;

            // A slab fills the doorway: it touches the door and is about as wide and tall as it.
            // (Jambs are too narrow and headers too short, so they are left alone.)
            // The frame and door colliders can be rotated differently, so compare sizes, not centres.
            Bounds slabBounds = slab.bounds;
            float leafWidth = Mathf.Max(leafBounds.size.x, leafBounds.size.z);
            float slabWidth = Mathf.Max(slabBounds.size.x, slabBounds.size.z);

            if (!slabBounds.Intersects(leafBounds)
                || slabWidth < leafWidth * 0.8f
                || slabBounds.size.y < leafBounds.size.y * 0.8f)
                continue;

            Transform st = slab.transform;
            bool upright = Vector3.Dot(st.up, Vector3.up) > 0.99f;

            Undo.RecordObject(slab, "Open Doorway");
            slab.enabled = false;

            if (upright)
            {
                float localTop = slab.center.y + slab.size.y * 0.5f;
                float localDoorTop = st.InverseTransformPoint(
                    new Vector3(slab.bounds.center.x, leafBounds.max.y, slab.bounds.center.z)).y;

                if (localTop - localDoorTop > 0.05f)
                {
                    BoxCollider header = Undo.AddComponent<BoxCollider>(st.gameObject);
                    header.center = new Vector3(slab.center.x, (localTop + localDoorTop) * 0.5f, slab.center.z);
                    header.size = new Vector3(slab.size.x, localTop - localDoorTop, slab.size.z);
                }
            }

            fixedCount++;
        }

        return fixedCount;
    }

    // Adds InteractionPromptUI to the on-screen button that is wired to PlayerInteraction.Interact.
    private static string SetUpPrompt(Scene scene)
    {
        PlayerInteraction player = null;
        Button promptButton = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (player == null)
                player = root.GetComponentInChildren<PlayerInteraction>(true);

            if (promptButton != null)
                continue;

            foreach (Button b in root.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentTarget(i) is PlayerInteraction)
                    {
                        promptButton = b;
                        break;
                    }
                }

                if (promptButton != null)
                    break;
            }
        }

        if (player == null)
            return "no PlayerInteraction in this scene - nothing to wire (expected for scenes without a player)";

        if (promptButton == null)
            return "PlayerInteraction found but no UI Button calls PlayerInteraction.Interact - hook one up (see steps in the chat)";

        List<string> notes = new List<string>();

        InteractionPromptUI prompt = promptButton.GetComponent<InteractionPromptUI>();
        if (prompt == null)
        {
            prompt = Undo.AddComponent<InteractionPromptUI>(promptButton.gameObject);
            notes.Add("added InteractionPromptUI to '" + promptButton.name + "'");
        }

        SerializedObject so = new SerializedObject(prompt);
        so.FindProperty("playerInteraction").objectReferenceValue = player;
        so.FindProperty("button").objectReferenceValue = promptButton;
        so.ApplyModifiedProperties();

        RectTransform rect = promptButton.GetComponent<RectTransform>();
        bool untouchedTestLayout = rect.anchorMin == new Vector2(0.5f, 0.5f)
                                   && rect.anchorMax == new Vector2(0.5f, 0.5f)
                                   && rect.sizeDelta == new Vector2(160f, 30f);
        if (untouchedTestLayout)
        {
            Undo.RecordObject(rect, "Layout Interact Button");
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(260f, 100f);
            rect.anchoredPosition = new Vector2(-80f, 260f);
            notes.Add("moved to bottom-right and enlarged for thumb taps");

            TMP_Text label = promptButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                Undo.RecordObject(label, "Interact Button Label");
                label.text = "OPEN";
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 48f;
                label.alignment = TextAlignmentOptions.Center;
            }
        }

        return notes.Count > 0 ? string.Join("; ", notes) : "already configured";
    }

    private static string GetPath(Transform t)
    {
        StringBuilder sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent)
            sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Set Up Sliding Entrance Door
// Replaces the small box that stood in the entrance with a proper bi-parting automatic sliding door:
//   - measures the doorway from the front walls (the gap between Wall_Front_Left and Wall_Front_Right, floor to the
//     lintel Wall_front_top);
//   - builds two full-height panels that fill the whole gap and meet in the middle;
//   - they split apart (each sliding in front of the wall) when the player comes near, and close again afterwards.
// The old box and its hinge object are deleted (Ctrl+Z restores them). Safe to run repeatedly.
public static class SlidingDoorSetupTool
{
    private const string DoorName = "Entrance_Door";
    private const float PanelThickness = 0.05f;
    private const float Overlap = 0.02f;   // the two panels meet with this overlap so there is no gap when closed

    [MenuItem("Tools/Firewall Duty/Set Up Sliding Entrance Door")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();

        Transform leftWall = FindObject("Wall_Front_Left");
        Transform rightWall = FindObject("Wall_Front_Right");
        Transform lintel = FindObject("Wall_front_top");

        if (leftWall == null || rightWall == null || lintel == null)
        {
            UnityEngine.Debug.LogError("Could not find the front walls ('Wall_Front_Left', 'Wall_Front_Right', 'Wall_front_top') to measure the doorway.");
            return;
        }

        Transform old = FindObject(DoorName);
        Material material = null;

        if (old != null)
        {
            SlidingDoorInteractable existing = old.GetComponent<SlidingDoorInteractable>();
            if (existing != null && new SerializedObject(existing).FindProperty("secondLeaf").objectReferenceValue != null)
            {
                UnityEngine.Debug.Log("The entrance is already a two-panel sliding door. To resize it, edit the panels or the 'Slide Distance' on '" + DoorName + "'.");
                return;
            }

            MeshRenderer oldRenderer = old.GetComponentInChildren<MeshRenderer>();
            if (oldRenderer != null)
                material = oldRenderer.sharedMaterial;

            // Remove the old box, and its hinge object if the box was the only thing on it.
            Transform hinge = old.parent;
            Undo.DestroyObjectImmediate(old.gameObject);
            report.AppendLine("Removed the old entrance box.");

            if (hinge != null && hinge.name.ToLowerInvariant().Contains("hinge") && hinge.childCount == 0)
            {
                Undo.DestroyObjectImmediate(hinge.gameObject);
                report.AppendLine("Removed its hinge object.");
            }
        }

        if (material == null)
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Door_material.mat");

        // Measure the doorway.
        Physics.SyncTransforms();
        Bounds left = MeasureBounds(leftWall);
        Bounds right = MeasureBounds(rightWall);
        Bounds top = MeasureBounds(lintel);

        float openLeft = left.max.x;
        float openRight = right.min.x;
        float floorY = FloorHeight();
        float topY = top.min.y;
        float openWidth = openRight - openLeft;
        float height = topY - floorY;
        float centerX = (openLeft + openRight) * 0.5f;

        if (openWidth < 0.5f || height < 1f)
        {
            UnityEngine.Debug.LogError("The doorway measured " + openWidth.ToString("F2") + " m wide and " + height.ToString("F2") +
                                       " m high, which looks wrong. Check the three front wall objects.");
            return;
        }

        // Sit the panels just inside the room, against the wall's inner face, so they slide along the wall without clipping it.
        float z = left.max.z + PanelThickness * 0.5f + 0.005f;

        GameObject group = new GameObject(DoorName);
        Undo.RegisterCreatedObjectUndo(group, "Create Sliding Entrance Door");
        group.transform.position = new Vector3(centerX, floorY, z);

        float panelWidth = openWidth * 0.5f + Overlap * 0.5f;
        float slide = openWidth * 0.5f;

        Transform leftPanel = CreatePanel(group.transform, "Entrance_Door_Left", -(openWidth * 0.25f - Overlap * 0.25f), panelWidth, height, material);
        Transform rightPanel = CreatePanel(group.transform, "Entrance_Door_Right", openWidth * 0.25f - Overlap * 0.25f, panelWidth, height, material);

        SlidingDoorInteractable door = Undo.AddComponent<SlidingDoorInteractable>(group);
        SerializedObject so = new SerializedObject(door);
        so.FindProperty("leaf").objectReferenceValue = leftPanel;
        so.FindProperty("secondLeaf").objectReferenceValue = rightPanel;
        so.FindProperty("slideDirection").vector3Value = Vector3.left;   // the left panel goes left, the right one goes right
        so.FindProperty("slideDistance").floatValue = Mathf.Round(slide * 100f) / 100f;
        so.FindProperty("automatic").boolValue = true;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(door);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        report.AppendLine("Doorway: " + openWidth.ToString("F2") + " m wide (x " + openLeft.ToString("F2") + " to " + openRight.ToString("F2") + "), " +
                          height.ToString("F2") + " m high (floor " + floorY.ToString("F2") + " to the lintel at " + topY.ToString("F2") + ").");
        report.AppendLine("Built two panels, each " + panelWidth.ToString("F2") + " m wide, that slide " + slide.ToString("F2") + " m apart.");
        report.AppendLine("They open by themselves when the player comes within about 2 m; the hand button also opens and closes them.");
        report.AppendLine("Save the scene (Ctrl+S) and press Play, then walk up to the entrance.");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static Transform CreatePanel(Transform parent, string panelName, float centerOffsetX, float width, float height, Material material)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(panel, "Create Door Panel");
        panel.name = panelName;
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = new Vector3(centerOffsetX, height * 0.5f, 0f);
        panel.transform.localScale = new Vector3(width, height, PanelThickness);

        if (material != null)
            panel.GetComponent<MeshRenderer>().sharedMaterial = material;

        return panel.transform;
    }

    // Top of the floor, so the panels stand on it. Falls back to the height the characters stand at.
    private static float FloorHeight()
    {
        Transform floor = FindObject("Floor");
        if (floor != null)
        {
            Bounds b = MeasureBounds(floor);
            if (b.size.sqrMagnitude > 0f)
                return b.max.y;
        }

        return 0.05f;
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

    private static Bounds MeasureBounds(Transform t)
    {
        Bounds bounds = new Bounds(t.position, Vector3.zero);
        bool any = false;

        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!any)
        {
            foreach (Collider c in t.GetComponentsInChildren<Collider>())
            {
                if (!any) { bounds = c.bounds; any = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }

        return bounds;
    }
}

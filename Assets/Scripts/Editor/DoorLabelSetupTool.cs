using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Set Up Door Labels
// Puts a room title above each room door, on the side people approach from: "Meeting Room", "Head Manager",
// "IT Tech Room". Each is a small dark sign plate with white 3D text (TextMeshPro) on its outer face.
// The IT door already has a blank sign plate ("IT_Restricted_Sign"); that one is reused instead of adding another.
// Safe to run repeatedly: existing signs and labels are not moved, only their text is refreshed.
public static class DoorLabelSetupTool
{
    private struct Entry
    {
        public string door;          // name of the door object in the scene
        public string text;
        public Vector2 roomCenter;   // roughly the middle of the room (x, z): the sign faces away from it

        public Entry(string door, string text, Vector2 roomCenter)
        {
            this.door = door;
            this.text = text;
            this.roomCenter = roomCenter;
        }
    }

    private static readonly Entry[] Entries =
    {
        new Entry("(Prb)SeparatorDoor (2)", "Meeting Room", new Vector2(-9.3f, 8.3f)),
        new Entry("(Prb)SeparatorDoor (1)", "Head Manager", new Vector2(12f, 8f)),
        new Entry("(Prb)SeparatorDoor", "IT Tech Room", new Vector2(11f, -8f))
    };

    private const string RootName = "Door_Labels";
    private const string ExistingItSign = "IT_Restricted_Sign";
    private const string MaterialPath = "Assets/Materials/DoorSign.mat";
    private const float PlateWidth = 1.0f;       // metres (as the IT sign)
    private const float MaxPlateHeight = 0.30f;  // the plate is shorter than this when the gap above the door is small
    private const float PlateThickness = 0.05f;

    [MenuItem("Tools/Firewall Duty/Set Up Door Labels")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();
        Material plateMaterial = EnsurePlateMaterial();

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Door Labels");
        }

        float ceilingY = CeilingHeight();
        report.AppendLine("Ceiling height used: " + ceilingY.ToString("F2") + " m (the top of the outer walls).");

        foreach (Entry entry in Entries)
            report.AppendLine("[" + entry.text + "] " + SetUpDoor(entry, root.transform, plateMaterial, ceilingY));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S) and press Play, then look at the doors from the corridor side. " +
                          "To move or resize a sign, move the 'DoorSign_...' and 'DoorLabel_...' objects under " + RootName + ".");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static string SetUpDoor(Entry entry, Transform root, Material plateMaterial, float ceilingY)
    {
        Transform door = FindObject(entry.door);
        if (door == null)
            return "SKIPPED: door '" + entry.door + "' not found in the open scene.";

        Bounds doorBounds = MeasureBounds(door);

        // The sign faces away from the room, along whichever axis the door's wall runs across.
        Vector2 fromRoom = new Vector2(doorBounds.center.x, doorBounds.center.z) - entry.roomCenter;
        Vector3 outward = Mathf.Abs(fromRoom.x) >= Mathf.Abs(fromRoom.y)
            ? new Vector3(Mathf.Sign(fromRoom.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(fromRoom.y));

        // The sign goes in the gap between the top of the door and the ceiling, with a little air above and below.
        float gap = ceilingY - doorBounds.max.y;
        float plateHeight = Mathf.Clamp(gap - 0.08f, 0.12f, MaxPlateHeight);
        float plateCenterY = doorBounds.max.y + gap * 0.5f;
        plateCenterY = Mathf.Min(plateCenterY, ceilingY - plateHeight * 0.5f - 0.03f);

        string labelName = "DoorLabel_" + entry.text;
        string signName = "DoorSign_" + entry.text;
        List<string> notes = new List<string>();

        // 1. The sign plate: the existing blank IT sign, or a new one above the door.
        Transform plate = null;

        if (entry.text == "IT Tech Room")
        {
            Transform existing = FindObject(ExistingItSign);
            if (existing != null)
            {
                plate = existing;
                notes.Add("used the existing '" + ExistingItSign + "' plate");
            }
        }

        if (plate == null)
            plate = root.Find(signName);

        if (plate == null)
        {
            float halfDepth = outward.x != 0f ? doorBounds.extents.x : doorBounds.extents.z;
            float offset = Mathf.Clamp(halfDepth, 0.05f, 0.15f) + PlateThickness * 0.5f + 0.005f;

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Create Door Sign");
            cube.name = signName;
            cube.transform.SetParent(root, false);
            cube.transform.position = new Vector3(doorBounds.center.x, plateCenterY, doorBounds.center.z) + outward * offset;
            cube.transform.rotation = Quaternion.LookRotation(-outward);
            cube.transform.localScale = new Vector3(PlateWidth, plateHeight, PlateThickness);

            // The label is the readable part; the plate needs no collider (it must not block the player or the crosshair).
            Collider col = cube.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);

            plate = cube.transform;
            notes.Add("made a sign above the door");
        }

        Renderer plateRenderer = plate.GetComponent<Renderer>();
        if (plateRenderer == null)
            return string.Join("; ", notes) + "; SKIPPED: the sign plate has no renderer.";

        if (plateMaterial != null)
        {
            Undo.RecordObject(plateRenderer, "Style Door Sign");
            plateRenderer.sharedMaterial = plateMaterial;
        }

        // Re-fit a plate that is too high (through the ceiling) or overlaps the door. A plate that already fits,
        // for example one you moved by hand, is left exactly where it is.
        Bounds plateBounds = plateRenderer.bounds;
        bool badFit = plateBounds.max.y > ceilingY - 0.02f || plateBounds.min.y < doorBounds.max.y + 0.01f;
        bool refit = badFit;

        if (refit)
        {
            Undo.RecordObject(plate, "Fit Door Sign");
            Vector3 scale = plate.localScale;
            scale.y = plateHeight;
            plate.localScale = scale;

            Vector3 position = plate.position;
            position.y = plateCenterY;
            plate.position = position;

            notes.Add("moved down to fit under the ceiling (centre " + plateCenterY.ToString("F2") + ", height " + plateHeight.ToString("F2") + ")");
            plateBounds = plateRenderer.bounds;
        }

        float width = outward.x != 0f ? plateBounds.size.z : plateBounds.size.x;
        float height = plateBounds.size.y;

        // 2. The text, just in front of the plate's outer face, readable from outside.
        Transform label = root.Find(labelName);
        bool created = label == null;
        TextMeshPro tmp;

        if (created)
        {
            GameObject go = new GameObject(labelName, typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(go, "Create Door Label");
            go.transform.SetParent(root, false);
            label = go.transform;
            tmp = go.GetComponent<TextMeshPro>();
            label.rotation = Quaternion.LookRotation(-outward);   // TextMeshPro reads correctly when its forward points away from the viewer
        }
        else
        {
            tmp = label.GetComponent<TextMeshPro>();
            if (tmp == null)
                tmp = Undo.AddComponent<TextMeshPro>(label.gameObject);
        }

        if (created || refit)
        {
            float faceOffset = (outward.x != 0f ? plateBounds.extents.x : plateBounds.extents.z) + 0.004f;
            label.position = plateBounds.center + outward * faceOffset;
        }

        tmp.text = entry.text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.5f;
        tmp.fontSizeMax = 20f;
        tmp.rectTransform.sizeDelta = new Vector2(width * 0.92f, height * 0.8f);

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        MeshRenderer meshRenderer = tmp.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

        EditorUtility.SetDirty(tmp);

        string facing = outward.x > 0 ? "east" : outward.x < 0 ? "west" : outward.z > 0 ? "north" : "south";
        if (notes.Count == 0)
            notes.Add("kept the sign where it is (it already fits)");

        return string.Join("; ", notes) + "; door top " + doorBounds.max.y.ToString("F2") + ", ceiling " + ceilingY.ToString("F2") +
               "; text facing " + facing + " at " + label.position.ToString("F2");
    }

    // The top of the room's walls is where the ceiling is; signs must stay below it.
    private static float CeilingHeight()
    {
        float top = 0f;

        foreach (string wallName in new[] { "Wall_back", "Wall_left", "Wall_right", "Wall_Front_Left" })
        {
            Transform wall = FindObject(wallName);
            if (wall != null)
                top = Mathf.Max(top, MeasureBounds(wall).max.y);
        }

        return top > 1f ? top : 3f;
    }

    // A simple dark plate material (URP Lit), made once.
    private static Material EnsurePlateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        material = new Material(shader) { name = "DoorSign" };
        material.SetColor("_BaseColor", new Color(0.09f, 0.13f, 0.2f, 1f));
        material.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
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

        return bounds;
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Office > Add Wall Panelling   (run it with Company_scene open)
// Tools > Firewall Duty > Bank > Add Wall Panelling      (run it with Bank_scene open)
// Navy panelling with a cap on top goes on every wall inside the building: the outer walls on the inside, and both sides of the inner walls.
// (The office has a cream cap and 1 m panels; the bank has a gold cap and 1.3 m panels, because its walls are much taller.)
// It is only put where nothing stands against the wall: furniture, shelves, counters, ATMs, door frames and the place where the sliding entrance door opens
// leave a gap, so nothing is covered or pushed into the wall. Glass partitions get none (they stay see-through), and neither does the outside of the building.
// Everything is in one group, "Wall_Panelling", made of plain boxes without colliders. Run it again to rebuild it (after you move furniture, for example);
// Ctrl+Z removes it.
public static class OfficePanellingTool
{
    private const string RootName = "Wall_Panelling";
    private const string MaterialFolder = "Assets/Materials/Lounge";
    private const float PanelThickness = 0.04f;
    private const float CapThickness = 0.06f;
    private const float MinPiece = 0.25f;     // shorter pieces are left out

    // What differs between the two buildings.
    private class Config
    {
        public string scene;
        public string label;
        public float floorTop;             // the top of the floor
        public float panelHeight;
        public float maxWallThickness;     // a wall piece is thinner than this
        public float minWallHeight;        // ... and taller than this
        public Vector4 rect;               // the building: minX, maxX, minZ, maxZ
        public Vector3 doorExpand;         // how far the sliding door's panels reach beyond the door, per axis (total)
        public Color capColor;
    }

    private static readonly Config OfficeConfig = new Config
    {
        scene = "Company_scene", label = "office", floorTop = 0.05f, panelHeight = 1.0f, maxWallThickness = 0.35f, minWallHeight = 2.5f,
        rect = new Vector4(-15.3f, 15.3f, -12.3f, 12.3f), doorExpand = new Vector3(3.3f, 0f, 0.1f), capColor = new Color(0.95f, 0.92f, 0.84f)
    };

    private static readonly Config BankConfig = new Config
    {
        scene = "Bank_scene", label = "bank", floorTop = 0.25f, panelHeight = 1.3f, maxWallThickness = 0.6f, minWallHeight = 3.0f,
        rect = new Vector4(-3.3f, 17.0f, -7.5f, 14.8f), doorExpand = new Vector3(0.1f, 0f, 3.5f), capColor = new Color(0.92f, 0.72f, 0.28f)
    };

    private struct Face
    {
        public bool alongX;      // the wall runs along X (it is thin in Z)
        public float coordinate; // the wall face on the thin axis
        public float normal;     // +1 or -1: the side of the face the room is on
        public float lo, hi;     // the extent along the wall
    }

    [MenuItem("Tools/Firewall Duty/Office/Add Wall Panelling")]
    public static void AddOffice()
    {
        Add(OfficeConfig);
    }

    [MenuItem("Tools/Firewall Duty/Bank/Add Wall Panelling")]
    public static void AddBank()
    {
        Add(BankConfig);
    }

    private static void Add(Config c)
    {
        if (SceneManager.GetActiveScene().name != c.scene)
        {
            EditorUtility.DisplayDialog("Add Wall Panelling", "Open Assets/Scenes/" + c.scene + ".unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        foreach (GameObject old in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (old.name == RootName)
            {
                Undo.DestroyObjectImmediate(old);
                break;
            }
        }

        if (c == OfficeConfig)
            RemoveLoungePanels();

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Lounge");

        Material navy = Mat("Navy", new Color(0.09f, 0.2f, 0.38f), 0.2f);
        Material cap = c == BankConfig ? Mat("Gold", new Color(0.92f, 0.72f, 0.28f), 0.7f) : Mat("Cream", c.capColor, 0.15f);

        List<Renderer> walls = new List<Renderer>();
        List<Renderer> obstacles = new List<Renderer>();
        Collect(c, walls, obstacles);

        // The sliding door slides open over the wall on both sides of it, so that stretch of wall is treated as taken.
        Bounds? doorZone = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Entrance_Door")
                continue;

            Renderer[] parts = root.GetComponentsInChildren<Renderer>();
            if (parts.Length == 0)
                break;

            Bounds b = parts[0].bounds;
            foreach (Renderer part in parts)
                b.Encapsulate(part.bounds);

            b.Expand(c.doorExpand);
            doorZone = b;
        }

        GameObject rootObject = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Add Wall Panelling");

        int pieces = 0;
        float metres = 0f;

        foreach (Renderer wall in walls)
        {
            foreach (Face face in FacesOf(wall))
            {
                if (!FacesTheRoom(c, face))
                    continue;

                foreach (Vector2 piece in FreeStretches(c, face, obstacles, doorZone))
                {
                    Build(c, rootObject.transform, face, piece.x, piece.y, navy, cap);
                    pieces++;
                    metres += piece.y - piece.x;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Add Wall Panelling (" + c.label + "): " + pieces + " piece(s), " + metres.ToString("F0") + " m of navy panelling with a " + (c == BankConfig ? "gold" : "cream") +
                              " cap on the inside of the walls (left out where furniture, counters, ATMs, door frames, glass partitions or the sliding door are). " +
                              "Save the scene (Ctrl+S). Use Edit > Undo to take it away.");
    }

    // ---- Finding the walls and what stands in front of them -----------------------------------------------------------------------

    private static void Collect(Config c, List<Renderer> walls, List<Renderer> obstacles)
    {
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Transform t = renderer.transform;
            string lower = t.name.ToLowerInvariant();

            // (The sliding door and its trim are handled as one zone, below.)
            if (IsUnder(t, "Outside_Ground") || IsUnder(t, RootName) || IsUnder(t, "Entrance_Door") || lower == "ceiling" || lower == "floor" || lower.StartsWith("doorway_fill_"))
                continue;

            Bounds b = renderer.bounds;
            float thin = Mathf.Min(b.size.x, b.size.z);
            bool tall = b.size.y > c.minWallHeight;

            // A wall piece (or a glass partition, or the thin paint on a wall): tall and thin. Only solid, floor-standing, real walls get panelling.
            if (tall && thin < c.maxWallThickness)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                bool cube = filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "Cube";
                bool glass = IsGlass(renderer) || lower.Contains("glass");

                if (cube && !glass && thin >= 0.02f && b.min.y < c.floorTop + 0.15f && Vector3.Dot(t.up, Vector3.up) > 0.999f && Inside(c, b.center, 0.3f))
                    walls.Add(renderer);

                continue;   // never an obstacle itself
            }

            // Flat things on the floor (rugs, markings) are not in the way.
            if (b.max.y < c.floorTop + 0.12f)
                continue;

            if (Inside(c, b.center, 0.6f))
                obstacles.Add(renderer);
        }
    }

    private static IEnumerable<Face> FacesOf(Renderer wall)
    {
        Bounds b = wall.bounds;
        bool alongX = b.size.x >= b.size.z;

        if (alongX)
        {
            yield return new Face { alongX = true, coordinate = b.min.z, normal = -1f, lo = b.min.x, hi = b.max.x };
            yield return new Face { alongX = true, coordinate = b.max.z, normal = 1f, lo = b.min.x, hi = b.max.x };
        }
        else
        {
            yield return new Face { alongX = false, coordinate = b.min.x, normal = -1f, lo = b.min.z, hi = b.max.z };
            yield return new Face { alongX = false, coordinate = b.max.x, normal = 1f, lo = b.min.z, hi = b.max.z };
        }
    }

    // The face is inside the building when a point 30 cm in front of it is inside (the outside of the outer walls is not panelled).
    private static bool FacesTheRoom(Config c, Face face)
    {
        float mid = (face.lo + face.hi) * 0.5f;
        float ahead = face.coordinate + face.normal * 0.3f;
        Vector3 point = face.alongX ? new Vector3(mid, 1f, ahead) : new Vector3(ahead, 1f, mid);
        return Inside(c, point, 0f);
    }

    // The parts of the face (along the wall) with nothing against it.
    private static List<Vector2> FreeStretches(Config c, Face face, List<Renderer> obstacles, Bounds? doorZone)
    {
        // The box in front of the wall that the panelling would fill (a little deeper than the panelling).
        float depth = CapThickness + 0.01f;
        float regionHeight = c.panelHeight + 0.1f;
        float y = c.floorTop + regionHeight * 0.5f;
        float mid = (face.lo + face.hi) * 0.5f;
        float length = face.hi - face.lo;
        float thinCentre = face.coordinate + face.normal * depth * 0.5f;

        Bounds region = face.alongX
            ? new Bounds(new Vector3(mid, y, thinCentre), new Vector3(length, regionHeight, depth))
            : new Bounds(new Vector3(thinCentre, y, mid), new Vector3(depth, regionHeight, length));

        List<Vector2> blocked = new List<Vector2>();

        foreach (Renderer obstacle in obstacles)
        {
            Bounds b = obstacle.bounds;
            if (b.Intersects(region))
                blocked.Add(face.alongX ? new Vector2(b.min.x - 0.03f, b.max.x + 0.03f) : new Vector2(b.min.z - 0.03f, b.max.z + 0.03f));
        }

        if (doorZone.HasValue && doorZone.Value.Intersects(region))
        {
            Bounds d = doorZone.Value;
            blocked.Add(face.alongX ? new Vector2(d.min.x, d.max.x) : new Vector2(d.min.z, d.max.z));
        }

        blocked.Sort((a, b) => a.x.CompareTo(b.x));

        List<Vector2> free = new List<Vector2>();
        float cursor = face.lo;

        foreach (Vector2 block in blocked)
        {
            if (block.x - cursor >= MinPiece)
                free.Add(new Vector2(cursor, Mathf.Min(block.x, face.hi)));

            cursor = Mathf.Max(cursor, block.y);
        }

        if (face.hi - cursor >= MinPiece)
            free.Add(new Vector2(cursor, face.hi));

        return free;
    }

    // ---- Building a piece ------------------------------------------------------------------------------------------------------------------

    private static void Build(Config c, Transform root, Face face, float from, float to, Material navy, Material cap)
    {
        float mid = (from + to) * 0.5f;
        float length = to - from;

        MakeBox(root, "Panel", face, mid, length, c.floorTop + c.panelHeight * 0.5f, c.panelHeight, face.coordinate + face.normal * PanelThickness * 0.5f, PanelThickness, navy);
        MakeBox(root, "PanelCap", face, mid, length, c.floorTop + c.panelHeight + 0.02f, 0.04f, face.coordinate + face.normal * CapThickness * 0.5f, CapThickness, cap);
    }

    private static void MakeBox(Transform parent, string name, Face face, float along, float length, float y, float height, float thinCentre, float thickness, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);

        if (face.alongX)
        {
            go.transform.position = new Vector3(along, y, thinCentre);
            go.transform.localScale = new Vector3(length, height, thickness);
        }
        else
        {
            go.transform.position = new Vector3(thinCentre, y, along);
            go.transform.localScale = new Vector3(thickness, height, length);
        }

        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    // The lounge's own two panelled walls (they were made before this tool existed).
    private static void RemoveLoungePanels()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Lounge_Decor")
                continue;

            List<GameObject> panels = new List<GameObject>();
            foreach (Transform child in root.transform)
            {
                if (child.name.StartsWith("Panel_"))
                    panels.Add(child.gameObject);
            }

            foreach (GameObject panel in panels)
                Undo.DestroyObjectImmediate(panel);
        }
    }

    // ---- Small helpers ------------------------------------------------------------------------------------------------------------------------

    private static bool Inside(Config c, Vector3 p, float margin)
    {
        return p.x > c.rect.x - margin && p.x < c.rect.y + margin && p.z > c.rect.z - margin && p.z < c.rect.w + margin;
    }

    private static bool IsGlass(Renderer renderer)
    {
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material != null && material.name.ToLowerInvariant().Contains("glass"))
                return true;
        }

        return false;
    }

    private static bool IsUnder(Transform t, string ancestorName)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.name == ancestorName)
                return true;
        }

        return false;
    }

    private static Material Mat(string name, Color color, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Firewall Duty > Set Up Computer Screens
// Lays a picture on every computer and on the meeting room TV:
//   - employee cubicle computers: a sign-in screen
//   - reception computer:         a list of names (staff and visitor directory)
//   - IT room computers:          scrolling green code
//   - head manager's computer:    a file explorer window
//   - meeting room TV:            a security-briefing slide (agenda, chart, live status)
// For each device it finds the screen (a mesh with a "screen" material, or the monitor panel), works out which way it
// faces from where people sit, and puts a small world-space Canvas on it with a ComputerScreenDisplay.
// Safe to run repeatedly: a screen that already has one keeps its position and only gets its design refreshed.
public static class ComputerScreenSetupTool
{
    private const string CanvasName = "ScreenUI";
    private const float Lift = 0.003f;   // metres in front of the screen surface, so it never z-fights

    private struct Entry
    {
        public string device;     // exact name of the computer, or the start of it for the TV
        public bool startsWith;
        public ComputerScreenDisplay.Design design;
        public Vector3 viewerHint; // where to look from when no chair is near (used for the TV)

        public Entry(string device, bool startsWith, ComputerScreenDisplay.Design design, Vector3 viewerHint)
        {
            this.device = device;
            this.startsWith = startsWith;
            this.design = design;
            this.viewerHint = viewerHint;
        }
    }

    // Named devices. The computers are not listed: each one gets its design from the room it stands in (see DesignForPosition),
    // so swapping a computer for another model, or adding one, needs no change here.
    private static readonly Entry[] Entries =
    {
        // Meeting room TV
        new Entry("TV LED", true, ComputerScreenDisplay.Design.Meeting, new Vector3(-9.3f, 1.2f, 8.3f))
    };

    private struct Quad
    {
        public Vector3 center;   // the middle of the screen surface, in the world
        public Vector3 normal;   // the way the screen faces (toward the viewer)
        public Vector3 up;
        public float width;      // metres
        public float height;
        public string how;
    }

    [MenuItem("Tools/Firewall Duty/Set Up Computer Screens")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();
        List<Transform> chairs = NpcCubicleSetupTool.FindByName(lower => lower.Contains("chair"));
        int done = 0;

        foreach (Entry entry in Entries)
        {
            Transform device = FindDevice(entry);
            if (device == null)
            {
                report.AppendLine("[" + entry.device + "] not found in the open scene, skipped.");
                continue;
            }

            report.AppendLine("[" + device.name + "] " + SetUpDevice(device, entry, chairs));
            done++;
        }

        // Every computer in the scene, whatever it is called now.
        foreach (Transform computer in NpcCubicleSetupTool.FindByName(NpcCubicleSetupTool.IsComputer))
        {
            if (computer.GetComponentInParent<ComputerScreenDisplay>() != null)
                continue;

            bool known;
            ComputerScreenDisplay.Design design = DesignForPosition(computer.position, out known);
            if (!known)
            {
                report.AppendLine("[" + computer.name + "] at " + computer.position.ToString("F1") + " is not in a room I know, skipped.");
                continue;
            }

            Entry entry = new Entry(computer.name, false, design, Vector3.zero);
            report.AppendLine("[" + computer.name + "] " + SetUpDevice(computer, entry, chairs));
            done++;
        }

        if (done > 0)
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S) and press Play, then look at the screens. The pictures are drawn when Play starts. " +
                          "If one is not on its screen exactly, select its 'ScreenUI' child and move or resize it.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // Which screen a computer shows depends on the room it is in (x and z are world coordinates, as laid out in your scene).
    private static ComputerScreenDisplay.Design DesignForPosition(Vector3 p, out bool known)
    {
        known = true;

        // The bank: the counter computers (west side) show the tellers' customer files, the manager's computer the executive files.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Bank_scene")
        {
            if (p.x < 4f)
                return ComputerScreenDisplay.Design.BankFiles;

            if (p.x > 12f && p.z > 8f)
                return ComputerScreenDisplay.Design.Files;

            known = false;
            return ComputerScreenDisplay.Design.Login;
        }

        if (p.x < -5f && p.z > -13f && p.z < 1.5f)
            return ComputerScreenDisplay.Design.Login;      // employee cubicles

        if (p.x > 6.5f && p.z < -4f)
            return ComputerScreenDisplay.Design.Code;       // IT room

        if (p.x > 7f && p.z > 3.5f && p.z < 12.5f)
            return ComputerScreenDisplay.Design.Files;      // head manager's office

        if (p.x > -4f && p.x < 4f && p.z > -8f && p.z < -2f)
            return ComputerScreenDisplay.Design.NameList;   // reception

        known = false;
        return ComputerScreenDisplay.Design.Login;
    }

    private static Transform FindDevice(Entry entry)
    {
        // Several objects can share a name (a model has a child mesh named like the model itself). Take the outermost.
        Transform best = null;
        int bestDepth = int.MaxValue;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            bool match = entry.startsWith ? t.name.StartsWith(entry.device) : t.name == entry.device;

            // The real device is the one that has a mesh somewhere in it (not an empty parent of the same name).
            if (!match || t.GetComponentInChildren<MeshRenderer>() == null || t.GetComponentInParent<ComputerScreenDisplay>() != null
                || t.GetComponent<Canvas>() != null)
                continue;

            int depth = 0;
            for (Transform p = t.parent; p != null; p = p.parent)
                depth++;

            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = t;
            }
        }

        return best;
    }

    private static string SetUpDevice(Transform device, Entry entry, List<Transform> chairs)
    {
        // Where people look at it from: the nearest chair, else the hint.
        Vector3 viewer;
        Transform chair = NpcCubicleSetupTool.Nearest(chairs, device.position, 3f);

        if (chair != null)
        {
            Bounds cb = NpcCubicleSetupTool.GetBounds(chair);
            viewer = cb.size.sqrMagnitude > 0f ? cb.center : chair.position;
        }
        else if (entry.viewerHint != Vector3.zero)
        {
            viewer = entry.viewerHint;
        }
        else
        {
            return "SKIPPED: no chair within 3 m to tell which way the screen faces.";
        }

        // Already has a screen: only refresh the design.
        Transform existing = device.Find(CanvasName);
        if (existing != null)
        {
            ComputerScreenDisplay display = existing.GetComponent<ComputerScreenDisplay>();
            if (display == null)
                display = Undo.AddComponent<ComputerScreenDisplay>(existing.gameObject);

            SerializedObject existingSo = new SerializedObject(display);
            existingSo.FindProperty("design").enumValueIndex = (int)entry.design;
            existingSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(display);
            return "kept the existing screen where it is; design set to " + entry.design;
        }

        if (!TryFindScreen(device, viewer, out Quad quad))
            return "SKIPPED: could not find the screen surface on this device.";

        // Canvas size in pixels follows the screen's shape (the TV is wider).
        float aspect = Mathf.Clamp(quad.height / quad.width, 0.4f, 1.0f);
        float pixelWidth = entry.design == ComputerScreenDisplay.Design.Meeting ? 1000f : 800f;
        Vector2 pixelSize = new Vector2(pixelWidth, Mathf.Round(pixelWidth * aspect));

        GameObject go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(ComputerScreenDisplay));
        Undo.RegisterCreatedObjectUndo(go, "Create Screen");
        go.transform.SetParent(device, true);
        go.transform.position = quad.center + quad.normal * Lift;
        go.transform.rotation = Quaternion.LookRotation(-quad.normal, quad.up);   // a canvas reads correctly with its forward pointing away from the viewer

        // World size of one canvas pixel, whatever the parent's scale.
        float metresPerPixel = quad.width / pixelSize.x;
        Vector3 parentScale = device.lossyScale;
        go.transform.localScale = new Vector3(metresPerPixel / Mathf.Max(parentScale.x, 0.0001f),
                                              metresPerPixel / Mathf.Max(parentScale.y, 0.0001f),
                                              metresPerPixel / Mathf.Max(parentScale.z, 0.0001f));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = pixelSize;

        SerializedObject so = new SerializedObject(go.GetComponent<ComputerScreenDisplay>());
        so.FindProperty("design").enumValueIndex = (int)entry.design;
        so.FindProperty("pixelSize").vector2Value = pixelSize;
        so.ApplyModifiedProperties();

        return entry.design + " screen, " + quad.width.ToString("F2") + " x " + quad.height.ToString("F2") + " m (" + quad.how + "), facing " +
               quad.normal.ToString("F2");
    }

    // ---- Finding the screen ----------------------------------------------------------------------------------

    private static bool TryFindScreen(Transform device, Vector3 viewer, out Quad quad)
    {
        // 1. A mesh part that uses a screen material: "screen" (the laptops), or the monitor panel of the PCs.
        foreach (MeshRenderer r in device.GetComponentsInChildren<MeshRenderer>())
        {
            MeshFilter filter = r.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;

            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length && i < filter.sharedMesh.subMeshCount; i++)
            {
                string materialName = materials[i] != null ? materials[i].name.ToLowerInvariant() : "";
                bool isScreen = materialName.Contains("screen")
                                || (materialName.Contains("metallicblack") && r.name.ToLowerInvariant().Contains("monitor"));

                if (isScreen && SubmeshQuad(r, filter.sharedMesh, i, viewer, out quad))
                    return true;
            }
        }

        // 2. No separate screen part (the TV is one mesh): the front face of the whole thing, trimmed to the panel.
        return BoundsQuad(device, viewer, out quad);
    }

    private static bool SubmeshQuad(MeshRenderer r, Mesh mesh, int submesh, Vector3 viewer, out Quad quad)
    {
        quad = default;

        int[] indices = mesh.GetIndices(submesh);
        if (indices.Length == 0)
            return false;

        Vector3[] vertices = mesh.vertices;
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        foreach (int index in indices)
        {
            min = Vector3.Min(min, vertices[index]);
            max = Vector3.Max(max, vertices[index]);
        }

        Vector3 extent = max - min;

        // The thinnest direction of the part is its thickness: the screen faces along it.
        int thin = extent.x <= extent.y && extent.x <= extent.z ? 0 : extent.y <= extent.z ? 1 : 2;
        int a = (thin + 1) % 3;
        int b = (thin + 2) % 3;

        Transform t = r.transform;
        int heightAxis = Mathf.Abs(t.TransformDirection(Axis(a)).y) >= Mathf.Abs(t.TransformDirection(Axis(b)).y) ? a : b;
        int widthAxis = heightAxis == a ? b : a;

        Vector3 centerLocal = (min + max) * 0.5f;
        Vector3 normalLocal = Axis(thin);
        Vector3 normalWorld = t.TransformDirection(normalLocal).normalized;

        // Face toward the viewer.
        if (Vector3.Dot(normalWorld, viewer - t.TransformPoint(centerLocal)) < 0f)
        {
            normalLocal = -normalLocal;
            normalWorld = -normalWorld;
        }

        Vector3 faceLocal = centerLocal + normalLocal * (extent[thin] * 0.5f);
        Vector3 scale = t.lossyScale;

        float width = extent[widthAxis] * Mathf.Abs(scale[widthAxis]);
        float height = extent[heightAxis] * Mathf.Abs(scale[heightAxis]);

        // A thick part is the whole monitor casing, so leave room for the bezel; a thin one is just the glass.
        bool casing = extent[thin] > 0.15f * Mathf.Min(extent[widthAxis], extent[heightAxis]);
        width *= casing ? 0.9f : 0.97f;
        height *= casing ? 0.82f : 0.97f;

        Vector3 up = t.TransformDirection(Axis(heightAxis));
        if (up.y < 0f)
            up = -up;

        quad = new Quad
        {
            center = t.TransformPoint(faceLocal) + (casing ? up.normalized * (height * 0.03f) : Vector3.zero),
            normal = normalWorld,
            up = up.normalized,
            width = width,
            height = height,
            how = "from the '" + r.name + "' screen part"
        };

        return width > 0.02f && height > 0.02f;
    }

    private static bool BoundsQuad(Transform device, Vector3 viewer, out Quad quad)
    {
        quad = default;
        Bounds b = NpcCubicleSetupTool.GetBounds(device);
        if (b.size.sqrMagnitude <= 0f)
            return false;

        // Facing along whichever horizontal direction the object is thinnest in, toward the viewer.
        bool alongX = b.size.x <= b.size.z;
        Vector3 normal = alongX ? Vector3.right : Vector3.forward;
        Vector3 toViewer = viewer - b.center;
        if (Vector3.Dot(normal, toViewer) < 0f)
            normal = -normal;

        float thickness = alongX ? b.size.x : b.size.z;
        float width = alongX ? b.size.z : b.size.x;
        float height = b.size.y;

        // Trim to the glass: the bezel at the sides and the stand underneath.
        quad = new Quad
        {
            center = b.center + normal * (thickness * 0.5f) + Vector3.up * (height * 0.06f),
            normal = normal,
            up = Vector3.up,
            width = width * 0.9f,
            height = height * 0.78f,
            how = "estimated from the object's outline"
        };

        return true;
    }

    private static Vector3 Axis(int i)
    {
        return i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    }
}

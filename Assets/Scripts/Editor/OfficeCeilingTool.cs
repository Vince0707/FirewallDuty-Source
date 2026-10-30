using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Office > Raise Ceiling   (run it with Company_scene open)
// Makes the office ceiling higher by RaiseBy metres and changes nothing else:
//   - the ceiling slab goes up;
//   - every wall and glass partition whose top reached the old ceiling (3 m) is made taller, from where it stands up to the new ceiling (the bottom stays
//     on the floor, the width and position stay as they were), and so are the thin paint panels of the kitchen wall;
//   - the ceiling fans (and their lamps and lights, which hang on them) go up with the ceiling.
// Furniture, people, doors, windows and the street are not touched. The Undo history (Ctrl+Z) takes it all back. It does nothing a second time: the ceiling
// height is checked first.
public static class OfficeCeilingTool
{
    private const float RaiseBy = 1.0f;       // a metre higher: the ceiling goes from 3.05 m to 4.05 m
    private const float OldWallTop = 3.0f;    // where the walls ended
    private const float Tolerance = 0.06f;

    [MenuItem("Tools/Firewall Duty/Office/Raise Ceiling")]
    public static void Raise()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Raise Ceiling", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        Transform ceiling = FindRoot("Ceiling");
        if (ceiling == null)
        {
            UnityEngine.Debug.LogError("Raise Ceiling: the object 'Ceiling' was not found, so nothing was changed.");
            return;
        }

        if (ceiling.position.y > 3.5f)
        {
            UnityEngine.Debug.Log("Raise Ceiling: the ceiling is already higher (at " + ceiling.position.y.ToString("F2") + " m), so nothing was changed.");
            return;
        }

        StringBuilder report = new StringBuilder();
        int walls = 0, fans = 0, lights = 0;

        // 1. The walls and partitions (and the kitchen's paint panels): taller, bottom unchanged.
        List<MeshFilter> candidates = new List<MeshFilter>();
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (IsWallPiece(filter))
                candidates.Add(filter);
        }

        foreach (MeshFilter filter in candidates)
        {
            Transform t = filter.transform;
            Bounds b = filter.GetComponent<Renderer>().bounds;
            float height = b.size.y;

            Undo.RecordObject(t, "Raise Ceiling");
            Vector3 scale = t.localScale;
            scale.y *= (height + RaiseBy) / height;
            t.localScale = scale;
            t.position += Vector3.up * (RaiseBy * 0.5f);
            EditorUtility.SetDirty(t);

            walls++;
            report.AppendLine("  wall: " + t.name + " (top " + b.max.y.ToString("F2") + " -> " + (b.max.y + RaiseBy).ToString("F2") + ")");
        }

        // 2. The ceiling itself.
        Undo.RecordObject(ceiling, "Raise Ceiling");
        ceiling.position += Vector3.up * RaiseBy;
        EditorUtility.SetDirty(ceiling);

        // 3. The ceiling fans (the lamps and lights under them are their children, so they follow).
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.parent == null && t.name.StartsWith("(Prb)CeilingFan") && InsideOffice(t.position))
            {
                Undo.RecordObject(t, "Raise Ceiling");
                t.position += Vector3.up * RaiseBy;
                EditorUtility.SetDirty(t);
                fans++;
            }
        }

        // 4. Any other lamp up near the ceiling that is not on a fan.
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional || light.transform.position.y < 2.5f || !InsideOffice(light.transform.position) || IsUnderFan(light.transform))
                continue;

            Undo.RecordObject(light.transform, "Raise Ceiling");
            light.transform.position += Vector3.up * RaiseBy;
            EditorUtility.SetDirty(light.transform);
            lights++;
            report.AppendLine("  lamp: " + light.name);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        UnityEngine.Debug.Log("Raise Ceiling: the ceiling is now " + (RaiseBy + 3.05f).ToString("F2") + " m high (it was 3.05 m). " + walls + " wall piece(s) made taller, " +
                              fans + " ceiling fan(s) and " + lights + " other lamp(s) raised. Nothing else was moved.\n" + report +
                              "Save the scene (Ctrl+S). Use Edit > Undo to take it back.");
    }

    // ---- Step 2: the gaps over the doors, and a taller entrance door -----------------------------------------------------------

    private const float NewWallTop = 4.0f;       // where the walls end now
    private const float NewDoorHeight = 3.15f;   // the main entrance door (it was 2.15 m, then 2.9 m, then 3.3 m which was too high); the floor is at 0.05, so its top is at 3.2

    // Tools > Firewall Duty > Office > Fill Doorways And Raise Entrance Door   (run Raise Ceiling first)
    //   - above each interior door frame (IT Tech Room, Head Manager, Meeting Room) the wall now stops where the frame stops, leaving a gap up to the new
    //     ceiling: a wall piece fills it, made like the wall next to it;
    //   - the main entrance door and the wall piece over it are made higher (door 2.9 m), the sign on it moves up, and the glass trim is made again.
    [MenuItem("Tools/Firewall Duty/Office/Fill Doorways And Raise Entrance Door")]
    public static void FinishCeiling()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Fill Doorways", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        Transform ceiling = FindRoot("Ceiling");
        if (ceiling == null || ceiling.position.y < 3.5f)
        {
            UnityEngine.Debug.LogWarning("Fill Doorways: run Tools > Firewall Duty > Office > Raise Ceiling first, so the walls and the ceiling are the new height. Nothing was changed.");
            return;
        }

        StringBuilder report = new StringBuilder();
        int fills = FillDoorways(report);
        string door = RaiseEntranceDoor() + " " + AlignEntranceTitle();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Fill Doorways: " + fills + " doorway(s) filled up to the ceiling.\n" + report + door + "\nSave the scene (Ctrl+S). Use Edit > Undo to take it back.");
    }

    // Tools > Firewall Duty > Office > Move Entrance Title Onto Its Board
    // The "FIREWALL DUTY / CYBERSECURITY IS OUR DUTY." writing belongs on the dark board over the entrance ("Entrance_Sign"). When the door was raised the
    // board moved up with the wall piece but the writing stayed where it was, over the glass. This puts it back on the board, in front of it.
    [MenuItem("Tools/Firewall Duty/Office/Move Entrance Title Onto Its Board")]
    public static void MoveTitle()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Move Entrance Title", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        string result = AlignEntranceTitle();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log(result + " Save the scene (Ctrl+S). Use Edit > Undo to take it back.");
    }

    private static string AlignEntranceTitle()
    {
        Transform board = FindRoot("Entrance_Sign");
        Transform text = FindRoot("Entrance_Sign_Text");
        if (board == null || text == null)
            return "Entrance title: 'Entrance_Sign' or 'Entrance_Sign_Text' was not found, so it was not moved.";

        // The writing sits in the middle of the board, a hair in front of it (on the office side, +z).
        Vector3 target = new Vector3(board.position.x, board.position.y, board.position.z + board.localScale.z * 0.5f + 0.005f);

        if ((text.position - target).sqrMagnitude < 0.0001f)
            return "Entrance title: it is already on its board.";

        Undo.RecordObject(text, "Move Entrance Title");
        text.position = target;
        EditorUtility.SetDirty(text);
        return "Entrance title: moved onto the board over the door (height " + target.y.ToString("F2") + " m).";
    }

    private static int FillDoorways(StringBuilder report)
    {
        // Fills made by an earlier run are replaced.
        List<GameObject> old = new List<GameObject>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.StartsWith("Doorway_Fill_"))
                old.Add(root);
        }

        foreach (GameObject o in old)
            Undo.DestroyObjectImmediate(o);

        // The solid walls (not the glass partitions) that now reach the new ceiling.
        List<Renderer> walls = new List<Renderer>();
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null || filter.sharedMesh == null || filter.sharedMesh.name != "Cube")
                continue;

            Transform t = filter.transform;
            if (t.name == "Ceiling" || t.name == "Floor" || IsUnder(t, "Outside_Ground") || Vector3.Dot(t.up, Vector3.up) < 0.999f)
                continue;

            Material material = renderer.sharedMaterial;
            if (material == null || material.name.ToLowerInvariant().Contains("glass"))
                continue;

            Bounds b = renderer.bounds;
            if (Mathf.Abs(b.max.y - NewWallTop) <= Tolerance && b.min.y < 2.4f && b.size.y > 0.5f && Mathf.Min(b.size.x, b.size.z) < 0.3f && InsideOffice(b.center))
                walls.Add(renderer);
        }

        int made = 0;

        foreach (Transform door in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (door.parent != null || !door.name.StartsWith("(Prb)SeparatorDoor") || !InsideOffice(door.position))
                continue;

            Renderer[] parts = door.GetComponentsInChildren<Renderer>();
            if (parts.Length == 0)
                continue;

            Bounds frame = parts[0].bounds;
            foreach (Renderer part in parts)
                frame.Encapsulate(part.bounds);

            bool alongX = frame.size.x >= frame.size.z;   // the wall runs along X (the door is wide in X) or along Z

            // The nearest wall that runs the same way.
            Renderer best = null;
            float bestDistance = float.MaxValue;
            foreach (Renderer wall in walls)
            {
                bool wallAlongX = wall.bounds.size.x >= wall.bounds.size.z;
                float distance = wall.bounds.SqrDistance(frame.center);
                if (wallAlongX == alongX && distance < bestDistance)
                {
                    best = wall;
                    bestDistance = distance;
                }
            }

            if (best == null || bestDistance > 4f)
            {
                report.AppendLine("  no wall found next to " + door.name + ", so it was left as it is.");
                continue;
            }

            float bottom = frame.max.y - 0.02f;
            float top = best.bounds.max.y;
            if (top - bottom < 0.05f)
                continue;

            float thickness = (alongX ? best.bounds.size.z : best.bounds.size.x) * 0.98f;
            float length = (alongX ? frame.size.x : frame.size.z) + 0.12f;   // a little into the walls on both sides, where it is hidden
            float height = top - bottom;

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(fill, "Fill Doorway");
            fill.name = "Doorway_Fill_" + (made + 1);
            fill.GetComponent<Renderer>().sharedMaterial = best.sharedMaterial;

            Vector3 centre = alongX
                ? new Vector3(frame.center.x, bottom + height * 0.5f, best.bounds.center.z)
                : new Vector3(best.bounds.center.x, bottom + height * 0.5f, frame.center.z);
            fill.transform.position = centre;
            fill.transform.localScale = alongX ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length);

            made++;
            report.AppendLine("  filled over " + door.name + ": " + length.ToString("F2") + " m wide, from " + bottom.ToString("F2") + " m to " + top.ToString("F2") + " m, like " + best.name);
        }

        return made;
    }

    private static string RaiseEntranceDoor()
    {
        Transform lintel = FindRoot("Wall_front_top");
        Transform doorGroup = FindRoot("Entrance_Door");
        if (lintel == null || doorGroup == null)
            return "Entrance door: 'Wall_front_top' or 'Entrance_Door' was not found, so the entrance was not changed.";

        float lintelBottom = 0.05f + NewDoorHeight;
        float lintelHeight = NewWallTop - lintelBottom;

        if (Mathf.Abs(lintel.localScale.y - lintelHeight) < 0.01f)
            return "Entrance door: it is already " + NewDoorHeight.ToString("F2") + " m high.";

        // The wall piece over the door: from the top of the new door up to the top of the walls.
        Undo.RecordObject(lintel, "Raise Entrance Door");
        Vector3 scale = lintel.localScale;
        scale.y = lintelHeight;
        lintel.localScale = scale;
        Vector3 position = lintel.position;
        position.y = lintelBottom + lintelHeight * 0.5f;
        lintel.position = position;

        // The two glass panels.
        int panels = 0;
        foreach (Transform panel in doorGroup)
        {
            if (panel.GetComponent<MeshRenderer>() == null || panel.name == "Glass_Track")
                continue;

            Undo.RecordObject(panel, "Raise Entrance Door");
            Vector3 panelScale = panel.localScale;
            panelScale.y = NewDoorHeight;
            panel.localScale = panelScale;
            Vector3 panelPosition = panel.localPosition;
            panelPosition.y = NewDoorHeight * 0.5f;
            panel.localPosition = panelPosition;
            panels++;
        }

        // The sign that hung on the wall piece goes to its middle.
        Transform sign = FindRoot("Entrance_Sign");
        if (sign != null)
        {
            Undo.RecordObject(sign, "Raise Entrance Door");
            Vector3 signPosition = sign.position;
            signPosition.y = lintelBottom + lintelHeight * 0.5f;
            sign.position = signPosition;
        }

        // The frame and handle-less trim are measured from the panel size, so they are made again.
        string trim = OfficeStreetSetupTool.DressSlidingDoor();

        return "Entrance door: " + panels + " panel(s) now " + NewDoorHeight.ToString("F2") + " m high (was 2.15 m), the wall piece over it and the sign moved up. " + trim;
    }

    // A flat box (a cube) standing on the floor or hanging in a doorway, whose top is at the old ceiling height, inside the office.
    private static bool IsWallPiece(MeshFilter filter)
    {
        if (filter.sharedMesh == null || filter.sharedMesh.name != "Cube")
            return false;

        Renderer renderer = filter.GetComponent<Renderer>();
        if (renderer == null)
            return false;

        Transform t = filter.transform;
        if (t.name == "Ceiling" || t.name == "Floor" || IsUnder(t, "Outside_Ground"))
            return false;

        // Its up must be the world's up (turning around the vertical axis is fine), or the height is not its Y scale.
        if (Vector3.Dot(t.up, Vector3.up) < 0.999f)
            return false;

        Bounds b = renderer.bounds;
        return Mathf.Abs(b.max.y - OldWallTop) <= Tolerance && b.min.y < 2.4f && b.size.y > 0.5f && InsideOffice(b.center);
    }

    private static bool InsideOffice(Vector3 p)
    {
        return p.x > -15.3f && p.x < 15.3f && p.z > -12.3f && p.z < 12.3f;
    }

    private static bool IsUnderFan(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p.name.StartsWith("(Prb)CeilingFan"))
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

    private static Transform FindRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root.transform;
        }

        return null;
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Bank > Lower Teller Counters   (run it with Bank_scene open)
// The teller counters were too high (the desk top was 1.6 m above the floor, above the customers' shoulders). They are lowered by LowerBy metres, so the
// desk top is about 1.1 m above the floor, a normal bank counter:
//   - the counter slabs (the teller's desk and the customers' ledge) and the dark front panel go down; the floor, the dividers between the counters and
//     the board with the counter numbers stay where they are;
//   - everything that stands on the desk (the computers, lamps, plants, printers, phones, pen holders, staplers, the cash machines ...) goes down with it,
//     and the few boxes that stood on the drawers under the desk are put on the desk instead;
//   - the hand marks where the tellers rest their hands move with the desk.
// Nothing is deleted. It does nothing a second time (the desk height is checked first). Ctrl+Z takes it all back.
public static class BankCounterTool
{
    private const float LowerBy = 0.5f;
    private const float OldDeskTop = 1.85f;

    [MenuItem("Tools/Firewall Duty/Bank/Lower Teller Counters")]
    public static void Lower()
    {
        if (SceneManager.GetActiveScene().name != "Bank_scene")
        {
            EditorUtility.DisplayDialog("Lower Teller Counters", "Open Assets/Scenes/Bank_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        Transform front = FindRoot("wall_right (2)");   // the dark front panel (taller than the desk)
        Transform desk = FindRoot("wall_right (5)");    // the teller's desk slab
        Transform ledge = FindRoot("wall_right (6)");   // the customers' ledge slab

        if (front == null || desk == null || ledge == null)
        {
            UnityEngine.Debug.LogError("Lower Teller Counters: the counter pieces 'wall_right (2)', '(5)' and '(6)' were not found, so nothing was changed.");
            return;
        }

        if (desk.position.y < OldDeskTop - 0.4f)
        {
            UnityEngine.Debug.Log("Lower Teller Counters: the counters are already lower (the desk slab is at " + desk.position.y.ToString("F2") + " m), so nothing was changed.");
            return;
        }

        StringBuilder report = new StringBuilder();

        // 1. The counter itself.
        Undo.RecordObject(desk, "Lower Teller Counters");
        desk.position += Vector3.down * LowerBy;

        Undo.RecordObject(ledge, "Lower Teller Counters");
        ledge.position += Vector3.down * LowerBy;

        // The front panel keeps its bottom on the floor and its top comes down: it gets shorter by LowerBy, so its middle comes down by half of that.
        Undo.RecordObject(front, "Lower Teller Counters");
        Vector3 scale = front.localScale;
        scale.y -= LowerBy;
        front.localScale = scale;
        front.position += Vector3.down * (LowerBy * 0.5f);

        // 2. What stands on the desk.
        int moved = 0;
        int boxes = 0;

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform t = root.transform;
            Vector3 p = t.position;

            // On the desk: the teller side of the counters (x from 1.5 to 3.3), at the old desk height.
            if (p.x > 1.5f && p.x < 3.3f && p.y > OldDeskTop - 0.02f && p.y < OldDeskTop + 0.2f)
            {
                Undo.RecordObject(t, "Lower Teller Counters");
                t.position = new Vector3(p.x, p.y - LowerBy, p.z);
                moved++;
            }
            // The boxes that stood on the drawers under the desk would now be inside it: they go on the desk.
            else if (p.x > 1.5f && p.x < 2.6f && p.y > 1.15f && p.y < 1.3f && root.name.ToLowerInvariant().Contains("paperbox"))
            {
                Undo.RecordObject(t, "Lower Teller Counters");
                t.position = new Vector3(p.x, OldDeskTop - LowerBy, p.z);
                boxes++;
            }
        }

        // 3. The tellers' hand marks (children of the seat marks).
        int hands = 0;
        GameObject seats = GameObject.Find("NPC_Seats");
        if (seats != null)
        {
            foreach (Transform hand in seats.GetComponentsInChildren<Transform>(true))
            {
                string n = hand.name;
                bool tellerHand = (n.StartsWith("mesh_office_f_recepcionist01_Seat") || n.StartsWith("mesh_office_f_secretary01_Seat")) && (n.EndsWith("_RightHand") || n.EndsWith("_LeftHand"));

                if (tellerHand && hand.localPosition.y > 1.3f)
                {
                    Undo.RecordObject(hand, "Lower Teller Counters");
                    hand.localPosition += Vector3.down * LowerBy;
                    hands++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Lower Teller Counters: the counters are " + LowerBy.ToString("F1") + " m lower (the desk top is now " + (OldDeskTop - LowerBy).ToString("F2") + " m). " +
                              moved + " item(s) on the desks went down with them, " + boxes + " box(es) were put on the desk, " + hands + " teller hand mark(s) were lowered.\n" +
                              "Save the scene (Ctrl+S). Use Edit > Undo to take it back.");
    }

    // Tools > Firewall Duty > Bank > Lower Teller Chairs   (run it with Bank_scene open, after Lower Teller Counters)
    // The tall teller chairs were made to fit the old, high counters. Each chair is made lower by the same amount as the counters (LowerBy): its pole is
    // shorter, and the seat, the foot ring and the back come down with it. The tellers sitting on them, and the chair you sit on at the middle counter, are
    // fitted to the new seat height, so nobody floats or sinks. Nothing is deleted; Ctrl+Z takes it back. It does nothing a second time.
    [MenuItem("Tools/Firewall Duty/Bank/Lower Teller Chairs")]
    public static void LowerChairs()
    {
        if (SceneManager.GetActiveScene().name != "Bank_scene")
        {
            EditorUtility.DisplayDialog("Lower Teller Chairs", "Open Assets/Scenes/Bank_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        List<Transform> chairs = new List<Transform>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Bank_TellerChair")
                chairs.Add(root.transform);
        }

        if (chairs.Count == 0)
        {
            UnityEngine.Debug.LogWarning("Lower Teller Chairs: no 'Bank_TellerChair' was found, so nothing was changed.");
            return;
        }

        int lowered = 0;
        int seats = 0;
        int tellers = 0;

        foreach (Transform chair in chairs)
        {
            Transform marker = chair.Find("SeatTop");
            if (marker == null)
                continue;

            float oldTop = marker.localPosition.y;
            if (oldTop < 1.0f)
                continue;   // already lowered (the old chairs had their seat about 1.3 m above the floor)

            float seatTop = Mathf.Max(0.55f, oldTop - LowerBy);

            // The same measurements BankSceneSetupTool used when it built the chair, with the new seat height.
            float footLevel = Mathf.Max(0.2f, seatTop - 0.45f);
            float poleLength = seatTop - 0.1f - 0.03f;

            Set(chair, "Pole", new Vector3(0f, 0.03f + poleLength * 0.5f, 0f), new Vector3(0.08f, poleLength * 0.5f, 0.08f));
            Set(chair, "FootRing", new Vector3(0f, footLevel, 0f), null);
            Set(chair, "Seat", new Vector3(0f, seatTop - 0.05f, 0f), null);
            Set(chair, "Back", new Vector3(0f, seatTop + 0.27f, -0.19f), null);

            Undo.RecordObject(marker, "Lower Teller Chairs");
            marker.localPosition = new Vector3(marker.localPosition.x, seatTop, marker.localPosition.z);

            // The chair you can sit on (the middle counter): the seat point comes down too.
            SeatInteractable seat = chair.GetComponent<SeatInteractable>();
            if (seat != null)
            {
                SerializedObject seatObject = new SerializedObject(seat);
                SerializedProperty local = seatObject.FindProperty("seatLocalPosition");
                Vector3 p = local.vector3Value;
                local.vector3Value = new Vector3(p.x, seatTop, p.z);
                seatObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(seat);
                seats++;
            }

            lowered++;
        }

        // The tellers on these chairs: their hips are fitted to the new seat height.
        if (lowered > 0)
        {
            foreach (NpcWorker worker in Object.FindObjectsByType<NpcWorker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject workerObject = new SerializedObject(worker);
                Transform seatMark = workerObject.FindProperty("seat").objectReferenceValue as Transform;
                if (seatMark == null)
                    continue;

                Transform chair = null;
                float best = 0.6f;
                foreach (Transform candidate in chairs)
                {
                    Vector3 d = candidate.position - seatMark.position;
                    d.y = 0f;
                    if (d.magnitude < best)
                    {
                        best = d.magnitude;
                        chair = candidate;
                    }
                }

                if (chair == null)
                    continue;

                workerObject.FindProperty("hipTarget").vector3Value = NpcCubicleSetupTool.ComputeHipTarget(chair, seatMark, new List<string>());
                workerObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(worker);
                tellers++;
            }
        }

        if (lowered == 0)
        {
            UnityEngine.Debug.Log("Lower Teller Chairs: the chairs are already lower, so nothing was changed.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Lower Teller Chairs: " + lowered + " chair(s) are " + LowerBy.ToString("F1") + " m lower; " + tellers + " teller(s) were fitted to their new seat height" +
                              (seats > 0 ? " and your seat was lowered too" : "") + ". Save the scene (Ctrl+S). Use Edit > Undo to take it back.");
    }

    // Tools > Firewall Duty > Bank > Add Teller Glass   (run it with Bank_scene open, after Lower Teller Counters)
    // Glass windows over the teller counters, between the top of the counter's front panel and the board with the counter numbers, like in a real bank:
    //   - each counter has a pane on each side and a small pane over a serving opening, level with the teller's chair, with brass edges around the opening;
    //   - a dark sill sits on the counter under each pane;
    //   - the panes have no colliders, so the crosshair and the hand button still reach the teller behind the glass (the counter itself keeps people out).
    // The glass is the same material as the other glass in the bank. Everything is in one group, "Teller_Glass". Run it again to rebuild the group (for example
    // after changing the counter height); Ctrl+Z removes it.
    [MenuItem("Tools/Firewall Duty/Bank/Add Teller Glass")]
    public static void AddGlass()
    {
        if (SceneManager.GetActiveScene().name != "Bank_scene")
        {
            EditorUtility.DisplayDialog("Add Teller Glass", "Open Assets/Scenes/Bank_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        Transform front = FindRoot("wall_right (2)");     // the dark front panel of the counters
        Transform board = FindRoot("wall_right (1)");     // the board with the counter numbers, above the windows
        Transform south = FindRoot("wall_left (2)");      // the south wall
        Transform north = FindRoot("wall_left (3)");      // the partition at the north end of the counters
        Transform dividerA = FindRoot("wall_right (3)");  // the walls between the counters
        Transform dividerB = FindRoot("wall_right (4)");

        if (front == null || board == null || south == null || north == null || dividerA == null || dividerB == null)
        {
            UnityEngine.Debug.LogError("Add Teller Glass: one of the counter walls ('wall_right (1)' to '(4)', 'wall_left (2)', 'wall_left (3)') was not found, so nothing was changed.");
            return;
        }

        Bounds frontBounds = BoundsOf(front);
        Bounds boardBounds = BoundsOf(board);
        float bottom = frontBounds.max.y;
        float top = boardBounds.min.y;
        float x = frontBounds.center.x;

        if (top - bottom < 0.5f)
        {
            UnityEngine.Debug.LogWarning("Add Teller Glass: the gap between the counter (" + bottom.ToString("F2") + " m) and the number board (" + top.ToString("F2") + " m) is too small, so nothing was changed.");
            return;
        }

        // The three stretches of counter, between the south wall, the two dividers and the north partition.
        Bounds a = BoundsOf(dividerA);
        Bounds b = BoundsOf(dividerB);
        float firstDivider = Mathf.Min(a.center.z, b.center.z) == a.center.z ? 0f : 1f;
        Bounds low = firstDivider == 0f ? a : b;
        Bounds high = firstDivider == 0f ? b : a;

        float[] starts = { BoundsOf(south).max.z, low.max.z, high.max.z };
        float[] ends = { low.min.z, high.min.z, BoundsOf(north).min.z };

        // The tellers' chairs, south to north: the serving opening of each counter is level with its teller.
        List<float> chairZ = new List<float>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Bank_TellerChair" && root.transform.position.x < 3f)
                chairZ.Add(root.transform.position.z);
        }

        chairZ.Sort();

        foreach (GameObject old in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (old.name == "Teller_Glass")
            {
                Undo.DestroyObjectImmediate(old);
                break;
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials/Lounge"))
            AssetDatabase.CreateFolder("Assets/Materials", "Lounge");

        Material glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Glass_material.mat");
        OfficeLoungeTool.Palette p = new OfficeLoungeTool.Palette();

        GameObject rootObject = new GameObject("Teller_Glass");
        Undo.RegisterCreatedObjectUndo(rootObject, "Add Teller Glass");
        Transform parent = rootObject.transform;

        const float openingWidth = 1.3f;
        const float openingHeight = 0.7f;
        float openingTop = bottom + openingHeight;
        int counters = 0;

        for (int i = 0; i < 3; i++)
        {
            float lo = starts[i] + 0.05f;
            float hi = ends[i] - 0.05f;
            float centre = i < chairZ.Count ? chairZ[i] : (lo + hi) * 0.5f;
            centre = Mathf.Clamp(centre, lo + openingWidth * 0.5f + 0.3f, hi - openingWidth * 0.5f - 0.3f);
            float openLo = centre - openingWidth * 0.5f;
            float openHi = centre + openingWidth * 0.5f;

            // Two full panes and the small pane over the opening.
            Pane(parent, glass, x, bottom, top, lo, openLo);
            Pane(parent, glass, x, bottom, top, openHi, hi);
            Pane(parent, glass, x, openingTop, top, openLo, openHi);

            // Brass edges around the opening, and a dark sill on the counter under the panes.
            Bar(parent, p.gold, x, (bottom + openingTop) * 0.5f, openLo, 0.045f, openingHeight, 0.07f);
            Bar(parent, p.gold, x, (bottom + openingTop) * 0.5f, openHi, 0.045f, openingHeight, 0.07f);
            Bar(parent, p.gold, x, openingTop, centre, openingWidth + 0.045f, 0.045f, 0.07f);
            Bar(parent, p.dark, x, bottom + 0.02f, (lo + hi) * 0.5f, hi - lo, 0.04f, 0.14f);
            counters++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Add Teller Glass: glass windows with a serving opening over " + counters + " counter(s), from " + bottom.ToString("F2") + " m to " + top.ToString("F2") +
                              " m. Save the scene (Ctrl+S). Use Edit > Undo to take it away.");
    }

    // A glass pane in the plane of the counter front: x is the plane, z runs along the counter.
    private static void Pane(Transform parent, Material glass, float x, float y0, float y1, float z0, float z1)
    {
        if (z1 - z0 < 0.05f || y1 - y0 < 0.05f)
            return;

        GameObject pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pane.name = "Teller_Pane";
        pane.transform.SetParent(parent, false);
        pane.transform.position = new Vector3(x, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
        pane.transform.localScale = new Vector3(0.03f, y1 - y0, z1 - z0);

        if (glass != null)
            pane.GetComponent<MeshRenderer>().sharedMaterial = glass;

        // No collider: the crosshair has to reach the teller behind the glass.
        Collider collider = pane.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    // A bar across the window: "size" is the length along z and "height" its height; "depth" is how far it reaches across the window plane (x).
    private static void Bar(Transform parent, Material material, float x, float y, float z, float length, float height, float depth)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "Teller_Frame";
        bar.transform.SetParent(parent, false);
        bar.transform.position = new Vector3(x, y, z);
        bar.transform.localScale = new Vector3(depth, height, length);
        bar.GetComponent<MeshRenderer>().sharedMaterial = material;

        Collider collider = bar.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static Bounds BoundsOf(Transform t)
    {
        Renderer renderer = t.GetComponent<Renderer>();
        return renderer != null ? renderer.bounds : new Bounds(t.position, t.lossyScale);
    }

    // Moves a part of the chair to a new local position (and scale, when given).
    private static void Set(Transform chair, string part, Vector3 position, Vector3? scale)
    {
        Transform t = chair.Find(part);
        if (t == null)
            return;

        Undo.RecordObject(t, "Lower Teller Chairs");
        t.localPosition = position;
        if (scale.HasValue)
            t.localScale = scale.Value;
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

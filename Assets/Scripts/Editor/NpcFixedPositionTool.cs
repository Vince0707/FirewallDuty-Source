using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Stores a sitting position (and optionally facing) for an NPC in the scene itself.
// Used by:
//  - Tools > Firewall Duty > NPC > Apply Fixed Sitting Positions  (the positions listed below)
//  - NpcPositionKeeper, which applies positions you saved in Play Mode once Play stops.
// Each NPC then stands at exactly that position every time it sits, instead of the automatic chair fit.
// To change a position later, edit "Seat Offset" on the NPC's Npc Worker, or move its seat marker.
public static class NpcFixedPositionTool
{
    private struct Fixed
    {
        public string npc;
        public Vector3 worldPosition;

        public Fixed(string npc, Vector3 worldPosition)
        {
            this.npc = npc;
            this.worldPosition = worldPosition;
        }
    }

    // World positions of each NPC's root while seated, as read from the Transform in Play Mode.
    private static readonly Fixed[] Positions =
    {
        new Fixed("mesh_office_m_suspenders01", new Vector3(1.942f, -0.322f, 6.484f)),      // kitchen, on the beanbag
        new Fixed("mesh_office_f_recepcionist01", new Vector3(0.9363026f, 0.17f, -4.67388f)) // reception desk
    };

    [MenuItem("Tools/Firewall Duty/NPC/Apply Fixed Sitting Positions")]
    public static void Apply()
    {
        System.Text.StringBuilder report = new System.Text.StringBuilder();

        foreach (Fixed entry in Positions)
            report.AppendLine("[" + entry.npc + "] " + ApplyTo(entry.npc, entry.worldPosition, float.NaN));

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S). From now on these NPCs sit at exactly these positions every time you press Play.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // Makes the named NPC stand at 'worldPosition' (and face 'yaw' degrees, unless NaN) whenever it sits.
    internal static string ApplyTo(string npcName, Vector3 worldPosition, float yaw)
    {
        Transform npc = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name == npcName && NpcSetupTool.IsNpc(t))
            {
                npc = t;
                break;
            }
        }

        if (npc == null)
            return "SKIPPED: not found in the open scene.";

        NpcWorker worker = npc.GetComponent<NpcWorker>();
        if (worker == null)
            return "SKIPPED: has no Npc Worker. Run its setup tool first.";

        SerializedObject so = new SerializedObject(worker);
        Transform seat = so.FindProperty("seat").objectReferenceValue as Transform;
        if (seat == null)
            return "SKIPPED: Npc Worker has no Seat marker. Run its setup tool first.";

        // Facing first (the offset below is in the marker's rotated space). Children of the marker, such as the hand
        // markers, turn with it, so their positions relative to the character stay the same.
        string facing = "";
        if (!float.IsNaN(yaw) && Mathf.Abs(Mathf.DeltaAngle(seat.eulerAngles.y, yaw)) > 0.5f)
        {
            Undo.RecordObject(seat, "Set Seat Facing");
            seat.rotation = Quaternion.Euler(0f, yaw, 0f);
            EditorUtility.SetDirty(seat);
            facing = ", facing " + yaw.ToString("F0") + " degrees";
        }

        // Seat Offset is in the marker's local space, so the result is exactly the world position given, while the
        // marker's own position (and the floor height used for the feet) stay untouched.
        Vector3 localOffset = seat.InverseTransformPoint(worldPosition);

        so.FindProperty("seatOffset").vector3Value = localOffset;
        so.FindProperty("fitToSeat").boolValue = false;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        return "will sit at " + seat.TransformPoint(localOffset).ToString("F3") + facing +
               " (marker '" + seat.name + "' + offset " + localOffset.ToString("F3") + "); automatic chair fit turned off";
    }
}

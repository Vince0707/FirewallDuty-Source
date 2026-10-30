using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Firewall Duty > Set Up Sittable Chairs
// Lets the player sit on chairs with the hand button:
//   - adds PlayerSeatController to the player (sit / stand up);
//   - adds SeatInteractable to every chair that no NPC is using, with the seat in the middle of the chair and the view
//     turned toward the nearest desk (or table);
//   - gives a chair a collider if it has none, so the crosshair can point at it.
// Chairs an NPC sits on are skipped. Values you tuned on an existing SeatInteractable are kept. Safe to run repeatedly.
public static class SeatSetupTool
{
    private const float NpcSeatClearance = 0.7f;   // a chair this close to an NPC's seat counts as taken
    private const float OfficeEyeHeight = 1.12f;
    private const float LoungeEyeHeight = 1.0f;
    private const float StandardEyeHeight = 1.42f;   // the lowest seated view for any seat, in metres above the floor
    private const float EyesAboveDesk = 0.45f;       // how far above the desk or table top the eyes sit
    private const float StayBelowStanding = 0.10f;   // the seated view is always at least this much lower than standing

    [MenuItem("Tools/Firewall Duty/Set Up Sittable Chairs")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();

        PlayerMobileController player = Object.FindAnyObjectByType<PlayerMobileController>();
        if (player == null)
        {
            UnityEngine.Debug.LogError("No player (PlayerMobileController) found in the open scene.");
            return;
        }

        if (player.GetComponent<PlayerSeatController>() == null)
        {
            Undo.AddComponent<PlayerSeatController>(player.gameObject);
            report.AppendLine("Added PlayerSeatController to '" + player.name + "'.");
        }

        // Seats that NPCs already use.
        List<Vector3> npcSeats = new List<Vector3>();
        foreach (NpcWorker worker in Object.FindObjectsByType<NpcWorker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (worker.Seat != null)
                npcSeats.Add(worker.SeatPosition);
        }

        List<Transform> chairs = NpcCubicleSetupTool.FindByName(lower => lower.Contains("chair") || lower.Contains("sofa"));
        List<Transform> computers = NpcCubicleSetupTool.FindByName(NpcCubicleSetupTool.IsComputer);
        List<Transform> desks = NpcCubicleSetupTool.FindByName(NpcCubicleSetupTool.IsDesk);

        int added = 0;
        int skippedTaken = 0;
        int kept = 0;
        int colliders = 0;
        int raised = 0;
        int turned = 0;
        int unbatched = 0;

        // The seated view must stay below the standing view.
        float standingEyeHeight = 1.6f;
        Camera playerCamera = player.GetComponentInChildren<Camera>();
        if (playerCamera != null)
            standingEyeHeight = playerCamera.transform.localPosition.y;

        foreach (Transform chair in chairs)
        {
            Bounds bounds = NpcCubicleSetupTool.GetBounds(chair);
            Vector3 center = bounds.size.sqrMagnitude > 0f ? bounds.center : chair.position;

            if (IsTaken(center, npcSeats))
            {
                skippedTaken++;
                continue;
            }

            string lowerName = chair.name.ToLowerInvariant();
            bool lounge = lowerName.Contains("lounge") || lowerName.Contains("sofa");
            float eye = EyeHeightFor(center, desks, lounge, standingEyeHeight);

            SeatInteractable seat = chair.GetComponent<SeatInteractable>();
            if (seat != null)
            {
                // Only an eye height that is still one of the old defaults is replaced; a value you set by hand is kept.
                SerializedObject existing = new SerializedObject(seat);
                SerializedProperty current = existing.FindProperty("seatedEyeHeight");

                // The tool records the value it wrote. A seat with no record is from an older version of the tool (its value
                // was automatic too); a seat whose value differs from the record was changed by hand and is left alone.
                SerializedProperty recorded = existing.FindProperty("autoEyeHeight");
                bool automatic = recorded.floatValue <= 0f || Mathf.Abs(current.floatValue - recorded.floatValue) < 0.001f;

                if (automatic && Mathf.Abs(current.floatValue - eye) > 0.001f)
                {
                    current.floatValue = eye;
                    recorded.floatValue = eye;
                    existing.ApplyModifiedProperties();
                    EditorUtility.SetDirty(seat);
                    raised++;
                }

                // A facing that is still exactly what the older automatic rule chose is replaced by the better rule (face a
                // computer that is right in front of the chair, else the desk); a facing you set by hand is kept.
                SerializedProperty currentYaw = existing.FindProperty("sitYaw");
                float oldRuleYaw = FacingYaw(chair, center, desks, computers, false);
                float newRuleYaw = FacingYaw(chair, center, desks, computers, true);
                if (Mathf.Abs(Mathf.DeltaAngle(currentYaw.floatValue, oldRuleYaw)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(oldRuleYaw, newRuleYaw)) > 0.5f)
                {
                    currentYaw.floatValue = newRuleYaw;
                    existing.ApplyModifiedProperties();
                    EditorUtility.SetDirty(seat);
                    turned++;
                }

                if (ClearStatic(chair))
                    unbatched++;

                kept++;
                continue;
            }

            if (chair.GetComponentInChildren<Collider>() == null)
            {
                AddBoxCollider(chair, bounds);
                colliders++;
            }

            seat = Undo.AddComponent<SeatInteractable>(chair.gameObject);

            if (ClearStatic(chair))
                unbatched++;

            float floorY = bounds.size.sqrMagnitude > 0f ? bounds.min.y : chair.position.y;
            Vector3 seatWorld = new Vector3(center.x, floorY, center.z);
            float yaw = FacingYaw(chair, center, desks, computers, true);
            SerializedObject so = new SerializedObject(seat);
            so.FindProperty("seatLocalPosition").vector3Value = chair.InverseTransformPoint(seatWorld);
            so.FindProperty("sitYaw").floatValue = yaw;
            so.FindProperty("seatedEyeHeight").floatValue = eye;
            so.FindProperty("autoEyeHeight").floatValue = eye;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(seat);
            added++;
        }

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);

        report.AppendLine(chairs.Count + " chair(s) in the scene: " + added + " made sittable, " + kept + " already set up, " +
                          skippedTaken + " skipped because an NPC sits there.");
        if (unbatched > 0)
            report.AppendLine("Unticked Static on " + unbatched + " chair(s): Static chairs are merged into one big mesh when Play starts, " +
                              "which made the highlight light up several chairs at once. Now only the chair you look at is highlighted.");
        if (turned > 0)
            report.AppendLine("Turned " + turned + " chair(s) to face the computer in front of them.");
        if (raised > 0)
            report.AppendLine("Raised the seated view on " + raised + " chair(s) so the eyes are above the desk or table in front.");
        if (colliders > 0)
            report.AppendLine("Added a box collider to " + colliders + " chair(s) that had none, so the crosshair can find them. They are now solid.");
        report.AppendLine("Save the scene (Ctrl+S), press Play, look at a free chair within about 3 m and tap the hand button. " +
                          "Tap it again to stand up.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // Seated eye height: a comfortable amount above the desk or table the chair faces, so you look over it, never lower
    // than the default and always below the standing eye height. (Only the seated view; the walking view is unchanged.)
    private static float EyeHeightFor(Vector3 chairCenter, List<Transform> desks, bool lounge, float standingEyeHeight)
    {
        // One standard seated view for every seat (office chairs, lounge chairs, sofas), the same as the meeting room.
        float baseline = StandardEyeHeight;

        Transform desk = NpcCubicleSetupTool.Nearest(desks, chairCenter, 3.5f);
        if (desk == null)
            return baseline;

        Bounds b = NpcCubicleSetupTool.GetBounds(desk);
        float overDesk = b.max.y - floorOf(chairCenter) + EyesAboveDesk;
        return Mathf.Clamp(Mathf.Max(baseline, overDesk), baseline, Mathf.Max(baseline, standingEyeHeight - StayBelowStanding));
    }

    // Floor level near a chair: the characters and the player stand on the floor at about this height.
    private static float floorOf(Vector3 position)
    {
        return 0.05f;
    }

    // A chair that is Batching Static is merged with the other static chairs at Play start; it cannot be highlighted
    // (or moved) on its own. Returns true if anything was changed.
    private static bool ClearStatic(Transform chair)
    {
        bool changed = false;

        foreach (Transform t in chair.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0)
                continue;

            Undo.RecordObject(t.gameObject, "Clear Chair Static Flags");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            changed = true;
        }

        return changed;
    }

    private static bool IsTaken(Vector3 chairCenter, List<Vector3> npcSeats)
    {
        foreach (Vector3 seat in npcSeats)
        {
            Vector3 d = seat - chairCenter;
            d.y = 0f;
            if (d.sqrMagnitude < NpcSeatClearance * NpcSeatClearance)
                return true;
        }

        return false;
    }

    // Face the nearest desk or table (the closest point of it), else the nearest computer, else the chair's own forward.
    private static float FacingYaw(Transform chair, Vector3 center, List<Transform> desks, List<Transform> computers, bool computerFirst)
    {
        // A computer within arm's reach of the chair is what you sit down to use, so face it before the desk edge.
        Transform target = computerFirst ? NpcCubicleSetupTool.Nearest(computers, center, 1.2f) : null;
        if (target == null)
            target = NpcCubicleSetupTool.Nearest(desks, center, 3.5f);
        if (target == null)
            target = NpcCubicleSetupTool.Nearest(computers, center, 3f);

        Vector3 direction = chair.forward;

        if (target != null)
        {
            Bounds b = NpcCubicleSetupTool.GetBounds(target);
            Vector3 toTarget = b.ClosestPoint(center) - center;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f)
                toTarget = b.center - center;

            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
                direction = toTarget;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.forward;

        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    // A box around the chair, in the chair's own space (so it follows a rotated chair).
    private static void AddBoxCollider(Transform chair, Bounds worldBounds)
    {
        BoxCollider box = Undo.AddComponent<BoxCollider>(chair.gameObject);

        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = worldBounds.center + Vector3.Scale(worldBounds.extents, new Vector3(
                (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 local = chair.InverseTransformPoint(corner);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        box.center = (min + max) * 0.5f;
        box.size = max - min;
    }
}

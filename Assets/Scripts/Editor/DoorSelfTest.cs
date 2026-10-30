using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > Firewall Duty > Run Door Self-Test
// Builds two throwaway doors and a throwaway player in the editor (nothing is saved), then checks the
// interaction logic: prompt text, range, independent state, swing direction, open/close animation.
public static class DoorSelfTest
{
    private static int passed;
    private static int failed;
    private static StringBuilder log;

    [MenuItem("Tools/Firewall Duty/Run Door Self-Test")]
    public static void Run()
    {
        passed = 0;
        failed = 0;
        log = new StringBuilder();
        List<GameObject> created = new List<GameObject>();

        try
        {
            DoorInteractable doorA = CreateDoor("TestDoor_A", new Vector3(0f, 0f, 0f), created);
            DoorInteractable doorB = CreateDoor("TestDoor_B", new Vector3(10f, 0f, 0f), created);
            PlayerInteraction player = CreatePlayer(created, out Transform cameraTransform);

            // Player stands 1.5 m in front of door A (on its -Z side), looking at it.
            Place(player.transform, new Vector3(0.5f, 0f, -1.5f));
            player.RefreshTarget();

            Check("crosshair targets door A when aimed at it", player.CurrentInteractable == doorA);
            Check("door A is highlighted while targeted", IsHighlighted(doorA));
            Check("door B is not highlighted", !IsHighlighted(doorB));

            player.Interact();
            Check("tap opens door A", doorA.IsOpen);

            // One tap must never act twice (same frame, inside the cooldown).
            player.Interact();
            Check("duplicate tap in the same frame is ignored", doorA.IsOpen);
            player.ResetTapGuard();

            doorA.Tick(10f);
            Check("door A reaches 90 degrees", Mathf.Abs(Quaternion.Angle(Quaternion.identity, doorA.DoorPivot.localRotation) - 90f) < 0.1f);
            Check("door swings away from the player (+Z)", doorA.DoorPivot.GetChild(0).position.z > 0.3f);
            Check("door B is unaffected", !doorB.IsOpen &&
                  Quaternion.Angle(Quaternion.identity, doorB.DoorPivot.localRotation) < 0.01f);

            // The open leaf has swung away, so aim at where it is now before closing it again.
            Place(player.transform, new Vector3(0.5f, 0f, -1.5f));
            cameraTransform.LookAt(doorA.DoorPivot.GetChild(0).position);
            Physics.SyncTransforms();
            player.RefreshTarget();
            Check("crosshair still targets the open door", player.CurrentInteractable == doorA);

            player.Interact();
            Check("second tap closes door A", !doorA.IsOpen);
            player.ResetTapGuard();
            doorA.Tick(10f);
            Check("door A returns to closed pose", Quaternion.Angle(Quaternion.identity, doorA.DoorPivot.localRotation) < 0.01f);

            // Looking away: no target, highlight removed immediately, tap does nothing.
            cameraTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Physics.SyncTransforms();
            player.RefreshTarget();
            Check("no target when looking away", player.CurrentInteractable == null);
            Check("highlight removed when looking away", !IsHighlighted(doorA));
            player.Interact();
            Check("tap with no target does nothing", !doorA.IsOpen && !doorB.IsOpen);
            player.ResetTapGuard();

            // Range: aimed at the door from too far away.
            cameraTransform.localRotation = Quaternion.identity;
            Place(player.transform, new Vector3(0.5f, 0f, -8f));
            player.RefreshTarget();
            Check("no target when out of range", player.CurrentInteractable == null);

            // Opening from the other side swings the other way.
            cameraTransform.localRotation = Quaternion.identity;
            Place(player.transform, new Vector3(0.5f, 0f, 1.5f));
            cameraTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Physics.SyncTransforms();
            player.RefreshTarget();
            player.Interact();
            doorA.Tick(10f);
            Check("opening from the +Z side swings toward -Z", doorA.DoorPivot.GetChild(0).position.z < -0.3f);
        }
        finally
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        string summary = "Door self-test: " + passed + " passed, " + failed + " failed.\n" + log;
        if (failed > 0) UnityEngine.Debug.LogError(summary);
        else UnityEngine.Debug.Log(summary);
    }

    // Hinge at 'position'; the door leaf (cube) extends 1 m along +X from the hinge.
    private static DoorInteractable CreateDoor(string name, Vector3 position, List<GameObject> created)
    {
        GameObject hinge = new GameObject(name + "_Hinge");
        hinge.transform.position = position;
        created.Add(hinge);

        GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = name;
        leaf.transform.SetParent(hinge.transform, false);
        leaf.transform.localPosition = new Vector3(0.5f, 1f, 0f);
        leaf.transform.localScale = new Vector3(1f, 2f, 0.05f);

        DoorInteractable door = leaf.AddComponent<DoorInteractable>();
        SerializedObject so = new SerializedObject(door);
        so.FindProperty("doorPivot").objectReferenceValue = hinge.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        door.CacheClosedPose();

        return door;
    }

    private static PlayerInteraction CreatePlayer(List<GameObject> created, out Transform cameraTransform)
    {
        GameObject player = new GameObject("TestPlayer");
        created.Add(player);

        GameObject cam = new GameObject("TestCamera");
        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        Camera camera = cam.AddComponent<Camera>();
        camera.enabled = false;
        cameraTransform = cam.transform;

        PlayerInteraction interaction = player.AddComponent<PlayerInteraction>();
        SerializedObject so = new SerializedObject(interaction);
        so.FindProperty("playerCamera").objectReferenceValue = camera;
        so.ApplyModifiedPropertiesWithoutUndo();

        return interaction;
    }

    private static bool IsHighlighted(Interactable target)
    {
        InteractableHighlight highlight = target.GetComponent<InteractableHighlight>();
        return highlight != null && highlight.IsHighlighted;
    }

    private static void Place(Transform t, Vector3 position)
    {
        t.position = position;
        Physics.SyncTransforms();
    }

    private static void Check(string description, bool ok)
    {
        if (ok) passed++;
        else failed++;

        log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + description);
    }
}

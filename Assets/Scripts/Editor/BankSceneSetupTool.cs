using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Bank > Build Bank Scene   (run it with Bank_scene open)
// Turns the furnished bank shell into a playable scene, without moving any of the furniture you already placed:
//   - copies the player, the on-screen controls (joystick, look area, hand button, crosshair, mission HUD) and the mission session
//     from Company_scene (only the first time), and puts the player just inside the entrance;
//   - colours the walls, floor, ceiling and teller counters (materials in Assets/Materials/Bank, edit them to change the look);
//   - lights the bank: a warm lamp under every ceiling light, and brighter ambient light;
//   - adds the people: tellers on tall chairs at the counters, the bank manager in the office, customers queuing, sitting and
//     walking, and two IT officers in the back; all with the same height and animations as the office people;
//   - replaces the entrance with a big glass sliding door like the office one (the doorway is widened for it);
//   - bakes a NavMesh in which the furniture is NOT walkable (so customers never walk on chairs and tables) and sets up every door.
// Safe to run repeatedly: anything already there is kept, and nobody that you moved by hand is moved back (except the two walking
// customers, who are re-placed on free floor each time so their routes are always valid).
public static class BankSceneSetupTool
{
    private const string OfficeScenePath = "Assets/Scenes/Company_scene.unity";
    private const string CharacterFolder = "Assets/PolygonalMind/LowPolyOffice/Characters/";
    private const string MaterialFolder = "Assets/Materials/Bank";
    private const string GlassMaterialPath = "Assets/Materials/Glass_material.mat";
    private const string GuardName = "mesh_office_m_security01";
    private const string SeatRootName = "NPC_Seats";
    private const string WaypointRootName = "NPC_Waypoints";
    private const string EntranceName = "Entrance_Door";
    private const string TellerChairName = "Bank_TellerChair";

    private const float Floor = 0.25f;                                     // the top of the bank floor
    private static readonly Vector3 PlayerStart = new Vector3(14.2f, Floor + 0.05f, -1.9f);
    private const float PlayerYaw = 270f;                                  // looking west, into the lobby

    // Every character in the office is scaled like this (the same height for everybody), so the bank's are too.
    private static readonly Vector3 NpcScale = new Vector3(1f, 0.8666178f, 1f);

    // The east wall has the doorway. These are the two wall pieces next to it and the piece above it.
    private const string WallNearDoor = "wall_left (10)";
    private const string WallAboveDoor = "wall_left (11)";
    private const float DoorStartZ = -3.6f;      // the doorway runs from here ...
    private const float DoorEndZ = -0.175f;      // ... to the start of the long wall
    private const float WallInnerX = 16.364f;    // the inside face of the east wall
    private const float PanelThickness = 0.06f;
    private const float PanelOverlap = 0.02f;

    // Staff that sit at a desk (the seat tool pairs each with its nearest chair).
    private static readonly string[] SeatedStaff =
    {
        "mesh_office_f_recepcionist01", "mesh_office_f_secretary01", "mesh_office_m_boss01"
    };

    private struct Person
    {
        public string model;      // the character file name (also the object's name in the scene)
        public Vector3 position;
        public float yaw;
        public Kind kind;
    }

    private enum Kind { Seated, Queue, Tech, Sit, Walk, Atm, Talk, Wait }

    private static readonly Person[] People =
    {
        // Tellers at the first and third counters (the middle counter is the player's own); the bank manager behind his desk in the corner office.
        new Person { model = "mesh_office_f_recepcionist01", position = new Vector3(1.49f, Floor, -4.42f), yaw = 90f, kind = Kind.Seated },
        new Person { model = "mesh_office_f_secretary01", position = new Vector3(1.49f, Floor, 5.38f), yaw = 90f, kind = Kind.Seated },
        new Person { model = "mesh_office_m_boss01", position = new Vector3(15.5f, Floor, 10.9f), yaw = 270f, kind = Kind.Seated },

        // Customers using the cash machines on the south wall (the position only says which machine: the nearest one).
        new Person { model = "mesh_office_m_hipster01", position = new Vector3(11.15f, Floor, -5.9f), yaw = 0f, kind = Kind.Atm },
        new Person { model = "mesh_office_f_hipster01", position = new Vector3(14.63f, Floor, -5.9f), yaw = 0f, kind = Kind.Atm },

        // Customers standing at the counter, talking to the tellers.
        new Person { model = "mesh_office_m_black01", position = new Vector3(3.4f, Floor, -4.42f), yaw = 270f, kind = Kind.Talk },
        new Person { model = "mesh_office_f_slacker01", position = new Vector3(3.4f, Floor, 5.38f), yaw = 270f, kind = Kind.Talk },

        // Customers waiting in the armchairs.
        new Person { model = "mesh_office_m_slacker01", position = new Vector3(4.125f, Floor, -1.64f), yaw = 0f, kind = Kind.Sit },
        new Person { model = "mesh_office_f_black01", position = new Vector3(4.125f, Floor, 3.61f), yaw = 0f, kind = Kind.Sit },

        // Customers walking about the lobby (their start point is chosen on the NavMesh, from WalkSpots).
        new Person { model = "mesh_office_m_marketing01", position = new Vector3(7.0f, Floor, 5.0f), yaw = 0f, kind = Kind.Walk },
        new Person { model = "mesh_office_f_marketing01", position = new Vector3(12.2f, Floor, -3.6f), yaw = 0f, kind = Kind.Walk },

        // The IT officers in the back office area.
        new Person { model = "mesh_office_m_it01", position = new Vector3(11.75f, Floor, 11.5f), yaw = 270f, kind = Kind.Wait },
        new Person { model = "mesh_office_f_it01", position = new Vector3(5.5f, Floor, 9.2f), yaw = 180f, kind = Kind.Tech }
    };

    // Free-floor places to start a walk, tried in order until one is really on the NavMesh.
    private static readonly Dictionary<string, Vector3[]> WalkSpots = new Dictionary<string, Vector3[]>
    {
        { "mesh_office_m_marketing01", new[] { new Vector3(7.0f, Floor, 5.0f), new Vector3(6.6f, Floor, 4.0f), new Vector3(7.4f, Floor, 3.2f), new Vector3(7.0f, Floor, -4.0f) } },
        { "mesh_office_f_marketing01", new[] { new Vector3(12.2f, Floor, -3.6f), new Vector3(13.4f, Floor, -2.8f), new Vector3(11.2f, Floor, -2.4f), new Vector3(14.0f, Floor, 3.0f) } }
    };

    [MenuItem("Tools/Firewall Duty/Bank/Build Bank Scene")]
    public static void Build()
    {
        Scene bank = SceneManager.GetActiveScene();
        if (bank.name != "Bank_scene")
        {
            EditorUtility.DisplayDialog("Build Bank Scene", "Open Assets/Scenes/Bank_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        StringBuilder report = new StringBuilder();

        string failure = BringPlayer(bank, report, out bool broughtNow);
        if (failure != null)
        {
            report.AppendLine("ERROR: " + failure);
            UnityEngine.Debug.LogError(report.ToString());
            return;
        }

        if (broughtNow)
            report.AppendLine(PlacePlayer(bank));

        report.AppendLine(ColourRooms(bank));
        report.AppendLine(LightRooms());

        AnimatorController controller = NpcSetupTool.EnsureLocomotionController(report);
        if (controller == null)
        {
            UnityEngine.Debug.LogError(report.ToString());
            return;
        }

        report.AppendLine(RemoveGuard());
        report.AppendLine(BuildEntranceDoor(bank));

        report.AppendLine(RemoveMiddleTeller());
        PlacePeople(bank, controller, report);

        // Computer screens: the tellers' computers show bank files, the manager's the same executive files as the office.
        ComputerScreenSetupTool.SetUp();
        report.AppendLine("Screens: set up (see the separate report in the Console): tellers show bank files, the manager shows the executive files.");

        // The middle teller's chair is the player's: sit with the hand button, and the computer in front of it is "your computer".
        report.AppendLine(SetUpPlayerSeat());

        // The titles above the doors.
        report.AppendLine(BuildDoorSigns());

        // Doors: every swinging door in the open scene, plus the hand button's prompt (the sliding entrance is skipped by it).
        DoorSetupTool.SetUpDoorsInOpenScenes();
        report.AppendLine("Doors: set up (see the separate [door] report in the Console).");

        // The mission session, timer and the build list (office first, then bank).
        MissionSetupTool.SetUp();

        EditorSceneManager.MarkSceneDirty(bank);
        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S), then press Play.");
        report.AppendLine("If someone stands in a wrong spot, move that character in the Scene view (their seat marker is under NPC_Seats) and save.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // ---- The player and the on-screen controls -----------------------------------------------------------------

    // Returns null on success, or the reason it failed. moved says whether the player was copied in just now.
    private static string BringPlayer(Scene bank, StringBuilder report, out bool moved)
    {
        moved = false;

        if (Object.FindAnyObjectByType<PlayerMobileController>() != null)
        {
            report.AppendLine("Player: already in the bank scene (left where it is).");
            return null;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).path == OfficeScenePath)
                return "Company_scene is open too. Close it (save it first if you changed it), keep only Bank_scene open, and run this again.";
        }

        if (!File.Exists(OfficeScenePath))
            return OfficeScenePath + " was not found.";

        // The office is opened next to the bank only to take the player and the controls out of it, and is closed again WITHOUT
        // saving, so the office scene on disk is never changed.
        Scene office = EditorSceneManager.OpenScene(OfficeScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(bank);

        List<GameObject> toMove = new List<GameObject>();
        foreach (GameObject root in office.GetRootGameObjects())
        {
            bool isPlayer = root.GetComponentInChildren<PlayerMobileController>(true) != null;
            bool isCanvas = root.GetComponent<Canvas>() != null;
            bool isEvents = root.GetComponent<EventSystem>() != null;
            bool isSession = root.GetComponent<MissionSession>() != null;
            bool isVolume = root.name == "Global Volume";

            if (isPlayer || isCanvas || isEvents || isSession || isVolume)
                toMove.Add(root);
        }

        if (toMove.Count == 0)
        {
            EditorSceneManager.CloseScene(office, true);
            return "found no player or controls in Company_scene.";
        }

        foreach (GameObject go in toMove)
        {
            SceneManager.MoveGameObjectToScene(go, bank);
            report.AppendLine("Player/controls: moved '" + go.name + "' from the office.");
        }

        EditorSceneManager.CloseScene(office, true);
        moved = true;
        return null;
    }

    private static string PlacePlayer(Scene bank)
    {
        PlayerMobileController player = Object.FindAnyObjectByType<PlayerMobileController>();
        if (player == null)
            return "Player: none found.";

        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        string note = "";

        // Two cameras (and two audio listeners) fight each other: the bank's default camera is not needed.
        if (playerCamera != null)
        {
            foreach (GameObject root in bank.GetRootGameObjects())
            {
                if (root.name == "Main Camera" && root.GetComponent<Camera>() != null && root.GetComponentInChildren<PlayerMobileController>() == null)
                {
                    Undo.DestroyObjectImmediate(root);
                    note = " Removed the bank's own Main Camera (the player has one).";
                    break;
                }
            }
        }

        CharacterController body = player.GetComponent<CharacterController>();
        if (body != null)
            body.enabled = false;

        Undo.RecordObject(player.transform, "Place Player");
        player.transform.SetPositionAndRotation(PlayerStart, Quaternion.Euler(0f, PlayerYaw, 0f));

        if (body != null)
            body.enabled = true;

        return "Player: standing at " + PlayerStart.ToString("F2") + " just inside the entrance, looking into the lobby." + note;
    }

    // ---- Colours -----------------------------------------------------------------------------------------------

    private static string ColourRooms(Scene bank)
    {
        Material walls = EnsureMaterial("Bank_Wall", new Color(0.91f, 0.87f, 0.78f), 0.15f);
        Material floor = EnsureMaterial("Bank_Floor", new Color(0.74f, 0.75f, 0.76f), 0.55f);
        Material ceiling = EnsureMaterial("Bank_Ceiling", new Color(0.96f, 0.96f, 0.94f), 0.05f);
        Material counters = EnsureMaterial("Bank_Counter", new Color(0.36f, 0.22f, 0.13f), 0.4f);

        int count = 0;
        foreach (GameObject root in bank.GetRootGameObjects())
        {
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;

            Material wanted = null;
            string n = root.name;

            if (n == "floor") wanted = floor;
            else if (n == "ceiling") wanted = ceiling;
            else if (n == "wall_right (2)" || n == "wall_right (5)" || n == "wall_right (6)") wanted = counters;   // the teller counter
            else if (n.StartsWith("wall_left") || n.StartsWith("wall_right")) wanted = walls;

            if (wanted == null || renderer.sharedMaterial == wanted)
                continue;

            Undo.RecordObject(renderer, "Colour Bank");
            renderer.sharedMaterial = wanted;
            count++;
        }

        return "Colours: " + count + " surface(s) coloured (cream walls, grey marble floor, white ceiling, walnut counters). " +
               "Edit the materials in " + MaterialFolder + " to change them.";
    }

    private static Material EnsureMaterial(string name, Color colour, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Directory.CreateDirectory(MaterialFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.name = name;
        material.color = colour;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }

    // ---- Lights ------------------------------------------------------------------------------------------------

    private static string LightRooms()
    {
        int lamps = 0;
        int added = 0;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("(Prb)CeilingLight") || (t.parent != null && t.parent.name.StartsWith("(Prb)CeilingLight")))
                continue;

            lamps++;
            if (t.Find("BankLight") != null)
                continue;

            Renderer renderer = t.GetComponentInChildren<Renderer>();
            Vector3 position = renderer != null
                ? new Vector3(renderer.bounds.center.x, renderer.bounds.min.y - 0.1f, renderer.bounds.center.z)
                : t.position + Vector3.down * 0.2f;

            GameObject go = new GameObject("BankLight", typeof(Light));
            Undo.RegisterCreatedObjectUndo(go, "Create Bank Light");
            go.transform.SetParent(t, true);
            go.transform.position = position;

            Light light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.1f;
            light.range = 7f;
            light.shadows = LightShadows.None;   // cheap, and right for a lamp under a ceiling
            added++;
        }

        // Without a sky the rooms are dark wherever no lamp reaches: a flat, warm ambient light fills them in.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f, 0.56f, 0.52f);

        string pipeline = CeilingFanLightSetupTool.FixPipeline("Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/Mobile_Renderer.asset");
        pipeline += " " + CeilingFanLightSetupTool.FixPipeline("Assets/Settings/PC_RPAsset.asset", null);

        return "Lights: " + lamps + " ceiling light(s) found, " + added + " lamp(s) added, warm ambient light set. " + pipeline;
    }

    // ---- The entrance ------------------------------------------------------------------------------------------

    private static string RemoveGuard()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == GuardName && t.parent == null)
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                return "Guard: removed.";
            }
        }

        return "Guard: none (removed earlier).";
    }

    // Replaces the small swinging door in the east wall with a wide two-panel glass sliding door, like the office entrance.
    private static string BuildEntranceDoor(Scene bank)
    {
        foreach (GameObject root in bank.GetRootGameObjects())
        {
            if (root.name == EntranceName && root.GetComponent<SlidingDoorInteractable>() != null)
                return "Entrance: the glass sliding door is already there.";
        }

        GameObject nearDoor = FindRoot(bank, WallNearDoor);
        GameObject aboveDoor = FindRoot(bank, WallAboveDoor);
        if (nearDoor == null || aboveDoor == null)
            return "Entrance: WARNING, could not find the walls '" + WallNearDoor + "' and '" + WallAboveDoor + "' around the doorway, so the entrance was not changed.";

        // The old swinging door in the doorway (frame and leaf are one prefab).
        // Collected first and destroyed afterwards: destroying while looping would also destroy the door's children in the list.
        List<GameObject> oldDoors = new List<GameObject>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.parent == null && t.name.StartsWith("(Prb)SeparatorDoor") && t.position.x > 15.5f && t.position.z < 0f && t.position.z > -4f)
                oldDoors.Add(t.gameObject);
        }

        int removed = 0;
        foreach (GameObject oldDoor in oldDoors)
        {
            if (oldDoor == null)
                continue;

            Undo.DestroyObjectImmediate(oldDoor);
            removed++;
        }

        // Widen the doorway: the wall piece south of it ends sooner, and the piece above it gets longer.
        // (These walls are rotated 90 degrees, so their length is the X scale and runs along Z.)
        Transform wall = nearDoor.transform;
        float wallStartZ = wall.position.z - wall.localScale.x * 0.5f;
        float wallLength = DoorStartZ - wallStartZ;
        Undo.RecordObject(wall, "Widen Doorway");
        wall.localScale = new Vector3(wallLength, wall.localScale.y, wall.localScale.z);
        wall.position = new Vector3(wall.position.x, wall.position.y, wallStartZ + wallLength * 0.5f);

        Transform header = aboveDoor.transform;
        float headerStart = DoorStartZ - 0.1f;
        float headerEnd = DoorEndZ + 0.25f;
        Undo.RecordObject(header, "Widen Doorway Header");
        header.localScale = new Vector3(headerEnd - headerStart, header.localScale.y, header.localScale.z);
        header.position = new Vector3(header.position.x, header.position.y, (headerStart + headerEnd) * 0.5f);

        float topY = header.position.y - header.localScale.y * 0.5f;
        float height = topY - Floor;
        float openWidth = DoorEndZ - DoorStartZ;
        float centerZ = (DoorStartZ + DoorEndZ) * 0.5f;

        if (height < 1.5f || openWidth < 1f)
            return "Entrance: WARNING, the doorway measured " + openWidth.ToString("F2") + " m wide and " + height.ToString("F2") + " m high, which looks wrong, so no door was built.";

        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);

        float x = WallInnerX - PanelThickness * 0.5f - 0.005f;
        GameObject group = new GameObject(EntranceName);
        Undo.RegisterCreatedObjectUndo(group, "Create Sliding Entrance Door");
        group.transform.SetPositionAndRotation(new Vector3(x, Floor, centerZ), Quaternion.Euler(0f, 90f, 0f));

        float panelWidth = openWidth * 0.5f + PanelOverlap * 0.5f;
        Transform left = CreatePanel(group.transform, EntranceName + "_Left", -(openWidth * 0.25f - PanelOverlap * 0.25f), panelWidth, height, glass);
        Transform right = CreatePanel(group.transform, EntranceName + "_Right", openWidth * 0.25f - PanelOverlap * 0.25f, panelWidth, height, glass);

        SlidingDoorInteractable door = Undo.AddComponent<SlidingDoorInteractable>(group);
        SerializedObject so = new SerializedObject(door);
        so.FindProperty("leaf").objectReferenceValue = left;
        so.FindProperty("secondLeaf").objectReferenceValue = right;
        so.FindProperty("slideDirection").vector3Value = Vector3.left;   // the left panel goes left, the right one goes right
        so.FindProperty("slideDistance").floatValue = Mathf.Round(openWidth * 0.5f * 100f) / 100f;
        so.FindProperty("automatic").boolValue = true;
        so.FindProperty("autoOpenDistance").floatValue = 3.2f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(door);

        return "Entrance: removed " + removed + " old door(s); built a glass sliding door " + openWidth.ToString("F2") + " m wide and " + height.ToString("F2") +
               " m high that opens by itself when you come near (the hand button works too).";
    }

    private static Transform CreatePanel(Transform parent, string name, float centerOffsetX, float width, float height, Material material)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(panel, "Create Door Panel");
        panel.name = name;
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = new Vector3(centerOffsetX, height * 0.5f, 0f);
        panel.transform.localScale = new Vector3(width, height, PanelThickness);

        if (material != null)
            panel.GetComponent<MeshRenderer>().sharedMaterial = material;

        return panel.transform;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
    }

    // ---- The player's own teller station -------------------------------------------------------------------------

    // The teller who sat at the middle counter is not there: that station is the player's.
    private const string RemovedTeller = "mesh_office_m_recepcionist01";

    private static string RemoveMiddleTeller()
    {
        int removed = 0;

        Transform npc = FindNpc(RemovedTeller);
        if (npc != null)
        {
            Undo.DestroyObjectImmediate(npc.gameObject);
            removed++;
        }

        // Its seat marker (and the hand markers under it), or the chair would still count as taken.
        GameObject seatRoot = GameObject.Find(SeatRootName);
        if (seatRoot != null)
        {
            foreach (string suffix in new[] { "_Seat", "_Seat (fitted)" })
            {
                Transform marker = seatRoot.transform.Find(RemovedTeller + suffix);
                if (marker != null)
                {
                    Undo.DestroyObjectImmediate(marker.gameObject);
                    removed++;
                }
            }
        }

        return removed > 0 ? "Middle teller: removed (" + removed + " object(s)); that counter is yours." : "Middle teller: none (removed earlier).";
    }

    // Makes the free tall chair sittable (the same tool as the office chairs), lifts the seat to the chair's real height with the eyes just
    // above the counter, and marks the computer in front of it as the player's.
    private static string SetUpPlayerSeat()
    {
        SeatSetupTool.SetUp();

        const float seatedEyeHeight = 0.81f;     // eye height above the (raised) seat, in the player's own units
        const float customMark = 1.4201f;        // recorded so the seat tool knows this height was set on purpose

        List<Transform> computers = NpcCubicleSetupTool.FindByName(NpcCubicleSetupTool.IsComputer);
        string result = "Player seat: no free tall chair with a seat was found.";

        foreach (Transform chair in NpcCubicleSetupTool.FindByName(lower => lower == TellerChairName.ToLowerInvariant()))
        {
            SeatInteractable seat = chair.GetComponent<SeatInteractable>();
            Transform seatTop = chair.Find("SeatTop");
            if (seat == null || seatTop == null)
                continue;

            Bounds bounds = NpcCubicleSetupTool.GetBounds(chair);
            SerializedObject so = new SerializedObject(seat);
            SerializedProperty recorded = so.FindProperty("autoEyeHeight");

            // Only the first time: after that, a seat height you tuned is kept.
            if (Mathf.Abs(recorded.floatValue - customMark) > 0.00005f)
            {
                so.FindProperty("seatLocalPosition").vector3Value = chair.InverseTransformPoint(new Vector3(bounds.center.x, seatTop.position.y, bounds.center.z));
                so.FindProperty("seatedEyeHeight").floatValue = seatedEyeHeight;
                recorded.floatValue = customMark;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(seat);
            }

            string computerNote = "no computer within reach";
            Transform computer = NpcCubicleSetupTool.Nearest(computers, bounds.center, 1.5f);
            if (computer != null)
            {
                foreach (MyComputerMarker old in Object.FindObjectsByType<MyComputerMarker>(FindObjectsInactive.Exclude))
                {
                    if (old.gameObject != computer.gameObject)
                        Undo.DestroyObjectImmediate(old);
                }

                if (computer.GetComponent<MyComputerMarker>() == null)
                    Undo.AddComponent<MyComputerMarker>(computer.gameObject);

                computerNote = "'" + computer.name + "' is marked as your computer";
            }

            result = "Player seat: the tall chair at " + chair.position.ToString("F2") + " can be sat on with the hand button; " + computerNote + ".";
            break;
        }

        return result;
    }

    // ---- Titles above the doors ----------------------------------------------------------------------------------

    private struct Sign
    {
        public string text;
        public Vector3 position;   // the middle of the plate, on the wall face
        public Vector3 outward;    // the way the text faces (toward the people who read it)
    }

    // The bank's walls are 0.5 m thick and 7 m high, so a sign goes on the wall face just above each doorway, like the office signs.
    // "Teller Area" is on the back-room side of the door to the teller counters (staff go in there); "Bank Manager" faces the lobby.
    private static readonly Sign[] Signs =
    {
        new Sign { text = "Teller Area", position = new Vector3(-1.95f, 3.75f, 7.628f), outward = new Vector3(0f, 0f, 1f) },
        new Sign { text = "Bank Manager", position = new Vector3(11.33f, 3.75f, 7.072f), outward = new Vector3(0f, 0f, -1f) },
        new Sign { text = "Main Entrance", position = new Vector3(16.336f, 3.75f, -1.8875f), outward = new Vector3(-1f, 0f, 0f) },

        // The counter numbers: on the lobby side of the teller wall, above each teller window (the tellers sit at z -4.42, 0.77 and 5.38).
        new Sign { text = "Counter 1", position = new Vector3(2.728f, 3.4f, -4.42f), outward = new Vector3(1f, 0f, 0f) },
        new Sign { text = "Counter 2", position = new Vector3(2.728f, 3.4f, 0.77f), outward = new Vector3(1f, 0f, 0f) },
        new Sign { text = "Counter 3", position = new Vector3(2.728f, 3.4f, 5.38f), outward = new Vector3(1f, 0f, 0f) }
    };

    private static bool SignsNeedCleaning(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform)
                continue;

            if (t.name.Contains("_Hinge") || t.name.Contains("Head Manager") || t.GetComponent<DoorInteractable>() != null)
                return true;
        }

        return false;
    }

    private static string BuildDoorSigns()
    {
        const float plateWidth = 1.4f;
        const float plateHeight = 0.4f;
        const float plateThickness = 0.05f;

        Material plateMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DoorSign.mat");

        GameObject root = GameObject.Find("Door_Labels");

        // An earlier run let the door tool treat the signs as doors (their names contain "door"): it wrapped each plate and text in a hinge, so
        // every run made new ones and they piled up on top of each other. If any of that is there (a "_Hinge" object, a door on a sign, or the old
        // "Head Manager" sign), the whole group is thrown away and built fresh. A group that is clean is left exactly as it is.
        if (root != null && SignsNeedCleaning(root))
        {
            Undo.DestroyObjectImmediate(root);
            root = null;
        }

        if (root == null)
        {
            root = new GameObject("Door_Labels");
            Undo.RegisterCreatedObjectUndo(root, "Create Door Labels");
        }

        int made = 0;
        foreach (Sign sign in Signs)
        {
            string plateName = "DoorSign_" + sign.text;
            string labelName = "DoorLabel_" + sign.text;

            Transform plate = root.transform.Find(plateName);
            if (plate == null)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(cube, "Create Door Sign");
                cube.name = plateName;
                cube.transform.SetParent(root.transform, false);
                cube.transform.position = sign.position;
                cube.transform.rotation = Quaternion.LookRotation(-sign.outward);
                cube.transform.localScale = new Vector3(plateWidth, plateHeight, plateThickness);

                // The text is the readable part; the plate must never block the player or the crosshair.
                Collider collider = cube.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);

                if (plateMaterial != null)
                    cube.GetComponent<MeshRenderer>().sharedMaterial = plateMaterial;

                plate = cube.transform;
                made++;
            }

            Transform label = root.transform.Find(labelName);
            TextMeshPro tmp;
            if (label == null)
            {
                GameObject go = new GameObject(labelName, typeof(TextMeshPro));
                Undo.RegisterCreatedObjectUndo(go, "Create Door Label");
                go.transform.SetParent(root.transform, false);
                label = go.transform;
                label.rotation = Quaternion.LookRotation(-sign.outward);   // TextMeshPro reads correctly when its forward points away from the viewer
                label.position = plate.position + sign.outward * (plateThickness * 0.5f + 0.004f);
                tmp = go.GetComponent<TextMeshPro>();
            }
            else
            {
                tmp = label.GetComponent<TextMeshPro>();
                if (tmp == null)
                    tmp = Undo.AddComponent<TextMeshPro>(label.gameObject);
            }

            tmp.text = sign.text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 20f;
            tmp.rectTransform.sizeDelta = new Vector2(plateWidth * 0.92f, plateHeight * 0.8f);

            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            MeshRenderer meshRenderer = tmp.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            EditorUtility.SetDirty(tmp);
        }

        return "Door signs: " + Signs.Length + " title(s) above the doors (" + made + " new plate(s)): Teller Area, Bank Manager, Main Entrance, Counter 1, Counter 2, Counter 3.";
    }

    // ---- People ------------------------------------------------------------------------------------------------

    private static void PlacePeople(Scene bank, AnimatorController controller, StringBuilder report)
    {
        // 1. Everybody is placed first (and given the office height), so the seat and walking tools can see the whole scene.
        Dictionary<string, Transform> placed = new Dictionary<string, Transform>();
        foreach (Person person in People)
        {
            Transform npc = FindNpc(person.model);
            if (npc == null)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFolder + person.model + ".fbx");
                if (asset == null)
                {
                    report.AppendLine("[" + person.model + "] WARNING: character file not found, skipped.");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, bank);
                Undo.RegisterCreatedObjectUndo(instance, "Create Bank Character");
                instance.transform.SetPositionAndRotation(person.position, Quaternion.Euler(0f, person.yaw, 0f));
                npc = instance.transform;
                report.AppendLine("[" + person.model + "] placed at " + person.position.ToString("F2") + ".");
            }

            if (npc.localScale != NpcScale)
            {
                Undo.RecordObject(npc, "Office Height");
                npc.localScale = NpcScale;
            }

            // Everybody stands on the bank floor (its top is Floor). A character that ended up lower has its feet in the floor, and the
            // markers made from its position would be too low as well.
            if (person.kind != Kind.Walk && Mathf.Abs(npc.position.y - Floor) > 0.001f)
            {
                Undo.RecordObject(npc, "Stand On Floor");
                npc.position = new Vector3(npc.position.x, Floor, npc.position.z);
            }

            placed[person.model] = npc;
        }

        Transform seatRoot = GetSeatRoot();

        // 2. The tellers sit on tall chairs at the counters (replacing the office chairs), the manager at his desk.
        report.AppendLine(ReplaceTellerChairs());

        List<string> seated = new List<string>();
        foreach (string name in SeatedStaff)
        {
            if (placed.ContainsKey(name))
                seated.Add(name);
        }

        NpcCubicleSetupTool.SetUpSeated(seated.ToArray());
        report.AppendLine(PushTellersBack(seatRoot));

        // 3. The others.
        List<Transform> armchairs = NpcCubicleSetupTool.FindByName(lower => lower.Contains("armchair"));
        List<Transform> atms = NpcCubicleSetupTool.FindByName(lower => lower.StartsWith("atm_01"));

        foreach (Person person in People)
        {
            if (!placed.TryGetValue(person.model, out Transform npc))
                continue;

            string result;
            switch (person.kind)
            {
                case Kind.Queue:
                    result = ConfigureStanding(npc, controller, seatRoot, NpcSetupTool.StandIdleParameter, NpcSetupTool.StandIdleState);
                    break;
                case Kind.Tech:
                    result = ConfigureStanding(npc, controller, seatRoot, NpcSetupTool.StandWorkingParameter, NpcSetupTool.StandWorkingState);
                    break;
                case Kind.Sit:
                    result = ConfigureSitting(npc, controller, armchairs, seatRoot);
                    break;
                case Kind.Wait:
                    result = ConfigureWaiting(npc, controller, seatRoot);
                    break;
                case Kind.Atm:
                    result = ConfigureAtmUser(npc, controller, seatRoot, atms);
                    break;
                case Kind.Talk:
                    result = ConfigureTalking(npc, controller, seatRoot, placed);
                    break;
                default:
                    continue;
            }

            report.AppendLine("[" + npc.name + "] " + result);
        }

        // 4. The NavMesh: floor only. Every piece of furniture is marked NOT walkable first, so nobody can walk on a chair or a table.
        report.AppendLine(BlockFurniture(bank));

        if (NpcSetupTool.EnsureSurface(bank, true, report) != null)
        {
            foreach (Person person in People)
            {
                if (person.kind == Kind.Walk && placed.TryGetValue(person.model, out Transform npc))
                    report.AppendLine("[" + npc.name + "] " + SetUpWalker(npc, controller));
            }
        }
        else
        {
            report.AppendLine("WARNING: the NavMesh could not be baked, so the walking customers were not set up.");
        }
    }

    // ---- Tall teller chairs ------------------------------------------------------------------------------------

    // The counters are high, so the tellers get tall chairs (a round seat on a pole, a back and a foot ring) instead of office chairs.
    // The seat is built 30 cm under the counter top, measured from the computer that stands on it.
    private static string ReplaceTellerChairs()
    {
        List<Transform> computers = NpcCubicleSetupTool.FindByName(NpcCubicleSetupTool.IsComputer);
        List<Transform> oldChairs = NpcCubicleSetupTool.FindByName(lower => lower.StartsWith("(prb)officechair"));

        Material metal = EnsureMaterial("Bank_ChairMetal", new Color(0.55f, 0.57f, 0.6f), 0.7f);
        Material leather = EnsureMaterial("Bank_ChairSeat", new Color(0.45f, 0.1f, 0.12f), 0.35f);

        int replaced = 0;
        foreach (Transform old in oldChairs)
        {
            if (old == null)
                continue;

            // Only the chairs behind the teller counters (the west side), not the manager's.
            if (old.parent != null || old.position.x > 3f)
                continue;

            Transform computer = NpcCubicleSetupTool.Nearest(computers, old.position, 2.5f);
            float deskTop = computer != null ? NpcCubicleSetupTool.GetBounds(computer).min.y : 1.85f;

            Vector3 at = new Vector3(old.position.x, Floor, old.position.z);
            CreateTellerChair(at, deskTop - 0.30f, metal, leather);
            Undo.DestroyObjectImmediate(old.gameObject);
            replaced++;
        }

        int existing = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name == TellerChairName)
                existing++;
        }

        return "Teller chairs: replaced " + replaced + " office chair(s) with tall ones (" + existing + " tall chair(s) in the scene).";
    }

    // A teller sits far enough from the counter to stretch both arms onto it. A teller closer than TellerMinDistance to the computer
    // is moved straight back (with the chair and the hand markers) until TellerDistance away. Tellers already far enough are left alone,
    // so a re-run never moves them again, and neither does a distance you set by hand.
    private const float TellerMinDistance = 0.75f;
    private const float TellerDistance = 0.8f;

    private static string PushTellersBack(Transform seatRoot)
    {
        List<Transform> computers = NpcCubicleSetupTool.FindByName(NpcCubicleSetupTool.IsComputer);
        List<Transform> chairs = NpcCubicleSetupTool.FindByName(lower => lower == TellerChairName.ToLowerInvariant());
        int moved = 0;
        int handsMade = 0;
        string handsNote = "";

        foreach (string name in SeatedStaff)
        {
            if (name == "mesh_office_m_boss01")
                continue;   // the manager sits at his desk, not at a counter

            Transform seat = seatRoot.Find(name + "_Seat");
            if (seat == null)
                continue;

            Transform computer = NpcCubicleSetupTool.Nearest(computers, seat.position, 2f);
            if (computer == null)
                continue;

            Vector3 flat = new Vector3(computer.position.x - seat.position.x, 0f, computer.position.z - seat.position.z);
            float current = flat.magnitude;

            if (current < TellerMinDistance)
            {
                float push = TellerDistance - current;
                Vector3 back = -Vector3.ProjectOnPlane(seat.forward, Vector3.up).normalized * push;

                Undo.RecordObject(seat, "Move Teller Back");
                seat.position += back;

                // The chair under this teller.
                Transform underneath = NpcCubicleSetupTool.Nearest(chairs, seat.position - back, 0.4f);
                if (underneath != null)
                {
                    Undo.RecordObject(underneath, "Move Teller Chair Back");
                    underneath.position += back;
                }

                // Hand markers that already exist are children of the seat, so they moved back with it: bring them forward again.
                foreach (string side in new[] { "_LeftHand", "_RightHand" })
                {
                    Transform hand = seat.Find(seat.name + side);
                    if (hand != null)
                    {
                        Undo.RecordObject(hand, "Move Teller Hand");
                        hand.localPosition += new Vector3(0f, 0f, push);
                    }
                }

                moved++;
            }

            // Both hands rest on the counter: markers on the desk surface in front of the seat (made once, never moved by a re-run).
            Transform chair = NpcCubicleSetupTool.Nearest(chairs, seat.position, 0.6f);
            Transform npc = FindNpc(name);
            NpcWorker worker = npc != null ? npc.GetComponent<NpcWorker>() : null;
            if (chair == null || worker == null)
            {
                handsNote += " [" + name + ": no tall chair or worker found, hands not set]";
                continue;
            }

            SerializedObject workerSo = new SerializedObject(worker);
            if (seat.Find(seat.name + "_LeftHand") == null || seat.Find(seat.name + "_RightHand") == null)
            {
                NpcCubicleSetupTool.EnsureHandRests(workerSo, seat, chair, computers, new List<string>());
                handsMade++;
            }

            workerSo.FindProperty("leftHandRest").objectReferenceValue = seat.Find(seat.name + "_LeftHand");
            workerSo.FindProperty("rightHandRest").objectReferenceValue = seat.Find(seat.name + "_RightHand");
            workerSo.FindProperty("restHandsOnDesk").boolValue = true;
            workerSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(worker);
        }

        return "Tellers: " + moved + " moved back from the counter (" + TellerDistance.ToString("F2") + " m from the computer); hands rest on the counter for all tellers (" +
               handsMade + " new hand marker pair(s))." + handsNote;
    }

    private static void CreateTellerChair(Vector3 floorPosition, float seatTopWorld, Material metal, Material leather)
    {
        GameObject root = new GameObject(TellerChairName);
        Undo.RegisterCreatedObjectUndo(root, "Create Teller Chair");
        // Facing east (+X): the counter and the customers are that way; the backrest is behind (west).
        root.transform.SetPositionAndRotation(floorPosition, Quaternion.Euler(0f, 90f, 0f));

        float seatTop = seatTopWorld - floorPosition.y;               // seat height above the floor, local
        float footLevel = Mathf.Max(0.2f, seatTop - 0.45f);

        AddPart(root.transform, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.6f, 0.015f, 0.6f), metal);
        float poleLength = seatTop - 0.1f - 0.03f;
        AddPart(root.transform, "Pole", PrimitiveType.Cylinder, new Vector3(0f, 0.03f + poleLength * 0.5f, 0f), new Vector3(0.08f, poleLength * 0.5f, 0.08f), metal);
        AddPart(root.transform, "FootRing", PrimitiveType.Cylinder, new Vector3(0f, footLevel, 0f), new Vector3(0.62f, 0.012f, 0.62f), metal);
        AddPart(root.transform, "Seat", PrimitiveType.Cylinder, new Vector3(0f, seatTop - 0.05f, 0f), new Vector3(0.48f, 0.05f, 0.48f), leather);
        AddPart(root.transform, "Back", PrimitiveType.Cube, new Vector3(0f, seatTop + 0.27f, -0.19f), new Vector3(0.36f, 0.3f, 0.05f), leather);

        // Tells the seat tool where the seat surface is (it cannot measure a seat this high by itself).
        GameObject marker = new GameObject("SeatTop");
        marker.transform.SetParent(root.transform, false);
        marker.transform.localPosition = new Vector3(0f, seatTop, 0f);
    }

    private static void AddPart(Transform parent, string name, PrimitiveType shape, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;

        // No collider: the chair must never trap the player or the person sitting on it.
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    // ---- Standing, sitting and walking -------------------------------------------------------------------------

    // Stands at its spot (a marker under NPC_Seats, never moved by a re-run) and plays a standing idle.
    private static string ConfigureStanding(Transform npc, AnimatorController controller, Transform seatRoot, string parameter, string state)
    {
        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        string spotName = npc.name + "_StandSpot";
        Transform spot = seatRoot.Find(spotName);
        if (spot == null)
        {
            GameObject marker = new GameObject(spotName);
            Undo.RegisterCreatedObjectUndo(marker, "Create Stand Spot");
            marker.transform.SetParent(seatRoot, false);
            marker.transform.SetPositionAndRotation(npc.position, npc.rotation);
            spot = marker.transform;
            notes.Add("stands at " + npc.position.ToString("F2"));
        }
        else
        {
            notes.Add("kept existing stand spot (not moved)");

            string lowered = LowerToFloor(spot);
            if (lowered.Length > 0)
                notes.Add(lowered);
        }

        NpcWorker worker = npc.GetComponent<NpcWorker>();
        if (worker == null)
        {
            worker = Undo.AddComponent<NpcWorker>(npc.gameObject);
            notes.Add("added NpcWorker");
        }

        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = spot;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("fitToSeat").boolValue = false;
        so.FindProperty("workingParameter").stringValue = parameter;
        so.FindProperty("workingStateName").stringValue = state;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        return string.Join("; ", notes);
    }

    // Stands in front of the nearest free cash machine, facing it, and taps its keypad (NpcAtmUser moves the hand and the eyes).
    private static string ConfigureAtmUser(Transform npc, AnimatorController controller, Transform seatRoot, List<Transform> atms)
    {
        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        Vector3 wanted = npc.position;
        foreach (Person person in People)
        {
            if (person.model == npc.name)
                wanted = person.position;
        }

        Transform atm = NpcCubicleSetupTool.Nearest(atms, wanted, 3f);
        if (atm == null)
            return string.Join("; ", notes) + "; SKIPPED: no free cash machine (ATM_01) within 3 m of " + wanted.ToString("F1") + ".";

        atms.Remove(atm);   // one customer per machine

        string spotName = npc.name + "_AtmSpot (" + atm.name + ")";
        string keypadName = npc.name + "_AtmKeypad (" + atm.name + ")";

        // Markers from an earlier choice (another machine, or the old queue spot) are replaced; the ones for this machine are kept.
        for (int i = seatRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = seatRoot.GetChild(i);
            bool ours = child.name.StartsWith(npc.name + "_StandSpot") || child.name.StartsWith(npc.name + "_AtmSpot") || child.name.StartsWith(npc.name + "_AtmKeypad");
            if (ours && child.name != spotName && child.name != keypadName)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        Transform spot = seatRoot.Find(spotName);
        Transform keypad = seatRoot.Find(keypadName);

        if (spot == null || keypad == null)
        {
            // The side of the machine that faces the lobby: the machine's own axis that points most toward the middle of the bank.
            Bounds bounds = NpcCubicleSetupTool.GetBounds(atm);
            Vector3 toLobby = new Vector3(9f - bounds.center.x, 0f, -bounds.center.z);

            Vector3 front = Vector3.forward;
            float best = float.NegativeInfinity;
            foreach (Vector3 axis in new[] { atm.forward, -atm.forward, atm.right, -atm.right })
            {
                Vector3 flat = new Vector3(axis.x, 0f, axis.z).normalized;
                float dot = Vector3.Dot(flat, toLobby.normalized);
                if (dot > best)
                {
                    best = dot;
                    front = flat;
                }
            }

            float halfDepth = Mathf.Abs(front.x) * bounds.extents.x + Mathf.Abs(front.z) * bounds.extents.z;
            Vector3 face = new Vector3(bounds.center.x, 0f, bounds.center.z) + front * (halfDepth + 0.01f);
            float keypadY = bounds.min.y + Mathf.Clamp(bounds.size.y * 0.52f, 0.9f, 1.15f);

            if (spot == null)
            {
                GameObject marker = new GameObject(spotName);
                Undo.RegisterCreatedObjectUndo(marker, "Create ATM Spot");
                marker.transform.SetParent(seatRoot, false);
                marker.transform.SetPositionAndRotation(new Vector3(bounds.center.x, Floor, bounds.center.z) + front * (halfDepth + 0.5f),
                                                        Quaternion.LookRotation(-front));
                spot = marker.transform;
                notes.Add("stands at '" + atm.name + "' " + atm.position.ToString("F2"));

                // Show it there in the editor too (the worker moves it there anyway when Play starts).
                Undo.RecordObject(npc, "Place ATM Customer");
                npc.SetPositionAndRotation(spot.position, spot.rotation);
            }

            if (keypad == null)
            {
                GameObject marker = new GameObject(keypadName);
                Undo.RegisterCreatedObjectUndo(marker, "Create ATM Keypad");
                marker.transform.SetParent(seatRoot, false);
                marker.transform.SetPositionAndRotation(new Vector3(face.x, keypadY, face.z), Quaternion.LookRotation(-front));   // forward points into the machine
                keypad = marker.transform;
                notes.Add("keypad marker at height " + keypadY.ToString("F2"));
            }
        }
        else
        {
            notes.Add("kept existing ATM spot and keypad (not moved)");
        }

        // An earlier version put this marker 20 cm too low (feet under the floor): lift it onto the floor.
        string lowered = LowerToFloor(spot);
        if (lowered.Length > 0)
        {
            notes.Add(lowered);
            Undo.RecordObject(npc, "Stand On Floor");
            npc.position = new Vector3(spot.position.x, Floor, spot.position.z);
        }

        NpcWorker worker = npc.GetComponent<NpcWorker>();
        if (worker == null)
        {
            worker = Undo.AddComponent<NpcWorker>(npc.gameObject);
            notes.Add("added NpcWorker");
        }

        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = spot;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("fitToSeat").boolValue = false;
        so.FindProperty("workingParameter").stringValue = NpcSetupTool.StandIdleParameter;
        so.FindProperty("workingStateName").stringValue = NpcSetupTool.StandIdleState;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        NpcAtmUser user = npc.GetComponent<NpcAtmUser>();
        if (user == null)
        {
            user = Undo.AddComponent<NpcAtmUser>(npc.gameObject);
            notes.Add("added NpcAtmUser");
        }

        SerializedObject userSo = new SerializedObject(user);
        userSo.FindProperty("keypad").objectReferenceValue = keypad;
        userSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(user);

        // No head wandering or gestures on top of this: the eyes belong to the machine.
        NpcHeadBehaviour head = npc.GetComponent<NpcHeadBehaviour>();
        if (head != null)
            Undo.DestroyObjectImmediate(head);

        notes.Add("taps the keypad and watches the screen");
        return string.Join("; ", notes);
    }

    // Stands at the counter facing the teller, talking with small head movements and hand gestures; the teller looks back and nods.
    private static string ConfigureTalking(Transform npc, AnimatorController controller, Transform seatRoot, Dictionary<string, Transform> placed)
    {
        string result = ConfigureStanding(npc, controller, seatRoot, NpcSetupTool.StandIdleParameter, NpcSetupTool.StandIdleState);
        List<string> notes = new List<string> { result };

        // The teller at this counter: the seated staff member closest along the counter.
        Transform teller = null;
        float bestGap = float.MaxValue;
        foreach (string name in SeatedStaff)
        {
            if (name == "mesh_office_m_boss01" || !placed.TryGetValue(name, out Transform candidate))
                continue;

            float gap = Mathf.Abs(candidate.position.z - npc.position.z);
            if (gap < bestGap)
            {
                bestGap = gap;
                teller = candidate;
            }
        }

        if (teller == null)
            return string.Join("; ", notes) + "; no teller found to talk to.";

        // The customer talks, looking at the teller (a seated teller's head is about 2 m above the root on the tall chair).
        NpcHeadBehaviour talk = npc.GetComponent<NpcHeadBehaviour>();
        if (talk == null)
            talk = Undo.AddComponent<NpcHeadBehaviour>(npc.gameObject);

        SerializedObject talkSo = new SerializedObject(talk);
        talkSo.FindProperty("mode").enumValueIndex = (int)NpcHeadBehaviour.Mode.Talk;
        talkSo.FindProperty("lookAtTarget").objectReferenceValue = teller;
        talkSo.FindProperty("lookAtHeight").floatValue = 2.1f;
        talkSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(talk);

        if (npc.GetComponent<NpcGestures>() == null)
            Undo.AddComponent<NpcGestures>(npc.gameObject);

        // The teller listens: looks at the customer and nods now and then.
        NpcHeadBehaviour listen = teller.GetComponent<NpcHeadBehaviour>();
        if (listen == null)
            listen = Undo.AddComponent<NpcHeadBehaviour>(teller.gameObject);

        SerializedObject listenSo = new SerializedObject(listen);
        listenSo.FindProperty("mode").enumValueIndex = (int)NpcHeadBehaviour.Mode.Nod;
        listenSo.FindProperty("lookAtTarget").objectReferenceValue = npc;
        listenSo.FindProperty("lookAtHeight").floatValue = 1.5f;
        listenSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(listen);

        notes.Add("talks to '" + teller.name + "' with head movements and hand gestures; the teller looks back and nods");
        return string.Join("; ", notes);
    }

    // The IT officer in the head manager's room sits on the sofa "Prop_Sofa_06_A", as if just waiting for the manager. The sofa's real shape is
    // MEASURED (see MeasureSofa): where its seat is and how high, and which side its backrest is on. He sits on the cushion facing away from
    // the backrest, with his back a hand's width in front of it, in the relaxed sitting pose.
    private const string SofaName = "Prop_Sofa_06_A";
    private const string WaitingChairName = "Bank_WaitingChair";
    private const float HipAboveCushion = 0.21f;   // hips above the measured cushion top: 12 cm sank him into it, so this is 9 cm higher (change it to raise or lower him)

    private static string ConfigureWaiting(Transform npc, AnimatorController controller, Transform seatRoot)
    {
        // A waiting chair built by an earlier version of this tool is not wanted any more.
        List<GameObject> oldChairs = new List<GameObject>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == WaitingChairName && t.parent == null)
                oldChairs.Add(t.gameObject);
        }

        foreach (GameObject chair in oldChairs)
        {
            if (chair != null)
                Undo.DestroyObjectImmediate(chair);
        }

        Transform sofa = null;
        foreach (Transform t in NpcCubicleSetupTool.FindByName(lower => lower.StartsWith(SofaName.ToLowerInvariant())))
        {
            sofa = t;
            break;
        }

        if (sofa == null)
            return "SKIPPED: the sofa '" + SofaName + "' was not found in the scene.";

        if (!MeasureSofa(sofa, out Vector3 position, out Vector3 facing, out float seatTop, out string how))
            return "SKIPPED: could not measure the sofa (" + how + ").";

        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        // New seat marker on the floor under his hips, turned away from the backrest. The old markers (standing spot, earlier seat) go.
        foreach (string old in new[] { "_StandSpot", "_Seat", "_Seat (fitted)" })
        {
            Transform oldMarker = seatRoot.Find(npc.name + old);
            if (oldMarker != null)
                Undo.DestroyObjectImmediate(oldMarker.gameObject);
        }

        GameObject marker = new GameObject(npc.name + "_Seat");
        Undo.RegisterCreatedObjectUndo(marker, "Create Sofa Seat");
        marker.transform.SetParent(seatRoot, false);
        marker.transform.SetPositionAndRotation(new Vector3(position.x, Floor, position.z), Quaternion.LookRotation(facing));

        // Where the hips go, relative to the marker (the marker is on the floor, so this includes the cushion height).
        Vector3 hipWorld = new Vector3(position.x, seatTop + HipAboveCushion, position.z) - facing * 0.04f;
        Vector3 hipTarget = marker.transform.InverseTransformPoint(hipWorld);

        Undo.RecordObject(npc, "Place Waiting Person");
        npc.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);

        NpcWorker worker = npc.GetComponent<NpcWorker>();
        bool workerIsNew = worker == null;
        if (worker == null)
        {
            worker = Undo.AddComponent<NpcWorker>(npc.gameObject);
            notes.Add("added NpcWorker");
        }

        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = marker.transform;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("workingParameter").stringValue = NpcSetupTool.LoungeParameter;
        so.FindProperty("workingStateName").stringValue = NpcSetupTool.LoungeState;
        NpcCubicleSetupTool.ApplyFitSettings(so, workerIsNew, hipTarget, false, false, notes);

        // Fitted on purpose, whatever the worker was before (it used to stand): hips on the cushion, feet on the floor.
        so.FindProperty("fitToSeat").boolValue = true;
        so.FindProperty("keepFeetAboveFloor").boolValue = false;
        so.FindProperty("straightenLegs").boolValue = false;
        so.FindProperty("hipTarget").vector3Value = hipTarget;
        so.FindProperty("autoHipTarget").vector3Value = hipTarget;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        // While he waits: the hands drum on the thighs, one after the other (hand IK over the sitting clip), then rest, then again.
        if (npc.GetComponent<NpcLapTapper>() == null)
        {
            Undo.AddComponent<NpcLapTapper>(npc.gameObject);
            notes.Add("added NpcLapTapper (taps his legs while waiting)");
        }

        notes.Add("sits on '" + sofa.name + "' at " + position.ToString("F2") + ", cushion " + (seatTop - Floor).ToString("F2") + " m above the floor, facing " +
                  facing.ToString("F2") + " (" + how + ")");
        return string.Join("; ", notes);
    }

    // Measures a sofa from its mesh: drops a grid of rays onto it (from a temporary collider that is removed again), and works out
    //  - the seat height: the most common height between 28 and 70 cm above the floor (the flat top of the cushions),
    //  - the backrest side: where the cells that stand well above the seat are, seen from the middle of the seat (arms are on both sides, so
    //    they cancel out; the backrest is on one),
    //  - where to sit: in the middle of the seat across, and a hand's width in front of the backrest along its depth.
    // Returns false (with the reason) when it cannot tell.
    private static bool MeasureSofa(Transform sofa, out Vector3 position, out Vector3 facing, out float seatTop, out string how)
    {
        position = sofa.position;
        facing = Vector3.forward;
        seatTop = Floor + 0.45f;
        how = "";

        List<MeshCollider> temporary = new List<MeshCollider>();
        foreach (MeshFilter filter in sofa.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            temporary.Add(collider);
        }

        if (temporary.Count == 0)
        {
            how = "the sofa has no mesh";
            return false;
        }

        Physics.SyncTransforms();

        Bounds bounds = NpcCubicleSetupTool.GetBounds(sofa);
        const float step = 0.04f;
        int nx = Mathf.CeilToInt(bounds.size.x / step) + 1;
        int nz = Mathf.CeilToInt(bounds.size.z / step) + 1;
        float[,] height = new float[nx, nz];
        bool[,] has = new bool[nx, nz];
        HashSet<Collider> ours = new HashSet<Collider>(temporary.ToArray());

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                Vector3 origin = new Vector3(bounds.min.x + ix * step, bounds.max.y + 0.5f, bounds.min.z + iz * step);
                float top = float.NegativeInfinity;

                foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, bounds.size.y + 1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (ours.Contains(hit.collider) && hit.point.y > top)
                        top = hit.point.y;
                }

                if (top > Floor + 0.05f)
                {
                    has[ix, iz] = true;
                    height[ix, iz] = top;
                }
            }
        }

        foreach (MeshCollider collider in temporary)
            Object.DestroyImmediate(collider);

        // 1. The seat height: the busiest height band between 28 and 70 cm above the floor.
        const float low = 0.28f;
        const float high = 0.70f;
        const float bin = 0.015f;
        int bins = Mathf.CeilToInt((high - low) / bin) + 1;
        int[] histogram = new int[bins];

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                float above = height[ix, iz] - Floor;
                if (has[ix, iz] && above >= low && above <= high)
                    histogram[Mathf.Clamp(Mathf.RoundToInt((above - low) / bin), 0, bins - 1)]++;
            }
        }

        int bestBin = -1;
        int bestCount = 0;
        for (int i = 0; i < bins; i++)
        {
            int smoothed = histogram[i] + (i > 0 ? histogram[i - 1] : 0) + (i < bins - 1 ? histogram[i + 1] : 0);
            if (smoothed > bestCount)
            {
                bestCount = smoothed;
                bestBin = i;
            }
        }

        if (bestBin < 0 || bestCount < 12)
        {
            how = "no flat seat found between 28 and 70 cm above the floor";
            return false;
        }

        seatTop = Floor + low + bestBin * bin;

        // 2. The seat cells (flat, at that height) and the tall cells (backrest and arms, well above it).
        List<Vector3> seatCells = new List<Vector3>();
        List<Vector3> tallCells = new List<Vector3>();
        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                if (!has[ix, iz])
                    continue;

                Vector3 p = new Vector3(bounds.min.x + ix * step, 0f, bounds.min.z + iz * step);
                if (Mathf.Abs(height[ix, iz] - seatTop) <= 0.03f)
                    seatCells.Add(p);
                else if (height[ix, iz] > seatTop + 0.18f)
                    tallCells.Add(p);
            }
        }

        if (seatCells.Count < 8)
        {
            how = "the seat is too small to measure";
            return false;
        }

        Vector3 seatCenter = Average(seatCells);

        // 3. Which way he faces: away from the backrest. Falls back to facing the coffee table when no backrest side can be told.
        bool fromBackrest = false;
        Vector3 away = Vector3.zero;
        if (tallCells.Count >= 8)
        {
            Vector3 toBack = Average(tallCells) - seatCenter;
            if (toBack.magnitude >= 0.06f)
            {
                away = -toBack.normalized;
                fromBackrest = true;
            }
        }

        if (!fromBackrest)
        {
            Vector3 table = new Vector3(10.1f, 0f, 11.09f);
            foreach (Transform t in NpcCubicleSetupTool.FindByName(lower => lower.Contains("coffetable")))
            {
                table = NpcCubicleSetupTool.GetBounds(t).center;
                break;
            }

            away = table - seatCenter;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        }

        // Straight along the sofa's own axes (a sofa is square to its walls).
        Vector3 forward = Vector3.ProjectOnPlane(sofa.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(sofa.right, Vector3.up).normalized;
        float bestDot = float.NegativeInfinity;
        foreach (Vector3 axis in new[] { forward, -forward, right, -right })
        {
            float dot = Vector3.Dot(away, axis);
            if (dot > bestDot)
            {
                bestDot = dot;
                facing = axis;
            }
        }

        // 4. Where to sit: across, the middle of the seat; along the depth, a hand's width in front of the back edge of the seat.
        Vector3 side = new Vector3(facing.z, 0f, -facing.x);
        float dMin = float.PositiveInfinity;
        float dMax = float.NegativeInfinity;
        float lateral = 0f;
        foreach (Vector3 p in seatCells)
        {
            float d = Vector3.Dot(p, facing);
            dMin = Mathf.Min(dMin, d);
            dMax = Mathf.Max(dMax, d);
            lateral += Vector3.Dot(p, side);
        }

        lateral /= seatCells.Count;
        float depth = dMin + Mathf.Clamp((dMax - dMin) * 0.5f, 0.2f, 0.32f);

        position = facing * depth + side * lateral;
        how = (fromBackrest ? "backrest measured behind him" : "no backrest side found, facing the coffee table") +
              ", seat " + (dMax - dMin).ToString("F2") + " m deep";
        return true;
    }

    private static Vector3 Average(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 p in points)
            sum += p;

        return sum / Mathf.Max(1, points.Count);
    }

    // Puts a marker that should stand on the floor onto it. Returns a note, or "" when it was already there.
    private static string LowerToFloor(Transform marker)
    {
        if (marker == null || Mathf.Abs(marker.position.y - Floor) <= 0.001f)
            return "";

        float was = marker.position.y;
        Undo.RecordObject(marker, "Marker On Floor");
        marker.position = new Vector3(marker.position.x, Floor, marker.position.z);
        return "put the marker on the floor (it was " + (Floor - was).ToString("F2") + " m too low)";
    }

    // Sits in the nearest armchair, facing the way the armchair faces (away from its backrest), relaxed.
    private static string ConfigureSitting(Transform npc, AnimatorController controller, List<Transform> armchairs, Transform seatRoot, bool faceAwayFromBackrest = true, float forwardShift = 0f)
    {
        List<string> notes = new List<string>();
        Animator animator = NpcSetupTool.ConfigureAnimator(npc, controller, notes);

        Transform chair = NpcCubicleSetupTool.Nearest(armchairs, npc.position, 2f);
        if (chair == null)
            return string.Join("; ", notes) + "; SKIPPED: no seat within 2 m of where this person was placed.";

        armchairs.Remove(chair);   // one person per armchair

        // Always make a fresh seat marker: an earlier one may face the wrong way (toward the tellers' computers).
        Transform oldSeat = seatRoot.Find(npc.name + "_Seat");
        if (oldSeat != null)
            Undo.DestroyObjectImmediate(oldSeat.gameObject);

        Transform seat = NpcCubicleSetupTool.EnsureSeat(npc, chair, new List<Transform>(), false, out Vector3 hipTarget, notes);

        // The marker stands on the floor (an earlier version left it 20 cm too low, with the feet through the floor).
        string lowered = LowerToFloor(seat);
        if (lowered.Length > 0)
            notes.Add(lowered);

        if (faceAwayFromBackrest)
        {
            Vector3 facing = ArmchairFacing(chair);
            seat.rotation = Quaternion.LookRotation(facing);
        }

        // The hips are fitted AFTER the marker is on the floor and turned, because the target is stored relative to the marker.
        hipTarget = NpcCubicleSetupTool.ComputeHipTarget(chair, seat, notes);
        hipTarget += Vector3.forward * forwardShift;   // scoots the hips forward, away from the backrest (0 unless asked)

        bool workerIsNew = npc.GetComponent<NpcWorker>() == null;
        NpcWorker worker = npc.GetComponent<NpcWorker>();
        if (worker == null)
        {
            worker = Undo.AddComponent<NpcWorker>(npc.gameObject);
            notes.Add("added NpcWorker");
        }

        SerializedObject so = new SerializedObject(worker);
        so.FindProperty("seat").objectReferenceValue = seat;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("startSeated").boolValue = true;
        so.FindProperty("workingParameter").stringValue = NpcSetupTool.LoungeParameter;
        so.FindProperty("workingStateName").stringValue = NpcSetupTool.LoungeState;
        NpcCubicleSetupTool.ApplyFitSettings(so, workerIsNew, hipTarget, false, false, notes);

        // The hip target must follow the new facing even for a worker made by the first version of this tool.
        so.FindProperty("hipTarget").vector3Value = hipTarget;
        so.FindProperty("autoHipTarget").vector3Value = hipTarget;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(worker);

        notes.Add("waits in '" + chair.name + "'");
        return string.Join("; ", notes);
    }

    // The direction an armchair faces: along its own forward or backward axis, whichever side its backrest is NOT on.
    private static Vector3 ArmchairFacing(Transform chair)
    {
        Vector3 forward = Vector3.ProjectOnPlane(chair.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.5f)
            forward = Vector3.forward;

        Vector3 right = Vector3.ProjectOnPlane(chair.right, Vector3.up).normalized;

        // The backrest is the tallest part: where the highest vertices are, on average, is the back of the seat.
        Bounds bounds = NpcCubicleSetupTool.GetBounds(chair);
        float tall = bounds.min.y + bounds.size.y * 0.65f;
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (MeshFilter filter in chair.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;

            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                if (world.y < tall)
                    continue;

                Vector3 offset = world - bounds.center;
                offset.y = 0f;
                sum += offset;
                count++;
            }
        }

        if (count == 0)
            return forward;

        Vector3 backrest = sum / count;
        if (backrest.magnitude < 0.03f)
            return forward;

        // People face away from the backrest, along the chair's own nearest axis (forward, back, right or left).
        Vector3 away = -backrest.normalized;
        Vector3 best = forward;
        float bestDot = float.NegativeInfinity;
        foreach (Vector3 axis in new[] { forward, -forward, right, -right })
        {
            float dot = Vector3.Dot(away, axis);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = axis;
            }
        }

        return best;
    }

    // Every object with a mesh, except the floor and carpets, is made NOT walkable for the NavMesh.
    private static string BlockFurniture(Scene bank)
    {
        int blocked = 0;
        int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkable < 0)
            notWalkable = 1;

        foreach (GameObject root in bank.GetRootGameObjects())
        {
            string lower = root.name.ToLowerInvariant();
            if (lower == "floor" || lower == "ceiling" || lower.StartsWith("floor_") || lower.Contains("carpet"))
                continue;

            if (root.GetComponentInChildren<Renderer>() == null
                || root.GetComponentInChildren<PlayerMobileController>() != null
                || root.GetComponentInChildren<Canvas>() != null
                || NpcSetupTool.IsNpc(root.transform))
                continue;

            NavMeshModifier modifier = root.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = Undo.AddComponent<NavMeshModifier>(root);

            if (!modifier.overrideArea || modifier.area != notWalkable)
            {
                Undo.RecordObject(modifier, "Block Furniture");
                modifier.overrideArea = true;
                modifier.area = notWalkable;
                blocked++;
            }
        }

        return "NavMesh: " + blocked + " object(s) marked not walkable (furniture, walls), so only open floor can be walked on.";
    }

    // Puts the customer on free floor, throws away any old route (it may cross furniture), and makes a new one.
    private static string SetUpWalker(Transform npc, AnimatorController controller)
    {
        Vector3[] spots;
        if (!WalkSpots.TryGetValue(npc.name, out spots))
            spots = new[] { npc.position };

        bool onFloor = false;
        foreach (Vector3 spot in spots)
        {
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 0.25f, NavMesh.AllAreas))
            {
                Undo.RecordObject(npc, "Place Walking Customer");
                npc.position = hit.position;
                onFloor = true;
                break;
            }
        }

        if (!onFloor)
            return "SKIPPED: none of the usual starting places is free floor. Move the customer to open floor and run again.";

        NpcWalker walker = npc.GetComponent<NpcWalker>();
        if (walker != null)
        {
            SerializedObject so = new SerializedObject(walker);
            SerializedProperty list = so.FindProperty("waypoints");

            HashSet<GameObject> groups = new HashSet<GameObject>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Transform wp = list.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (wp != null && wp.parent != null && wp.parent.parent != null && wp.parent.parent.name == WaypointRootName)
                    groups.Add(wp.parent.gameObject);
            }

            list.arraySize = 0;
            so.ApplyModifiedProperties();

            foreach (GameObject group in groups)
                Undo.DestroyObjectImmediate(group);
        }

        return NpcSetupTool.ConfigureWalker(npc, controller);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    private static Transform FindNpc(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name == name && NpcSetupTool.IsNpc(t))
                return t;
        }

        return null;
    }

    private static Transform GetSeatRoot()
    {
        GameObject rootObject = GameObject.Find(SeatRootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(SeatRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Seat Root");
        }

        return rootObject.transform;
    }
}

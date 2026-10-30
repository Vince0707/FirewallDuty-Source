using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Office > Build Entrance Street   (run it with Company_scene open)
// Tools > Firewall Duty > Bank > Build Entrance Street     (the same street in front of the bank: run it with Bank_scene open)
// Builds what is outside the office's main entrance, for the arrival in the first mission (see MissionIntro): a street with a pavement, trees,
// street lamps, parked cars, buildings on both sides and across the road, a bench, planters and a doormat at the entrance, and the taxi that
// brings the player. Inside, a carpet runs from the entrance toward the reception, so the walk in is not bare.
// Everything is made of simple shapes and sits in one group called "Outside_Ground" (the name keeps the game's automatic collider fixer away from
// it). Run "Rebuild Entrance Street" to throw the group away and make it again. Nothing of the office itself is changed or moved.
public static class OfficeStreetSetupTool
{
    private const string RootName = "Outside_Ground";
    private const string MaterialFolder = "Assets/Materials/Outside";
    private const string TextureFolder = "Assets/Textures/Outside";
    private const string GlassPath = "Assets/Materials/Glass_material.mat";

    // The street runs along X in front of the office (the entrance is at z = -12, x = 0; the office floor top is 0.05).
    private const float WalkTop = 0.05f;          // the pavement, level with the office floor
    private const float RoadTop = -0.10f;         // the road, a curb below it
    private const float SidewalkInner = -12f;     // the office front wall
    private const float Curb = -16f;              // where the pavement ends and the road begins
    private const float RoadFar = -26f;           // the far edge of the road
    private const float FarWalkEnd = -30f;        // the far pavement ends, the buildings across start
    private const float LaneZ = -17.0f;           // the taxi's lane (next to the curb, curb on its right)

    // The bank: its entrance is in the east wall. The street is built exactly as in the office (along X, the building on the +z side) and then the whole group is
    // turned and moved so that the street's front line (local z = -12) lies on the bank's outer wall and its entrance path on the bank's glass door.
    private const float BankOuterWallX = 16.864f;   // the outer face of the bank's east wall
    private const float BankDoorZ = -1.8875f;       // the middle of the bank's sliding door
    private const float BankFloorTop = 0.25f;        // the top of the bank's floor (the office's is 0.05)

    [MenuItem("Tools/Firewall Duty/Office/Build Entrance Street")]
    public static void Build()
    {
        if (!InScene("Company_scene"))
            return;

        if (GameObject.Find(RootName) != null)
        {
            int removed = RemoveInteriorPlants(GameObject.Find(RootName).transform);
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                UnityEngine.Debug.Log("Removed the " + (removed / 2) + " plants inside the office (pots and leaves: " + removed + " objects). The rest of the street is unchanged. Save the scene (Ctrl+S).");
                return;
            }

            UnityEngine.Debug.Log("The entrance street is already built ('" + RootName + "'). Use Tools > Firewall Duty > Office > Rebuild Entrance Street to make it again.");
            return;
        }

        BuildAll(false);
    }

    [MenuItem("Tools/Firewall Duty/Office/Rebuild Entrance Street")]
    public static void Rebuild()
    {
        if (!InScene("Company_scene"))
            return;

        GameObject old = GameObject.Find(RootName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        BuildAll(false);
    }

    [MenuItem("Tools/Firewall Duty/Bank/Build Entrance Street")]
    public static void BuildBank()
    {
        if (!InScene("Bank_scene"))
            return;

        if (GameObject.Find(RootName) != null)
        {
            UnityEngine.Debug.Log("The bank's entrance street is already built ('" + RootName + "'). Use Tools > Firewall Duty > Bank > Rebuild Entrance Street to make it again.");
            return;
        }

        BuildAll(true);
    }

    [MenuItem("Tools/Firewall Duty/Bank/Rebuild Entrance Street")]
    public static void RebuildBank()
    {
        if (!InScene("Bank_scene"))
            return;

        GameObject old = GameObject.Find(RootName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        BuildAll(true);
    }

    // The plants that stood on both sides of the carpet inside the office (pots and their leaves; inside = in front of the wall's inner side).
    private static int RemoveInteriorPlants(Transform root)
    {
        System.Collections.Generic.List<GameObject> found = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in root)
        {
            if ((child.name == "Plant_Pot" || child.name == "Plant_Leaves") && child.position.z > SidewalkInner)
                found.Add(child.gameObject);
        }

        foreach (GameObject go in found)
            Undo.DestroyObjectImmediate(go);

        return found.Count;
    }

    private static bool InScene(string sceneName)
    {
        if (SceneManager.GetActiveScene().name == sceneName)
            return true;

        EditorUtility.DisplayDialog("Entrance Street", "Open Assets/Scenes/" + sceneName + ".unity first (it must be the active scene), then run this again.", "OK");
        return false;
    }

    // ---- Everything ------------------------------------------------------------------------------------------------------

    private static void BuildAll(bool bank)
    {
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(TextureFolder);

        GameObject rootObject = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Build Entrance Street");
        Transform root = rootObject.transform;

        Texture2D windows = EnsureWindowTexture();

        // Everything is built in the street's own coordinates (the group still at the origin, not turned): the building on the +z side, the entrance at
        // x = 0, z = -12. For the bank the finished group is then turned and moved onto the bank's entrance (below).
        BuildGround(root);
        BuildBuildings(root, windows, bank);
        BuildStreetDecor(root);
        BuildSnowDecor(root);
        BuildEntrance(root);
        BuildEntranceDesign(root, bank);
        BuildTaxiAndPath(root);
        BuildBounds(root);

        if (!bank)
            BuildInterior(root);

        if (bank)
        {
            // The street's front line (z = -12) goes on the bank's outer wall, its entrance (x = 0) on the bank's glass door, its pavement level with the
            // bank's floor, and "outward" (-z) becomes the bank's east.
            root.SetPositionAndRotation(new Vector3(BankOuterWallX - 12f, BankFloorTop - WalkTop, BankDoorZ), Quaternion.Euler(0f, 270f, 0f));
        }

        // These two use world positions, so they come after the group has been placed.
        BuildPedestrians(root);
        BuildShelter(root, bank);
        string doorReport = DressSlidingDoor();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        UnityEngine.Debug.Log("Entrance street built in '" + RootName + "' for the " + (bank ? "bank" : "office") + ": road, pavements, buildings on both sides, trees, lamps, parked cars, " +
                              "people walking, the entrance bench and planters, the taxi and the bus (switched off until the arrival)" +
                              (bank ? "" : " and the carpet from the entrance toward the reception") + ". Save the scene (Ctrl+S). " +
                              "To see the arrival: press Play on a mission of the " + (bank ? "bank" : "office") + ". " + doorReport);
    }

    // An invisible box over the building: the rain and the snow that get into it are removed (nothing falls inside the building).
    private static void BuildShelter(Transform root, bool bank)
    {
        GameObject go = new GameObject("Intro_Shelter");
        go.transform.SetParent(root, false);

        // The inside of the office is 30 x 24 m around the origin; the bank is about 19 x 22 m (x from -2.8 to 16.4, z from -7.1 to 14.5). Ten metres high.
        Vector3 center = bank ? new Vector3(6.8f, 5f, 3.7f) : new Vector3(0f, 5f, 0f);
        Vector3 size = bank ? new Vector3(19.2f, 10f, 21.6f) : new Vector3(29.6f, 10f, 23.6f);

        go.transform.SetPositionAndRotation(center, Quaternion.identity);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
    }

    // ---- Ground, road, pavements -----------------------------------------------------------------------------------------

    private static void BuildGround(Transform root)
    {
        Material grass = Mat("Ground", new Color(0.22f, 0.3f, 0.2f), 0.05f);
        Material asphalt = Mat("Asphalt", new Color(0.17f, 0.17f, 0.18f), 0.1f);
        Material paving = Mat("Paving", new Color(0.62f, 0.61f, 0.58f), 0.1f);
        Material pavingLight = Mat("PavingEntrance", new Color(0.78f, 0.76f, 0.7f), 0.15f);
        Material paint = Mat("RoadPaint", new Color(0.92f, 0.92f, 0.88f), 0.1f);
        Material yellow = Mat("RoadYellow", new Color(0.95f, 0.78f, 0.15f), 0.1f);

        // Under everything, so there is never a hole to look into.
        Part(root, "Ground", PrimitiveType.Cube, new Vector3(0f, -0.6f, 0f), new Vector3(400f, 0.4f, 400f), grass, true);

        // The road, and the pavements on both sides (the near one is level with the office floor, so there is no step at the door).
        Part(root, "Road", PrimitiveType.Cube, new Vector3(0f, RoadTop - 0.1f, (Curb + RoadFar) * 0.5f), new Vector3(260f, 0.2f, Curb - RoadFar), asphalt, true);
        Part(root, "Pavement_Near", PrimitiveType.Cube, new Vector3(0f, WalkTop - 0.1f, (SidewalkInner + Curb) * 0.5f), new Vector3(260f, 0.2f, SidewalkInner - Curb), paving, true);
        Part(root, "Pavement_Far", PrimitiveType.Cube, new Vector3(0f, WalkTop - 0.1f, (RoadFar + FarWalkEnd) * 0.5f), new Vector3(260f, 0.2f, RoadFar - FarWalkEnd), paving, true);

        // A lighter path from the entrance to the curb.
        Part(root, "Entrance_Path", PrimitiveType.Cube, new Vector3(0f, WalkTop - 0.005f, -14f), new Vector3(3.4f, 0.02f, 4f), pavingLight, false);

        // Road markings: a dashed centre line, a yellow edge line along the far curb and a zebra crossing.
        float lineY = RoadTop + 0.011f;
        for (float x = -110f; x <= 110f; x += 8f)
            Part(root, "CenterLine", PrimitiveType.Cube, new Vector3(x, lineY, -21f), new Vector3(3f, 0.02f, 0.18f), paint, false);

        Part(root, "EdgeLine_Far", PrimitiveType.Cube, new Vector3(0f, lineY, -25.6f), new Vector3(260f, 0.02f, 0.12f), yellow, false);

        for (float z = -16.7f; z > -25.4f; z -= 0.95f)
            Part(root, "Crosswalk", PrimitiveType.Cube, new Vector3(17.5f, lineY, z), new Vector3(3.2f, 0.02f, 0.45f), paint, false);
    }

    // ---- Buildings -------------------------------------------------------------------------------------------------------

    private struct Block
    {
        public float x;
        public float width;
        public float height;
        public Color color;
        public string sign;

        public Block(float x, float width, float height, Color color, string sign)
        {
            this.x = x;
            this.width = width;
            this.height = height;
            this.color = color;
            this.sign = sign;
        }
    }

    private static void BuildBuildings(Transform root, Texture2D windows, bool bank)
    {
        Color cream = new Color(0.9f, 0.84f, 0.7f);
        Color brick = new Color(0.72f, 0.42f, 0.34f);
        Color grey = new Color(0.66f, 0.7f, 0.74f);
        Color blue = new Color(0.55f, 0.68f, 0.82f);
        Color sand = new Color(0.82f, 0.74f, 0.56f);
        Color green = new Color(0.6f, 0.75f, 0.64f);

        // On the office's side of the street, left and right of the office (its fronts are in line with the pavement).
        Block[] left =
        {
            new Block(-21f, 11f, 11f, brick, "CAFE"),
            new Block(-32.5f, 12f, 17f, grey, ""),
            new Block(-43f, 9f, 9f, cream, "BAKERY"),
            new Block(-54f, 11f, 14f, blue, ""),
            new Block(-65f, 11f, 19f, sand, "PHARMACY"),
            new Block(-76f, 11f, 12f, green, ""),
            new Block(-87f, 12f, 16f, brick, "DELI"),
            new Block(-98f, 10f, 10f, cream, ""),
            new Block(-109f, 12f, 20f, blue, "HOTEL")
        };

        Block[] right =
        {
            new Block(21f, 11f, 9f, cream, "BOOKS"),
            new Block(32.5f, 12f, 15f, blue, ""),
            new Block(43f, 9f, 12f, brick, "MARKET"),
            new Block(54f, 11f, 18f, grey, ""),
            new Block(65f, 11f, 10f, green, "DENTAL"),
            new Block(76f, 11f, 17f, sand, ""),
            new Block(87f, 12f, 11f, cream, "GYM"),
            new Block(98f, 10f, 15f, brick, ""),
            new Block(109f, 12f, 21f, grey, "OFFICES")
        };

        // The neighbours stand side by side from the edge of the building outward: the office is 30 m wide (x from -15 to 15), the bank's front is its
        // east wall (x from -5.5 to 16.7, as the street sees it, because its entrance is not in the middle).
        float leftEdge = bank ? -6.0f : -15.5f;
        float rightEdge = bank ? 17.2f : 15.5f;

        for (int i = 0; i < left.Length; i++)
        {
            left[i].x = leftEdge - left[i].width * 0.5f;
            leftEdge -= left[i].width;
        }

        for (int i = 0; i < right.Length; i++)
        {
            right[i].x = rightEdge + right[i].width * 0.5f;
            rightEdge += right[i].width;
        }

        for (int i = 0; i < left.Length; i++)
            Building(root, "Building_N_L" + i, left[i], SidewalkInner, -1f, windows);

        for (int i = 0; i < right.Length; i++)
            Building(root, "Building_N_R" + i, right[i], SidewalkInner, -1f, windows);

        // Across the street, facing the office.
        Block[] across =
        {
            new Block(-60f, 13f, 21f, grey, ""),
            new Block(-46f, 12f, 12f, brick, "DINER"),
            new Block(-33f, 13f, 16f, sand, ""),
            new Block(-19f, 12f, 10f, blue, "SALON"),
            new Block(-6f, 13f, 24f, cream, ""),
            new Block(8f, 12f, 13f, green, "FLORIST"),
            new Block(21f, 13f, 18f, brick, ""),
            new Block(34f, 12f, 11f, grey, "BANK"),
            new Block(47f, 13f, 22f, blue, ""),
            new Block(60f, 12f, 14f, sand, "CINEMA"),
            new Block(73f, 13f, 19f, green, ""),
            new Block(86f, 12f, 12f, cream, "PIZZA"),
            new Block(99f, 13f, 23f, blue, ""),
            new Block(112f, 12f, 15f, brick, "SHOES"),
            new Block(-73f, 13f, 14f, cream, "BAR"),
            new Block(-86f, 12f, 20f, blue, ""),
            new Block(-99f, 13f, 13f, sand, "TAILOR"),
            new Block(-112f, 12f, 18f, grey, "")
        };

        for (int i = 0; i < across.Length; i++)
        {
            if (bank && across[i].sign == "BANK")
                across[i].sign = "STORE";   // not a second bank across from the bank

            Building(root, "Building_S_" + i, across[i], FarWalkEnd, 1f, windows);
        }
    }

    // A building block: a tall box with windows, a shop front with an awning, a roof box, and perhaps a sign. facing = -1: its front faces -z (toward
    // the street from the office side); +1: its front faces +z (toward the office from across the street). frontZ is where the front face is.
    private static void Building(Transform root, string name, Block block, float frontZ, float facing, Texture2D windows)
    {
        float depth = 14f;
        float height = block.height;
        float centerZ = frontZ - facing * depth * 0.5f;   // the body goes away from the street

        Material wall = BuildingMaterial(name, block.color, windows, new Vector2(block.width / 3.4f, height / 3.6f));
        GameObject body = Part(root, name, PrimitiveType.Cube, new Vector3(block.x, (height - 0.4f) * 0.5f, centerZ), new Vector3(block.width, height + 0.4f, depth), wall, true);

        // Shop front: dark glass with an awning over it.
        Material glass = Mat("ShopGlass", new Color(0.12f, 0.17f, 0.22f), 0.7f);
        Material awning = Mat("Awning_" + ColorUtility.ToHtmlStringRGB(block.color), Color.Lerp(block.color, new Color(0.7f, 0.15f, 0.15f), 0.6f), 0.3f);
        float streetSide = facing;   // the direction toward the street (the way the front faces)

        Part(body.transform.parent, name + "_Shop", PrimitiveType.Cube, new Vector3(block.x, 1.6f, frontZ + streetSide * 0.06f), new Vector3(block.width * 0.86f, 2.8f, 0.1f), glass, false);
        Part(body.transform.parent, name + "_Awning", PrimitiveType.Cube, new Vector3(block.x, 3.3f, frontZ + streetSide * 0.7f), new Vector3(block.width * 0.9f, 0.12f, 1.4f), awning, false);

        // A box on the roof (air conditioning).
        Material roofBox = Mat("RoofBox", new Color(0.4f, 0.42f, 0.44f), 0.2f);
        Part(body.transform.parent, name + "_RoofBox", PrimitiveType.Cube, new Vector3(block.x + block.width * 0.2f, height + 0.45f, centerZ), new Vector3(block.width * 0.3f, 0.9f, depth * 0.3f), roofBox, false);

        if (!string.IsNullOrEmpty(block.sign))
            Sign(body.transform.parent, name + "_Sign", block.sign, new Vector3(block.x, 4.15f, frontZ + streetSide * 0.12f), streetSide, block.width * 0.7f);
    }

    // White letters on a dark board on a building front; "toward" is the direction the board faces (toward whoever reads it).
    private static void Sign(Transform parent, string name, string text, Vector3 position, float towardZ, float width, float height = 0.9f)
    {
        Material board = Mat("SignBoard", new Color(0.09f, 0.13f, 0.2f), 0.2f);
        Part(parent, name + "_Board", PrimitiveType.Cube, position, new Vector3(width, height, 0.08f), board, false);

        GameObject go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.position = position + new Vector3(0f, 0f, towardZ * 0.06f);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -towardZ));   // TextMeshPro reads correctly when its forward points away from the reader

        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 20f;
        tmp.rectTransform.sizeDelta = new Vector2(width * 0.9f, height * 0.78f);

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        MeshRenderer meshRenderer = tmp.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---- Trees, lamps, cars, bench ---------------------------------------------------------------------------------------

    private static void BuildStreetDecor(Transform root)
    {
        // Trees along the near pavement (not at the entrance) and the far one.
        for (float x = -108f; x <= 108f; x += 8f)
        {
            if (Mathf.Abs(x) < 6f)
                continue;

            Tree(root, "Tree_Near", new Vector3(x + 2f, WalkTop, -15.2f), 1f);
        }

        for (float x = -104f; x <= 104f; x += 14f)
            Tree(root, "Tree_Far", new Vector3(x, WalkTop, -27f), 0.9f);

        // Street lamps on the near pavement, arms reaching over the road.
        for (float x = -105f; x <= 105f; x += 14f)
            Lamp(root, new Vector3(x, WalkTop, -15.5f));

        // Cars parked along the far curb (and a couple on the near side of the road, further down the street).
        Color[] colors =
        {
            new Color(0.75f, 0.12f, 0.12f), new Color(0.15f, 0.3f, 0.65f), new Color(0.85f, 0.85f, 0.86f), new Color(0.12f, 0.12f, 0.14f),
            new Color(0.2f, 0.5f, 0.3f), new Color(0.9f, 0.6f, 0.1f)
        };

        float[] xs = { -92f, -78f, -66f, -52f, -38f, -24f, -10f, 12f, 27f, 41f, 55f, 70f, 84f, 98f };
        for (int i = 0; i < xs.Length; i++)
        {
            Transform car = BuildCar(root, "ParkedCar_" + i, colors[i % colors.Length], false);
            car.position = new Vector3(xs[i], RoadTop, -24.4f);
            car.rotation = Quaternion.Euler(0f, i % 2 == 0 ? 90f : 270f, 0f);
        }

    }

    private static void Tree(Transform root, string name, Vector3 position, float scale)
    {
        Material trunk = Mat("TreeTrunk", new Color(0.35f, 0.24f, 0.15f), 0.1f);
        Material leaves = Mat("TreeLeaves", new Color(0.18f, 0.45f, 0.2f), 0.1f);
        Material leaves2 = Mat("TreeLeaves2", new Color(0.24f, 0.55f, 0.24f), 0.1f);

        GameObject tree = new GameObject(name);
        tree.transform.SetParent(root, false);
        tree.transform.position = position;

        Part(tree.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1.2f * scale, 0f), new Vector3(0.4f * scale, 1.2f * scale, 0.4f * scale), trunk, true);
        Part(tree.transform, "Leaves", PrimitiveType.Sphere, new Vector3(0f, 3.5f * scale, 0f), new Vector3(3f * scale, 2.6f * scale, 3f * scale), leaves, false);
        Part(tree.transform, "Leaves2", PrimitiveType.Sphere, new Vector3(0.5f * scale, 4.4f * scale, 0.3f * scale), new Vector3(2.1f * scale, 1.9f * scale, 2.1f * scale), leaves2, false);
    }

    private static void Lamp(Transform root, Vector3 position)
    {
        Material pole = Mat("LampPole", new Color(0.2f, 0.22f, 0.25f), 0.4f);
        Material bulb = Emissive("LampLight", new Color(1f, 0.9f, 0.6f));

        GameObject lamp = new GameObject("StreetLamp");
        lamp.transform.SetParent(root, false);
        lamp.transform.position = position;

        Part(lamp.transform, "Pole", PrimitiveType.Cylinder, new Vector3(0f, 2.6f, 0f), new Vector3(0.14f, 2.6f, 0.14f), pole, false);
        Part(lamp.transform, "Arm", PrimitiveType.Cube, new Vector3(0f, 5.1f, -0.6f), new Vector3(0.1f, 0.1f, 1.2f), pole, false);
        Part(lamp.transform, "Head", PrimitiveType.Cube, new Vector3(0f, 5.0f, -1.2f), new Vector3(0.3f, 0.12f, 0.5f), bulb, false);
    }

    // ---- Snow decorations (mission 3) ----------------------------------------------------------------------------------------

    // Snow on the roofs, awnings, trees and parked cars, heaps along the pavements and a snowman by the entrance. The whole group is switched off in the
    // scene and only appears in the snowy arrival (MissionIntro), so the other missions show the street without snow.
    private static void BuildSnowDecor(Transform root)
    {
        GameObject decorObject = new GameObject("Snow_Decor");
        decorObject.transform.SetParent(root, false);
        Transform decor = decorObject.transform;

        Material snow = Mat("Snow", new Color(0.95f, 0.96f, 0.99f), 0.25f);

        // On top of what is already built: buildings, awnings, trees, parked cars.
        foreach (Transform child in root)
        {
            if (child == decor)
                continue;

            string n = child.name;
            Renderer renderer = child.GetComponent<Renderer>();

            if (n.StartsWith("Building_") && !n.Contains("_Shop") && !n.Contains("_Awning") && !n.Contains("_RoofBox") && !n.Contains("_Sign") && renderer != null)
            {
                Bounds b = renderer.bounds;
                Part(decor, "RoofSnow", PrimitiveType.Cube, new Vector3(b.center.x, b.max.y + 0.12f, b.center.z), new Vector3(b.size.x + 0.3f, 0.35f, b.size.z + 0.3f), snow, false);
            }
            else if (n.EndsWith("_Awning") && renderer != null)
            {
                Bounds b = renderer.bounds;
                Part(decor, "AwningSnow", PrimitiveType.Cube, new Vector3(b.center.x, b.max.y + 0.05f, b.center.z), new Vector3(b.size.x, 0.1f, b.size.z), snow, false);
            }
            else if (n.StartsWith("Tree_"))
            {
                Transform top = child.Find("Leaves2");
                Renderer leaves = top != null ? top.GetComponent<Renderer>() : null;
                if (leaves != null)
                {
                    Bounds b = leaves.bounds;
                    Part(decor, "TreeSnow", PrimitiveType.Sphere, new Vector3(b.center.x, b.max.y - 0.25f, b.center.z), new Vector3(b.size.x * 0.8f, 0.55f, b.size.z * 0.8f), snow, false);
                }
            }
            else if (n.StartsWith("ParkedCar_"))
            {
                GameObject cap = Part(decor, "CarSnow", PrimitiveType.Cube, child.TransformPoint(new Vector3(0f, 1.71f, -0.2f)), new Vector3(1.8f, 0.07f, 2.7f), snow, false);
                cap.transform.rotation = child.rotation;
            }
        }

        // Heaps of snow along the curbs and in front of the buildings (leaving the entrance and the place where the taxi stops free).
        for (float x = -100f; x <= 100f; x += 3.1f)
        {
            if (Mathf.Abs(x) < 4.5f)
                continue;

            Part(decor, "Heap_Curb", PrimitiveType.Sphere, new Vector3(x, WalkTop + 0.05f, -15.85f), new Vector3(2.3f, 0.5f, 0.8f), snow, false);
            Part(decor, "Heap_Wall", PrimitiveType.Sphere, new Vector3(x + 1.2f, WalkTop + 0.05f, -12.35f), new Vector3(2.0f, 0.45f, 0.7f), snow, false);
            Part(decor, "Heap_Far", PrimitiveType.Sphere, new Vector3(x, WalkTop + 0.05f, -26.15f), new Vector3(2.3f, 0.5f, 0.8f), snow, false);
        }

        // A snowman by the entrance, looking toward the street.
        Material coal = Mat("Car_Dark", new Color(0.08f, 0.08f, 0.09f), 0.3f);
        Material carrot = Mat("Carrot", new Color(0.95f, 0.5f, 0.1f), 0.2f);
        Vector3 at = new Vector3(-5.4f, WalkTop, -14.6f);

        Part(decor, "Snowman_Base", PrimitiveType.Sphere, at + new Vector3(0f, 0.42f, 0f), new Vector3(0.95f, 0.85f, 0.95f), snow, false);
        Part(decor, "Snowman_Middle", PrimitiveType.Sphere, at + new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 0.65f, 0.7f), snow, false);
        Part(decor, "Snowman_Head", PrimitiveType.Sphere, at + new Vector3(0f, 1.6f, 0f), new Vector3(0.5f, 0.48f, 0.5f), snow, false);
        Part(decor, "Snowman_EyeL", PrimitiveType.Sphere, at + new Vector3(-0.1f, 1.67f, -0.22f), new Vector3(0.06f, 0.06f, 0.06f), coal, false);
        Part(decor, "Snowman_EyeR", PrimitiveType.Sphere, at + new Vector3(0.1f, 1.67f, -0.22f), new Vector3(0.06f, 0.06f, 0.06f), coal, false);
        Part(decor, "Snowman_Nose", PrimitiveType.Cube, at + new Vector3(0f, 1.6f, -0.32f), new Vector3(0.07f, 0.07f, 0.3f), carrot, false);
        Part(decor, "Snowman_HatBrim", PrimitiveType.Cylinder, at + new Vector3(0f, 1.84f, 0f), new Vector3(0.42f, 0.02f, 0.42f), coal, false);
        Part(decor, "Snowman_Hat", PrimitiveType.Cylinder, at + new Vector3(0f, 2.0f, 0f), new Vector3(0.28f, 0.16f, 0.28f), coal, false);

        decorObject.SetActive(false);
    }

    // ---- The colour of the people ------------------------------------------------------------------------------------------------

    // A character straight from its model file has the plain grey material (it looks silver). The people in the office have the material the project
    // gave them (a textured one): the material of an office character in the scene is taken and put on the new people too, so they look the same.
    private static Material FindOfficeCharacterMaterial()
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("mesh_office_") || !NpcSetupTool.IsNpc(t))
                continue;

            SkinnedMeshRenderer renderer = t.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer != null && renderer.sharedMaterial != null)
                return renderer.sharedMaterial;
        }

        // No office character in the scene: the material the project uses for its props (the same colour palette).
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/PolygonalMind/LowPolyOffice/Props/mesh_fan.fbx"))
        {
            if (asset is Material material)
                return material;
        }

        return null;
    }

    private static void ApplyOfficeLook(GameObject person, Material material)
    {
        if (material == null)
            return;

        foreach (SkinnedMeshRenderer renderer in person.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;

            renderer.sharedMaterials = materials;
        }
    }

    // ---- People walking on the pavements -------------------------------------------------------------------------------------

    private struct Walker
    {
        public string model;
        public float fromX;
        public float toX;
        public float z;
        public float speed;
        public float startAt;

        public Walker(string model, float fromX, float toX, float z, float speed, float startAt)
        {
            this.model = model;
            this.fromX = fromX;
            this.toX = toX;
            this.z = z;
            this.speed = speed;
            this.startAt = startAt;
        }
    }

    // Office characters (the same models and walking animation as the people inside), each walking up and down one stretch of pavement. They are named
    // Pedestrian_..., not like the office people, so no mission or tool mixes them up with the real ones. None stands in the entrance area.
    private static void BuildPedestrians(Transform root)
    {
        StringBuilder report = new StringBuilder();
        AnimatorController controller = NpcSetupTool.EnsureLocomotionController(report);
        if (controller == null)
        {
            UnityEngine.Debug.LogWarning("No people were added outside: " + report);
            return;
        }

        Walker[] walkers =
        {
            new Walker("mesh_office_f_hipster01", -9f, -42f, -14.2f, 1.3f, 0.2f),
            new Walker("mesh_office_m_marketing01", -12f, -30f, -13.6f, 1.1f, 0.7f),
            new Walker("mesh_office_m_hipster01", 9f, 40f, -14.4f, 1.4f, 0.4f),
            new Walker("mesh_office_f_marketing01", 14f, 55f, -13.7f, 1.2f, 0.9f),
            new Walker("mesh_office_f_black01", -20f, -75f, -14.7f, 1.25f, 0.5f),
            new Walker("mesh_office_f_annoying01", -8f, -40f, -28.2f, 1.2f, 0.5f),
            new Walker("mesh_office_m_stoned01", 10f, 45f, -27.6f, 1.3f, 0.1f),
            new Walker("mesh_office_m_black01", -25f, -65f, -28.8f, 1.15f, 0.8f)
        };

        Material officeLook = FindOfficeCharacterMaterial();

        int made = 0;
        for (int i = 0; i < walkers.Length; i++)
        {
            Walker w = walkers[i];
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonalMind/LowPolyOffice/Characters/" + w.model + ".fbx");
            if (asset == null)
                continue;

            GameObject person = (GameObject)PrefabUtility.InstantiatePrefab(asset, SceneManager.GetActiveScene());
            person.name = "Pedestrian_" + (i + 1);
            person.transform.SetParent(root, false);
            person.transform.localScale = new Vector3(1f, 0.8666178f, 1f);   // the same height as everyone in the office
            ApplyOfficeLook(person, officeLook);

            Vector3 a = root.TransformPoint(new Vector3(w.fromX, WalkTop, w.z));
            Vector3 b = root.TransformPoint(new Vector3(w.toX, WalkTop, w.z));
            person.transform.position = Vector3.Lerp(a, b, w.startAt);

            NpcSetupTool.ConfigureAnimator(person.transform, controller, new System.Collections.Generic.List<string>());

            Pedestrian pedestrian = person.AddComponent<Pedestrian>();
            pedestrian.Setup(a, b, w.speed, w.startAt);
            EditorUtility.SetDirty(pedestrian);
            made++;
        }

        UnityEngine.Debug.Log("People outside: " + made + " pedestrian(s) walking on the pavements.");
    }

    // ---- The entrance ------------------------------------------------------------------------------------------------------

    private static void BuildEntrance(Transform root)
    {
        Material wood = Mat("BenchWood", new Color(0.5f, 0.33f, 0.18f), 0.2f);
        Material metal = Mat("BenchMetal", new Color(0.18f, 0.2f, 0.22f), 0.5f);
        Material pot = Mat("PlanterPot", new Color(0.55f, 0.3f, 0.2f), 0.2f);
        Material leaves = Mat("TreeLeaves2", new Color(0.24f, 0.55f, 0.24f), 0.1f);
        Material mat = Mat("DoorMat", new Color(0.45f, 0.1f, 0.12f), 0.05f);
        Material bin = Mat("Bin", new Color(0.25f, 0.4f, 0.3f), 0.3f);

        // Planters on both sides of the door.
        foreach (float x in new[] { -3.3f, 3.3f })
        {
            Part(root, "Planter_Pot", PrimitiveType.Cube, new Vector3(x, WalkTop + 0.3f, -12.9f), new Vector3(1.1f, 0.6f, 0.7f), pot, true);
            Part(root, "Planter_Plant", PrimitiveType.Sphere, new Vector3(x, WalkTop + 1.0f, -12.9f), new Vector3(1.0f, 0.9f, 0.7f), leaves, false);
        }

        // A bench by the wall.
        foreach (float x in new[] { -7.5f, 7.5f })
        {
            Part(root, "Bench_Seat", PrimitiveType.Cube, new Vector3(x, WalkTop + 0.5f, -12.95f), new Vector3(1.9f, 0.08f, 0.5f), wood, false);
            Part(root, "Bench_Back", PrimitiveType.Cube, new Vector3(x, WalkTop + 0.85f, -12.65f), new Vector3(1.9f, 0.4f, 0.06f), wood, false);
            Part(root, "Bench_LegL", PrimitiveType.Cube, new Vector3(x - 0.8f, WalkTop + 0.25f, -12.95f), new Vector3(0.06f, 0.5f, 0.45f), metal, false);
            Part(root, "Bench_LegR", PrimitiveType.Cube, new Vector3(x + 0.8f, WalkTop + 0.25f, -12.95f), new Vector3(0.06f, 0.5f, 0.45f), metal, false);
        }

        // A bin, and a doormat in front of the door.
        Part(root, "Bin", PrimitiveType.Cylinder, new Vector3(5.2f, WalkTop + 0.45f, -12.9f), new Vector3(0.5f, 0.45f, 0.5f), bin, true);
        Part(root, "DoorMat", PrimitiveType.Cube, new Vector3(0f, WalkTop + 0.011f, -12.55f), new Vector3(2.4f, 0.022f, 0.9f), mat, false);
    }

    // ---- The front of the building around the entrance ------------------------------------------------------------------------

    // Built in the street's own coordinates, like everything else: the wall's outer face is at z = faceZ, "outward" is -z and the door is centred on x = 0.
    // The office wall is 3 m high and 0.1 m thick with a 3 m wide doorway; the bank's is 6.5 m high and 0.5 m thick with a wider, taller one.
    // It is stone cladding with a dark base and grooves, a steel frame with light strips around the doorway, a canopy on two pillars with the name on its
    // front edge and downlights under it (and, on the bank, a big sign on the wall above the door).
    private static void BuildEntranceDesign(Transform root, bool bank)
    {
        float faceZ = bank ? -12f : -12.05f;
        float doorHalf = bank ? 1.7125f : 1.5f;
        float doorH = bank ? 3.15f : 3.15f;   // the office door is 3.15 m high (it was 2.15 m, 2.9 m, then 3.3 m which was too high)
        float wallH = bank ? 6.5f : 4.0f;   // the office wall is 4 m high since Office > Raise Ceiling (it was 3 m)
        float halfSpan = bank ? 4.6f : 6.0f;
        float plinthH = 0.5f;
        float g = WalkTop;

        Material stone = Mat("Portal_Stone", new Color(0.66f, 0.63f, 0.58f), 0.15f);
        Material groove = Mat("Portal_Groove", new Color(0.42f, 0.4f, 0.37f), 0.1f);
        Material dark = Mat("Portal_DarkStone", new Color(0.15f, 0.16f, 0.18f), 0.3f);
        Material steel = Mat("Portal_Steel", new Color(0.24f, 0.26f, 0.3f), 0.6f);
        Material accent = Emissive(bank ? "Portal_AccentGold" : "Portal_AccentBlue", bank ? new Color(0.95f, 0.75f, 0.3f) : new Color(0.3f, 0.7f, 1f));
        Material lamp = Emissive("Portal_Downlight", new Color(1f, 0.95f, 0.8f));

        GameObject portal = new GameObject("Portal");
        portal.transform.SetParent(root, false);
        Transform p = portal.transform;

        // The wall on both sides of the door: a dark base, stone cladding with grooves, and a steel strip at the outer end.
        foreach (float side in new[] { -1f, 1f })
        {
            float inner = doorHalf + 0.22f;
            float width = halfSpan - inner;
            float cx = side * (inner + width * 0.5f);
            float clad = wallH - plinthH;

            Part(p, "Portal_Plinth", PrimitiveType.Cube, new Vector3(cx, g + plinthH * 0.5f, faceZ - 0.06f), new Vector3(width, plinthH, 0.12f), dark, false);
            Part(p, "Portal_Cladding", PrimitiveType.Cube, new Vector3(cx, g + plinthH + clad * 0.5f, faceZ - 0.03f), new Vector3(width, clad, 0.06f), stone, false);

            for (float gx = inner + 1.15f; gx < halfSpan - 0.3f; gx += 1.15f)
                Part(p, "Portal_Groove", PrimitiveType.Cube, new Vector3(side * gx, g + plinthH + clad * 0.5f, faceZ - 0.065f), new Vector3(0.035f, clad - 0.2f, 0.02f), groove, false);

            Part(p, "Portal_EdgeTrim", PrimitiveType.Cube, new Vector3(side * (halfSpan - 0.04f), g + wallH * 0.5f, faceZ - 0.07f), new Vector3(0.08f, wallH, 0.14f), steel, false);

            // The steel jamb with a light strip on its front, and a wall lamp.
            Part(p, "Portal_Jamb", PrimitiveType.Cube, new Vector3(side * (doorHalf + 0.11f), g + (doorH + 0.22f) * 0.5f, faceZ - 0.07f), new Vector3(0.22f, doorH + 0.22f, 0.14f), steel, false);
            Part(p, "Portal_Led", PrimitiveType.Cube, new Vector3(side * (doorHalf + 0.11f), g + doorH * 0.5f, faceZ - 0.15f), new Vector3(0.04f, doorH - 0.2f, 0.02f), accent, false);
            Part(p, "Portal_WallLamp", PrimitiveType.Cube, new Vector3(side * (doorHalf + 0.7f), g + doorH * 0.8f, faceZ - 0.12f), new Vector3(0.14f, 0.34f, 0.12f), lamp, false);

            // The edge of the paved entrance path.
            Part(p, "Portal_PathEdge", PrimitiveType.Cube, new Vector3(side * 1.75f, g + 0.012f, -14f), new Vector3(0.08f, 0.012f, 4f), dark, false);
        }

        // Over the door: the cladding continues, a steel beam, and a light strip along it.
        float aboveH = wallH - (doorH + 0.22f);
        if (aboveH > 0.05f)
            Part(p, "Portal_AboveDoor", PrimitiveType.Cube, new Vector3(0f, g + doorH + 0.22f + aboveH * 0.5f, faceZ - 0.03f), new Vector3((doorHalf + 0.22f) * 2f, aboveH, 0.06f), stone, false);

        Part(p, "Portal_Head", PrimitiveType.Cube, new Vector3(0f, g + doorH + 0.11f, faceZ - 0.07f), new Vector3(doorHalf * 2f + 0.44f, 0.22f, 0.14f), steel, false);
        Part(p, "Portal_HeadLed", PrimitiveType.Cube, new Vector3(0f, g + doorH + 0.11f, faceZ - 0.15f), new Vector3(doorHalf * 2f, 0.03f, 0.02f), accent, false);
        // The motion sensor that opens the door (it is what you pass in front of, so the door needs no handle).
        Part(p, "Portal_Sensor", PrimitiveType.Cube, new Vector3(0f, g + doorH + 0.11f, faceZ - 0.17f), new Vector3(0.45f, 0.1f, 0.04f), dark, false);
        Part(p, "Portal_SensorEye", PrimitiveType.Sphere, new Vector3(0f, g + doorH + 0.11f, faceZ - 0.2f), new Vector3(0.04f, 0.04f, 0.02f), accent, false);
        Part(p, "Portal_Threshold", PrimitiveType.Cube, new Vector3(0f, g + 0.015f, faceZ - 0.2f), new Vector3(doorHalf * 2f + 0.44f, 0.03f, 0.4f), steel, false);

        // The canopy: a steel slab on two round pillars, with the name on its front edge and downlights underneath. Solid, so rain and snow stay off the door.
        float slabW = doorHalf * 2f + 2.1f;
        float depth = 1.9f;
        float cy = bank ? doorH + 0.55f : doorH + 0.5f;   // the office wall is only 4 m high, so its canopy sits closer to the door
        float underside = cy - 0.06f;

        Part(p, "Portal_Canopy", PrimitiveType.Cube, new Vector3(0f, g + cy, faceZ - depth * 0.5f), new Vector3(slabW, 0.12f, depth), steel, true);

        float fascia = bank ? 0.5f : 0.4f;   // the office canopy is close to the top of the door, so its name board is slimmer
        Sign(p, "Portal_Name", bank ? "BANK" : "FIREWALL DUTY", new Vector3(0f, g + underside - fascia * 0.5f, faceZ - depth + 0.04f), -1f, slabW - 0.3f, fascia);
        Part(p, "Portal_NameLed", PrimitiveType.Cube, new Vector3(0f, g + underside - fascia - 0.015f, faceZ - depth + 0.04f), new Vector3(slabW - 0.3f, 0.03f, 0.06f), accent, false);

        foreach (float side in new[] { -1f, 1f })
        {
            Part(p, "Portal_Pillar", PrimitiveType.Cylinder, new Vector3(side * (slabW * 0.5f - 0.25f), g + underside * 0.5f, faceZ - depth + 0.25f), new Vector3(0.16f, underside * 0.5f, 0.16f), steel, true);

            foreach (float z in new[] { faceZ - 0.6f, faceZ - 1.3f })
                Part(p, "Portal_Downlight", PrimitiveType.Cylinder, new Vector3(side * 0.9f, g + underside - 0.012f, z), new Vector3(0.3f, 0.01f, 0.3f), lamp, false);
        }

        // The bank also has its name big on the wall above the canopy.
        if (bank)
        {
            float signY = g + doorH + 1.9f;
            Sign(p, "Portal_WallSign", "BANK", new Vector3(0f, signY, faceZ - 0.1f), -1f, 3.2f, 1.1f);
            Part(p, "Portal_WallSignLed", PrimitiveType.Cube, new Vector3(0f, signY - 0.6f, faceZ - 0.1f), new Vector3(3.2f, 0.04f, 0.05f), accent, false);
        }
    }

    // The entrance door's own glass: a copy of the shared glass material (so the meeting room partitions and the vehicle windows stay as they are), but
    // darker and more solid, so the inside of the building is only faintly visible from the street.
    private const string TintedGlassPath = "Assets/Materials/Door_Glass_Tinted.mat";

    private static Material TintedGlass()
    {
        Material tinted = AssetDatabase.LoadAssetAtPath<Material>(TintedGlassPath);

        if (tinted == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            if (source == null)
                return null;

            tinted = new Material(source) { name = "Door_Glass_Tinted" };
            AssetDatabase.CreateAsset(tinted, TintedGlassPath);
        }

        Color color = new Color(0.09f, 0.16f, 0.22f, 0.9f);   // deep blue-grey; the alpha is how solid the glass looks (the shared glass is 0.39; this was 0.72 at first, then asked for more)
        tinted.color = color;
        if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", color);
        if (tinted.HasProperty("_Smoothness")) tinted.SetFloat("_Smoothness", 0.85f);
        EditorUtility.SetDirty(tinted);
        return tinted;
    }

    // Tools > Firewall Duty > Tint Entrance Door Glass   (works in Company_scene and Bank_scene)
    // Gives the two glass panels of the open scene's sliding entrance door the tinted glass, and changes nothing else.
    [MenuItem("Tools/Firewall Duty/Tint Entrance Door Glass")]
    public static void TintDoorGlass()
    {
        Material tinted = TintedGlass();
        if (tinted == null)
        {
            UnityEngine.Debug.LogError("Tint Entrance Door Glass: " + GlassPath + " was not found, so nothing was changed.");
            return;
        }

        int panels = 0;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Entrance_Door")
                continue;

            foreach (Transform panel in root.transform)
            {
                MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
                if (renderer == null || panel.name == "Glass_Track")
                    continue;

                Undo.RecordObject(renderer, "Tint Entrance Door Glass");
                renderer.sharedMaterial = tinted;
                panels++;
            }
        }

        if (panels == 0)
        {
            UnityEngine.Debug.LogWarning("Tint Entrance Door Glass: no 'Entrance_Door' panels were found in the open scene.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Tint Entrance Door Glass: " + panels + " panel(s) now use '" + TintedGlassPath + "'. Save the scene (Ctrl+S). " +
                              "To change how dark it is, edit that material's Base Map colour (the A value is how solid it looks).");
    }

    // Makes the sliding door's two panels glass (the same glass as the bank's) with a slim aluminium frame, a kick plate and a handle on each panel.
    // The trim sits on the panels, so it slides with them. Safe to run again: the old trim is replaced.
    internal static string DressSlidingDoor()
    {
        GameObject door = null;
        foreach (GameObject candidate in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (candidate.name == "Entrance_Door")
            {
                door = candidate;
                break;
            }
        }

        if (door == null)
            return "The sliding door ('Entrance_Door') was not found, so it was not changed.";

        Material glass = TintedGlass();
        Material aluminium = Mat("Portal_DoorAluminium", new Color(0.62f, 0.65f, 0.69f), 0.75f);
        Material rubber = Mat("Portal_DoorRubber", new Color(0.06f, 0.06f, 0.07f), 0.2f);
        int panels = 0;
        float panelWidth = 1.5f;

        // Copied first: the track is added to the same group while the panels are handled.
        System.Collections.Generic.List<Transform> children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in door.transform)
        {
            if (child.name != "Glass_Track")
                children.Add(child);
        }

        foreach (Transform panel in children)
        {
            MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
            if (renderer == null)
                continue;

            Transform old = panel.Find("Glass_Trim");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            if (glass != null)
            {
                Undo.RecordObject(renderer, "Glass Door");
                renderer.sharedMaterial = glass;
            }

            Vector3 s = panel.localScale;
            float w = s.x, h = s.y, t = s.z;
            float outer = panel.localPosition.x < 0f ? -1f : 1f;   // the side of this panel away from the middle of the doorway

            // A child that cancels the panel's scale, so the trim is measured in metres.
            GameObject trimObject = new GameObject("Glass_Trim");
            trimObject.transform.SetParent(panel, false);
            trimObject.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            Transform trim = trimObject.transform;
            float d = t + 0.03f;

            Part(trim, "Stile_Outer", PrimitiveType.Cube, new Vector3(outer * (w * 0.5f - 0.035f), 0f, 0f), new Vector3(0.07f, h, d), aluminium, false);
            Part(trim, "Stile_Inner", PrimitiveType.Cube, new Vector3(-outer * (w * 0.5f - 0.035f), 0f, 0f), new Vector3(0.07f, h, d), aluminium, false);
            Part(trim, "Rail_Top", PrimitiveType.Cube, new Vector3(0f, h * 0.5f - 0.04f, 0f), new Vector3(w, 0.08f, d), aluminium, false);
            Part(trim, "Rail_Kick", PrimitiveType.Cube, new Vector3(0f, -h * 0.5f + 0.11f, 0f), new Vector3(w, 0.22f, d), aluminium, false);

            // An automatic sliding door has no handle: a rubber seal runs down the edge where the two panels meet.
            Part(trim, "Seal", PrimitiveType.Cube, new Vector3(-outer * (w * 0.5f - 0.012f), 0f, 0f), new Vector3(0.024f, h - 0.3f, d + 0.01f), rubber, false);

            panels++;
            panelWidth = w;
        }

        // The guide track in the floor that both panels run along.
        if (panels > 0)
        {
            Transform oldTrack = door.transform.Find("Glass_Track");
            if (oldTrack != null)
                Object.DestroyImmediate(oldTrack.gameObject);

            GameObject track = Part(door.transform, "Glass_Track", PrimitiveType.Cube, new Vector3(0f, 0.008f, 0f), new Vector3(panelWidth * 4f, 0.016f, 0.12f), aluminium, false);
            track.transform.localScale = new Vector3(panelWidth * 4f, 0.016f, 0.12f);
        }

        return panels > 0 ? "The sliding door now has " + panels + " glass panel(s) with aluminium frames." : "The sliding door has no panels, so it was not changed.";
    }

    // ---- The taxi and its path ---------------------------------------------------------------------------------------------

    private static void BuildTaxiAndPath(Transform root)
    {
        Transform taxi = BuildCar(root, MissionIntro.TaxiName, new Color(0.98f, 0.8f, 0.1f), true);
        taxi.position = new Vector3(95f, RoadTop, LaneZ);
        taxi.rotation = Quaternion.Euler(0f, 270f, 0f);
        taxi.gameObject.SetActive(false);   // it appears when the first mission starts

        GameObject path = new GameObject(MissionIntro.PathName);
        path.transform.SetParent(root, false);

        // The taxi drives from +x toward -x (the curb is on its right), and stops with its right rear door in front of the entrance path.
        Marker(path.transform, "Start", new Vector3(95f, RoadTop, LaneZ), 270f);
        Marker(path.transform, "Stop", new Vector3(-0.85f, RoadTop, LaneZ), 270f);
        Marker(path.transform, "Away", new Vector3(-110f, RoadTop, LaneZ), 270f);
        Marker(path.transform, "PlayerDrop", new Vector3(0.2f, WalkTop, -14.3f), 0f);   // on the pavement, facing the office
        Marker(path.transform, "Entrance", new Vector3(0f, WalkTop, SidewalkInner), 180f);   // its blue arrow points out to the street (it tells which side of the door the player is on)

        // Mission 2: the bus (a bit wider, in a lane a little further out), stopping with its front door at the entrance path.
        Transform bus = BuildBus(root);
        bus.position = new Vector3(95f, RoadTop, BusLaneZ);
        bus.rotation = Quaternion.Euler(0f, 270f, 0f);
        BuildPassengers(bus);
        bus.gameObject.SetActive(false);

        Marker(path.transform, "Bus_Start", new Vector3(95f, RoadTop, BusLaneZ), 270f);
        Marker(path.transform, "Bus_Stop", new Vector3(3.6f, RoadTop, BusLaneZ), 270f);
        Marker(path.transform, "Bus_Away", new Vector3(-110f, RoadTop, BusLaneZ), 270f);
    }

    private static void Marker(Transform parent, string name, Vector3 position, float yaw)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    // A car of simple shapes. Local axes: forward = +z, right = +x. The roof stands on thin pillars, so the windows are open gaps (the player looks out
    // of the taxi through them). The taxi also gets seats, a driver and a "Seat" marker (where the player's feet go) and an opening right rear door.
    private static Transform BuildCar(Transform parent, string name, Color bodyColor, bool taxi)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        Transform car = root.transform;

        Material paint = Mat("Car_" + ColorUtility.ToHtmlStringRGB(bodyColor), bodyColor, 0.65f);
        Material dark = Mat("Car_Dark", new Color(0.08f, 0.08f, 0.09f), 0.3f);
        Material seat = Mat("Car_Seat", new Color(0.22f, 0.2f, 0.19f), 0.2f);
        Material silver = Mat("Car_Silver", new Color(0.7f, 0.72f, 0.75f), 0.8f);
        Material head = Emissive("Car_Headlight", new Color(1f, 0.97f, 0.85f));
        Material tail = Emissive("Car_Taillight", new Color(0.9f, 0.1f, 0.1f));
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);

        Part(car, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.33f, 0f), new Vector3(1.9f, 0.16f, 4.4f), dark, false);
        Part(car, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.4f, -0.1f), new Vector3(1.8f, 0.1f, 2.8f), seat, false);
        Part(car, "Hood", PrimitiveType.Cube, new Vector3(0f, 0.7f, 1.65f), new Vector3(1.85f, 0.5f, 1.5f), paint, false);
        Part(car, "Trunk", PrimitiveType.Cube, new Vector3(0f, 0.7f, -1.8f), new Vector3(1.85f, 0.5f, 0.9f), paint, false);
        Part(car, "SideLeft", PrimitiveType.Cube, new Vector3(-0.93f, 0.7f, -0.1f), new Vector3(0.06f, 0.5f, 3.2f), paint, false);
        Part(car, "SideRightFront", PrimitiveType.Cube, new Vector3(0.93f, 0.7f, 0.85f), new Vector3(0.06f, 0.5f, 1.4f), paint, false);
        Part(car, "SideRightRear", PrimitiveType.Cube, new Vector3(0.93f, 0.7f, -1.7f), new Vector3(0.06f, 0.5f, 0.7f), paint, false);

        // The right rear door, hinged at its front edge.
        GameObject doorPivot = new GameObject("DoorRight");
        doorPivot.transform.SetParent(car, false);
        doorPivot.transform.localPosition = new Vector3(0.93f, 0.7f, -0.35f);
        Part(doorPivot.transform, "DoorLeaf", PrimitiveType.Cube, new Vector3(0f, 0f, -0.5f), new Vector3(0.06f, 0.5f, 1.0f), paint, false);

        // Roof and pillars.
        Part(car, "Roof", PrimitiveType.Cube, new Vector3(0f, 1.64f, -0.2f), new Vector3(1.8f, 0.07f, 2.7f), paint, taxi);   // the taxi roof is solid for rain and snow
        foreach (float x in new[] { -0.88f, 0.88f })
        {
            Part(car, "PillarFront", PrimitiveType.Cube, new Vector3(x, 1.25f, 0.85f), new Vector3(0.07f, 0.75f, 0.07f), dark, false);
            Part(car, "PillarRear", PrimitiveType.Cube, new Vector3(x, 1.25f, -1.3f), new Vector3(0.07f, 0.75f, 0.07f), dark, false);
        }

        Part(car, "PillarMiddle", PrimitiveType.Cube, new Vector3(0.9f, 1.22f, -0.1f), new Vector3(0.07f, 0.74f, 0.07f), dark, false);

        // Seats and dashboard.
        Part(car, "Dashboard", PrimitiveType.Cube, new Vector3(0f, 0.95f, 1.0f), new Vector3(1.7f, 0.25f, 0.4f), dark, false);
        foreach (float x in new[] { -0.45f, 0.45f })
        {
            Part(car, "FrontSeat", PrimitiveType.Cube, new Vector3(x, 0.55f, 0.25f), new Vector3(0.55f, 0.16f, 0.5f), seat, false);
            Part(car, "FrontSeatBack", PrimitiveType.Cube, new Vector3(x, 0.9f, -0.02f), new Vector3(0.55f, 0.6f, 0.1f), seat, false);
        }

        Part(car, "RearBench", PrimitiveType.Cube, new Vector3(0f, 0.55f, -0.85f), new Vector3(1.5f, 0.16f, 0.55f), seat, false);
        Part(car, "RearBenchBack", PrimitiveType.Cube, new Vector3(0f, 0.92f, -1.2f), new Vector3(1.5f, 0.62f, 0.12f), seat, false);

        // Wheels (each in a pivot that spins around the axle) and lights.
        foreach (float x in new[] { -0.95f, 0.95f })
        {
            foreach (float z in new[] { -1.3f, 1.3f })
            {
                GameObject pivot = new GameObject("WheelPivot_" + (x < 0 ? "L" : "R") + (z < 0 ? "B" : "F"));
                pivot.transform.SetParent(car, false);
                pivot.transform.localPosition = new Vector3(x, 0.33f, z);

                GameObject tire = Part(pivot.transform, "Tire", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.66f, 0.12f, 0.66f), dark, false);
                tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                GameObject hub = Part(pivot.transform, "Hub", PrimitiveType.Cylinder, new Vector3(Mathf.Sign(x) * 0.07f, 0f, 0f), new Vector3(0.34f, 0.03f, 0.34f), silver, false);
                hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        foreach (float x in new[] { -0.6f, 0.6f })
        {
            Part(car, "Headlight", PrimitiveType.Cube, new Vector3(x, 0.75f, 2.4f), new Vector3(0.3f, 0.15f, 0.05f), head, false);
            Part(car, "Taillight", PrimitiveType.Cube, new Vector3(x, 0.75f, -2.27f), new Vector3(0.3f, 0.12f, 0.05f), tail, false);
        }

        if (taxi)
        {
            Material sign = Emissive("Taxi_Sign", new Color(1f, 0.95f, 0.7f));
            Part(car, "TaxiSign", PrimitiveType.Cube, new Vector3(0f, 1.78f, -0.15f), new Vector3(0.7f, 0.2f, 0.3f), sign, false);

            // A black stripe along the sides, and the driver at the wheel.
            Part(car, "Stripe_L", PrimitiveType.Cube, new Vector3(-0.965f, 0.7f, -0.1f), new Vector3(0.02f, 0.1f, 3.0f), dark, false);
            Part(car, "Stripe_R", PrimitiveType.Cube, new Vector3(0.965f, 0.7f, 0.85f), new Vector3(0.02f, 0.1f, 1.3f), dark, false);

            Material shirt = Mat("Driver_Shirt", new Color(0.2f, 0.3f, 0.55f), 0.2f);
            Material skin = Mat("Driver_Skin", new Color(0.85f, 0.65f, 0.5f), 0.2f);
            Part(car, "Driver_Body", PrimitiveType.Cube, new Vector3(-0.45f, 0.95f, 0.15f), new Vector3(0.42f, 0.55f, 0.25f), shirt, false);
            Part(car, "Driver_Head", PrimitiveType.Sphere, new Vector3(-0.45f, 1.36f, 0.15f), new Vector3(0.24f, 0.26f, 0.24f), skin, false);
            Part(car, "Driver_Cap", PrimitiveType.Cube, new Vector3(-0.45f, 1.5f, 0.17f), new Vector3(0.27f, 0.07f, 0.3f), dark, false);
            GameObject wheel = Part(car, "SteeringWheel", PrimitiveType.Cylinder, new Vector3(-0.45f, 1.04f, 0.7f), new Vector3(0.36f, 0.015f, 0.36f), dark, false);
            wheel.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);

            // Where the player's feet go: the right rear seat, next to the door.
            GameObject seatMarker = new GameObject("Seat");
            seatMarker.transform.SetParent(car, false);
            seatMarker.transform.localPosition = new Vector3(0.42f, 0.45f, -0.85f);

            GameObject eyeMarker = new GameObject("Eye");
            eyeMarker.transform.SetParent(seatMarker.transform, false);
            eyeMarker.transform.localPosition = new Vector3(0f, 0.82f, 0f);
        }
        else if (glass != null)
        {
            // A parked car has glass (the taxi does not: the player looks through it).
            Part(car, "Windshield", PrimitiveType.Cube, new Vector3(0f, 1.26f, 0.86f), new Vector3(1.7f, 0.7f, 0.03f), glass, false);
            Part(car, "RearWindow", PrimitiveType.Cube, new Vector3(0f, 1.25f, -1.3f), new Vector3(1.7f, 0.6f, 0.03f), glass, false);
            Part(car, "WindowLeft", PrimitiveType.Cube, new Vector3(-0.9f, 1.25f, -0.2f), new Vector3(0.03f, 0.6f, 2.0f), glass, false);
            Part(car, "WindowRight", PrimitiveType.Cube, new Vector3(0.9f, 1.25f, -0.2f), new Vector3(0.03f, 0.6f, 2.0f), glass, false);
        }

        return car;
    }

    // ---- The bus (mission 2) and its passengers ------------------------------------------------------------------------------

    private const float BusLaneZ = -17.4f;   // the bus is wider than the taxi: its lane is a little further from the curb

    // A city bus of simple shapes, local axes forward = +z, right = +x. The roof stands on thin pillars, so the windows are open gaps. The front door
    // is on the right, in front of the player's seat; "Seat" is where the player's feet go, "Eye" the height of their eyes, "Exit_1.." the way out.
    private static Transform BuildBus(Transform parent)
    {
        GameObject root = new GameObject(MissionIntro.BusName);
        root.transform.SetParent(parent, false);
        Transform bus = root.transform;

        Material paint = Mat("Bus_Paint", new Color(0.15f, 0.4f, 0.75f), 0.6f);
        Material white = Mat("Bus_White", new Color(0.92f, 0.93f, 0.95f), 0.5f);
        Material dark = Mat("Car_Dark", new Color(0.08f, 0.08f, 0.09f), 0.3f);
        Material floor = Mat("Bus_Floor", new Color(0.25f, 0.25f, 0.27f), 0.2f);
        Material seatMat = Mat("Bus_Seat", new Color(0.15f, 0.35f, 0.55f), 0.2f);
        Material silver = Mat("Car_Silver", new Color(0.7f, 0.72f, 0.75f), 0.8f);
        Material head = Emissive("Car_Headlight", new Color(1f, 0.97f, 0.85f));
        Material tail = Emissive("Car_Taillight", new Color(0.9f, 0.1f, 0.1f));
        Material amber = Emissive("Bus_Destination", new Color(1f, 0.7f, 0.15f));

        Part(bus, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f), new Vector3(2.3f, 0.24f, 10.2f), dark, false);
        Part(bus, "Floor", PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.2f), new Vector3(2.4f, 0.1f, 9.7f), floor, false);
        Part(bus, "Front", PrimitiveType.Cube, new Vector3(0f, 1.05f, 5.15f), new Vector3(2.5f, 1.0f, 0.14f), paint, false);
        Part(bus, "Rear", PrimitiveType.Cube, new Vector3(0f, 1.7f, -5.2f), new Vector3(2.5f, 2.3f, 0.14f), paint, false);

        // The lower side panels with a white stripe; the right side leaves the front door open.
        Part(bus, "SideLeft", PrimitiveType.Cube, new Vector3(-1.25f, 0.85f, 0f), new Vector3(0.08f, 0.6f, 10.2f), paint, false);
        Part(bus, "SideLeftStripe", PrimitiveType.Cube, new Vector3(-1.255f, 1.2f, 0f), new Vector3(0.02f, 0.1f, 10.2f), white, false);
        Part(bus, "SideRightRear", PrimitiveType.Cube, new Vector3(1.25f, 0.85f, -1.5f), new Vector3(0.08f, 0.6f, 7.6f), paint, false);
        Part(bus, "SideRightRearStripe", PrimitiveType.Cube, new Vector3(1.255f, 1.2f, -1.5f), new Vector3(0.02f, 0.1f, 7.6f), white, false);
        Part(bus, "SideRightFront", PrimitiveType.Cube, new Vector3(1.25f, 0.85f, 4.65f), new Vector3(0.08f, 0.6f, 0.7f), paint, false);
        Part(bus, "DoorHeader", PrimitiveType.Cube, new Vector3(1.25f, 2.7f, 3.6f), new Vector3(0.08f, 0.5f, 1.4f), paint, false);

        // The front door on the right, hinged at its front edge, a full-height leaf.
        GameObject doorPivot = new GameObject("DoorRight");
        doorPivot.transform.SetParent(bus, false);
        doorPivot.transform.localPosition = new Vector3(1.25f, 1.475f, 4.3f);
        Part(doorPivot.transform, "DoorLeaf", PrimitiveType.Cube, new Vector3(0f, 0f, -0.7f), new Vector3(0.08f, 1.85f, 1.4f), paint, false);

        // The roof (solid for the rain: its drops stop on it) with a lit destination board, and the pillars between the windows.
        Part(bus, "Roof", PrimitiveType.Cube, new Vector3(0f, 3.0f, 0f), new Vector3(2.5f, 0.1f, 10.4f), white, true);
        Part(bus, "DestinationBoard", PrimitiveType.Cube, new Vector3(0f, 2.65f, 5.12f), new Vector3(1.8f, 0.3f, 0.06f), amber, false);

        foreach (float z in new[] { -4.2f, -2.2f, -0.2f, 1.8f })
        {
            Part(bus, "PillarL", PrimitiveType.Cube, new Vector3(-1.22f, 2.05f, z), new Vector3(0.08f, 1.8f, 0.1f), dark, false);
            Part(bus, "PillarR", PrimitiveType.Cube, new Vector3(1.22f, 2.05f, z), new Vector3(0.08f, 1.8f, 0.1f), dark, false);
        }

        Part(bus, "WindshieldPillarL", PrimitiveType.Cube, new Vector3(-1.2f, 2.2f, 5.1f), new Vector3(0.1f, 1.5f, 0.1f), dark, false);
        Part(bus, "WindshieldPillarR", PrimitiveType.Cube, new Vector3(1.2f, 2.2f, 5.1f), new Vector3(0.1f, 1.5f, 0.1f), dark, false);
        Part(bus, "Dashboard", PrimitiveType.Cube, new Vector3(0f, 1.35f, 4.85f), new Vector3(2.2f, 0.3f, 0.5f), dark, false);

        // Seats: a bench on each side in each row, and two poles in the aisle.
        foreach (float z in new[] { -3.8f, -2.6f, -1.4f, -0.2f, 1.0f, 2.2f })
        {
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                Part(bus, "SeatBase", PrimitiveType.Cube, new Vector3(x, 0.75f, z), new Vector3(0.8f, 0.4f, 0.5f), seatMat, false);
                Part(bus, "SeatCushion", PrimitiveType.Cube, new Vector3(x, 0.97f, z), new Vector3(0.8f, 0.05f, 0.5f), white, false);
                Part(bus, "SeatBack", PrimitiveType.Cube, new Vector3(x, 1.3f, z - 0.3f), new Vector3(0.8f, 0.55f, 0.1f), seatMat, false);
            }
        }

        Part(bus, "PoleA", PrimitiveType.Cylinder, new Vector3(0f, 1.75f, -0.8f), new Vector3(0.06f, 1.2f, 0.06f), silver, false);
        Part(bus, "PoleB", PrimitiveType.Cylinder, new Vector3(0f, 1.75f, -3.2f), new Vector3(0.06f, 1.2f, 0.06f), silver, false);

        // Wheels (each in a pivot that spins around the axle), lights.
        foreach (float x in new[] { -1.1f, 1.1f })
        {
            foreach (float z in new[] { -3.4f, 3.4f })
            {
                GameObject pivot = new GameObject("WheelPivot_" + (x < 0 ? "L" : "R") + (z < 0 ? "B" : "F"));
                pivot.transform.SetParent(bus, false);
                pivot.transform.localPosition = new Vector3(x, 0.5f, z);

                GameObject tire = Part(pivot.transform, "Tire", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.0f, 0.18f, 1.0f), dark, false);
                tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                GameObject hub = Part(pivot.transform, "Hub", PrimitiveType.Cylinder, new Vector3(Mathf.Sign(x) * 0.1f, 0f, 0f), new Vector3(0.5f, 0.04f, 0.5f), silver, false);
                hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        foreach (float x in new[] { -0.9f, 0.9f })
        {
            Part(bus, "Headlight", PrimitiveType.Cube, new Vector3(x, 0.75f, 5.24f), new Vector3(0.3f, 0.15f, 0.05f), head, false);
            Part(bus, "Taillight", PrimitiveType.Cube, new Vector3(x, 0.95f, -5.29f), new Vector3(0.3f, 0.15f, 0.05f), tail, false);
        }

        // Lights inside: under the roof the sun does not reach, so without them the passengers look grey. Two warm lamps and lit ceiling strips.
        Material strip = Emissive("Bus_CeilingStrip", new Color(1f, 0.97f, 0.88f));
        Part(bus, "CeilingStrip_A", PrimitiveType.Cube, new Vector3(0f, 2.93f, -2.2f), new Vector3(0.25f, 0.03f, 3.2f), strip, false);
        Part(bus, "CeilingStrip_B", PrimitiveType.Cube, new Vector3(0f, 2.93f, 2.0f), new Vector3(0.25f, 0.03f, 3.2f), strip, false);
        foreach (float lampZ in new[] { -2.2f, 2.0f })
        {
            GameObject lampObject = new GameObject("CabinLight");
            lampObject.transform.SetParent(bus, false);
            lampObject.transform.localPosition = new Vector3(0f, 2.6f, lampZ);

            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.95f, 0.85f);
            lamp.intensity = 2.5f;
            lamp.range = 6.5f;
            lamp.shadows = LightShadows.None;
        }

        // The driver.
        Material shirt = Mat("Driver_Shirt", new Color(0.2f, 0.3f, 0.55f), 0.2f);
        Material skin = Mat("Driver_Skin", new Color(0.85f, 0.65f, 0.5f), 0.2f);
        Part(bus, "Driver_Seat", PrimitiveType.Cube, new Vector3(-0.8f, 0.75f, 4.0f), new Vector3(0.6f, 0.4f, 0.5f), seatMat, false);
        Part(bus, "Driver_Body", PrimitiveType.Cube, new Vector3(-0.8f, 1.25f, 4.05f), new Vector3(0.45f, 0.6f, 0.28f), shirt, false);
        Part(bus, "Driver_Head", PrimitiveType.Sphere, new Vector3(-0.8f, 1.7f, 4.1f), new Vector3(0.25f, 0.27f, 0.25f), skin, false);
        Part(bus, "Driver_Cap", PrimitiveType.Cube, new Vector3(-0.8f, 1.84f, 4.12f), new Vector3(0.28f, 0.07f, 0.3f), dark, false);
        GameObject wheel = Part(bus, "SteeringWheel", PrimitiveType.Cylinder, new Vector3(-0.8f, 1.5f, 4.6f), new Vector3(0.5f, 0.02f, 0.5f), dark, false);
        wheel.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

        // The player's seat: on the right, in the middle of the bus (the row nearest its centre). Eyes are 1.16 m above the floor when sitting there.
        GameObject seatMarker = new GameObject("Seat");
        seatMarker.transform.SetParent(bus, false);
        seatMarker.transform.localPosition = new Vector3(0.75f, 0.55f, -0.2f);

        GameObject eyeMarker = new GameObject("Eye");
        eyeMarker.transform.SetParent(seatMarker.transform, false);
        eyeMarker.transform.localPosition = new Vector3(0f, 1.16f, 0f);

        // The way out, in order: into the aisle beside the seat, along the aisle to the front, then through the doorway.
        Vector3[] wayOut = { new Vector3(0.1f, 0.55f, -0.2f), new Vector3(0.1f, 0.55f, 3.2f), new Vector3(1.05f, 0.55f, 3.6f) };
        for (int i = 0; i < wayOut.Length; i++)
        {
            GameObject exitMarker = new GameObject("Exit_" + (i + 1));
            exitMarker.transform.SetParent(bus, false);
            exitMarker.transform.localPosition = wayOut[i];
        }

        return bus;
    }

    // People on the bus: six sitting on the benches and two standing by the poles, all office characters (named Passenger_..., so no mission
    // mixes them up with the real office people). They are the bus's children, so they ride with it; the office's own worker script sits them down.
    private static void BuildPassengers(Transform bus)
    {
        StringBuilder report = new StringBuilder();
        AnimatorController controller = NpcSetupTool.EnsureLocomotionController(report);
        if (controller == null)
        {
            UnityEngine.Debug.LogWarning("No passengers were added to the bus: " + report);
            return;
        }

        // model, x, z, sitting
        object[][] people =
        {
            new object[] { "mesh_office_f_cleaning01", -0.75f, -3.8f, true },
            new object[] { "mesh_office_m_cleaning01", -0.75f, -1.4f, true },
            new object[] { "mesh_office_f_suspenders01", -0.75f, 1.0f, true },
            new object[] { "mesh_office_m_slacker01", -0.75f, 2.2f, true },
            new object[] { "mesh_office_f_intern01", 0.75f, -2.6f, true },
            new object[] { "mesh_office_m_intern01", 0.75f, 2.2f, true },
            new object[] { "mesh_office_f_accountant01", 0.15f, -0.8f, false },
            new object[] { "mesh_office_m_secretary01", 0.1f, -3.2f, false }
        };

        Material officeLook = FindOfficeCharacterMaterial();

        for (int i = 0; i < people.Length; i++)
        {
            string model = (string)people[i][0];
            float x = (float)people[i][1];
            float z = (float)people[i][2];
            bool sitting = (bool)people[i][3];

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonalMind/LowPolyOffice/Characters/" + model + ".fbx");
            if (asset == null)
                continue;

            // Where this passenger sits or stands: a marker on the bus floor, facing the front of the bus.
            GameObject marker = new GameObject("Passenger_Spot_" + (i + 1));
            marker.transform.SetParent(bus, false);
            marker.transform.localPosition = new Vector3(x, 0.55f, z);

            GameObject person = (GameObject)PrefabUtility.InstantiatePrefab(asset, SceneManager.GetActiveScene());
            person.name = "Passenger_" + (i + 1);
            person.transform.SetParent(bus, false);
            person.transform.localPosition = new Vector3(x, 0.55f, z);
            person.transform.localRotation = Quaternion.identity;
            person.transform.localScale = new Vector3(1f, 0.8666178f, 1f);   // the same height as everyone in the office
            ApplyOfficeLook(person, officeLook);

            Animator animator = NpcSetupTool.ConfigureAnimator(person.transform, controller, new System.Collections.Generic.List<string>());

            NpcWorker worker = person.AddComponent<NpcWorker>();
            SerializedObject so = new SerializedObject(worker);
            so.FindProperty("seat").objectReferenceValue = marker.transform;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("startSeated").boolValue = true;
            so.FindProperty("fitToSeat").boolValue = sitting;
            so.FindProperty("workingParameter").stringValue = sitting ? NpcSetupTool.LoungeParameter : NpcSetupTool.StandIdleParameter;
            so.FindProperty("workingStateName").stringValue = sitting ? NpcSetupTool.LoungeState : NpcSetupTool.StandIdleState;

            if (sitting)
                so.FindProperty("hipTarget").vector3Value = new Vector3(0f, 0.58f, -0.04f);   // the hips go on the cushion (0.44 m above the floor) plus 0.14

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(worker);
        }
    }

    // ---- Invisible walls ------------------------------------------------------------------------------------------------------

    // The street around the entrance is a closed area: the player cannot wander out to the edge of the world.
    private static void BuildBounds(Transform root)
    {
        float west = -34f;
        float east = 34f;
        float north = -12.4f;
        float south = -29.5f;

        Barrier(root, "Barrier_West", new Vector3(west, 2f, (north + south) * 0.5f), new Vector3(0.5f, 4f, north - south));
        Barrier(root, "Barrier_East", new Vector3(east, 2f, (north + south) * 0.5f), new Vector3(0.5f, 4f, north - south));
        Barrier(root, "Barrier_South", new Vector3(0f, 2f, south), new Vector3(east - west, 4f, 0.5f));
    }

    private static void Barrier(Transform root, string name, Vector3 position, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = position;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = size;
    }

    // ---- Inside: the walk from the door to the reception -------------------------------------------------------------------

    private static void BuildInterior(Transform root)
    {
        Material carpet = Mat("Runner", new Color(0.12f, 0.22f, 0.38f), 0.1f);
        Material gold = Mat("RunnerGold", new Color(0.8f, 0.65f, 0.25f), 0.3f);

        // A carpet from the entrance toward the reception (it stops before the reception furniture).
        float y = 0.05f + 0.011f;
        Part(root, "Runner", PrimitiveType.Cube, new Vector3(0f, y, -9.4f), new Vector3(2.2f, 0.022f, 4.8f), carpet, false);
        Part(root, "Runner_EdgeL", PrimitiveType.Cube, new Vector3(-1.04f, y + 0.002f, -9.4f), new Vector3(0.08f, 0.022f, 4.8f), gold, false);
        Part(root, "Runner_EdgeR", PrimitiveType.Cube, new Vector3(1.04f, y + 0.002f, -9.4f), new Vector3(0.08f, 0.022f, 4.8f), gold, false);

    }

    // ---- Materials, textures, shapes -------------------------------------------------------------------------------------------

    private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 localPosition, Vector3 localScale, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (material != null)
            renderer.sharedMaterial = material;

        if (!collider)
        {
            Collider existing = go.GetComponent<Collider>();
            if (existing != null)
                Object.DestroyImmediate(existing);
        }

        return go;
    }

    // A plain lit material, made once as an asset.
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

    private static Material Emissive(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = Mat(name, color, 0.4f);
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.6f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    // A building's wall: its colour, the window texture, and the number of windows across and up (so the windows are the same size on every building).
    private static Material BuildingMaterial(string name, Color color, Texture2D windows, Vector2 tiling)
    {
        string path = MaterialFolder + "/Wall_" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Wall_" + name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);

        if (windows != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", windows);
                material.SetTextureScale("_BaseMap", tiling);
            }

            material.mainTexture = windows;
            material.mainTextureScale = tiling;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    // One window on a wall, to be tiled over the building: made once as a picture file.
    private static Texture2D EnsureWindowTexture()
    {
        string path = TextureFolder + "/Windows.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
            return existing;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color wall = new Color(0.95f, 0.95f, 0.95f);
        Color frame = new Color(0.45f, 0.47f, 0.5f);
        Color glassTop = new Color(0.32f, 0.45f, 0.6f);
        Color glassBottom = new Color(0.16f, 0.24f, 0.36f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = wall;

                bool inFrame = x >= 26 && x < 102 && y >= 24 && y < 104;
                bool inGlass = x >= 30 && x < 98 && y >= 28 && y < 100;

                if (inFrame)
                    c = frame;

                if (inGlass)
                    c = Color.Lerp(glassBottom, glassTop, (y - 28) / 72f);

                // A bar across the middle and a light reflection.
                if (inGlass && Mathf.Abs(y - 64) < 2)
                    c = frame;

                if (inGlass && Mathf.Abs((x - 30) - (y - 28) * 0.6f - 8f) < 3f)
                    c = Color.Lerp(c, Color.white, 0.25f);

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}

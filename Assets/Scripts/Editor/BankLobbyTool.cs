using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Bank > Decorate Entrance Lobby   (run it with Bank_scene open)
// Dresses the bank's entrance wall inside, around the sliding glass door, in navy and gold. Nothing in the bank is moved or removed (the ATMs, the tellers, the
// sofas and the door signs stay exactly where they are):
//   - the entrance glass gets the same tint as the office door (so the inside is only faintly visible from the street);
//   - a navy board with gold edges above the door: "WELCOME TO THE BANK";
//   - a growth chart picture (gold bars and an arrow) north of the door and a round vault picture south of it, both in gold frames;
//   - two warm brass wall lamps (with a dim real light) above the height of the door, a tall plant on each side and a navy and gold rug inside the door.
// The pictures and plants stay out of the stretch of wall the sliding panels open over. All in one group, "Bank_Lobby_Decor". Run it again to rebuild the
// group; Ctrl+Z removes it.
public static class BankLobbyTool
{
    private const string RootName = "Bank_Lobby_Decor";
    private const float F = 0.25f;               // the top of the bank floor
    private const float WallFace = 16.364f;      // the inside face of the east wall (the bank is on the -x side)
    private const float DoorZ = -1.8875f;        // the middle of the sliding door

    [MenuItem("Tools/Firewall Duty/Bank/Decorate Entrance Lobby")]
    public static void Decorate()
    {
        if (SceneManager.GetActiveScene().name != "Bank_scene")
        {
            EditorUtility.DisplayDialog("Decorate Entrance Lobby", "Open Assets/Scenes/Bank_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        OfficeStreetSetupTool.TintDoorGlass();

        foreach (GameObject old in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (old.name == RootName)
            {
                Undo.DestroyObjectImmediate(old);
                break;
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Materials/Lounge"))
            AssetDatabase.CreateFolder("Assets/Materials", "Lounge");

        GameObject rootObject = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Decorate Bank Entrance Lobby");
        Transform root = rootObject.transform;

        OfficeLoungeTool.Palette p = new OfficeLoungeTool.Palette();

        BuildBoard(root, p);
        BuildChartPicture(root, p, new Vector3(WallFace, F + 2.2f, 3.1f));
        BuildVaultPicture(root, p, new Vector3(WallFace, F + 2.2f, -6.15f));

        // Wall lamps above the height of the door (3.15 m), so the sliding panels never meet them.
        BuildSconce(root, p, new Vector3(WallFace, F + 4.1f, DoorZ - 2.7f));
        BuildSconce(root, p, new Vector3(WallFace, F + 4.1f, DoorZ + 2.7f));

        // A tall plant on each side of the door, close to the floor-level edge of the wall but clear of the sliding panels (they run on the wall itself).
        OfficeLoungeTool.BuildPlant(root, p, "Plant_South", new Vector3(WallFace - 0.6f, F, DoorZ - 2.5f), 1.05f, true);
        OfficeLoungeTool.BuildPlant(root, p, "Plant_North", new Vector3(WallFace - 0.6f, F, DoorZ + 2.5f), 1.05f, true);

        BuildRug(root, p);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Decorate Bank Entrance Lobby: built in '" + RootName + "' (" + rootObject.GetComponentsInChildren<Renderer>().Length +
                              " parts); the door glass is tinted. Press Play and look at the entrance wall from inside. Save the scene (Ctrl+S). Use Edit > Undo to take it away.");
    }

    // A group on the entrance wall: local +z points into the bank (world -x), local +x runs north along the wall (world +z).
    private static Transform OnWall(Transform parent, string name, Vector3 position)
    {
        return OfficeLoungeTool.Group(parent, name, position, 270f);
    }

    // ---- The welcome board over the door -----------------------------------------------------------------------------------------

    private static void BuildBoard(Transform root, OfficeLoungeTool.Palette p)
    {
        float y = F + 4.75f;
        Transform g = OnWall(root, "Welcome_Board", new Vector3(WallFace, y, DoorZ));

        OfficeLoungeTool.Part(g, "Board_Back", PrimitiveType.Cube, new Vector3(0f, 0f, 0.03f), new Vector3(4.0f, 0.85f, 0.06f), p.navy);
        OfficeLoungeTool.Part(g, "Board_EdgeTop", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.035f), new Vector3(4.1f, 0.035f, 0.07f), p.gold);
        OfficeLoungeTool.Part(g, "Board_EdgeBottom", PrimitiveType.Cube, new Vector3(0f, -0.45f, 0.035f), new Vector3(4.1f, 0.035f, 0.07f), p.gold);
        OfficeLoungeTool.Part(g, "Board_Strip", PrimitiveType.Cube, new Vector3(0f, -0.54f, 0.03f), new Vector3(3.4f, 0.025f, 0.03f), p.led);

        // The writing faces into the bank (west).
        Vector3 toward = Vector3.left;
        OfficeLoungeTool.Text(g, "Board_Title", "WELCOME TO THE BANK", new Vector3(WallFace - 0.07f, y + 0.1f, DoorZ), toward, 3.7f, 0.42f, p.white.color, true);
        OfficeLoungeTool.Text(g, "Board_Subtitle", "SAFE  •  SECURE  •  TRUSTED", new Vector3(WallFace - 0.07f, y - 0.2f, DoorZ), toward, 3.2f, 0.2f, new Color(0.95f, 0.78f, 0.35f), false);
    }

    // ---- Pictures --------------------------------------------------------------------------------------------------------------------

    // A gold frame with a cream mat and a coloured panel, size 1.8 x 1.25 (or "scale" times that).
    private static Transform Frame(Transform parent, OfficeLoungeTool.Palette p, string name, Vector3 position, float scale, Material background)
    {
        Transform g = OnWall(parent, name, position);
        OfficeLoungeTool.Part(g, "Picture_Frame", PrimitiveType.Cube, new Vector3(0f, 0f, 0.025f), new Vector3(1.8f * scale, 1.25f * scale, 0.05f), p.gold);
        OfficeLoungeTool.Part(g, "Picture_Inner", PrimitiveType.Cube, new Vector3(0f, 0f, 0.035f), new Vector3(1.72f * scale, 1.17f * scale, 0.04f), p.dark);
        OfficeLoungeTool.Part(g, "Picture_Mat", PrimitiveType.Cube, new Vector3(0f, 0f, 0.045f), new Vector3(1.66f * scale, 1.11f * scale, 0.02f), p.cream);
        OfficeLoungeTool.Part(g, "Picture_Art", PrimitiveType.Cube, new Vector3(0f, 0f, 0.055f), new Vector3(1.46f * scale, 0.91f * scale, 0.02f), background);
        return g;
    }

    // A growth chart: gold and cream bars getting taller, and an arrow rising over them, on navy.
    private static void BuildChartPicture(Transform root, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = Frame(root, p, "Picture_Growth", position, 1f, p.navy);

        float[] heights = { 0.2f, 0.32f, 0.44f, 0.58f, 0.74f };
        for (int i = 0; i < heights.Length; i++)
        {
            float h = heights[i];
            OfficeLoungeTool.Part(g, "Picture_Bar", PrimitiveType.Cube, new Vector3(-0.5f + i * 0.25f, -0.4f + h * 0.5f, 0.07f), new Vector3(0.17f, h, 0.02f), i % 2 == 0 ? p.gold : p.cream);
        }

        // The arrow: a slanted line and a head, rising to the right.
        GameObject line = OfficeLoungeTool.Part(g, "Picture_Arrow", PrimitiveType.Cube, new Vector3(-0.05f, 0.12f, 0.085f), new Vector3(1.0f, 0.035f, 0.01f), p.sky);
        line.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
        GameObject head = OfficeLoungeTool.Part(g, "Picture_ArrowHead", PrimitiveType.Cube, new Vector3(0.43f, 0.31f, 0.085f), new Vector3(0.16f, 0.16f, 0.01f), p.sky);
        head.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    // A round vault door: gold ring, steel and navy discs, spokes and a gold wheel in the middle, on a cream-blue panel.
    private static void BuildVaultPicture(Transform root, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = Frame(root, p, "Picture_Vault", position, 0.7f, p.sky);

        OfficeLoungeTool.Part(g, "Vault_Ring", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.07f), new Vector3(0.62f, 0.62f, 0.02f), p.gold);
        OfficeLoungeTool.Part(g, "Vault_Door", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.075f), new Vector3(0.56f, 0.56f, 0.02f), p.navy);
        OfficeLoungeTool.Part(g, "Vault_Disc", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.08f), new Vector3(0.42f, 0.42f, 0.02f), p.dark);

        for (int i = 0; i < 3; i++)
        {
            GameObject spoke = OfficeLoungeTool.Part(g, "Vault_Spoke", PrimitiveType.Cube, new Vector3(0f, 0f, 0.09f), new Vector3(0.4f, 0.03f, 0.01f), p.gold);
            spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
        }

        OfficeLoungeTool.Part(g, "Vault_Hub", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.095f), new Vector3(0.12f, 0.12f, 0.02f), p.gold);
    }

    // ---- Lamps and rug -------------------------------------------------------------------------------------------------------------------

    // A brass wall lamp with a warm shade and a dim light.
    private static void BuildSconce(Transform root, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = OnWall(root, "Sconce", position);
        OfficeLoungeTool.Part(g, "Sconce_Plate", PrimitiveType.Cube, new Vector3(0f, 0f, 0.015f), new Vector3(0.12f, 0.32f, 0.03f), p.gold);
        OfficeLoungeTool.Part(g, "Sconce_Shade", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.09f), new Vector3(0.13f, 0.14f, 0.13f), p.shade);
        OfficeLoungeTool.Part(g, "Sconce_Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0.09f), new Vector3(0.15f, 0.012f, 0.15f), p.gold);
        OfficeLoungeTool.Part(g, "Sconce_Cap", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0.09f), new Vector3(0.15f, 0.012f, 0.15f), p.gold);

        GameObject lightObject = new GameObject("Sconce_Light");
        lightObject.transform.SetParent(g, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, 0.3f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.88f, 0.68f);
        light.intensity = 0.35f;
        light.range = 3.2f;
        light.shadows = LightShadows.None;
    }

    // A rug just inside the door: gold border, navy, a cream line and a navy centre. Flat on the floor (no collider).
    private static void BuildRug(Transform root, OfficeLoungeTool.Palette p)
    {
        Transform g = OfficeLoungeTool.Group(root, "Rug", new Vector3(14.7f, F, DoorZ), 0f);
        OfficeLoungeTool.Part(g, "Rug_Border", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(3.0f, 0.02f, 2.6f), p.gold);
        OfficeLoungeTool.Part(g, "Rug_Navy", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(2.84f, 0.03f, 2.44f), p.navy);
        OfficeLoungeTool.Part(g, "Rug_Line", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(2.5f, 0.04f, 2.1f), p.cream);
        OfficeLoungeTool.Part(g, "Rug_Centre", PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(2.4f, 0.05f, 2.0f), p.navy);
    }
}

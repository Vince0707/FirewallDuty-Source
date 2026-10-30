using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Office > Decorate Entrance Lobby   (run it with Company_scene open)
// A few touches on the empty entrance wall inside the office, on both sides of the sliding door, in the same style as the lounge:
//   - a large framed picture on each side (a city skyline with the sun on the left, circles in the brand blues and gold on the right);
//   - two wall lamps beside each picture and a wooden bench with a navy cushion under it;
//   - a tall plant on each side of the door and one in each corner.
// Everything is simple shapes in one group, "Lobby_Decor". The walkway from the door to the reception, the navy panelling on the walls and the place where the
// sliding door opens are left as they are. Run it again to rebuild the group; Ctrl+Z removes it.
public static class OfficeLobbyTool
{
    private const string RootName = "Lobby_Decor";
    private const float F = 0.05f;            // the top of the floor
    private const float WallFace = -11.95f;   // the inside face of the front wall (the office is on the +z side)

    [MenuItem("Tools/Firewall Duty/Office/Decorate Entrance Lobby")]
    public static void Decorate()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Decorate Entrance Lobby", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
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

        if (!AssetDatabase.IsValidFolder("Assets/Materials/Lounge"))
            AssetDatabase.CreateFolder("Assets/Materials", "Lounge");

        GameObject rootObject = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Decorate Entrance Lobby");
        Transform root = rootObject.transform;

        OfficeLoungeTool.Palette p = new OfficeLoungeTool.Palette();

        foreach (float side in new[] { -1f, 1f })
        {
            // The picture, the bench under it and the two lamps beside it.
            if (side < 0f)
                BuildSkylinePicture(root, p, new Vector3(side * 6.0f, F + 1.95f, WallFace + 0.025f));
            else
                BuildCirclesPicture(root, p, new Vector3(side * 6.0f, F + 1.95f, WallFace + 0.025f));

            BuildBench(root, p, new Vector3(side * 6.0f, F, WallFace + 0.06f + 0.24f));
            BuildSconce(root, p, new Vector3(side * 4.55f, F + 2.3f, WallFace));
            BuildSconce(root, p, new Vector3(side * 7.45f, F + 2.3f, WallFace));

            // A tall plant beside the door (outside the stretch the sliding panels cover) and one in the corner.
            OfficeLoungeTool.BuildPlant(root, p, "Plant_Door", new Vector3(side * 3.6f, F, WallFace + 0.55f), 1.0f, true);
            OfficeLoungeTool.BuildPlant(root, p, "Plant_Corner", new Vector3(side * 14.3f, F, WallFace + 0.65f), 1.15f, true);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Decorate Entrance Lobby: built in '" + RootName + "' (" + rootObject.GetComponentsInChildren<Renderer>().Length +
                              " parts). Press Play and look at the entrance wall from inside. Save the scene (Ctrl+S). Use Edit > Undo to take it away.");
    }

    // A large frame with a mat, ready for the picture itself (it faces +z, into the room).
    private static Transform Frame(Transform parent, OfficeLoungeTool.Palette p, string name, Vector3 position, Material background)
    {
        Transform g = OfficeLoungeTool.Group(parent, name, position, 0f);
        OfficeLoungeTool.Part(g, "Picture_Frame", PrimitiveType.Cube, Vector3.zero, new Vector3(1.8f, 1.25f, 0.05f), p.dark);
        OfficeLoungeTool.Part(g, "Picture_Mat", PrimitiveType.Cube, new Vector3(0f, 0f, 0.02f), new Vector3(1.7f, 1.15f, 0.02f), p.cream);
        OfficeLoungeTool.Part(g, "Picture_Art", PrimitiveType.Cube, new Vector3(0f, 0f, 0.03f), new Vector3(1.5f, 0.95f, 0.02f), background);
        return g;
    }

    // A city at sunrise: the sky, a gold sun and a row of navy towers.
    private static void BuildSkylinePicture(Transform parent, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = Frame(parent, p, "Picture_Skyline", position, p.sky);
        OfficeLoungeTool.Part(g, "Picture_Sun", PrimitiveType.Sphere, new Vector3(0.38f, 0.17f, 0.045f), new Vector3(0.3f, 0.3f, 0.02f), p.gold);

        float[] heights = { 0.3f, 0.5f, 0.38f, 0.66f, 0.46f, 0.34f, 0.54f };
        for (int i = 0; i < heights.Length; i++)
        {
            float h = heights[i];
            OfficeLoungeTool.Part(g, "Picture_Tower", PrimitiveType.Cube, new Vector3(-0.6f + i * 0.2f, -0.475f + h * 0.5f, 0.045f), new Vector3(0.17f, h, 0.02f), i % 2 == 0 ? p.navy : p.sofa);
        }

        // A few lit windows.
        OfficeLoungeTool.Part(g, "Picture_Window", PrimitiveType.Cube, new Vector3(-0.2f, -0.2f, 0.058f), new Vector3(0.05f, 0.05f, 0.01f), p.gold);
        OfficeLoungeTool.Part(g, "Picture_Window", PrimitiveType.Cube, new Vector3(0.2f, -0.12f, 0.058f), new Vector3(0.05f, 0.05f, 0.01f), p.gold);
        OfficeLoungeTool.Part(g, "Picture_Window", PrimitiveType.Cube, new Vector3(0.0f, -0.28f, 0.058f), new Vector3(0.05f, 0.05f, 0.01f), p.white);
    }

    // Circles inside circles on navy: the brand blues with a gold ring.
    private static void BuildCirclesPicture(Transform parent, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = Frame(parent, p, "Picture_Circles", position, p.navy);
        OfficeLoungeTool.Part(g, "Picture_Ring", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.045f), new Vector3(0.88f, 0.88f, 0.02f), p.gold);
        OfficeLoungeTool.Part(g, "Picture_Disc", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.05f), new Vector3(0.82f, 0.82f, 0.02f), p.navy);
        OfficeLoungeTool.Part(g, "Picture_Disc", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.055f), new Vector3(0.64f, 0.64f, 0.02f), p.blue);
        OfficeLoungeTool.Part(g, "Picture_Disc", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.06f), new Vector3(0.44f, 0.44f, 0.02f), p.sky);
        OfficeLoungeTool.Part(g, "Picture_Disc", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.065f), new Vector3(0.22f, 0.22f, 0.02f), p.white);
        OfficeLoungeTool.Part(g, "Picture_Bar", PrimitiveType.Cube, new Vector3(-0.58f, 0f, 0.045f), new Vector3(0.05f, 0.7f, 0.01f), p.blue);
        OfficeLoungeTool.Part(g, "Picture_Bar", PrimitiveType.Cube, new Vector3(0.58f, 0f, 0.045f), new Vector3(0.05f, 0.7f, 0.01f), p.blue);
    }

    // A wooden bench with a navy cushion, 1.8 m long, with its back to the wall.
    private static void BuildBench(Transform parent, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = OfficeLoungeTool.Group(parent, "Bench", position, 0f);

        OfficeLoungeTool.Part(g, "Bench_Seat", PrimitiveType.Cube, new Vector3(0f, 0.44f, 0f), new Vector3(1.8f, 0.07f, 0.46f), p.lightWood);
        OfficeLoungeTool.Part(g, "Bench_Cushion", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.66f, 0.06f, 0.4f), p.navy);
        OfficeLoungeTool.Part(g, "Bench_Rail", PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.05f, 0.05f), p.dark);

        foreach (float x in new[] { -0.8f, 0.8f })
            foreach (float z in new[] { -0.18f, 0.18f })
                OfficeLoungeTool.Part(g, "Bench_Leg", PrimitiveType.Cube, new Vector3(x, 0.2f, z), new Vector3(0.05f, 0.4f, 0.05f), p.dark);

        OfficeLoungeTool.Solid(g, new Vector3(0f, 0.27f, 0f), new Vector3(1.8f, 0.54f, 0.46f));
    }

    // A wall lamp: a dark plate on the wall and a glowing shade.
    private static void BuildSconce(Transform parent, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = OfficeLoungeTool.Group(parent, "Sconce", position, 0f);
        OfficeLoungeTool.Part(g, "Sconce_Plate", PrimitiveType.Cube, new Vector3(0f, 0f, 0.015f), new Vector3(0.12f, 0.32f, 0.03f), p.dark);
        OfficeLoungeTool.Part(g, "Sconce_Shade", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.09f), new Vector3(0.13f, 0.14f, 0.13f), p.shade);
        OfficeLoungeTool.Part(g, "Sconce_Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0.09f), new Vector3(0.15f, 0.012f, 0.15f), p.gold);
        OfficeLoungeTool.Part(g, "Sconce_Cap", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0.09f), new Vector3(0.15f, 0.012f, 0.15f), p.gold);

        // The lamp really gives light: a small warm light a little out from the wall, so it washes the wall and the picture beside it.
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
}

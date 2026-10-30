using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Office > Decorate West Corridor   (run it with Company_scene open)
// The empty corridor on the west side of the office, between the wall at z = 0 and the meeting room's glass wall (x from -15 to -6.7), gets a few touches
// in the lounge style: a navy runner rug down the middle, a console table with a plant and a lamp on the long wall under a large "network" picture with a
// wall lamp on each side, and a tall plant at each end. The posters, the panelling and the meeting room door are left alone, and the space in front of the
// door stays free. Everything is simple shapes in one group, "Corridor_Decor". Run it again to rebuild the group; Ctrl+Z removes it.
public static class OfficeCorridorTool
{
    private const string RootName = "Corridor_Decor";
    private const float F = 0.05f;             // the top of the floor
    private const float WallFace = -0.05f;     // the corridor side (+z) of the wall at z = 0
    private const float PanelCap = 0.06f;      // how far the navy panelling reaches out from the wall

    [MenuItem("Tools/Firewall Duty/Office/Decorate West Corridor")]
    public static void Decorate()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Decorate West Corridor", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
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
        Undo.RegisterCreatedObjectUndo(rootObject, "Decorate West Corridor");
        Transform root = rootObject.transform;

        OfficeLoungeTool.Palette p = new OfficeLoungeTool.Palette();

        const float centreX = -11.0f;

        BuildRunner(root, p, new Vector3(centreX, F, 1.95f));
        BuildConsole(root, p, new Vector3(centreX, F, WallFace + PanelCap + 0.04f + 0.19f));
        BuildNetworkPicture(root, p, new Vector3(centreX, F + 1.95f, WallFace + 0.025f));
        BuildSconce(root, p, new Vector3(centreX - 1.4f, F + 2.0f, WallFace));
        BuildSconce(root, p, new Vector3(centreX + 1.4f, F + 2.0f, WallFace));

        // A tall plant at each end of the corridor, out of the way of the posters (west wall) and the meeting room door (x = -8.1, on the glass wall).
        OfficeLoungeTool.BuildPlant(root, p, "Plant_West", new Vector3(-14.5f, F, 3.5f), 1.1f, true);
        OfficeLoungeTool.BuildPlant(root, p, "Plant_East", new Vector3(-7.4f, F, WallFace + PanelCap + 0.3f), 1.0f, true);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Decorate West Corridor: built in '" + RootName + "' (" + rootObject.GetComponentsInChildren<Renderer>().Length +
                              " parts). Press Play and walk down the corridor. Save the scene (Ctrl+S). Use Edit > Undo to take it away.");
    }

    // A runner down the middle of the corridor: navy with a cream border and a gold line.
    private static void BuildRunner(Transform root, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = OfficeLoungeTool.Group(root, "Rug", position, 0f);
        OfficeLoungeTool.Part(g, "Rug_Outer", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(5.6f, 0.02f, 1.5f), p.navy);
        OfficeLoungeTool.Part(g, "Rug_Border", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(5.4f, 0.03f, 1.3f), p.cream);
        OfficeLoungeTool.Part(g, "Rug_Inner", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(5.24f, 0.04f, 1.14f), p.navy);
        OfficeLoungeTool.Part(g, "Rug_Line", PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(5.0f, 0.05f, 0.9f), p.gold);
        OfficeLoungeTool.Part(g, "Rug_Centre", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(4.96f, 0.06f, 0.86f), p.navy);
    }

    // A walnut console table with a lower shelf, a small plant, a lamp and a few books. Its back is to the wall (it faces +z).
    private static void BuildConsole(Transform root, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = OfficeLoungeTool.Group(root, "Console", position, 0f);

        foreach (float x in new[] { -0.64f, 0.64f })
            foreach (float z in new[] { -0.16f, 0.16f })
                OfficeLoungeTool.Part(g, "Console_Leg", PrimitiveType.Cube, new Vector3(x, 0.38f, z), new Vector3(0.04f, 0.76f, 0.04f), p.dark);

        OfficeLoungeTool.Part(g, "Console_Top", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0f), new Vector3(1.4f, 0.04f, 0.38f), p.walnut);
        OfficeLoungeTool.Part(g, "Console_Shelf", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(1.28f, 0.025f, 0.32f), p.lightWood);

        OfficeLoungeTool.BuildPlant(g, p, "Console_Plant", new Vector3(-0.42f, 0.8f, 0f), 0.3f, false);

        OfficeLoungeTool.Part(g, "Console_LampBase", PrimitiveType.Cylinder, new Vector3(0.45f, 0.805f, 0f), new Vector3(0.14f, 0.01f, 0.14f), p.gold);
        OfficeLoungeTool.Part(g, "Console_LampStem", PrimitiveType.Cylinder, new Vector3(0.45f, 0.97f, 0f), new Vector3(0.025f, 0.16f, 0.025f), p.gold);
        OfficeLoungeTool.Part(g, "Console_LampShade", PrimitiveType.Cylinder, new Vector3(0.45f, 1.17f, 0f), new Vector3(0.26f, 0.1f, 0.26f), p.shade);

        OfficeLoungeTool.Part(g, "Console_BookA", PrimitiveType.Cube, new Vector3(0.1f, 0.825f, 0f), new Vector3(0.3f, 0.05f, 0.22f), p.books[1]).transform.localRotation = Quaternion.Euler(0f, 10f, 0f);
        OfficeLoungeTool.Part(g, "Console_BookB", PrimitiveType.Cube, new Vector3(0.1f, 0.87f, 0f), new Vector3(0.25f, 0.04f, 0.18f), p.books[2]).transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
        OfficeLoungeTool.Part(g, "Console_BoxA", PrimitiveType.Cube, new Vector3(-0.1f, 0.3f, 0f), new Vector3(0.3f, 0.07f, 0.2f), p.cream);
        OfficeLoungeTool.Part(g, "Console_BoxB", PrimitiveType.Cube, new Vector3(0.35f, 0.31f, 0f), new Vector3(0.22f, 0.09f, 0.2f), p.blue);

        OfficeLoungeTool.Solid(g, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 0.38f));
    }

    // A large picture of a network: navy with glowing nodes and the lines between them (a nod to the game), in a gold frame.
    private static void BuildNetworkPicture(Transform root, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = OfficeLoungeTool.Group(root, "Picture_Network", position, 0f);

        OfficeLoungeTool.Part(g, "Picture_Frame", PrimitiveType.Cube, new Vector3(0f, 0f, 0.025f), new Vector3(1.8f, 1.25f, 0.05f), p.gold);
        OfficeLoungeTool.Part(g, "Picture_Inner", PrimitiveType.Cube, new Vector3(0f, 0f, 0.035f), new Vector3(1.72f, 1.17f, 0.04f), p.dark);
        OfficeLoungeTool.Part(g, "Picture_Mat", PrimitiveType.Cube, new Vector3(0f, 0f, 0.045f), new Vector3(1.66f, 1.11f, 0.02f), p.cream);
        OfficeLoungeTool.Part(g, "Picture_Art", PrimitiveType.Cube, new Vector3(0f, 0f, 0.055f), new Vector3(1.46f, 0.91f, 0.02f), p.navy);

        Vector2[] nodes =
        {
            new Vector2(-0.55f, 0.18f), new Vector2(-0.25f, -0.22f), new Vector2(0f, 0.05f), new Vector2(0.28f, 0.28f), new Vector2(0.5f, -0.12f), new Vector2(0.62f, 0.3f)
        };

        // Lines first (under the nodes): a chain through the nodes and two cross links.
        int[,] links = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 4 }, { 3, 5 }, { 0, 2 }, { 2, 4 } };
        for (int i = 0; i < links.GetLength(0); i++)
            Line(g, p.sky, nodes[links[i, 0]], nodes[links[i, 1]], 0.07f);

        for (int i = 0; i < nodes.Length; i++)
        {
            float size = i == 2 ? 0.2f : 0.13f;
            OfficeLoungeTool.Part(g, "Picture_Node", PrimitiveType.Sphere, new Vector3(nodes[i].x, nodes[i].y, 0.085f), new Vector3(size, size, 0.02f), i == 2 ? p.gold : p.blue);
            OfficeLoungeTool.Part(g, "Picture_NodeCore", PrimitiveType.Sphere, new Vector3(nodes[i].x, nodes[i].y, 0.095f), new Vector3(size * 0.45f, size * 0.45f, 0.02f), p.white);
        }
    }

    // A thin line between two points of the picture (in its own plane).
    private static void Line(Transform picture, Material material, Vector2 from, Vector2 to, float z)
    {
        Vector2 delta = to - from;
        GameObject line = OfficeLoungeTool.Part(picture, "Picture_Link", PrimitiveType.Cube, new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, z), new Vector3(delta.magnitude, 0.018f, 0.01f), material);
        line.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // A wall lamp: a gold plate and a glowing shade.
    private static void BuildSconce(Transform root, OfficeLoungeTool.Palette p, Vector3 position)
    {
        Transform g = OfficeLoungeTool.Group(root, "Sconce", position, 0f);
        OfficeLoungeTool.Part(g, "Sconce_Plate", PrimitiveType.Cube, new Vector3(0f, 0f, 0.015f), new Vector3(0.12f, 0.32f, 0.03f), p.gold);
        OfficeLoungeTool.Part(g, "Sconce_Shade", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.09f), new Vector3(0.13f, 0.14f, 0.13f), p.shade);
        OfficeLoungeTool.Part(g, "Sconce_Cap", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0.09f), new Vector3(0.15f, 0.012f, 0.15f), p.gold);
        OfficeLoungeTool.Part(g, "Sconce_Cap", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0.09f), new Vector3(0.15f, 0.012f, 0.15f), p.gold);
    }
}

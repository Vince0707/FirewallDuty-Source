using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Office > Decorate Empty Hall   (run it with Company_scene open)
// The big empty space on the east side of the office (between the IT room and the Head Manager's room, from x = 7 to 15 and z = -5 to 4) becomes a
// waiting lounge in the Firewall Duty colours:
//   - a navy rug with a light border under a sofa, two armchairs and a round coffee table (books, a vase);
//   - three bookcases along the north wall, full of books, boxes and a plant; a credenza with a lamp along the south wall;
//   - navy wall panelling on the east and south walls (below your posters, which are not touched), a big "FIREWALL DUTY" sign above them and three
//     framed pictures over the sofa;
//   - a reading corner with a side table and two poufs, a floor lamp, and tall plants;
//   - one warm light over the seating.
// Everything is made of simple shapes in one group, "Lounge_Decor". The doorway of the Head Manager's room and the walkway to it stay free, and nothing
// that was already in the office is moved or changed. Run it again to rebuild the group; Ctrl+Z removes it.
public static class OfficeLoungeTool
{
    private const string RootName = "Lounge_Decor";
    private const string MaterialFolder = "Assets/Materials/Lounge";
    private const float F = 0.05f;   // the top of the office floor

    // The free space: its walls' inner faces.
    private const float WestX = 7.15f, EastFace = 14.95f, SouthFace = -4.95f, NorthFace = 3.95f;

    [MenuItem("Tools/Firewall Duty/Office/Decorate Empty Hall")]
    public static void Decorate()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Decorate Empty Hall", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
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

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Lounge");

        GameObject rootObject = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Decorate Empty Hall");
        Transform root = rootObject.transform;

        Palette p = new Palette();

        BuildRug(root, p);
        // (The navy panelling on the walls is made for the whole office by Office > Add Wall Panelling; run it after this.)
        BuildSeating(root, p);
        BuildBookcases(root, p);
        BuildSouthWall(root, p);
        BuildEastWall(root, p);
        BuildReadingCorner(root, p);
        BuildPlantsAndLamps(root, p);

        // One warm light over the seating (a real light, so the room feels cosy; everything else is only coloured).
        GameObject lightObject = new GameObject("Lounge_Glow");
        lightObject.transform.SetParent(root, false);
        lightObject.transform.position = new Vector3(11.9f, 3.1f, -1.6f);
        Light glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.92f, 0.8f);
        glow.intensity = 1.1f;
        glow.range = 7f;
        glow.shadows = LightShadows.None;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Decorate Empty Hall: the lounge is built in '" + RootName + "' (" + rootObject.GetComponentsInChildren<Renderer>().Length +
                              " parts). Press Play and walk in from the main hall. Save the scene (Ctrl+S). Use Edit > Undo to take it away.");
    }

    // ---- The three posters on the east wall --------------------------------------------------------------------------------------

    // Tools > Firewall Duty > Office > Center Lounge Posters
    // The three posters on the east wall of the lounge hung off to one side, under the end of the "FIREWALL DUTY" sign. They are moved so the middle one is
    // under the middle of the sign, 1.6 m apart (as before). Only their position along the wall changes, not their height or which wall they are on.
    [MenuItem("Tools/Firewall Duty/Office/Center Lounge Posters")]
    public static void CenterPosters()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Center Lounge Posters", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        float centre = (NorthFace + SouthFace) * 0.5f;   // the middle of the sign and of the wall: z = -0.5
        string[] names = { "Poster_Monitor_14", "Poster_Brand_13", "Poster_Lock_12" };   // from the north end to the south end
        float spacing = 1.6f;
        int moved = 0;

        for (int i = 0; i < names.Length; i++)
        {
            GameObject poster = FindByName(names[i]);
            if (poster == null)
            {
                UnityEngine.Debug.LogWarning("Center Lounge Posters: '" + names[i] + "' was not found, so it was left out.");
                continue;
            }

            float z = centre + (1 - i) * spacing;   // north = +z
            Vector3 position = poster.transform.position;

            if (Mathf.Abs(position.z - z) < 0.001f)
                continue;

            Undo.RecordObject(poster.transform, "Center Lounge Posters");
            poster.transform.position = new Vector3(position.x, position.y, z);
            EditorUtility.SetDirty(poster.transform);
            moved++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("Center Lounge Posters: " + moved + " poster(s) moved so the middle one is under the middle of the sign. Save the scene (Ctrl+S). Use Edit > Undo to take it back.");
    }

    private static GameObject FindByName(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name)
                return t.gameObject;
        }

        return null;
    }

    // ---- Materials ---------------------------------------------------------------------------------------------------------------

    internal class Palette
    {
        public readonly Material navy = Mat("Navy", new Color(0.09f, 0.2f, 0.38f), 0.2f);
        public readonly Material sky = Mat("Sky", new Color(0.62f, 0.8f, 0.96f), 0.15f);
        public readonly Material blue = Mat("Blue", new Color(0.18f, 0.48f, 0.88f), 0.3f);
        public readonly Material sofa = Mat("Sofa", new Color(0.16f, 0.38f, 0.62f), 0.15f);
        public readonly Material cushion = Mat("Cushion", new Color(0.3f, 0.57f, 0.86f), 0.1f);
        public readonly Material dark = Mat("Dark", new Color(0.1f, 0.12f, 0.16f), 0.35f);
        public readonly Material walnut = Mat("Walnut", new Color(0.42f, 0.27f, 0.16f), 0.3f);
        public readonly Material lightWood = Mat("LightWood", new Color(0.74f, 0.57f, 0.4f), 0.3f);
        public readonly Material cream = Mat("Cream", new Color(0.95f, 0.92f, 0.84f), 0.15f);
        public readonly Material white = Mat("White", new Color(0.96f, 0.96f, 0.97f), 0.2f);
        public readonly Material green1 = Mat("Green1", new Color(0.18f, 0.5f, 0.24f), 0.1f);
        public readonly Material green2 = Mat("Green2", new Color(0.32f, 0.64f, 0.3f), 0.1f);
        public readonly Material pot = Mat("Pot", new Color(0.62f, 0.34f, 0.22f), 0.2f);
        public readonly Material gold = Mat("Gold", new Color(0.92f, 0.72f, 0.28f), 0.7f);
        public readonly Material teal = Mat("Teal", new Color(0.15f, 0.6f, 0.58f), 0.2f);
        public readonly Material coral = Mat("Coral", new Color(0.9f, 0.42f, 0.38f), 0.2f);
        public readonly Material led = Emissive("Led", new Color(0.35f, 0.75f, 1f));
        public readonly Material shade = Emissive("Shade", new Color(1f, 0.9f, 0.7f));
        public readonly Material[] books =
        {
            Mat("Book1", new Color(0.75f, 0.2f, 0.2f), 0.2f), Mat("Book2", new Color(0.2f, 0.35f, 0.7f), 0.2f), Mat("Book3", new Color(0.9f, 0.75f, 0.25f), 0.2f),
            Mat("Book4", new Color(0.22f, 0.55f, 0.35f), 0.2f), Mat("Book5", new Color(0.92f, 0.9f, 0.85f), 0.2f), Mat("Book6", new Color(0.45f, 0.3f, 0.5f), 0.2f),
            Mat("Book7", new Color(0.85f, 0.5f, 0.2f), 0.2f), Mat("Book8", new Color(0.15f, 0.2f, 0.3f), 0.2f)
        };
    }

    // ---- Rug and panelling -------------------------------------------------------------------------------------------------------

    private static void BuildRug(Transform root, Palette p)
    {
        Transform g = Group(root, "Rug", new Vector3(11.9f, F, -2.8f), 0f);
        Part(g, "Rug_Outer", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(5.6f, 0.02f, 3.4f), p.navy);
        Part(g, "Rug_Border", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(5.3f, 0.03f, 3.1f), p.sky);
        Part(g, "Rug_Inner", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(4.9f, 0.04f, 2.7f), p.navy);
        Part(g, "Rug_Stripe", PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(4.2f, 0.05f, 2.0f), p.blue);
        Part(g, "Rug_Centre", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(3.9f, 0.06f, 1.7f), p.navy);
    }

    // ---- Seating ------------------------------------------------------------------------------------------------------------------

    private static void BuildSeating(Transform root, Palette p)
    {
        // The sofa has its back to the south wall (and the panelling), facing north.
        BuildSofa(root, p, "Sofa", new Vector3(12.0f, F, SouthFace + 0.04f + 0.01f + 0.475f), 0f, 3.2f, 3);

        // An armchair on each side of the coffee table.
        BuildSofa(root, p, "Armchair_West", new Vector3(9.85f, F, -2.85f), 90f, 1.0f, 1);
        BuildSofa(root, p, "Armchair_East", new Vector3(14.1f, F, -2.85f), 270f, 1.0f, 1);

        // The round coffee table.
        Transform table = Group(root, "CoffeeTable", new Vector3(12.0f, F, -2.85f), 0f);
        Part(table, "Table_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.55f, 0.015f, 0.55f), p.dark);
        Part(table, "Table_Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.1f, 0.2f, 0.1f), p.dark);
        Part(table, "Table_Top", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(1.15f, 0.02f, 1.15f), p.walnut);
        Part(table, "Table_Rim", PrimitiveType.Cylinder, new Vector3(0f, 0.405f, 0f), new Vector3(1.18f, 0.012f, 1.18f), p.gold);
        Solid(table, new Vector3(0f, 0.22f, 0f), new Vector3(1.15f, 0.44f, 1.15f));

        // Two books and a small vase with a plant.
        Part(table, "Table_BookA", PrimitiveType.Cube, new Vector3(-0.25f, 0.465f, 0.12f), new Vector3(0.34f, 0.045f, 0.24f), p.books[1]).transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
        Part(table, "Table_BookB", PrimitiveType.Cube, new Vector3(-0.24f, 0.51f, 0.12f), new Vector3(0.28f, 0.04f, 0.2f), p.books[2]).transform.localRotation = Quaternion.Euler(0f, -8f, 0f);
        BuildPlant(table, p, "Table_Plant", new Vector3(0.22f, 0.43f, -0.1f), 0.28f, false);
    }

    // A sofa or armchair built facing +z with its back at -z, then turned by yaw. "seats" is the number of seat cushions.
    private static void BuildSofa(Transform parent, Palette p, string name, Vector3 position, float yaw, float length, int seats)
    {
        Transform g = Group(parent, name, position, yaw);
        const float depth = 0.95f;
        const float arm = 0.22f;
        float inner = length - 2f * arm;
        float seatWidth = inner / seats;

        Part(g, "Sofa_Base", PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(length, 0.28f, depth), p.dark);
        Part(g, "Sofa_Back", PrimitiveType.Cube, new Vector3(0f, 0.63f, -depth * 0.5f + 0.11f), new Vector3(inner, 0.7f, 0.22f), p.sofa);

        foreach (float side in new[] { -1f, 1f })
            Part(g, "Sofa_Arm", PrimitiveType.Cube, new Vector3(side * (length * 0.5f - arm * 0.5f), 0.33f, 0f), new Vector3(arm, 0.66f, depth), p.sofa);

        for (int i = 0; i < seats; i++)
        {
            float x = -inner * 0.5f + seatWidth * (i + 0.5f);
            Part(g, "Sofa_Seat", PrimitiveType.Cube, new Vector3(x, 0.36f, 0.11f), new Vector3(seatWidth - 0.03f, 0.17f, depth - 0.24f), p.cushion);
            Part(g, "Sofa_BackCushion", PrimitiveType.Cube, new Vector3(x, 0.66f, -depth * 0.5f + 0.29f), new Vector3(seatWidth - 0.05f, 0.42f, 0.14f), p.cushion);
        }

        Solid(g, new Vector3(0f, 0.49f, 0f), new Vector3(length, 0.98f, depth));
    }

    // ---- Bookcases along the north wall -------------------------------------------------------------------------------------------

    private static void BuildBookcases(Transform root, Palette p)
    {
        // From x = 10.15 to 14.65: the Head Manager's doorway (x 8.0 to 9.4) and the walkway to it stay free.
        System.Random random = new System.Random(24);
        float[] centres = { 10.9f, 12.4f, 13.9f };

        for (int i = 0; i < centres.Length; i++)
            BuildBookcase(root, p, "Bookcase_" + (i + 1), new Vector3(centres[i], F, NorthFace), 1.5f, random, i == 1);
    }

    // Built facing +z with its back plane at z = 0, then turned to face south (into the room).
    private static void BuildBookcase(Transform parent, Palette p, string name, Vector3 position, float width, System.Random random, bool withPlant)
    {
        Transform g = Group(parent, name, position, 180f);
        const float depth = 0.4f;
        const float height = 2.3f;

        Part(g, "Case_Back", PrimitiveType.Cube, new Vector3(0f, height * 0.5f, 0.02f), new Vector3(width, height, 0.04f), p.walnut);

        foreach (float side in new[] { -1f, 1f })
            Part(g, "Case_Side", PrimitiveType.Cube, new Vector3(side * (width * 0.5f - 0.02f), height * 0.5f, depth * 0.5f), new Vector3(0.04f, height, depth), p.walnut);

        float[] shelves = { 0.62f, 1.18f, 1.74f, 2.3f };
        Part(g, "Case_Plinth", PrimitiveType.Cube, new Vector3(0f, 0.03f, depth * 0.5f), new Vector3(width - 0.08f, 0.06f, depth - 0.02f), p.dark);

        foreach (float y in shelves)
            Part(g, "Case_Shelf", PrimitiveType.Cube, new Vector3(0f, y - 0.02f, depth * 0.5f + 0.01f), new Vector3(width - 0.08f, 0.04f, depth - 0.02f), p.walnut);

        // The lower part is a cupboard with two doors.
        foreach (float side in new[] { -1f, 1f })
        {
            Part(g, "Case_Door", PrimitiveType.Cube, new Vector3(side * (width * 0.25f - 0.01f), 0.34f, depth - 0.005f), new Vector3(width * 0.5f - 0.08f, 0.5f, 0.02f), p.lightWood);
            Part(g, "Case_Handle", PrimitiveType.Cube, new Vector3(side * 0.06f, 0.4f, depth + 0.02f), new Vector3(0.02f, 0.12f, 0.02f), p.gold);
        }

        // Books, boxes and a few ornaments on the three open shelves.
        for (int s = 0; s < 3; s++)
        {
            float shelfTop = shelves[s];
            float x = -width * 0.5f + 0.08f;

            while (x < width * 0.5f - 0.1f)
            {
                double roll = random.NextDouble();
                float w = 0.04f + (float)random.NextDouble() * 0.06f;

                if (x + w > width * 0.5f - 0.07f)
                    break;

                if (roll < 0.1)
                {
                    x += 0.12f;   // a gap
                    continue;
                }

                if (roll < 0.16)
                {
                    // A small storage box.
                    Part(g, "Case_Box", PrimitiveType.Cube, new Vector3(x + 0.1f, shelfTop + 0.09f, depth * 0.5f), new Vector3(0.2f, 0.18f, 0.22f), p.cream);
                    Part(g, "Case_BoxLid", PrimitiveType.Cube, new Vector3(x + 0.1f, shelfTop + 0.185f, depth * 0.5f), new Vector3(0.21f, 0.025f, 0.23f), p.blue);
                    x += 0.23f;
                    continue;
                }

                float h = 0.2f + (float)random.NextDouble() * 0.16f;
                float d = 0.22f + (float)random.NextDouble() * 0.06f;
                Material colour = p.books[random.Next(p.books.Length)];
                Part(g, "Case_Book", PrimitiveType.Cube, new Vector3(x + w * 0.5f, shelfTop + h * 0.5f, depth * 0.5f - 0.02f), new Vector3(w, h, d), colour);
                x += w + 0.005f;
            }
        }

        if (withPlant)
            BuildPlant(g, p, "Case_Plant", new Vector3(0f, height, depth * 0.5f), 0.45f, false);

        Solid(g, new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, depth));
    }

    // ---- South wall: pictures and the credenza --------------------------------------------------------------------------------------

    private static void BuildSouthWall(Transform root, Palette p)
    {
        // Three framed pictures over the sofa.
        Material[] colours = { p.sky, p.teal, p.gold };
        Material[] shapes = { p.white, p.white, p.coral };
        float z = SouthFace + 0.025f;

        for (int i = 0; i < 3; i++)
        {
            float x = 10.85f + i * 1.15f;
            Part(root, "Picture_Frame", PrimitiveType.Cube, new Vector3(x, F + 1.95f, z), new Vector3(0.92f, 0.67f, 0.05f), p.dark);
            Part(root, "Picture_Mat", PrimitiveType.Cube, new Vector3(x, F + 1.95f, z + 0.02f), new Vector3(0.84f, 0.59f, 0.02f), p.cream);
            Part(root, "Picture_Art", PrimitiveType.Cube, new Vector3(x, F + 1.95f, z + 0.03f), new Vector3(0.7f, 0.45f, 0.02f), colours[i]);
            Part(root, "Picture_Sun", PrimitiveType.Sphere, new Vector3(x + (i - 1) * 0.12f, F + 1.97f, z + 0.045f), new Vector3(0.26f, 0.26f, 0.02f), shapes[i]);
            Part(root, "Picture_Hill", PrimitiveType.Sphere, new Vector3(x, F + 1.8f, z + 0.045f), new Vector3(0.7f, 0.2f, 0.02f), p.navy);
        }

        // The credenza at the west end of the south wall: a lamp, a plant and a few books on top.
        Transform c = Group(root, "Credenza", new Vector3(8.4f, F, SouthFace + 0.04f + 0.01f + 0.25f), 0f);

        foreach (float sx in new[] { -0.7f, 0.7f })
            foreach (float sz in new[] { -0.2f, 0.2f })
                Part(c, "Credenza_Leg", PrimitiveType.Cylinder, new Vector3(sx, 0.05f, sz), new Vector3(0.05f, 0.05f, 0.05f), p.dark);

        Part(c, "Credenza_Body", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.7f, 0.5f), p.walnut);
        Part(c, "Credenza_Top", PrimitiveType.Cube, new Vector3(0f, 0.815f, 0f), new Vector3(1.66f, 0.03f, 0.54f), p.lightWood);

        foreach (float side in new[] { -1f, 1f })
        {
            Part(c, "Credenza_Door", PrimitiveType.Cube, new Vector3(side * 0.39f, 0.45f, 0.255f), new Vector3(0.76f, 0.6f, 0.02f), p.lightWood);
            Part(c, "Credenza_Handle", PrimitiveType.Cube, new Vector3(side * 0.06f, 0.5f, 0.275f), new Vector3(0.02f, 0.14f, 0.02f), p.gold);
        }

        Part(c, "Credenza_BookA", PrimitiveType.Cube, new Vector3(0.4f, 0.855f, 0f), new Vector3(0.3f, 0.05f, 0.22f), p.books[3]).transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        Part(c, "Credenza_BookB", PrimitiveType.Cube, new Vector3(0.4f, 0.9f, 0f), new Vector3(0.25f, 0.04f, 0.18f), p.books[0]).transform.localRotation = Quaternion.Euler(0f, -10f, 0f);
        BuildPlant(c, p, "Credenza_Plant", new Vector3(-0.4f, 0.83f, 0f), 0.32f, false);

        Part(c, "Credenza_LampBase", PrimitiveType.Cylinder, new Vector3(0.05f, 0.835f, 0.05f), new Vector3(0.14f, 0.01f, 0.14f), p.gold);
        Part(c, "Credenza_LampStem", PrimitiveType.Cylinder, new Vector3(0.05f, 1.0f, 0.05f), new Vector3(0.025f, 0.17f, 0.025f), p.gold);
        Part(c, "Credenza_LampShade", PrimitiveType.Cylinder, new Vector3(0.05f, 1.2f, 0.05f), new Vector3(0.26f, 0.1f, 0.26f), p.shade);
        Solid(c, new Vector3(0f, 0.42f, 0f), new Vector3(1.66f, 0.84f, 0.54f));
    }

    // ---- East wall: the sign ----------------------------------------------------------------------------------------------------------

    private static void BuildEastWall(Transform root, Palette p)
    {
        float centreZ = (NorthFace + SouthFace) * 0.5f;
        float x = EastFace - 0.03f;
        float y = F + 3.1f;

        Part(root, "Sign_Board", PrimitiveType.Cube, new Vector3(x, y, centreZ), new Vector3(0.06f, 0.75f, 4.2f), p.navy);
        Part(root, "Sign_Edge", PrimitiveType.Cube, new Vector3(x - 0.01f, y + 0.4f, centreZ), new Vector3(0.05f, 0.03f, 4.3f), p.gold);
        Part(root, "Sign_Edge", PrimitiveType.Cube, new Vector3(x - 0.01f, y - 0.4f, centreZ), new Vector3(0.05f, 0.03f, 4.3f), p.gold);
        Part(root, "Sign_Strip", PrimitiveType.Cube, new Vector3(x - 0.01f, y - 0.5f, centreZ), new Vector3(0.03f, 0.025f, 3.6f), p.led);

        // The writing faces west, into the room.
        Vector3 towardReader = Vector3.left;
        Text(root, "Sign_Name", "FIREWALL DUTY", new Vector3(x - 0.04f, y + 0.08f, centreZ), towardReader, 4.0f, 0.42f, p.white.color, true);
        Text(root, "Sign_Tagline", "MONITOR  •  DETECT  •  DEFEND", new Vector3(x - 0.04f, y - 0.2f, centreZ), towardReader, 3.6f, 0.2f, new Color(0.62f, 0.8f, 0.96f), false);
    }

    // ---- Reading corner --------------------------------------------------------------------------------------------------------------------

    private static void BuildReadingCorner(Transform root, Palette p)
    {
        Transform t = Group(root, "SideTable", new Vector3(14.25f, F, 1.0f), 0f);
        Part(t, "Side_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(0.38f, 0.01f, 0.38f), p.dark);
        Part(t, "Side_Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.26f, 0f), new Vector3(0.06f, 0.26f, 0.06f), p.dark);
        Part(t, "Side_Top", PrimitiveType.Cylinder, new Vector3(0f, 0.53f, 0f), new Vector3(0.58f, 0.02f, 0.58f), p.walnut);
        Part(t, "Side_Saucer", PrimitiveType.Cylinder, new Vector3(0.05f, 0.56f, 0.04f), new Vector3(0.17f, 0.005f, 0.17f), p.white);
        Part(t, "Side_Mug", PrimitiveType.Cylinder, new Vector3(0.05f, 0.605f, 0.04f), new Vector3(0.09f, 0.045f, 0.09f), p.coral);
        Part(t, "Side_Magazine", PrimitiveType.Cube, new Vector3(-0.12f, 0.555f, -0.06f), new Vector3(0.24f, 0.012f, 0.17f), p.teal).transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        Solid(t, new Vector3(0f, 0.28f, 0f), new Vector3(0.58f, 0.56f, 0.58f));

        BuildPouf(root, p, "Pouf_1", new Vector3(13.35f, F, 1.75f), p.teal);
        BuildPouf(root, p, "Pouf_2", new Vector3(13.5f, F, 0.2f), p.gold);
    }

    private static void BuildPouf(Transform root, Palette p, string name, Vector3 position, Material colour)
    {
        Transform g = Group(root, name, position, 0f);
        Part(g, "Pouf_Body", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.2f, 0.5f), colour);
        Part(g, "Pouf_Top", PrimitiveType.Cylinder, new Vector3(0f, 0.405f, 0f), new Vector3(0.44f, 0.012f, 0.44f), p.cream);
        Solid(g, new Vector3(0f, 0.21f, 0f), new Vector3(0.5f, 0.42f, 0.5f));
    }

    // ---- Plants and lamps ----------------------------------------------------------------------------------------------------------------------

    private static void BuildPlantsAndLamps(Transform root, Palette p)
    {
        // Tall plants: by the Head Manager's wall (beside the walkway) and in the south-east corner.
        BuildPlant(root, p, "Plant_North", new Vector3(7.55f, F, NorthFace - 0.4f), 1.0f, true);
        BuildPlant(root, p, "Plant_Corner", new Vector3(14.5f, F, SouthFace + 0.5f), 1.1f, true);

        // A floor lamp between the east armchair and the reading corner.
        Transform l = Group(root, "FloorLamp", new Vector3(14.45f, F, -1.55f), 0f);
        Part(l, "Lamp_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.3f, 0.015f, 0.3f), p.dark);
        Part(l, "Lamp_Pole", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), new Vector3(0.03f, 0.8f, 0.03f), p.gold);
        Part(l, "Lamp_Shade", PrimitiveType.Cylinder, new Vector3(0f, 1.7f, 0f), new Vector3(0.42f, 0.17f, 0.42f), p.shade);
        Solid(l, new Vector3(0f, 0.85f, 0f), new Vector3(0.3f, 1.7f, 0.3f));
    }

    // A potted plant: a pot, soil, a trunk and a bush of leaf balls. Built at "position" (the bottom of the pot), scaled by "scale" (1 is about 1.7 m tall).
    internal static void BuildPlant(Transform parent, Palette p, string name, Vector3 position, float scale, bool solid)
    {
        Transform g = Group(parent, name, position, 0f, true);
        float s = scale;

        Part(g, "Plant_Pot", PrimitiveType.Cylinder, new Vector3(0f, 0.22f * s, 0f), new Vector3(0.42f * s, 0.22f * s, 0.42f * s), p.pot);
        Part(g, "Plant_PotRim", PrimitiveType.Cylinder, new Vector3(0f, 0.43f * s, 0f), new Vector3(0.46f * s, 0.02f * s, 0.46f * s), p.pot);
        Part(g, "Plant_Soil", PrimitiveType.Cylinder, new Vector3(0f, 0.44f * s, 0f), new Vector3(0.38f * s, 0.01f * s, 0.38f * s), p.dark);
        Part(g, "Plant_Trunk", PrimitiveType.Cylinder, new Vector3(0f, 0.84f * s, 0f), new Vector3(0.06f * s, 0.4f * s, 0.06f * s), p.walnut);

        float top = 1.2f * s;
        Vector4[] leaves =
        {
            new Vector4(0f, 0.15f, 0f, 0.7f), new Vector4(0.24f, -0.02f, 0.1f, 0.5f), new Vector4(-0.22f, -0.05f, -0.12f, 0.5f),
            new Vector4(0.06f, 0.32f, -0.2f, 0.45f), new Vector4(-0.1f, 0.26f, 0.22f, 0.45f)
        };

        for (int i = 0; i < leaves.Length; i++)
        {
            Vector4 leaf = leaves[i];
            Part(g, "Plant_Leaves", PrimitiveType.Sphere, new Vector3(leaf.x * s, top + leaf.y * s, leaf.z * s), Vector3.one * (leaf.w * s), i % 2 == 0 ? p.green1 : p.green2);
        }

        if (solid)
            Solid(g, new Vector3(0f, 0.5f * s, 0f), new Vector3(0.45f * s, 1f * s, 0.45f * s));
    }

    // ---- Small pieces ----------------------------------------------------------------------------------------------------------------------------

    // An empty object at "position" (world; the lounge group sits at the origin), turned by yaw. A child of another piece uses a local position.
    internal static Transform Group(Transform parent, string name, Vector3 position, float yaw, bool local = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        if (local)
            go.transform.localPosition = position;
        else
            go.transform.position = position;

        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return go.transform;
    }

    internal static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        Collider existing = go.GetComponent<Collider>();
        if (existing != null)
            Object.DestroyImmediate(existing);

        return go;
    }

    // An invisible solid box: the pieces themselves have no colliders, so the player is stopped by one simple box per piece of furniture.
    internal static void Solid(Transform parent, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject("Solid");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = size;
    }

    // White writing on a wall; "towardReader" is the direction from the wall to whoever reads it.
    internal static void Text(Transform parent, string name, string text, Vector3 position, Vector3 towardReader, float width, float height, Color color, bool bold)
    {
        GameObject go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(-towardReader);   // TextMeshPro reads correctly when its forward points away from the reader

        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 20f;
        tmp.rectTransform.sizeDelta = new Vector2(width, height);

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        MeshRenderer meshRenderer = tmp.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    internal static Material Mat(string name, Color color, float smoothness)
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

    internal static Material Emissive(string name, Color color)
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
}

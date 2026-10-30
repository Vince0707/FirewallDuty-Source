using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Set Up Branding And Wall Design
// Gives the office the Firewall Duty look, without touching anything you placed yourself:
//   - the logo you supplied (Assets/UI/FirewallDutyLogo_Original.png) is cleaned up (white background made transparent)
//     and put on the front of the reception counter that faces the sliding door;
//   - new wall colours (navy and white brand palette), applied as NEW materials to the wall objects only;
//     the counter gets a white front (so the navy logo reads), a navy top and a blue trim;
//   - a "FIREWALL DUTY" sign over the entrance, on the inside, where the lintel is;
//   - security-awareness posters on the free spaces of the outer walls. A poster is only put where nothing else is: any
//     furniture, board, clock, TV or window of yours in the way is respected, and nothing is moved or deleted.
// Your original materials are never edited. Safe to run repeatedly: finished parts are skipped.
public static class BrandingSetupTool
{
    private const string OriginalLogo = "Assets/UI/FirewallDutyLogo_Original.png";
    private const string LogoPath = "Assets/UI/FirewallDutyLogo.png";
    private const string MaterialFolder = "Assets/Materials/Brand";
    private const string DecorRootName = "Wall_Decor";

    private static readonly Color Navy = new Color(0.07f, 0.15f, 0.3f);
    private static readonly Color Blue = new Color(0.18f, 0.48f, 0.88f);

    private const int MaxPosters = 14;
    private const float PosterWidth = 0.6f;
    private const float PosterHeight = 0.85f;
    private const float PosterCenterY = 1.55f;

    [MenuItem("Tools/Firewall Duty/Set Up Branding And Wall Design")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();

        Sprite logo = EnsureLogo(report);

        // Every room has a visible colour, and all of them are LIGHT so the dark doors and frames stand out against them.
        // (If a material still has the colour an earlier version made, it is updated; a colour you chose yourself is kept.)
        Material wallLight = Lit("Wall_Light", new Color(0.78f, 0.88f, 0.97f), 0.1f, 0f, new Color(0.91f, 0.94f, 0.97f));       // outer walls: sky blue
        Material wallIt = Lit("Wall_IT", new Color(0.7f, 0.85f, 0.92f), 0.1f, 0f, new Color(0.16f, 0.23f, 0.33f));              // IT room: cool light teal
        Material wallManager = Lit("Wall_Manager", new Color(0.97f, 0.91f, 0.8f), 0.1f, 0f, new Color(0.85f, 0.84f, 0.8f));     // head office: warm cream
        Material wallMeeting = Lit("Wall_Meeting", new Color(0.8f, 0.93f, 0.88f), 0.1f, 0f, new Color(0.74f, 0.84f, 0.94f));   // meeting room: soft mint
        Material wallKitchen = Lit("Wall_Kitchen", new Color(0.8f, 0.93f, 0.88f), 0.1f, 0f);
        Material counterFront = Lit("Counter_Front", new Color(0.97f, 0.98f, 1f), 0.25f, 0f);
        Material counterTop = Lit("Counter_Top", Navy, 0.65f, 0.1f);
        Material counterTrim = Lit("Counter_Trim", Blue, 0.5f, 0.1f);
        Material plate = Lit("Sign_Plate", Navy, 0.4f, 0f);

        report.AppendLine(PaintWalls(wallLight, wallIt, wallManager, wallMeeting));
        report.AppendLine(PaintCubicles(wallLight));
        report.AppendLine(PaintKitchen(wallKitchen));
        report.AppendLine(PaintCounter(counterFront, counterTop, counterTrim));
        report.AppendLine(PlaceCounterLogo(logo));
        report.AppendLine(PlaceEntranceSign(plate));
        report.AppendLine(PlacePosters(logo));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S) and press Play. Nothing of yours was moved or deleted; to undo a part, use Ctrl+Z right after, " +
                          "or delete the new objects ('Reception_Logo', 'Entrance_Sign', '" + DecorRootName + "'). Original materials are untouched.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // ---- Logo ------------------------------------------------------------------------------------------------

    // Makes the logo's white background transparent (only the white that touches the picture's edge, so the white
    // inside the emblem, such as the padlock, stays), and imports the result as a sprite.
    private static Sprite EnsureLogo(StringBuilder report)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        if (existing != null)
        {
            report.AppendLine("Logo: using the existing " + LogoPath + ".");
            return existing;
        }

        string full = Path.Combine(Directory.GetCurrentDirectory(), OriginalLogo);
        if (!File.Exists(full))
        {
            report.AppendLine("Logo: could not find " + OriginalLogo + ". Put your logo image there and run this again.");
            return null;
        }

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(full));

        int w = tex.width;
        int h = tex.height;
        Color32[] px = tex.GetPixels32();
        bool[] background = new bool[w * h];
        Queue<int> queue = new Queue<int>();

        for (int x = 0; x < w; x++)
        {
            TryAdd(px, background, queue, x, 0, w, h);
            TryAdd(px, background, queue, x, h - 1, w, h);
        }

        for (int y = 0; y < h; y++)
        {
            TryAdd(px, background, queue, 0, y, w, h);
            TryAdd(px, background, queue, w - 1, y, w, h);
        }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w;
            int y = i / w;
            TryAdd(px, background, queue, x + 1, y, w, h);
            TryAdd(px, background, queue, x - 1, y, w, h);
            TryAdd(px, background, queue, x, y + 1, w, h);
            TryAdd(px, background, queue, x, y - 1, w, h);
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;

                if (background[i])
                {
                    px[i].a = 0;
                    continue;
                }

                // Soften the edge: a bright pixel touching the cleared area fades out instead of leaving a white halo.
                bool touches = (x > 0 && background[i - 1]) || (x < w - 1 && background[i + 1]) || (y > 0 && background[i - w]) || (y < h - 1 && background[i + w]);
                if (touches)
                {
                    float brightness = (px[i].r + px[i].g + px[i].b) / 3f;
                    if (brightness > 170f)
                        px[i].a = (byte)Mathf.Clamp((255f - brightness) * (255f / 85f), 0f, 255f);
                }
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), LogoPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(LogoPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(LogoPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();

        report.AppendLine("Logo: cleaned the white background and saved " + LogoPath + ".");
        return AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
    }

    private static void TryAdd(Color32[] px, bool[] background, Queue<int> queue, int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || x >= w || y >= h)
            return;

        int i = y * w + x;
        if (background[i])
            return;

        Color32 c = px[i];
        bool isBackground = c.a < 20 || (c.r >= 235 && c.g >= 235 && c.b >= 235);
        if (!isBackground)
            return;

        background[i] = true;
        queue.Enqueue(i);
    }

    // ---- Materials and colours -------------------------------------------------------------------------------

    // A simple URP Lit material, created once; if it already exists it is left as it is, so your tweaks survive.
    private static Material Lit(string name, Color color, float smoothness, float metallic, Color? previousDefault = null)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Brand");

        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            // Upgrade a material that still has the colour an earlier version of this tool gave it.
            if (previousDefault.HasValue)
            {
                Color current = material.GetColor("_BaseColor");
                Color old = previousDefault.Value;

                if (Mathf.Abs(current.r - old.r) + Mathf.Abs(current.g - old.g) + Mathf.Abs(current.b - old.b) < 0.02f)
                {
                    material.SetColor("_BaseColor", color);
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssets();
                }
            }

            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static string PaintWalls(Material light, Material it, Material manager, Material meeting)
    {
        int count = 0;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Wall_"))
                continue;

            MeshRenderer r = t.GetComponent<MeshRenderer>();
            if (r == null)
                continue;

            // Head office, meeting room, IT room, employee cubicles and the outer walls are all the same sky blue, inside and out.
            Material m = light;
            if (m == null)
                continue;

            Undo.RecordObject(r, "Paint Wall");
            r.sharedMaterial = m;
            count++;
        }

        return "Walls: painted " + count + " wall(s) sky blue - the head office, meeting room, IT room, employee cubicles and the outer walls, inside and out.";
    }

    // The employee cubicle dividers ("Cubicle wlala..."): the tall panels get the same sky blue as the walls. The thin
    // flat plank among them (a desk surface, not a divider) is left alone.
    private static string PaintCubicles(Material light)
    {
        if (light == null)
            return "Cubicles: no wall material, skipped.";

        int painted = 0;
        int left = 0;

        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Cubicle wlala"))
                continue;

            MeshRenderer r = t.GetComponent<MeshRenderer>();
            if (r == null)
                continue;

            // A divider is tall; a desk plank is thin.
            if (r.bounds.size.y < 1.5f)
            {
                left++;
                continue;
            }

            Undo.RecordObject(r, "Paint Cubicle");
            r.sharedMaterial = light;
            painted++;
        }

        return "Cubicles: painted " + painted + " divider(s) sky blue" + (left > 0 ? ", left " + left + " thin desk plank(s) as they were." : ".");
    }

    // The kitchen is the open area between the meeting room and the head office. The only wall that belongs to it alone is
    // the back wall between those two rooms, so that stretch (both faces) is covered with thin soft-mint panels. The panels
    // sit a few millimetres in front of the wall: the wall itself, and anything of yours on it, is not touched.
    private static string PaintKitchen(Material mint)
    {
        Transform back = FindObject("Wall_back");
        Transform west = FindObject("Wall_Meeting_Right");   // the meeting room's east wall
        Transform east = FindObject("Wall_Manager_Left");    // the head office's west wall

        if (back == null || west == null || east == null || mint == null)
            return "Kitchen: could not find 'Wall_back', 'Wall_Meeting_Right' and 'Wall_Manager_Left' (or the mint material), skipped.";

        Bounds wb = back.GetComponent<Renderer>().bounds;
        float minX = west.GetComponent<Renderer>().bounds.max.x;
        float maxX = east.GetComponent<Renderer>().bounds.min.x;

        if (maxX - minX < 1f)
            return "Kitchen: the stretch between the two room walls measured " + (maxX - minX).ToString("F2") + " m, which looks wrong, skipped.";

        Transform existing = FindObject("Kitchen_Wall_Paint");
        GameObject root = existing != null ? existing.gameObject : new GameObject("Kitchen_Wall_Paint");
        if (existing == null)
            Undo.RegisterCreatedObjectUndo(root, "Create Kitchen Wall Paint");

        float width = maxX - minX;
        float centerX = (minX + maxX) * 0.5f;
        float height = wb.size.y;
        float centerY = wb.center.y;
        const float thickness = 0.004f;
        const float gap = 0.003f;

        // Inner face looks into the office (toward -Z); outer face looks outside (toward +Z).
        SetPanel(root.transform, "Kitchen_Wall_Inner", new Vector3(centerX, centerY, wb.min.z - gap - thickness * 0.5f), new Vector3(width, height, thickness), mint);
        SetPanel(root.transform, "Kitchen_Wall_Outer", new Vector3(centerX, centerY, wb.max.z + gap + thickness * 0.5f), new Vector3(width, height, thickness), mint);

        return "Kitchen: soft mint on the back wall between the meeting room and the head office (x " + minX.ToString("F1") + " to " +
               maxX.ToString("F1") + "), inside and outside.";
    }

    private static void SetPanel(Transform parent, string panelName, Vector3 position, Vector3 scale, Material material)
    {
        Transform panel = parent.Find(panelName);

        if (panel == null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Create Wall Paint");
            cube.name = panelName;
            cube.transform.SetParent(parent, true);
            cube.transform.position = position;
            cube.transform.localScale = scale;

            Collider col = cube.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);

            panel = cube.transform;
        }

        MeshRenderer r = panel.GetComponent<MeshRenderer>();
        Undo.RecordObject(r, "Paint Kitchen Wall");
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static string PaintCounter(Material front, Material top, Material trim)
    {
        Transform reception = FindObject("Reception Area");
        if (reception == null)
            return "Counter: 'Reception Area' not found, skipped.";

        int count = 0;

        foreach (MeshRenderer r in reception.GetComponentsInChildren<MeshRenderer>())
        {
            Material m = null;
            if (r.name.StartsWith("R_Front")) m = front;
            else if (r.name.StartsWith("R_Top")) m = top;
            else if (r.name.StartsWith("R_Accent")) m = trim;

            if (m == null)
                continue;

            Undo.RecordObject(r, "Paint Counter");
            r.sharedMaterial = m;
            count++;
        }

        return "Counter: painted " + count + " part(s) - white front, navy top, blue trim.";
    }

    // ---- The logo on the counter -----------------------------------------------------------------------------

    private static string PlaceCounterLogo(Sprite logo)
    {
        if (logo == null)
            return "Counter logo: no logo image, skipped.";

        if (FindObject("Reception_Logo") != null)
            return "Counter logo: already there, kept where it is.";

        Transform reception = FindObject("Reception Area");
        Transform front = FindObject("R_Front");
        if (front == null)
            return "Counter logo: 'R_Front' not found, skipped.";

        Renderer fr = front.GetComponent<Renderer>();
        Bounds b = fr != null ? fr.bounds : new Bounds(front.position, front.lossyScale);

        // The front face looks toward the sliding door (south, -Z).
        float height = Mathf.Min(0.9f, b.size.y * 0.92f);
        float scale = height / Mathf.Max(logo.bounds.size.y, 0.0001f);

        GameObject go = new GameObject("Reception_Logo", typeof(SpriteRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create Counter Logo");
        if (reception != null)
            go.transform.SetParent(reception, true);

        go.transform.position = new Vector3(b.center.x, b.center.y + 0.01f, b.min.z - 0.004f);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * scale / (reception != null ? Mathf.Max(reception.lossyScale.x, 0.0001f) : 1f);

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = logo;

        return "Counter logo: placed on the front of the counter, " + height.ToString("F2") + " m tall, facing the door.";
    }

    // ---- The company name over the entrance ------------------------------------------------------------------

    private static string PlaceEntranceSign(Material plateMaterial)
    {
        if (FindObject("Entrance_Sign") != null)
            return "Entrance sign: already there, kept where it is.";

        Transform lintel = FindObject("Wall_front_top");
        if (lintel == null)
            return "Entrance sign: 'Wall_front_top' (the wall above the entrance) not found, skipped.";

        Renderer lr = lintel.GetComponent<Renderer>();
        if (lr == null)
            return "Entrance sign: the wall above the entrance has no renderer, skipped.";

        Bounds b = lr.bounds;
        float insideZ = b.max.z;   // the office is on the +Z side of the front wall

        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(plate, "Create Entrance Sign");
        plate.name = "Entrance_Sign";
        plate.transform.position = new Vector3(b.center.x, b.center.y, insideZ + 0.02f);
        plate.transform.localScale = new Vector3(Mathf.Max(0.5f, b.size.x - 0.2f), Mathf.Max(0.3f, b.size.y - 0.18f), 0.04f);

        Collider col = plate.GetComponent<Collider>();
        if (col != null)
            Object.DestroyImmediate(col);

        if (plateMaterial != null)
            plate.GetComponent<MeshRenderer>().sharedMaterial = plateMaterial;

        GameObject text = new GameObject("Entrance_Sign_Text", typeof(TextMeshPro));
        Undo.RegisterCreatedObjectUndo(text, "Create Entrance Sign Text");
        text.transform.SetParent(plate.transform.parent, true);
        text.transform.position = new Vector3(b.center.x, b.center.y, insideZ + 0.045f);
        text.transform.rotation = Quaternion.LookRotation(Vector3.back);   // forward points away from someone standing inside

        TextMeshPro tmp = text.GetComponent<TextMeshPro>();
        tmp.text = "FIREWALL DUTY\n<size=38%><color=#9FC6FF>CYBERSECURITY IS OUR DUTY.</color></size>";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.5f;
        tmp.fontSizeMax = 30f;
        tmp.rectTransform.sizeDelta = new Vector2(plate.transform.localScale.x * 0.94f, plate.transform.localScale.y * 0.86f);

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        MeshRenderer tr = tmp.GetComponent<MeshRenderer>();
        if (tr != null)
            tr.shadowCastingMode = ShadowCastingMode.Off;

        return "Entrance sign: 'FIREWALL DUTY' on the inside of the wall above the entrance (" + plate.transform.localScale.x.ToString("F1") + " x " +
               plate.transform.localScale.y.ToString("F1") + " m).";
    }

    // ---- Posters on the free wall space ----------------------------------------------------------------------

    private static string PlacePosters(Sprite logo)
    {
        Transform existing = FindObject(DecorRootName);
        if (existing != null && existing.childCount > 0)
            return "Posters: '" + DecorRootName + "' already has " + existing.childCount + " poster(s), kept. Delete that object to place them again.";

        string[] wallNames = { "Wall_left", "Wall_right", "Wall_back", "Wall_Front_Left", "Wall_Front_Right" };
        ComputerScreenDisplay.Design[] designs =
        {
            ComputerScreenDisplay.Design.PosterBrand,
            ComputerScreenDisplay.Design.PosterMonitor,
            ComputerScreenDisplay.Design.PosterThink,
            ComputerScreenDisplay.Design.PosterPassword,
            ComputerScreenDisplay.Design.PosterPhishing,
            ComputerScreenDisplay.Design.PosterLock
        };

        // Everything that already stands or hangs in the office: a poster must not overlap any of it.
        List<Bounds> occupied = new List<Bounds>();
        // (The five outer walls are what the posters hang on, so they are not obstacles; every other wall, such as the
        // partitions of the rooms, the doors, furniture and decorations, is.)
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string n = r.name;
            bool outerWall = System.Array.IndexOf(wallNames, n) >= 0;
            bool inDecor = existing != null && r.transform.IsChildOf(existing);

            if (outerWall || n == "Floor" || inDecor)
                continue;

            occupied.Add(r.bounds);
        }

        GameObject root = existing != null ? existing.gameObject : new GameObject(DecorRootName);
        if (existing == null)
            Undo.RegisterCreatedObjectUndo(root, "Create Wall Decor");

        int placed = 0;
        int designIndex = 0;

        foreach (string wallName in wallNames)
        {
            Transform wall = FindObject(wallName);
            Renderer wr = wall != null ? wall.GetComponent<Renderer>() : null;
            if (wr == null)
                continue;

            Bounds wb = wr.bounds;

            // The thin direction of the wall is its thickness; the poster faces inward (toward the middle of the office).
            bool thinX = wb.size.x < wb.size.z;
            Vector3 normal = thinX ? new Vector3(Mathf.Sign(-wb.center.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(-wb.center.z));
            float half = (thinX ? wb.size.x : wb.size.z) * 0.5f;
            Vector3 faceCenter = wb.center + normal * half;

            float start = (thinX ? wb.min.z : wb.min.x) + 0.9f;
            float end = (thinX ? wb.max.z : wb.max.x) - 0.9f;
            float lastPlaced = float.NegativeInfinity;

            for (float along = start; along <= end && placed < MaxPosters; along += 0.4f)
            {
                if (along - lastPlaced < 1.3f)
                    continue;

                Vector3 center = faceCenter + normal * 0.015f;
                if (thinX)
                    center.z = along;
                else
                    center.x = along;

                center.y = PosterCenterY;

                if (Overlaps(center, normal, occupied))
                    continue;

                CreatePoster(root.transform, center, normal, designs[designIndex % designs.Length], logo, placed);
                designIndex++;
                placed++;
                lastPlaced = along;
            }
        }

        return "Posters: placed " + placed + " in free wall space (nothing was moved to make room).";
    }

    // Would a poster here touch anything? A little margin is kept around it.
    private static bool Overlaps(Vector3 center, Vector3 normal, List<Bounds> occupied)
    {
        bool thinX = Mathf.Abs(normal.x) > 0.5f;
        float margin = 0.12f;

        Vector3 size = thinX
            ? new Vector3(0.2f, PosterHeight + margin * 2f, PosterWidth + margin * 2f)
            : new Vector3(PosterWidth + margin * 2f, PosterHeight + margin * 2f, 0.2f);

        Bounds poster = new Bounds(center + normal * 0.05f, size);

        foreach (Bounds b in occupied)
        {
            if (b.Intersects(poster))
                return true;
        }

        return false;
    }

    private static void CreatePoster(Transform parent, Vector3 position, Vector3 normal, ComputerScreenDisplay.Design design, Sprite logo, int number)
    {
        const float pixelWidth = 480f;
        const float pixelHeight = 680f;

        GameObject go = new GameObject("Poster_" + design.ToString().Replace("Poster", "") + "_" + (number + 1),
                                       typeof(RectTransform), typeof(Canvas), typeof(ComputerScreenDisplay));
        Undo.RegisterCreatedObjectUndo(go, "Create Poster");
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);   // forward points into the wall, away from the reader
        go.transform.localScale = Vector3.one * (PosterWidth / pixelWidth);

        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(pixelWidth, pixelHeight);

        SerializedObject so = new SerializedObject(go.GetComponent<ComputerScreenDisplay>());
        so.FindProperty("design").enumValueIndex = (int)design;
        so.FindProperty("pixelSize").vector2Value = new Vector2(pixelWidth, pixelHeight);
        so.FindProperty("logo").objectReferenceValue = logo;
        so.ApplyModifiedProperties();
    }

    private static Transform FindObject(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }
}

using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Firewall Duty > Make Joystick Round
// Gives the on-screen joystick round graphics: a translucent circle with a clear ring edge for the base, and a solid
// round handle. Only the pictures change: the size, position and the drag logic of VirtualJoystick are untouched
// (its handle already moves inside a circle). Safe to run repeatedly.
public static class JoystickLookTool
{
    private const string BasePath = "Assets/UI/JoystickBase.png";
    private const string HandlePath = "Assets/UI/JoystickHandle.png";

    private static readonly Color BaseTint = new Color(0.78f, 0.9f, 1f, 0.9f);
    private static readonly Color HandleTint = new Color(0.9f, 0.96f, 1f, 0.95f);

    [MenuItem("Tools/Firewall Duty/Make Joystick Round")]
    public static void MakeRound()
    {
        StringBuilder report = new StringBuilder();

        Sprite baseSprite = EnsureSprite(BasePath, true);
        Sprite handleSprite = EnsureSprite(HandlePath, false);

        int count = 0;

        foreach (VirtualJoystick joystick in Object.FindObjectsByType<VirtualJoystick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject so = new SerializedObject(joystick);
            RectTransform background = so.FindProperty("background").objectReferenceValue as RectTransform;
            RectTransform handle = so.FindProperty("handle").objectReferenceValue as RectTransform;

            // The same fallbacks VirtualJoystick uses at runtime.
            if (background == null)
                background = joystick.GetComponent<RectTransform>();
            if (handle == null && joystick.transform.childCount > 0)
                handle = joystick.transform.GetChild(0) as RectTransform;

            Style(background, baseSprite, BaseTint, true, report);
            Style(handle, handleSprite, HandleTint, false, report);
            count++;
        }

        if (count == 0)
        {
            UnityEngine.Debug.LogWarning("No VirtualJoystick found in the open scene(s).");
            return;
        }

        foreach (VirtualJoystick joystick in Object.FindObjectsByType<VirtualJoystick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            EditorSceneManager.MarkSceneDirty(joystick.gameObject.scene);

        report.AppendLine("Joystick graphics are now round. Save the scene (Ctrl+S) and press Play.");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static void Style(RectTransform rect, Sprite sprite, Color tint, bool isBase, StringBuilder report)
    {
        if (rect == null)
            return;

        Image image = rect.GetComponent<Image>();
        if (image == null)
            return;

        Undo.RecordObject(image, "Make Joystick Round");
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = tint;

        // The base takes the touches; the handle only has to be drawn (a touch on it reaches the base anyway).
        image.raycastTarget = isBase;

        // A round picture only looks round if its rectangle is square.
        Vector2 size = rect.sizeDelta;
        if (!Mathf.Approximately(size.x, size.y))
        {
            float side = Mathf.Min(size.x, size.y);
            Undo.RecordObject(rect, "Make Joystick Round");
            rect.sizeDelta = new Vector2(side, side);
            report.AppendLine("Made '" + rect.name + "' square (" + side + " px) so it is a true circle.");
        }

        EditorUtility.SetDirty(image);
        report.AppendLine("'" + rect.name + "' now uses a round sprite (" + rect.sizeDelta.x + " px).");
    }

    // A crisp, anti-aliased circle. The base also gets a bright ring around a translucent fill.
    private static Sprite EnsureSprite(string path, bool withRing)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        const int size = 256;
        float radius = size * 0.5f - 1f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float edge = Mathf.Clamp01(radius - d + 0.5f);   // 1 inside, anti-aliased over the last pixel

                float alpha = edge;
                if (withRing)
                {
                    float ringWidth = 9f;
                    float inRing = Mathf.Clamp01(d - (radius - ringWidth) + 0.5f);
                    alpha = edge * Mathf.Lerp(0.35f, 1f, inRing);   // translucent fill, solid ring at the rim
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

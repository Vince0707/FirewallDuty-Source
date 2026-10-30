using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Firewall Duty > Set Up Hand Interaction Button In Open Scenes
// Turns the on-screen interact button into a fixed, circular hand button on the right side of the screen
// (mirroring the joystick on the left). Reuses the existing button if the scene has one. Safe to run repeatedly.
public static class HandButtonSetupTool
{
    private const string IconPath = "Assets/UI/HandIcon.png";
    private const string ButtonName = "HandInteractButton";
    private const string IconChildName = "HandIcon";

    // Mirrors the joystick (anchored bottom-left at 200,190, size 180) on the right edge.
    private static readonly Vector2 ButtonSize = new Vector2(180f, 180f);
    private static readonly Vector2 ButtonPosition = new Vector2(-200f, 190f);

    [MenuItem("Tools/Firewall Duty/Set Up Hand Interaction Button In Open Scenes")]
    public static void SetUpInOpenScenes()
    {
        StringBuilder report = new StringBuilder();
        Sprite handSprite = EnsureHandSprite();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            report.AppendLine("Scene: " + scene.name);
            report.AppendLine("  " + SetUpScene(scene, handSprite));
            EditorSceneManager.MarkSceneDirty(scene);
        }

        report.AppendLine();
        report.AppendLine("Save the scene(s) (Ctrl+S) to keep the changes.");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static string SetUpScene(Scene scene, Sprite handSprite)
    {
        PlayerInteraction player = null;
        Canvas canvas = null;
        Button button = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (player == null)
                player = root.GetComponentInChildren<PlayerInteraction>(true);

            if (canvas == null)
            {
                foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                    {
                        canvas = c;
                        break;
                    }
                }
            }

            if (button == null)
                button = FindInteractButton(root);
        }

        if (player == null)
            return "no PlayerInteraction in this scene - nothing to do (expected for scenes without a player).";

        List<string> notes = new List<string>();

        if (button == null)
        {
            if (canvas == null)
                return "PlayerInteraction found but the scene has no screen-space Canvas.";

            button = CreateButton(canvas, player);
            notes.Add("created '" + ButtonName + "' under '" + canvas.name + "'");
        }

        notes.Add(StyleButton(button, handSprite));
        notes.Add(WirePrompt(button, player));

        return string.Join("; ", notes);
    }

    private static Button FindInteractButton(GameObject root)
    {
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            if (b.GetComponent<InteractionPromptUI>() != null)
                return b;

            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            {
                if (b.onClick.GetPersistentTarget(i) is PlayerInteraction)
                    return b;
            }
        }

        return null;
    }

    private static Button CreateButton(Canvas canvas, PlayerInteraction player)
    {
        GameObject go = new GameObject(ButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create Hand Button");
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        UnityEventTools.AddPersistentListener(button.onClick, player.Interact);
        return button;
    }

    private static string StyleButton(Button button, Sprite handSprite)
    {
        Undo.RecordObject(button.gameObject, "Style Hand Button");
        button.gameObject.name = ButtonName;

        // Fixed bottom-right placement, mirroring the joystick.
        RectTransform rect = (RectTransform)button.transform;
        Undo.RecordObject(rect, "Layout Hand Button");
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = ButtonSize;
        rect.anchoredPosition = ButtonPosition;
        rect.localScale = Vector3.one;

        // Circular background (Unity's built-in round knob sprite).
        Image background = button.GetComponent<Image>();
        Undo.RecordObject(background, "Style Hand Button");
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        background.type = Image.Type.Simple;
        background.preserveAspect = true;
        background.color = new Color(0.1f, 0.18f, 0.26f, 0.85f);
        background.raycastTarget = true;
        button.targetGraphic = background;

        // Remove the old OPEN/CLOSE text; the hand icon replaces it.
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(label.gameObject, "Hide Button Label");
            label.gameObject.SetActive(false);
        }

        // Hand icon child. It must not catch taps, or the button's own circle would be partly blocked.
        Transform iconTransform = button.transform.Find(IconChildName);
        GameObject iconObject;
        if (iconTransform == null)
        {
            iconObject = new GameObject(IconChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(iconObject, "Create Hand Icon");
            iconObject.layer = button.gameObject.layer;
            iconObject.transform.SetParent(button.transform, false);
        }
        else
        {
            iconObject = iconTransform.gameObject;
        }

        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = ButtonSize * 0.62f;
        iconRect.anchoredPosition = Vector2.zero;

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = handSprite;
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.raycastTarget = false;

        EditorUtility.SetDirty(button);
        return "styled '" + button.name + "' as a " + ButtonSize.x + "px round hand button at bottom-right";
    }

    private static string WirePrompt(Button button, PlayerInteraction player)
    {
        string result = "";

        InteractionPromptUI prompt = button.GetComponent<InteractionPromptUI>();
        if (prompt == null)
        {
            prompt = Undo.AddComponent<InteractionPromptUI>(button.gameObject);
            result += "added InteractionPromptUI; ";
        }

        SerializedObject so = new SerializedObject(prompt);
        so.FindProperty("playerInteraction").objectReferenceValue = player;
        so.FindProperty("button").objectReferenceValue = button;
        so.ApplyModifiedProperties();

        // The button must call PlayerInteraction.Interact exactly once per tap.
        int calls = 0;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) is PlayerInteraction)
                calls++;
        }

        if (calls == 0)
        {
            UnityEventTools.AddPersistentListener(button.onClick, player.Interact);
            result += "hooked OnClick to PlayerInteraction.Interact";
        }
        else if (calls > 1)
        {
            result += "WARNING: OnClick calls PlayerInteraction more than once - remove the duplicate";
        }
        else
        {
            result += "OnClick already calls PlayerInteraction.Interact";
        }

        return result;
    }

    // ---- Hand icon -------------------------------------------------------------------------------------------

    // Draws a white open-hand silhouette (palm, four fingers, thumb) and imports it as a UI sprite.
    private static Sprite EnsureHandSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        if (existing != null)
            return existing;

        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = HandDistance(new Vector2(x + 0.5f, y + 0.5f));
                float a = Mathf.Clamp01(0.5f - d); // 1px anti-aliased edge
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), IconPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    // Signed distance (pixels) to the hand shape; negative inside.
    private static float HandDistance(Vector2 p)
    {
        float d = RoundedBox(p, new Vector2(128f, 96f), new Vector2(52f, 50f), 22f);               // palm
        d = Mathf.Min(d, Capsule(p, new Vector2(94f, 120f), new Vector2(92f, 196f), 14f));         // index
        d = Mathf.Min(d, Capsule(p, new Vector2(119f, 128f), new Vector2(119f, 214f), 14f));       // middle
        d = Mathf.Min(d, Capsule(p, new Vector2(144f, 126f), new Vector2(146f, 206f), 14f));       // ring
        d = Mathf.Min(d, Capsule(p, new Vector2(168f, 116f), new Vector2(174f, 182f), 13f));       // pinky
        d = Mathf.Min(d, Capsule(p, new Vector2(84f, 84f), new Vector2(46f, 136f), 15f));          // thumb
        return d;
    }

    private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - radius;
    }

    private static float RoundedBox(Vector2 p, Vector2 center, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - halfSize + Vector2.one * radius;
        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
        return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + outside - radius;
    }
}

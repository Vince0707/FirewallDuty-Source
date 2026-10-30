using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Main menu backgrounds made from the game's own office.
//
// Tools > Firewall Duty > Menu UI > Make Background Options   (run it with Company_scene open)
//   Takes pictures of the office from eight different spots (the lounge, the main hall, the entrance, the reception, the corridor, a high corner view ...),
//   softens them (a light blur) and gives them the deep blue night look of the new menu design. They are saved as Option_1.png to Option_8.png in
//   Assets/UI/MenuBackgrounds (outside the game's Resources, so they add nothing to the APK). Open them in the Project window and pick your favourite.
//
// Tools > Firewall Duty > Menu UI > Use Selected Image As Menu Background
//   Select the picture you like (one of the options, or your own image) in the Project window and run this: it is copied to
//   Assets/Resources/MenuUI/Images/menu_bg.png, which the main menu uses. Delete that file to go back to the old background.
//
// Nothing in the scene is changed: a temporary camera takes the pictures and is removed.
public static class MenuBackgroundTool
{
    private const string OptionsFolder = "Assets/UI/MenuBackgrounds";
    private const string MenuPath = "Assets/Resources/MenuUI/Images/menu_bg.png";
    private const int Width = 1280;
    private const int Height = 720;

    private struct View
    {
        public string name;
        public Vector3 eye;
        public Vector3 target;
        public float fov;
        public int blur;   // blur passes
    }

    // Where each picture is taken from and what it looks at (all inside the office: x -15..15, z -12..12, ceiling about 4 m).
    private static readonly View[] Views =
    {
        new View { name = "Lounge from the hall",      eye = new Vector3(7.6f, 1.75f, 2.9f),    target = new Vector3(13.2f, 1.3f, -2.8f), fov = 64f, blur = 2 },
        new View { name = "Lounge, wide",              eye = new Vector3(14.5f, 2.0f, 3.5f),    target = new Vector3(8.5f, 1.1f, -3.0f),  fov = 70f, blur = 2 },
        new View { name = "Hall from the west corner", eye = new Vector3(-13.6f, 2.1f, -10.6f), target = new Vector3(6f, 1.2f, 5f),       fov = 62f, blur = 2 },
        new View { name = "Hall from the east corner", eye = new Vector3(13.6f, 2.1f, -10.6f),  target = new Vector3(-6f, 1.2f, 4f),      fov = 62f, blur = 2 },
        new View { name = "Up the hall from the door", eye = new Vector3(0f, 2.6f, -11.3f),     target = new Vector3(0f, 1.4f, 8f),       fov = 62f, blur = 2 },
        new View { name = "Reception",                 eye = new Vector3(-5.2f, 1.9f, -8.8f),   target = new Vector3(1.8f, 1.1f, -5f),    fov = 66f, blur = 2 },
        new View { name = "West corridor",             eye = new Vector3(-7.0f, 1.9f, 2.0f),    target = new Vector3(-13.5f, 1.5f, 1.6f), fov = 66f, blur = 2 },
        new View { name = "High corner, whole office", eye = new Vector3(13.8f, 3.5f, 11.2f),   target = new Vector3(-4f, 0.8f, -4f),     fov = 76f, blur = 3 }
    };

    [MenuItem("Tools/Firewall Duty/Menu UI/Make Background Options")]
    public static void MakeOptions()
    {
        if (SceneManager.GetActiveScene().name != "Company_scene")
        {
            EditorUtility.DisplayDialog("Make Background Options", "Open Assets/Scenes/Company_scene.unity first (it must be the active scene), then run this again.", "OK");
            return;
        }

        Directory.CreateDirectory(OptionsFolder);

        for (int i = 0; i < Views.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Make Background Options", "Taking picture " + (i + 1) + " of " + Views.Length + ": " + Views[i].name, i / (float)Views.Length);

            try
            {
                Render(Views[i], OptionsFolder + "/Option_" + (i + 1) + ".png");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Make Background Options: picture " + (i + 1) + " failed: " + e.Message);
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        string list = "";
        for (int i = 0; i < Views.Length; i++)
            list += "\n  Option_" + (i + 1) + ".png - " + Views[i].name;

        UnityEngine.Debug.Log("Make Background Options: " + Views.Length + " pictures saved in " + OptionsFolder + ":" + list +
                              "\nOpen them in the Project window, pick one, select it and run Tools > Firewall Duty > Menu UI > Use Selected Image As Menu Background.");
        EditorUtility.RevealInFinder(OptionsFolder);
    }

    [MenuItem("Tools/Firewall Duty/Menu UI/Use Selected Image As Menu Background")]
    public static void UseSelected()
    {
        Texture2D selected = Selection.activeObject as Texture2D;
        string path = selected != null ? AssetDatabase.GetAssetPath(selected) : null;

        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("Use Selected Image", "Select a picture in the Project window first (one of the Option_N.png in Assets/UI/MenuBackgrounds, or your own image), then run this again.", "OK");
            return;
        }

        if (path == MenuPath)
        {
            UnityEngine.Debug.Log("Use Selected Image: that picture already is the menu background.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(MenuPath));

        if (File.Exists(MenuPath))
            AssetDatabase.DeleteAsset(MenuPath);

        if (!AssetDatabase.CopyAsset(path, MenuPath))
        {
            UnityEngine.Debug.LogError("Use Selected Image: could not copy " + path + " to " + MenuPath + ".");
            return;
        }

        AssetDatabase.ImportAsset(MenuPath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(MenuPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        UnityEngine.Debug.Log("Use Selected Image: '" + path + "' is now the main menu background (" + MenuPath + "). Press Play to see it.");
    }

    // ---- Taking and softening a picture --------------------------------------------------------------------------------------------

    private static void Render(View view, string outputPath)
    {
        GameObject cameraObject = new GameObject("MenuBackgroundCamera");
        RenderTexture target = null;
        Texture2D picture = null;

        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = view.fov;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = view.eye;
            camera.transform.rotation = Quaternion.LookRotation(view.target - view.eye);

            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
                camera.Render();

            RenderTexture.active = target;
            picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            picture.Apply();
            RenderTexture.active = null;

            Color[] pixels = picture.GetPixels();
            for (int i = 0; i < view.blur; i++)
                Blur(pixels, Width, Height, 3);

            BlueNight(pixels, Width, Height);
            picture.SetPixels(pixels);
            picture.Apply();

            File.WriteAllBytes(outputPath, picture.EncodeToPNG());
        }
        finally
        {
            if (target != null)
            {
                RenderTexture.active = null;
                target.Release();
                Object.DestroyImmediate(target);
            }

            if (picture != null)
                Object.DestroyImmediate(picture);

            Object.DestroyImmediate(cameraObject);
        }
    }

    // A box blur (a few pixels wide), done along the rows and then the columns.
    private static void Blur(Color[] pixels, int width, int height, int radius)
    {
        Color[] temp = new Color[pixels.Length];
        float count = radius * 2 + 1;

        for (int y = 0; y < height; y++)
        {
            Color sum = Color.black;
            for (int x = -radius; x <= radius; x++)
                sum += pixels[y * width + Mathf.Clamp(x, 0, width - 1)];

            for (int x = 0; x < width; x++)
            {
                temp[y * width + x] = sum / count;
                sum += pixels[y * width + Mathf.Clamp(x + radius + 1, 0, width - 1)] - pixels[y * width + Mathf.Clamp(x - radius, 0, width - 1)];
            }
        }

        for (int x = 0; x < width; x++)
        {
            Color sum = Color.black;
            for (int y = -radius; y <= radius; y++)
                sum += temp[Mathf.Clamp(y, 0, height - 1) * width + x];

            for (int y = 0; y < height; y++)
            {
                pixels[y * width + x] = sum / count;
                sum += temp[Mathf.Clamp(y + radius + 1, 0, height - 1) * width + x] - temp[Mathf.Clamp(y - radius, 0, height - 1) * width + x];
            }
        }
    }

    // The new menu's look: the picture turned deep blue, a little darker, with the corners darker still.
    private static void BlueNight(Color[] pixels, int width, int height)
    {
        Color deep = new Color(0.06f, 0.2f, 0.5f);
        Color light = new Color(0.62f, 0.8f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                Color c = pixels[i];
                float luminance = Mathf.Clamp01(c.r * 0.3f + c.g * 0.59f + c.b * 0.11f);

                // Dark parts go deep blue, bright parts light blue; some of the original colour is kept.
                Color blue = Color.Lerp(deep, light, Mathf.Pow(luminance, 0.8f));
                Color result = Color.Lerp(c, blue, 0.72f) * 0.82f;

                float dx = (x / (float)width - 0.5f) * 2f;
                float dy = (y / (float)height - 0.5f) * 2f;
                float vignette = Mathf.Clamp01(1f - (dx * dx * 0.25f + dy * dy * 0.3f));
                pixels[i] = new Color(result.r * vignette, result.g * vignette, result.b * vignette, 1f);
            }
        }
    }
}

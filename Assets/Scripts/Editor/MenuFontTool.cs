using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Tools > Firewall Duty > Menu UI > Create Menu Fonts
// Makes the TextMeshPro font assets of the new main menu from the font files in Assets/Fonts (Exo 2 Bold, Rajdhani SemiBold, Inter Regular, all free
// Google Fonts under the SIL Open Font License) and saves them in Assets/Resources/MenuUI/Fonts, where MenuFonts finds them. The letters, digits and
// signs the menu uses are drawn into each font at once, so nothing has to be created while the game runs; other letters are added on the fly.
// It also runs by itself when the project opens, if an asset is missing. Safe to run again: existing font assets are kept.
[InitializeOnLoad]
public static class MenuFontTool
{
    private const string FontFolder = "Assets/Fonts";
    private const string OutputFolder = "Assets/Resources/MenuUI/Fonts";

    private static readonly string[][] Fonts =
    {
        new[] { "Exo2-Bold.ttf", MenuFonts.TitleName },
        new[] { "Rajdhani-SemiBold.ttf", MenuFonts.LabelName },
        new[] { "Inter-Regular.ttf", MenuFonts.BodyName }
    };

    static MenuFontTool()
    {
        EditorApplication.delayCall += () => Create(false);
    }

    [MenuItem("Tools/Firewall Duty/Menu UI/Create Menu Fonts")]
    public static void CreateFromMenu()
    {
        Create(true);
    }

    private static void Create(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        int made = 0;
        string missing = "";

        foreach (string[] entry in Fonts)
        {
            string fontPath = FontFolder + "/" + entry[0];
            string assetPath = OutputFolder + "/" + entry[1] + ".asset";

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                continue;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                missing += " " + entry[0];
                continue;
            }

            Directory.CreateDirectory(OutputFolder);

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                UnityEngine.Debug.LogWarning("Create Menu Fonts: could not make a font asset from " + fontPath + ".");
                continue;
            }

            asset.name = entry[1];
            AssetDatabase.CreateAsset(asset, assetPath);

            // The material and the picture of the letters are saved inside the asset.
            if (asset.material != null)
            {
                asset.material.name = entry[1] + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            if (asset.atlasTextures != null)
            {
                for (int i = 0; i < asset.atlasTextures.Length; i++)
                {
                    if (asset.atlasTextures[i] == null)
                        continue;

                    asset.atlasTextures[i].name = entry[1] + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
                }
            }

            // Draw the usual characters now (letters, digits, punctuation, the bullet, quotes, the arrows used in the menu).
            string characters = "";
            for (int c = 32; c < 127; c++)
                characters += (char)c;
            characters += "•’‘“”–— …";

            asset.TryAddCharacters(characters, out string notAdded);

            EditorUtility.SetDirty(asset);
            made++;
        }

        if (made > 0)
        {
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("Create Menu Fonts: made " + made + " font asset(s) in " + OutputFolder + ".");
        }
        else if (verbose)
        {
            UnityEngine.Debug.Log(missing.Length > 0 ? "Create Menu Fonts: the font files were not found in " + FontFolder + ":" + missing + ". Put the .ttf files there and run this again."
                                                     : "Create Menu Fonts: the font assets already exist in " + OutputFolder + ".");
        }
    }
}

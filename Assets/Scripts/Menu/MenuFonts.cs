using TMPro;
using UnityEngine;

// The fonts of the new main menu design. They are made once from the font files in Assets/Fonts by
// Tools > Firewall Duty > Menu UI > Create Menu Fonts (it also runs by itself when Unity opens the project) and saved in Assets/Resources/MenuUI/Fonts.
//   Title (FIREWALL DUTY)                    Exo 2 Bold
//   Buttons and mission labels               Rajdhani SemiBold
//   Instructions and descriptions            Inter Regular
// If a font asset is missing, nothing breaks: the text just uses the default font.
public static class MenuFonts
{
    public const string TitleName = "Exo2-Bold SDF";
    public const string LabelName = "Rajdhani-SemiBold SDF";
    public const string BodyName = "Inter-Regular SDF";

    private static TMP_FontAsset title, label, body;

    public static TMP_FontAsset Title { get { return title != null ? title : title = Load(TitleName); } }
    public static TMP_FontAsset Label { get { return label != null ? label : label = Load(LabelName); } }
    public static TMP_FontAsset Body { get { return body != null ? body : body = Load(BodyName); } }

    private static TMP_FontAsset Load(string name)
    {
        return Resources.Load<TMP_FontAsset>("MenuUI/Fonts/" + name);
    }

    // Sets the font (when it exists) and returns the text, so it can be used in a chain.
    public static T Use<T>(T text, TMP_FontAsset font) where T : TMP_Text
    {
        if (text != null && font != null)
            text.font = font;

        return text;
    }
}

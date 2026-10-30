using System;
using UnityEngine;
using UnityEngine.UI;

// activity_settings.xml / SettingsActivity.java: graphics quality, touch sensitivity and sound. The screen is the Android layout
// itself (see AndroidLayout), so it is placed as in Android Studio. Changes are saved as they are made and applied to the game
// (GameSettings). The language choice was removed on request. The SETTINGS title is beige like the BACK button so it can be read
// against the background.
public class SettingsScreen : MenuScreen
{
    private AndroidLayout.View layout;
    private Action<int> showGraphics;
    private Slider sensitivity, master, sfx;
    private AndroidLayout.View sensitivityText, masterText, sfxText;
    private bool loading;

    protected override void Build(RectTransform root)
    {
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex("#70000000"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);
        layout = AndroidLayout.Inflate(frame, "activity_settings");
        if (layout == null)
            return;

        // The layout's own picture and shade are the first two views; the screen-wide ones above replace them.
        layout.children[0].SetVisible(false);
        layout.children[1].SetVisible(false);

        layout.Find("btnBackSettings").SetBackground("briefing_panel_bg");
        layout.Find("btnBackSettings").OnClick(() => App.Back());
        layout.Find("txtSettingsTitle").SetTextColor(UiKit.Hex("#F2D58A"));

        RemoveLanguage();

        AddLogOut();

        // With the language gone the content is shorter than the screen: keep it in the middle instead of at the top.
        layout.Find("settingsScroll").attributes["x_center"] = "true";

        // GRAPHICS QUALITY: the layout's drop-down.
        AndroidLayout.View graphicsHost = layout.Find("spinnerGraphics");
        RectTransform spinner = UiWidgets.Spinner(graphicsHost.rect, "Graphics", new[] { "Low", "Medium", "High" }, GameSettings.GraphicsQuality, index =>
        {
            if (!loading) GameSettings.GraphicsQuality = index;
            GameSettings.Apply();
        }, UiKit.Hex("#64B5F6"), out showGraphics);
        UiKit.Stretch(spinner);

        // The three seek bars and their labels.
        sensitivityText = layout.Find("txtSensitivityValue");
        masterText = layout.Find("txtMasterVolume");
        sfxText = layout.Find("txtSfxVolume");

        sensitivity = layout.Find("seekSensitivity").slider;
        master = layout.Find("seekMasterVolume").slider;
        sfx = layout.Find("seekSfxVolume").slider;

        sensitivity.onValueChanged.AddListener(value =>
        {
            sensitivityText.SetText("Sensitivity: " + (int)value + "%");
            if (!loading) GameSettings.Sensitivity = (int)value;
        });

        master.onValueChanged.AddListener(value =>
        {
            masterText.SetText("Master Volume: " + (int)value + "%");
            if (loading)
                return;

            GameSettings.MasterVolume = (int)value;
            GameSettings.Apply();
            GameAudio.PlayPreview();   // so the change can be heard
        });

        sfx.onValueChanged.AddListener(value =>
        {
            sfxText.SetText("Sound Effects: " + (int)value + "%");
            if (loading)
                return;

            GameSettings.SfxVolume = (int)value;
            GameAudio.PlayPreview();
        });

        layout.Relayout();
        layout.Find("settingsScroll").ScrollToTop();
    }

    // LOG OUT, at the very bottom of the settings: forgets the saved sign-in and goes back to the login screen (to use another
    // account). The button is the same black-text menu button used on the main menu.
    private void AddLogOut()
    {
        AndroidLayout.View footer = layout.Find("spinnerGraphics").parent;   // the box that holds every setting

        AndroidLayout.View button = footer.AddView("Button",
            "layout_width", "match_parent", "layout_height", "40dp", "layout_marginTop", "16dp",
            "background", "@drawable/menu_button_small", "text", "LOG OUT",
            "textColor", "#000000", "textSize", "14sp", "textStyle", "bold", "gravity", "center");

        button.OnClick(() =>
        {
            PlayerSession.SignOut();
            PlayerSession.SaveRememberedEmail("", false);
            App.ClearHistory();
            App.Show("Login", null, false);
        });
    }

    // The language heading, its sentence, the drop-down and the rule under it are taken out of the screen.
    private void RemoveLanguage()
    {
        AndroidLayout.View language = layout.Find("spinnerLanguage");
        if (language == null || language.parent == null)
            return;

        var siblings = language.parent.children;
        int index = siblings.IndexOf(language);

        for (int i = index - 2; i <= index + 1 && i < siblings.Count; i++)
        {
            if (i >= 0)
                siblings[i].SetVisible(false);
        }
    }

    public override void OnShow(object arg)
    {
        if (layout == null)
            return;

        loading = true;
        showGraphics(GameSettings.GraphicsQuality);
        sensitivity.value = GameSettings.Sensitivity;
        master.value = GameSettings.MasterVolume;
        sfx.value = GameSettings.SfxVolume;

        sensitivityText.SetText("Sensitivity: " + GameSettings.Sensitivity + "%");
        masterText.SetText("Master Volume: " + GameSettings.MasterVolume + "%");
        sfxText.SetText("Sound Effects: " + GameSettings.SfxVolume + "%");
        loading = false;

        layout.Relayout();
        layout.Find("settingsScroll").ScrollToTop();
    }
}

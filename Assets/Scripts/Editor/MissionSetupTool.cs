using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Firewall Duty > Set Up Mission System
// Gets the game ready to be started by the Android app:
//   - adds a MissionSession to the office scene (timer, objectives, result, link to the app);
//   - adds a countdown, an objectives counter and a TABLET button to the on-screen UI (the tablet is a screen of the app);
//   - adds a DEBUG FINISH button that only exists in the Editor and development builds, to test the outcome screen;
//   - puts the office and bank scenes in the build list (office first: the game always starts in the first scene).
// Safe to run repeatedly.
public static class MissionSetupTool
{
    private const string OfficeScene = "Assets/Scenes/Company_scene.unity";
    private const string BankScene = "Assets/Scenes/Bank_scene.unity";

    [MenuItem("Tools/Firewall Duty/Set Up Mission System")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();

        MissionSession session = Object.FindAnyObjectByType<MissionSession>();
        if (session == null)
        {
            GameObject go = new GameObject("MissionSession", typeof(MissionSession));
            Undo.RegisterCreatedObjectUndo(go, "Create MissionSession");
            session = go.GetComponent<MissionSession>();
            report.AppendLine("Added MissionSession.");
        }
        else
        {
            report.AppendLine("MissionSession already there.");
        }

        report.AppendLine(SetUpHud(session));
        report.AppendLine(MenuSceneTool.SetBuildList());

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        report.AppendLine();
        report.AppendLine("Save the scene (Ctrl+S). In the Editor, press Play: the timer counts down from 06:00, and DEBUG FINISH ends the mission " +
                          "(the Console shows what would be sent to the app). On a phone, the app supplies the mission.");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static string SetUpHud(MissionSession session)
    {
        Canvas canvas = null;
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas = c;
                break;
            }
        }

        if (canvas == null)
            return "HUD: no screen-space Canvas found, skipped.";

        if (canvas.transform.Find("MissionHud") != null)
            return "HUD: already there.";

        Sprite pill = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject hudRoot = new GameObject("MissionHud", typeof(RectTransform), typeof(MissionHud));
        Undo.RegisterCreatedObjectUndo(hudRoot, "Create Mission HUD");
        hudRoot.transform.SetParent(canvas.transform, false);
        RectTransform hudRect = (RectTransform)hudRoot.transform;
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        // Timer pill, top centre
        GameObject timerBox = Panel(hudRoot.transform, "TimerBox", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(240f, 56f),
                                    new Color(0.04f, 0.1f, 0.22f, 0.8f), pill);
        TMP_Text timer = AddText(timerBox.transform, "Timer", "06:00", 38, Color.white, TextAlignmentOptions.Center, true);

        // Objectives counter just under it
        TMP_Text objectives = AddText(hudRoot.transform, "Objectives", "Objectives 0 / 6", 22, new Color(0.8f, 0.9f, 1f), TextAlignmentOptions.Center, false);
        RectTransform objectivesRect = (RectTransform)objectives.transform;
        objectivesRect.anchorMin = objectivesRect.anchorMax = new Vector2(0.5f, 1f);
        objectivesRect.pivot = new Vector2(0.5f, 1f);
        objectivesRect.anchoredPosition = new Vector2(0f, -88f);
        objectivesRect.sizeDelta = new Vector2(300f, 34f);

        SerializedObject hudSo = new SerializedObject(hudRoot.GetComponent<MissionHud>());
        hudSo.FindProperty("timerText").objectReferenceValue = timer;
        hudSo.FindProperty("objectivesText").objectReferenceValue = objectives;
        hudSo.ApplyModifiedProperties();

        // TABLET button, top right: opens the app's tablet screen.
        GameObject tablet = Panel(hudRoot.transform, "TabletButton", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -28f), new Vector2(200f, 60f),
                                  new Color(0.12f, 0.35f, 0.75f, 0.95f), pill, new Vector2(1f, 1f));
        Button tabletButton = tablet.AddComponent<Button>();
        tabletButton.targetGraphic = tablet.GetComponent<Image>();
        UnityEventTools.AddPersistentListener(tabletButton.onClick, session.OpenTablet);
        AddText(tablet.transform, "Label", "TABLET", 28, Color.white, TextAlignmentOptions.Center, true);

        return "HUD: added the timer, objectives counter and TABLET button.";
    }

    private static string SetUpBuildList()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        // Office first, then bank; anything else stays after them.
        EditorBuildSettingsScene Take(string path)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                {
                    EditorBuildSettingsScene found = scenes[i];
                    scenes.RemoveAt(i);
                    found.enabled = true;
                    return found;
                }
            }

            return System.IO.File.Exists(path) ? new EditorBuildSettingsScene(path, true) : null;
        }

        EditorBuildSettingsScene office = Take(OfficeScene);
        EditorBuildSettingsScene bank = Take(BankScene);

        List<EditorBuildSettingsScene> result = new List<EditorBuildSettingsScene>();
        if (office != null) result.Add(office);
        if (bank != null) result.Add(bank);
        result.AddRange(scenes);

        EditorBuildSettings.scenes = result.ToArray();

        return "Build list: " + (office != null ? "Company_scene (first)" : "Company_scene MISSING") + ", " + (bank != null ? "Bank_scene" : "Bank_scene MISSING") +
               (scenes.Count > 0 ? ", then " + scenes.Count + " other scene(s)." : ".");
    }

    // ---- UI helpers ------------------------------------------------------------------------------------------

    private static GameObject Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color, Sprite sprite,
                                    Vector2? pivot = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? new Vector2(0.5f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return go;
    }

    private static TMP_Text AddText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment, bool bold)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}

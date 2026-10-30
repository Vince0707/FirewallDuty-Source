using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Firewall Duty > Menu UI > Create Menu Scene
// Makes Assets/Scenes/MainMenu_Scene.unity (a camera and the MenuApp that draws every menu screen) and puts it FIRST in the
// build list, so the game opens on the login screen. Your open scene is not touched. Safe to run again.
public static class MenuSceneTool
{
    private const string MenuScenePath = "Assets/Scenes/MainMenu_Scene.unity";
    private const string OfficeScenePath = "Assets/Scenes/Company_scene.unity";
    private const string BankScenePath = "Assets/Scenes/Bank_scene.unity";

    [MenuItem("Tools/Firewall Duty/Menu UI/Create Menu Scene")]
    public static void CreateMenuScene()
    {
        string report = "";

        if (!File.Exists(MenuScenePath))
        {
            // Built next to whatever is open, then saved and closed again.
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            GameObject cameraObject = new GameObject("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.1f, 0.2f);
            camera.cullingMask = 0;   // the menu is a screen overlay; the camera only clears the background
            cameraObject.AddComponent<AudioListener>();   // without a listener the menu is silent

            new GameObject("MenuApp", typeof(MenuApp));

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            EditorSceneManager.CloseScene(scene, true);
            report += "Created " + MenuScenePath + ".\n";
        }
        else
        {
            report += MenuScenePath + " already exists.\n";
        }

        report += SetBuildList();
        report += "\nPress Play with MainMenu_Scene open to try the menu: open it from Assets/Scenes.";
        UnityEngine.Debug.Log(report);
    }

    // Menu, office, bank, in that order; anything else stays after them.
    public static string SetBuildList()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        List<EditorBuildSettingsScene> result = new List<EditorBuildSettingsScene>();

        foreach (string path in new[] { MenuScenePath, OfficeScenePath, BankScenePath })
        {
            int index = scenes.FindIndex(s => s.path == path);

            if (index >= 0)
            {
                EditorBuildSettingsScene found = scenes[index];
                scenes.RemoveAt(index);
                found.enabled = true;
                result.Add(found);
            }
            else if (File.Exists(path))
            {
                result.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        result.AddRange(scenes);
        EditorBuildSettings.scenes = result.ToArray();

        string text = "Build list:";
        for (int i = 0; i < result.Count; i++)
            text += "\n  " + i + ": " + result[i].path;

        return text + "\n";
    }
}

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Set Up Ceiling Fans
// Makes every ceiling fan in the open scenes turn: adds CeilingFanSpinner to each "ceilingFan_blades" object and clears
// its Static flags (the fan prefab is fully Static, and Batching Static meshes cannot rotate). Safe to run repeatedly.
public static class CeilingFanSetupTool
{
    private const string BladesName = "ceilingFan_blades";

    [MenuItem("Tools/Firewall Duty/Set Up Ceiling Fans")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();
        int fans = 0;
        int added = 0;
        int unstatic = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != BladesName)
                        continue;

                    fans++;

                    if (t.GetComponent<CeilingFanSpinner>() == null)
                    {
                        Undo.AddComponent<CeilingFanSpinner>(t.gameObject);
                        added++;
                    }

                    // Only the blades need to move; the base and the rest of the fan stay Static.
                    foreach (Transform part in t.GetComponentsInChildren<Transform>(true))
                    {
                        if (GameObjectUtility.GetStaticEditorFlags(part.gameObject) != 0)
                        {
                            Undo.RecordObject(part.gameObject, "Clear Fan Blade Static Flags");
                            GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
                            unstatic++;
                        }
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        report.AppendLine(fans + " ceiling fan(s) found, " + added + " spinner(s) added, Static cleared on " + unstatic + " blade object(s).");
        if (fans == 0)
            report.AppendLine("No object named '" + BladesName + "' in the open scene(s).");

        report.AppendLine("Save the scene (Ctrl+S) and press Play: the blades should turn.");
        UnityEngine.Debug.Log(report.ToString());
    }
}

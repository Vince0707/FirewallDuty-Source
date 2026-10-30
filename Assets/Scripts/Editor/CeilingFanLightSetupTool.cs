using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Firewall Duty > Set Up Ceiling Fan Lights
// Makes the lamp under every ceiling fan really light the room:
//   - adds a warm point light under each fan (the bulb already glows; it just gave off no light);
//   - makes sure the render settings can actually show that light. The mobile pipeline drew extra lights per VERTEX and
//     only 4 per object, which looks wrong on large flat walls and floors, so it is switched to per-pixel lights and the
//     Forward+ renderer (the PC pipeline already uses Forward+).
// A light that already exists under a fan is left exactly as it is, so values you tune on a light are kept.
public static class CeilingFanLightSetupTool
{
    private const string BladesName = "ceilingFan_blades";
    private const string LightName = "FanLight";

    private static readonly Color WarmWhite = new Color(1f, 0.92f, 0.8f);

    [MenuItem("Tools/Firewall Duty/Set Up Ceiling Fan Lights")]
    public static void SetUp()
    {
        StringBuilder report = new StringBuilder();

        int fans = 0;
        int added = 0;

        foreach (Transform blades in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (blades.name != BladesName || blades.parent == null)
                continue;

            fans++;

            // The light is a child of the fan body (not of the spinning blades), so it never turns.
            Transform body = blades.parent;
            if (body.Find(LightName) != null)
                continue;

            // The lamp hangs at the bottom of the blades' mesh.
            Renderer renderer = blades.GetComponentInChildren<Renderer>();
            Vector3 position = renderer != null
                ? new Vector3(renderer.bounds.center.x, renderer.bounds.min.y - 0.05f, renderer.bounds.center.z)
                : blades.position + Vector3.down * 0.1f;

            GameObject go = new GameObject(LightName, typeof(Light));
            Undo.RegisterCreatedObjectUndo(go, "Create Fan Light");
            go.transform.SetParent(body, true);
            go.transform.position = position;

            Light light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = WarmWhite;
            light.intensity = 1.0f;
            light.range = 6.5f;
            light.shadows = LightShadows.None;   // no shadows: cheap, and right for a lamp under a ceiling
            light.lightmapBakeType = LightmapBakeType.Realtime;

            added++;
        }

        report.AppendLine(fans + " ceiling fan(s) found, " + added + " light(s) added.");
        if (fans == 0)
            report.AppendLine("No object named '" + BladesName + "' in the open scene(s).");

        report.AppendLine(FixPipeline("Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/Mobile_Renderer.asset"));
        report.AppendLine(FixPipeline("Assets/Settings/PC_RPAsset.asset", null));

        if (added > 0)
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        report.AppendLine("Save the scene (Ctrl+S) and press Play: each fan lamp should light the area around it. " +
                          "To change how bright or far a lamp reaches, select its 'FanLight' and edit Intensity and Range.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // Per-pixel extra lights, and Forward+ so a big floor can be lit by every nearby lamp, not just 4.
    internal static string FixPipeline(string pipelineAssetPath, string rendererPath)
    {
        StringBuilder notes = new StringBuilder();

        Object pipeline = AssetDatabase.LoadMainAssetAtPath(pipelineAssetPath);
        if (pipeline == null)
        {
            notes.Append("(" + pipelineAssetPath + " not found, skipped) ");
        }
        else
        {
            SerializedObject so = new SerializedObject(pipeline);
            SerializedProperty mode = so.FindProperty("m_AdditionalLightsRenderingMode");

            // 0 = off, 1 = per vertex, 2 = per pixel
            if (mode != null && mode.intValue != 2)
            {
                mode.intValue = 2;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(pipeline);
                notes.Append("extra lights set to per-pixel in " + System.IO.Path.GetFileName(pipelineAssetPath) + "; ");
            }

            SerializedProperty limit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            if (limit != null && limit.intValue < 8)
            {
                limit.intValue = 8;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(pipeline);
                notes.Append("per-object light limit raised to 8; ");
            }
        }

        if (!string.IsNullOrEmpty(rendererPath))
        {
            Object renderer = AssetDatabase.LoadMainAssetAtPath(rendererPath);
            if (renderer != null)
            {
                SerializedObject so = new SerializedObject(renderer);
                SerializedProperty renderingMode = so.FindProperty("m_RenderingMode");

                // 0 = Forward, 1 = Deferred, 2 = Forward+
                if (renderingMode != null && renderingMode.intValue != 2)
                {
                    renderingMode.intValue = 2;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(renderer);
                    notes.Append("renderer " + System.IO.Path.GetFileName(rendererPath) + " switched to Forward+; ");
                }
            }
        }

        AssetDatabase.SaveAssets();

        string text = notes.ToString().Trim();
        return "Render settings (" + System.IO.Path.GetFileNameWithoutExtension(pipelineAssetPath) + "): " +
               (text.Length > 0 ? text : "already fine, nothing changed.");
    }
}

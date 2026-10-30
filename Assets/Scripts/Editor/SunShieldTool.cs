using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Firewall Duty > Add Sun Shield (Open Scene)   (works in Company_scene and Bank_scene)
// Stops the sun from lighting the inside of the building. The sun's shadows have a small error near thin surfaces (the shadow of the ceiling and the thin walls
// "leaks" a little): white jagged bands along the tops of the walls and patches on the floor next to them, even with the ceiling closed. A thick slab above
// the ceiling and thick slabs outside the outer walls, that only cast shadows (they cannot be seen or touched), make the leak impossible, because the sun then
// meets a thick surface first. Everything is in one group, "Sun_Shield". Run it again to rebuild it; Ctrl+Z removes it.
public static class SunShieldTool
{
    private const string RootName = "Sun_Shield";
    private const float Thickness = 1.0f;

    [MenuItem("Tools/Firewall Duty/Add Sun Shield (Open Scene)")]
    public static void Add()
    {
        Scene scene = SceneManager.GetActiveScene();

        foreach (GameObject old in scene.GetRootGameObjects())
        {
            if (old.name == RootName)
            {
                Undo.DestroyObjectImmediate(old);
                break;
            }
        }

        // The building: the footprint of its ceiling, and the lowest floor.
        Bounds? ceiling = null;
        float floorBottom = float.MaxValue;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            string lower = root.name.ToLowerInvariant();
            Renderer renderer = root.GetComponent<Renderer>();
            if (renderer == null)
                continue;

            if (lower == "ceiling")
            {
                if (ceiling == null)
                    ceiling = renderer.bounds;
                else
                {
                    Bounds b = ceiling.Value;
                    b.Encapsulate(renderer.bounds);
                    ceiling = b;
                }
            }
            else if (lower == "floor")
            {
                floorBottom = Mathf.Min(floorBottom, renderer.bounds.min.y);
            }
        }

        if (ceiling == null)
        {
            UnityEngine.Debug.LogError("Add Sun Shield: no object named 'Ceiling' was found in the open scene, so nothing was built.");
            return;
        }

        Bounds c = ceiling.Value;
        float top = c.max.y;
        float bottom = floorBottom < float.MaxValue ? floorBottom - 0.5f : c.min.y - 7f;
        float height = top - bottom + 1.2f;
        float middleY = bottom + height * 0.5f;

        GameObject rootObject = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Add Sun Shield");

        // A rebake of the walkable surface must not treat these as obstacles.
        NavMeshModifier modifier = rootObject.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = true;

        Transform parent = rootObject.transform;
        float w = c.size.x;
        float d = c.size.z;
        float overlap = 0.1f;   // the slabs overlap the walls a little, so there is no gap

        // The roof, a thick slab over the whole ceiling and a bit more.
        Slab(parent, "Roof", new Vector3(c.center.x, top + 0.6f, c.center.z), new Vector3(w + 2f * Thickness, 1.2f, d + 2f * Thickness));

        // The four sides, just outside the outer walls.
        float offsetZ = d * 0.5f + Thickness * 0.5f - overlap;
        float offsetX = w * 0.5f + Thickness * 0.5f - overlap;
        Slab(parent, "North", new Vector3(c.center.x, middleY, c.center.z + offsetZ), new Vector3(w + 2f * Thickness, height, Thickness));
        Slab(parent, "South", new Vector3(c.center.x, middleY, c.center.z - offsetZ), new Vector3(w + 2f * Thickness, height, Thickness));
        Slab(parent, "East", new Vector3(c.center.x + offsetX, middleY, c.center.z), new Vector3(Thickness, height, d));
        Slab(parent, "West", new Vector3(c.center.x - offsetX, middleY, c.center.z), new Vector3(Thickness, height, d));

        EditorSceneManager.MarkSceneDirty(scene);
        UnityEngine.Debug.Log("Add Sun Shield: built '" + RootName + "' round the building (ceiling " + w.ToString("F1") + " x " + d.ToString("F1") + " m): a roof slab and four side slabs that only " +
                              "cast shadows. Save the scene (Ctrl+S). Use Edit > Undo to take it away.");
    }

    // A box that casts shadows but is never drawn and has no collider.
    private static void Slab(Transform parent, string name, Vector3 center, Vector3 size)
    {
        GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "Shield_" + name;
        slab.transform.SetParent(parent, false);
        slab.transform.position = center;
        slab.transform.localScale = size;

        Collider collider = slab.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        MeshRenderer renderer = slab.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
}

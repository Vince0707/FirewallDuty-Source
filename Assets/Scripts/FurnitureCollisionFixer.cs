using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Makes furniture solid. Some props in the office (tables, the whiteboard, cabinets, sofas, ...) came without colliders, so the player
// walked through them. Every time a scene with the player in it loads, each prop that has no solid collider gets a box collider the
// size of its mesh. Nothing is saved into the scene: the boxes are added when the scene starts.
//
// Left alone on purpose: anything already solid; doors, door frames, walls, windows, glass, floors and ceilings (they have openings, and a
// box would close the doorway); things hanging above head height; very small or very flat things; very large combined models (a whole
// cubicle as one mesh would be closed off); characters; and anything the player interacts with (they have colliders of their own).
public static class FurnitureCollisionFixer
{
    private static readonly string[] Skip =
    {
        "door", "frame", "wall", "window", "glass", "floor", "ceiling", "carpet", "rug", "poster", "sign", "label", "fan", "light", "lamp",
        "curtain", "cable", "outlet", "switch", "vent", "blind", "skirting", "baseboard", "stair", "ramp", "step", "plane", "terrain",
        "ground", "grass", "separator", "entrance", "collider", "trigger", "navmesh", "npc_", "screenui"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only the 3D scenes (the ones with the player).
        if (Object.FindAnyObjectByType<PlayerMobileController>() == null)
            return;

        int added = 0;
        StringBuilder names = new StringBuilder();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (TryAddCollider(renderer))
                {
                    added++;
                    if (added <= 40)
                        names.Append("\n  ").Append(renderer.name);
                }
            }
        }

        Debug.Log("[Collision] Added " + added + " collider(s) to furniture without one in '" + scene.name + "'" +
                  (added > 0 ? ":" + names + (added > 40 ? "\n  ..." : "") : "."));
    }

    private static bool TryAddCollider(MeshRenderer renderer)
    {
        GameObject go = renderer.gameObject;

        MeshFilter filter = go.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return false;

        if (HasSolidCollider(go.transform))
            return false;

        if (IsExcluded(go.transform))
            return false;

        Bounds world = renderer.bounds;
        Vector3 size = world.size;

        if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 0.25f)     // small things
            return false;

        if (size.y < 0.08f)                                             // flat things on the floor
            return false;

        if (world.min.y > 1.9f)                                         // hanging above head height
            return false;

        if (Mathf.Max(size.x, size.z) > 4.5f || size.x * size.z > 8f)  // large combined models: a box would close off the space inside
            return false;

        Bounds local = filter.sharedMesh.bounds;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = local.center;
        box.size = local.size;
        go.AddComponent<InteractionPassThrough>();   // solid for walking, but the crosshair can reach people and things behind it
        return true;
    }

    private static bool HasSolidCollider(Transform t)
    {
        foreach (Collider collider in t.GetComponentsInParent<Collider>(false))
        {
            if (collider.enabled && !collider.isTrigger)
                return true;
        }

        // A solid collider below it (a table made of a top and legs, one of them solid) also counts.
        foreach (Collider collider in t.GetComponentsInChildren<Collider>(false))
        {
            if (collider.enabled && !collider.isTrigger)
                return true;
        }

        return false;
    }

    private static bool IsExcluded(Transform t)
    {
        // Characters, the player, canvases and anything interactive.
        if (t.GetComponentInParent<Animator>() != null || t.GetComponentInParent<PlayerMobileController>() != null ||
            t.GetComponentInParent<Canvas>() != null || t.GetComponentInParent<Interactable>() != null ||
            t.GetComponentInParent<NpcWorker>() != null)
            return true;

        // The names of this object and the ones above it.
        string path = "";
        int levels = 0;
        for (Transform current = t; current != null && levels < 6; current = current.parent, levels++)
            path += current.name.ToLowerInvariant() + "/";

        foreach (string word in Skip)
        {
            if (path.Contains(word))
                return true;
        }

        return false;
    }
}

// Put on furniture that got a collider from FurnitureCollisionFixer. It stops the player walking through it, but PlayerInteraction looks
// past it, so a character behind a counter or a desk (the receptionist, the Head Manager) can still be talked to.
public class InteractionPassThrough : MonoBehaviour
{
}

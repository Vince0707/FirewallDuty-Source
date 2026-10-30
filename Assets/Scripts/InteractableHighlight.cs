using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws a glow over an Interactable while it is the crosshair target.
//   - Things made of ordinary meshes (doors, chairs, ...) get a rim-lit overlay over the whole object.
//   - Characters (skinned meshes) get just an outline round their edge, so the player sees "this person can be talked to" without the body being covered.
//     The outline is a second copy of the character's skin, sharing the same bones (so it always follows the character exactly, in every pose), a hair
//     bigger, with only its back faces showing.
// It only adds extra drawing, so the object's own materials are never modified.
// Added automatically by Interactable when missing; add it by hand to tweak the look per object.
public class InteractableHighlight : MonoBehaviour
{
    private const string ShaderResource = "FirewallDuty/HighlightOverlay";
    private const string OutlineShaderResource = "FirewallDuty/OutlineHull";

    [SerializeField] private Color color = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.25f;
    [SerializeField, Range(0.5f, 8f)] private float rimPower = 2.5f;
    [Tooltip("Brightness pulse speed. 0 = steady glow.")]
    [SerializeField, Min(0f)] private float pulseSpeed = 3f;
    [Tooltip("How thick the outline round a character is, in metres.")]
    [SerializeField, Range(0.004f, 0.05f)] private float outlineWidth = 0.014f;
    [Tooltip("Meshes to highlight. Leave empty to use every mesh under this object that does not belong to another Interactable.")]
    [SerializeField] private MeshRenderer[] targetRenderers;

    // One skinned mesh of a character and its outline copy.
    private class Skinned
    {
        public SkinnedMeshRenderer source;
        public SkinnedMeshRenderer hull;
        public Mesh smoothMesh;
    }

    private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    private readonly List<MeshFilter> filters = new List<MeshFilter>();
    private readonly List<Skinned> skinned = new List<Skinned>();
    private Material overlay;
    private Material outline;
    private Interactable owner;
    private bool highlighted;
    private bool collected;
    private bool outlineShown;
    private bool warnedMissingShader;
    private bool warnedStatic;

    public bool IsHighlighted => highlighted;

    public void SetHighlighted(bool value)
    {
        highlighted = value;

        if (!value)
            ShowOutline(false);
    }

    private void OnDisable()
    {
        highlighted = false;
        ShowOutline(false);
    }

    private void OnDestroy()
    {
        if (overlay != null)
            Destroy(overlay);

        if (outline != null)
            Destroy(outline);

        foreach (Skinned item in skinned)
        {
            if (item.hull != null)
                Destroy(item.hull.gameObject);

            if (item.smoothMesh != null)
                Destroy(item.smoothMesh);
        }
    }

    private void LateUpdate()
    {
        // An object can say it should not glow right now (a character that is not part of the current task).
        if (owner == null)
            owner = GetComponent<Interactable>();

        bool show = highlighted && (owner == null || owner.WantsHighlight);

        if (!show)
        {
            ShowOutline(false);
            return;
        }

        if (!collected)
            CollectRenderers();

        float pulse = pulseSpeed > 0f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * pulseSpeed) : 1f;

        if (renderers.Count > 0 && EnsureOverlay())
            DrawOverlay(pulse);

        if (skinned.Count > 0 && EnsureOutline())
        {
            outline.SetColor("_Color", color);
            outline.SetFloat("_Width", outlineWidth);
            outline.SetFloat("_Intensity", pulse);
            ShowOutline(true);
        }
    }

    // ---- The rim-lit overlay on ordinary meshes -------------------------------------------------------------------------

    private void DrawOverlay(float pulse)
    {
        overlay.SetColor("_Color", color);
        overlay.SetFloat("_FillAlpha", fillAlpha);
        overlay.SetFloat("_RimPower", rimPower);
        overlay.SetFloat("_Intensity", pulse);

        for (int i = 0; i < renderers.Count; i++)
        {
            MeshRenderer r = renderers[i];
            Mesh mesh = filters[i] != null ? filters[i].sharedMesh : null;
            if (r == null || mesh == null || !r.enabled || !r.gameObject.activeInHierarchy)
                continue;

            Matrix4x4 matrix = r.localToWorldMatrix;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                Graphics.DrawMesh(mesh, matrix, overlay, r.gameObject.layer, null, sub, null, ShadowCastingMode.Off, false);
        }
    }

    // ---- The outline round a character ----------------------------------------------------------------------------------

    // Switches the outline copies on or off (they only exist for characters).
    private void ShowOutline(bool on)
    {
        if (outlineShown == on)
            return;

        outlineShown = on;

        foreach (Skinned item in skinned)
        {
            if (item.hull != null)
                item.hull.enabled = on && item.source != null && item.source.enabled;
        }
    }

    private bool EnsureOverlay()
    {
        if (overlay != null)
            return true;

        Shader shader = Resources.Load<Shader>(ShaderResource);
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            if (!warnedMissingShader)
            {
                UnityEngine.Debug.LogWarning("InteractableHighlight: overlay shader not found (Assets/Resources/" + ShaderResource + ".shader).", this);
                warnedMissingShader = true;
            }
            return false;
        }

        overlay = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return true;
    }

    private bool EnsureOutline()
    {
        if (outline != null)
            return true;

        Shader shader = Resources.Load<Shader>(OutlineShaderResource);
        if (shader == null)
        {
            if (!warnedMissingShader)
            {
                UnityEngine.Debug.LogWarning("InteractableHighlight: outline shader not found (Assets/Resources/" + OutlineShaderResource + ".shader).", this);
                warnedMissingShader = true;
            }
            return false;
        }

        outline = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        // The copies made so far (they are made before the material) get it now.
        foreach (Skinned item in skinned)
            ApplyOutlineMaterial(item);

        return true;
    }

    private void ApplyOutlineMaterial(Skinned item)
    {
        if (item.hull == null || outline == null)
            return;

        Material[] materials = new Material[Mathf.Max(1, item.smoothMesh.subMeshCount)];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = outline;

        item.hull.sharedMaterials = materials;
    }

    private void CollectRenderers()
    {
        collected = true;
        renderers.Clear();
        filters.Clear();
        skinned.Clear();

        MeshRenderer[] source = targetRenderers != null && targetRenderers.Length > 0
            ? targetRenderers
            : GetComponentsInChildren<MeshRenderer>();

        Interactable owner = GetComponent<Interactable>();

        foreach (MeshRenderer r in source)
        {
            if (r == null)
                continue;

            // Skip meshes that belong to a different interactable nested under this one.
            if (owner != null && r.GetComponentInParent<Interactable>() != owner)
                continue;

            // A Batching Static mesh has been merged with every other static object that shares its material, so the
            // "mesh" is the whole merged batch: drawing it would light up all of them at once. Never draw those.
            if (r.isPartOfStaticBatch)
            {
                if (!warnedStatic)
                {
                    UnityEngine.Debug.LogWarning(name + " is Batching Static, so it cannot be highlighted on its own (it would light up every object merged with it). " +
                                                 "Untick Static on it, or run Tools > Firewall Duty > Set Up Sittable Chairs for chairs.", this);
                    warnedStatic = true;
                }

                continue;
            }

            MeshFilter filter = r.GetComponent<MeshFilter>();
            if (filter == null)
                continue;

            renderers.Add(r);
            filters.Add(filter);
        }

        // Characters: each skinned mesh gets an outline copy (switched off until it is needed).
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (r == null || r.sharedMesh == null)
                continue;

            if (owner != null && r.GetComponentInParent<Interactable>() != owner)
                continue;

            Skinned item = MakeSkinned(r);
            ApplyOutlineMaterial(item);
            skinned.Add(item);
        }
    }

    // A copy of the skin: the same mesh with its normals smoothed over vertices that sit at the same place (so the pushed-out copy does not split open along
    // hard edges), using the same bones as the original, so it moves exactly with it.
    private static Skinned MakeSkinned(SkinnedMeshRenderer source)
    {
        Mesh smooth = Instantiate(source.sharedMesh);
        smooth.name = source.sharedMesh.name + " (outline)";

        // (A mesh that is not readable on the phone - the default for imported models - cannot be smoothed; it is used as it is, which only means a few
        // small cracks in the line where the model has hard edges.)
        Vector3[] vertices = smooth.isReadable ? smooth.vertices : null;
        Vector3[] normals = smooth.isReadable ? smooth.normals : null;

        if (vertices != null && normals != null && normals.Length == vertices.Length)
        {
            Dictionary<Vector3Int, Vector3> sums = new Dictionary<Vector3Int, Vector3>();
            Vector3Int[] keys = new Vector3Int[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i] * 2000f;   // positions closer than half a millimetre (in the mesh's own units) count as the same place
                keys[i] = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                sums.TryGetValue(keys[i], out Vector3 sum);
                sums[keys[i]] = sum + normals[i];
            }

            for (int i = 0; i < normals.Length; i++)
                normals[i] = sums[keys[i]].normalized;

            smooth.normals = normals;
        }

        GameObject go = new GameObject("OutlineHull");
        go.transform.SetParent(source.transform, false);

        SkinnedMeshRenderer hull = go.AddComponent<SkinnedMeshRenderer>();
        hull.sharedMesh = smooth;
        hull.bones = source.bones;
        hull.rootBone = source.rootBone;
        hull.localBounds = source.localBounds;
        hull.updateWhenOffscreen = source.updateWhenOffscreen;
        hull.shadowCastingMode = ShadowCastingMode.Off;
        hull.receiveShadows = false;
        hull.lightProbeUsage = LightProbeUsage.Off;
        hull.reflectionProbeUsage = ReflectionProbeUsage.Off;
        hull.enabled = false;

        return new Skinned { source = source, hull = hull, smoothMesh = smooth };
    }
}

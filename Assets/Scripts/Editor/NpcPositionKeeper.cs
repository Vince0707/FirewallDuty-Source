using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Keeps a position you set in Play Mode. Unity throws away every change made in Play Mode when Play stops; this
// remembers where you put an NPC and writes it into the scene as soon as you are back in Edit Mode.
//
//   1. In Play Mode, drag/rotate the NPC (or the NPC's seated body) until it sits right.
//   2. With the NPC selected, press Ctrl+Alt+P  (Tools > Firewall Duty > NPC > Save Selected NPC Position).
//   3. Stop Play. The position is stored in the scene automatically; save the scene (Ctrl+S).
[InitializeOnLoad]
public static class NpcPositionKeeper
{
    private const string PrefsKey = "FirewallDuty.NpcSavedPositions";

    [Serializable]
    private class Entry
    {
        public string npc;
        public Vector3 position;
        public float yaw;
    }

    [Serializable]
    private class Store
    {
        public List<Entry> entries = new List<Entry>();
    }

    static NpcPositionKeeper()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Firewall Duty/NPC/Save Selected NPC Position (use in Play Mode) %&p")]
    public static void SaveSelected()
    {
        if (!Application.isPlaying)
        {
            UnityEngine.Debug.LogWarning("Save Selected NPC Position is for Play Mode: press Play, move the NPC until it sits right, select it, then use this. " +
                                         "In Edit Mode, edit 'Seat Offset' on its Npc Worker instead.");
            return;
        }

        Store store = Load();
        StringBuilder report = new StringBuilder();

        foreach (Transform selected in Selection.transforms)
        {
            // A click in the Scene view may select a child (the mesh or a bone); climb to the NPC root.
            Transform root = selected;
            while (root != null && !NpcSetupTool.IsNpc(root))
                root = root.parent;

            if (root == null || root.GetComponent<NpcWorker>() == null)
                continue;

            store.entries.RemoveAll(e => e.npc == root.name);
            store.entries.Add(new Entry { npc = root.name, position = root.position, yaw = root.eulerAngles.y });
            report.AppendLine("Saved " + root.name + " at " + root.position.ToString("F3") + ", facing " + root.eulerAngles.y.ToString("F0") + " degrees.");
        }

        if (report.Length == 0)
        {
            UnityEngine.Debug.LogWarning("Select an NPC that has an Npc Worker (the receptionist, a cubicle or IT worker, the kitchen sitter) and try again.");
            return;
        }

        Save(store);
        report.AppendLine("It will be written into the scene when you stop Play. Then save the scene (Ctrl+S).");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        Store store = Load();
        if (store.entries.Count == 0)
            return;

        StringBuilder report = new StringBuilder();
        List<Entry> failed = new List<Entry>();

        foreach (Entry entry in store.entries)
        {
            string result = NpcFixedPositionTool.ApplyTo(entry.npc, entry.position, entry.yaw);
            report.AppendLine("[" + entry.npc + "] " + result);

            if (result.StartsWith("SKIPPED"))
                failed.Add(entry);
        }

        // Keep anything that could not be applied, so it is tried again after the problem is fixed.
        store.entries = failed;
        Save(store);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        report.AppendLine("Saved Play Mode positions written into the scene. Save the scene (Ctrl+S) to keep them.");
        UnityEngine.Debug.Log(report.ToString());
    }

    private static Store Load()
    {
        string json = EditorPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json))
            return new Store();

        try
        {
            return JsonUtility.FromJson<Store>(json) ?? new Store();
        }
        catch (Exception)
        {
            return new Store();
        }
    }

    private static void Save(Store store)
    {
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(store));
    }
}

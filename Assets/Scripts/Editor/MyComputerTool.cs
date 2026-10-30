using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Firewall Duty > Mission > Mark Selected Computer As Mine
// Select the computer that is the player's in the Hierarchy (the computer itself, or its ScreenUI), then run this. The mission then
// uses it for "YOUR COMPUTER" (the arrow, and Check Email). Run it on another computer to move the mark. Save the scene afterwards.
public static class MyComputerTool
{
    [MenuItem("Tools/Firewall Duty/Mission/Mark Selected Computer As Mine")]
    public static void Mark()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Select the player's computer in the Hierarchy first.");
            return;
        }

        // Take the computer rather than its screen, when the selection is the ScreenUI.
        GameObject target = selected;
        if (selected.GetComponent<ComputerScreenDisplay>() != null && selected.transform.parent != null)
            target = selected.transform.parent.gameObject;

        // One mark only.
        foreach (MyComputerMarker old in Object.FindObjectsByType<MyComputerMarker>(FindObjectsInactive.Exclude))
            Undo.DestroyObjectImmediate(old);

        Undo.AddComponent<MyComputerMarker>(target);
        EditorSceneManager.MarkSceneDirty(target.scene);
        Debug.Log("'" + target.name + "' is now marked as the player's computer. Save the scene (Ctrl+S).");
    }
}

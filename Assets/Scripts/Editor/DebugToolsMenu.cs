using UnityEditor;
using UnityEngine;

// Tools > Firewall Duty > Testing > Toggle Debug Buttons
// The DEBUG FINISH button is hidden while playing. Switch this on when you want it for testing.
public static class DebugToolsMenu
{
    [MenuItem("Tools/Firewall Duty/Testing/Toggle Debug Buttons")]
    public static void Toggle()
    {
        bool on = PlayerPrefs.GetInt(DevelopmentOnly.Key, 0) != 1;
        PlayerPrefs.SetInt(DevelopmentOnly.Key, on ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("Debug buttons are now " + (on ? "ON (DEBUG FINISH shows while playing)." : "OFF."));
    }

    [MenuItem("Tools/Firewall Duty/Testing/Toggle Unlock All Missions")]
    public static void ToggleUnlockAll()
    {
        bool on = PlayerPrefs.GetInt("FirewallDuty.UnlockAll", 0) != 1;
        PlayerPrefs.SetInt("FirewallDuty.UnlockAll", on ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("All missions are now " + (on ? "UNLOCKED (for testing)." : "locked again until the one before is finished."));
    }
}

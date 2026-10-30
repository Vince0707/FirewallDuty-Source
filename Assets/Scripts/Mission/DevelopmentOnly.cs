using UnityEngine;

// Hides testing buttons (like DEBUG FINISH). They stay hidden, in the Editor too, unless the testing tools are switched on:
// PlayerPrefs int "FirewallDuty.DebugTools" = 1 (menu: Tools > Firewall Duty > Testing > Toggle Debug Buttons).
public class DevelopmentOnly : MonoBehaviour
{
    public const string Key = "FirewallDuty.DebugTools";

    private void Awake()
    {
        if (PlayerPrefs.GetInt(Key, 0) != 1)
            gameObject.SetActive(false);
    }
}

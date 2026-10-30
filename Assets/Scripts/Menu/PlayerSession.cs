using System.Collections.Generic;
using UnityEngine;

// One player's record in the "players" collection (the same fields the Android app uses).
public class PlayerData
{
    public string uid;
    public string fullName;
    public string username;
    public string email;
    public int level = 1;
    public int exp;
    public List<string> unlockedAchievements = new List<string>();
    public string photoUri;
    public int totalPoints;
    public int missionsCompleted;
    public int bestScore;
    public int threatsIdentified;
    public int officeProgress;   // the highest office mission finished (missions unlock one after another)
    public int bankProgress;
    public int triviaPerfect;    // 1 once the player has scored 30 / 30 in the trivia challenge (Trivia Master)

    public static PlayerData FromDocument(Dictionary<string, object> doc)
    {
        PlayerData p = new PlayerData();
        object fields = MiniJson.Get(doc, "fields");

        p.uid = Field(fields, "uid");
        p.fullName = Field(fields, "fullName");
        p.username = Field(fields, "username");
        p.email = Field(fields, "email");
        p.photoUri = Field(fields, "photoUri");

        if (int.TryParse(Field(fields, "level"), out int level)) p.level = level;
        if (int.TryParse(Field(fields, "exp"), out int exp)) p.exp = exp;
        p.level = Mathf.Max(p.level, 1 + p.exp / 200);   // 200 EXP per level, as on the Profile
        if (int.TryParse(Field(fields, "totalPoints"), out int points)) p.totalPoints = points;
        if (int.TryParse(Field(fields, "missionsCompleted"), out int missions)) p.missionsCompleted = missions;
        if (int.TryParse(Field(fields, "bestScore"), out int best)) p.bestScore = best;
        if (int.TryParse(Field(fields, "threatsIdentified"), out int threats)) p.threatsIdentified = threats;
        if (int.TryParse(Field(fields, "officeProgress"), out int officeDone)) p.officeProgress = officeDone;
        if (int.TryParse(Field(fields, "bankProgress"), out int bankDone)) p.bankProgress = bankDone;
        if (int.TryParse(Field(fields, "triviaPerfect"), out int perfect)) p.triviaPerfect = perfect;

        List<object> values = MiniJson.Get(fields, "unlockedAchievements", "arrayValue", "values") as List<object>;
        if (values != null)
        {
            foreach (object v in values)
            {
                string s = MiniJson.GetString(v, "stringValue");
                if (!string.IsNullOrEmpty(s))
                    p.unlockedAchievements.Add(s);
            }
        }

        // The document name ends with the uid, in case the field is missing.
        if (string.IsNullOrEmpty(p.uid))
        {
            string name = MiniJson.GetString(doc, "name");
            if (!string.IsNullOrEmpty(name))
                p.uid = name.Substring(name.LastIndexOf('/') + 1);
        }

        return p;
    }

    // A Firestore value is wrapped in its type: {"stringValue": "..."}, {"integerValue": "5"} ...
    private static string Field(object fields, string key)
    {
        object v = MiniJson.Get(fields, key);
        Dictionary<string, object> typed = v as Dictionary<string, object>;
        if (typed == null)
            return null;

        foreach (KeyValuePair<string, object> kv in typed)
        {
            if (kv.Key == "stringValue" || kv.Key == "integerValue" || kv.Key == "doubleValue" || kv.Key == "booleanValue")
                return System.Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }
}

// Who is playing right now, and the small things the app remembers on the phone (like Android SharedPreferences).
public static class PlayerSession
{
    public static PlayerData Player { get; private set; }
    public static bool IsSignedIn => Player != null;

    public static void SignIn(PlayerData player)
    {
        if (Player != null && Player.uid == player.uid && Player.exp > player.exp)
        {
            // This phone has progress the online record does not have yet (a save that has not arrived): keep it and save it again.
            Player.username = player.username;
            Player.fullName = player.fullName;
            Player.email = player.email;
            Player.photoUri = player.photoUri;
            RecalculateAchievements();
            SaveProgress(null);
            return;
        }

        Player = player;

        // The certificates always follow from what the player has really done (see RecalculateAchievements).
        if (RecalculateAchievements())
            SaveProgress(null);
    }


    public static void SignOut()
    {
        Player = null;
        FirebaseRest.SignOut();
        ClearSession();
    }

    // ---- Staying signed in (a Firebase refresh token on the phone; never the password) ---------------------------

    public static bool HasSavedSession => !string.IsNullOrEmpty(PlayerPrefs.GetString("sessionRefresh", ""));
    public static string SavedRefreshToken => PlayerPrefs.GetString("sessionRefresh", "");

    public static void SaveSession(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return;

        PlayerPrefs.SetString("sessionRefresh", refreshToken);
        PlayerPrefs.Save();
    }

    public static void ClearSession()
    {
        PlayerPrefs.DeleteKey("sessionRefresh");
        PlayerPrefs.Save();
    }

    // ---- Remember me (email only, never the password) --------------------------------------------------------

    public static string RememberedEmail => PlayerPrefs.GetInt("remember", 0) == 1 ? PlayerPrefs.GetString("email", "") : "";
    public static bool RememberMe => PlayerPrefs.GetInt("remember", 0) == 1;

    public static void SaveRememberedEmail(string email, bool remember)
    {
        if (remember)
        {
            PlayerPrefs.SetString("email", email);
            PlayerPrefs.SetInt("remember", 1);
        }
        else
        {
            PlayerPrefs.DeleteKey("email");
            PlayerPrefs.SetInt("remember", 0);
        }

        PlayerPrefs.Save();
    }

    // ---- Unfinished mission ("Continue") ---------------------------------------------------------------------

    // Every mission that was started and not finished (newest first), each with the date and time it was started. The older single-mission save is
    // moved into the list the first time it is read.
    private static string PendingKey => "pendingMission_" + (Player != null ? Player.uid : "guest");
    private static string PendingListKey => "pendingMissions_" + (Player != null ? Player.uid : "guest");

    [System.Serializable]
    private class PendingList
    {
        public List<MissionRequest> items = new List<MissionRequest>();
    }

    public static List<MissionRequest> PendingMissions
    {
        get
        {
            string json = PlayerPrefs.GetString(PendingListKey, "");
            PendingList list = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PendingList>(json);
            List<MissionRequest> items = list != null && list.items != null ? list.items : new List<MissionRequest>();

            // The old save held one mission and no date.
            string old = PlayerPrefs.GetString(PendingKey, "");
            if (!string.IsNullOrEmpty(old))
            {
                MissionRequest single = JsonUtility.FromJson<MissionRequest>(old);
                if (single != null && !string.IsNullOrEmpty(single.environment) && !items.Exists(m => m.environment == single.environment && m.sceneNumber == single.sceneNumber))
                    items.Add(single);

                PlayerPrefs.DeleteKey(PendingKey);
                WritePending(items);
            }

            return items;
        }
    }

    // The most recent unfinished mission (null when there is none).
    public static MissionRequest PendingMission
    {
        get
        {
            List<MissionRequest> items = PendingMissions;
            return items.Count > 0 ? items[0] : null;
        }
    }

    // A mission that is started now: it is added to the list (or moved to the top, with a new time, if it is already there).
    public static void SavePendingMission(MissionRequest mission)
    {
        List<MissionRequest> items = PendingMissions;
        items.RemoveAll(m => m.environment == mission.environment && m.sceneNumber == mission.sceneNumber);

        MissionRequest copy = JsonUtility.FromJson<MissionRequest>(JsonUtility.ToJson(mission));
        copy.startedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        items.Insert(0, copy);

        WritePending(items);
    }

    // A mission that is finished is no longer waiting to be continued.
    public static void ClearPendingMission(string environment, int sceneNumber)
    {
        List<MissionRequest> items = PendingMissions;
        items.RemoveAll(m => m.environment == environment && m.sceneNumber == sceneNumber);
        WritePending(items);
    }

    private static void WritePending(List<MissionRequest> items)
    {
        PlayerPrefs.SetString(PendingListKey, JsonUtility.ToJson(new PendingList { items = items }));
        PlayerPrefs.Save();
    }

    // ---- Progress ---------------------------------------------------------------------------------------------

    // As in the Android profile: exp is a running total and the bar shows exp within the current 200.
    public const int ExpPerLevel = 200;

    // Adds experience, levels up when the bar fills, and saves to Firestore (best effort).
    public static void AddExp(int amount, System.Action<bool> saved = null)
    {
        if (Player == null || amount <= 0)
        {
            saved?.Invoke(false);
            return;
        }

        Player.exp += amount;
        Player.level = Mathf.Max(Player.level, 1 + Player.exp / ExpPerLevel);

        SaveProgress(saved);
    }

    // ---- Missions unlock one after another ----------------------------------------------------------------------

    private static string ProgressKey(string environment)
    {
        return "progress_" + (Player != null ? Player.uid : "guest") + "_" + (MissionData.IsBank(environment) ? "bank" : "office");
    }

    // The highest mission of this environment the player has finished (kept on the phone and in the player's record).
    public static int Progress(string environment)
    {
        int saved = PlayerPrefs.GetInt(ProgressKey(environment), 0);
        int online = Player == null ? 0 : (MissionData.IsBank(environment) ? Player.bankProgress : Player.officeProgress);
        return Mathf.Max(saved, online);
    }

    // Mission 1 is always open; each next one opens when the one before it is finished. (Testing: the editor menu
    // Tools > Firewall Duty > Testing > Toggle Unlock All Missions opens them all.)
    public static bool IsSceneUnlocked(string environment, int sceneNumber)
    {
        if (PlayerPrefs.GetInt("FirewallDuty.UnlockAll", 0) == 1)
            return true;

        return sceneNumber <= 1 || sceneNumber <= Progress(environment) + 1;
    }

    public static void CompleteMission(string environment, int sceneNumber)
    {
        if (Progress(environment) >= sceneNumber)
            return;

        PlayerPrefs.SetInt(ProgressKey(environment), sceneNumber);
        PlayerPrefs.Save();

        if (Player != null)
        {
            if (MissionData.IsBank(environment)) Player.bankProgress = sceneNumber;
            else Player.officeProgress = sceneNumber;
        }
    }

    // Which certificate a finished mission earns, by its threat (the ids the Achievements screen uses).
    public static string CertificateFor(string threat)
    {
        switch ((threat ?? "").Trim().ToLowerInvariant())
        {
            case "phishing email": return "phishing_detection";
            case "malware files": return "malware_detection";
            case "fake pop-up windows": return "popup_protection";
            case "spam email": return "spam_mail_analyst";
            case "social engineering": return "social_engineering_awareness";
            case "unauthorized access": return "access_control_defender";
            default: return null;
        }
    }

    // After a mission: experience and level, the four totals on the Profile screen, the progress that unlocks the next mission, and the
    // certificates. Saved to Firestore in one go.
    public static void RecordMission(MissionResult result)
    {
        if (Player == null || result == null)
            return;

        bool[] done = result.objectives ?? new bool[0];
        bool finalDone = done.Length > 0 && done[done.Length - 1];

        // Experience and points are earned once per mission: the first time it is finished. Playing it again, or not finishing it,
        // gives none (the Mission Outcome screen says so).
        bool firstTime = finalDone && Progress(result.environment) < result.sceneNumber;
        result.alreadyCompleted = finalDone && !firstTime;

        if (!firstTime)
        {
            result.expGained = 0;
            return;
        }

        Player.exp += result.expGained;
        Player.level = Mathf.Max(Player.level, 1 + Player.exp / ExpPerLevel);
        Player.totalPoints += result.expGained;
        Player.bestScore = Mathf.Max(Player.bestScore, result.expGained);
        Player.missionsCompleted++;

        bool threatFound = done.Length > 4 && done[4];
        if (threatFound) Player.threatsIdentified++;

        CompleteMission(result.environment, result.sceneNumber);

        RecalculateAchievements();
        SaveProgress(null);
    }

    // ---- Certificates ---------------------------------------------------------------------------------------------
    // A threat certificate is earned when EVERY mission about that threat has been finished, in both environments (for example Phishing
    // Detection: the phishing missions of the office and of the bank). Trivia Master needs a perfect trivia score. The list is always
    // worked out from what the player has really done, so a certificate can never be there by mistake.

    private static readonly string[] AllThreats =
    {
        "Phishing Email", "Malware Files", "Fake Pop-up Windows", "Spam Email", "Social Engineering", "Unauthorized Access"
    };

    // Have all the missions of this threat been finished (in the office and in the bank)?
    public static bool AllMissionsDone(string threat)
    {
        bool any = false;

        for (int i = 0; i < MissionData.OfficeThreats.Length; i++)
        {
            if (string.Equals(MissionData.OfficeThreats[i], threat, System.StringComparison.OrdinalIgnoreCase))
            {
                any = true;
                if (i + 1 > Progress("office"))
                    return false;
            }
        }

        for (int i = 0; i < MissionData.BankThreats.Length; i++)
        {
            if (string.Equals(MissionData.BankThreats[i], threat, System.StringComparison.OrdinalIgnoreCase))
            {
                any = true;
                if (i + 1 > Progress("bank"))
                    return false;
            }
        }

        return any;
    }

    // Rebuilds the player's certificate list. True when it was not already like that.
    public static bool RecalculateAchievements()
    {
        if (Player == null)
            return false;

        List<string> earned = new List<string>();

        foreach (string threat in AllThreats)
        {
            string id = CertificateFor(threat);
            if (id != null && AllMissionsDone(threat))
                earned.Add(id);
        }

        if (Player.triviaPerfect == 1)
            earned.Add("trivia_master");

        bool same = earned.Count == Player.unlockedAchievements.Count;
        if (same)
        {
            foreach (string id in earned)
            {
                if (!Player.unlockedAchievements.Contains(id))
                {
                    same = false;
                    break;
                }
            }
        }

        Player.unlockedAchievements = earned;
        return !same;
    }

    // Called when the trivia challenge ends with every answer right.
    public static void MarkTriviaPerfect(System.Action<bool> saved = null)
    {
        if (Player == null)
        {
            saved?.Invoke(false);
            return;
        }

        Player.triviaPerfect = 1;
        RecalculateAchievements();
        SaveProgress(saved);
    }

    // Adds an achievement id (like "trivia_master") to the player's unlocked list and saves it. Safe to call again.
    public static void UnlockAchievement(string id, System.Action<bool> saved = null)
    {
        if (Player == null)
        {
            saved?.Invoke(false);
            return;
        }

        if (!Player.unlockedAchievements.Contains(id))
            Player.unlockedAchievements.Add(id);

        SaveProgress(saved);
    }

    // The reason the last save failed ("" when it worked). The menu shows it, so a refused save is not silent.
    public static string SaveError = "";

    public static void SaveProgress(System.Action<bool> saved = null)
    {
        if (Player == null)
        {
            saved?.Invoke(false);
            return;
        }

        // Two requests, so that if the database refuses one kind of field the other still gets through, and the message names which.
        Dictionary<string, object> stats = new Dictionary<string, object>
        {
            { "level", Player.level },
            { "exp", Player.exp },
            { "totalPoints", Player.totalPoints },
            { "missionsCompleted", Player.missionsCompleted },
            { "bestScore", Player.bestScore },
            { "threatsIdentified", Player.threatsIdentified }
        };

        Dictionary<string, object> progress = new Dictionary<string, object>
        {
            { "unlockedAchievements", Player.unlockedAchievements },
            { "officeProgress", Player.officeProgress },
            { "bankProgress", Player.bankProgress },
            { "triviaPerfect", Player.triviaPerfect }
        };

        string uid = Player.uid;
        string failure = "";
        int pending = 2;

        System.Action<string, FirebaseRest.Result> finished = (what, result) =>
        {
            if (!result.ok)
                failure += (failure.Length > 0 ? " | " : "") + what + ": " + result.error;

            pending--;
            if (pending > 0)
                return;

            SaveError = failure;
            if (failure.Length > 0)
            {
                Debug.LogWarning("[PlayerSession] Saving progress to Firestore failed - " + failure + "  (signed in as " + FirebaseRest.AuthUid + ", saving to players/" + uid + ")");

                // While the menu is on screen, say so.
                if (MenuApp.Instance != null)
                    MenuApp.Instance.Toast("Could not save your progress online - " + failure);
            }

            saved?.Invoke(failure.Length == 0);
        };

        FirebaseRest.SetPlayerFields(uid, stats, result => finished("EXP and totals", result));
        FirebaseRest.SetPlayerFields(uid, progress, result => finished("mission progress", result));
    }
}

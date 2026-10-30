using System;

// A mission chosen in the menu, passed to the 3D game.
[Serializable]
public class MissionRequest
{
    public string environment;   // "office" or "bank"
    public int sceneNumber;      // 1..10
    public string missionTitle;
    public string threatType;
    public string startedAt;     // when it was started ("yyyy-MM-dd HH:mm"), for the Continue page
}

// What happened in the mission, passed back to the Mission Outcome screen.
[Serializable]
public class MissionResult
{
    public string environment;
    public int sceneNumber;
    public string missionTitle;
    public string threatType;
    public int timeRemainingSeconds;
    public int timeLimitSeconds;
    public int expGained;
    public bool[] objectives;
    public bool alreadyCompleted;   // the mission had been finished before, so no EXP this time
    public bool timedOut;           // the time ran out: the Mission Outcome screen offers RESTART
}

// The hand-over between the menu scene and the mission scenes.
public static class MissionFlow
{
    public static MissionRequest Current;
    public static MissionResult LastResult;
}

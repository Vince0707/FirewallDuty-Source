using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The mission being played in the 3D scene. The menu (Mission Briefing > Start Mission) stores the chosen mission in
// MissionFlow.Current and loads this scene; this runs the 6 minute timer and the six objectives, and when the mission ends it
// stores the result and goes back to the menu, which shows the Mission Outcome screen.
//
// Other game scripts only need two calls:
//     MissionSession.Instance.CompleteObjective(index);   // index 0..5, in the order of the briefing's objectives
//     MissionSession.Instance.EndMission();               // when the player submits the final response (or gives up)
// The timer reaching zero also ends the mission.
public class MissionSession : MonoBehaviour
{
    public static MissionSession Instance { get; private set; }

    [Header("Test values (used when this scene is played directly in the Editor, without the menu)")]
    [SerializeField] private string environment = "office";
    [SerializeField, Min(1)] private int sceneNumber = 1;
    [SerializeField] private string missionTitle = "Payroll Information Update";
    [SerializeField] private string threatType = "Phishing Email";

    [Header("Rules")]
    [SerializeField, Min(10f)] private float timeLimitSeconds = 360f;
    [SerializeField, Min(1)] private int objectiveCount = 6;
    [Tooltip("Experience for each objective completed.")]
    [SerializeField, Min(0)] private int expPerObjective = 10;
    [Tooltip("Extra experience when the last objective (submitting the correct response) is completed.")]
    [SerializeField, Min(0)] private int expBonusForFinalObjective = 10;
    [Tooltip("Experience (and points) taken off for each WRONG response chosen in the last objective. Each wrong option is counted once.")]
    [SerializeField, Min(0)] private int expPenaltyPerWrongResponse = 10;

    private bool[] objectivesDone;
    private readonly HashSet<int> wrongResponses = new HashSet<int>();   // the wrong responses already chosen (each costs experience once)
    private float timeRemaining;
    private bool ended;
    private bool clockRunning = true;   // the first mission waits for the player to time in at the receptionist (MissionIntro)

    public string Environment => environment;
    public int SceneNumber => sceneNumber;
    public string MissionTitle => missionTitle;
    public string ThreatType => threatType;
    public float TimeLimit => timeLimitSeconds;
    public float TimeRemaining => Mathf.Max(0f, timeRemaining);
    public bool HasEnded => ended;

    public event Action<int> ObjectiveCompleted;
    public event Action MissionEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // What the player does in the scene for this mission (the computer and its email).
        if (GetComponent<MissionScenarioController>() == null)
            gameObject.AddComponent<MissionScenarioController>();

        // Started from the menu: use the mission that was chosen there.
        MissionRequest request = MissionFlow.Current;
        if (request != null)
        {
            environment = request.environment;
            sceneNumber = request.sceneNumber;
            missionTitle = request.missionTitle;
            threatType = request.threatType;
        }

        objectivesDone = new bool[objectiveCount];
        timeRemaining = timeLimitSeconds;

        // The arrival in the first mission (taxi ride, time in): the clock only starts once the player has timed in.
        if (MissionIntro.WillRun(environment, sceneNumber))
            clockRunning = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (ended || !clockRunning)
            return;

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            EndMission();
        }
    }

    // A wrong response in the last objective costs experience and points, once per wrong option. Returns how much was taken off (0 if that
    // option was already chosen before).
    public int RegisterWrongResponse(int option)
    {
        if (ended || !wrongResponses.Add(option))
            return 0;

        return expPenaltyPerWrongResponse;
    }

    public bool ClockRunning => clockRunning;

    // Starts the countdown (after the player timed in, or when there is no arrival to wait for).
    public void StartClock()
    {
        clockRunning = true;
    }

    // A mistake costs time.
    public void AddTimePenalty(float seconds)
    {
        if (ended)
            return;

        timeRemaining -= seconds;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            EndMission();
        }
    }

    // index is 0-based, in the order of the objectives shown in the mission briefing.
    public void CompleteObjective(int index)
    {
        if (ended || index < 0 || index >= objectivesDone.Length || objectivesDone[index])
            return;

        objectivesDone[index] = true;
        ObjectiveCompleted?.Invoke(index);
    }

    public bool IsObjectiveDone(int index)
    {
        return index >= 0 && index < objectivesDone.Length && objectivesDone[index];
    }

    public int ObjectivesCompleted
    {
        get
        {
            int count = 0;
            foreach (bool done in objectivesDone)
            {
                if (done)
                    count++;
            }

            return count;
        }
    }

    public int ExpGained
    {
        get
        {
            int exp = ObjectivesCompleted * expPerObjective;

            if (objectivesDone.Length > 0 && objectivesDone[objectivesDone.Length - 1])
                exp += expBonusForFinalObjective;

            exp -= wrongResponses.Count * expPenaltyPerWrongResponse;
            return Mathf.Max(0, exp);
        }
    }

    // "02:34"
    public string FormatTime()
    {
        int seconds = Mathf.CeilToInt(TimeRemaining);
        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    // Ends the mission: saves the result, awards the experience, and returns to the menu's Mission Outcome screen.
    public void EndMission()
    {
        if (ended)
            return;

        ended = true;
        MissionEnded?.Invoke();

        MissionFlow.LastResult = new MissionResult
        {
            environment = environment,
            sceneNumber = sceneNumber,
            missionTitle = missionTitle,
            threatType = threatType,
            timeRemainingSeconds = Mathf.RoundToInt(TimeRemaining),
            timeLimitSeconds = Mathf.RoundToInt(timeLimitSeconds),
            expGained = ExpGained,
            timedOut = TimeRemaining <= 0f,
            objectives = (bool[])objectivesDone.Clone()
        };

        // The mission is over, so there is nothing to continue.
        PlayerSession.ClearPendingMission(environment, sceneNumber);
        PlayerSession.RecordMission(MissionFlow.LastResult);

        MenuApp.StartScreen = "MissionOutcome";
        MissionFlow.Current = null;
        SceneManager.LoadScene(MenuApp.MenuSceneName);
    }

    // The in-game tablet.
    public void OpenTablet()
    {
        if (ended || TabletAccess.Locked)
            return;

        TabletOverlay.Open();
    }

    // For testing: completes four objectives and ends the mission.
    public void DebugFinish()
    {
        for (int i = 0; i < Mathf.Min(4, objectivesDone.Length); i++)
            CompleteObjective(i);

        EndMission();
    }
}

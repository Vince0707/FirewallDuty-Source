using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the steps of the chosen mission (MissionScenarios): who to talk to, what to carry, where to stand, which computer to use. It lives on
// the MissionSession object (added automatically), so the scene needs no editing. It finds the characters and objects by name when the
// mission starts, makes them interactable, and leads the player with the arrow and the hint at the top of the screen.
// A step whose character or object cannot be found in the scene is skipped (with a warning in the Console), so the mission can always be
// finished.
public static class MissionStory
{
    public static int Step;                // the step the player is on (index into the mission's steps)
    public static string Carrying = "";    // what the player is carrying ("" = nothing)
    public static bool ResponseChosen;     // the right response was chosen at the computer

    public static void Reset()
    {
        Step = 0;
        Carrying = "";
        ResponseChosen = false;
    }
}

// Put on a computer (or its screen) in the scene to say "this is the player's computer". The menu
// Tools > Firewall Duty > Mission > Mark Selected Computer As Mine does it. Without it the controller looks for "PC (5)", then for
// the computer nearest to where the player starts.
public class MyComputerMarker : MonoBehaviour
{
}

// Something the player can use with the hand button during a mission (a character, an item, a computer). What it says, when it can be used
// and what it does are set by the controller.
public class MissionAction : Interactable
{
    public System.Func<string> Prompt;
    public System.Func<bool> Available;
    public System.Action Action;
    public System.Func<bool> HighlightWhen;   // when the glow should show (null = whenever it is targeted)
    public bool AllowWhileSeated;   // a computer can be used from the chair
    public int PriorityValue;       // above 0 for what must win over a character standing in front of it (a computer, a place to inspect)

    public override int Priority => PriorityValue;

    public override string PromptText => Prompt != null ? Prompt() : "Interact";

    public override bool WantsHighlight => HighlightWhen == null || HighlightWhen();

    public override bool CanInteract
    {
        get
        {
            MissionSession session = MissionSession.Instance;
            if (session == null || session.HasEnded || MissionDialogue.IsOpen || EmailInvestigationUI.IsOpen || InspectionOverlay.IsOpen || InterviewOverlay.IsOpen)
                return false;

            return Available == null || Available();
        }
    }

    public override void Interact()
    {
        Action?.Invoke();
    }
}

public class MissionScenarioController : MonoBehaviour
{
    public static MissionScenarioController Instance { get; private set; }

    // A place the player can inspect in an Inspect step.
    private class StationState
    {
        public EmailScenario.Station def;
        public Transform thing;
        public Vector3 point;
        public bool resolved;       // the player made the right decision about it
        public Vector3 facing;      // the side it is used from (flat; zero = any): where the screen faces, or toward the person who works there
    }

    // What the controller knows about one step of the scenario.
    private class StepState
    {
        public MissionStep def;
        public bool ok;
        public readonly List<Transform> npcs = new List<Transform>();
        public Bounds zone;                     // Meeting: the room
        public Vector3 facing;                  // Meeting: the side to end the floor trail on (a teller: behind the counter), zero = any
        public Vector3[] via;                   // Meeting: the way in to that side (see FloorGuide.Show)
        public GameObject item;                 // PickUp: the object on the desk
        public ComputerScreenDisplay display;   // Analyze: the computer
        public Vector3 point;                   // Analyze: where its screen is
        public float progress;                  // Meeting: seconds spent so far
        public readonly List<StationState> stations = new List<StationState>();   // Inspect: the places to examine
        public bool[] asked;                    // Interview: the questions already asked
    }

    private EmailScenario scenario;
    private bool ready;
    private Transform player;
    private MissionMarker marker;

    private readonly List<StepState> states = new List<StepState>();
    private readonly Dictionary<Transform, MissionAction> npcActions = new Dictionary<Transform, MissionAction>();
    private readonly Dictionary<Transform, Vector3> tellerSides = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3[]> tellerWays = new Dictionary<Transform, Vector3[]>();
    private readonly Dictionary<StepState, HeldTablet> heldTablets = new Dictionary<StepState, HeldTablet>();   // the tablets held out by Tablet steps
    private readonly HashSet<ComputerScreenDisplay> computersWithAction = new HashSet<ComputerScreenDisplay>();
    private bool meetingInside;
    private ComputerScreenDisplay myComputer;
    private bool myComputerSearched;
    private MissionIntro intro;          // the arrival (taxi, time in) of the first mission
    private Transform timeInReceptionist;

    private void Awake()
    {
        Instance = this;
        MissionStory.Reset();
        EmailInvestigationUI.ResetState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (MissionSession.Instance != null)
            MissionSession.Instance.ObjectiveCompleted -= OnObjectiveSound;

        GameSounds.StopRoomTone();

        MissionSubtitle.Hide();
    }

    private IEnumerator Start()
    {
        MissionSession session = MissionSession.Instance;
        if (session == null)
            yield break;

        // The PAUSE button (Resume / Settings / Quit) is there for the whole mission, the arrival included.
        PauseMenu.Create();
        HudStyle.Apply();   // the TABLET button in the look of the main menu
        gameObject.AddComponent<IndoorSun>();   // no sunlight inside the building

        scenario = MissionScenarios.EmailFor(session.Environment, session.SceneNumber);

        // Mission 1 hands the tablet over during the mission (a Tablet step): until then the TABLET button is dim and cannot be pressed.
        bool handsTablet = false;
        if (scenario != null)
        {
            foreach (MissionStep step in scenario.steps)
            {
                if (step.kind == StepKind.Tablet)
                    handsTablet = true;
            }
        }

        TabletAccess.Begin(handsTablet);

        // Let the scene finish setting itself up (NPCs, screens), then wait for the first frame of play.
        yield return null;

        if (scenario == null)
        {
            session.StartClock();
            MissionBanner.Show(session.MissionTitle, "This mission's action has not been built yet.", 5f);
            yield break;
        }

        PlayerMobileController playerController = FindAnyObjectByType<PlayerMobileController>();
        player = playerController != null ? playerController.transform : null;
        MoveCustomerToPlayersWindow(session);

        // The hint at the top and the arrow are made first, because the arrival uses them too.
        gameObject.AddComponent<MissionHintHud>();
        marker = gameObject.AddComponent<MissionMarker>();
        SetUpTellerArea();

        // Every mission starts with the ride to work (a taxi, a bus in the rain, a taxi in the snow) and the time-in (at the receptionist in the office,
        // at the first teller in the bank); the clock starts after it. See MissionIntro.
        if (MissionIntro.WillRun(session.Environment, session.SceneNumber))
        {
            bool bank = MissionData.IsBank(session.Environment);
            bool arrived = false;

            // The receptionist of the office, or the first teller of the bank (the same character model).
            Transform receptionist = FindOutermost("mesh_office_f_recepcionist01");
            if (player != null && receptionist != null && MissionIntro.RigExists(session.SceneNumber))
            {
                intro = gameObject.AddComponent<MissionIntro>();
                timeInReceptionist = receptionist;
                EnsureNpcAction(receptionist);

                yield return StartCoroutine(intro.Play(player, receptionist, marker, bank));

                intro = null;
                timeInReceptionist = null;
                arrived = true;
            }
            else
            {
                Debug.LogWarning("[Mission] The arrival (vehicle and time-in) was skipped: it needs the street from Tools > Firewall Duty > " +
                                 (bank ? "Bank" : "Office") + " > Build Entrance Street, and " + (bank ? "the first teller." : "the receptionist."));
            }

            session.StartClock();
        }

        // The chime when something is found. (There is no background noise of the building: only the player's footsteps.)
        session.ObjectiveCompleted += OnObjectiveSound;

        foreach (MissionStep step in scenario.steps)
        {
            StepState state = new StepState { def = step };
            states.Add(state);
            SetUpStep(state);
        }

        MissionStory.Step = 0;
        SkipUnavailable();
        RefreshItems();

        ready = true;
    }

    // Bank, every mission: the customer who stands at counter 1 (the counter whose computer is inspected) is moved to the player's own window (counter 2), outside the
    // counter, so nobody stands in front of the computer the player has to look at. His stand spot is moved, and he follows it; he now looks at the player.
    private void MoveCustomerToPlayersWindow(MissionSession session)
    {
        if (!MissionData.IsBank(session.Environment))
            return;

        Transform customer = FindOutermost("mesh_office_m_black01");
        NpcWorker worker = customer != null ? customer.GetComponent<NpcWorker>() : null;
        if (worker == null || worker.Seat == null)
            return;

        // In front of the window of counter 2, on the customers' side (counter 1's customer stands 0.67 m outside the counter signs).
        GameObject sign = GameObject.Find("DoorSign_Counter 2");
        Vector3 spot = sign != null ? new Vector3(sign.transform.position.x + 0.67f, worker.Seat.position.y, sign.transform.position.z)
                                    : new Vector3(3.4f, worker.Seat.position.y, 0.77f);

        worker.Seat.position = spot;

        NpcHeadBehaviour head = customer.GetComponent<NpcHeadBehaviour>();
        if (head != null && player != null)
            head.LookAt(player, 1.5f);
    }

    private void OnObjectiveSound(int index)
    {
        GameSounds.Found();
    }

    public void OpenComputer()
    {
        if (scenario == null || EmailInvestigationUI.IsOpen)
            return;

        EmailInvestigationUI.Open(scenario);
    }

    // ---- The steps -------------------------------------------------------------------------------------------------

    private StepState CurrentState()
    {
        return MissionStory.Step >= 0 && MissionStory.Step < states.Count ? states[MissionStory.Step] : null;
    }

    private void Advance()
    {
        MissionStory.Step++;
        SkipUnavailable();
        RefreshItems();
    }

    // A step that cannot be done in this scene (a character or object that is missing) is skipped. What it would have given you is
    // given anyway, so the steps after it still work.
    private void SkipUnavailable()
    {
        while (MissionStory.Step < states.Count && !states[MissionStory.Step].ok)
        {
            MissionStep skipped = states[MissionStory.Step].def;
            if (skipped.kind == StepKind.Inspect)
                CompleteInspectObjectives();

            if ((skipped.kind == StepKind.Talk || skipped.kind == StepKind.PickUp) && !string.IsNullOrEmpty(skipped.item))
                MissionStory.Carrying = skipped.item;

            // The tablet could not be handed over (the character is missing): the player has it anyway.
            if (skipped.kind == StepKind.Tablet)
                TabletAccess.Receive();

            MissionStory.Step++;
        }
    }

    // An item lies on its desk only while it is the thing to pick up.
    private void RefreshItems()
    {
        StepState current = CurrentState();

        // The tablet is in the character's hand only while it is their turn to hand it over.
        foreach (KeyValuePair<StepState, HeldTablet> held in heldTablets)
        {
            if (held.Value != null)
                held.Value.Hold(held.Key == current);
        }

        foreach (StepState state in states)
        {
            if (state.item != null)
                state.item.SetActive(state == current);
        }
    }

    // ---- Each frame ----------------------------------------------------------------------------------------------

    private void Update()
    {
        if (!ready)
            return;

        MissionSession session = MissionSession.Instance;
        if (session == null || session.HasEnded)
        {
            MissionSubtitle.Hide();
            marker.Hide();
            return;
        }

        UpdateMeeting();
        UpdateMarker();
    }

    private void UpdateMeeting()
    {
        StepState state = CurrentState();
        meetingInside = false;

        if (state == null || state.def.kind != StepKind.Meeting || player == null)
            return;

        Bounds area = state.zone;
        area.Expand(new Vector3(3f, 4f, 3f));
        meetingInside = area.Contains(player.position);

        if (!meetingInside || MissionDialogue.IsOpen || EmailInvestigationUI.IsOpen || InspectionOverlay.IsOpen || InterviewOverlay.IsOpen)
        {
            MissionSubtitle.Hide();
            return;
        }

        // A briefing that asks the player to sit: it does not start (and does not go on) while they are standing; the plate in the upper left tells them to sit.
        if (state.def.requireSeat && !PlayerSeated)
        {
            MissionSubtitle.Hide();
            return;
        }

        // The briefing is a conversation, with no timer: its lines come one by one in the box at the bottom, each with NEXT (the last one with OK).
        // The player is held still while it is open. It starts by itself when they reach the place (and, where it asks for it, are sitting).
        MissionSubtitle.Hide();

        StepState briefing = state;
        MissionDialogue.Open(state.def.speaker, state.def.lines, () =>
        {
            // The briefing is over: whoever sat down for it stands up again, so they can walk to the next person.
            if (briefing.def.requireSeat && player != null)
            {
                PlayerSeatController seat = player.GetComponent<PlayerSeatController>();
                if (seat != null && seat.IsSeated)
                    seat.StandUp();
            }

            Advance();
        });
    }

    // The arrow over where to go next.
    private void UpdateMarker()
    {
        StepState state = CurrentState();

        if (state == null || MissionDialogue.IsOpen || EmailInvestigationUI.IsOpen || InspectionOverlay.IsOpen || InterviewOverlay.IsOpen)
        {
            marker.Hide();
            return;
        }

        switch (state.def.kind)
        {
            case StepKind.Meeting:
                if (meetingInside)
                    marker.Hide();
                else
                    marker.Track(state.zone.center + Vector3.up * 1.4f, state.def.label, state.facing, state.via);
                break;

            case StepKind.PickUp:
                if (state.item != null)
                    marker.Track(state.item.transform.position + Vector3.up * 0.5f, state.def.label);
                else
                    marker.Hide();
                break;

            case StepKind.Inspect:
            {
                // The arrow leads to the nearest place not checked yet (it does not say which ones show evidence).
                Vector3 from = player != null ? player.position : Vector3.zero;
                StationState nearestStation = null;
                float nearestDistance = float.MaxValue;
                foreach (StationState station in state.stations)
                {
                    float distance = Vector3.Distance(from, station.point);
                    if (!station.resolved && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestStation = station;
                    }
                }

                if (nearestStation != null)
                    marker.Track(nearestStation.point + Vector3.up * 0.5f, state.def.label, nearestStation.facing);
                else
                    marker.Hide();
                break;
            }

            case StepKind.Analyze:
                if (!EmailInvestigationUI.EverOpened && state.display != null)
                    marker.Track(state.point + Vector3.up * 0.35f, state.def.label, -state.display.transform.forward);
                else
                    marker.Hide();
                break;

            default:
            {
                Transform nearest = Nearest(state.npcs);
                if (nearest != null)
                    marker.Track(HeadPoint(nearest), state.def.label, TellerSide(nearest), TellerWay(nearest));
                else
                    marker.Hide();
                break;
            }
        }
    }

    private Transform Nearest(List<Transform> candidates)
    {
        Transform best = null;
        float bestDistance = float.MaxValue;
        Vector3 from = player != null ? player.position : Vector3.zero;

        foreach (Transform candidate in candidates)
        {
            float distance = Vector3.Distance(from, candidate.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    // ---- The hint at the top of the screen --------------------------------------------------------------------------

    // Is the player sitting on a chair (or sitting down)?
    private bool PlayerSeated
    {
        get
        {
            if (player == null)
                return false;

            PlayerSeatController seat = player.GetComponent<PlayerSeatController>();
            return seat != null && seat.IsSeated;
        }
    }

    // The objective plate is not shown while the player rides in the taxi or bus (only the driver talks); it comes back when they step out.
    public bool HintHidden => intro != null && intro.Riding;

    public void GetHint(out string heading, out string hint)
    {
        MissionSession session = MissionSession.Instance;
        StepState state = CurrentState();

        heading = "";
        hint = "";

        if (intro != null && intro.Active)
        {
            heading = intro.Heading;
            hint = intro.Hint;
            return;
        }

        if (state == null)
            return;

        if (state.def.side)
        {
            int total = 0, index = 0;
            for (int i = 0; i < states.Count; i++)
            {
                if (!states[i].ok || !states[i].def.side)
                    continue;

                total++;
                if (i <= MissionStory.Step)
                    index = total;
            }

            heading = "SIDE QUEST " + index + " / " + total + ":  " + state.def.heading;
            hint = state.def.kind == StepKind.Meeting && meetingInside
                ? (state.def.requireSeat && !PlayerSeated ? "Sit down on a chair: the " + state.def.speaker + " starts when you are seated." : "Stay and listen to the " + state.def.speaker + ".")
                : state.def.hint;
            return;
        }

        // Main objectives: the first one not done yet.
        int next = -1;
        for (int i = 0; i < 6; i++)
        {
            if (!session.IsObjectiveDone(i))
            {
                next = i;
                break;
            }
        }

        if (next == -1)
            return;

        string[] objectives = MissionData.Objectives(session.ThreatType);
        heading = "OBJECTIVE " + (next + 1) + " / 6:  " + objectives[next];

        if (state.def.kind == StepKind.Analyze && EmailInvestigationUI.EverOpened)
            hint = scenario.hints != null && next < scenario.hints.Length ? scenario.hints[next] : "";
        else
            hint = state.def.hint;

        if (state.def.kind == StepKind.Interview)
        {
            if (AllEvidenceFound())
            {
                hint = "Talk to the person again to decide what kind of threat this is, then choose the response.";
            }
            else
            {
                int asked = 0;
                foreach (EmailScenario.Question q in scenario.questions)
                {
                    if (q.objective >= 0 && session.IsObjectiveDone(q.objective))
                        asked++;
                }

                hint = state.def.hint + "  (" + asked + " / " + scenario.clueCount + " found)";
            }
        }

        if (state.def.kind == StepKind.Inspect)
        {
            int found = 0;
            int total = 0;
            foreach (EmailScenario.Station def in scenario.stations)
            {
                if (def.objective < 0)
                    continue;

                total++;
                if (session.IsObjectiveDone(def.objective))
                    found++;
            }

            hint = state.def.hint + "  (" + found + " / " + total + " found)";
        }
    }

    // ---- Setting things up ---------------------------------------------------------------------------------------------------

    private void SetUpStep(StepState state)
    {
        switch (state.def.kind)
        {
            case StepKind.Meeting: SetUpMeeting(state); break;
            case StepKind.PickUp: SetUpPickUp(state); break;
            case StepKind.Analyze: SetUpComputer(state); break;
            case StepKind.Inspect: SetUpInspect(state); break;
            default: SetUpNpcs(state); break;
        }
    }

    // The meeting room: the area around the table.
    private void SetUpMeeting(StepState state)
    {
        Transform table = FindOutermost(state.def.target);
        if (table == null)
            table = FindDeskNear(state.def.target, player != null ? player.position : Vector3.zero);   // by part of the name
        if (table == null || state.def.lines == null || state.def.lines.Length == 0)
        {
            Debug.LogWarning("[Mission] The meeting place '" + state.def.target + "' was not found, so the meeting step was skipped.");
            return;
        }

        Renderer[] renderers = table.GetComponentsInChildren<Renderer>();
        state.zone = renderers.Length > 0 ? renderers[0].bounds : new Bounds(table.position, new Vector3(3f, 1f, 1.5f));
        for (int i = 1; i < renderers.Length; i++)
            state.zone.Encapsulate(renderers[i].bounds);
        state.facing = TellerSide(table);
        state.via = TellerWay(table);

        state.ok = true;
    }

    // The characters of a Talk / Deliver / Report step.
    private void SetUpNpcs(StepState state)
    {
        foreach (string name in state.def.target.Split(','))
        {
            Transform npc = FindOutermost(name.Trim());
            if (npc == null)
                continue;

            state.npcs.Add(npc);
            EnsureNpcAction(npc);

            if (state.def.kind == StepKind.Tablet && !heldTablets.ContainsKey(state))
                heldTablets[state] = HeldTablet.Ensure(npc);
        }

        state.ok = state.npcs.Count > 0;
        if (!state.ok)
            Debug.LogWarning("[Mission] No character named '" + state.def.target + "' was found, so the step '" + state.def.kind + "' was skipped.");
    }

    // The computer of an Analyze step: the player's own, or a terminal of the IT room.
    private void SetUpComputer(StepState state)
    {
        ComputerScreenDisplay display = state.def.computer == "terminal" ? FindTerminal(state.def.target) : GetMyComputer();

        // The investigation can only be done at a computer: the step stays, and the Console says what is missing.
        state.ok = true;

        if (display == null)
        {
            Debug.LogWarning("[Mission] No computer was found for '" + state.def.label + "', so the investigation cannot be opened.");
            return;
        }

        state.display = display;

        RectTransform canvas = (RectTransform)display.transform;
        state.point = canvas.TransformPoint(canvas.rect.center);

        if (!computersWithAction.Add(display))
            return;

        GameObject hit = new GameObject("MissionComputerHit");
        hit.transform.SetParent(canvas, false);

        BoxCollider box = hit.AddComponent<BoxCollider>();
        float depth = 0.3f / Mathf.Max(0.00001f, Mathf.Abs(canvas.lossyScale.z));
        box.center = new Vector3(canvas.rect.center.x, canvas.rect.center.y, 0f);
        box.size = new Vector3(canvas.rect.width, canvas.rect.height, depth);
        state.point = box.bounds.center;

        ConfigureComputer(hit.AddComponent<MissionAction>(), display);

        // The computer model itself answers too when it is a small object of its own (so any part of it can be tapped).
        Transform device = canvas.parent;
        if (device != null && device.GetComponent<Interactable>() == null && SizeOf(device) < 1.6f)
            ConfigureComputer(device.gameObject.AddComponent<MissionAction>(), display);

        Debug.Log("[Mission] Computer for '" + state.def.label + "': '" + (device != null ? device.name : canvas.name) + "'.");
    }

    private void ConfigureComputer(MissionAction action, ComputerScreenDisplay display)
    {
        action.PriorityValue = 1;
        action.DisableHighlight();
        action.AllowWhileSeated = true;

        action.Prompt = () =>
        {
            StepState state = CurrentState();
            return state != null && state.def.kind == StepKind.Analyze ? state.def.prompt : "Computer";
        };
        action.Available = () =>
        {
            StepState state = CurrentState();
            return state != null && state.def.kind == StepKind.Analyze && state.display == display;
        };
        action.Action = OpenComputer;
    }

    // "My computer": the one marked with MyComputerMarker, else the one named like "PC (5)", else the nearest to where the player starts.
    private ComputerScreenDisplay GetMyComputer()
    {
        if (myComputerSearched)
            return myComputer;

        myComputerSearched = true;

        // The marked computer wins, whatever its screen shows (the bank counters show bank files, not the employee sign-in).
        ComputerScreenDisplay[] all = FindObjectsByType<ComputerScreenDisplay>(FindObjectsInactive.Exclude);
        foreach (ComputerScreenDisplay display in all)
        {
            if (display.GetComponentInParent<MyComputerMarker>() != null)
            {
                Debug.Log("[Mission] Your computer is marked as yours.");
                return myComputer = display;
            }
        }

        // Otherwise the employee computers: the office sign-in screens, and the bank counters' bank-file screens.
        List<ComputerScreenDisplay> cubicle = new List<ComputerScreenDisplay>();
        foreach (ComputerScreenDisplay display in all)
        {
            if (display.CurrentDesign == ComputerScreenDisplay.Design.Login || display.CurrentDesign == ComputerScreenDisplay.Design.BankFiles)
                cubicle.Add(display);
        }

        foreach (ComputerScreenDisplay display in cubicle)
        {
            Transform device = display.transform.parent != null ? display.transform.parent : display.transform;
            string plain = device.name.ToLowerInvariant().Replace(" ", "").Replace("(", "").Replace(")", "").Replace("_", "");
            if (plain.EndsWith("pc5"))
            {
                Debug.Log("[Mission] Your computer is '" + device.name + "' (named like PC 5).");
                return myComputer = display;
            }
        }

        ComputerScreenDisplay best = null;
        float bestDistance = float.MaxValue;
        Vector3 from = player != null ? player.position : Vector3.zero;

        foreach (ComputerScreenDisplay display in cubicle)
        {
            float distance = Vector3.Distance(from, display.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = display;
            }
        }

        if (best != null)
            Debug.Log("[Mission] Your computer is the nearest to where you start (" + bestDistance.ToString("0.0") +
                      " m). If it is the wrong one, select the right computer and use Tools > Firewall Duty > Mission > Mark Selected Computer As Mine.");

        return myComputer = best;
    }

    // A computer of the IT room (the "Code" screens): the one nearest to the named character.
    private ComputerScreenDisplay FindTerminal(string nearCharacter)
    {
        Transform near = string.IsNullOrEmpty(nearCharacter) ? null : FindOutermost(nearCharacter);
        ComputerScreenDisplay best = null;
        float bestDistance = float.MaxValue;

        foreach (ComputerScreenDisplay display in FindObjectsByType<ComputerScreenDisplay>(FindObjectsInactive.Exclude))
        {
            if (display.CurrentDesign != ComputerScreenDisplay.Design.Code)
                continue;

            float distance = near != null ? Vector3.Distance(near.position, display.transform.position) : 0f;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = display;
            }
        }

        return best;
    }

    // ---- Interviewing (an Interview step) ---------------------------------------------------------------------------------------

    private bool AllEvidenceFound()
    {
        MissionSession session = MissionSession.Instance;
        if (session == null || scenario == null)
            return false;

        int total = 0;
        foreach (EmailScenario.Question q in scenario.questions)
        {
            if (q.objective < 0)
                continue;

            total++;
            if (!session.IsObjectiveDone(q.objective))
                return false;
        }

        return total > 0;
    }

    private void OpenInterview(StepState state, Transform npc)
    {
        if (state.asked == null || state.asked.Length != scenario.questions.Count)
            state.asked = new bool[scenario.questions.Count];

        InterviewOverlay.Open(state.def.speaker, scenario.interviewOpening, scenario.questions.ToArray(),
                              index => state.asked[index], index => Ask(state, index));
    }

    // One question: it either records evidence or costs time. The last piece of evidence opens the decision by itself.
    private InterviewOutcome Ask(StepState state, int index)
    {
        MissionSession session = MissionSession.Instance;
        EmailScenario.Question q = scenario.questions[index];
        InterviewOutcome outcome = new InterviewOutcome { answer = q.answer };

        if (state.asked[index])
        {
            outcome.note = "You already asked that.";
            return outcome;
        }

        state.asked[index] = true;

        if (q.objective >= 0)
        {
            session.CompleteObjective(q.objective);
            outcome.note = "Evidence recorded: " + q.callout + ".";
            outcome.good = true;

            if (AllEvidenceFound())
            {
                session.CompleteObjective(scenario.summaryObjective);
                outcome.note += " That is all the evidence. Now decide what kind of threat this is.";
                outcome.finished = true;
                StartCoroutine(OpenDecisionSoon(state));
            }
        }
        else
        {
            session.AddTimePenalty(scenario.wrongTapPenalty);
            outcome.note = (string.IsNullOrEmpty(q.note) ? "That tells you nothing about the three things on your tablet." : q.note) +
                           "  (-" + (int)scenario.wrongTapPenalty + " seconds)";
            outcome.error = true;
        }

        return outcome;
    }

    // After the last answer has been read, the decision opens by itself.
    private IEnumerator OpenDecisionSoon(StepState state)
    {
        yield return new WaitForSecondsRealtime(2.6f);

        if (CurrentState() == state && !EmailInvestigationUI.IsOpen && !InterviewOverlay.IsOpen)
            OpenComputer();
    }

    // ---- Inspecting (an Inspect step) -------------------------------------------------------------------------------------------

    // Finds the object of a station: "pc@npc", "thing@Name@npc" or "name@Name" (see EmailScenario.Station).
    private Transform FindStationObject(string locate)
    {
        if (string.IsNullOrEmpty(locate))
            return null;

        string[] parts = locate.Split('@');

        if (parts[0] == "pc" && parts.Length >= 2)
        {
            Transform npc = FindOutermost(parts[1]);
            if (npc == null)
                return null;

            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (ComputerScreenDisplay display in FindObjectsByType<ComputerScreenDisplay>(FindObjectsInactive.Exclude))
            {
                ComputerScreenDisplay.Design design = display.CurrentDesign;
                if (design != ComputerScreenDisplay.Design.Login && design != ComputerScreenDisplay.Design.BankFiles && design != ComputerScreenDisplay.Design.Files
                    && design != ComputerScreenDisplay.Design.NameList)
                    continue;

                Transform device = display.transform.parent != null ? display.transform.parent : display.transform;
                float distance = FlatDistance(npc.position, device.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = device;
                }
            }

            return bestDistance <= 3f ? best : null;
        }

        if (parts[0] == "thing" && parts.Length >= 3)
        {
            Transform npc = FindOutermost(parts[2]);
            if (npc == null)
                return null;

            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!t.name.StartsWith(parts[1]) || t.GetComponentInChildren<Renderer>() == null || (t.parent != null && t.parent.name.StartsWith(parts[1])))
                    continue;

                float distance = FlatDistance(npc.position, t.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = t;
                }
            }

            return bestDistance <= 4f ? best : null;
        }

        if (parts[0] == "name" && parts.Length >= 2)
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (t.name.StartsWith(parts[1]) && t.GetComponentInChildren<Renderer>() != null && (t.parent == null || !t.parent.name.StartsWith(parts[1])))
                    return t;
            }
        }

        return null;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    // The closed-off places of the bank, which the floor trail must go into and out of by their doors (see FloorGuide.AddPocket). Measured from the wall layout of
    // Bank_scene: the wall with the doors runs along z = 7.3. North of it is the back office (open to the lobby between x 3.3 and 8), and from there the teller area
    // (x up to the counters at 2.6, door at x -2.2) and the manager's room (x 8.2 to 16.6, up to the north wall at 14.6, door at x 11.35) are entered.
    private void SetUpTellerArea()
    {
        if (marker == null)
            return;

        GameObject tellerSign = GameObject.Find("DoorSign_Teller Area");
        if (tellerSign != null)
        {
            Vector3 sign = tellerSign.transform.position;
            float doorX = sign.x - 0.25f;
            float doorZ = sign.z - 0.33f;   // the sign hangs just north of the wall

            Bounds region = new Bounds();
            region.SetMinMax(new Vector3(-3.2f, -5f, -9f), new Vector3(2.8f, 10f, doorZ - 0.15f));
            marker.AddPocket(region, new Vector3(doorX, 0f, doorZ + 1.3f), new Vector3(doorX, 0f, doorZ - 1.3f), true);
        }

        GameObject managerSign = GameObject.Find("DoorSign_Bank Manager");
        if (managerSign != null)
        {
            Vector3 sign = managerSign.transform.position;
            float doorZ = sign.z + 0.23f;   // the sign hangs just south of the wall

            Bounds region = new Bounds();
            region.SetMinMax(new Vector3(8.35f, -5f, doorZ + 0.15f), new Vector3(16.45f, 10f, 14.4f));
            marker.AddPocket(region, new Vector3(sign.x, 0f, doorZ - 1.3f), new Vector3(sign.x, 0f, doorZ + 1.3f), false);
        }
    }

    // The way in to a teller: the door of the teller area (the "Teller Area" sign hangs on it, on the back-room side) seen from outside and from inside,
    // and the place behind the teller to end. Null when there is no such door or the person is not a teller.
    private Vector3[] TellerWay(Transform npc)
    {
        if (npc == null)
            return null;

        if (tellerWays.TryGetValue(npc, out Vector3[] known))
            return known;

        Vector3[] way = null;
        Vector3 side = TellerSide(npc);
        GameObject sign = GameObject.Find("DoorSign_Teller Area");

        if (sign != null && side.sqrMagnitude > 0.01f)
        {
            Vector3 door = sign.transform.position;
            way = new[]
            {
                new Vector3(door.x, 0f, door.z + 1.3f),
                new Vector3(door.x, 0f, door.z - 1.3f),
                npc.position + side * 1.1f
            };
        }

        tellerWays[npc] = way;
        return way;
    }

    // A teller sits at a counter with the customers in front of her and the teller area behind. The floor trail to her ends inside the teller area, so the
    // player is led in through its door and not along the customers' side. Zero for anyone who does not sit at a bank counter.
    private Vector3 TellerSide(Transform npc)
    {
        if (npc == null)
            return Vector3.zero;

        if (tellerSides.TryGetValue(npc, out Vector3 known))
            return known;

        Vector3 side = Vector3.zero;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (t.name != "Bank_TellerChair")
                continue;

            Vector3 offset = t.position - npc.position;
            offset.y = 0f;
            if (offset.magnitude < 1.6f)
            {
                Vector3 behind = -npc.forward;
                behind.y = 0f;
                side = behind.sqrMagnitude > 0.01f ? behind.normalized : Vector3.zero;
                break;
            }
        }

        tellerSides[npc] = side;
        return side;
    }

    // The side a place is used from (flat): toward the person who works there (the teller at the counter, the manager at the desk), else the way its screen
    // faces (the setup tool turned each screen toward its chair). Zero when it has no side (an ATM): the trail may end anywhere in reach.
    private Vector3 StationFacing(Transform thing, string locate)
    {
        string[] parts = locate.Split('@');
        string owner = parts[0] == "pc" && parts.Length >= 2 ? parts[1] : parts[0] == "thing" && parts.Length >= 3 ? parts[2] : null;
        Transform npc = owner != null ? FindOutermost(owner) : null;

        if (npc != null)
        {
            Vector3 toOwner = npc.position - thing.position;
            toOwner.y = 0f;
            if (toOwner.magnitude > 0.3f)
                return toOwner.normalized;
        }

        ComputerScreenDisplay display = thing.GetComponentInChildren<ComputerScreenDisplay>();
        if (display != null)
        {
            Vector3 forward = -display.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
                return forward.normalized;
        }

        return Vector3.zero;
    }

    // The places to inspect: each gets an invisible box the crosshair can hit, and the hand button opens its inspection window.
    // The step can only be done when every place that shows evidence was found in the scene; otherwise it is skipped (and the evidence is
    // given anyway, see SkipUnavailable).
    private void SetUpInspect(StepState state)
    {
        int evidenceTotal = 0;
        int evidenceFound = 0;

        foreach (EmailScenario.Station def in scenario.stations)
        {
            if (def.objective >= 0)
                evidenceTotal++;

            Transform thing = FindStationObject(def.locate);
            if (thing == null)
            {
                Debug.LogWarning("[Mission] Nothing was found for the place to inspect '" + def.locate + "' (" + def.title + ").");
                continue;
            }

            StationState station = new StationState { def = def, thing = thing };

            Bounds bounds = new Bounds(thing.position, new Vector3(0.6f, 0.6f, 0.6f));
            Renderer[] renderers = thing.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            station.point = bounds.center;
            station.facing = StationFacing(thing, def.locate);

            GameObject hit = new GameObject("MissionInspectHit");
            hit.transform.SetParent(thing, false);

            Vector3 scale = thing.lossyScale;
            BoxCollider box = hit.AddComponent<BoxCollider>();
            box.center = thing.InverseTransformPoint(bounds.center);
            box.size = new Vector3(bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                                   bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                                   bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

            MissionAction action = hit.AddComponent<MissionAction>();
            action.DisableHighlight();
            action.PriorityValue = 1;
            action.Prompt = () => string.IsNullOrEmpty(def.prompt) ? "Inspect" : def.prompt;
            action.Available = () => CurrentState() == state;
            action.Action = () => OpenStation(state, station);

            state.stations.Add(station);
            if (def.objective >= 0)
                evidenceFound++;

            Debug.Log("[Mission] Place to inspect: '" + thing.name + "' (" + (def.objective >= 0 ? "shows evidence: " + def.callout : "normal") + ").");
        }

        state.ok = evidenceTotal > 0 && evidenceFound >= evidenceTotal;
        if (!state.ok)
            Debug.LogWarning("[Mission] Only " + evidenceFound + " of the " + evidenceTotal + " places that show the evidence were found, so the inspection step was skipped.");
    }

    private void OpenStation(StepState state, StationState station)
    {
        InspectionOverlay.Open(station.def, station.resolved, flagged => Decide(state, station, flagged));
    }

    // The player's decision about a place: flag it as evidence, or say it looks normal. Right decisions are kept, wrong ones cost time.
    private InspectionOutcome Decide(StepState state, StationState station, bool flagged)
    {
        MissionSession session = MissionSession.Instance;
        InspectionOutcome outcome = new InspectionOutcome();
        bool isEvidence = station.def.objective >= 0;

        if (isEvidence == flagged)
        {
            station.resolved = true;

            if (isEvidence)
            {
                session.CompleteObjective(station.def.objective);

                bool all = true;
                foreach (EmailScenario.Station def in scenario.stations)
                {
                    if (def.objective >= 0)
                        all &= session.IsObjectiveDone(def.objective);
                }

                outcome.message = "Evidence recorded: " + station.def.callout + ".";
                if (all)
                {
                    session.CompleteObjective(scenario.summaryObjective);
                    outcome.message += " That is all the evidence. Go to your computer and read the incident report.";
                    Advance();
                }
            }
            else
            {
                outcome.message = "Right: nothing wrong with this one.";
            }

            outcome.close = true;
            return outcome;
        }

        session.AddTimePenalty(scenario.wrongTapPenalty);
        outcome.message = (string.IsNullOrEmpty(station.def.hint) ? (flagged ? "Nothing wrong here." : "Look again.") : station.def.hint) +
                          "  (-" + (int)scenario.wrongTapPenalty + " seconds)";
        outcome.close = false;
        return outcome;
    }

    // An Inspect step that cannot be done in this scene gives its evidence anyway, so the mission can still be finished.
    private void CompleteInspectObjectives()
    {
        MissionSession session = MissionSession.Instance;
        if (session == null || scenario == null)
            return;

        foreach (EmailScenario.Station def in scenario.stations)
        {
            if (def.objective >= 0)
                session.CompleteObjective(def.objective);
        }

        session.CompleteObjective(scenario.summaryObjective);
    }

    // A sheet of paper on a desk. Where: the desk named by the step, nearest to the chair of the player's computer.
    private void SetUpPickUp(StepState state)
    {
        ComputerScreenDisplay mine = GetMyComputer();

        Vector3 anchor = mine != null ? mine.transform.position : (player != null ? player.position : Vector3.zero);
        Vector3 toward = player != null ? player.forward : Vector3.forward;

        // The side of the screen the player sits on: toward the nearest chair (within 3 m).
        Vector3 chairPoint = anchor;
        float nearestSeat = 3f;
        foreach (SeatInteractable seat in FindObjectsByType<SeatInteractable>(FindObjectsInactive.Exclude))
        {
            float distance = Vector3.Distance(seat.transform.position, anchor);
            if (distance < nearestSeat)
            {
                nearestSeat = distance;
                chairPoint = seat.transform.position;
                toward = seat.transform.position - anchor;
            }
        }

        toward.y = 0f;
        if (toward.sqrMagnitude < 0.01f)
            toward = Vector3.forward;
        toward.Normalize();

        Vector3 point;
        Transform desk = FindDeskNear(state.def.target, chairPoint);
        if (desk != null)
        {
            point = PointOnDesk(desk, chairPoint);
            Debug.Log("[Mission] The '" + state.def.item + "' is on '" + desk.name + "'.");
        }
        else
        {
            Debug.LogWarning("[Mission] No '" + state.def.target + "' was found, so the item is on the floor in front of the player.");
            Vector3 start = anchor + toward * 0.6f + Vector3.up * 1.5f;
            point = start + Vector3.down * 1.5f;
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                point = hit.point + Vector3.up * 0.004f;
        }

        GameObject paper = new GameObject("Mission_" + state.def.item);
        paper.transform.position = point;
        paper.transform.rotation = Quaternion.LookRotation(toward, Vector3.up);

        GameObject sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sheet.name = "Sheet";
        Destroy(sheet.GetComponent<Collider>());
        sheet.transform.SetParent(paper.transform, false);
        sheet.transform.localScale = new Vector3(0.21f, 0.004f, 0.297f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            Material material = new Material(shader);
            material.color = new Color(0.97f, 0.97f, 0.95f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(0.97f, 0.97f, 0.95f));
            sheet.GetComponent<Renderer>().material = material;
        }

        // A bigger invisible box, so it is easy to aim at.
        BoxCollider box = paper.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.08f, 0f);
        box.size = new Vector3(0.5f, 0.2f, 0.55f);

        MissionAction action = paper.AddComponent<MissionAction>();
        action.DisableHighlight();
        action.Prompt = () => state.def.prompt;
        action.Available = () => CurrentState() == state;
        action.Action = () =>
        {
            MissionStory.Carrying = state.def.item;
            Destroy(state.item);
            state.item = null;
            Advance();
        };

        state.item = paper;
        state.ok = true;
        paper.SetActive(false);   // it appears when it is its turn
    }

    // An invisible box around a character that the crosshair can hit, and the action on the character.
    private MissionAction EnsureNpcAction(Transform character)
    {
        if (npcActions.TryGetValue(character, out MissionAction existing))
            return existing;

        Bounds bounds = new Bounds(character.position + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f));
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
        }

        GameObject hit = new GameObject("MissionTalkHit");
        hit.transform.SetParent(character, false);

        Vector3 scale = character.lossyScale;
        BoxCollider box = hit.AddComponent<BoxCollider>();
        box.center = character.InverseTransformPoint(bounds.center);
        box.size = new Vector3(bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                               bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                               bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

        // On the character itself (not on the box), so aiming at any part of the character works.
        MissionAction action = character.GetComponent<MissionAction>();
        if (action == null)
            action = character.gameObject.AddComponent<MissionAction>();

        // A character glows (an outline round the body) while the crosshair is on them and it is their turn: the person to time in with, or the one the
        // current step is about. Anyone else can still be talked to, but does not glow.
        action.HighlightWhen = () => NeedsInteraction(character);
        action.Prompt = () => NpcPrompt(character);
        action.Action = () => NpcInteract(character);

        npcActions[character] = action;
        return action;
    }

    // ---- Talking -----------------------------------------------------------------------------------------------------------------

    // The step this character is the target of right now (null when it is not their turn).
    private StepState StepFor(Transform npc)
    {
        StepState state = CurrentState();
        if (state == null)
            return null;

        bool talkKind = state.def.kind == StepKind.Talk || state.def.kind == StepKind.Deliver || state.def.kind == StepKind.Report || state.def.kind == StepKind.Interview || state.def.kind == StepKind.Tablet;
        return talkKind && state.npcs.Contains(npc) ? state : null;
    }

    private bool NeedsInteraction(Transform npc)
    {
        if (intro != null && intro.AwaitingTimeIn && npc == timeInReceptionist)
            return true;

        StepState state = StepFor(npc);
        if (state == null)
            return false;

        // Someone to hand something to only needs you once you are carrying it.
        if (state.def.kind == StepKind.Deliver && MissionStory.Carrying != state.def.item)
            return false;

        return true;
    }

    private string NpcPrompt(Transform npc)
    {
        if (intro != null && intro.AwaitingTimeIn && npc == timeInReceptionist)
            return "Time In";

        StepState state = StepFor(npc);
        if (state == null)
            return "Talk";

        if (state.def.kind == StepKind.Deliver && MissionStory.Carrying != state.def.item)
            return "Talk";

        if (state.def.kind == StepKind.Interview && AllEvidenceFound())
            return "Decide";

        return string.IsNullOrEmpty(state.def.prompt) ? "Talk" : state.def.prompt;
    }

    // What the person you time in with says: the receptionist (office) or the first teller (bank), by the way you arrived (see MissionIntro.Variant).
    private static string[] TimeInLines(out string speaker)
    {
        MissionSession session = MissionSession.Instance;
        int scene = session != null ? session.SceneNumber : 1;
        bool bank = session != null && MissionData.IsBank(session.Environment);
        int variant = MissionIntro.Variant(scene);
        bool first = scene == 1;

        speaker = bank ? "Teller" : "Receptionist";
        string place = bank ? "the bank" : "Firewall Duty";

        string opening = variant == 1 ? "Good morning! What a rainy day. Dry off a little, you are all wet."
                       : variant == 2 ? "Good morning! Brr, it is snowing hard out there. Warm up a little."
                       : first ? "Good morning! You must be the new hire. Welcome to " + place + "."
                       : "Good morning! Welcome back to " + place + ".";

        string timeIn = variant == 1 ? "Please time in. I will write it down: 8:00 AM. Welcome back."
                      : variant == 2 ? "Please time in. I will write it down: 8:00 AM. You made it through the snow."
                      : first ? "Please time in. I will write it down: 8:00 AM, first day. There you go."
                      : "Please time in. I will write it down: 8:00 AM. There you go.";

        string closing = first ? "Your day starts now. Do your tasks in order, keep your eyes open and stay careful. Good luck!"
                                : "Your day starts now. Stay sharp out there. Good luck!";

        return new[] { opening, timeIn, closing };
    }

    private void NpcInteract(Transform npc)
    {
        if (intro != null && intro.AwaitingTimeIn && npc == timeInReceptionist)
        {
            string speaker;
            string[] timeInLines = TimeInLines(out speaker);
            MissionDialogue.Open(speaker, timeInLines, intro.FinishTimeIn);

            return;
        }

        StepState state = StepFor(npc);

        if (state == null)
        {
            MissionDialogue.Open(DisplayName(npc), new[] { scenario.IdleFor(npc.name) }, null);
            return;
        }

        MissionStep step = state.def;

        switch (step.kind)
        {
            case StepKind.Talk:
                MissionDialogue.Open(step.speaker, step.lines, () =>
                {
                    if (!string.IsNullOrEmpty(step.item))
                        MissionStory.Carrying = step.item;

                    Advance();
                });
                break;

            case StepKind.Deliver:
                if (MissionStory.Carrying == step.item)
                {
                    MissionDialogue.Open(step.speaker, step.lines, () =>
                    {
                        MissionStory.Carrying = "";
                        Advance();
                    });
                }
                else
                {
                    MissionDialogue.Open(step.speaker, new[] { "Do you have the " + step.item.ToLowerInvariant() + "? Bring it to me." }, null);
                }

                break;

            case StepKind.Report:
                MissionDialogue.Open(step.speaker, step.lines, FinishMission);
                break;

            case StepKind.Tablet:
                // The character hands over the tablet: it leaves their hand and the TABLET button comes alive.
                MissionDialogue.Open(step.speaker, step.lines, () =>
                {
                    TabletAccess.Receive();
                    Advance();
                });
                break;

            case StepKind.Interview:
                if (AllEvidenceFound())
                    OpenComputer();   // the evidence is in: the decision (what kind of threat, which response)
                else
                    OpenInterview(state, npc);
                break;
        }
    }

    // What a character is called in a conversation they have no step for: the speaker name of a step that mentions them.
    private string DisplayName(Transform npc)
    {
        foreach (StepState state in states)
        {
            if (state.npcs.Contains(npc) && !string.IsNullOrEmpty(state.def.speaker))
                return state.def.speaker;
        }

        return "Colleague";
    }

    // Called when the right response was chosen at the computer.
    public void OnResponseChosen()
    {
        MissionStory.ResponseChosen = true;
        Advance();

        // Nobody to report to in this scene: the mission is complete now.
        if (CurrentState() == null)
            FinishMission();
    }

    // The report is made: the last objective is done and the mission is over.
    private void FinishMission()
    {
        MissionSession session = MissionSession.Instance;
        if (session == null)
            return;

        session.CompleteObjective(5);
        StartCoroutine(EndSoon(2.5f));
    }

    private IEnumerator EndSoon(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        if (MissionSession.Instance != null)
            MissionSession.Instance.EndMission();
    }

    // ---- Finding things ---------------------------------------------------------------------------------------------------------------

    // The desk (a model named like "desk_01") nearest to a point.
    private static Transform FindDeskNear(string deskName, Vector3 point)
    {
        string wanted = (deskName ?? "").ToLowerInvariant();
        Transform best = null;
        float bestDistance = float.MaxValue;

        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!candidate.name.ToLowerInvariant().Contains(wanted))
                continue;

            Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                continue;

            // The outermost one of a model and its mesh child that share the name.
            if (candidate.parent != null && candidate.parent.name.ToLowerInvariant().Contains(wanted))
                continue;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float distance = Vector3.Distance(bounds.ClosestPoint(point), point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    // A spot on top of the desk, on the side of the chair and a little in from the edge, resting on the surface.
    private static Vector3 PointOnDesk(Transform desk, Vector3 chairPoint)
    {
        Renderer[] renderers = desk.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 edge = bounds.ClosestPoint(new Vector3(chairPoint.x, bounds.center.y, chairPoint.z));
        Vector3 inward = bounds.center - edge;
        inward.y = 0f;
        Vector3 spot = inward.sqrMagnitude > 0.01f ? edge + inward.normalized * 0.4f : edge;

        spot.x = Mathf.Clamp(spot.x, bounds.min.x + 0.2f, bounds.max.x - 0.2f);
        spot.z = Mathf.Clamp(spot.z, bounds.min.z + 0.2f, bounds.max.z - 0.2f);

        // Rest on the desk's own surface: the highest part of the desk under that spot, else the top of its bounds.
        float top = bounds.max.y;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(spot.x, bounds.max.y + 0.5f, spot.z), Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore);
        float bestHit = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(desk) && hit.distance < bestHit)
            {
                bestHit = hit.distance;
                top = hit.point.y;
            }
        }

        return new Vector3(spot.x, top + 0.004f, spot.z);
    }

    // Several objects can share a name (a model has a child mesh named like the model); the outermost is the model.
    private Transform FindOutermost(string objectName)
    {
        Transform best = null;
        int bestDepth = int.MaxValue;

        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (candidate.name != objectName)
                continue;

            // The player uses the same model as one of the IT experts: never pick the player's own body.
            if (player != null && candidate.IsChildOf(player))
                continue;

            int depth = 0;
            for (Transform parent = candidate.parent; parent != null; parent = parent.parent)
                depth++;

            if (depth < bestDepth)
            {
                best = candidate;
                bestDepth = depth;
            }
        }

        return best;
    }

    // The top of a character's head: the head bone when it has one, else a little above its body.
    private static Vector3 HeadPoint(Transform character)
    {
        if (character == null)
            return Vector3.zero;

        Animator animator = character.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
                return head.position + Vector3.up * 0.45f;
        }

        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return character.position + Vector3.up * 2f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return new Vector3(bounds.center.x, bounds.max.y + 0.35f, bounds.center.z);
    }

    private static float SizeOf(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return float.MaxValue;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
    }
}

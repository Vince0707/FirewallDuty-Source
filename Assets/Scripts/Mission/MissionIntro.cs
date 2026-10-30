using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The arrival at work, at the start of every mission (the office AND the bank). The player rides to work, the vehicle stops in front of the building,
// the door opens and the player gets out by themselves. Then they walk in through the main entrance and TIME IN (at the receptionist in the office,
// at the first teller in the bank): the timer, the side quests and the objectives only begin after that.
//
// How they arrive repeats every three missions:
//     missions 1, 4, 7, 10   a taxi, on a clear day            (mission 1 is the first day)
//     missions 2, 5, 8       a bus, in the rain, with passengers
//     missions 3, 6, 9       a taxi, in the snow
//
// The street, the vehicles and the markers are made by Tools > Firewall Duty > Office (or Bank) > Build Entrance Street (a group called
// "Outside_Ground" in the scene: the taxi "Intro_Taxi" and the bus "Intro_Bus", and the points under "Intro_Path"). Without that group the arrival
// is skipped and the mission starts as before.
public class MissionIntro : MonoBehaviour
{
    public const string TaxiName = "Intro_Taxi";
    public const string BusName = "Intro_Bus";
    public const string PathName = "Intro_Path";

    private const float RideSeconds = 10f;
    private const float DefaultEyeAboveFeet = 0.82f;   // metres, the eyes of a player seated in the taxi, above the floor of the vehicle
    private const string GroupName = "Outside_Ground";

    // Every mission of the office and of the bank has an arrival.
    public static bool WillRun(string environment, int sceneNumber)
    {
        return sceneNumber >= 1 && sceneNumber <= 10;
    }

    // 0 = a taxi on a clear day, 1 = the bus in the rain, 2 = a taxi in the snow. Repeats every three missions.
    public static int Variant(int sceneNumber)
    {
        return Mathf.Abs(sceneNumber - 1) % 3;
    }

    // The group made by the setup tool. The vehicles are switched off in the scene (so the street is empty when nothing arrives), which is why they
    // are looked for inside the group and not with GameObject.Find.
    public static bool RigExists(int sceneNumber)
    {
        bool bus = Variant(sceneNumber) == 1;
        string[] points = bus ? new[] { "Bus_Start", "Bus_Stop", "Bus_Away", "PlayerDrop" } : new[] { "Start", "Stop", "Away", "PlayerDrop" };

        if (FindByName(bus ? BusName : TaxiName) == null || FindByName(PathName) == null)
            return false;

        foreach (string point in points)
        {
            if (FindByName(point) == null)
                return false;
        }

        return true;
    }

    public bool Active { get; private set; }            // the arrival (the ride and the time-in) is not over yet
    public bool Riding { get; private set; }            // in the taxi or bus (until the player steps out): the objective plate is hidden then
    public bool AwaitingTimeIn { get; private set; }    // the player has to time in
    public string Heading { get; private set; } = "";   // the hint at the top of the screen while it is going on
    public string Hint { get; private set; } = "";

    // Called by the controller when the time-in conversation is over.
    public void FinishTimeIn()
    {
        AwaitingTimeIn = false;
    }

    // What the weather was before the rain or snow, to put back.
    private bool fogWas;
    private Color fogColorWas;
    private FogMode fogModeWas;
    private float fogDensityWas;
    private Light sun;
    private float sunIntensityWas;
    private Color sunColorWas;
    private LightShadows sunShadowsWas;
    private Material skyboxWas;
    private Material overcastSky;
    private float ambientIntensityWas;
    private bool weatherChanged;
    private RainEffect rain;
    private GameObject snowDecor;   // the snow on the roofs, the heaps, the snowman (switched off in the scene)

    public IEnumerator Play(Transform player, Transform receptionist, MissionMarker marker, bool bank)
    {
        Active = true;
        Riding = true;

        MissionSession session = MissionSession.Instance;
        int scene = session != null ? session.SceneNumber : 1;
        int variant = Variant(scene);
        bool bus = variant == 1;
        bool snow = variant == 2;
        bool first = scene == 1;
        string driver = bus ? "Bus driver" : "Taxi driver";
        string place = bank ? "the bank" : "the office";
        string greeter = bank ? "the first teller (Counter 1)" : "the receptionist";

        Transform vehicle = FindByName(bus ? BusName : TaxiName);
        Transform path = FindByName(PathName);
        Transform seat = vehicle != null ? vehicle.Find("Seat") : null;
        Transform door = vehicle != null ? vehicle.Find("DoorRight") : null;
        Transform start = FindByName(bus ? "Bus_Start" : "Start");
        Transform stop = FindByName(bus ? "Bus_Stop" : "Stop");
        Transform away = FindByName(bus ? "Bus_Away" : "Away");
        Transform drop = FindByName("PlayerDrop");

        if (vehicle == null || path == null || seat == null || start == null || stop == null || away == null || drop == null)
        {
            Debug.LogWarning("[Mission] The " + (bus ? "bus" : "taxi") + " or its path is missing, so the arrival is skipped. Run Tools > Firewall Duty > " +
                             (bank ? "Bank" : "Office") + " > Rebuild Entrance Street.");
            Active = false;
            Riding = false;
            yield break;
        }

        float doorAngle = bus ? -85f : -70f;
        Transform eye = seat.Find("Eye");                 // where the seated player's eyes are, above their feet (the bus seats are higher)

        // The way out, when it is not a straight line (the bus): Exit_1, Exit_2, ... in order (a taxi has none).
        System.Collections.Generic.List<Transform> wayOut = new System.Collections.Generic.List<Transform>();
        for (int i = 1; i < 10; i++)
        {
            Transform point = vehicle.Find("Exit_" + i);
            if (point == null)
                break;

            wayOut.Add(point);
        }

        PlayerMobileController controller = player.GetComponent<PlayerMobileController>();
        CharacterController body = player.GetComponent<CharacterController>();
        Camera camera = player.GetComponentInChildren<Camera>();
        Transform cameraTransform = camera != null ? camera.transform : null;

        float standingCameraY = cameraTransform != null ? cameraTransform.localPosition.y : 1.6f;
        float scaleY = Mathf.Max(0.1f, Mathf.Abs(player.lossyScale.y));
        float seatedCameraY = (eye != null ? eye.localPosition.y : DefaultEyeAboveFeet) / scaleY;

        // Black screen first, so nothing pops up.
        RectTransform fadeRoot = MissionOverlay.CreateCanvas("IntroFade", 120);
        Image black = UiKit.Solid(fadeRoot, "Black", Color.black);
        UiKit.Stretch(black.rectTransform);
        black.raycastTarget = false;

        // The player sits in the vehicle: no walking, the view can turn a little, the vehicle carries them.
        if (controller != null)
            controller.MovementLocked = true;
        if (body != null)
            body.enabled = false;

        vehicle.SetPositionAndRotation(start.position, start.rotation);
        vehicle.gameObject.SetActive(true);
        if (door != null)
            door.localRotation = Quaternion.identity;

        player.SetParent(seat, false);
        player.localPosition = Vector3.zero;
        player.localRotation = Quaternion.identity;

        if (cameraTransform != null)
        {
            Vector3 local = cameraTransform.localPosition;
            local.y = seatedCameraY;
            cameraTransform.localPosition = local;
        }

        if (controller != null)
            controller.LimitYaw(seat.eulerAngles.y, 80f);

        if (bus || snow)
            BeginWeather(cameraTransform != null ? cameraTransform : player, snow);

        Heading = bus ? "RAINY MORNING" : snow ? "SNOWY MORNING" : first ? "FIRST DAY" : "A NEW DAY";
        Hint = bus ? "You are on the bus to work. Look around while you ride." : "You are on your way to work. Look around while you ride.";
        // (No title banner here: the plate in the upper left already says what the player is doing, and the driver speaks at the bottom.)

        // The passengers sit down and settle while the bus still stands (the screen is black). The seating script measures where each person's hips
        // are and moves them onto the seat: that must be finished before the bus moves, or the moving seat keeps pulling them back and they fall behind.
        // Afterwards the script is switched off: they are the bus's children and simply ride with it.
        if (bus)
        {
            yield return new WaitForSeconds(2.6f);

            foreach (NpcWorker worker in vehicle.GetComponentsInChildren<NpcWorker>(true))
                worker.enabled = false;
        }

        // Fade in.
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.0f)
        {
            SetAlpha(black, 1f - t);
            yield return null;
        }

        SetAlpha(black, 0f);
        GameSounds.StartRide(bus);

        // The ride: fast at first, slowing down smoothly to the stop. The wheels turn and the view bobs a little.
        Transform[] wheels = FindWheels(vehicle);
        float elapsed = 0f;
        Vector3 from = start.position;
        Vector3 to = stop.position;
        float distance = Vector3.Distance(from, to);
        bool saidCity = false;
        bool saidClose = false;

        while (elapsed < RideSeconds)
        {
            elapsed += Time.deltaTime;

            if (!saidCity && elapsed > 3.2f)
            {
                saidCity = true;
                Hint = bus ? "Look around: the rain is heavy today." : snow ? "Look around: everything is white." : "Look around: the city is waking up.";
                MissionBanner.Say(driver, bus ? "Wet day. Good thing there is a bus." : snow ? "Snow since midnight. The roads are slow today." : "Busy morning in the city today.", 2.5f);
            }

            if (!saidClose && elapsed > RideSeconds - 3.2f)
            {
                saidClose = true;
                Hint = "Almost there.";
                MissionBanner.Say(driver, "Almost there. " + (bank ? "The bank" : "The Firewall Duty office") + " is just ahead.", 2.5f);
            }

            float t = Mathf.Clamp01(elapsed / RideSeconds);
            float eased = 1f - (1f - t) * (1f - t);
            float speedFactor = 1f - t;

            vehicle.position = Vector3.Lerp(from, to, eased);
            SpinWheels(wheels, 2f * distance / RideSeconds * speedFactor, Time.deltaTime);   // metres per second
            GameSounds.RideSpeed(0.25f + 0.75f * speedFactor, bus);

            if (cameraTransform != null)
            {
                float bob = (Mathf.Sin(Time.time * 17f) * 0.004f + Mathf.Sin(Time.time * 7.3f) * 0.006f) * speedFactor;
                Vector3 local = cameraTransform.localPosition;
                local.y = seatedCameraY + bob / scaleY;
                cameraTransform.localPosition = local;
            }

            yield return null;
        }

        vehicle.position = to;
        GameSounds.StopRide();
        Hint = "You have arrived.";
        MissionBanner.Say(driver, bus ? "We are here! Mind the rain, and have a good day." : snow ? "Here we are. Watch your step, the pavement is icy!" :
                                   first ? "Here we are! Have a great first day." : "Here we are! Have a good day.", 3f);
        yield return new WaitForSeconds(0.8f);

        // The door opens.
        yield return Rotate(door, Quaternion.identity, Quaternion.Euler(0f, doorAngle, 0f), 0.6f);

        // The player gets out by themselves: out of the seat (through the doorway, on a bus), onto the pavement, turning to face the building.
        Vector3 seatWorld = player.position;
        Quaternion seatRotation = player.rotation;
        player.SetParent(null, true);

        if (controller != null)
            controller.ClearYawLimit();

        float cameraFrom = cameraTransform != null ? cameraTransform.localPosition.y : standingCameraY;
        float exitSeconds = wayOut.Count > 0 ? 1.3f * (wayOut.Count + 1) : 1.7f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / exitSeconds)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 position;

            if (wayOut.Count > 0)
            {
                // Seat -> each point of the way out in turn -> the pavement, the same time for each part.
                int parts = wayOut.Count + 1;
                float scaled = Mathf.Clamp01(t) * parts;
                int part = Mathf.Min(parts - 1, (int)scaled);
                float within = Mathf.SmoothStep(0f, 1f, scaled - part);

                Vector3 partFrom = part == 0 ? seatWorld : wayOut[part - 1].position;
                Vector3 partTo = part < wayOut.Count ? wayOut[part].position : drop.position;
                position = Vector3.Lerp(partFrom, partTo, within);
            }
            else
            {
                position = Vector3.Lerp(seatWorld, drop.position, eased) + Vector3.up * (0.14f * Mathf.Sin(eased * Mathf.PI));
            }

            player.SetPositionAndRotation(position, Quaternion.Slerp(seatRotation, drop.rotation, eased));

            if (cameraTransform != null)
            {
                Vector3 local = cameraTransform.localPosition;
                local.y = Mathf.Lerp(cameraFrom, standingCameraY, eased);
                cameraTransform.localPosition = local;
            }

            yield return null;
        }

        player.SetPositionAndRotation(drop.position, drop.rotation);
        Riding = false;   // out of the vehicle: the objective plate comes back
        if (cameraTransform != null)
        {
            Vector3 local = cameraTransform.localPosition;
            local.y = standingCameraY;
            cameraTransform.localPosition = local;
        }

        if (body != null)
            body.enabled = true;
        if (controller != null)
            controller.MovementLocked = false;

        Destroy(fadeRoot.gameObject);
        StartCoroutine(Depart(vehicle, door, doorAngle, stop.position, away.position));

        // Now: walk in, and time in.
        AwaitingTimeIn = true;
        Heading = bus ? "RAINY MORNING:  TIME IN" : snow ? "SNOWY MORNING:  TIME IN" : first ? "FIRST DAY:  TIME IN" : "A NEW DAY:  TIME IN";
        Hint = "Walk through the main entrance and time in at " + greeter + ".";

        // The entrance: its marker (facing out to the street) tells which side of it the player is on.
        Transform entranceMarker = FindByName("Entrance");
        Vector3 entrancePoint = entranceMarker != null ? entranceMarker.position + Vector3.up * 1.4f : new Vector3(0f, 1.4f, -12f);
        Vector3 outward = entranceMarker != null ? entranceMarker.forward : Vector3.back;
        Vector3 entranceBase = entranceMarker != null ? entranceMarker.position : new Vector3(0f, 0f, -12f);

        while (AwaitingTimeIn)
        {
            if (!MissionDialogue.IsOpen)
            {
                bool outside = Vector3.Dot(player.position - entranceBase, outward) > 0.4f;
                Hint = outside ? "Walk through the main entrance and time in at " + greeter + "." : "Go to " + greeter + " and time in.";

                if (outside)
                    marker.Track(entrancePoint, "ENTRANCE");
                else
                    marker.Track(HeadPoint(receptionist), "TIME IN");
            }
            else
            {
                marker.Hide();
            }

            yield return null;
        }

        marker.Hide();

        // Inside now: the rain stops falling and the fog lifts, but the day stays grey (no sun) and the snow stays on the street for the whole mission, so what
        // you see through the windows still matches. Everything is put back when the mission scene ends (OnDestroy).
        StopRain();
        Active = false;
    }

    // Above a character's head (a teller on a tall chair sits much higher than a receptionist).
    private static Vector3 HeadPoint(Transform character)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return character.position + Vector3.up * 1.55f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return new Vector3(bounds.center.x, bounds.max.y + 0.25f, bounds.center.z);
    }

    // ---- The weather ------------------------------------------------------------------------------------------------------------

    private void BeginWeather(Transform follow, bool snow)
    {
        // The street has an invisible box over the building ("Intro_Shelter"): rain and snow that get into it are removed.
        Transform shelter = FindByName("Intro_Shelter");
        BoxCollider shelterBox = shelter != null ? shelter.GetComponent<BoxCollider>() : null;
        rain = RainEffect.Create(follow, snow, shelterBox);

        fogWas = RenderSettings.fog;
        fogColorWas = RenderSettings.fogColor;
        fogModeWas = RenderSettings.fogMode;
        fogDensityWas = RenderSettings.fogDensity;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = snow ? 0.014f : 0.012f;
        RenderSettings.fogColor = snow ? new Color(0.8f, 0.85f, 0.9f) : new Color(0.52f, 0.57f, 0.63f);

        // No sun while it rains or snows: the sun (the first directional light) is nearly switched off and casts no shadows, the sun disc is taken out of the
        // sky and the sky turns a flat overcast grey. The scene's soft light comes from the sky instead, so it stays bright enough to see.
        sun = RenderSettings.sun;
        if (sun == null)
        {
            foreach (Light candidate in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (candidate.type == LightType.Directional)
                {
                    sun = candidate;
                    break;
                }
            }
        }

        if (sun != null)
        {
            sunIntensityWas = sun.intensity;
            sunColorWas = sun.color;
            sunShadowsWas = sun.shadows;
            sun.intensity = sunIntensityWas * 0.06f;
            sun.shadows = LightShadows.None;
            sun.color = snow ? new Color(0.85f, 0.9f, 1f) : new Color(0.75f, 0.82f, 0.92f);
        }

        // The sky: a copy of the scene's sky with no sun disc and a grey tint (the project's own material is never changed).
        skyboxWas = RenderSettings.skybox;
        ambientIntensityWas = RenderSettings.ambientIntensity;

        if (skyboxWas != null)
        {
            overcastSky = new Material(skyboxWas);

            Color grey = snow ? new Color(0.82f, 0.85f, 0.9f) : new Color(0.5f, 0.54f, 0.6f);
            if (overcastSky.HasProperty("_SunSize")) overcastSky.SetFloat("_SunSize", 0f);
            if (overcastSky.HasProperty("_SunSizeConvergence")) overcastSky.SetFloat("_SunSizeConvergence", 1f);
            if (overcastSky.HasProperty("_SkyTint")) overcastSky.SetColor("_SkyTint", grey);
            if (overcastSky.HasProperty("_GroundColor")) overcastSky.SetColor("_GroundColor", grey);
            if (overcastSky.HasProperty("_Tint")) overcastSky.SetColor("_Tint", grey);
            if (overcastSky.HasProperty("_AtmosphereThickness")) overcastSky.SetFloat("_AtmosphereThickness", 2.2f);
            if (overcastSky.HasProperty("_Exposure")) overcastSky.SetFloat("_Exposure", snow ? 1.1f : 0.85f);

            RenderSettings.skybox = overcastSky;
        }

        // A little more light from the sky to make up for the sun.
        RenderSettings.ambientIntensity = ambientIntensityWas * (snow ? 1.45f : 1.3f);
        DynamicGI.UpdateEnvironment();

        // Snow lies on the ground, the pavement, the road and the trees, and the snow decorations appear.
        if (snow)
        {
            Transform decor = FindByName("Snow_Decor");
            if (decor != null)
            {
                snowDecor = decor.gameObject;
                snowDecor.SetActive(true);
            }

            GameObject group = GameObject.Find(GroupName);
            if (group != null)
            {
                foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    string n = renderer.name;
                    if (n == "Ground") Whiten(renderer, 0.85f);
                    else if (n == "Pavement_Near" || n == "Pavement_Far" || n == "Entrance_Path") Whiten(renderer, 0.8f);
                    else if (n == "Road") Whiten(renderer, 0.4f);
                    else if (n == "Leaves" || n == "Leaves2") Whiten(renderer, 0.55f);
                }
            }
        }

        weatherChanged = true;
    }

    // Mixes a surface's colour toward snow white. (A copy of the material is used, so the project's own materials are never changed.)
    private static void Whiten(Renderer renderer, float amount)
    {
        Color snowWhite = new Color(0.93f, 0.95f, 0.98f);
        Material material = renderer.material;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.Lerp(material.GetColor("_BaseColor"), snowWhite, amount));

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.Lerp(material.GetColor("_Color"), snowWhite, amount));
    }

    // The falling rain or snow stops and the fog goes; the grey sky, the missing sun and the snow on the ground stay (see the end of Play).
    private void StopRain()
    {
        if (rain != null)
        {
            rain.Stop();
            rain = null;
        }

        if (!weatherChanged)
            return;

        RenderSettings.fog = fogWas;
        RenderSettings.fogColor = fogColorWas;
        RenderSettings.fogMode = fogModeWas;
        RenderSettings.fogDensity = fogDensityWas;
    }

    private void EndWeather()
    {
        if (rain != null)
        {
            rain.Stop();
            rain = null;
        }

        if (snowDecor != null)
        {
            snowDecor.SetActive(false);
            snowDecor = null;
        }

        if (!weatherChanged)
            return;

        RenderSettings.fog = fogWas;
        RenderSettings.fogColor = fogColorWas;
        RenderSettings.fogMode = fogModeWas;
        RenderSettings.fogDensity = fogDensityWas;

        if (sun != null)
        {
            sun.intensity = sunIntensityWas;
            sun.color = sunColorWas;
            sun.shadows = sunShadowsWas;
        }

        RenderSettings.skybox = skyboxWas;
        RenderSettings.ambientIntensity = ambientIntensityWas;
        DynamicGI.UpdateEnvironment();

        if (overcastSky != null)
        {
            Destroy(overcastSky);
            overcastSky = null;
        }

        weatherChanged = false;
    }

    private void OnDestroy()
    {
        EndWeather();
    }

    // ---- Leaving, wheels, helpers ----------------------------------------------------------------------------------------------

    // The vehicle closes its door and drives off along the street.
    private IEnumerator Depart(Transform vehicle, Transform door, float doorAngle, Vector3 from, Vector3 to)
    {
        yield return new WaitForSeconds(1.2f);
        yield return Rotate(door, Quaternion.Euler(0f, doorAngle, 0f), Quaternion.identity, 0.5f);

        Transform[] wheels = FindWheels(vehicle);
        const float seconds = 4f;
        float distance = Vector3.Distance(from, to);

        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            float eased = t * t;
            vehicle.position = Vector3.Lerp(from, to, eased);
            SpinWheels(wheels, 2f * distance / seconds * t, Time.deltaTime);
            yield return null;
        }

        vehicle.gameObject.SetActive(false);
    }

    private static IEnumerator Rotate(Transform t, Quaternion from, Quaternion to, float seconds)
    {
        if (t == null)
            yield break;

        for (float time = 0f; time < 1f; time += Time.deltaTime / seconds)
        {
            t.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, time));
            yield return null;
        }

        t.localRotation = to;
    }

    // Turns every wheel by what the vehicle has driven (the wheel's radius is read from its tire).
    private static void SpinWheels(Transform[] wheels, float metersPerSecond, float deltaTime)
    {
        foreach (Transform wheel in wheels)
        {
            float radius = wheel.childCount > 0 ? Mathf.Max(0.1f, wheel.GetChild(0).localScale.x * 0.5f) : 0.33f;
            wheel.Rotate(Vector3.right * (metersPerSecond / radius * Mathf.Rad2Deg * deltaTime), Space.Self);
        }
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
    }

    private static Transform[] FindWheels(Transform vehicle)
    {
        System.Collections.Generic.List<Transform> wheels = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in vehicle.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("WheelPivot"))
                wheels.Add(t);
        }

        return wheels.ToArray();
    }

    private static Transform FindByName(string name)
    {
        GameObject group = GameObject.Find(GroupName);
        if (group == null)
            return null;

        foreach (Transform t in group.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }
}

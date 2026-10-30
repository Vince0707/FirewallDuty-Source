using UnityEngine;
using UnityEngine.SceneManagement;

// The sounds of the game (not the menu clicks, those are GameAudio). There are no sound files in the project, so every sound is made in code the first time it is
// needed and kept: the vehicle ride, doors (a swing door and a sliding door), the quiet noise of the office or the bank, the laptop opening, and the bright
// "you found it" chime. Everything plays at the Sound Effects volume (and the Master volume, which is the AudioListener's).
public static class GameSounds
{
    private const int Rate = 22050;

    private static GameObject host;
    private static AudioSource rideSource;
    private static AudioSource roomSource;
    private static AudioClip rideClip, roomOffice, roomBank, doorOpen, doorClose, slide, laptop, found;
    private static bool hooked;
    private static readonly AudioClip[] talk = new AudioClip[3];
    private static AudioClip interact;
    private static readonly AudioClip[] steps = new AudioClip[2];
    private static AudioSource stepSource;
    private static int stepFoot;
    private static float lastFound = -10f;

    // ---- Playing -----------------------------------------------------------------------------------------------------------------------

    // The engine and road of the taxi or bus. Call StopRide when the vehicle has stopped.
    public static void StartRide(bool bus)
    {
        if (!Application.isPlaying)
            return;

        if (rideClip == null)
            rideClip = MakeRide();

        rideSource = Loop(rideSource, "Ride", rideClip);
        rideSource.pitch = bus ? 0.8f : 1f;
        rideSource.volume = 0.5f * GameSettings.SfxLevel;
        rideSource.Play();
    }

    // The engine slows down with the vehicle (speed 0..1).
    public static void RideSpeed(float speed, bool bus)
    {
        if (rideSource == null)
            return;

        speed = Mathf.Clamp01(speed);
        rideSource.pitch = (bus ? 0.8f : 1f) * Mathf.Lerp(0.6f, 1f, speed);
        rideSource.volume = Mathf.Lerp(0.3f, 0.5f, speed) * GameSettings.SfxLevel;
    }

    public static void StopRide()
    {
        if (rideSource != null)
            rideSource.Stop();
    }

    // The quiet noise of the building while the player is in it. It stops by itself when the scene ends.
    public static void StartRoomTone(bool bank)
    {
        if (!Application.isPlaying)
            return;

        if (bank)
        {
            if (roomBank == null)
                roomBank = MakeRoom(true);
        }
        else if (roomOffice == null)
        {
            roomOffice = MakeRoom(false);
        }

        roomSource = Loop(roomSource, "RoomTone", bank ? roomBank : roomOffice);
        roomSource.volume = 0.32f * GameSettings.SfxLevel;   // "just a little noise"
        roomSource.Play();
    }

    public static void StopRoomTone()
    {
        if (roomSource != null)
            roomSource.Stop();
    }

    // A swing door opening or closing, heard from where the door is.
    public static void Door(Vector3 position, bool open)
    {
        if (!Application.isPlaying)
            return;

        if (doorOpen == null)
        {
            doorOpen = MakeDoor(true);
            doorClose = MakeDoor(false);
        }

        At(position, open ? doorOpen : doorClose, 0.9f);
    }

    public static void SlidingDoor(Vector3 position)
    {
        if (!Application.isPlaying)
            return;

        if (slide == null)
            slide = MakeSlide();

        At(position, slide, 0.8f);
    }

    // Somebody speaks to the player (a line of conversation, an answer): a soft babble, no words.
    public static void Talk()
    {
        if (!Application.isPlaying)
            return;

        int version = Random.Range(0, 3);
        if (talk[version] == null)
            talk[version] = MakeTalk(version);

        Flat(talk[version], 0.5f);
    }

    // The hand button was pressed on something.
    public static void Interact()
    {
        if (!Application.isPlaying)
            return;

        if (interact == null)
            interact = MakeInteract();

        Flat(interact, 0.6f);
    }

    // One footstep of the player. Called once per stride by PlayerMobileController; the left and right foot sound a little different.
    public static void Footstep()
    {
        if (!Application.isPlaying)
            return;

        if (steps[0] == null)
        {
            steps[0] = MakeStep(0);
            steps[1] = MakeStep(1);
        }

        if (stepSource == null)
        {
            stepSource = Host().AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;
        }

        stepFoot = 1 - stepFoot;
        stepSource.pitch = 0.94f + Random.value * 0.12f;
        stepSource.PlayOneShot(steps[stepFoot], 0.55f * GameSettings.SfxLevel);
    }

    public static void Laptop()
    {
        if (!Application.isPlaying)
            return;

        if (laptop == null)
            laptop = MakeLaptop();

        Flat(laptop, 0.9f);
    }

    // A hint or a piece of evidence was found (a "ka-ching" with a sparkle, no voice).
    public static void Found()
    {
        if (!Application.isPlaying)
            return;

        if (Time.unscaledTime - lastFound < 0.5f)
            return;

        lastFound = Time.unscaledTime;

        if (found == null)
            found = MakeFound();

        Flat(found, 1f);
    }

    // ---- Playback helpers --------------------------------------------------------------------------------------------------------------

    private static GameObject Host()
    {
        if (host == null)
        {
            host = new GameObject("GameSounds");
            Object.DontDestroyOnLoad(host);
        }

        if (!hooked)
        {
            hooked = true;
            SceneManager.sceneUnloaded += _ =>
            {
                StopRide();
                StopRoomTone();
            };
        }

        return host;
    }

    private static AudioSource Loop(AudioSource existing, string name, AudioClip clip)
    {
        if (existing == null)
        {
            existing = Host().AddComponent<AudioSource>();
            existing.playOnAwake = false;
            existing.spatialBlend = 0f;
            existing.loop = true;
        }

        existing.clip = clip;
        return existing;
    }

    private static void Flat(AudioClip clip, float volume)
    {
        AudioSource source = Host().AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.PlayOneShot(clip, volume * GameSettings.SfxLevel);
        Object.Destroy(source, clip.length + 0.2f);
    }

    private static void At(Vector3 position, AudioClip clip, float volume)
    {
        GameObject go = new GameObject("Sound");
        go.transform.position = position;

        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2.5f;
        source.maxDistance = 16f;
        source.volume = volume * GameSettings.SfxLevel;
        source.Play();
        Object.Destroy(go, clip.length + 0.2f);
    }

    // ---- Making the sounds -------------------------------------------------------------------------------------------------------------

    private static AudioClip Build(string name, float[] data)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));

        float scale = 0.85f / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] *= scale;

        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Soft noise: white noise run through a one-pole low-pass (the smaller the amount, the deeper the rumble).
    private static float[] Noise(int length, float amount, int seed)
    {
        System.Random random = new System.Random(seed);
        float[] data = new float[length];
        float last = 0f;

        for (int i = 0; i < length; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            last += (white - last) * amount;
            data[i] = last;
        }

        return data;
    }

    // Makes a loop seamless: the end fades into the start.
    private static void Seamless(float[] data, int fade)
    {
        int n = data.Length;
        float[] copy = (float[])data.Clone();

        for (int i = 0; i < fade; i++)
        {
            float w = i / (float)fade;
            data[i] = copy[i] * w + copy[n - fade + i] * (1f - w);
        }
    }

    // Engine hum (every note fits a whole number of times into the two seconds, so the loop has no click), with the rumble of the road.
    private static AudioClip MakeRide()
    {
        int n = Rate * 2;
        float[] rumble = Noise(n, 0.04f, 11);
        Seamless(rumble, Rate / 4);
        float[] data = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float wobble = 0.8f + 0.2f * Mathf.Sin(6.2831853f * 3f * t);
            float engine = Mathf.Sin(6.2831853f * 56f * t) * 0.5f + Mathf.Sin(6.2831853f * 112f * t) * 0.3f + Mathf.Sin(6.2831853f * 168f * t) * 0.12f;
            data[i] = engine * wobble * 0.5f + rumble[i] * 9f;
        }

        return Build("Ride", data);
    }

    // The noise of a busy room, kept quiet: the air and the hum of the lights and, in the office, far-away typing.
    private static AudioClip MakeRoom(bool bank)
    {
        int n = Rate * 16;
        float[] air = Noise(n, 0.10f, bank ? 21 : 22);
        Seamless(air, Rate);
        float[] data = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float hum = Mathf.Sin(6.2831853f * 120f * t) * 0.012f + Mathf.Sin(6.2831853f * 60f * t) * 0.01f;
            data[i] = air[i] * 0.22f + hum;
        }

        System.Random random = new System.Random(bank ? 5 : 6);

        if (!bank)
        {
            // Far-away keys: tiny clicks in short bursts.
            for (int burst = 0; burst < 5; burst++)
            {
                int at = random.Next(0, n - Rate * 2);
                int count = random.Next(4, 10);

                for (int c = 0; c < count; c++)
                {
                    int start = at + c * Rate / 9 + random.Next(0, Rate / 40);
                    float level = 0.25f + (float)random.NextDouble() * 0.15f;

                    for (int i = 0; i < 260 && start + i < n; i++)
                        data[start + i] += Mathf.Sin(i * 0.9f) * Mathf.Exp(-i / 40f) * level;
                }
            }
        }

        return Build("Room", data);
    }

    // An elevator door: the arrival "ding" (two soft notes, the second lower), then the door gliding open with a smooth, low hum (no whine).
    private static AudioClip MakeSlide()
    {
        int n = Rate * 22 / 10;
        float[] data = new float[n];
        float[] air = Noise(n, 0.05f, 51);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            float ding = Bell(t, 0f, 987.77f, 3.2f) * 0.55f + Bell(t, 0.34f, 740f, 3.0f) * 0.5f;

            float p = Mathf.Clamp01((t - 0.55f) / 1.4f);
            float envelope = Mathf.Sin(Mathf.PI * p);
            float glide = (air[i] * 4f + Mathf.Sin(6.2831853f * 82f * t) * 0.25f + Mathf.Sin(6.2831853f * 164f * t) * 0.08f) * envelope * envelope;

            data[i] = ding + glide * 0.5f;
        }

        return Build("Slide", data);
    }

    // Somebody answering (no words): a burst of keyboard typing, a few quick keys with small pauses, ending with a firmer key. Three versions, so it does not
    // repeat exactly.
    private static AudioClip MakeTalk(int version)
    {
        System.Random random = new System.Random(70 + version);
        int keys = 7 + version * 2;
        float[] data = new float[Rate * 3];
        float time = 0.02f;

        for (int k = 0; k < keys; k++)
        {
            Key(data, Mathf.RoundToInt(time * Rate), 0.7f + (float)random.NextDouble() * 0.3f, random);
            time += random.NextDouble() < 0.2 ? 0.2f : 0.06f + (float)random.NextDouble() * 0.07f;
        }

        // The last key (like enter) is firmer.
        Key(data, Mathf.RoundToInt((time + 0.1f) * Rate), 1.2f, random);

        return Build("Talk", Trim(data, time + 0.4f));
    }

    // Pressing the hand button on something: two quick key taps.
    private static AudioClip MakeInteract()
    {
        System.Random random = new System.Random(90);
        float[] data = new float[Rate / 2];
        Key(data, 0, 0.9f, random);
        Key(data, Mathf.RoundToInt(0.085f * Rate), 1.0f, random);
        return Build("Interact", Trim(data, 0.3f));
    }

    private static float[] Trim(float[] data, float seconds)
    {
        int length = Mathf.Min(data.Length, Mathf.RoundToInt(seconds * Rate));
        float[] copy = new float[length];
        System.Array.Copy(data, copy, length);
        return copy;
    }

    // One key of a keyboard: a sharp plastic "tick", a short hollow "thock" underneath, and a little rattle.
    private static void Key(float[] data, int start, float level, System.Random random)
    {
        float thockPitch = 150f + (float)random.NextDouble() * 90f;
        float tickPitch = 2200f + (float)random.NextDouble() * 1400f;

        for (int i = 0; i < 700 && start + i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float tick = Mathf.Sin(6.2831853f * tickPitch * t) * Mathf.Exp(-i / 30f);
            float thock = Mathf.Sin(6.2831853f * thockPitch * t) * Mathf.Exp(-t * 85f);
            float rattle = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-i / 55f);
            data[start + i] += (tick * 0.45f + thock * 0.9f + rattle * 0.5f) * level;
        }
    }

    // One footstep on a hard floor: the heel (a short click), the weight of the step (a low thump) and a little scuff of the sole.
    private static AudioClip MakeStep(int foot)
    {
        float[] data = new float[Rate * 22 / 100];
        System.Random random = new System.Random(300 + foot);

        Filtered(data, 0f, 0.02f, foot == 0 ? 1800f : 2100f, 1300f, 4f, 0.9f, 60f, random);   // the heel
        Thump(data, 0f, foot == 0 ? 105f : 120f, 0.9f, 38f);                                   // the weight
        Filtered(data, 0.03f, 0.13f, 900f, 450f, 1.3f, 0.35f, 22f, random);                   // the sole

        return Build("Step", data);
    }

    // A door like a real one. Opening: the handle turns (a metal click), the latch lets go (a lower click and a small thump), then the door swings with the
    // sound of air and wood and a faint hinge squeak. Closing: the swing, a solid wooden "thud" and the latch catching.
    private static AudioClip MakeDoor(bool open)
    {
        float seconds = open ? 1.1f : 0.9f;
        float[] data = new float[Mathf.RoundToInt(Rate * seconds)];
        System.Random random = new System.Random(open ? 41 : 42);

        if (open)
        {
            Filtered(data, 0f, 0.03f, 2600f, 3200f, 6f, 1.0f, 60f, random);          // the handle
            Filtered(data, 0.085f, 0.04f, 1500f, 1200f, 4f, 1.2f, 45f, random);      // the latch lets go
            Thump(data, 0.09f, 200f, 0.5f, 40f);
            Filtered(data, 0.16f, 0.75f, 380f, 620f, 1.2f, 0.45f, 0f, random);      // the swing (air and wood)
            Filtered(data, 0.22f, 0.65f, 760f, 1050f, 22f, 0.22f, -1f, random);     // the hinge, faint
        }
        else
        {
            Filtered(data, 0f, 0.4f, 320f, 520f, 1.2f, 0.4f, 0f, random);            // the swing
            Filtered(data, 0.36f, 0.12f, 250f, 800f, 1.4f, 1.1f, 28f, random);       // the knock of wood on wood
            Thump(data, 0.36f, 85f, 1.0f, 22f);                                     // the weight of the door
            Filtered(data, 0.375f, 0.03f, 1900f, 1500f, 5f, 0.9f, 60f, random);     // the latch catches
        }

        return Build(open ? "DoorOpen" : "DoorClose", data);
    }

    // Noise pushed through a band that glides from one pitch to another (a state-variable filter). decay > 0: a click or knock that dies away at once;
    // decay = 0: a swell that rises and falls; decay < 0: an uneven swell (a squeak that comes and goes).
    private static void Filtered(float[] data, float startSeconds, float lengthSeconds, float fromHz, float toHz, float q, float level, float decay, System.Random random)
    {
        int start = Mathf.RoundToInt(startSeconds * Rate);
        int length = Mathf.RoundToInt(lengthSeconds * Rate);
        float low = 0f, band = 0f;
        float damping = 1f / q;

        // The loudness of noise through a narrow band is small, so it is raised to about the level of the noise itself.
        float compensation = 1f / Mathf.Max(0.05f, Mathf.Sqrt(1.57f * (Mathf.Max(fromHz, toHz) / q) / (Rate * 0.5f)) * 0.577f) * 0.577f;

        for (int i = 0; i < length && start + i < data.Length; i++)
        {
            float p = i / (float)length;
            float hz = Mathf.Lerp(fromHz, toHz, p);
            float g = 2f * Mathf.Sin(Mathf.PI * hz / Rate);

            float white = (float)random.NextDouble() * 2f - 1f;
            low += g * band;
            float high = white - low - damping * band;
            band += g * high;

            float envelope;
            if (decay > 0f)
                envelope = Mathf.Min(1f, i / (Rate * 0.002f)) * Mathf.Exp(-p * decay * lengthSeconds * 4f);
            else if (decay < 0f)
                envelope = Mathf.Sin(Mathf.PI * p) * (0.35f + 0.65f * Mathf.Abs(Mathf.Sin(6.2831853f * 9f * p)));
            else
                envelope = Mathf.Sin(Mathf.PI * p) * Mathf.Sin(Mathf.PI * p);

            data[start + i] += band * damping * compensation * envelope * level;
        }
    }

    // A low thump (the weight of something solid).
    private static void Thump(float[] data, float startSeconds, float hz, float level, float decay)
    {
        int start = Mathf.RoundToInt(startSeconds * Rate);
        for (int i = 0; i < Rate / 4 && start + i < data.Length; i++)
        {
            float t = i / (float)Rate;
            data[start + i] += Mathf.Sin(6.2831853f * hz * t) * Mathf.Exp(-t * decay) * Mathf.Min(1f, i / (Rate * 0.003f)) * level;
        }
    }

    // The laptop: the lid, a soft key click, then a small rising start-up chime.
    private static AudioClip MakeLaptop()
    {
        int n = Rate * 9 / 10;
        float[] data = new float[n];
        float[] air = Noise(n, 0.3f, 61);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            float lid = t < 0.14f ? air[i] * Mathf.Sin(Mathf.PI * t / 0.14f) * 0.6f : 0f;
            float click = i < 220 ? Mathf.Sin(i * 0.7f) * Mathf.Exp(-i / 45f) * 0.7f : 0f;
            float chime = Note(t, 0.28f, 523.25f, 5f) + Note(t, 0.40f, 659.25f, 5f) + Note(t, 0.52f, 783.99f, 4.2f);
            data[i] = lid + click + chime * 0.35f;
        }

        return Build("Laptop", data);
    }

    // "Ka-ching": a quick high tick, then two bright bell notes (a rising fifth) with a sparkle on top. No voice.
    private static AudioClip MakeFound()
    {
        int n = Rate * 12 / 10;
        float[] data = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            float tick = i < 260 ? Mathf.Sin(i * 1.3f) * Mathf.Exp(-i / 50f) * 0.5f : 0f;
            float low = Bell(t, 0.05f, 1318.5f, 4.5f);
            float high = Bell(t, 0.17f, 1975.5f, 3.6f);
            float sparkle = Bell(t, 0.17f, 3951f, 6f) * 0.25f + Bell(t, 0.24f, 5274f, 8f) * 0.15f;
            data[i] = tick + low * 0.7f + high * 0.8f + sparkle;
        }

        return Build("Found", data);
    }

    // A soft sine note that starts at "start" seconds and fades away.
    private static float Note(float t, float start, float frequency, float decay)
    {
        if (t < start)
            return 0f;

        float local = t - start;
        return Mathf.Sin(6.2831853f * frequency * local) * Mathf.Min(1f, local / 0.01f) * Mathf.Exp(-local * decay);
    }

    // A bell: the note with two overtones that are not exact multiples (that is what makes it ring like metal).
    private static float Bell(float t, float start, float frequency, float decay)
    {
        if (t < start)
            return 0f;

        float local = t - start;
        float wave = Mathf.Sin(6.2831853f * frequency * local)
                   + 0.5f * Mathf.Sin(6.2831853f * frequency * 2.76f * local) * Mathf.Exp(-local * 6f)
                   + 0.25f * Mathf.Sin(6.2831853f * frequency * 5.4f * local) * Mathf.Exp(-local * 10f);

        return wave * Mathf.Min(1f, local / 0.003f) * Mathf.Exp(-local * decay);
    }
}

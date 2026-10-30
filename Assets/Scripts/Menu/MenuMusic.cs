using System.Collections;
using UnityEngine;

// The background music of the menu: a calm, slow "cybersecurity / IT" loop in D minor (a wide, soft synth pad, a deep bass note, a quiet digital bell sequence with
// an echo and now and then a tiny data blip). It fades in when the menu opens and plays until the player starts a mission. There is no music file in the project, so
// the loop is made in code the first time the menu opens (a second or so of work, spread over a few frames, so the menu does not freeze); the sound is the
// player's Master Volume in Settings. MenuApp adds it to the menu scene.
public class MenuMusic : MonoBehaviour
{
    private const int Rate = 22050;
    private const float Tempo = 84f;
    private const float MusicVolume = 0.32f;   // under the click sounds and the voices of the game, whatever the master volume is

    private static AudioClip cached;   // made once; the menu comes back after every mission

    private AudioSource source;
    private float fadeIn;

    public static void Create(GameObject host)
    {
        if (host.GetComponent<MenuMusic>() == null)
            host.AddComponent<MenuMusic>();
    }

    private void Start()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;

        StartCoroutine(MakeAndPlay());
    }

    private void Update()
    {
        if (source == null || !source.isPlaying)
            return;

        // Fades in over three seconds.
        fadeIn = Mathf.MoveTowards(fadeIn, 1f, Time.unscaledDeltaTime / 3f);
        source.volume = MusicVolume * fadeIn;
    }

    // ---- Making the loop ---------------------------------------------------------------------------------------------------------------

    private IEnumerator MakeAndPlay()
    {
        if (cached != null)
        {
            source.clip = cached;
            source.Play();
            yield break;
        }

        float beat = 60f / Tempo;
        int chordSamples = Mathf.RoundToInt(beat * 8f * Rate);   // each chord lasts two bars
        int total = chordSamples * 4;
        float[] data = new float[total];

        // Dm7 - Bbmaj7 - Gm7 - Asus: the notes of each chord (hertz).
        float[][] chords =
        {
            new[] { 220.00f, 261.63f, 349.23f },
            new[] { 233.08f, 349.23f, 440.00f },
            new[] { 196.00f, 233.08f, 349.23f },
            new[] { 220.00f, 293.66f, 329.63f }
        };

        float[] roots = { 73.42f, 58.27f, 49.00f, 55.00f };

        // The soft chords: each note swells in and out, overlapping the next chord so the changes are smooth (and the end joins the start of the loop).
        for (int c = 0; c < 4; c++)
        {
            foreach (float frequency in chords[c])
            {
                Pad(data, c * chordSamples, chordSamples + 2 * Rate, frequency, 0.10f);
                yield return null;
            }
        }

        // The bass: the root of the chord, twice per chord.
        for (int c = 0; c < 4; c++)
        {
            Bass(data, c * chordSamples, chordSamples / 2 + Rate, roots[c], 0.2f);
            Bass(data, c * chordSamples + chordSamples / 2, chordSamples / 2 + Rate, roots[c], 0.16f);
            yield return null;
        }

        // The pluck: eighth notes through the notes of the chord, an octave up, quiet.
        int[] pattern = { 0, -1, 2, -1, 1, -1, 2, 1, 0, -1, 1, -1, 2, -1, 1, -1 };
        int step = Mathf.RoundToInt(beat * 0.5f * Rate);
        float[] pluck = new float[total];

        for (int c = 0; c < 4; c++)
        {
            for (int n = 0; n < pattern.Length; n++)
            {
                if (pattern[n] >= 0)
                    Pluck(pluck, c * chordSamples + n * step, chords[c][pattern[n]] * 2f, 0.07f);
            }

            // A few tiny "data blips" (high, short, quiet) on notes of D minor pentatonic, at fixed places so the loop is always the same.
            float[] blips = { 587.33f, 698.46f, 880.00f, 1046.50f, 1174.66f };
            int[] places = { 3, 7, 12 };
            for (int b = 0; b < places.Length; b++)
                Blip(pluck, c * chordSamples + places[b] * step + step / 2, blips[(c * 3 + b) % blips.Length], 0.035f);

            yield return null;
        }

        // The echo on the pluck (a dotted eighth later, twice), then the plucks join the music.
        int delay = Mathf.RoundToInt(beat * 0.75f * Rate);
        for (int i = 0; i < total; i++)
        {
            float echo = pluck[(i - delay + total) % total] * 0.38f + pluck[(i - 2 * delay + total) % total] * 0.18f;
            data[i] += pluck[i] + echo;
        }

        yield return null;

        // A gentle smoothing (the top is rounded off), and the loudness set so that nothing clips.
        float last = 0f;
        float peak = 0.0001f;
        for (int i = 0; i < total; i++)
        {
            last += (data[i] - last) * 0.45f;
            data[i] = last;
            peak = Mathf.Max(peak, Mathf.Abs(last));
        }

        float scale = 0.7f / peak;
        for (int i = 0; i < total; i++)
            data[i] *= scale;

        AudioClip clip = AudioClip.Create("MenuMusic", total, 1, Rate, false);
        clip.SetData(data, 0);

        cached = clip;
        source.clip = clip;
        source.Play();
    }

    // Adds one swelling note at "start" (wrapping round the end of the loop).
    private static void Pad(float[] data, int start, int length, float frequency, float amplitude)
    {
        float seconds = length / (float)Rate;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Min(1f, t / 2f) * Mathf.Min(1f, (seconds - t) / 2f);
            envelope = envelope * envelope * (3f - 2f * envelope);

            float wave = Mathf.Sin(6.2831853f * frequency * t)
                       + 0.6f * Mathf.Sin(6.2831853f * frequency * 1.004f * t)
                       + 0.25f * Mathf.Sin(6.2831853f * frequency * 2f * t);

            data[(start + i) % data.Length] += wave * envelope * amplitude;
        }
    }

    private static void Bass(float[] data, int start, int length, float frequency, float amplitude)
    {
        float seconds = length / (float)Rate;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Min(1f, t / 0.08f) * Mathf.Exp(-t * 0.9f) * Mathf.Min(1f, (seconds - t) / 0.6f);
            data[(start + i) % data.Length] += Mathf.Sin(6.2831853f * frequency * t) * envelope * amplitude;
        }
    }

    private static void Pluck(float[] data, int start, float frequency, float amplitude)
    {
        int length = Rate;   // a second is enough for it to die away

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 6f);
            // A soft digital bell: the note, with a little FM that fades fast (the "glassy" start).
            float wave = Mathf.Sin(6.2831853f * frequency * t + 1.6f * Mathf.Exp(-t * 9f) * Mathf.Sin(6.2831853f * frequency * 2f * t));
            data[(start + i) % data.Length] += wave * envelope * amplitude;
        }
    }

    // A very short, high "blip" (a data packet).
    private static void Blip(float[] data, int start, float frequency, float amplitude)
    {
        int length = Rate / 8;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)Rate;
            float envelope = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 30f);
            data[(start + i) % data.Length] += Mathf.Sin(6.2831853f * frequency * t) * envelope * amplitude;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The Settings screen's choices (SettingsActivity): graphics quality, touch sensitivity, master and effects volume.
// Saved on the phone with the same names and defaults as the Android app, and applied to the game.
public static class GameSettings
{
    public static int GraphicsQuality { get => PlayerPrefs.GetInt("graphicsQuality", 1); set => Save("graphicsQuality", value); }
    public static int Language { get => PlayerPrefs.GetInt("language", 0); set => Save("language", value); }
    public static int Sensitivity { get => PlayerPrefs.GetInt("sensitivity", 50); set => Save("sensitivity", value); }
    public static int MasterVolume { get => PlayerPrefs.GetInt("masterVolume", 80); set => Save("masterVolume", value); }
    public static int SfxVolume { get => PlayerPrefs.GetInt("sfxVolume", 80); set => Save("sfxVolume", value); }

    // 50 % is the normal look speed of the player controller; 0 % is very slow, 100 % is twice as fast.
    public static float LookMultiplier => Mathf.Max(0.1f, Sensitivity / 50f);

    // Sound effects volume as 0..1 (master volume is applied to everything through AudioListener).
    public static float SfxLevel => SfxVolume / 100f;

    private static void Save(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }

    // ---- Graphics ---------------------------------------------------------------------------------------------

    // Low / Medium / High really change how heavy the game is: the render resolution, how far shadows are drawn, edge smoothing,
    // how far detailed models are used, and texture sharpness.
    private struct Quality
    {
        public float renderScale;
        public int msaa;
        public float shadowDistance;
        public float lodBias;
        public int pixelLights;
        public int textureMipLimit;
    }

    private static readonly Quality[] Levels =
    {
        new Quality { renderScale = 0.7f,  msaa = 1, shadowDistance = 20f, lodBias = 0.7f, pixelLights = 1, textureMipLimit = 1 },   // Low
        new Quality { renderScale = 0.85f, msaa = 1, shadowDistance = 35f, lodBias = 1f,   pixelLights = 2, textureMipLimit = 0 },   // Medium
        new Quality { renderScale = 1f,    msaa = 2, shadowDistance = 60f, lodBias = 1.5f, pixelLights = 4, textureMipLimit = 0 },   // High
    };

    private static bool originalsSaved;
    private static float originalScale, originalShadow;
    private static int originalMsaa;

    public static void Apply()
    {
        Quality q = Levels[Mathf.Clamp(GraphicsQuality, 0, Levels.Length - 1)];

        QualitySettings.lodBias = q.lodBias;
        QualitySettings.pixelLightCount = q.pixelLights;
        QualitySettings.globalTextureMipmapLimit = q.textureMipLimit;

        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            // Changing the pipeline asset in the Editor would change the file on disk, so the original values come back on exit.
            if (!originalsSaved)
            {
                originalsSaved = true;
                originalScale = urp.renderScale;
                originalShadow = urp.shadowDistance;
                originalMsaa = urp.msaaSampleCount;
                Application.quitting += RestoreAsset;
            }

            urp.renderScale = q.renderScale;
            urp.shadowDistance = q.shadowDistance;
            urp.msaaSampleCount = q.msaa;
        }

        // Master volume for everything that makes sound.
        AudioListener.volume = MasterVolume / 100f;
    }

    private static void RestoreAsset()
    {
        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null || !originalsSaved)
            return;

        urp.renderScale = originalScale;
        urp.shadowDistance = originalShadow;
        urp.msaaSampleCount = originalMsaa;
    }
}

// Menu sounds. There is no sound file in the project yet, so a short click is made in code; it plays at the Sound Effects volume
// (and the Master volume), which is how the two sliders in Settings can be heard.
public static class GameAudio
{
    private static AudioSource source;
    private static AudioClip click;
    private static float lastPlayed = -1f;

    public static void PlayClick()
    {
        Play(1f);
    }

    // Used while dragging a volume slider, so it does not stutter.
    public static void PlayPreview()
    {
        if (Time.unscaledTime - lastPlayed < 0.12f)
            return;

        Play(1f);
    }

    private static void Play(float scale)
    {
        lastPlayed = Time.unscaledTime;

        if (source == null)
        {
            GameObject go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        if (click == null)
            click = MakeClick();

        source.PlayOneShot(click, GameSettings.SfxLevel * scale);
    }

    // A soft button click: a short high "tick" on top of a lower, rounder "tap", both dying away at once (90 ms).
    private static AudioClip MakeClick()
    {
        const int rate = 44100;
        int samples = rate * 90 / 1000;
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float tick = Mathf.Sin(2f * Mathf.PI * 1600f * t) * Mathf.Exp(-i / (samples * 0.14f)) * 0.45f;
            float tap = Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-i / (samples * 0.3f)) * 0.5f;
            data[i] = tick + tap;
        }

        AudioClip clip = AudioClip.Create("Click", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

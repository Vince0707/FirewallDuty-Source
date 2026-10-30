using UnityEngine;

// Rain for the rainy arrival (office mission 2), or snow for the snowy one (mission 3): a sheet of falling streaks that follows the player's camera. The drops stop when they hit
// anything solid (roofs, the ground, the bus roof), so no rain falls inside the bus or inside the office. Nothing is saved into the scene:
// it is made when the arrival starts and removed when the player has timed in.
public class RainEffect : MonoBehaviour
{
    private Transform follow;
    private ParticleSystem system;
    private GameObject shelter;   // an invisible box over the office: a raindrop that gets in is removed
    private bool snow;
    private BoxCollider givenShelter;

    // shelter: the invisible box over the building (the street group has one: "Intro_Shelter"); without one the office's box is used.
    public static RainEffect Create(Transform follow, bool snow = false, BoxCollider shelterBox = null)
    {
        GameObject go = new GameObject(snow ? "Snow" : "Rain");
        RainEffect rain = go.AddComponent<RainEffect>();
        rain.follow = follow;
        rain.snow = snow;
        rain.givenShelter = shelterBox;
        rain.Build();
        return rain;
    }

    private void Build()
    {
        system = gameObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 5f;
        main.startLifetime = 0.9f;
        main.startSpeed = 0f;
        main.startSize = 0.035f;
        main.startColor = new Color(0.82f, 0.88f, 0.96f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2500;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 900f;

        // A wide flat box high above the player, so the rain falls all around.
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30f, 0.1f, 30f);

        // The drops fall fast, with a little wind. (All three axes must be set together.)
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-22f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        // A drop that hits a roof, the road or the ground is gone: this is what keeps the bus and the office dry.
        ParticleSystem.CollisionModule collision = system.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.lifetimeLoss = 1f;
        collision.dampen = 0f;
        collision.bounce = 0f;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.maxCollisionShapes = 128;
        collision.radiusScale = 0.2f;

        // The office is a shelter: its roof has no collider the drops could hit, so an invisible box fills the office (30 x 24 m, and far above it) and
        // every drop that gets inside it is removed. Rain falls outside, never inside.
        BoxCollider box = givenShelter;
        if (box == null)
        {
            shelter = new GameObject("RainShelter");
            shelter.transform.position = new Vector3(0f, 5f, 0f);
            box = shelter.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(29.6f, 10f, 23.6f);
        }

        ParticleSystem.TriggerModule trigger = system.trigger;
        trigger.enabled = true;
        trigger.inside = ParticleSystemOverlapAction.Kill;
        trigger.outside = ParticleSystemOverlapAction.Ignore;
        trigger.enter = ParticleSystemOverlapAction.Ignore;
        trigger.exit = ParticleSystemOverlapAction.Ignore;
        trigger.SetCollider(0, box);

        ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.lengthScale = 8f;
        particleRenderer.velocityScale = 0.02f;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
            particleRenderer.sharedMaterial = new Material(shader);

        // Snow: big soft flakes that drift down slowly and sway (instead of the fast thin streaks of rain).
        if (snow)
        {
            ParticleSystem.MainModule snowMain = system.main;
            snowMain.startLifetime = 6.5f;
            snowMain.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            snowMain.startColor = new Color(1f, 1f, 1f, 0.95f);
            snowMain.maxParticles = 3500;

            ParticleSystem.EmissionModule snowEmission = system.emission;
            snowEmission.rateOverTime = 480f;

            ParticleSystem.VelocityOverLifetimeModule snowVelocity = system.velocityOverLifetime;
            snowVelocity.x = new ParticleSystem.MinMaxCurve(0.4f);
            snowVelocity.y = new ParticleSystem.MinMaxCurve(-1.7f);
            snowVelocity.z = new ParticleSystem.MinMaxCurve(0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.7f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.4f;

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            if (particleRenderer.sharedMaterial != null)
                particleRenderer.sharedMaterial.mainTexture = SoftDot();
        }

        system.Play();
    }

    // A small soft white round dot (a flake), drawn once.
    private static Texture2D SoftDot()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, alpha * 1.6f)));
            }
        }

        texture.Apply();
        return texture;
    }

    private void LateUpdate()
    {
        if (follow != null)
            transform.position = follow.position + Vector3.up * 9f;
    }

    // The rain stops (the drops already in the air finish falling).
    public void Stop()
    {
        if (system != null)
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(gameObject, 2f);
    }

    private void OnDestroy()
    {
        if (shelter != null)
            Destroy(shelter);
    }
}

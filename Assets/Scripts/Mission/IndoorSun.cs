using UnityEngine;

// No sunlight inside the building. The sun (the scene's directional light) shone into the office and the bank through the thin walls, the gaps under the ceiling
// and the glass: white patches on the floor and jagged white edges along the tops of the walls. While the player is inside, the sun is faded right down (the
// building is lit by its own lamps and the ambient light, which is turned up a little to make up for it) and it comes back when the player is outside again,
// for the street and the arrival. It starts when the mission starts (MissionScenarioController adds it); the scenes are not edited.
public class IndoorSun : MonoBehaviour
{
    private const float FadeSpeed = 3f;          // how fast the sun fades (per second)
    private const float SunLeft = 0.03f;         // the part of the sun that is left inside
    private const float AmbientBoost = 1.35f;    // the ambient light inside, compared with outside

    private Light sun;
    private Transform player;
    private Bounds building;
    private bool hasBuilding;

    private bool applied;        // the sun is currently being faded by this component
    private float fade;          // 0 = outside, 1 = fully inside
    private float baseIntensity;
    private float baseAmbient;

    private void Start()
    {
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

        FindBuilding();
    }

    // The building is the area under the ceiling: the ceiling's footprint (or, without one, the floor's).
    private void FindBuilding()
    {
        Bounds? found = null;

        foreach (string wanted in new[] { "ceiling", "floor" })
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name.ToLowerInvariant() != wanted)
                    continue;

                Renderer renderer = root.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                if (found == null)
                    found = renderer.bounds;
                else
                {
                    Bounds b = found.Value;
                    b.Encapsulate(renderer.bounds);
                    found = b;
                }
            }

            if (found != null)
                break;
        }

        if (found != null)
        {
            building = found.Value;
            hasBuilding = true;
        }
    }

    private bool PlayerInside()
    {
        if (player == null)
        {
            PlayerMobileController controller = FindAnyObjectByType<PlayerMobileController>();
            if (controller == null)
                return false;

            player = controller.transform;
        }

        Vector3 p = player.position;
        return p.x > building.min.x && p.x < building.max.x && p.z > building.min.z && p.z < building.max.z;
    }

    private void LateUpdate()
    {
        if (sun == null || !hasBuilding)
            return;

        bool inside = PlayerInside();

        // Going in: note what the light is now (the weather may have changed it) and start fading it.
        if (inside && !applied)
        {
            applied = true;
            fade = 0f;
            baseIntensity = sun.intensity;
            baseAmbient = RenderSettings.ambientIntensity;
        }

        if (!applied)
            return;

        fade = Mathf.MoveTowards(fade, inside ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);

        sun.intensity = Mathf.Lerp(baseIntensity, baseIntensity * SunLeft, fade);
        RenderSettings.ambientIntensity = Mathf.Lerp(baseAmbient, baseAmbient * AmbientBoost, fade);

        // Back outside and fully faded back: hand the light back exactly as it was.
        if (!inside && fade <= 0.001f)
        {
            sun.intensity = baseIntensity;
            RenderSettings.ambientIntensity = baseAmbient;
            applied = false;
        }
    }

    private void OnDestroy()
    {
        if (applied && sun != null)
        {
            sun.intensity = baseIntensity;
            RenderSettings.ambientIntensity = baseAmbient;
        }
    }
}

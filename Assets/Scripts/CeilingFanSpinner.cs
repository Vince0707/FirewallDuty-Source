using UnityEngine;

// Spins a ceiling fan's blades. Put it on the blades object ("ceilingFan_blades"), whose pivot is the fan's shaft.
// The blades must not be Static (Batching Static meshes ignore transform changes): the setup tool clears that.
public class CeilingFanSpinner : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second. 360 = one turn per second.")]
    [SerializeField] private float degreesPerSecond = 300f;
    [Tooltip("Each fan runs this much faster or slower than the others (0.2 = plus or minus 20 percent), so they never look in step.")]
    [SerializeField, Range(0f, 0.5f)] private float speedVariation = 0.2f;
    [Tooltip("Axis to spin around, in the blades' own space. Y is up for these fans.")]
    [SerializeField] private Vector3 axis = Vector3.up;
    [Tooltip("Don't turn the blades while no camera can see them (saves a little work on mobile).")]
    [SerializeField] private bool onlyWhenVisible = true;

    private Renderer[] renderers;
    private float speed;
    private bool warnedStatic;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();

        // A fixed random speed per fan and a random starting angle, so a room of fans doesn't turn in lockstep.
        speed = degreesPerSecond * (1f + Random.Range(-speedVariation, speedVariation));
        transform.Rotate(axis, Random.Range(0f, 360f), Space.Self);

        foreach (Renderer r in renderers)
        {
            if (r.isPartOfStaticBatch)
            {
                UnityEngine.Debug.LogWarning(name + " is Batching Static, so the blades will not visibly turn. " +
                                             "Run Tools > Firewall Duty > Set Up Ceiling Fans, or untick Static on the blades.", this);
                warnedStatic = true;
                break;
            }
        }
    }

    private void Update()
    {
        if (onlyWhenVisible && !IsVisible())
            return;

        transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
    }

    private bool IsVisible()
    {
        if (renderers == null || renderers.Length == 0)
            return true;

        foreach (Renderer r in renderers)
        {
            if (r != null && r.isVisible)
                return true;
        }

        return false;
    }
}

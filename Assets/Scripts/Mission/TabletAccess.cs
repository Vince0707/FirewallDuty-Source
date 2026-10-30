using UnityEngine;

// Whether the player has their Cyber Tablet yet. In mission 1 of each environment the player does not start with it: it is handed over during the mission
// (the Vice President in the office, the Bank Manager in the bank; see StepKind.Tablet). Until then the TABLET button is dim and cannot be pressed. When the
// tablet is received the button goes back to normal and an arrow under it tells the player they can open it (until they do).
// In every other mission the tablet is there from the start.
public static class TabletAccess
{
    // True while the player has not received the tablet yet.
    public static bool Locked { get; private set; }

    // True from the moment the tablet is received until the player opens it for the first time: the arrow under the button.
    public static bool ShowArrow { get; private set; }

    // At the start of a mission: locked when the mission hands the tablet over, free otherwise.
    public static void Begin(bool locked)
    {
        Locked = locked;
        ShowArrow = false;
    }

    // The tablet is in the player's hands.
    public static void Receive()
    {
        if (!Locked)
            return;

        Locked = false;
        ShowArrow = true;
    }

    // The player opened the tablet: the arrow has done its job.
    public static void Opened()
    {
        ShowArrow = false;
    }
}

// A tablet held out in a character's hand, for the player to take (the Vice President, the Bank Manager). It is a small tablet with a glowing screen made of
// plain shapes, shown only while it is that character's turn to hand it over. A character who stands holds it out in front of them with a hand reaching for it;
// a character who sits (the Bank Manager) has it raised over the desk.
public class HeldTablet : MonoBehaviour
{
    private Transform prop;
    private Material screen;
    private Animator animator;
    private NpcGestures gestures;
    private bool holding;
    private bool releasing;
    private bool seated;
    private float handWeight;

    public static HeldTablet Ensure(Transform character)
    {
        HeldTablet held = character.GetComponent<HeldTablet>();
        if (held == null)
            held = character.gameObject.AddComponent<HeldTablet>();

        return held;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        gestures = GetComponent<NpcGestures>();
        Build();
        prop.gameObject.SetActive(false);
    }

    // Shows the tablet in the hand (true) or puts it away because it was taken (false).
    public void Hold(bool on)
    {
        if (holding == on)
            return;

        holding = on;
        prop.gameObject.SetActive(on);

        // While the hand holds the tablet out, the random hand gestures would fight with it.
        if (gestures != null)
            gestures.enabled = !on;

        if (!on)
            releasing = true;
    }

    private void Build()
    {
        GameObject root = new GameObject("TabletInHand");
        root.transform.SetParent(transform, false);
        prop = root.transform;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material body = new Material(lit != null ? lit : Shader.Find("Sprites/Default")) { color = new Color(0.07f, 0.09f, 0.13f) };
        if (body.HasProperty("_BaseColor"))
            body.SetColor("_BaseColor", new Color(0.07f, 0.09f, 0.13f));

        screen = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.3f, 0.85f, 1f) };

        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Body";
        frame.transform.SetParent(prop, false);
        frame.transform.localScale = new Vector3(0.34f, 0.23f, 0.022f);
        frame.GetComponent<MeshRenderer>().sharedMaterial = body;
        Destroy(frame.GetComponent<Collider>());

        GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glass.name = "Screen";
        glass.transform.SetParent(prop, false);
        glass.transform.localPosition = new Vector3(0f, 0f, 0.0125f);
        glass.transform.localScale = new Vector3(0.31f, 0.2f, 0.004f);
        glass.GetComponent<MeshRenderer>().sharedMaterial = screen;
        Destroy(glass.GetComponent<Collider>());

        // The picture on the screen: a few bars, like the tablet's own screen.
        for (int i = 0; i < 3; i++)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Line";
            bar.transform.SetParent(prop, false);
            bar.transform.localPosition = new Vector3(-0.03f, 0.055f - i * 0.05f, 0.0155f);
            bar.transform.localScale = new Vector3(0.2f - i * 0.04f, 0.018f, 0.002f);
            Material line = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 0.25f, 0.4f) };
            bar.GetComponent<MeshRenderer>().sharedMaterial = line;
            Destroy(bar.GetComponent<Collider>());
        }
    }

    private void LateUpdate()
    {
        if (!holding)
            return;

        // Seated (the hips low): the tablet is raised over the desk, standing on its edge facing the player; standing: held out in front of the waist.
        seated = Hips() < 0.65f;

        if (seated)
        {
            prop.localPosition = Vector3.Lerp(prop.localPosition, new Vector3(0.12f, 0.86f, 0.5f), 8f * Time.deltaTime);
            prop.localRotation = Quaternion.Slerp(prop.localRotation, Quaternion.Euler(-55f, 0f, 0f), 8f * Time.deltaTime);
        }
        else
        {
            prop.localPosition = Vector3.Lerp(prop.localPosition, new Vector3(0.18f, 1.02f, 0.42f), 8f * Time.deltaTime);
            prop.localRotation = Quaternion.Slerp(prop.localRotation, Quaternion.Euler(-28f, 0f, 0f), 8f * Time.deltaTime);
        }

        // The screen glows, so the player notices it.
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3.5f);
        screen.color = new Color(0.3f * pulse + 0.2f, 0.85f * pulse + 0.1f, 1f, 1f);
    }

    // How high the hips are above the character's feet (a standing character: about 0.8, a seated one: about 0.5).
    private float Hips()
    {
        if (animator == null)
            return 1f;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        return hips != null ? hips.position.y - transform.position.y : 1f;
    }

    // The right hand reaches for the tablet (a standing character only).
    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null)
            return;

        if (!holding || seated)
        {
            // Let go once, after the tablet is taken.
            if (releasing)
            {
                handWeight = Mathf.MoveTowards(handWeight, 0f, 4f * Time.deltaTime);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);

                if (handWeight <= 0f)
                    releasing = false;
            }

            return;
        }

        handWeight = Mathf.MoveTowards(handWeight, 0.9f, 3f * Time.deltaTime);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, prop.TransformPoint(new Vector3(0.1f, -0.1f, -0.03f)));
    }
}

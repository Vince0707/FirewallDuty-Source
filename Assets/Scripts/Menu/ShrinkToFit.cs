using UnityEngine;

// Scales a block of UI down when it is taller than the screen it sits on, so nothing is ever cut off (the Android layouts
// scrolled or overflowed on short screens). It never scales up.
public class ShrinkToFit : MonoBehaviour
{
    [SerializeField] private float margin = 12f;

    private RectTransform rect;
    private RectTransform parent;

    private void Awake()
    {
        rect = (RectTransform)transform;
        parent = rect.parent as RectTransform;
    }

    private void LateUpdate()
    {
        if (rect == null || parent == null)
            return;

        float available = parent.rect.height - margin * 2f;
        float needed = rect.rect.height;   // the unscaled height

        float scale = needed > 1f && needed > available ? Mathf.Max(0.5f, available / needed) : 1f;
        if (!Mathf.Approximately(rect.localScale.x, scale))
            rect.localScale = new Vector3(scale, scale, 1f);
    }
}

using UnityEngine;

// The pictures of the hand button, drawn in code at a high resolution with smooth edges (the old ones were a 64 pixel knob and a blocky hand, blown up, so they
// looked blurry): a round plate in the navy and gold of the menu, and a hand in cream and gold. HudStyle.Apply puts them on the button when a mission starts.
public static class HandButtonArt
{
    private const int Size = 384;

    private static Sprite plate;
    private static Sprite hand;

    public static Sprite Plate()
    {
        if (plate == null)
            plate = Make("HandPlate", PlatePixel);

        return plate;
    }

    public static Sprite Hand()
    {
        if (hand == null)
            hand = Make("HandIcon", HandPixel);

        return hand;
    }

    private static Sprite Make(string name, System.Func<Vector2, Color> pixel)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel(new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size));
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---- The plate ---------------------------------------------------------------------------------------------------------------

    private static Color PlatePixel(Vector2 p)
    {
        float radius = (p - new Vector2(0.5f, 0.5f)).magnitude;   // 0 in the middle, 0.5 at the edge
        float edge = Mathf.Clamp01((0.5f - radius) * Size);      // one pixel of soft edge
        if (edge <= 0f)
            return Color.clear;

        Color gold = new Color32(0xF3, 0xD7, 0x93, 255);
        Color navyCenter = new Color32(0x1B, 0x4A, 0x92, 255);
        Color navyEdge = new Color32(0x0A, 0x22, 0x48, 255);

        // The gold rim, with a soft step into the navy inside.
        float rim = Mathf.Clamp01((radius - 0.455f) * Size);
        Color fill = Color.Lerp(navyCenter, navyEdge, Mathf.Clamp01(radius / 0.46f) * Mathf.Clamp01(radius / 0.46f));

        // A light sheen on the upper half, like a glass button.
        float sheen = Mathf.Clamp01((p.y - 0.5f) * 2f) * Mathf.Clamp01(1f - radius / 0.42f) * 0.16f;
        fill = Color.Lerp(fill, Color.white, sheen);

        // A thin inner ring.
        float ring = 1f - Mathf.Clamp01(Mathf.Abs(radius - 0.415f) * Size / 1.6f);
        fill = Color.Lerp(fill, new Color32(0x6F, 0xB1, 0xF2, 255), ring * 0.55f);

        Color color = Color.Lerp(fill, gold, rim);
        color.a = edge;
        return color;
    }

    // ---- The hand ----------------------------------------------------------------------------------------------------------------

    // An open hand (palm, four fingers, thumb), in coordinates 0..1. The shapes are blended smoothly into one.
    private static Color HandPixel(Vector2 p)
    {
        float d = RoundedBox(p, new Vector2(0.5f, 0.33f), new Vector2(0.20f, 0.16f), 0.09f);              // palm
        d = Smooth(d, RoundedBox(p, new Vector2(0.5f, 0.17f), new Vector2(0.15f, 0.09f), 0.06f));         // wrist
        d = Smooth(d, Capsule(p, new Vector2(0.375f, 0.45f), new Vector2(0.355f, 0.80f), 0.056f));        // index
        d = Smooth(d, Capsule(p, new Vector2(0.475f, 0.47f), new Vector2(0.475f, 0.88f), 0.058f));        // middle
        d = Smooth(d, Capsule(p, new Vector2(0.575f, 0.46f), new Vector2(0.595f, 0.83f), 0.055f));        // ring
        d = Smooth(d, Capsule(p, new Vector2(0.67f, 0.42f), new Vector2(0.71f, 0.68f), 0.047f));          // little finger
        d = Smooth(d, Capsule(p, new Vector2(0.32f, 0.29f), new Vector2(0.165f, 0.50f), 0.058f));         // thumb

        float alpha = Mathf.Clamp01(0.5f - d * Size);
        if (alpha <= 0f)
            return Color.clear;

        // Cream at the fingertips, going to gold at the wrist.
        Color top = new Color32(0xFF, 0xF3, 0xD0, 255);
        Color bottom = new Color32(0xF3, 0xD7, 0x93, 255);
        Color color = Color.Lerp(bottom, top, Mathf.Clamp01((p.y - 0.1f) / 0.75f));

        // A soft shade just inside the edge gives the hand a little roundness.
        float inside = Mathf.Clamp01(-d * Size / 7f);
        color = Color.Lerp(color * 0.82f, color, inside);

        color.a = alpha;
        return color;
    }

    private static float Smooth(float a, float b)
    {
        const float k = 0.025f;
        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return Mathf.Lerp(b, a, h) - k * h * (1f - h);
    }

    private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        Vector2 pa = p - a;
        Vector2 ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - radius;
    }

    private static float RoundedBox(Vector2 p, Vector2 center, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - halfSize + Vector2.one * radius;
        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
        return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + outside - radius;
    }
}

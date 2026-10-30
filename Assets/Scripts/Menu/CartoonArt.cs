using System;
using System.Collections.Generic;
using UnityEngine;

// Cartoon pictures drawn in code (the project has no icon files for these): threat icons, a trophy, stars, a shield, and the coloured
// card backgrounds of the mission list. They are drawn once, the first time they are needed, and kept.
// Pictures are 128 x 128 (cards 128 x 176) with the origin at the bottom left.
public static class CartoonArt
{
    private static readonly Color Ink = new Color32(0x0B, 0x1F, 0x33, 255);
    private static readonly Color White = new Color32(0xF6, 0xF9, 0xFC, 255);
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ---- A small painter: shapes are distance functions, drawn smooth-edged over what is already there ---------------

    private class Painter
    {
        private readonly int width, height;
        private readonly Color[] pixels;

        public Painter(int width, int height)
        {
            this.width = width;
            this.height = height;
            pixels = new Color[width * height];
        }

        // distance(x, y) is negative inside the shape.
        public void Fill(Func<float, float, float> distance, Color color)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cover = Mathf.Clamp01(0.5f - distance(x + 0.5f, y + 0.5f));
                    if (cover <= 0f)
                        continue;

                    int i = y * width + x;
                    Color under = pixels[i];
                    float a = color.a * cover;
                    float outA = a + under.a * (1f - a);
                    Color result = new Color(
                        (color.r * a + under.r * under.a * (1f - a)) / outA,
                        (color.g * a + under.g * under.a * (1f - a)) / outA,
                        (color.b * a + under.b * under.a * (1f - a)) / outA,
                        outA);
                    pixels[i] = result;
                }
            }
        }

        public void Circle(float cx, float cy, float r, Color color)
        {
            Fill((x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r, color);
        }

        public void Ring(float cx, float cy, float r, float thickness, Color color)
        {
            Fill((x, y) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - thickness * 0.5f, color);
        }

        public void Box(float cx, float cy, float halfWidth, float halfHeight, float radius, Color color)
        {
            Fill((x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - halfWidth + radius;
                float qy = Mathf.Abs(y - cy) - halfHeight + radius;
                return Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;
            }, color);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color color)
        {
            float smallest = Mathf.Min(rx, ry);
            Fill((x, y) => (Mathf.Sqrt((x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry)) - 1f) * smallest, color);
        }

        public void Line(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Fill((x, y) => SegmentDistance(new Vector2(x, y), a, b) - thickness * 0.5f, color);
        }

        // grow > 0 makes the polygon bigger all round (used for outlines).
        public void Polygon(Vector2[] points, Color color, float grow = 0f)
        {
            Fill((x, y) =>
            {
                Vector2 p = new Vector2(x, y);
                float nearest = float.MaxValue;
                bool inside = false;

                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    nearest = Mathf.Min(nearest, SegmentDistance(p, points[j], points[i]));

                    if ((points[i].y > y) != (points[j].y > y) &&
                        x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                        inside = !inside;
                }

                return (inside ? -nearest : nearest) - grow;
            }, color);
        }

        public Sprite ToSprite()
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    private static Sprite Make(string key, Func<Sprite> draw)
    {
        if (!cache.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            sprite = draw();
            cache[key] = sprite;
        }

        return sprite;
    }

    // ---- The icons ------------------------------------------------------------------------------------------------------

    // The icon of a threat ("Phishing Email", "Malware Files", "Fake Pop-up Windows", "Spam Email", "Social Engineering",
    // "Unauthorized Access").
    public static Sprite ThreatIcon(string threat)
    {
        switch ((threat ?? "").Trim().ToLowerInvariant())
        {
            case "phishing email": return PhishingIcon();
            case "malware files": return SkullIcon();
            case "fake pop-up windows": return WarningIcon();
            case "spam email": return SpamIcon();
            case "social engineering": return DetectiveIcon();
            case "unauthorized access": return LockIcon();
            default: return ShieldIcon();
        }
    }

    // The icon of a certificate (the ids used by the Achievements screen).
    public static Sprite CertificateIcon(string id)
    {
        switch (id)
        {
            case "phishing_detection": return PhishingIcon();
            case "malware_detection": return SkullIcon();
            case "popup_protection": return WarningIcon();
            case "spam_mail_analyst": return SpamIcon();
            case "social_engineering_awareness": return DetectiveIcon();
            case "access_control_defender": return LockIcon();
            case "trivia_master": return TrophyIcon();
            default: return ShieldIcon();
        }
    }

    public static Sprite ShieldIcon()
    {
        return Make("shield", () =>
        {
            Painter p = new Painter(128, 128);
            Vector2[] shield = { new Vector2(64, 120), new Vector2(112, 102), new Vector2(112, 62), new Vector2(64, 10), new Vector2(16, 62), new Vector2(16, 102) };
            Vector2[] lightHalf = { new Vector2(64, 120), new Vector2(64, 10), new Vector2(16, 62), new Vector2(16, 102) };

            p.Polygon(shield, Ink, 4f);
            p.Polygon(shield, new Color32(0x2F, 0x80, 0xED, 255));
            p.Polygon(lightHalf, new Color32(0x56, 0x9C, 0xFF, 255));
            p.Line(new Vector2(40, 64), new Vector2(57, 47), 14f, White);
            p.Line(new Vector2(57, 47), new Vector2(90, 86), 14f, White);
            return p.ToSprite();
        });
    }

    public static Sprite PhishingIcon()
    {
        return Make("phishing", () =>
        {
            Painter p = new Painter(128, 128);
            Envelope(p, new Color32(0xE8, 0xF1, 0xFB, 255));

            // A fishing hook hanging from the corner: the bait.
            Color gold = new Color32(0xFF, 0xB3, 0x00, 255);
            p.Line(new Vector2(104, 118), new Vector2(104, 54), 10f, Ink);
            p.Line(new Vector2(104, 118), new Vector2(104, 54), 5f, gold);
            p.Ring(92, 54, 14f, 10f, Ink);
            p.Ring(92, 54, 14f, 5f, gold);
            return p.ToSprite();
        });
    }

    public static Sprite SpamIcon()
    {
        return Make("spam", () =>
        {
            Painter p = new Painter(128, 128);
            Envelope(p, new Color32(0xF3, 0xE5, 0xF5, 255));

            // A red badge: junk mail.
            p.Circle(98, 98, 24f, Ink);
            p.Circle(98, 98, 20f, new Color32(0xE5, 0x39, 0x35, 255));
            p.Line(new Vector2(89, 89), new Vector2(107, 107), 8f, White);
            p.Line(new Vector2(89, 107), new Vector2(107, 89), 8f, White);
            return p.ToSprite();
        });
    }

    private static void Envelope(Painter p, Color paper)
    {
        p.Box(64, 60, 58, 42, 9f, Ink);
        p.Box(64, 60, 53, 37, 6f, paper);
        Color fold = new Color32(0x6F, 0x8B, 0xA8, 255);
        p.Line(new Vector2(14, 92), new Vector2(64, 56), 6f, fold);
        p.Line(new Vector2(114, 92), new Vector2(64, 56), 6f, fold);
        p.Line(new Vector2(14, 28), new Vector2(50, 58), 4f, fold);
        p.Line(new Vector2(114, 28), new Vector2(78, 58), 4f, fold);
    }

    public static Sprite SkullIcon()
    {
        return Make("skull", () =>
        {
            Painter p = new Painter(128, 128);
            p.Circle(64, 76, 46f, Ink);
            p.Box(64, 34, 28, 22, 8f, Ink);
            p.Circle(64, 76, 42f, White);
            p.Box(64, 34, 24, 18, 6f, White);
            p.Circle(46, 74, 12f, Ink);
            p.Circle(82, 74, 12f, Ink);
            p.Polygon(new[] { new Vector2(64, 62), new Vector2(56, 46), new Vector2(72, 46) }, Ink);
            for (int i = 0; i < 4; i++)
                p.Line(new Vector2(48 + i * 10.7f, 20), new Vector2(48 + i * 10.7f, 34), 3.5f, Ink);
            p.Circle(41, 80, 4f, new Color(1f, 1f, 1f, 0.9f));
            p.Circle(77, 80, 4f, new Color(1f, 1f, 1f, 0.9f));
            return p.ToSprite();
        });
    }

    public static Sprite WarningIcon()
    {
        return Make("warning", () =>
        {
            Painter p = new Painter(128, 128);
            Vector2[] triangle = { new Vector2(64, 118), new Vector2(120, 20), new Vector2(8, 20) };

            p.Polygon(triangle, Ink, 7f);
            p.Polygon(triangle, new Color32(0xFF, 0xC4, 0x00, 255), 2f);
            p.Line(new Vector2(64, 52), new Vector2(64, 80), 13f, Ink);
            p.Circle(64, 35, 7.5f, Ink);
            return p.ToSprite();
        });
    }

    public static Sprite DetectiveIcon()
    {
        return Make("detective", () =>
        {
            Painter p = new Painter(128, 128);
            Color coat = new Color32(0x5B, 0x6B, 0x7B, 255);

            // shoulders
            p.Polygon(new[] { new Vector2(8, 4), new Vector2(120, 4), new Vector2(104, 36), new Vector2(24, 36) }, Ink, 3f);
            p.Polygon(new[] { new Vector2(8, 4), new Vector2(120, 4), new Vector2(104, 36), new Vector2(24, 36) }, coat);
            p.Polygon(new[] { new Vector2(50, 38), new Vector2(78, 38), new Vector2(64, 18) }, White);

            // face
            p.Circle(64, 62, 33f, Ink);
            p.Circle(64, 62, 29f, new Color32(0xF2, 0xC9, 0xA0, 255));

            // sunglasses
            p.Box(49, 64, 13, 9, 5f, Ink);
            p.Box(79, 64, 13, 9, 5f, Ink);
            p.Line(new Vector2(60, 66), new Vector2(68, 66), 4f, Ink);
            p.Line(new Vector2(52, 68), new Vector2(46, 71), 2.5f, new Color(1f, 1f, 1f, 0.7f));
            p.Line(new Vector2(54, 48), new Vector2(74, 48), 4f, Ink);

            // hat
            p.Ellipse(64, 86, 48, 10, Ink);
            p.Box(64, 102, 27, 17, 7f, Ink);
            p.Ellipse(64, 86, 44, 7, new Color32(0x37, 0x47, 0x4F, 255));
            p.Box(64, 101, 23, 13, 5f, new Color32(0x37, 0x47, 0x4F, 255));
            p.Box(64, 93, 23, 4, 0f, new Color32(0xFF, 0xB3, 0x00, 255));
            return p.ToSprite();
        });
    }

    public static Sprite LockIcon()
    {
        return Make("lock", () =>
        {
            Painter p = new Painter(128, 128);
            Color gold = new Color32(0xFF, 0xB3, 0x00, 255);

            // shackle
            p.Ring(64, 80, 26f, 17f, Ink);
            p.Ring(64, 80, 26f, 9f, new Color32(0xC9, 0xD1, 0xD9, 255));

            // body
            p.Box(64, 46, 42, 34, 10f, Ink);
            p.Box(64, 46, 38, 30, 8f, gold);
            p.Box(64, 58, 38, 10, 6f, new Color32(0xFF, 0xCB, 0x4D, 255));

            // keyhole
            p.Circle(64, 50, 8f, Ink);
            p.Polygon(new[] { new Vector2(64, 50), new Vector2(57, 30), new Vector2(71, 30) }, Ink);
            return p.ToSprite();
        });
    }

    public static Sprite TrophyIcon()
    {
        return Make("trophy", () =>
        {
            Painter p = new Painter(128, 128);
            Color gold = new Color32(0xFF, 0xC1, 0x07, 255);
            Color light = new Color32(0xFF, 0xE0, 0x82, 255);

            // handles
            p.Ring(30, 86, 22f, 13f, Ink);
            p.Ring(98, 86, 22f, 13f, Ink);
            p.Ring(30, 86, 22f, 7f, gold);
            p.Ring(98, 86, 22f, 7f, gold);

            // cup
            Vector2[] cup = { new Vector2(26, 118), new Vector2(102, 118), new Vector2(96, 70), new Vector2(76, 52), new Vector2(52, 52), new Vector2(32, 70) };
            p.Polygon(cup, Ink, 5f);
            p.Polygon(cup, gold);
            p.Polygon(new[] { new Vector2(26, 118), new Vector2(54, 118), new Vector2(50, 56), new Vector2(32, 70) }, light);

            // stem and base
            p.Box(64, 40, 9, 14, 3f, Ink);
            p.Box(64, 40, 6, 11, 2f, gold);
            p.Box(64, 20, 30, 12, 5f, Ink);
            p.Box(64, 20, 26, 8, 3f, new Color32(0xE6, 0x9A, 0x00, 255));

            // a star on the cup
            Star(p, 64, 92, 17f, 7f, White);
            return p.ToSprite();
        });
    }

    public static Sprite StarIcon()
    {
        return Make("star", () =>
        {
            Painter p = new Painter(128, 128);
            Star(p, 64, 66, 58f, 24f, Ink);
            Star(p, 64, 66, 48f, 20f, new Color32(0xFF, 0xD5, 0x4F, 255));
            return p.ToSprite();
        });
    }

    private static void Star(Painter p, float cx, float cy, float outer, float inner, Color color)
    {
        Vector2[] points = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? outer : inner;
            points[i] = new Vector2(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius);
        }

        p.Polygon(points, color);
    }

    // ---- Plain white shapes for the tablet (tinted by the layout's text colour) ---------------------------------------

    public static Sprite InfoIcon()
    {
        return Make("info", () =>
        {
            Painter p = new Painter(128, 128);
            p.Ring(64, 64, 50f, 11f, Color.white);
            p.Circle(64, 92, 8f, Color.white);
            p.Line(new Vector2(64, 74), new Vector2(64, 40), 13f, Color.white);
            return p.ToSprite();
        });
    }

    public static Sprite HouseIcon()
    {
        return Make("house", () =>
        {
            Painter p = new Painter(128, 128);
            p.Polygon(new[] { new Vector2(64, 116), new Vector2(6, 62), new Vector2(122, 62) }, Color.white);
            p.Box(64, 40, 38, 26, 3f, Color.white);
            return p.ToSprite();
        });
    }

    public static Sprite ListIcon()
    {
        return Make("list", () =>
        {
            Painter p = new Painter(128, 128);
            for (int row = 0; row < 3; row++)
            {
                float y = 100 - row * 36;
                p.Circle(20, y, 9f, Color.white);
                p.Box(78, y, 44, 8, 4f, Color.white);
            }

            return p.ToSprite();
        });
    }

    // ---- The main menu's gold-and-navy pictures ---------------------------------------------------------------------------------

    private static readonly Color MenuGold = new Color32(0xF0, 0xCF, 0x8A, 255);
    private static readonly Color MenuGoldDark = new Color32(0xC9, 0x9A, 0x4B, 255);
    private static readonly Color MenuNavy = new Color32(0x0E, 0x2A, 0x57, 255);
    private static readonly Color MenuSky = new Color32(0x8C, 0xC8, 0xFF, 255);

    // A rounded box that stretches (nine-sliced): filled, or just its outline. Tint it with the Image colour.
    public static Sprite MenuPanel(bool outline)
    {
        return Make(outline ? "menuPanelOutline" : "menuPanelFill", () =>
        {
            const int size = 64;
            const float radius = 16f;
            const float thickness = 2.4f;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float qy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float d = Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;   // negative inside

                    float cover = outline ? Mathf.Clamp01(0.5f - (Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f)) : Mathf.Clamp01(0.5f - d);

                    // The fill is lighter at the top and darker at the bottom (a soft vertical gradient); tint it with the Image colour.
                    float shade = outline ? 1f : Mathf.Lerp(0.62f, 1f, (y + 0.5f) / size);
                    pixels[y * size + x] = new Color(shade, shade, shade, cover);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        });
    }

    // A box with cut (chamfered) corners, like the panels of the menu design: the cut sizes are in canvas units for the top left, top right, bottom left
    // and bottom right corners (0 = square corner). Stretchable (nine-sliced). Filled with a soft vertical gradient, or just the outline.
    public static Sprite MenuChamfer(float topLeft, float topRight, float bottomLeft, float bottomRight, bool outline)
    {
        string key = "chamfer_" + topLeft + "_" + topRight + "_" + bottomLeft + "_" + bottomRight + "_" + outline;

        return Make(key, () =>
        {
            const int size = 96;
            const float thickness = 2.2f;
            float border = Mathf.Max(Mathf.Max(topLeft, topRight), Mathf.Max(bottomLeft, bottomRight)) + 3f;

            // The outline of the shape, going round (y is up).
            List<Vector2> points = new List<Vector2>();
            if (bottomLeft > 0f) { points.Add(new Vector2(0f, bottomLeft)); points.Add(new Vector2(bottomLeft, 0f)); } else points.Add(new Vector2(0f, 0f));
            if (bottomRight > 0f) { points.Add(new Vector2(size - bottomRight, 0f)); points.Add(new Vector2(size, bottomRight)); } else points.Add(new Vector2(size, 0f));
            if (topRight > 0f) { points.Add(new Vector2(size, size - topRight)); points.Add(new Vector2(size - topRight, size)); } else points.Add(new Vector2(size, size));
            if (topLeft > 0f) { points.Add(new Vector2(topLeft, size)); points.Add(new Vector2(0f, size - topLeft)); } else points.Add(new Vector2(0f, size));

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = PolygonDistance(points, x + 0.5f, y + 0.5f);   // negative inside
                    float cover = outline ? Mathf.Clamp01(0.5f - (Mathf.Abs(d + thickness * 0.5f) - thickness * 0.5f)) : Mathf.Clamp01(0.5f - d);
                    float shade = outline ? 1f : Mathf.Lerp(0.62f, 1f, (y + 0.5f) / size);
                    pixels[y * size + x] = new Color(shade, shade, shade, cover);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        });
    }

    // Signed distance to a polygon: negative inside, positive outside.
    private static float PolygonDistance(List<Vector2> points, float x, float y)
    {
        Vector2 p = new Vector2(x, y);
        float nearest = float.MaxValue;
        bool inside = false;

        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            nearest = Mathf.Min(nearest, SegmentDistance(p, points[j], points[i]));

            if ((points[i].y > y) != (points[j].y > y) &&
                x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                inside = !inside;
        }

        return inside ? -nearest : nearest;
    }

    // The game's emblem: a gold hexagon with a navy shield.
    public static Sprite MenuLogo()
    {
        return Make("menuLogo", () =>
        {
            Painter p = new Painter(128, 128);
            Vector2[] hex = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (60f * i + 30f) * Mathf.Deg2Rad;
                hex[i] = new Vector2(64f + Mathf.Cos(a) * 60f, 64f + Mathf.Sin(a) * 60f);
            }

            p.Polygon(hex, MenuGoldDark, 2f);
            p.Polygon(hex, MenuGold);

            Vector2[] shield = { new Vector2(64, 106), new Vector2(98, 94), new Vector2(98, 64), new Vector2(64, 24), new Vector2(30, 64), new Vector2(30, 94) };
            Vector2[] lightHalf = { new Vector2(64, 106), new Vector2(64, 24), new Vector2(30, 64), new Vector2(30, 94) };
            p.Polygon(shield, MenuNavy);
            p.Polygon(lightHalf, new Color32(0x2A, 0x5E, 0xA8, 255));
            Vector2[] inner = { new Vector2(64, 92), new Vector2(86, 84), new Vector2(86, 66), new Vector2(64, 40), new Vector2(42, 66), new Vector2(42, 84) };
            p.Polygon(inner, new Color32(0x14, 0x3A, 0x72, 255));
            p.Line(new Vector2(52, 66), new Vector2(61, 56), 6f, MenuGold);
            p.Line(new Vector2(61, 56), new Vector2(78, 76), 6f, MenuGold);
            return p.ToSprite();
        });
    }

    // MISSIONS: a clipboard with a map and a pin.
    public static Sprite MenuMissionIcon()
    {
        return Make("menuMission", () =>
        {
            Painter p = new Painter(128, 128);
            p.Box(64, 62, 40, 52, 8f, MenuGold);
            p.Box(64, 108, 18, 8, 4f, MenuGoldDark);
            p.Box(64, 58, 32, 42, 4f, MenuNavy);

            // folded map
            Vector2[] map = { new Vector2(40, 40), new Vector2(54, 48), new Vector2(66, 40), new Vector2(88, 52), new Vector2(88, 84), new Vector2(66, 72), new Vector2(54, 82), new Vector2(40, 74) };
            p.Polygon(map, new Color32(0x1B, 0x4A, 0x8C, 255));
            p.Line(new Vector2(54, 48), new Vector2(54, 82), 2.5f, MenuSky);
            p.Line(new Vector2(66, 40), new Vector2(66, 72), 2.5f, MenuSky);

            // pin
            p.Circle(70, 72, 11f, MenuGold);
            p.Polygon(new[] { new Vector2(62, 66), new Vector2(78, 66), new Vector2(70, 48) }, MenuGold);
            p.Circle(70, 72, 4.5f, MenuNavy);
            return p.ToSprite();
        });
    }

    // TRIVIA: a brain.
    public static Sprite MenuBrainIcon()
    {
        return Make("menuBrain", () =>
        {
            Painter p = new Painter(128, 128);
            Color lobe = MenuSky;
            Color shade = new Color32(0x5D, 0xA8, 0xF0, 255);

            foreach (float side in new[] { -1f, 1f })
            {
                float cx = 64f + side * 22f;
                p.Circle(cx, 78, 27f, lobe);
                p.Circle(64f + side * 30f, 58, 22f, lobe);
                p.Circle(64f + side * 18f, 40, 22f, lobe);
                p.Circle(64f + side * 26f, 74, 9f, shade);
                p.Circle(64f + side * 16f, 50, 7f, shade);
            }

            p.Box(64, 62, 3.5f, 40f, 2f, MenuNavy);
            p.Box(64, 18, 9, 10, 4f, MenuGold);
            p.Circle(40, 88, 5f, MenuGold);
            p.Circle(88, 88, 5f, MenuGold);
            return p.ToSprite();
        });
    }

    // PROFILE: a person inside a gold ring.
    public static Sprite MenuPersonIcon()
    {
        return Make("menuPerson", () =>
        {
            Painter p = new Painter(128, 128);
            p.Ring(64, 64, 52f, 9f, MenuGold);
            p.Circle(64, 80, 17f, MenuGold);
            p.Ellipse(64, 38, 32f, 22f, MenuGold);
            return p.ToSprite();
        });
    }

    // SETTINGS: a gold gear.
    public static Sprite MenuGearIcon()
    {
        return Make("menuGear", () =>
        {
            Painter p = new Painter(128, 128);

            for (int i = 0; i < 8; i++)
            {
                float a = 45f * i * Mathf.Deg2Rad;
                Vector2 along = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 across = new Vector2(-along.y, along.x);
                Vector2 centre = new Vector2(64f, 64f);
                Vector2[] tooth =
                {
                    centre + along * 30f + across * 10f, centre + along * 54f + across * 8f,
                    centre + along * 54f - across * 8f, centre + along * 30f - across * 10f
                };
                p.Polygon(tooth, MenuGold);
            }

            p.Circle(64, 64, 40f, MenuGold);
            p.Circle(64, 64, 17f, MenuNavy);
            return p.ToSprite();
        });
    }

    // EXIT GAME: a doorway with an arrow going out.
    public static Sprite MenuExitIcon()
    {
        return Make("menuExit", () =>
        {
            Painter p = new Painter(128, 128);
            Color c = MenuGold;
            p.Line(new Vector2(36, 104), new Vector2(78, 104), 9f, c);
            p.Line(new Vector2(36, 104), new Vector2(36, 24), 9f, c);
            p.Line(new Vector2(36, 24), new Vector2(78, 24), 9f, c);
            p.Line(new Vector2(58, 64), new Vector2(108, 64), 9f, c);
            p.Line(new Vector2(108, 64), new Vector2(88, 84), 9f, c);
            p.Line(new Vector2(108, 64), new Vector2(88, 44), 9f, c);
            return p.ToSprite();
        });
    }

    // NEW GAME: a play triangle.
    public static Sprite MenuPlayIcon()
    {
        return Make("menuPlay", () =>
        {
            Painter p = new Painter(128, 128);
            p.Polygon(new[] { new Vector2(30, 18), new Vector2(30, 110), new Vector2(108, 64) }, MenuNavy);
            return p.ToSprite();
        });
    }

    // ---- Backgrounds -----------------------------------------------------------------------------------------------------

    // The background of a mission card: a coloured gradient with rounded corners and, faded in the corner, the threat's own picture.
    public static Sprite ThreatCard(string threat)
    {
        string key = "card_" + (threat ?? "").ToLowerInvariant();

        return Make(key, () =>
        {
            Color top, bottom, edge;
            switch ((threat ?? "").Trim().ToLowerInvariant())
            {
                case "phishing email": top = Hex("#1E88E5"); bottom = Hex("#0B2545"); edge = Hex("#64B5F6"); break;
                case "malware files": top = Hex("#D32F2F"); bottom = Hex("#2B0B10"); edge = Hex("#FF8A80"); break;
                case "fake pop-up windows": top = Hex("#F9A825"); bottom = Hex("#3B2905"); edge = Hex("#FFE082"); break;
                case "spam email": top = Hex("#8E24AA"); bottom = Hex("#25102C"); edge = Hex("#E1BEE7"); break;
                case "social engineering": top = Hex("#00897B"); bottom = Hex("#06302C"); edge = Hex("#80CBC4"); break;
                case "unauthorized access": top = Hex("#3949AB"); bottom = Hex("#101A3D"); edge = Hex("#9FA8DA"); break;
                default: top = Hex("#1E88E5"); bottom = Hex("#0B2545"); edge = Hex("#64B5F6"); break;
            }

            const int width = 128, height = 176;
            Painter p = new Painter(width, height);

            // outline, then the gradient inside it (a stack of thin bands)
            p.Box(width / 2f, height / 2f, width / 2f - 1f, height / 2f - 1f, 14f, edge);
            for (int band = 0; band < height - 4; band += 2)
            {
                float t = band / (float)(height - 4);
                Color c = Color.Lerp(bottom, top, t);
                float y = 2 + band + 1f;
                p.Fill((x, yy) =>
                {
                    float inRow = Mathf.Abs(yy - y) - 1.05f;
                    float qx = Mathf.Abs(x - width / 2f) - (width / 2f - 3f) + 12f;
                    float qy = Mathf.Abs(yy - height / 2f) - (height / 2f - 3f) + 12f;
                    float rounded = Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - 12f;
                    return Mathf.Max(inRow, rounded);
                }, c);
            }

            // a few soft sparkles
            Color sparkle = new Color(1f, 1f, 1f, 0.14f);
            p.Circle(24, 150, 3f, sparkle);
            p.Circle(104, 130, 2f, sparkle);
            p.Circle(92, 160, 2.5f, sparkle);
            p.Circle(16, 100, 2f, sparkle);
            return p.ToSprite();
        });
    }

    // A soft glowing disc behind an icon.
    public static Sprite Glow()
    {
        return Make("glow", () =>
        {
            Painter p = new Painter(128, 128);
            for (int ring = 0; ring < 16; ring++)
            {
                float radius = 62f - ring * 3.8f;
                p.Circle(64, 64, radius, new Color(1f, 1f, 1f, 0.07f));
            }

            return p.ToSprite();
        });
    }

    private static Color Hex(string hex)
    {
        return UiKit.Hex(hex);
    }
}

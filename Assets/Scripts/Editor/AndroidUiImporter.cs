using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

// Tools > Firewall Duty > Menu UI > Import Android Assets
// Brings the look of the Android Studio app into Unity, exactly, by converting its resources instead of redrawing them:
//   - drawable PNG/JPG pictures are copied in as sprites;
//   - XML shape drawables (rounded panels, buttons, gradients, strokes, selectors) become 9-sliced sprites;
//   - XML vector icons (eye, malware, phishing ...) are drawn into sprites;
//   - google-services.json is read for the Firebase project id and web API key (stored as a small json in Resources).
// Everything goes to Assets/Resources/MenuUI/{Images,Shapes,Icons}. Safe to run again: files are rewritten.
// Sizes: 1 dp in the Android layouts = 1 unit of the menu canvas (reference 800 x 360), and sprites are made at 4 pixels per dp.
public static class AndroidUiImporter
{
    private const string DefaultAndroidProject = @"C:\Users\User\AndroidStudioProjects\Firewall_3D\firewall3d";
    private const string PrefsKey = "FirewallDuty.AndroidProjectPath";
    private const string OutRoot = "Assets/Resources/MenuUI";
    public const int PixelsPerDp = 4;

    [MenuItem("Tools/Firewall Duty/Menu UI/Import Android Assets")]
    public static void Import()
    {
        StringBuilder report = new StringBuilder();
        string project = EditorPrefs.GetString(PrefsKey, DefaultAndroidProject);
        string res = Path.Combine(project, "app", "src", "main", "res");

        if (!Directory.Exists(res))
        {
            UnityEngine.Debug.LogError("Android resources not found at: " + res +
                                       "\nSet the project folder with EditorPrefs key '" + PrefsKey + "' or edit DefaultAndroidProject in AndroidUiImporter.cs.");
            return;
        }

        Directory.CreateDirectory(Path.Combine(OutRoot, "Images"));
        Directory.CreateDirectory(Path.Combine(OutRoot, "Shapes"));
        Directory.CreateDirectory(Path.Combine(OutRoot, "Icons"));

        Dictionary<string, Color> colors = ReadColors(Path.Combine(res, "values", "colors.xml"));
        string drawable = Path.Combine(res, "drawable");

        List<string> images = new List<string>();
        List<(string path, Vector4 border, bool sliced)> shapeSprites = new List<(string, Vector4, bool)>();
        List<string> iconSprites = new List<string>();
        int skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string file in Directory.GetFiles(drawable))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string name = Path.GetFileNameWithoutExtension(file);

                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    string dest = Path.Combine(OutRoot, "Images", name + ext);

                    // Android reads WebP pictures even when they are named .png; Unity cannot. Keep the PNG that was converted
                    // from it (never overwrite it with the WebP).
                    if (IsWebP(file))
                    {
                        report.AppendLine("  " + name + ext + " is a WebP picture: keeping the converted PNG already in Unity.");
                        if (File.Exists(dest))
                            images.Add(dest.Replace('\\', '/'));
                        continue;
                    }

                    File.Copy(file, dest, true);
                    images.Add(dest.Replace('\\', '/'));
                    continue;
                }

                if (ext != ".xml")
                    continue;

                XDocument doc;
                try { doc = XDocument.Load(file); }
                catch (Exception e) { report.AppendLine("  skipped " + name + ": " + e.Message); skipped++; continue; }

                string root = doc.Root != null ? doc.Root.Name.LocalName : "";

                if (root == "shape" || root == "selector")
                {
                    ConvertShapeFile(doc.Root, name, colors, shapeSprites);
                }
                else if (root == "vector")
                {
                    if (name.StartsWith("ic_launcher"))
                        continue;

                    string dest = Path.Combine(OutRoot, "Icons", name + ".png").Replace('\\', '/');
                    if (RenderVector(doc.Root, colors, dest))
                        iconSprites.Add(dest);
                    else
                    {
                        report.AppendLine("  skipped icon " + name + " (unsupported path data)");
                        skipped++;
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (string path in images)
            ConfigureSprite(path, 100f, Vector4.zero);

        foreach (var s in shapeSprites)
            ConfigureSprite(s.path, 100f * PixelsPerDp, s.border);

        foreach (string path in iconSprites)
            ConfigureSprite(path, 100f * PixelsPerDp / 2f, Vector4.zero);

        report.Insert(0, "Menu UI import: " + images.Count + " picture(s), " + shapeSprites.Count + " shape sprite(s), " + iconSprites.Count + " icon(s)" +
                         (skipped > 0 ? ", " + skipped + " skipped" : "") + ".\n");

        report.AppendLine(CopyLayouts(project));
        report.AppendLine(WriteFirebaseConfig(project));
        report.AppendLine("Next: Tools > Firewall Duty > Menu UI > Create Menu Scene.");
        UnityEngine.Debug.Log(report.ToString());
    }

    // ---- Colours ---------------------------------------------------------------------------------------------

    private static Dictionary<string, Color> ReadColors(string path)
    {
        Dictionary<string, Color> colors = new Dictionary<string, Color>
        {
            { "white", Color.white }, { "black", Color.black }, { "transparent", new Color(0, 0, 0, 0) },
            { "holo_blue_dark", new Color(0f, 0.6f, 0.8f) }, { "holo_blue_light", new Color(0.2f, 0.71f, 0.9f) }
        };

        if (!File.Exists(path))
            return colors;

        foreach (XElement e in XDocument.Load(path).Descendants("color"))
        {
            string name = (string)e.Attribute("name");
            if (name != null && TryParseHex(e.Value.Trim(), out Color c))
                colors[name] = c;
        }

        return colors;
    }

    private static bool TryParseHex(string text, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrEmpty(text) || text[0] != '#')
            return false;

        string h = text.Substring(1);
        if (h.Length == 3) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
        if (h.Length == 4) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2] + h[3] + h[3];

        try
        {
            if (h.Length == 6)
            {
                color = new Color(Convert.ToInt32(h.Substring(0, 2), 16) / 255f, Convert.ToInt32(h.Substring(2, 2), 16) / 255f,
                                  Convert.ToInt32(h.Substring(4, 2), 16) / 255f, 1f);
                return true;
            }

            if (h.Length == 8)
            {
                color = new Color(Convert.ToInt32(h.Substring(2, 2), 16) / 255f, Convert.ToInt32(h.Substring(4, 2), 16) / 255f,
                                  Convert.ToInt32(h.Substring(6, 2), 16) / 255f, Convert.ToInt32(h.Substring(0, 2), 16) / 255f);
                return true;
            }
        }
        catch (Exception) { }

        return false;
    }

    private static bool ResolveColor(string text, Dictionary<string, Color> colors, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrEmpty(text))
            return false;

        text = text.Trim();
        if (text.StartsWith("#"))
            return TryParseHex(text, out color);

        int slash = text.LastIndexOf('/');
        if (text.StartsWith("@") && slash >= 0)
            return colors.TryGetValue(text.Substring(slash + 1), out color);

        return false;
    }

    private static float Dp(string text, float fallback = 0f)
    {
        if (string.IsNullOrEmpty(text))
            return fallback;

        text = text.Trim().Replace("dp", "").Replace("dip", "").Replace("sp", "").Replace("px", "");
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
    }

    // ---- Shapes ----------------------------------------------------------------------------------------------

    private class ShapeSpec
    {
        public bool oval;
        public bool hasSolid;
        public Color solid;
        public bool hasGradient;
        public Color gradStart, gradEnd, gradCenter;
        public bool hasCenter;
        public float angle;
        public float rTL, rTR, rBL, rBR;
        public float strokeWidth;
        public Color strokeColor = Color.black;
    }

    private static void ConvertShapeFile(XElement root, string name, Dictionary<string, Color> colors, List<(string, Vector4, bool)> output)
    {
        if (root.Name.LocalName == "shape")
        {
            Output(ParseShape(root, colors), name, output);
            return;
        }

        // selector: the item with no state is the normal look; an item with a checked/selected state is the other one.
        XElement normal = null, selected = null;
        foreach (XElement item in root.Elements("item"))
        {
            XElement shape = item.Element("shape");
            if (shape == null)
                continue;

            bool hasState = false;
            foreach (XAttribute a in item.Attributes())
            {
                if (a.Name.LocalName.StartsWith("state_"))
                {
                    hasState = true;
                    if ((a.Name.LocalName == "state_checked" || a.Name.LocalName == "state_selected") && a.Value == "true")
                        selected = selected ?? shape;
                }
            }

            if (!hasState)
                normal = normal ?? shape;
        }

        if (normal != null) Output(ParseShape(normal, colors), name, output);
        if (selected != null) Output(ParseShape(selected, colors), name + "_checked", output);
    }

    private static ShapeSpec ParseShape(XElement shape, Dictionary<string, Color> colors)
    {
        ShapeSpec s = new ShapeSpec();
        s.oval = ((string)shape.Attribute(XName.Get("shape", "http://schemas.android.com/apk/res/android")) ?? "rectangle") == "oval";

        XNamespace a = "http://schemas.android.com/apk/res/android";

        XElement solid = shape.Element("solid");
        if (solid != null && ResolveColor((string)solid.Attribute(a + "color"), colors, out Color sc))
        {
            s.hasSolid = true;
            s.solid = sc;
        }

        XElement grad = shape.Element("gradient");
        if (grad != null)
        {
            ResolveColor((string)grad.Attribute(a + "startColor"), colors, out s.gradStart);
            ResolveColor((string)grad.Attribute(a + "endColor"), colors, out s.gradEnd);
            s.hasCenter = ResolveColor((string)grad.Attribute(a + "centerColor"), colors, out s.gradCenter);
            s.angle = Dp((string)grad.Attribute(a + "angle"), 0f);
            s.hasGradient = true;
        }

        XElement corners = shape.Element("corners");
        if (corners != null)
        {
            float all = Dp((string)corners.Attribute(a + "radius"));
            s.rTL = Dp((string)corners.Attribute(a + "topLeftRadius"), all);
            s.rTR = Dp((string)corners.Attribute(a + "topRightRadius"), all);
            s.rBL = Dp((string)corners.Attribute(a + "bottomLeftRadius"), all);
            s.rBR = Dp((string)corners.Attribute(a + "bottomRightRadius"), all);
        }

        XElement stroke = shape.Element("stroke");
        if (stroke != null)
        {
            s.strokeWidth = Dp((string)stroke.Attribute(a + "width"));
            ResolveColor((string)stroke.Attribute(a + "color"), colors, out s.strokeColor);
        }

        return s;
    }

    private static void Output(ShapeSpec spec, string name, List<(string, Vector4, bool)> output)
    {
        Texture2D tex = RenderShape(spec, out Vector4 border);
        string path = Path.Combine(OutRoot, "Shapes", name + ".png").Replace('\\', '/');
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        output.Add((path, border, border != Vector4.zero));
    }

    private static Texture2D RenderShape(ShapeSpec s, out Vector4 border)
    {
        int S = PixelsPerDp;
        float strokePx = s.strokeWidth * S;
        int size;
        float rTL, rTR, rBL, rBR;

        if (s.oval)
        {
            size = 256;
            border = Vector4.zero;
            rTL = rTR = rBL = rBR = size * 0.5f;
        }
        else
        {
            float rmax = Mathf.Max(Mathf.Max(s.rTL, s.rTR), Mathf.Max(s.rBL, s.rBR)) * S;

            if (rmax <= 0f && strokePx <= 0f)
            {
                // A plain rectangle or full-area gradient (such as a screen background): not sliced, so a gradient stays smooth.
                size = 64;
                border = Vector4.zero;
                rTL = rTR = rBL = rBR = 0f;
            }
            else
            {
                int b = Mathf.CeilToInt(Mathf.Max(rmax, strokePx + 1f));
                if (b < 2) b = 2;
                size = 2 * b + 2;
                border = new Vector4(b, b, b, b);
                rTL = s.rTL * S; rTR = s.rTR * S; rBL = s.rBL * S; rBR = s.rBR * S;
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;

                // Signed distance (pixels, negative inside) to the rounded box; corner radius depends on the quadrant.
                float r = fx < half ? (fy < half ? rBL : rTL) : (fy < half ? rBR : rTR);
                r = Mathf.Min(r, half);
                float qx = Mathf.Abs(fx - half) - (half - r);
                float qy = Mathf.Abs(fy - half) - (half - r);
                float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;

                float coverage = Mathf.Clamp01(0.5f - d);

                Color fill = new Color(0, 0, 0, 0);
                if (s.hasGradient)
                    fill = GradientColor(s, fx / size, fy / size);
                else if (s.hasSolid)
                    fill = s.solid;

                Color c = fill;
                if (strokePx > 0f)
                {
                    float stroke = Mathf.Clamp01(d + strokePx + 0.5f);   // 1 inside the stroke band, 0 further in
                    c = Color.Lerp(fill, s.strokeColor, stroke);
                    c.a = Mathf.Lerp(fill.a, s.strokeColor.a, stroke);
                }

                c.a *= coverage;
                px[y * size + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Color GradientColor(ShapeSpec s, float u, float v)
    {
        float rad = s.angle * Mathf.Deg2Rad;
        float t = Mathf.Clamp01((u - 0.5f) * Mathf.Cos(rad) + (v - 0.5f) * Mathf.Sin(rad) + 0.5f);

        if (!s.hasCenter)
            return Color.Lerp(s.gradStart, s.gradEnd, t);

        return t < 0.5f ? Color.Lerp(s.gradStart, s.gradCenter, t * 2f) : Color.Lerp(s.gradCenter, s.gradEnd, (t - 0.5f) * 2f);
    }

    // ---- Vector icons ----------------------------------------------------------------------------------------

    private static bool RenderVector(XElement root, Dictionary<string, Color> colors, string destPath)
    {
        XNamespace a = "http://schemas.android.com/apk/res/android";

        float vw = Dp((string)root.Attribute(a + "viewportWidth"), 24f);
        float vh = Dp((string)root.Attribute(a + "viewportHeight"), 24f);

        int size = 128;
        float scale = size / Mathf.Max(vw, vh);
        int w = Mathf.Max(8, Mathf.RoundToInt(vw * scale));
        int h = Mathf.Max(8, Mathf.RoundToInt(vh * scale));

        Color[] px = new Color[w * h];

        foreach (XElement path in root.Descendants("path"))
        {
            string data = (string)path.Attribute(a + "pathData");
            if (string.IsNullOrEmpty(data))
                continue;

            List<List<Vector2>> polys = ParsePath(data);
            if (polys == null)
                return false;

            bool hasFill = ResolveColor((string)path.Attribute(a + "fillColor"), colors, out Color fill);
            float fillAlpha = Dp((string)path.Attribute(a + "fillAlpha"), 1f);
            bool evenOdd = ((string)path.Attribute(a + "fillType") ?? "nonZero").ToLowerInvariant() == "evenodd";

            bool hasStroke = ResolveColor((string)path.Attribute(a + "strokeColor"), colors, out Color strokeColor);
            float strokeWidth = Dp((string)path.Attribute(a + "strokeWidth"), 0f);

            const int ss = 4;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int inside = 0;
                    int onStroke = 0;

                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            // Path data has y pointing down from the top; texture rows count up from the bottom, so use the row from the top.
                            Vector2 p = new Vector2((x + (sx + 0.5f) / ss) / scale, (h - 1 - y + (sy + 0.5f) / ss) / scale);

                            if (hasFill && Inside(polys, p, evenOdd))
                                inside++;

                            if (hasStroke && strokeWidth > 0f && NearEdge(polys, p, strokeWidth * 0.5f))
                                onStroke++;
                        }
                    }

                    int i = y * w + x;

                    if (inside > 0)
                    {
                        Color c = fill;
                        c.a *= fillAlpha * inside / (ss * ss);
                        px[i] = Over(px[i], c);
                    }

                    if (onStroke > 0)
                    {
                        Color c = strokeColor;
                        c.a *= onStroke / (float)(ss * ss);
                        px[i] = Over(px[i], c);
                    }
                }
            }
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(destPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        return true;
    }

    private static Color Over(Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0f)
            return new Color(0, 0, 0, 0);

        Color c = (src * src.a + dst * dst.a * (1f - src.a)) / a;
        c.a = a;
        return c;
    }

    private static bool Inside(List<List<Vector2>> polys, Vector2 p, bool evenOdd)
    {
        int winding = 0;
        int crossings = 0;

        foreach (List<Vector2> poly in polys)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];

                if ((a.y <= p.y) != (b.y <= p.y))
                {
                    float x = a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x);
                    if (x > p.x)
                    {
                        crossings++;
                        winding += b.y > a.y ? 1 : -1;
                    }
                }
            }
        }

        return evenOdd ? (crossings & 1) == 1 : winding != 0;
    }

    private static bool NearEdge(List<List<Vector2>> polys, Vector2 p, float radius)
    {
        float r2 = radius * radius;

        foreach (List<Vector2> poly in polys)
        {
            for (int i = 0; i + 1 < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[i + 1];
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                if ((a + ab * t - p).sqrMagnitude <= r2)
                    return true;
            }
        }

        return false;
    }

    // SVG path data to polylines (curves are flattened). Supports M L H V C S Q T A Z in both cases.
    private static List<List<Vector2>> ParsePath(string d)
    {
        List<List<Vector2>> result = new List<List<Vector2>>();
        List<Vector2> current = null;
        Vector2 pos = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero;
        char cmd = ' ', prev = ' ';
        int i = 0;

        bool SkipSep()
        {
            while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ','))
                i++;
            return i < d.Length;
        }

        bool Num(out float v)
        {
            v = 0f;
            SkipSep();
            int s = i;
            if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
            bool dot = false;
            while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot)))
            {
                if (d[i] == '.') dot = true;
                i++;
            }

            if (i < d.Length && (d[i] == 'e' || d[i] == 'E') && i + 1 < d.Length && (char.IsDigit(d[i + 1]) || d[i + 1] == '-' || d[i + 1] == '+'))
            {
                i += 2;
                while (i < d.Length && char.IsDigit(d[i])) i++;
            }

            return i > s && float.TryParse(d.Substring(s, i - s), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        bool Flag(out bool v)
        {
            v = false;
            SkipSep();
            if (i >= d.Length || (d[i] != '0' && d[i] != '1'))
                return false;

            v = d[i] == '1';
            i++;
            return true;
        }

        void MoveTo(Vector2 p)
        {
            current = new List<Vector2> { p };
            result.Add(current);
            pos = start = p;
        }

        void LineTo(Vector2 p)
        {
            if (current == null) MoveTo(pos);
            current.Add(p);
            pos = p;
        }

        void Cubic(Vector2 c1, Vector2 c2, Vector2 p)
        {
            if (current == null) MoveTo(pos);
            Vector2 p0 = pos;
            for (int k = 1; k <= 20; k++)
            {
                float t = k / 20f, u = 1f - t;
                current.Add(u * u * u * p0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p);
            }

            pos = p;
            lastCtrl = c2;
        }

        while (SkipSep())
        {
            char ch = d[i];
            if (char.IsLetter(ch))
            {
                cmd = ch;
                i++;
            }
            else if (cmd == ' ')
            {
                return null;
            }
            else if (cmd == 'M') cmd = 'L';
            else if (cmd == 'm') cmd = 'l';

            bool rel = char.IsLower(cmd);
            float x, y, x1, y1, x2, y2;
            Vector2 o = rel ? pos : Vector2.zero;

            switch (char.ToUpperInvariant(cmd))
            {
                case 'M':
                    if (!Num(out x) || !Num(out y)) return null;
                    MoveTo(new Vector2(x, y) + o);
                    break;
                case 'L':
                    if (!Num(out x) || !Num(out y)) return null;
                    LineTo(new Vector2(x, y) + o);
                    break;
                case 'H':
                    if (!Num(out x)) return null;
                    LineTo(new Vector2(rel ? pos.x + x : x, pos.y));
                    break;
                case 'V':
                    if (!Num(out y)) return null;
                    LineTo(new Vector2(pos.x, rel ? pos.y + y : y));
                    break;
                case 'C':
                    if (!Num(out x1) || !Num(out y1) || !Num(out x2) || !Num(out y2) || !Num(out x) || !Num(out y)) return null;
                    Cubic(new Vector2(x1, y1) + o, new Vector2(x2, y2) + o, new Vector2(x, y) + o);
                    break;
                case 'S':
                    if (!Num(out x2) || !Num(out y2) || !Num(out x) || !Num(out y)) return null;
                    {
                        char up = char.ToUpperInvariant(prev);
                        Vector2 c1 = (up == 'C' || up == 'S') ? 2f * pos - lastCtrl : pos;
                        Cubic(c1, new Vector2(x2, y2) + o, new Vector2(x, y) + o);
                    }
                    break;
                case 'Q':
                    if (!Num(out x1) || !Num(out y1) || !Num(out x) || !Num(out y)) return null;
                    {
                        Vector2 p0 = pos, q = new Vector2(x1, y1) + o, p = new Vector2(x, y) + o;
                        Cubic(p0 + 2f / 3f * (q - p0), p + 2f / 3f * (q - p), p);
                        lastCtrl = q;
                    }
                    break;
                case 'T':
                    if (!Num(out x) || !Num(out y)) return null;
                    {
                        char up = char.ToUpperInvariant(prev);
                        Vector2 q = (up == 'Q' || up == 'T') ? 2f * pos - lastCtrl : pos;
                        Vector2 p0 = pos, p = new Vector2(x, y) + o;
                        Cubic(p0 + 2f / 3f * (q - p0), p + 2f / 3f * (q - p), p);
                        lastCtrl = q;
                    }
                    break;
                case 'A':
                    {
                        if (!Num(out float rx) || !Num(out float ry) || !Num(out float rot)) return null;
                        if (!Flag(out bool large) || !Flag(out bool sweep)) return null;
                        if (!Num(out x) || !Num(out y)) return null;
                        if (current == null) MoveTo(pos);
                        Arc(current, pos, new Vector2(x, y) + o, rx, ry, rot, large, sweep);
                        pos = new Vector2(x, y) + o;
                    }
                    break;
                case 'Z':
                    pos = start;
                    current = null;
                    break;
                default:
                    return null;
            }

            prev = cmd;
        }

        return result;
    }

    // SVG elliptical arc (implementation notes F.6.5), flattened into line segments.
    private static void Arc(List<Vector2> output, Vector2 p0, Vector2 p1, float rx, float ry, float rotation, bool large, bool sweep)
    {
        if (p0 == p1)
            return;

        rx = Mathf.Abs(rx);
        ry = Mathf.Abs(ry);
        if (rx < 1e-6f || ry < 1e-6f)
        {
            output.Add(p1);
            return;
        }

        float phi = rotation * Mathf.Deg2Rad;
        float cosPhi = Mathf.Cos(phi), sinPhi = Mathf.Sin(phi);
        Vector2 dp = (p0 - p1) * 0.5f;
        float x1 = cosPhi * dp.x + sinPhi * dp.y;
        float y1 = -sinPhi * dp.x + cosPhi * dp.y;

        float lambda = (x1 * x1) / (rx * rx) + (y1 * y1) / (ry * ry);
        if (lambda > 1f)
        {
            float s = Mathf.Sqrt(lambda);
            rx *= s;
            ry *= s;
        }

        float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        float coef = Mathf.Sqrt(Mathf.Max(0f, num / den)) * (large == sweep ? -1f : 1f);
        float cxp = coef * rx * y1 / ry;
        float cyp = -coef * ry * x1 / rx;

        Vector2 center = new Vector2(cosPhi * cxp - sinPhi * cyp + (p0.x + p1.x) * 0.5f, sinPhi * cxp + cosPhi * cyp + (p0.y + p1.y) * 0.5f);

        float Angle(float ux, float uy, float vx, float vy)
        {
            float a = Mathf.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            return a;
        }

        float theta1 = Mathf.Atan2((y1 - cyp) / ry, (x1 - cxp) / rx);
        float delta = Angle((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);

        if (!sweep && delta > 0f) delta -= 2f * Mathf.PI;
        if (sweep && delta < 0f) delta += 2f * Mathf.PI;

        int steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI / 16f)));
        for (int k = 1; k <= steps; k++)
        {
            float t = theta1 + delta * k / steps;
            float ex = rx * Mathf.Cos(t), ey = ry * Mathf.Sin(t);
            output.Add(new Vector2(cosPhi * ex - sinPhi * ey + center.x, sinPhi * ex + cosPhi * ey + center.y));
        }
    }

    // ---- Sprite import settings ------------------------------------------------------------------------------

    private static void ConfigureSprite(string path, float pixelsPerUnit, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.spriteBorder = border;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = path.Contains("/Images/") ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    // ---- Firebase config -------------------------------------------------------------------------------------

    // The Cyber Tablet and its example screens are built from the Android layout files themselves (see AndroidLayout).
    private static readonly string[] TabletLayouts =
    {
        "activity_tablet", "activity_examples", "activity_malware", "activity_popup", "activity_spam", "activity_social", "activity_unauthorized", "activity_profile", "activity_settings"
    };

    private static bool IsWebP(string path)
    {
        byte[] head = new byte[12];
        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.Read(head, 0, 12) < 12)
                return false;
        }

        return head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P';
    }

    private static string CopyLayouts(string project)
    {
        string folder = OutRoot + "/Layouts";
        Directory.CreateDirectory(folder);
        int copied = 0;

        foreach (string name in TabletLayouts)
        {
            string source = Path.Combine(project, "app", "src", "main", "res", "layout", name + ".xml");
            if (!File.Exists(source))
                continue;

            File.Copy(source, folder + "/" + name + ".xml", true);
            copied++;
        }

        AssetDatabase.Refresh();
        return "Layouts: " + copied + " of " + TabletLayouts.Length + " tablet layouts copied.";
    }

    private static string WriteFirebaseConfig(string project)
    {
        string path = Path.Combine(project, "app", "google-services.json");
        if (!File.Exists(path))
            return "Firebase: google-services.json not found (" + path + "), sign-in will not work until it is available.";

        string json = File.ReadAllText(path);

        string projectId = Between(json, "\"project_id\"");
        string apiKey = Between(json, "\"current_key\"");

        if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(apiKey))
            return "Firebase: could not read project_id / api key from google-services.json.";

        // The web client id (client_type 3) is what Google sign-in asks the phone for.
        System.Text.RegularExpressions.Match web = System.Text.RegularExpressions.Regex.Match(
            json, "\"client_id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"client_type\"\\s*:\\s*3");
        string webClientId = web.Success ? web.Groups[1].Value : "";

        File.WriteAllText(Path.Combine(OutRoot, "firebase_config.json"),
                          "{\n  \"projectId\": \"" + projectId + "\",\n  \"apiKey\": \"" + apiKey + "\",\n  \"webClientId\": \"" + webClientId + "\"\n}\n");
        AssetDatabase.ImportAsset(OutRoot + "/firebase_config.json");

        return "Firebase: project '" + projectId + "' configured (key stored in Assets/Resources/MenuUI/firebase_config.json).";
    }

    private static string Between(string json, string key)
    {
        int k = json.IndexOf(key, StringComparison.Ordinal);
        if (k < 0) return null;

        int colon = json.IndexOf(':', k);
        int q1 = json.IndexOf('"', colon + 1);
        int q2 = json.IndexOf('"', q1 + 1);
        return q1 >= 0 && q2 > q1 ? json.Substring(q1 + 1, q2 - q1 - 1) : null;
    }
}

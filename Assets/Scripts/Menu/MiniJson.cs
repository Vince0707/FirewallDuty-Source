using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// A tiny JSON reader/writer (no packages needed). Parse gives: Dictionary<string, object>, List<object>, string, double, long, bool or null.
public static class MiniJson
{
    public static object Parse(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        int i = 0;
        try
        {
            return ReadValue(json, ref i);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static Dictionary<string, object> ParseObject(string json)
    {
        return Parse(json) as Dictionary<string, object>;
    }

    // Safe lookups, so callers never have to null-check each step.
    public static object Get(object node, params string[] path)
    {
        foreach (string key in path)
        {
            Dictionary<string, object> map = node as Dictionary<string, object>;
            if (map == null || !map.TryGetValue(key, out node))
                return null;
        }

        return node;
    }

    public static string GetString(object node, params string[] path)
    {
        object v = Get(node, path);
        return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    private static object ReadValue(string s, ref int i)
    {
        SkipSpace(s, ref i);
        char c = s[i];

        if (c == '{')
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            i++;
            SkipSpace(s, ref i);

            if (s[i] == '}')
            {
                i++;
                return map;
            }

            while (true)
            {
                SkipSpace(s, ref i);
                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                i++; // ':'
                map[key] = ReadValue(s, ref i);
                SkipSpace(s, ref i);

                if (s[i] == ',') { i++; continue; }
                i++; // '}'
                return map;
            }
        }

        if (c == '[')
        {
            List<object> list = new List<object>();
            i++;
            SkipSpace(s, ref i);

            if (s[i] == ']')
            {
                i++;
                return list;
            }

            while (true)
            {
                list.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);

                if (s[i] == ',') { i++; continue; }
                i++; // ']'
                return list;
            }
        }

        if (c == '"')
            return ReadString(s, ref i);

        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }

        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
            i++;

        string number = s.Substring(start, i - start);
        if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            return l;

        return double.Parse(number, CultureInfo.InvariantCulture);
    }

    private static string ReadString(string s, ref int i)
    {
        StringBuilder sb = new StringBuilder();
        i++; // opening quote

        while (s[i] != '"')
        {
            char c = s[i++];

            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                    i += 4;
                    break;
                default: sb.Append(e); break;
            }
        }

        i++; // closing quote
        return sb.ToString();
    }

    private static void SkipSpace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
            i++;
    }

    public static string Serialize(object value)
    {
        StringBuilder sb = new StringBuilder();
        Write(sb, value);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, object v)
    {
        if (v == null) { sb.Append("null"); return; }

        if (v is string str) { WriteString(sb, str); return; }
        if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
        if (v is int || v is long || v is short || v is byte) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
        if (v is float || v is double) { sb.Append(Convert.ToDouble(v).ToString("R", CultureInfo.InvariantCulture)); return; }

        if (v is IDictionary dict)
        {
            sb.Append('{');
            bool first = true;

            foreach (DictionaryEntry e in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(sb, Convert.ToString(e.Key));
                sb.Append(':');
                Write(sb, e.Value);
            }

            sb.Append('}');
            return;
        }

        if (v is IEnumerable list)
        {
            sb.Append('[');
            bool first = true;

            foreach (object item in list)
            {
                if (!first) sb.Append(',');
                first = false;
                Write(sb, item);
            }

            sb.Append(']');
            return;
        }

        WriteString(sb, v.ToString());
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');

        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }
}

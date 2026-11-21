// HP Revit MCP - bo doc/ghi JSON nho (khong phu thuoc thu vien ngoai, de khong xung dot voi add-in khac)
// Ngay viet: 2026-11-21
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HPRevitMcp
{
    internal static class Json
    {
        // ---------- Ghi ----------
        public static string Write(object v)
        {
            var sb = new StringBuilder();
            W(sb, v);
            return sb.ToString();
        }

        private static void W(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string s) { WS(sb, s); return; }
            if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (v is double d) { sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is float f) { sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is int || v is long || v is short || v is byte) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            if (v is IDictionary dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WS(sb, Convert.ToString(e.Key));
                    sb.Append(':');
                    W(sb, e.Value);
                }
                sb.Append('}');
                return;
            }
            if (v is IEnumerable list)
            {
                sb.Append('[');
                bool first = true;
                foreach (var x in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    W(sb, x);
                }
                sb.Append(']');
                return;
            }
            WS(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static void WS(StringBuilder sb, string s)
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
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------- Doc ----------
        // Tra ve: Dictionary<string,object>, List<object>, string, double, bool, null
        public static object Parse(string text)
        {
            int i = 0;
            var v = PV(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException("JSON thua ky tu o vi tri " + i);
            return v;
        }

        private static void Skip(string t, ref int i)
        {
            while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
        }

        private static object PV(string t, ref int i)
        {
            Skip(t, ref i);
            if (i >= t.Length) throw new FormatException("JSON ket thuc som");
            char c = t[i];
            if (c == '{')
            {
                i++;
                var d = new Dictionary<string, object>();
                Skip(t, ref i);
                if (t[i] == '}') { i++; return d; }
                while (true)
                {
                    Skip(t, ref i);
                    string k = PS(t, ref i);
                    Skip(t, ref i);
                    if (t[i++] != ':') throw new FormatException("Thieu dau : o vi tri " + i);
                    d[k] = PV(t, ref i);
                    Skip(t, ref i);
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == '}') { i++; return d; }
                    throw new FormatException("JSON sai o vi tri " + i);
                }
            }
            if (c == '[')
            {
                i++;
                var l = new List<object>();
                Skip(t, ref i);
                if (t[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(PV(t, ref i));
                    Skip(t, ref i);
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == ']') { i++; return l; }
                    throw new FormatException("JSON sai o vi tri " + i);
                }
            }
            if (c == '"') return PS(t, ref i);
            if (string.CompareOrdinal(t, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(t, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(t, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < t.Length && "+-0123456789.eE".IndexOf(t[i]) >= 0) i++;
            if (st == i) throw new FormatException("JSON sai o vi tri " + i);
            return double.Parse(t.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        private static string PS(string t, ref int i)
        {
            if (t[i] != '"') throw new FormatException("Can chuoi o vi tri " + i);
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                char c = t[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = t[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(t.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
        }
    }
}

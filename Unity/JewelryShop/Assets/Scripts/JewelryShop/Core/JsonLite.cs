using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JewelryShop.Core
{
    /// <summary>
    /// Minimal JSON reader for untrusted packets (Core has no engine, so no JsonUtility, and
    /// JsonUtility cannot tell null from an object anyway). Gives Dictionary, List, double,
    /// string, bool or null. Never throws; depth is capped.
    /// </summary>
    internal static class JsonLite
    {
        private const int MaxDepth = 8;

        public static bool TryParse(string text, out object value)
        {
            value = null;
            if (text == null) return false;
            var i = 0;
            try
            {
                if (!ReadValue(text, ref i, 0, out var v)) return false;
                SkipSpace(text, ref i);
                if (i != text.Length) return false;
                value = v;
                return true;
            }
            catch (Exception) // shortcut: belt and braces for the trust boundary, never expected
            {
                return false;
            }
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static bool ReadValue(string s, ref int i, int depth, out object v)
        {
            v = null;
            if (depth > MaxDepth) return false;
            SkipSpace(s, ref i);
            if (i >= s.Length) return false;
            switch (s[i])
            {
                case '{': return ReadObject(s, ref i, depth, out v);
                case '[': return ReadArray(s, ref i, depth, out v);
                case '"':
                    if (!ReadString(s, ref i, out var str)) return false;
                    v = str;
                    return true;
                case 't': v = true; return Literal(s, ref i, "true");
                case 'f': v = false; return Literal(s, ref i, "false");
                case 'n': return Literal(s, ref i, "null");
                default: return ReadNumber(s, ref i, out v);
            }
        }

        private static bool Literal(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        private static bool ReadNumber(string s, ref int i, out object v)
        {
            v = null;
            var start = i;
            while (i < s.Length && "+-.eE0123456789".IndexOf(s[i]) >= 0) i++;
            if (i == start) return false;
            if (!double.TryParse(s.Substring(start, i - start),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                || double.IsNaN(d) || double.IsInfinity(d)) return false;
            v = d;
            return true;
        }

        private static bool ReadString(string s, ref int i, out string result)
        {
            result = null;
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                var c = s[i++];
                if (c == '"') { result = sb.ToString(); return true; }
                if (c < 0x20) return false;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return false;
                var e = s[i++];
                switch (e)
                {
                    case '"': case '\\': case '/': sb.Append(e); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4),
                                NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                                out var code)) return false;
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: return false;
                }
            }
            return false;
        }

        private static bool ReadArray(string s, ref int i, int depth, out object v)
        {
            v = null;
            var list = new List<object>();
            i++;
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; v = list; return true; }
            while (true)
            {
                if (!ReadValue(s, ref i, depth + 1, out var item)) return false;
                list.Add(item);
                SkipSpace(s, ref i);
                if (i >= s.Length) return false;
                if (s[i] == ',') { i++; continue; }
                if (s[i] != ']') return false;
                i++;
                v = list;
                return true;
            }
        }

        private static bool ReadObject(string s, ref int i, int depth, out object v)
        {
            v = null;
            var map = new Dictionary<string, object>();
            i++;
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; v = map; return true; }
            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') return false;
                if (!ReadString(s, ref i, out var key)) return false;
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != ':') return false;
                i++;
                if (!ReadValue(s, ref i, depth + 1, out var item)) return false;
                map[key] = item;
                SkipSpace(s, ref i);
                if (i >= s.Length) return false;
                if (s[i] == ',') { i++; continue; }
                if (s[i] != '}') return false;
                i++;
                v = map;
                return true;
            }
        }
    }
}

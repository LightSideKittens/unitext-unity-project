using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LightSide.Hub
{
    /// <summary>
    /// Minimal JSON reader and writer. The Hub compiles against nothing, so it carries its own rather
    /// than reaching for a package the project may not have yet. Objects come back as
    /// <see cref="Dictionary{TKey,TValue}"/>, arrays as <see cref="List{T}"/>, numbers as
    /// <see cref="double"/>.
    /// </summary>
    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var i = 0;
            return ParseValue(json, ref i);
        }

        /// <summary>The object at <paramref name="key"/>, or null when the value is missing or another kind.</summary>
        public static Dictionary<string, object> Object(Dictionary<string, object> source, string key)
            => source != null && source.TryGetValue(key, out var value)
                ? value as Dictionary<string, object>
                : null;

        /// <summary>The array at <paramref name="key"/>, or null when the value is missing or another kind.</summary>
        public static List<object> Array(Dictionary<string, object> source, string key)
            => source != null && source.TryGetValue(key, out var value) ? value as List<object> : null;

        /// <summary>The string at <paramref name="key"/>, or null when the value is missing or another kind.</summary>
        public static string String(Dictionary<string, object> source, string key)
            => source != null && source.TryGetValue(key, out var value) ? value as string : null;

        private static object ParseValue(string j, ref int i)
        {
            Skip(j, ref i);
            if (i >= j.Length) return null;
            return j[i] switch
            {
                '{' => ParseObject(j, ref i),
                '[' => ParseArray(j, ref i),
                '"' => ParseString(j, ref i),
                't' or 'f' => ParseBool(j, ref i),
                'n' => ParseNull(j, ref i),
                _ => ParseNumber(j, ref i)
            };
        }

        private static Dictionary<string, object> ParseObject(string j, ref int i)
        {
            var o = new Dictionary<string, object>();
            i++; Skip(j, ref i);
            if (i < j.Length && j[i] == '}') { i++; return o; }
            while (i < j.Length)
            {
                Skip(j, ref i);
                var k = ParseString(j, ref i);
                Skip(j, ref i); i++;
                o[k] = ParseValue(j, ref i);
                Skip(j, ref i);
                if (i < j.Length && j[i] == ',') i++; else break;
            }
            if (i < j.Length && j[i] == '}') i++;
            return o;
        }

        private static List<object> ParseArray(string j, ref int i)
        {
            var a = new List<object>();
            i++; Skip(j, ref i);
            if (i < j.Length && j[i] == ']') { i++; return a; }
            while (i < j.Length)
            {
                a.Add(ParseValue(j, ref i));
                Skip(j, ref i);
                if (i < j.Length && j[i] == ',') i++; else break;
            }
            if (i < j.Length && j[i] == ']') i++;
            return a;
        }

        private static string ParseString(string j, ref int i)
        {
            i++;
            var sb = new StringBuilder();
            while (i < j.Length)
            {
                var c = j[i++];
                if (c == '"') break;
                if (c == '\\' && i < j.Length)
                {
                    var n = j[i++];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(j.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: sb.Append(n); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static double ParseNumber(string j, ref int i)
        {
            var s = i;
            while (i < j.Length && "0123456789.eE+-".IndexOf(j[i]) >= 0) i++;
            return double.Parse(j.Substring(s, i - s), CultureInfo.InvariantCulture);
        }

        private static bool ParseBool(string j, ref int i)
        {
            if (j.Substring(i, 4) == "true") { i += 4; return true; }
            i += 5;
            return false;
        }

        private static object ParseNull(string j, ref int i) { i += 4; return null; }

        private static void Skip(string j, ref int i)
        {
            while (i < j.Length && char.IsWhiteSpace(j[i])) i++;
        }

        public static string Serialize(object obj, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, obj, pretty, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v, bool p, int d)
        {
            if (v == null) sb.Append("null");
            else if (v is Dictionary<string, object> dict) WriteObj(sb, dict, p, d);
            else if (v is List<object> list) WriteArr(sb, list, p, d);
            else if (v is string s) WriteStr(sb, s);
            else if (v is bool b) sb.Append(b ? "true" : "false");
            else if (v is double n) sb.Append(n.ToString(CultureInfo.InvariantCulture));
            else sb.Append(v);
        }

        private static void WriteObj(StringBuilder sb, Dictionary<string, object> o, bool p, int d)
        {
            sb.Append('{');
            var first = true;
            foreach (var kv in o)
            {
                if (!first) sb.Append(',');
                first = false;
                if (p) { sb.Append('\n'); Ind(sb, d + 1); }
                WriteStr(sb, kv.Key);
                sb.Append(p ? ": " : ":");
                Write(sb, kv.Value, p, d + 1);
            }
            if (p && o.Count > 0) { sb.Append('\n'); Ind(sb, d); }
            sb.Append('}');
        }

        private static void WriteArr(StringBuilder sb, List<object> a, bool p, int d)
        {
            sb.Append('[');
            for (var i = 0; i < a.Count; i++)
            {
                if (i > 0) sb.Append(',');
                if (p) { sb.Append('\n'); Ind(sb, d + 1); }
                Write(sb, a[i], p, d + 1);
            }
            if (p && a.Count > 0) { sb.Append('\n'); Ind(sb, d); }
            sb.Append(']');
        }

        private static void WriteStr(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
                sb.Append(c switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    _ => c < 0x20 ? $"\\u{(int)c:x4}" : c.ToString()
                });
            sb.Append('"');
        }

        private static void Ind(StringBuilder sb, int d)
        {
            for (var i = 0; i < d; i++) sb.Append("  ");
        }
    }
}

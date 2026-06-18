using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>Minimal JSON reader/writer for Anim Browser grouping persistence.
    /// Hand-written per project policy (no JsonUtility); supports the small fixed schema used here.</summary>
    internal static class AnimGroupJson
    {
        public static string Escape(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            var sb = new StringBuilder(s!.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public static object? Parse(string json)
        {
            int pos = 0;
            object? value = ParseValue(json, ref pos);
            return value;
        }

        private static object? ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length)
                return null;

            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return ParseString(s, ref pos);
                case 't':
                case 'f': return ParseBool(s, ref pos);
                case 'n':
                    pos += 4;
                    return null;
                default: return ParseNumber(s, ref pos);
            }
        }

        private static Dictionary<string, object?> ParseObject(string s, ref int pos)
        {
            var dict = new Dictionary<string, object?>();
            pos++; // {
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '}')
            {
                pos++;
                return dict;
            }

            while (pos < s.Length)
            {
                SkipWhitespace(s, ref pos);
                string key = ParseString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos < s.Length && s[pos] == ':')
                    pos++;
                object? value = ParseValue(s, ref pos);
                dict[key] = value;
                SkipWhitespace(s, ref pos);
                if (pos < s.Length && s[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (pos < s.Length && s[pos] == '}')
                {
                    pos++;
                    break;
                }
                break;
            }
            return dict;
        }

        private static List<object?> ParseArray(string s, ref int pos)
        {
            var list = new List<object?>();
            pos++; // [
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ']')
            {
                pos++;
                return list;
            }

            while (pos < s.Length)
            {
                object? value = ParseValue(s, ref pos);
                list.Add(value);
                SkipWhitespace(s, ref pos);
                if (pos < s.Length && s[pos] == ',')
                {
                    pos++;
                    continue;
                }
                if (pos < s.Length && s[pos] == ']')
                {
                    pos++;
                    break;
                }
                break;
            }
            return list;
        }

        private static string ParseString(string s, ref int pos)
        {
            var sb = new StringBuilder();
            if (pos < s.Length && s[pos] == '"')
                pos++;
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"')
                    break;
                if (c == '\\' && pos < s.Length)
                {
                    char esc = s[pos++];
                    switch (esc)
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
                            if (pos + 4 <= s.Length &&
                                int.TryParse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            {
                                sb.Append((char)code);
                                pos += 4;
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool ParseBool(string s, ref int pos)
        {
            if (s[pos] == 't')
            {
                pos += 4;
                return true;
            }
            pos += 5;
            return false;
        }

        private static double ParseNumber(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length && "0123456789.-+eE".IndexOf(s[pos]) >= 0)
                pos++;
            if (pos > start &&
                double.TryParse(s.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }
            return 0d;
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                pos++;
        }

        public static string AsString(object? value) => value as string ?? string.Empty;

        public static int AsInt(object? value)
        {
            if (value is double d)
                return (int)d;
            return 0;
        }

        public static List<object?> AsArray(object? value) => value as List<object?> ?? new List<object?>();

        public static Dictionary<string, object?> AsObject(object? value) =>
            value as Dictionary<string, object?> ?? new Dictionary<string, object?>();
    }
}

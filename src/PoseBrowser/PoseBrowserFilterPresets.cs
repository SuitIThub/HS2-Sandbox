using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace HS2SandboxPlugin
{
    internal sealed class PoseBrowserFilterPreset
    {
        public string name = "";
        public string searchText = "";
        public bool searchUseRegex;
        public bool tagFilterAndMode = true;
        public int tagFilterGroupsMode;
        public int tagFilterThumbnailMode;
        public string[] includeTags = new string[0];
        public string[] excludeTags = new string[0];

        public bool MatchesState(
            string searchText,
            bool searchUseRegex,
            bool tagFilterAndMode,
            int tagFilterGroupsMode,
            int tagFilterThumbnailMode,
            ICollection<string> includeTags,
            ICollection<string> excludeTags)
        {
            return string.Equals(this.searchText ?? "", searchText ?? "", StringComparison.Ordinal)
                && searchUseRegex == this.searchUseRegex
                && tagFilterAndMode == this.tagFilterAndMode
                && tagFilterGroupsMode == this.tagFilterGroupsMode
                && tagFilterThumbnailMode == this.tagFilterThumbnailMode
                && TagSetsEqual(this.includeTags, includeTags)
                && TagSetsEqual(this.excludeTags, excludeTags);
        }

        private static bool TagSetsEqual(string[]? saved, ICollection<string> current)
        {
            var a = saved ?? new string[0];
            var set = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            if (set.Count != a.Length)
                return false;
            foreach (var t in a)
            {
                if (!set.Contains(t))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Persists named Pose Browser search/tag filter presets to
    /// <c>BepInEx/config/com.hs2.sandbox/pose_browser_filter_presets.json</c>.
    /// </summary>
    internal static class PoseBrowserFilterPresets
    {
        public const int FormatVersion = 1;

        public static string GetDefaultPath()
            => PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_browser_filter_presets.json");

        public static bool TryLoad(string path, out List<PoseBrowserFilterPreset> presets)
        {
            presets = new List<PoseBrowserFilterPreset>();
            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!TryParseV1(json, out presets))
                    presets = new List<PoseBrowserFilterPreset>();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not load filter presets: {ex.Message}");
                presets = new List<PoseBrowserFilterPreset>();
            }

            return presets.Count > 0;
        }

        public static void Save(string path, IList<PoseBrowserFilterPreset> presets)
        {
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = BuildJson(presets);
                string tempPath = path + ".tmp";
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(tempPath, json, utf8);

                FileEx.CommitTempFile(tempPath, path);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not save filter presets: {ex.Message}");
            }
        }

        private static string BuildJson(IList<PoseBrowserFilterPreset> presets)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\n  \"version\": ").Append(FormatVersion)
                .Append(",\n  \"presets\": [\n");

            for (int i = 0; i < presets.Count; i++)
            {
                if (i > 0)
                    sb.Append(",\n");
                var p = presets[i];
                sb.Append("    {\n      \"name\":");
                AppendJsonString(sb, p.name);
                sb.Append(",\n      \"searchText\":");
                AppendJsonString(sb, p.searchText ?? "");
                sb.Append(",\n      \"searchUseRegex\": ").Append(p.searchUseRegex ? "true" : "false")
                    .Append(",\n      \"tagFilterAndMode\": ").Append(p.tagFilterAndMode ? "true" : "false")
                    .Append(",\n      \"tagFilterGroupsMode\": ").Append(p.tagFilterGroupsMode)
                    .Append(",\n      \"tagFilterThumbnailMode\": ").Append(p.tagFilterThumbnailMode)
                    .Append(",\n      \"includeTags\": ");
                AppendStringArray(sb, p.includeTags);
                sb.Append(",\n      \"excludeTags\": ");
                AppendStringArray(sb, p.excludeTags);
                sb.Append("\n    }");
            }

            sb.Append("\n  ]\n}");
            return sb.ToString();
        }

        private static void AppendStringArray(StringBuilder sb, string[]? tags)
        {
            sb.Append('[');
            var arr = tags ?? new string[0];
            for (int i = 0; i < arr.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                AppendJsonString(sb, arr[i]);
            }

            sb.Append(']');
        }

        private static bool TryParseV1(string json, out List<PoseBrowserFilterPreset> presets)
        {
            presets = new List<PoseBrowserFilterPreset>();
            if (!TryReadInt(json, "version", out int version) || version != FormatVersion)
                return false;

            int key = json.IndexOf("\"presets\"", StringComparison.OrdinalIgnoreCase);
            if (key < 0)
                return false;

            int bracket = json.IndexOf('[', key);
            if (bracket < 0)
                return false;

            int end = IndexOfMatchingBracket(json, bracket);
            if (end < 0)
                return false;

            int i = bracket + 1;
            while (i < end)
            {
                SkipWs(json, ref i);
                if (i >= end || json[i] == ']')
                    break;

                if (json[i] != '{')
                {
                    i++;
                    continue;
                }

                int objEnd = IndexOfMatchingBrace(json, i);
                if (objEnd < 0)
                    break;

                string obj = json.Substring(i, objEnd - i + 1);
                if (TryParseJsonStringValue(obj, "name", out string? name)
                    && TryParseJsonStringValue(obj, "searchText", out string? searchText))
                {
                    var preset = new PoseBrowserFilterPreset
                    {
                        name = name ?? "",
                        searchText = searchText ?? ""
                    };
                    TryParseBool(obj, "searchUseRegex", out preset.searchUseRegex);
                    TryParseBool(obj, "tagFilterAndMode", out preset.tagFilterAndMode);
                    preset.tagFilterGroupsMode = TryParseDisplayFilterMode(obj, "tagFilterGroupsMode", "tagFilterExcludeGroups");
                    preset.tagFilterThumbnailMode = TryParseDisplayFilterMode(obj, "tagFilterThumbnailMode", "tagFilterExcludeNoThumbnail");
                    if (TryParseStringArray(obj, "includeTags", out string[]? inc))
                        preset.includeTags = inc ?? new string[0];
                    if (TryParseStringArray(obj, "excludeTags", out string[]? exc))
                        preset.excludeTags = exc ?? new string[0];
                    if (!StringEx.IsNullOrWhiteSpace(preset.name))
                        presets.Add(preset);
                }

                i = objEnd + 1;
                SkipWs(json, ref i);
                if (i < end && json[i] == ',')
                    i++;
            }

            return true;
        }

        private static bool TryParseStringArray(string obj, string key, out string[]? values)
        {
            values = new string[0];
            int keyIdx = obj.IndexOf('"' + key + '"', StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0)
                return false;

            int bracket = obj.IndexOf('[', keyIdx);
            if (bracket < 0)
                return false;

            int end = IndexOfMatchingBracket(obj, bracket);
            if (end < 0)
                return false;

            var list = new List<string>();
            int i = bracket + 1;
            while (i < end)
            {
                SkipWs(obj, ref i);
                if (i >= end || obj[i] == ']')
                    break;

                if (obj[i] != '"')
                {
                    i++;
                    continue;
                }

                if (TryReadJsonString(obj, ref i, out string? s))
                    list.Add(s ?? "");
            }

            values = list.ToArray();
            return true;
        }

        private static int TryParseDisplayFilterMode(string obj, string intKey, string legacyBoolKey)
        {
            if (TryReadInt(obj, intKey, out int mode))
                return ClampDisplayFilterMode(mode);

            if (TryParseBool(obj, legacyBoolKey, out bool legacy) && legacy)
                return (int)PoseDisplayFilterMode.Exclude;

            return (int)PoseDisplayFilterMode.Off;
        }

        private static int ClampDisplayFilterMode(int mode) =>
            mode switch
            {
                (int)PoseDisplayFilterMode.Exclude => (int)PoseDisplayFilterMode.Exclude,
                (int)PoseDisplayFilterMode.IncludeOnly => (int)PoseDisplayFilterMode.IncludeOnly,
                _ => (int)PoseDisplayFilterMode.Off
            };

        private static bool TryParseBool(string json, string key, out bool value)
        {
            value = false;
            if (!TryFindKey(json, key, out int start))
                return false;

            int i = start;
            SkipWs(json, ref i);
            if (i + 4 <= json.Length && string.Compare(json, i, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = true;
                return true;
            }

            if (i + 5 <= json.Length && string.Compare(json, i, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = false;
                return true;
            }

            return false;
        }

        private static bool TryReadInt(string json, string key, out int value)
        {
            value = 0;
            if (!TryFindKey(json, key, out int start))
                return false;

            int i = start;
            SkipWs(json, ref i);
            if (i >= json.Length)
                return false;

            bool neg = json[i] == '-';
            if (neg)
                i++;

            if (i >= json.Length || !char.IsDigit(json[i]))
                return false;

            long n = 0;
            while (i < json.Length && char.IsDigit(json[i]))
            {
                n = n * 10 + (json[i] - '0');
                i++;
            }

            value = (int)(neg ? -n : n);
            return true;
        }

        private static bool TryFindKey(string json, string key, out int valueStart)
        {
            valueStart = -1;
            string needle = "\"" + key + "\"";
            int keyIdx = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0)
                return false;

            int colon = json.IndexOf(':', keyIdx + needle.Length);
            if (colon < 0)
                return false;

            valueStart = colon + 1;
            return true;
        }

        private static bool TryParseJsonStringValue(string json, string key, out string? value)
        {
            value = null;
            if (!TryFindKey(json, key, out int start))
                return false;

            int i = start;
            SkipWs(json, ref i);
            return TryReadJsonString(json, ref i, out value);
        }

        private static bool TryReadJsonString(string json, ref int i, out string? value)
        {
            value = null;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '"')
                return false;

            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }

                if (c == '\\' && i < json.Length)
                {
                    char esc = json[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u' when i + 3 < json.Length:
                            if (int.TryParse(
                                    json.Substring(i, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out int code))
                            {
                                sb.Append((char)code);
                                i += 4;
                            }

                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }

                    continue;
                }

                sb.Append(c);
            }

            return false;
        }

        private static void AppendJsonString(StringBuilder sb, string? value)
        {
            sb.Append('"');
            if (string.IsNullOrEmpty(value))
            {
                sb.Append('"');
                return;
            }

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat(
                                System.Globalization.CultureInfo.InvariantCulture,
                                "\\u{0:X4}",
                                (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        private static void SkipWs(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
        }

        private static int IndexOfMatchingBracket(string json, int openIndex)
        {
            if (openIndex < 0 || openIndex >= json.Length || json[openIndex] != '[')
                return -1;

            int depth = 0;
            bool inString = false;
            for (int i = openIndex; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\')
                        i++;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '[')
                    depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int IndexOfMatchingBrace(string json, int openIndex)
        {
            if (openIndex < 0 || openIndex >= json.Length || json[openIndex] != '{')
                return -1;

            int depth = 0;
            bool inString = false;
            for (int i = openIndex; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\')
                        i++;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }
    }
}

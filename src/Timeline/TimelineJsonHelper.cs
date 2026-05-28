using System;
using System.Collections.Generic;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Shared static JSON parsing and building utilities for timeline serialization.
    /// Used by both ActionTimeline and SubTimelineCommand.
    /// </summary>
    internal static class TimelineJsonHelper
    {
        public static string EscapeJsonString(string? s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length + 8);
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
                        if (c < ' ') sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Appends a JSON array of saved timeline entries (same shape as root <c>entries</c>).</summary>
        public static void AppendSavedEntriesArray(StringBuilder sb, SavedTimelineEntry[] entries)
        {
            sb.Append('[');
            for (int i = 0; i < entries.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var e = entries[i];
                sb.Append("{\"typeId\":\"").Append(EscapeJsonString(e.typeId))
                    .Append("\",\"payload\":\"").Append(EscapeJsonString(e.payload))
                    .Append("\",\"enabled\":").Append(e.enabled ? "true" : "false").Append('}');
            }
            sb.Append(']');
        }

        /// <summary>Builds {"entries":[...]} JSON from an array of entries. Variables section is omitted (not needed for subtimeline payloads).</summary>
        public static string BuildTimelineJson(SavedTimelineEntry[] entries)
        {
            var sb = new StringBuilder();
            sb.Append("{\"entries\":");
            AppendSavedEntriesArray(sb, entries);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Minimal JSON for a <c>sub_timeline</c> row: references a body in the root <c>subtimelines</c> array.</summary>
        public static string BuildSubTimelineRefJson(string definitionId, string title, SubTimelineParamInputs? param)
        {
            var sb = new StringBuilder();
            sb.Append("{\"definitionId\":\"").Append(EscapeJsonString(definitionId))
              .Append("\",\"title\":\"").Append(EscapeJsonString(title ?? "")).Append('"');
            if (param != null)
                AppendParamJson(sb, param);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>Serializes one object in the root <c>subtimelines</c> array.</summary>
        public static void AppendSubTimelineDefinitionObject(StringBuilder sb, SavedSubTimelineDefinition def)
        {
            sb.Append("{\"id\":\"").Append(EscapeJsonString(def.id))
              .Append("\",\"title\":\"").Append(EscapeJsonString(def.title ?? ""))
              .Append("\",\"template\":").Append(def.template ? "true" : "false")
              .Append(",\"entries\":");
            AppendSavedEntriesArray(sb, def.entries ?? Array.Empty<SavedTimelineEntry>());
            sb.Append('}');
        }

        /// <summary>Builds {"id":"...","title":"...","entries":[...]} JSON for a subtimeline payload.</summary>
        public static string BuildSubTimelineJson(string id, string title, SavedTimelineEntry[] entries)
            => BuildSubTimelineJson(id, title, entries, null);

        /// <summary>Builds subtimeline JSON including optional parent-row <paramref name="param"/> state.</summary>
        public static string BuildSubTimelineJson(string id, string title, SavedTimelineEntry[] entries, SubTimelineParamInputs? param)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJsonString(id))
              .Append("\",\"title\":\"").Append(EscapeJsonString(title)).Append('"');
            if (param != null)
                AppendParamJson(sb, param);
            sb.Append(",\"entries\":");
            AppendSavedEntriesArray(sb, entries);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendParamJson(StringBuilder sb, SubTimelineParamInputs p)
        {
            sb.Append(",\"param\":{");
            sb.Append("\"s\":\"").Append(EscapeJsonString(p.StringText)).Append('"');
            sb.Append(",\"i\":\"").Append(EscapeJsonString(p.IntText)).Append('"');
            sb.Append(",\"b\":").Append(p.BoolValue ? "true" : "false");
            sb.Append(",\"l\":\"").Append(EscapeJsonString(string.Join("\u0002", p.ListItems))).Append('"');
            sb.Append(",\"d\":\"").Append(EscapeJsonString(SubTimelineParamInputs.SerializeDictLines(p.Dict))).Append('"');
            sb.Append('}');
        }

        /// <summary>Parses the optional <c>param</c> object from a subtimeline JSON payload into <paramref name="into"/>.</summary>
        public static void TryMergeSubTimelineParam(string json, SubTimelineParamInputs into)
        {
            int pi = json.IndexOf("\"param\"", StringComparison.OrdinalIgnoreCase);
            if (pi < 0) return;
            int brace = json.IndexOf('{', pi);
            if (brace < 0) return;
            int end = IndexOfMatchingBrace(json, brace);
            if (end < 0) return;
            string obj = json.Substring(brace, end - brace + 1);
            if (TryParseJsonStringValue(obj, "s", out string? s)) into.StringText = s ?? "";
            if (TryParseJsonStringValue(obj, "i", out string? i))
                into.IntText = string.IsNullOrEmpty(i) ? "0" : (i ?? "0");
            if (TryParseJsonBoolValue(obj, "b", out bool b)) into.BoolValue = b;
            if (TryParseJsonStringValue(obj, "l", out string? l))
            {
                into.ListItems.Clear();
                string l2 = l ?? "";
                if (l2.Length > 0)
                {
                    foreach (string part in l2.Split('\u0002'))
                    {
                        if (!string.IsNullOrEmpty(part)) into.ListItems.Add(part);
                    }
                }
            }
            if (TryParseJsonStringValue(obj, "d", out string? d))
            {
                into.Dict.Clear();
                foreach (var kv in SubTimelineParamInputs.ParseDictLines(d ?? ""))
                    into.Dict[kv.Key] = kv.Value;
            }
        }

        /// <summary>
        /// Parses our timeline JSON format {"entries":[{...},{...}]} without relying on JsonUtility (which fails on arrays).
        /// </summary>
        public static bool TryParseTimelineJson(string json, out List<SavedTimelineEntry>? entries)
        {
            entries = null;
            int i = json.IndexOf("\"entries\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            i = json.IndexOf('[', i);
            if (i < 0) return false;
            int arrayEnd = IndexOfMatchingBracket(json, i);
            if (arrayEnd < 0) return false;
            entries = new List<SavedTimelineEntry>();
            i++;
            while (i < arrayEnd)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart < 0 || objStart > arrayEnd) break;
                int objEnd = IndexOfMatchingBrace(json, objStart);
                if (objEnd < 0 || objEnd > arrayEnd) break;
                string obj = json.Substring(objStart, objEnd - objStart + 1);
                if (TryParseEntry(obj, out SavedTimelineEntry? entry) && entry != null)
                    entries.Add(entry);
                i = objEnd + 1;
                // Continue scanning for the next object within this entries array only.
            }
            return entries.Count >= 0;
        }

        public static bool TryParseEntry(string obj, out SavedTimelineEntry? entry)
        {
            entry = null;
            if (!TryParseJsonStringValue(obj, "typeId", out string? typeId)) typeId = "";
            if (!TryParseJsonStringValue(obj, "payload", out string? payload)) payload = "";
            if (!TryParseJsonBoolValue(obj, "enabled", out bool enabled)) enabled = true;
            entry = new SavedTimelineEntry { typeId = typeId ?? "", payload = payload ?? "", enabled = enabled };
            return true;
        }

        public static bool TryParseJsonStringValue(string json, string key, out string? value)
        {
            value = null;
            string keyPattern = "\"" + key + "\"";
            int ki = json.IndexOf(keyPattern, StringComparison.OrdinalIgnoreCase);
            if (ki < 0) return false;
            int colon = json.IndexOf(':', ki);
            if (colon < 0) return false;
            int start = json.IndexOf('"', colon);
            if (start < 0) return false;
            start++;
            var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    if (next == '"') { sb.Append('"'); i++; continue; }
                    if (next == '\\') { sb.Append('\\'); i++; continue; }
                    if (next == 'n') { sb.Append('\n'); i++; continue; }
                    if (next == 'r') { sb.Append('\r'); i++; continue; }
                    if (next == 't') { sb.Append('\t'); i++; continue; }
                    if (next == 'u' && i + 5 < json.Length)
                    {
                        string hex = json.Substring(i + 2, 4);
                        if (hex.Length == 4 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                        {
                            sb.Append((char)codePoint);
                            i += 5;
                            continue;
                        }
                    }
                }
                if (c == '"') break;
                sb.Append(c);
            }
            value = sb.ToString();
            return true;
        }

        public static bool TryParseJsonBoolValue(string json, string key, out bool value)
        {
            value = true;
            string keyPattern = "\"" + key + "\"";
            int ki = json.IndexOf(keyPattern, StringComparison.OrdinalIgnoreCase);
            if (ki < 0) return false;
            int colon = json.IndexOf(':', ki);
            if (colon < 0) return false;
            colon++;
            while (colon < json.Length && char.IsWhiteSpace(json[colon])) colon++;
            if (colon + 4 <= json.Length && string.Compare(json, colon, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            { value = true; return true; }
            if (colon + 5 <= json.Length && string.Compare(json, colon, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            { value = false; return true; }
            return false;
        }

        public static int IndexOfMatchingBrace(string s, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            char quote = '\0';
            for (int i = openIndex; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == quote) { inString = false; continue; }
                    continue;
                }
                if (c == '"' || c == '\'') { inString = true; quote = c; continue; }
                if (c == '{') { depth++; continue; }
                if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        public static int IndexOfMatchingBracket(string s, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            char quote = '\0';
            for (int i = openIndex; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == quote) { inString = false; continue; }
                    continue;
                }
                if (c == '"' || c == '\'') { inString = true; quote = c; continue; }
                if (c == '[') { depth++; continue; }
                if (c == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>Parses the root <c>subtimelines</c> array into DTOs (each object's <c>entries</c> list).</summary>
        public static bool TryParseSubTimelinesArray(string json, out List<SavedSubTimelineDefinition>? defs)
        {
            defs = null;
            int i = json.IndexOf("\"subtimelines\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            i = json.IndexOf('[', i);
            if (i < 0) return false;
            defs = new List<SavedSubTimelineDefinition>();
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i < json.Length && json[i] == ']') return true;
            while (i < json.Length)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart < 0) break;
                int objEnd = IndexOfMatchingBrace(json, objStart);
                if (objEnd < 0) break;
                string obj = json.Substring(objStart, objEnd - objStart + 1);
                var def = new SavedSubTimelineDefinition();
                if (TryParseJsonStringValue(obj, "id", out string? sid) && !string.IsNullOrEmpty(sid))
                    def.id = sid!;
                if (TryParseJsonStringValue(obj, "title", out string? stitle))
                    def.title = stitle ?? "";
                if (TryParseJsonBoolValue(obj, "template", out bool tmpl))
                    def.template = tmpl;
                if (TryParseTimelineJson(obj, out List<SavedTimelineEntry>? entList) && entList != null)
                    def.entries = entList.ToArray();
                else
                    def.entries = Array.Empty<SavedTimelineEntry>();
                defs.Add(def);
                i = objEnd + 1;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i < json.Length && json[i] == ']') break;
                if (i < json.Length && json[i] == ',') { i++; continue; }
                break;
            }
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal sealed class NotebookNoteData
    {
        public string title = "New Note";
        public string content = "";
    }

    internal sealed class NotebookPersistedState
    {
        public NotebookNoteData[] notes = Array.Empty<NotebookNoteData>();
        public int selectedIndex;
        public float windowX = 420f;
        public float windowY = 120f;
        public float windowW = 680f;
        public float windowH = 500f;
    }

    /// <summary>
    /// Saves notebook data to <c>BepInEx/config/com.hs2.sandbox/notebook.json</c> using explicit JSON
    /// (Unity <see cref="JsonUtility"/> does not reliably round-trip arrays of nested types).
    /// </summary>
    internal static class NotebookPersistence
    {
        public const int FormatVersion = 1;

        [Serializable]
        private sealed class LegacyNotebookSaveData
        {
            public LegacyNotebookNote[] notes = Array.Empty<LegacyNotebookNote>();
            public int selectedIndex;
        }

        [Serializable]
        private sealed class LegacyNotebookNote
        {
            public string title = "";
            public string content = "";
        }

        public static string GetDefaultPath()
            => Path.Combine(BepInEx.Paths.ConfigPath, "com.hs2.sandbox", "notebook.json");

        public static bool TryLoad(string path, out NotebookPersistedState state)
        {
            state = new NotebookPersistedState();
            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (TryParseV1(json, out state))
                    return state.notes.Length > 0;

                if (TryParseLegacy(json, out state))
                    return state.notes.Length > 0;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"Notebook: Could not load {path}: {ex.Message}");
            }

            return false;
        }

        public static void Save(string path, NotebookPersistedState state)
        {
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = BuildJson(state);
                string tempPath = path + ".tmp";
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(tempPath, json, utf8);

                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Notebook: Could not save {path}: {ex.Message}");
            }
        }

        private static string BuildJson(NotebookPersistedState state)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\n  \"version\": ").Append(FormatVersion)
                .Append(",\n  \"selectedIndex\": ").Append(state.selectedIndex)
                .Append(",\n  \"windowX\": ").Append(state.windowX.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\n  \"windowY\": ").Append(state.windowY.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\n  \"windowW\": ").Append(state.windowW.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\n  \"windowH\": ").Append(state.windowH.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\n  \"notes\": [\n");

            for (int i = 0; i < state.notes.Length; i++)
            {
                if (i > 0)
                    sb.Append(",\n");
                var note = state.notes[i];
                sb.Append("    {\"title\":");
                AppendJsonString(sb, note.title);
                sb.Append(",\"content\":");
                AppendJsonString(sb, note.content);
                sb.Append('}');
            }

            sb.Append("\n  ]\n}");
            return sb.ToString();
        }

        private static bool TryParseV1(string json, out NotebookPersistedState state)
        {
            state = new NotebookPersistedState();
            if (!TryReadInt(json, "version", out int version) || version != FormatVersion)
                return false;

            TryReadInt(json, "selectedIndex", out int selectedIndex);
            state.selectedIndex = selectedIndex;

            TryReadFloat(json, "windowX", out float wx);
            TryReadFloat(json, "windowY", out float wy);
            TryReadFloat(json, "windowW", out float ww);
            TryReadFloat(json, "windowH", out float wh);
            if (ww > 0f && wh > 0f)
            {
                state.windowX = wx;
                state.windowY = wy;
                state.windowW = ww;
                state.windowH = wh;
            }

            if (!TryParseNotesArray(json, out NotebookNoteData[] notes))
                return false;

            state.notes = notes;
            return notes.Length > 0;
        }

        private static bool TryParseLegacy(string json, out NotebookPersistedState state)
        {
            state = new NotebookPersistedState();
            try
            {
                var data = JsonUtility.FromJson<LegacyNotebookSaveData>(json);
                if (data?.notes == null || data.notes.Length == 0)
                    return false;

                var notes = new NotebookNoteData[data.notes.Length];
                for (int i = 0; i < data.notes.Length; i++)
                {
                    var src = data.notes[i];
                    notes[i] = new NotebookNoteData
                    {
                        title = src?.title ?? "",
                        content = src?.content ?? ""
                    };
                }

                state.notes = notes;
                state.selectedIndex = data.selectedIndex;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseNotesArray(string json, out NotebookNoteData[] notes)
        {
            notes = Array.Empty<NotebookNoteData>();
            int key = json.IndexOf("\"notes\"", StringComparison.OrdinalIgnoreCase);
            if (key < 0)
                return false;

            int bracket = json.IndexOf('[', key);
            if (bracket < 0)
                return false;

            int end = IndexOfMatchingBracket(json, bracket);
            if (end < 0)
                return false;

            var list = new List<NotebookNoteData>();
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
                if (TryParseJsonStringValue(obj, "title", out string? title)
                    && TryParseJsonStringValue(obj, "content", out string? content))
                {
                    list.Add(new NotebookNoteData
                    {
                        title = title ?? "",
                        content = content ?? ""
                    });
                }

                i = objEnd + 1;
                SkipWs(json, ref i);
                if (i < end && json[i] == ',')
                    i++;
            }

            notes = list.ToArray();
            return notes.Length > 0;
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

        private static bool TryReadFloat(string json, string key, out float value)
        {
            value = 0f;
            if (!TryFindKey(json, key, out int start))
                return false;

            int i = start;
            SkipWs(json, ref i);
            int end = i;
            while (end < json.Length && "0123456789.-eE+".IndexOf(json[end]) >= 0)
                end++;

            return end > i
                   && float.TryParse(
                       json.Substring(i, end - i),
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out value);
        }

        private static bool TryFindKey(string json, string key, out int valueStart)
        {
            valueStart = 0;
            string needle = "\"" + key + "\"";
            int idx = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return false;

            int colon = json.IndexOf(':', idx + needle.Length);
            if (colon < 0)
                return false;

            valueStart = colon + 1;
            return true;
        }

        public static bool TryParseJsonStringValue(string json, string key, out string? value)
        {
            value = null;
            if (!TryFindKey(json, key, out int i))
                return false;

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

                if (c != '\\' || i >= json.Length)
                {
                    sb.Append(c);
                    continue;
                }

                char esc = json[i++];
                switch (esc)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(esc);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u' when i + 3 < json.Length
                                 && int.TryParse(
                                     json.Substring(i, 4),
                                     System.Globalization.NumberStyles.HexNumber,
                                     null,
                                     out int code):
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default:
                        sb.Append(esc);
                        break;
                }
            }

            return false;
        }

        private static void AppendJsonString(StringBuilder sb, string? s)
        {
            sb.Append('"');
            if (string.IsNullOrEmpty(s))
            {
                sb.Append('"');
                return;
            }

            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal sealed class PoseBrowserStashEntry
    {
        public string Id = "";
        public long UtcTicks;
        public string CharacterDisplayName = "";
        public int SourceDicKey;
        public PoseCharacterSnapshot Snapshot = new PoseCharacterSnapshot();

        public DateTime TimestampUtc => new DateTime(UtcTicks, DateTimeKind.Utc);

        public string FormatTimestampLocal() =>
            TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

        public string ListLabel =>
            string.IsNullOrWhiteSpace(CharacterDisplayName)
                ? FormatTimestampLocal()
                : $"{CharacterDisplayName}  {FormatTimestampLocal()}";
    }

    /// <summary>
    /// User-managed pose stash: capture FK/IK from one character, apply to any selection.
    /// </summary>
    internal sealed class PoseBrowserStash
    {
        public const int FormatVersion = 1;

        private readonly List<PoseBrowserStashEntry> _entries = new List<PoseBrowserStashEntry>();
        private bool _dirty;

        public bool AutoDeleteAfterApply
        {
            get => _autoDeleteAfterApply;
            set
            {
                if (_autoDeleteAfterApply == value)
                    return;
                _autoDeleteAfterApply = value;
                _dirty = true;
            }
        }

        private bool _autoDeleteAfterApply;

        public IReadOnlyList<PoseBrowserStashEntry> Entries => _entries;

        public static string GetDefaultPath() =>
            Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_stash.json");

        public void LoadFromDisk()
        {
            _entries.Clear();
            _autoDeleteAfterApply = false;
            string path = GetDefaultPath();
            if (!File.Exists(path))
                return;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!TryParseV1(json, out bool autoDelete, out var entries))
                    return;
                _autoDeleteAfterApply = autoDelete;
                _entries.AddRange(entries);
                _dirty = false;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not load pose stash: {ex.Message}");
            }
        }

        public void SaveToDiskIfDirty()
        {
            if (!_dirty)
                return;
            SaveToDisk();
        }

        public void SaveToDisk()
        {
            try
            {
                string path = GetDefaultPath();
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = BuildJsonV1();
                string tempPath = path + ".tmp";
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(tempPath, json, utf8);
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
                _dirty = false;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not save pose stash: {ex.Message}");
            }
        }

        public bool TryStashFromCharacter(OCIChar oci, out PoseBrowserStashEntry? entry)
        {
            entry = null;
            if (oci == null)
                return false;
            if (!PoseDataService.TryCaptureCharacterSnapshot(oci, out var captured))
                return false;

            entry = new PoseBrowserStashEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                UtcTicks = DateTime.UtcNow.Ticks,
                CharacterDisplayName = PoseDataService.GetOCICharDisplayName(oci),
                SourceDicKey = PoseDataService.TryGetDicKey(oci, out int dicKey) ? dicKey : 0,
                Snapshot = new PoseCharacterSnapshot { PoseData = captured.PoseData }
            };
            _entries.Add(entry);
            _dirty = true;
            return true;
        }

        public int ApplyEntryToCharacters(PoseBrowserStashEntry entry, IEnumerable<OCIChar> characters)
        {
            if (entry == null)
                return 0;

            int applied = 0;
            foreach (var oci in characters)
            {
                if (oci == null)
                    continue;
                if (PoseDataService.TryRestoreCharacterSnapshot(oci, entry.Snapshot, restorePose: true,
                        restorePosition: false, restoreRotation: false))
                    applied++;
            }

            return applied;
        }

        public bool RemoveEntry(string id)
        {
            int index = _entries.FindIndex(e => string.Equals(e.Id, id, StringComparison.Ordinal));
            if (index < 0)
                return false;
            _entries.RemoveAt(index);
            _dirty = true;
            return true;
        }

        public void ClearAll()
        {
            if (_entries.Count == 0)
                return;
            _entries.Clear();
            _dirty = true;
        }

        public IEnumerable<PoseBrowserStashEntry> GetEntriesNewestFirst() =>
            _entries.OrderByDescending(e => e.UtcTicks);

        private string BuildJsonV1()
        {
            var sb = new StringBuilder();
            sb.Append("{\"version\":").Append(FormatVersion)
                .Append(",\"autoDeleteAfterApply\":").Append(AutoDeleteAfterApply ? "true" : "false")
                .Append(",\"entries\":[");
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                AppendEntryObject(sb, _entries[i]);
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendEntryObject(StringBuilder sb, PoseBrowserStashEntry e)
        {
            sb.Append("{\"id\":\"").Append(EscapeJsonString(e.Id))
                .Append("\",\"utcTicks\":").Append(e.UtcTicks)
                .Append(",\"displayName\":\"").Append(EscapeJsonString(e.CharacterDisplayName))
                .Append("\",\"sourceDicKey\":").Append(e.SourceDicKey)
                .Append(",\"pose\":\"").Append(Convert.ToBase64String(e.Snapshot.PoseData)).Append("\"}");
        }

        private static bool TryParseV1(string json, out bool autoDeleteAfterApply, out List<PoseBrowserStashEntry> entries)
        {
            autoDeleteAfterApply = false;
            entries = new List<PoseBrowserStashEntry>();
            if (string.IsNullOrWhiteSpace(json))
                return false;

            if (!TryReadIntField(json, "\"version\"", out int version) || version != FormatVersion)
                return false;

            TryReadBoolField(json, "\"autoDeleteAfterApply\"", out autoDeleteAfterApply);
            if (!TryExtractArray(json, "\"entries\"", out string? entriesJson) || entriesJson == null)
                return true;

            foreach (string obj in SplitTopLevelObjects(entriesJson))
            {
                if (TryParseEntry(obj, out var entry))
                    entries.Add(entry);
            }

            return true;
        }

        private static bool TryParseEntry(string obj, out PoseBrowserStashEntry entry)
        {
            entry = new PoseBrowserStashEntry();
            if (!TryReadStringField(obj, "\"id\"", out entry.Id) || string.IsNullOrEmpty(entry.Id))
                return false;
            if (!TryReadLongField(obj, "\"utcTicks\"", out entry.UtcTicks))
                return false;
            TryReadStringField(obj, "\"displayName\"", out entry.CharacterDisplayName);
            TryReadIntField(obj, "\"sourceDicKey\"", out entry.SourceDicKey);

            if (!TryReadStringField(obj, "\"pose\"", out string? b64) || string.IsNullOrEmpty(b64))
                return false;
            try
            {
                entry.Snapshot.PoseData = Convert.FromBase64String(b64);
            }
            catch
            {
                return false;
            }

            return entry.Snapshot.PoseData.Length > 0;
        }

        private static bool TryReadIntField(string json, string key, out int value)
        {
            value = 0;
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i) + 1;
            int end = i;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            return end > i && int.TryParse(json.Substring(i, end - i), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadLongField(string json, string key, out long value)
        {
            value = 0;
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i) + 1;
            int end = i;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            return end > i && long.TryParse(json.Substring(i, end - i), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadBoolField(string json, string key, out bool value)
        {
            value = false;
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i) + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            if (json.IndexOf("true", i, StringComparison.Ordinal) == i)
            {
                value = true;
                return true;
            }

            if (json.IndexOf("false", i, StringComparison.Ordinal) == i)
            {
                value = false;
                return true;
            }

            return false;
        }

        private static bool TryReadStringField(string json, string key, out string value)
        {
            value = "";
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf('"', i + key.Length);
            if (i < 0) return false;
            int start = i + 1;
            var sb = new StringBuilder();
            for (int p = start; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '\\' && p + 1 < json.Length)
                {
                    char n = json[p + 1];
                    switch (n)
                    {
                        case '\\': sb.Append('\\'); p++; break;
                        case '"': sb.Append('"'); p++; break;
                        case 'n': sb.Append('\n'); p++; break;
                        case 'r': sb.Append('\r'); p++; break;
                        case 't': sb.Append('\t'); p++; break;
                        case 'u' when p + 5 < json.Length:
                            if (int.TryParse(json.Substring(p + 2, 4), NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                p += 5;
                            }
                            break;
                        default: sb.Append(c); break;
                    }
                }
                else if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return false;
        }

        private static bool TryExtractArray(string json, string key, out string? arrayBody)
        {
            arrayBody = null;
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf('[', i);
            if (i < 0) return false;
            int depth = 0;
            for (int p = i; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        arrayBody = json.Substring(i + 1, p - i - 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<string> SplitTopLevelObjects(string arrayBody)
        {
            var list = new List<string>();
            int depth = 0;
            int start = -1;
            for (int i = 0; i < arrayBody.Length; i++)
            {
                char c = arrayBody[i];
                if (c == '{')
                {
                    if (depth == 0)
                        start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        list.Add(arrayBody.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return list;
        }

        private static string EscapeJsonString(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
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
                        if (c < ' ')
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}

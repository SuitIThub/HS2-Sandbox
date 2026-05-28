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
    internal sealed class PoseBrowserHistoryEntry
    {
        public long UtcTicks;
        public string FromPoseLabel = "";
        public string ToPoseLabel = "";
        public PoseCharacterSnapshot Snapshot = new PoseCharacterSnapshot();

        public DateTime TimestampUtc => new DateTime(UtcTicks, DateTimeKind.Utc);

        public string FormatTimestampLocal() =>
            TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

        public string SummaryLine =>
            string.IsNullOrEmpty(FromPoseLabel) && string.IsNullOrEmpty(ToPoseLabel)
                ? FormatTimestampLocal()
                : $"{FromPoseLabel} → {ToPoseLabel}";
    }

    internal sealed class PoseBrowserCharacterTimeline
    {
        public int DicKey;
        public string DisplayName = "";
        public readonly List<PoseBrowserHistoryEntry> Entries = new List<PoseBrowserHistoryEntry>();
        /// <summary>Index of the snapshot that matches the current scene state for this character (-1 = none).</summary>
        public int CursorIndex = -1;
    }

    /// <summary>
    /// Per-character pose/position/rotation history with undo/redo and session persistence.
    /// </summary>
    internal sealed class PoseBrowserHistory
    {
        public const int FormatVersion = 1;
        public const int DefaultMaxEntriesPerCharacter = 500;

        private readonly Dictionary<int, PoseBrowserCharacterTimeline> _timelines =
            new Dictionary<int, PoseBrowserCharacterTimeline>();

        private int _suppressDepth;
        private bool _dirty;

        public bool IsSuppressed => _suppressDepth > 0;

        public static string GetDefaultPath() =>
            Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_browser_history.json");

        public void LoadFromDisk()
        {
            _timelines.Clear();
            string path = GetDefaultPath();
            if (!File.Exists(path))
                return;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!TryParseV1(json, out var timelines))
                    return;
                foreach (var tl in timelines)
                    _timelines[tl.DicKey] = tl;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not load pose history: {ex.Message}");
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
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not save pose history: {ex.Message}");
            }
        }

        public void TrimAllTimelines(int maxEntriesPerCharacter)
        {
            int max = Mathf.Max(1, maxEntriesPerCharacter);
            foreach (var tl in _timelines.Values)
                TrimTimeline(tl, max);
        }

        private static void TrimTimeline(PoseBrowserCharacterTimeline tl, int max)
        {
            int remove = tl.Entries.Count - max;
            if (remove <= 0)
                return;

            tl.Entries.RemoveRange(0, remove);
            tl.CursorIndex -= remove;
            if (tl.CursorIndex < -1)
                tl.CursorIndex = -1;
            if (tl.CursorIndex >= tl.Entries.Count)
                tl.CursorIndex = tl.Entries.Count - 1;
        }

        /// <summary>Records pre-apply snapshots for selected characters. Skips when unchanged from the current cursor entry.</summary>
        public void RecordBeforePoseApply(IEnumerable<OCIChar> characters, string toPoseLabel)
        {
            if (IsSuppressed)
                return;

            string targetLabel = NormalizePoseLabel(toPoseLabel);
            foreach (var oci in characters)
            {
                if (!PoseDataService.TryGetDicKey(oci, out int dicKey))
                    continue;
                if (!PoseDataService.TryCaptureCharacterSnapshot(oci, out var snapshot))
                    continue;

                var tl = GetOrCreateTimeline(dicKey, PoseDataService.GetOCICharDisplayName(oci));
                string fromLabel = GetCurrentPoseLabel(tl);
                if (tl.CursorIndex >= 0 && tl.CursorIndex < tl.Entries.Count &&
                    snapshot.StateEquals(tl.Entries[tl.CursorIndex].Snapshot))
                    continue;

                TruncateRedoBranch(tl);
                tl.Entries.Add(new PoseBrowserHistoryEntry
                {
                    UtcTicks = DateTime.UtcNow.Ticks,
                    FromPoseLabel = fromLabel,
                    ToPoseLabel = targetLabel,
                    Snapshot = snapshot
                });
                tl.CursorIndex = tl.Entries.Count - 1;
                _dirty = true;
            }
        }

        /// <summary>Records post-apply snapshots (always appended after a pose apply).</summary>
        public void RecordAfterPoseApply(IEnumerable<OCIChar> characters, string appliedPoseLabel)
        {
            if (IsSuppressed)
                return;

            string label = NormalizePoseLabel(appliedPoseLabel);
            foreach (var oci in characters)
            {
                if (!PoseDataService.TryGetDicKey(oci, out int dicKey))
                    continue;
                if (!PoseDataService.TryCaptureCharacterSnapshot(oci, out var snapshot))
                    continue;

                var tl = GetOrCreateTimeline(dicKey, PoseDataService.GetOCICharDisplayName(oci));
                TruncateRedoBranch(tl);
                tl.Entries.Add(new PoseBrowserHistoryEntry
                {
                    UtcTicks = DateTime.UtcNow.Ticks,
                    FromPoseLabel = label,
                    ToPoseLabel = label,
                    Snapshot = snapshot
                });
                tl.CursorIndex = tl.Entries.Count - 1;
                _dirty = true;
            }
        }

        public void RecordBeforePoseApplyPlan(
            IEnumerable<(OCIChar character, string toPoseLabel)> assignments)
        {
            if (IsSuppressed)
                return;

            foreach (var group in assignments.GroupBy(a => a.character))
            {
                var oci = group.Key;
                if (oci == null)
                    continue;
                string toLabel = group.Last().toPoseLabel;
                RecordBeforePoseApply(new[] { oci }, toLabel);
            }
        }

        public void RecordAfterPoseApplyPlan(
            IEnumerable<(OCIChar character, string appliedPoseLabel)> assignments)
        {
            if (IsSuppressed)
                return;

            foreach (var group in assignments.GroupBy(a => a.character))
            {
                var oci = group.Key;
                if (oci == null)
                    continue;
                string label = group.Last().appliedPoseLabel;
                RecordAfterPoseApply(new[] { oci }, label);
            }
        }

        public bool CanUndo(IEnumerable<OCIChar> selected) =>
            GetSelectedTimelines(selected).Any(t => t.CursorIndex > 0);

        public bool CanRedo(IEnumerable<OCIChar> selected) =>
            GetSelectedTimelines(selected).Any(t =>
                t.CursorIndex >= 0 && t.CursorIndex < t.Entries.Count - 1);

        public void Undo(PoseDataService dataService, IEnumerable<OCIChar> selected, int maxEntriesPerCharacter)
        {
            if (IsSuppressed)
                return;

            using var scope = BeginSuppress();
            bool any = false;
            foreach (var tl in GetSelectedTimelines(selected))
            {
                if (tl.CursorIndex <= 0)
                    continue;
                tl.CursorIndex--;
                if (!TryResolveCharacter(tl, out var oci))
                    continue;
                var entry = tl.Entries[tl.CursorIndex];
                PoseDataService.TryRestoreCharacterSnapshot(oci, entry.Snapshot, true, true, true);
                any = true;
            }

            if (any)
            {
                TrimAllTimelines(maxEntriesPerCharacter);
                _dirty = true;
            }
        }

        public void Redo(PoseDataService dataService, IEnumerable<OCIChar> selected, int maxEntriesPerCharacter)
        {
            if (IsSuppressed)
                return;

            using var scope = BeginSuppress();
            bool any = false;
            foreach (var tl in GetSelectedTimelines(selected))
            {
                if (tl.CursorIndex < 0 || tl.CursorIndex >= tl.Entries.Count - 1)
                    continue;
                tl.CursorIndex++;
                if (!TryResolveCharacter(tl, out var oci))
                    continue;
                var entry = tl.Entries[tl.CursorIndex];
                PoseDataService.TryRestoreCharacterSnapshot(oci, entry.Snapshot, true, true, true);
                any = true;
            }

            if (any)
            {
                TrimAllTimelines(maxEntriesPerCharacter);
                _dirty = true;
            }
        }

        public void JumpToEntry(
            PoseDataService dataService,
            OCIChar oci,
            int entryIndex,
            bool restorePose,
            bool restorePosition,
            bool restoreRotation,
            int maxEntriesPerCharacter)
        {
            if (!PoseDataService.TryGetDicKey(oci, out int dicKey))
                return;
            if (!_timelines.TryGetValue(dicKey, out var tl))
                return;
            if (entryIndex < 0 || entryIndex >= tl.Entries.Count)
                return;

            using var scope = BeginSuppress();
            tl.CursorIndex = entryIndex;
            var entry = tl.Entries[entryIndex];
            PoseDataService.TryRestoreCharacterSnapshot(oci, entry.Snapshot, restorePose, restorePosition, restoreRotation);
            TrimAllTimelines(maxEntriesPerCharacter);
            _dirty = true;
        }

        public IReadOnlyList<PoseBrowserCharacterTimeline> GetTimelinesForSelected(IEnumerable<OCIChar> selected)
        {
            var list = new List<PoseBrowserCharacterTimeline>();
            var seen = new HashSet<int>();
            foreach (var oci in selected)
            {
                if (!PoseDataService.TryGetDicKey(oci, out int dicKey))
                    continue;
                if (!seen.Add(dicKey))
                    continue;
                if (_timelines.TryGetValue(dicKey, out var tl))
                    list.Add(tl);
            }

            list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public IDisposable BeginSuppress()
        {
            _suppressDepth++;
            return new SuppressScope(this);
        }

        private sealed class SuppressScope : IDisposable
        {
            private readonly PoseBrowserHistory _owner;
            public SuppressScope(PoseBrowserHistory owner) => _owner = owner;
            public void Dispose() => _owner._suppressDepth--;
        }

        private IEnumerable<PoseBrowserCharacterTimeline> GetSelectedTimelines(IEnumerable<OCIChar> selected)
        {
            var seen = new HashSet<int>();
            foreach (var oci in selected)
            {
                if (!PoseDataService.TryGetDicKey(oci, out int dicKey))
                    continue;
                if (!seen.Add(dicKey))
                    continue;
                if (_timelines.TryGetValue(dicKey, out var tl))
                    yield return tl;
            }
        }

        private static bool TryResolveCharacter(PoseBrowserCharacterTimeline tl, out OCIChar oci)
        {
            oci = null;
            try
            {
                if (Singleton<Studio.Studio>.Instance.dicObjectCtrl.TryGetValue(tl.DicKey, out var info) &&
                    info is OCIChar byKey)
                {
                    oci = byKey;
                    return true;
                }

                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar c &&
                        string.Equals(PoseDataService.GetOCICharDisplayName(c), tl.DisplayName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        oci = c;
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private PoseBrowserCharacterTimeline GetOrCreateTimeline(int dicKey, string displayName)
        {
            if (!_timelines.TryGetValue(dicKey, out var tl))
            {
                tl = new PoseBrowserCharacterTimeline { DicKey = dicKey, DisplayName = displayName };
                _timelines[dicKey] = tl;
            }
            else if (!string.IsNullOrWhiteSpace(displayName))
            {
                tl.DisplayName = displayName;
            }

            return tl;
        }

        private static void TruncateRedoBranch(PoseBrowserCharacterTimeline tl)
        {
            if (tl.CursorIndex < 0 || tl.CursorIndex >= tl.Entries.Count - 1)
                return;
            int removeFrom = tl.CursorIndex + 1;
            int removeCount = tl.Entries.Count - removeFrom;
            if (removeCount > 0)
                tl.Entries.RemoveRange(removeFrom, removeCount);
        }

        private static string GetCurrentPoseLabel(PoseBrowserCharacterTimeline tl)
        {
            if (tl.CursorIndex >= 0 && tl.CursorIndex < tl.Entries.Count)
                return tl.Entries[tl.CursorIndex].ToPoseLabel;
            return "(scene)";
        }

        private static string NormalizePoseLabel(string? label) =>
            string.IsNullOrWhiteSpace(label) ? "(unnamed pose)" : label.Trim();

        private string BuildJsonV1()
        {
            var sb = new StringBuilder();
            sb.Append("{\"version\":").Append(FormatVersion).Append(",\"characters\":[");
            bool firstChar = true;
            foreach (var tl in _timelines.Values.OrderBy(t => t.DicKey))
            {
                if (!firstChar) sb.Append(',');
                firstChar = false;
                AppendTimelineObject(sb, tl);
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendTimelineObject(StringBuilder sb, PoseBrowserCharacterTimeline tl)
        {
            sb.Append("{\"dicKey\":").Append(tl.DicKey)
                .Append(",\"displayName\":\"").Append(EscapeJsonString(tl.DisplayName))
                .Append("\",\"cursor\":").Append(tl.CursorIndex)
                .Append(",\"entries\":[");
            for (int i = 0; i < tl.Entries.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendEntryObject(sb, tl.Entries[i]);
            }

            sb.Append("]}");
        }

        private static void AppendEntryObject(StringBuilder sb, PoseBrowserHistoryEntry e)
        {
            var s = e.Snapshot;
            sb.Append("{\"utcTicks\":").Append(e.UtcTicks)
                .Append(",\"from\":\"").Append(EscapeJsonString(e.FromPoseLabel))
                .Append("\",\"to\":\"").Append(EscapeJsonString(e.ToPoseLabel))
                .Append("\",\"hasPos\":").Append(s.HasPosition ? "true" : "false")
                .Append(",\"hasRot\":").Append(s.HasRotation ? "true" : "false");
            if (s.HasPosition)
            {
                sb.Append(",\"px\":").Append(s.Position.x.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"py\":").Append(s.Position.y.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"pz\":").Append(s.Position.z.ToString(CultureInfo.InvariantCulture));
            }

            if (s.HasRotation)
            {
                sb.Append(",\"rx\":").Append(s.Rotation.x.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"ry\":").Append(s.Rotation.y.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"rz\":").Append(s.Rotation.z.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"rw\":").Append(s.Rotation.w.ToString(CultureInfo.InvariantCulture));
            }

            sb.Append(",\"pose\":\"").Append(Convert.ToBase64String(s.PoseData)).Append("\"}");
        }

        private static bool TryParseV1(string json, out List<PoseBrowserCharacterTimeline> timelines)
        {
            timelines = new List<PoseBrowserCharacterTimeline>();
            if (string.IsNullOrWhiteSpace(json))
                return false;

            if (!TryReadIntField(json, "\"version\"", out int version) || version != FormatVersion)
                return false;

            if (!TryExtractArray(json, "\"characters\"", out string? charsArray) || charsArray == null)
                return false;

            foreach (string obj in SplitTopLevelObjects(charsArray))
            {
                if (!TryParseTimeline(obj, out var tl))
                    continue;
                timelines.Add(tl);
            }

            return timelines.Count > 0;
        }

        private static bool TryParseTimeline(string obj, out PoseBrowserCharacterTimeline tl)
        {
            tl = new PoseBrowserCharacterTimeline();
            if (!TryReadIntField(obj, "\"dicKey\"", out tl.DicKey))
                return false;
            TryReadStringField(obj, "\"displayName\"", out tl.DisplayName);
            TryReadIntField(obj, "\"cursor\"", out tl.CursorIndex);
            if (!TryExtractArray(obj, "\"entries\"", out string? entriesJson) || entriesJson == null)
                return true;

            foreach (string entryObj in SplitTopLevelObjects(entriesJson))
            {
                if (TryParseEntry(entryObj, out var entry))
                    tl.Entries.Add(entry);
            }

            if (tl.CursorIndex >= tl.Entries.Count)
                tl.CursorIndex = tl.Entries.Count - 1;
            return true;
        }

        private static bool TryParseEntry(string obj, out PoseBrowserHistoryEntry entry)
        {
            entry = new PoseBrowserHistoryEntry();
            if (!TryReadLongField(obj, "\"utcTicks\"", out entry.UtcTicks))
                return false;
            TryReadStringField(obj, "\"from\"", out entry.FromPoseLabel);
            TryReadStringField(obj, "\"to\"", out entry.ToPoseLabel);

            var snap = entry.Snapshot;
            TryReadBoolField(obj, "\"hasPos\"", out snap.HasPosition);
            TryReadBoolField(obj, "\"hasRot\"", out snap.HasRotation);
            if (snap.HasPosition)
            {
                TryReadFloatField(obj, "\"px\"", out float px);
                TryReadFloatField(obj, "\"py\"", out float py);
                TryReadFloatField(obj, "\"pz\"", out float pz);
                snap.Position = new Vector3(px, py, pz);
            }

            if (snap.HasRotation)
            {
                TryReadFloatField(obj, "\"rx\"", out float rx);
                TryReadFloatField(obj, "\"ry\"", out float ry);
                TryReadFloatField(obj, "\"rz\"", out float rz);
                TryReadFloatField(obj, "\"rw\"", out float rw);
                snap.Rotation = new Quaternion(rx, ry, rz, rw);
            }

            if (!TryReadStringField(obj, "\"pose\"", out string? b64) || string.IsNullOrEmpty(b64))
                return false;
            try
            {
                snap.PoseData = Convert.FromBase64String(b64);
            }
            catch
            {
                return false;
            }

            return snap.PoseData.Length > 0;
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

        private static bool TryReadFloatField(string json, string key, out float value)
        {
            value = 0f;
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i = json.IndexOf(':', i) + 1;
            int end = i;
            while (end < json.Length && "0123456789.-eE+".IndexOf(json[end]) >= 0)
                end++;
            return end > i && float.TryParse(json.Substring(i, end - i), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

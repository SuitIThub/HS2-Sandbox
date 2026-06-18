using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class StudioCharacterPriorityStorage
    {
        public static string SharedConfigPath =>
            PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "studio_character_priority.json");

        public static string PoseBrowserLegacyPath =>
            PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_browser_character_config.json");
    }

    /// <summary>Persisted priority list shared across Sandbox modules.</summary>
    public sealed class StudioCharacterPriorityList : IStudioCharacterPriorityList
    {
        private const int CurrentVersion = 3;

        private readonly string _storagePath;
        private readonly string? _legacyImportPath;
        private readonly List<StudioCharacterSlot> _priority = new List<StudioCharacterSlot>();

        public StudioCharacterPriorityList()
            : this(StudioCharacterPriorityStorage.SharedConfigPath, StudioCharacterPriorityStorage.PoseBrowserLegacyPath)
        {
        }

        public StudioCharacterPriorityList(string storagePath, string? legacyImportPath = null)
        {
            _storagePath = storagePath;
            _legacyImportPath = legacyImportPath;
            LoadFromDisk();
        }

        public IList<StudioCharacterSlot> Priority => _priority;

        public void ReloadFromDisk() => LoadFromDisk();

        public void LoadFromDisk()
        {
            _priority.Clear();
            try
            {
                if (TryLoadFromPath(_storagePath))
                    return;

                if (!string.IsNullOrEmpty(_legacyImportPath) &&
                    !string.Equals(_legacyImportPath, _storagePath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(_legacyImportPath) &&
                    TryLoadFromPath(_legacyImportPath))
                {
                    SaveToDisk();
                    return;
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("StudioCharacterPriority: Could not load character config: " + ex.Message);
            }
        }

        private bool TryLoadFromPath(string path)
        {
            if (!File.Exists(path))
                return false;

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (TryLoadV3(json))
                return true;

            var data = JsonUtility.FromJson<StudioCharacterConfigFileV2>(json);
            if (data == null)
                return false;

            ImportLegacyV2(data);
            return true;
        }

        public void SaveToDisk()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                FileEx.WriteAllTextAtomic(
                    _storagePath,
                    BuildJsonV3(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("StudioCharacterPriority: Could not save character config: " + ex.Message);
            }
        }

        public void LoadFromScene(IEnumerable<OciDicKeyPair> sceneCharacters)
        {
            bool changed = false;
            var claimedInScene = new HashSet<OCIChar>();

            foreach (StudioCharacterSlot slot in _priority)
            {
                if (!StudioCharacterSlot.TryResolveInScene(slot, out OCIChar oci))
                    continue;
                if (!claimedInScene.Add(oci))
                    continue;
                if (StudioCharacterSelection.TryGetDicKey(oci, out int dicKey) &&
                    StudioCharacterSlot.RefreshIdentityFromScene(slot, oci, dicKey))
                    changed = true;
            }

            foreach (OciDicKeyPair pair in sceneCharacters)
            {
                if (claimedInScene.Contains(pair.Oci))
                    continue;

                _priority.Add(StudioCharacterSlot.FromScene(pair.Oci, pair.DicKey));
                claimedInScene.Add(pair.Oci);
                changed = true;
            }

            if (changed)
                SaveToDisk();
        }

        public int RemoveSlotsNotInScene()
        {
            int before = _priority.Count;
            _priority.RemoveAll(slot => !StudioCharacterSlot.TryResolveInScene(slot, out _));
            int removed = before - _priority.Count;
            if (removed > 0)
                SaveToDisk();
            return removed;
        }

        public void MoveSlot(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _priority.Count || target < 0 || target >= _priority.Count)
                return;
            var item = _priority[index];
            _priority.RemoveAt(index);
            _priority.Insert(target, item);
            SaveToDisk();
        }

        public void ToggleSlotGender(int index)
        {
            if (index < 0 || index >= _priority.Count)
                return;
            _priority[index].IsFemale = !_priority[index].IsFemale;
            SaveToDisk();
        }

        public void RemoveSlot(int index)
        {
            if (index < 0 || index >= _priority.Count)
                return;
            _priority.RemoveAt(index);
            SaveToDisk();
        }

        private bool TryLoadV3(string json)
        {
            if (!TryReadIntField(json, "version", out int version) || version < CurrentVersion)
                return false;

            int arrayStart = json.IndexOf("\"characters\"", StringComparison.Ordinal);
            if (arrayStart < 0)
                return false;

            arrayStart = json.IndexOf('[', arrayStart);
            if (arrayStart < 0)
                return false;

            if (!TryParseCharacterArray(json, arrayStart, out var slots))
                return false;

            _priority.Clear();
            _priority.AddRange(slots);
            return true;
        }

        private static bool TryParseCharacterArray(string json, int arrayStart, out List<StudioCharacterSlot> slots)
        {
            slots = new List<StudioCharacterSlot>();
            int i = arrayStart + 1;
            while (i < json.Length)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i]))
                    i++;
                if (i >= json.Length || json[i] == ']')
                    break;
                if (json[i] != '{')
                    return false;

                int objEnd = FindMatchingBrace(json, i);
                if (objEnd < 0)
                    return false;

                string obj = json.Substring(i, objEnd - i + 1);
                if (!TryReadIntField(obj, "dicKey", out int dicKey))
                    return false;
                if (!TryReadStringField(obj, "displayName", out string? displayName))
                    displayName = string.Empty;
                if (!TryReadBoolField(obj, "isFemale", out bool isFemale))
                    isFemale = false;

                slots.Add(new StudioCharacterSlot
                {
                    DicKey = dicKey,
                    DisplayName = displayName ?? string.Empty,
                    IsFemale = isFemale
                });

                i = objEnd + 1;
                while (i < json.Length && char.IsWhiteSpace(json[i]))
                    i++;
                if (i < json.Length && json[i] == ',')
                    i++;
            }

            return true;
        }

        private void ImportLegacyV2(StudioCharacterConfigFileV2 data)
        {
            _priority.Clear();
            var male = data.male ?? new StudioCharacterSlotPersisted[0];
            var female = data.female ?? new StudioCharacterSlotPersisted[0];
            int maxRank = Math.Max(male.Length, female.Length);
            for (int r = 0; r < maxRank; r++)
            {
                if (data.untaggedInterleaveFemaleFirst)
                {
                    if (r < female.Length)
                        _priority.Add(LegacySlot(female[r], isFemale: true));
                    if (r < male.Length)
                        _priority.Add(LegacySlot(male[r], isFemale: false));
                }
                else
                {
                    if (r < male.Length)
                        _priority.Add(LegacySlot(male[r], isFemale: false));
                    if (r < female.Length)
                        _priority.Add(LegacySlot(female[r], isFemale: true));
                }
            }
        }

        private static StudioCharacterSlot LegacySlot(StudioCharacterSlotPersisted p, bool isFemale)
        {
            var slot = StudioCharacterSlot.FromPersisted(p);
            slot.IsFemale = isFemale;
            return slot;
        }

        private string BuildJsonV3()
        {
            var sb = new StringBuilder(256 + _priority.Count * 64);
            sb.Append("{\n  \"version\": ").Append(CurrentVersion).Append(",\n  \"characters\": [\n");
            for (int i = 0; i < _priority.Count; i++)
            {
                var p = _priority[i].ToPersisted();
                if (i > 0)
                    sb.Append(",\n");
                sb.Append("    {\"dicKey\":").Append(p.dicKey)
                    .Append(",\"displayName\":\"").Append(EscapeJsonString(p.displayName))
                    .Append("\",\"isFemale\":").Append(p.isFemale ? "true" : "false").Append('}');
            }

            sb.Append("\n  ]\n}");
            return sb.ToString();
        }

        private static string EscapeJsonString(string? s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
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
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static int FindMatchingBrace(string json, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            for (int i = openIndex; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        i++;
                        continue;
                    }

                    if (c == '"')
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

        private static bool TryReadIntField(string json, string field, out int value)
        {
            value = 0;
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0)
                return false;
            idx = json.IndexOf(':', idx + key.Length);
            if (idx < 0)
                return false;
            idx++;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
                idx++;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            return end > idx && int.TryParse(json.Substring(idx, end - idx), out value);
        }

        private static bool TryReadBoolField(string json, string field, out bool value)
        {
            value = false;
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0)
                return false;
            idx = json.IndexOf(':', idx + key.Length);
            if (idx < 0)
                return false;
            int trueIdx = json.IndexOf("true", idx, StringComparison.Ordinal);
            int falseIdx = json.IndexOf("false", idx, StringComparison.Ordinal);
            if (trueIdx < 0 && falseIdx < 0)
                return false;
            if (trueIdx >= 0 && (falseIdx < 0 || trueIdx < falseIdx))
            {
                value = true;
                return true;
            }

            value = false;
            return falseIdx >= 0;
        }

        private static bool TryReadStringField(string json, string field, out string? value)
        {
            value = null;
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0)
                return false;
            idx = json.IndexOf(':', idx + key.Length);
            if (idx < 0)
                return false;
            idx++;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
                idx++;
            if (idx >= json.Length || json[idx] != '"')
                return false;
            idx++;
            var sb = new StringBuilder();
            while (idx < json.Length)
            {
                char c = json[idx++];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }

                if (c == '\\' && idx < json.Length)
                {
                    char esc = json[idx++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u' when idx + 3 < json.Length:
                            if (int.TryParse(json.Substring(idx, 4),
                                    System.Globalization.NumberStyles.HexNumber, null, out int code))
                            {
                                sb.Append((char)code);
                                idx += 4;
                            }

                            break;
                        default: sb.Append(esc); break;
                    }

                    continue;
                }

                sb.Append(c);
            }

            return false;
        }
    }
}

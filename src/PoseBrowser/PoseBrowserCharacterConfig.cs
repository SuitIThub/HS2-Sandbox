using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    [Serializable]
    internal sealed class PoseBrowserCharacterSlotPersisted
    {
        public int dicKey;
        public string displayName = "";
        public bool isFemale;
    }

    /// <summary>Legacy v2 on-disk shape (JsonUtility import only).</summary>
    [Serializable]
    internal sealed class PoseBrowserCharacterConfigFileV2
    {
        public int version = 2;
        public PoseBrowserCharacterSlotPersisted[] male = new PoseBrowserCharacterSlotPersisted[0];
        public PoseBrowserCharacterSlotPersisted[] female = new PoseBrowserCharacterSlotPersisted[0];
        public bool untaggedInterleaveFemaleFirst;
    }

    internal sealed class PoseBrowserCharacterSlot
    {
        public int DicKey { get; set; }
        public string DisplayName { get; set; } = "";
        public bool IsFemale { get; set; }

        public static bool TryResolveInScene(PoseBrowserCharacterSlot slot, out OCIChar oci)
        {
            oci = null;
            if (StringEx.IsNullOrWhiteSpace(slot.DisplayName) && slot.DicKey == 0)
                return false;

            try
            {
                if (Singleton<Studio.Studio>.Instance.dicObjectCtrl.TryGetValue(slot.DicKey, out var info) &&
                    info is OCIChar byKey &&
                    NamesMatch(slot, byKey))
                {
                    oci = byKey;
                    return true;
                }

                if (StringEx.IsNullOrWhiteSpace(slot.DisplayName))
                    return false;

                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar c && NamesMatch(slot, c))
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

        private static bool NamesMatch(PoseBrowserCharacterSlot slot, OCIChar oci) =>
            string.Equals(
                slot.DisplayName,
                PoseDataService.GetOCICharDisplayName(oci),
                StringComparison.OrdinalIgnoreCase);

        /// <summary>Updates stored dicKey/name when the slot still tracks the same character in the scene.</summary>
        public static bool RefreshIdentityFromScene(PoseBrowserCharacterSlot slot, OCIChar oci, int dicKey)
        {
            string name = PoseDataService.GetOCICharDisplayName(oci);
            bool changed = slot.DicKey != dicKey ||
                           !string.Equals(slot.DisplayName, name, StringComparison.OrdinalIgnoreCase);
            if (!changed)
                return false;

            slot.DicKey = dicKey;
            slot.DisplayName = name;
            return true;
        }

        public static PoseBrowserCharacterSlot FromScene(OCIChar oci, int dicKey) =>
            new PoseBrowserCharacterSlot
            {
                DicKey = dicKey,
                DisplayName = PoseDataService.GetOCICharDisplayName(oci),
                IsFemale = PoseDataService.IsFemaleCharacter(oci)
            };

        public PoseBrowserCharacterSlotPersisted ToPersisted() =>
            new PoseBrowserCharacterSlotPersisted
            {
                dicKey = DicKey,
                displayName = DisplayName ?? "",
                isFemale = IsFemale
            };

        public static PoseBrowserCharacterSlot FromPersisted(PoseBrowserCharacterSlotPersisted p) =>
            new PoseBrowserCharacterSlot
            {
                DicKey = p.dicKey,
                DisplayName = p.displayName ?? "",
                IsFemale = p.isFemale
            };
    }

    internal sealed class PoseBrowserCharacterConfig
    {
        private const int CurrentVersion = 3;

        private static string StoragePath =>
            PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_browser_character_config.json");

        private readonly List<PoseBrowserCharacterSlot> _priority = new List<PoseBrowserCharacterSlot>();

        public IList<PoseBrowserCharacterSlot> Priority => _priority;

        public PoseBrowserCharacterConfig()
        {
            LoadFromDisk();
        }

        public void LoadFromDisk()
        {
            _priority.Clear();
            try
            {
                if (!File.Exists(StoragePath)) return;
                string json = File.ReadAllText(StoragePath, Encoding.UTF8);
                if (TryLoadV3(json))
                    return;

                var data = JsonUtility.FromJson<PoseBrowserCharacterConfigFileV2>(json);
                if (data == null) return;
                ImportLegacyV2(data);
                SaveToDisk();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not load character config: {ex.Message}");
            }
        }

        public void SaveToDisk()
        {
            try
            {
                string? dir = Path.GetDirectoryName(StoragePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                FileEx.WriteAllTextAtomic(
                    StoragePath,
                    BuildJsonV3(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not save character config: {ex.Message}");
            }
        }

        public void LoadNewFromScene(IEnumerable<OciDicKeyPair> sceneCharacters)
        {
            bool changed = false;
            var claimedInScene = new HashSet<OCIChar>();

            foreach (PoseBrowserCharacterSlot slot in _priority)
            {
                if (!PoseBrowserCharacterSlot.TryResolveInScene(slot, out OCIChar oci))
                    continue;
                if (!claimedInScene.Add(oci))
                    continue;
                if (PoseDataService.TryGetDicKey(oci, out int dicKey) &&
                    PoseBrowserCharacterSlot.RefreshIdentityFromScene(slot, oci, dicKey))
                    changed = true;
            }

            foreach (OciDicKeyPair pair in sceneCharacters)
            {
                if (claimedInScene.Contains(pair.Oci))
                    continue;

                _priority.Add(PoseBrowserCharacterSlot.FromScene(pair.Oci, pair.DicKey));
                claimedInScene.Add(pair.Oci);
                changed = true;
            }

            if (changed)
                SaveToDisk();
        }

        public int RemoveSlotsNotInScene()
        {
            int before = _priority.Count;
            _priority.RemoveAll(slot => !PoseBrowserCharacterSlot.TryResolveInScene(slot, out _));
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
            if (index < 0 || index >= _priority.Count) return;
            _priority[index].IsFemale = !_priority[index].IsFemale;
            SaveToDisk();
        }

        public void RemoveSlot(int index)
        {
            if (index < 0 || index >= _priority.Count) return;
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

        private static bool TryParseCharacterArray(string json, int arrayStart, out List<PoseBrowserCharacterSlot> slots)
        {
            slots = new List<PoseBrowserCharacterSlot>();
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
                    displayName = "";
                if (!TryReadBoolField(obj, "isFemale", out bool isFemale))
                    isFemale = false;

                slots.Add(new PoseBrowserCharacterSlot
                {
                    DicKey = dicKey,
                    DisplayName = displayName ?? "",
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

        private void ImportLegacyV2(PoseBrowserCharacterConfigFileV2 data)
        {
            _priority.Clear();
            var male = data.male ?? new PoseBrowserCharacterSlotPersisted[0];
            var female = data.female ?? new PoseBrowserCharacterSlotPersisted[0];
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

        private static PoseBrowserCharacterSlot LegacySlot(PoseBrowserCharacterSlotPersisted p, bool isFemale)
        {
            var slot = PoseBrowserCharacterSlot.FromPersisted(p);
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
            if (string.IsNullOrEmpty(s)) return "";
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

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
    internal enum PoseBrowserCharacterListKind
    {
        Male = 0,
        Female = 1
    }

    [Serializable]
    internal sealed class PoseBrowserCharacterSlotPersisted
    {
        public int dicKey;
        public string displayName = "";
    }

    [Serializable]
    internal sealed class PoseBrowserCharacterConfigFile
    {
        public int version = 1;
        public PoseBrowserCharacterSlotPersisted[] male = Array.Empty<PoseBrowserCharacterSlotPersisted>();
        public PoseBrowserCharacterSlotPersisted[] female = Array.Empty<PoseBrowserCharacterSlotPersisted>();
    }

    internal sealed class PoseBrowserCharacterSlot
    {
        public int DicKey { get; set; }
        public string DisplayName { get; set; } = "";

        public static bool TryResolveInScene(PoseBrowserCharacterSlot slot, out OCIChar oci)
        {
            oci = null;
            try
            {
                if (Singleton<Studio.Studio>.Instance.dicObjectCtrl.TryGetValue(slot.DicKey, out var info) &&
                    info is OCIChar byKey)
                {
                    oci = byKey;
                    return true;
                }

                foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
                {
                    if (kvp.Value is OCIChar c &&
                        string.Equals(PoseDataService.GetOCICharDisplayName(c), slot.DisplayName,
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

        public static PoseBrowserCharacterSlot FromScene(OCIChar oci, int dicKey) =>
            new PoseBrowserCharacterSlot
            {
                DicKey = dicKey,
                DisplayName = PoseDataService.GetOCICharDisplayName(oci)
            };

        public PoseBrowserCharacterSlotPersisted ToPersisted() =>
            new PoseBrowserCharacterSlotPersisted { dicKey = DicKey, displayName = DisplayName ?? "" };

        public static PoseBrowserCharacterSlot FromPersisted(PoseBrowserCharacterSlotPersisted p) =>
            new PoseBrowserCharacterSlot { DicKey = p.dicKey, DisplayName = p.displayName ?? "" };
    }

    internal sealed class PoseBrowserCharacterConfig
    {
        private static string StoragePath =>
            Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "pose_browser_character_config.json");

        private readonly List<PoseBrowserCharacterSlot> _male = new List<PoseBrowserCharacterSlot>();
        private readonly List<PoseBrowserCharacterSlot> _female = new List<PoseBrowserCharacterSlot>();

        public IReadOnlyList<PoseBrowserCharacterSlot> Male => _male;
        public IReadOnlyList<PoseBrowserCharacterSlot> Female => _female;

        public PoseBrowserCharacterConfig()
        {
            LoadFromDisk();
        }

        public IReadOnlyList<PoseBrowserCharacterSlot> GetList(PoseBrowserCharacterListKind kind) =>
            kind == PoseBrowserCharacterListKind.Male ? _male : _female;

        private List<PoseBrowserCharacterSlot> GetMutableList(PoseBrowserCharacterListKind kind) =>
            kind == PoseBrowserCharacterListKind.Male ? _male : _female;

        public void LoadFromDisk()
        {
            _male.Clear();
            _female.Clear();
            try
            {
                if (!File.Exists(StoragePath)) return;
                string json = File.ReadAllText(StoragePath, Encoding.UTF8);
                var data = JsonUtility.FromJson<PoseBrowserCharacterConfigFile>(json);
                if (data == null) return;
                if (data.male != null)
                    _male.AddRange(data.male.Select(PoseBrowserCharacterSlot.FromPersisted));
                if (data.female != null)
                    _female.AddRange(data.female.Select(PoseBrowserCharacterSlot.FromPersisted));
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

                var data = new PoseBrowserCharacterConfigFile
                {
                    version = 1,
                    male = _male.Select(s => s.ToPersisted()).ToArray(),
                    female = _female.Select(s => s.ToPersisted()).ToArray()
                };
                File.WriteAllText(StoragePath, JsonUtility.ToJson(data, true),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not save character config: {ex.Message}");
            }
        }

        public bool ContainsDicKey(int dicKey) =>
            _male.Any(s => s.DicKey == dicKey) || _female.Any(s => s.DicKey == dicKey);

        public void LoadNewFromScene(IEnumerable<(OCIChar oci, int dicKey)> sceneCharacters)
        {
            bool changed = false;
            foreach (var (oci, dicKey) in sceneCharacters)
            {
                if (ContainsDicKey(dicKey)) continue;
                var slot = PoseBrowserCharacterSlot.FromScene(oci, dicKey);
                if (PoseDataService.IsFemaleCharacter(oci))
                    _female.Add(slot);
                else
                    _male.Add(slot);
                changed = true;
            }

            if (changed)
                SaveToDisk();
        }

        public void MoveSlot(PoseBrowserCharacterListKind list, int index, int delta)
        {
            var listRef = GetMutableList(list);
            int target = index + delta;
            if (index < 0 || index >= listRef.Count || target < 0 || target >= listRef.Count)
                return;
            var item = listRef[index];
            listRef.RemoveAt(index);
            listRef.Insert(target, item);
            SaveToDisk();
        }

        public void TransferSlot(PoseBrowserCharacterListKind from, int index)
        {
            var src = GetMutableList(from);
            if (index < 0 || index >= src.Count) return;
            var destKind = from == PoseBrowserCharacterListKind.Male
                ? PoseBrowserCharacterListKind.Female
                : PoseBrowserCharacterListKind.Male;
            var dest = GetMutableList(destKind);
            var slot = src[index];
            src.RemoveAt(index);
            dest.Add(slot);
            SaveToDisk();
        }

        public void RemoveSlot(PoseBrowserCharacterListKind list, int index)
        {
            var listRef = GetMutableList(list);
            if (index < 0 || index >= listRef.Count) return;
            listRef.RemoveAt(index);
            SaveToDisk();
        }

        public PoseBrowserCharacterListKind? GetListKindForDicKey(int dicKey)
        {
            if (_male.Any(s => s.DicKey == dicKey)) return PoseBrowserCharacterListKind.Male;
            if (_female.Any(s => s.DicKey == dicKey)) return PoseBrowserCharacterListKind.Female;
            return null;
        }
    }
}

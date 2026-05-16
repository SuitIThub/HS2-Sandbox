using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class PoseTagDatabase
    {
        private const string TsvHeader = "HS2SANDBOX_POSE_TAGS\t2";
        /// <summary>ASCII record separator — unlikely in user-defined tag names; splits tag list in one column.</summary>
        private const char TagDelimiter = '\x1e';

        private readonly Dictionary<string, PoseTagEntry> _entries = new Dictionary<string, PoseTagEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly string _poseRoot;
        private readonly string _storagePath;
        private readonly string _legacyJsonPath;
        private bool _dirty;
        private HashSet<string>? _allTagsCache;

        public PoseTagDatabase(string poseRootPath)
        {
            _poseRoot = Path.GetFullPath(poseRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dir = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox");
            _storagePath = Path.Combine(dir, "pose_tags.tsv");
            _legacyJsonPath = Path.Combine(dir, "pose_tags.json");
            LoadFromDisk();
        }

        public void Update()
        {
            if (_dirty)
                SaveToDisk();
        }

        public bool IsFavorite(PoseGridItem item)
        {
            string key = GetKey(item);
            return _entries.TryGetValue(key, out var entry) && entry.Favorite;
        }

        public HashSet<string> GetTags(PoseGridItem item)
        {
            string key = GetKey(item);
            if (_entries.TryGetValue(key, out var entry) && entry.Tags != null)
                return new HashSet<string>(entry.Tags, StringComparer.OrdinalIgnoreCase);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public void SetFavorite(PoseGridItem item, bool favorite)
        {
            string key = GetKey(item);
            var entry = GetOrCreate(key);
            entry.Favorite = favorite;
            item.IsFavorite = favorite;
            MarkDirty();
        }

        public void ToggleFavorite(PoseGridItem item)
        {
            SetFavorite(item, !IsFavorite(item));
        }

        public void SetTags(PoseGridItem item, IEnumerable<string> tags)
        {
            string key = GetKey(item);
            var entry = GetOrCreate(key);
            var tagList = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            entry.Tags = tagList;
            item.Tags = new HashSet<string>(tagList, StringComparer.OrdinalIgnoreCase);
            _allTagsCache = null;
            MarkDirty();
        }

        public void AddTags(PoseGridItem item, IEnumerable<string> tagsToAdd)
        {
            var existing = GetTags(item);
            foreach (var t in tagsToAdd)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    existing.Add(t.Trim());
            }
            SetTags(item, existing);
        }

        public void RemoveTags(PoseGridItem item, IEnumerable<string> tagsToRemove)
        {
            var existing = GetTags(item);
            foreach (var t in tagsToRemove)
                existing.Remove(t.Trim());
            SetTags(item, existing);
        }

        public HashSet<string> GetAllKnownTags()
        {
            if (_allTagsCache != null) return _allTagsCache;
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _entries.Values)
            {
                if (entry.Tags == null) continue;
                foreach (var tag in entry.Tags)
                    all.Add(tag);
            }
            _allTagsCache = all;
            return all;
        }

        public void ApplyToItem(PoseGridItem item)
        {
            string key = GetKey(item);
            if (_entries.TryGetValue(key, out var entry))
            {
                item.IsFavorite = entry.Favorite;
                item.Tags = entry.Tags != null && entry.Tags.Length > 0
                    ? new HashSet<string>(entry.Tags, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                item.IsFavorite = false;
                item.Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void OnItemPathChanged(string oldRelPath, PoseGridItem item)
        {
            string oldK = NormalizeStorageKey(oldRelPath);
            if (_entries.TryGetValue(oldK, out var entry))
            {
                _entries.Remove(oldK);
                _entries[GetKey(item)] = entry;
                MarkDirty();
            }
        }

        public void CopyTagsFromTo(PoseGridItem source, PoseGridItem dest)
        {
            if (!_entries.TryGetValue(GetKey(source), out var entry)) return;
            var clone = entry.Clone();
            _entries[GetKey(dest)] = clone;
            _allTagsCache = null;
            MarkDirty();
        }

        public void OnFolderPathRenamed(string oldRelativeDir, string newRelativeDir)
        {
            if (string.IsNullOrEmpty(oldRelativeDir)) return;
            string oldP = NormalizeStorageKey(oldRelativeDir).TrimEnd('/') + "/";
            string newP = NormalizeStorageKey(newRelativeDir).TrimEnd('/') + "/";

            var keys = _entries.Keys.ToList();
            foreach (var key in keys)
            {
                if (!key.StartsWith(oldP, StringComparison.OrdinalIgnoreCase)) continue;
                var ent = _entries[key];
                _entries.Remove(key);
                string suffix = key.Length >= oldP.Length ? key.Substring(oldP.Length) : "";
                _entries[newP + suffix] = ent;
            }
            _allTagsCache = null;
            MarkDirty();
        }

        public void RemoveEntry(PoseGridItem item)
        {
            if (_entries.Remove(GetKey(item)))
            {
                _allTagsCache = null;
                MarkDirty();
            }
        }

        public void RemoveAllEntriesUnderFolder(string relativeDirUnderPoseRoot)
        {
            if (string.IsNullOrEmpty(relativeDirUnderPoseRoot)) return;
            string prefix = NormalizeStorageKey(relativeDirUnderPoseRoot).TrimEnd('/') + "/";
            var keys = _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var k in keys)
                _entries.Remove(k);
            if (keys.Count > 0)
            {
                _allTagsCache = null;
                MarkDirty();
            }
        }

        private PoseTagEntry GetOrCreate(string key)
        {
            key = NormalizeStorageKey(key);
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new PoseTagEntry();
                _entries[key] = entry;
            }
            return entry;
        }

        private void MarkDirty()
        {
            _dirty = true;
            SaveToDisk();
        }

        public void ForceSave()
        {
            SaveToDisk();
        }

        private void SaveToDisk()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var utf8 = new UTF8Encoding(false);
                string tempPath = _storagePath + ".tmp";

                using (var sw = new StreamWriter(tempPath, false, utf8))
                {
                    sw.WriteLine(TsvHeader);
                    foreach (var kvp in _entries.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!kvp.Value.HasPersistableData()) continue;
                        string key = NormalizeStorageKey(kvp.Key);
                        string tagsCol = kvp.Value.Tags == null || kvp.Value.Tags.Length == 0
                            ? ""
                            : string.Join(TagDelimiter.ToString(), kvp.Value.Tags);
                        sw.WriteLine($"{key}\t{(kvp.Value.Favorite ? "1" : "0")}\t{tagsCol}");
                    }
                }

                if (File.Exists(_storagePath))
                    File.Replace(tempPath, _storagePath, null);
                else
                    File.Move(tempPath, _storagePath);

                _dirty = false;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to save tag database to '{_storagePath}': {ex.Message}");
                try
                {
                    string tmp = _storagePath + ".tmp";
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private void LoadFromDisk()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    if (TryLoadTsv(_storagePath, out int count))
                    {
                        if (count > 0)
                            SandboxServices.Log.LogInfo($"PoseBrowser: Loaded {count} tag/favorite entries from pose_tags.tsv");
                        return;
                    }

                    SandboxServices.Log.LogWarning(
                        "PoseBrowser: pose_tags.tsv exists but could not be read; trying legacy pose_tags.json if present.");
                }

                if (File.Exists(_legacyJsonPath) && TryLoadLegacyJson(_legacyJsonPath))
                {
                    SandboxServices.Log.LogInfo("PoseBrowser: Migrated tag data from pose_tags.json; saving pose_tags.tsv.");
                    _dirty = false;
                    SaveToDisk();
                    return;
                }

                if (File.Exists(_legacyJsonPath))
                    SandboxServices.Log.LogWarning("PoseBrowser: pose_tags.json present but was not readable; tags start empty.");
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"PoseBrowser: Failed to load tag database: {ex.Message}");
            }
        }

        private bool TryLoadTsv(string path, out int imported)
        {
            imported = 0;
            var temp = new Dictionary<string, PoseTagEntry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
                {
                    string line = rawLine.TrimStart('\uFEFF').TrimEnd('\r');
                    if (line.Length == 0) continue;
                    if (line.StartsWith("HS2SANDBOX_POSE_TAGS", StringComparison.Ordinal)) continue;

                    int i1 = line.IndexOf('\t');
                    if (i1 <= 0) continue;
                    int i2 = line.IndexOf('\t', i1 + 1);
                    if (i2 <= i1) continue;

                    string key = NormalizeStorageKey(line.Substring(0, i1));
                    if (string.IsNullOrEmpty(key)) continue;

                    string favCell = line.Substring(i1 + 1, i2 - i1 - 1).Trim();
                    string tagsCell = i2 + 1 < line.Length ? line.Substring(i2 + 1) : "";

                    bool favorite = favCell == "1" || string.Equals(favCell, "true", StringComparison.OrdinalIgnoreCase);

                    string[] tags;
                    if (string.IsNullOrEmpty(tagsCell))
                        tags = Array.Empty<string>();
                    else
                    {
                        tags = tagsCell.Split(TagDelimiter)
                            .Select(t => t.Trim())
                            .Where(t => t.Length > 0)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                    }

                    temp[key] = new PoseTagEntry { Favorite = favorite, Tags = tags };
                    imported++;
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: pose_tags.tsv parse error: {ex.Message}");
                return false;
            }

            _entries.Clear();
            foreach (var kvp in temp)
                _entries[kvp.Key] = kvp.Value;
            return true;
        }

        /// <summary>Legacy Unity JsonUtility format (unreliable nested arrays in some builds).</summary>
        private bool TryLoadLegacyJson(string path)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var store = JsonUtility.FromJson<PoseTagStoreDtoLegacy>(json);
                if (store?.keys == null || store.values == null) return false;
                int n = Math.Min(store.keys.Length, store.values.Length);
                _entries.Clear();
                for (int i = 0; i < n; i++)
                {
                    string? k = store.keys[i];
                    var v = store.values[i];
                    if (string.IsNullOrEmpty(k) || v == null) continue;
                    string[] tags = v.tags != null && v.tags.Length > 0
                        ? (string[])v.tags.Clone()
                        : Array.Empty<string>();
                    _entries[NormalizeStorageKey(k)] = new PoseTagEntry { Favorite = v.favorite, Tags = tags };
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeStorageKey(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return relPath;
            return relPath.Replace('\\', '/').TrimStart('/');
        }

        /// <summary>Stable relative path under pose root; avoids session-specific absolute path keys when possible.</summary>
        private string GetKey(PoseGridItem item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.FilePath)) return "";

                string full = Path.GetFullPath(item.FilePath);
                string root = _poseRoot;
                if (full.Length >= root.Length &&
                    full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    int i = root.Length;
                    if (i < full.Length && (full[i] == Path.DirectorySeparatorChar || full[i] == Path.AltDirectorySeparatorChar))
                        i++;
                    return NormalizeStorageKey(full.Substring(i));
                }

                SandboxServices.Log.LogWarning(
                    $"PoseBrowser: Pose file is not under UserData pose root; using full path as tag key (see pose_tags.tsv). File: {item.FilePath}");
                return NormalizeStorageKey(full);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: GetKey failed for '{item.FilePath}': {ex.Message}");
                return NormalizeStorageKey(item.FilePath);
            }
        }

        private sealed class PoseTagEntry
        {
            public bool Favorite;
            public string[] Tags = Array.Empty<string>();

            public bool HasPersistableData() =>
                Favorite || (Tags != null && Tags.Length > 0);

            public PoseTagEntry Clone() => new PoseTagEntry
            {
                Favorite = Favorite,
                Tags = Tags != null ? (string[])Tags.Clone() : Array.Empty<string>()
            };
        }

        #pragma warning disable CS0649 // Assigned by UnityEngine.JsonUtility when loading legacy pose_tags.json
        [Serializable]
        private sealed class PoseTagEntryDtoLegacy
        {
            public bool favorite;
            public string[] tags = Array.Empty<string>();
        }

        [Serializable]
        private sealed class PoseTagStoreDtoLegacy
        {
            public string[] keys = Array.Empty<string>();
            public PoseTagEntryDtoLegacy[] values = Array.Empty<PoseTagEntryDtoLegacy>();
        }
        #pragma warning restore CS0649
    }
}

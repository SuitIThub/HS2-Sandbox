using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public sealed class PoseGroupDatabase
    {
        private const int FileVersion = 1;
        private const string TsvHeader = "HS2SANDBOX_POSE_GROUPS\t1";
        private const char TagDelimiter = '\x1e';
        private const char MemberDelimiter = '\x1f';

        private readonly string _poseRoot;
        private readonly string _storagePath;
        private readonly string _legacyJsonPath;
        private readonly Dictionary<string, PoseGroup> _groupsById = new Dictionary<string, PoseGroup>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pathToGroupId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PoseGroupDatabase(string poseRootPath)
        {
            _poseRoot = Path.GetFullPath(poseRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dir = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox");
            _storagePath = Path.Combine(dir, "pose_groups.tsv");
            _legacyJsonPath = Path.Combine(dir, "pose_groups.json");
            LoadFromDisk();
        }

        public void Update()
        {
            // Saves happen immediately in MarkDirty; kept for API symmetry with PoseTagDatabase.
        }

        public void ForceSave() => SaveToDisk();

        public IReadOnlyDictionary<string, PoseGroup> GroupsById => _groupsById;

        public string? GetGroupIdForItem(PoseGridItem item)
        {
            string rel = GetRelativeKey(item);
            return string.IsNullOrEmpty(rel) ? null : _pathToGroupId.TryGetValue(rel, out var id) ? id : null;
        }

        public PoseGroup? GetGroupForItem(PoseGridItem item)
        {
            string? id = GetGroupIdForItem(item);
            if (id == null) return null;
            return _groupsById.TryGetValue(id, out var g) ? g : null;
        }

        public PoseGroup? TryGetGroup(string groupId)
        {
            return _groupsById.TryGetValue(groupId, out var g) ? g : null;
        }

        public IEnumerable<PoseGroup> GetAllGroups() => _groupsById.Values;

        public void ApplyMembershipToItems(IEnumerable<PoseGridItem> items)
        {
            foreach (var item in items)
                item.GroupId = GetGroupIdForItem(item);
        }

        public PoseGroup CreateGroup(string name, IEnumerable<PoseGridItem> members, IEnumerable<string>? tags = null)
        {
            var memberList = members.ToList();
            foreach (var it in memberList)
            {
                string rel = GetRelativeKey(it);
                if (string.IsNullOrEmpty(rel)) continue;
                if (_pathToGroupId.TryGetValue(rel, out var existingId))
                    RemoveMember(existingId, rel);
            }

            var group = new PoseGroup
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Group" : name.Trim()
            };
            if (tags != null)
            {
                foreach (var t in tags)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                        group.Tags.Add(t.Trim());
                }
            }

            foreach (var it in memberList)
            {
                string rel = GetRelativeKey(it);
                if (string.IsNullOrEmpty(rel)) continue;
                group.MemberRelativePaths.Add(rel);
                _pathToGroupId[rel] = group.Id;
                it.GroupId = group.Id;
            }

            _groupsById[group.Id] = group;
            MarkDirty();
            return group;
        }

        public void DissolveGroup(string groupId)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            foreach (var rel in group.MemberRelativePaths)
                _pathToGroupId.Remove(rel);
            _groupsById.Remove(groupId);
            MarkDirty();
        }

        public void RemoveMembersFromGroup(string groupId, IEnumerable<string> relativePaths)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            foreach (var rel in relativePaths)
            {
                string norm = NormalizeStorageKey(rel);
                group.MemberRelativePaths.RemoveAll(p => string.Equals(p, norm, StringComparison.OrdinalIgnoreCase));
                _pathToGroupId.Remove(norm);
            }

            if (group.MemberRelativePaths.Count == 0)
                _groupsById.Remove(groupId);
            MarkDirty();
        }

        public void RemoveMember(string groupId, string relativePath)
        {
            RemoveMembersFromGroup(groupId, new[] { relativePath });
        }

        public void RemoveItem(PoseGridItem item)
        {
            string rel = GetRelativeKey(item);
            if (string.IsNullOrEmpty(rel)) return;
            if (!_pathToGroupId.TryGetValue(rel, out var groupId)) return;
            RemoveMember(groupId, rel);
            item.GroupId = null;
        }

        public void OnItemPathChanged(string oldRelPath, PoseGridItem item)
        {
            string oldK = NormalizeStorageKey(oldRelPath);
            string newK = GetRelativeKey(item);
            if (string.IsNullOrEmpty(oldK) || string.IsNullOrEmpty(newK) || string.Equals(oldK, newK, StringComparison.OrdinalIgnoreCase))
                return;

            if (!_pathToGroupId.TryGetValue(oldK, out var groupId)) return;
            _pathToGroupId.Remove(oldK);
            if (_groupsById.TryGetValue(groupId, out var group))
            {
                for (int i = 0; i < group.MemberRelativePaths.Count; i++)
                {
                    if (string.Equals(group.MemberRelativePaths[i], oldK, StringComparison.OrdinalIgnoreCase))
                    {
                        group.MemberRelativePaths[i] = newK;
                        break;
                    }
                }
            }

            _pathToGroupId[newK] = groupId;
            item.GroupId = groupId;
            MarkDirty();
        }

        public void SetGroupName(string groupId, string name)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            group.Name = string.IsNullOrWhiteSpace(name) ? "Group" : name.Trim();
            MarkDirty();
        }

        public void SetGroupTags(string groupId, IEnumerable<string> tags)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            group.Tags = new HashSet<string>(
                tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()),
                StringComparer.OrdinalIgnoreCase);
            MarkDirty();
        }

        public List<PoseGroup> GetGroupsFullyContainedIn(IReadOnlyCollection<string> relativePaths)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in relativePaths)
            {
                string norm = NormalizeMemberPath(rel);
                if (!string.IsNullOrEmpty(norm))
                    set.Add(norm);
            }

            var result = new List<PoseGroup>();
            foreach (var group in _groupsById.Values)
            {
                if (group.MemberRelativePaths.Count == 0) continue;
                if (group.MemberRelativePaths.All(m => set.Contains(NormalizeMemberPath(m))))
                    result.Add(group);
            }

            return result;
        }

        public void ImportGroup(PoseGroup group, IReadOnlyDictionary<string, string> oldMemberRelToNewRel)
        {
            var newMembers = new List<string>();
            foreach (var kvp in oldMemberRelToNewRel)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    newMembers.Add(NormalizeStorageKey(kvp.Value));
            }

            if (newMembers.Count == 0) return;

            var imported = new PoseGroup
            {
                Id = string.IsNullOrEmpty(group.Id) ? Guid.NewGuid().ToString("N") : group.Id,
                Name = group.Name,
                Tags = new HashSet<string>(group.Tags, StringComparer.OrdinalIgnoreCase),
                MemberRelativePaths = newMembers
            };

            foreach (var rel in newMembers)
            {
                if (_pathToGroupId.TryGetValue(rel, out var existingId))
                    DissolveGroup(existingId);
            }

            foreach (var rel in newMembers)
                _pathToGroupId[rel] = imported.Id;
            _groupsById[imported.Id] = imported;
            MarkDirty();
        }

        private void MarkDirty() => SaveToDisk();

        private string GetRelativeKey(PoseGridItem item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.FilePath)) return "";
                string full = Path.GetFullPath(item.FilePath);
                if (full.Length >= _poseRoot.Length &&
                    full.StartsWith(_poseRoot, StringComparison.OrdinalIgnoreCase))
                {
                    int i = _poseRoot.Length;
                    if (i < full.Length && (full[i] == Path.DirectorySeparatorChar || full[i] == Path.AltDirectorySeparatorChar))
                        i++;
                    return NormalizeStorageKey(full.Substring(i));
                }

                return NormalizeStorageKey(full);
            }
            catch
            {
                return NormalizeStorageKey(item.FilePath);
            }
        }

        /// <summary>Canonical relative path key (forward slashes, no leading slash).</summary>
        public static string NormalizeMemberPath(string rel)
        {
            if (string.IsNullOrWhiteSpace(rel)) return "";
            return rel.Replace('\\', '/').TrimStart('/');
        }

        private static string NormalizeStorageKey(string rel) => NormalizeMemberPath(rel);

        private void LoadFromDisk()
        {
            _groupsById.Clear();
            _pathToGroupId.Clear();
            try
            {
                if (File.Exists(_storagePath) && TryLoadTsv(_storagePath, out int count))
                {
                    if (count > 0)
                        SandboxServices.Log.LogInfo($"PoseBrowser: Loaded {count} pose group(s) from pose_groups.tsv");
                    return;
                }

                if (File.Exists(_legacyJsonPath) && TryLoadLegacyJson(_legacyJsonPath, out int jsonCount))
                {
                    SandboxServices.Log.LogInfo($"PoseBrowser: Migrated {jsonCount} pose group(s) from pose_groups.json to pose_groups.tsv");
                    SaveToDisk();
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseGroupDatabase: load failed: {ex.Message}");
            }
        }

        private bool TryLoadTsv(string path, out int imported)
        {
            imported = 0;
            try
            {
                foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
                {
                    string line = rawLine.TrimStart('\uFEFF').TrimEnd('\r');
                    if (line.Length == 0) continue;
                    if (line.StartsWith("HS2SANDBOX_POSE_GROUPS", StringComparison.Ordinal)) continue;

                    string[] parts = line.Split('\t');
                    if (parts.Length < 5 || !string.Equals(parts[0], "group", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string id = parts[1];
                    if (string.IsNullOrEmpty(id)) continue;

                    var group = new PoseGroup
                    {
                        Id = id,
                        Name = parts[2] ?? "",
                        Tags = new HashSet<string>(ParseDelimited(parts[3], TagDelimiter), StringComparer.OrdinalIgnoreCase),
                        MemberRelativePaths = ParseDelimited(parts[4], MemberDelimiter)
                            .Select(NormalizeStorageKey)
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                    };

                    if (group.MemberRelativePaths.Count == 0) continue;

                    if (_groupsById.ContainsKey(group.Id))
                    {
                        SandboxServices.Log.LogWarning($"PoseGroupDatabase: duplicate group id \"{group.Id}\"; skipping.");
                        continue;
                    }

                    _groupsById[group.Id] = group;
                    foreach (var m in group.MemberRelativePaths)
                    {
                        if (_pathToGroupId.ContainsKey(m))
                        {
                            SandboxServices.Log.LogWarning($"PoseGroupDatabase: duplicate member \"{m}\"; keeping first group.");
                            continue;
                        }

                        _pathToGroupId[m] = group.Id;
                    }

                    imported++;
                }

                return true;
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseGroupDatabase: TSV read failed: {ex.Message}");
                return false;
            }
        }

        private static List<string> ParseDelimited(string? col, char delimiter)
        {
            if (string.IsNullOrEmpty(col)) return new List<string>();
            return col.Split(delimiter).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        }

        private bool TryLoadLegacyJson(string path, out int imported)
        {
            imported = 0;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonUtility.FromJson<PoseGroupsPersistedFile>(json);
                if (data?.groups == null || data.groups.Length == 0)
                    return false;

                foreach (var e in data.groups)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    var group = new PoseGroup
                    {
                        Id = e.id,
                        Name = e.name ?? "",
                        Tags = new HashSet<string>(e.tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
                        MemberRelativePaths = (e.members ?? Array.Empty<string>())
                            .Select(NormalizeStorageKey)
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                    };
                    if (group.MemberRelativePaths.Count == 0) continue;
                    _groupsById[group.Id] = group;
                    foreach (var m in group.MemberRelativePaths)
                    {
                        if (!_pathToGroupId.ContainsKey(m))
                            _pathToGroupId[m] = group.Id;
                    }

                    imported++;
                }

                return imported > 0;
            }
            catch
            {
                return false;
            }
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
                    foreach (var group in _groupsById.Values
                                 .Where(g => g.MemberRelativePaths.Count > 0)
                                 .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        string tagsCol = group.Tags.Count == 0
                            ? ""
                            : string.Join(TagDelimiter.ToString(), group.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
                        string membersCol = string.Join(
                            MemberDelimiter.ToString(),
                            group.MemberRelativePaths);
                        sw.WriteLine($"group\t{group.Id}\t{group.Name}\t{tagsCol}\t{membersCol}");
                    }
                }

                if (File.Exists(_storagePath))
                    File.Replace(tempPath, _storagePath, null);
                else
                    File.Move(tempPath, _storagePath);

                try
                {
                    if (File.Exists(_legacyJsonPath))
                        File.Delete(_legacyJsonPath);
                }
                catch { /* ignore */ }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseGroupDatabase: save failed: {ex.Message}");
                try
                {
                    string tmp = _storagePath + ".tmp";
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch { /* ignore */ }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public sealed class PoseGroupDatabase
    {
        private const int FileVersion = 5;
        private const string TsvHeader = "HS2SANDBOX_POSE_GROUPS\t5";
        private const char TagDelimiter = '\x1e';
        private const char MemberDelimiter = '\x1f';
        private const char OffsetDelimiter = '\x1f';

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

        public IDictionary<string, PoseGroup> GroupsById => _groupsById;

        public string? GetGroupIdForItem(PoseGridItem item)
        {
            string rel = GetRelativeKey(item);
            return string.IsNullOrEmpty(rel) ? null : ResolveGroupIdForMemberPath(rel);
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
            {
                string? groupId = ResolveGroupIdForMemberPath(GetRelativeKey(item));
                item.GroupId = groupId;
                if (!string.IsNullOrEmpty(groupId))
                {
                    string rel = GetRelativeKey(item);
                    if (!string.IsNullOrEmpty(rel))
                        _pathToGroupId[rel] = groupId;
                }
            }
        }

        /// <summary>
        /// Resolve group membership for a stored member path. Uses the fast path map first, then falls back to
        /// each group's persisted member list (keeps working when the map was never built or drifted out of sync).
        /// </summary>
        private string? ResolveGroupIdForMemberPath(string relativePath)
        {
            string norm = NormalizeStorageKey(relativePath);
            if (string.IsNullOrEmpty(norm))
                return null;

            if (_pathToGroupId.TryGetValue(norm, out var mappedId) && _groupsById.ContainsKey(mappedId))
                return mappedId;

            foreach (var kvp in _groupsById)
            {
                var members = kvp.Value.MemberRelativePaths;
                for (int i = 0; i < members.Count; i++)
                {
                    if (string.Equals(members[i], norm, StringComparison.OrdinalIgnoreCase))
                        return kvp.Key;
                }
            }

            return null;
        }

        private void RebuildPathToGroupIdFromGroups()
        {
            _pathToGroupId.Clear();
            foreach (var kvp in _groupsById)
            {
                foreach (var rel in kvp.Value.MemberRelativePaths)
                {
                    if (string.IsNullOrEmpty(rel))
                        continue;
                    if (_pathToGroupId.ContainsKey(rel))
                    {
                        SandboxServices.Log.LogWarning(
                            $"PoseGroupDatabase: duplicate member \"{rel}\"; keeping group \"{kvp.Key}\".");
                    }

                    _pathToGroupId[rel] = kvp.Key;
                }
            }
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
                Name = StringEx.IsNullOrWhiteSpace(name) ? "Group" : name.Trim()
            };
            if (tags != null)
            {
                foreach (var t in tags)
                {
                    if (!StringEx.IsNullOrWhiteSpace(t))
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
            else
                PruneOffsetsToMembers(group);
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

            string? groupId = ResolveGroupIdForMemberPath(oldK);
            if (groupId == null)
                return;

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

                if (group.MemberRelativeOffsets.TryGetValue(oldK, out var offset))
                {
                    group.MemberRelativeOffsets.Remove(oldK);
                    group.MemberRelativeOffsets[newK] = offset;
                }

                if (group.MemberBodyHeights.TryGetValue(oldK, out float height))
                {
                    group.MemberBodyHeights.Remove(oldK);
                    group.MemberBodyHeights[newK] = height;
                }

                if (group.MemberObjectScales.TryGetValue(oldK, out var objectScale))
                {
                    group.MemberObjectScales.Remove(oldK);
                    group.MemberObjectScales[newK] = objectScale;
                }

                if (group.MemberRelativeRotations.TryGetValue(oldK, out var rotation))
                {
                    group.MemberRelativeRotations.Remove(oldK);
                    group.MemberRelativeRotations[newK] = rotation;
                }
            }

            _pathToGroupId[newK] = groupId;
            item.GroupId = groupId;
            MarkDirty();
        }

        /// <summary>Rewrite stored member paths when a folder under the pose root is renamed.</summary>
        public void OnFolderPathRenamed(string oldRelativeDir, string newRelativeDir)
        {
            if (StringEx.IsNullOrWhiteSpace(oldRelativeDir))
                return;

            string oldPrefix = NormalizeStorageKey(oldRelativeDir).TrimEnd('/');
            string newPrefix = NormalizeStorageKey(newRelativeDir).TrimEnd('/');
            if (string.IsNullOrEmpty(oldPrefix) ||
                string.Equals(oldPrefix, newPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            bool changed = false;
            foreach (var group in _groupsById.Values)
            {
                changed |= RewriteMemberPathListPrefix(group.MemberRelativePaths, oldPrefix, newPrefix);
                changed |= RewriteMemberLayoutKeyPrefix(group.MemberRelativeOffsets, oldPrefix, newPrefix);
                changed |= RewriteMemberLayoutKeyPrefix(group.MemberBodyHeights, oldPrefix, newPrefix);
                changed |= RewriteMemberLayoutKeyPrefix(group.MemberObjectScales, oldPrefix, newPrefix);
                changed |= RewriteMemberLayoutKeyPrefix(group.MemberRelativeRotations, oldPrefix, newPrefix);
            }

            if (!changed)
                return;

            RebuildPathToGroupIdFromGroups();
            MarkDirty();
        }

        private static bool RewriteMemberPathListPrefix(List<string> paths, string oldPrefix, string newPrefix)
        {
            bool changed = false;
            for (int i = 0; i < paths.Count; i++)
            {
                string? rewritten = RewriteSingleMemberPathPrefix(paths[i], oldPrefix, newPrefix);
                if (rewritten == null)
                    continue;
                paths[i] = rewritten;
                changed = true;
            }

            return changed;
        }

        private static bool RewriteMemberLayoutKeyPrefix<T>(Dictionary<string, T> map, string oldPrefix, string newPrefix)
        {
            if (map.Count == 0)
                return false;

            var rewrites = new List<KeyValuePair<string, string>>();
            foreach (var key in map.Keys)
            {
                string? rewritten = RewriteSingleMemberPathPrefix(key, oldPrefix, newPrefix);
                if (rewritten != null && !string.Equals(key, rewritten, StringComparison.OrdinalIgnoreCase))
                    rewrites.Add(new KeyValuePair<string, string>(key, rewritten));
            }

            if (rewrites.Count == 0)
                return false;

            foreach (var kvp in rewrites)
            {
                if (!map.TryGetValue(kvp.Key, out var value))
                    continue;
                map.Remove(kvp.Key);
                map[kvp.Value] = value;
            }

            return true;
        }

        private static string? RewriteSingleMemberPathPrefix(string path, string oldPrefix, string newPrefix)
        {
            string norm = NormalizeMemberPath(path);
            if (string.Equals(norm, oldPrefix, StringComparison.OrdinalIgnoreCase))
                return newPrefix;
            string oldWithSlash = oldPrefix + "/";
            if (norm.StartsWith(oldWithSlash, StringComparison.OrdinalIgnoreCase))
                return newPrefix + norm.Substring(oldPrefix.Length);
            return null;
        }

        public void SetGroupName(string groupId, string name)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            group.Name = StringEx.IsNullOrWhiteSpace(name) ? "Group" : name.Trim();
            MarkDirty();
        }

        public void SetGroupTags(string groupId, IEnumerable<string> tags)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            group.Tags = new HashSet<string>(
                tags.Where(t => !StringEx.IsNullOrWhiteSpace(t)).Select(t => t.Trim()),
                StringComparer.OrdinalIgnoreCase);
            MarkDirty();
        }

        public void SetMemberRelativeOffsets(string groupId, IDictionary<string, Vector3> offsetsByMemberPath) =>
            SetMemberRelativeLayout(groupId, offsetsByMemberPath, null, null);

        public void SetMemberRelativeLayout(
            string groupId,
            IDictionary<string, Vector3> offsetsByMemberPath,
            IDictionary<string, float>? bodyHeightsByMemberPath,
            IDictionary<string, Quaternion>? rotationsByMemberPath = null,
            IDictionary<string, Vector3>? objectScalesByMemberPath = null)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            group.MemberRelativeOffsets.Clear();
            foreach (var kvp in offsetsByMemberPath)
            {
                string key = NormalizeStorageKey(kvp.Key);
                if (string.IsNullOrEmpty(key))
                    continue;
                group.MemberRelativeOffsets[key] = kvp.Value;
            }

            if (bodyHeightsByMemberPath != null)
            {
                group.MemberBodyHeights.Clear();
                foreach (var kvp in bodyHeightsByMemberPath)
                {
                    string key = NormalizeStorageKey(kvp.Key);
                    if (string.IsNullOrEmpty(key))
                        continue;
                    group.MemberBodyHeights[key] = kvp.Value;
                }
            }

            if (rotationsByMemberPath != null)
            {
                group.MemberRelativeRotations.Clear();
                foreach (var kvp in rotationsByMemberPath)
                {
                    string key = NormalizeStorageKey(kvp.Key);
                    if (string.IsNullOrEmpty(key))
                        continue;
                    group.MemberRelativeRotations[key] = kvp.Value;
                }
            }

            if (objectScalesByMemberPath != null)
            {
                group.MemberObjectScales.Clear();
                foreach (var kvp in objectScalesByMemberPath)
                {
                    string key = NormalizeStorageKey(kvp.Key);
                    if (string.IsNullOrEmpty(key))
                        continue;
                    group.MemberObjectScales[key] = kvp.Value;
                }
            }

            PruneLayoutToMembers(group);
            MarkDirty();
        }

        public void ClearMemberRelativeOffsets(string groupId)
        {
            if (!_groupsById.TryGetValue(groupId, out var group)) return;
            if (group.MemberRelativeOffsets.Count == 0 &&
                group.MemberBodyHeights.Count == 0 &&
                group.MemberObjectScales.Count == 0 &&
                group.MemberRelativeRotations.Count == 0)
                return;
            group.MemberRelativeOffsets.Clear();
            group.MemberBodyHeights.Clear();
            group.MemberObjectScales.Clear();
            group.MemberRelativeRotations.Clear();
            MarkDirty();
        }

        private static void PruneOffsetsToMembers(PoseGroup group) => PruneLayoutToMembers(group);

        private static void PruneLayoutToMembers(PoseGroup group)
        {
            if (group.MemberRelativeOffsets.Count == 0 &&
                group.MemberBodyHeights.Count == 0 &&
                group.MemberObjectScales.Count == 0 &&
                group.MemberRelativeRotations.Count == 0)
                return;

            var memberSet = new HashSet<string>(group.MemberRelativePaths, StringComparer.OrdinalIgnoreCase);
            var staleOffsets = group.MemberRelativeOffsets.Keys
                .Where(k => !memberSet.Contains(k))
                .ToList();
            foreach (var k in staleOffsets)
                group.MemberRelativeOffsets.Remove(k);

            var staleHeights = group.MemberBodyHeights.Keys
                .Where(k => !memberSet.Contains(k))
                .ToList();
            foreach (var k in staleHeights)
                group.MemberBodyHeights.Remove(k);

            var staleScales = group.MemberObjectScales.Keys
                .Where(k => !memberSet.Contains(k))
                .ToList();
            foreach (var k in staleScales)
                group.MemberObjectScales.Remove(k);

            var staleRotations = group.MemberRelativeRotations.Keys
                .Where(k => !memberSet.Contains(k))
                .ToList();
            foreach (var k in staleRotations)
                group.MemberRelativeRotations.Remove(k);
        }

        public List<PoseGroup> GetGroupsFullyContainedIn(ICollection<string> relativePaths)
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

        public void ImportGroup(
            PoseGroup group,
            IDictionary<string, string> oldMemberRelToNewRel,
            Vector3[]? memberRelativeOffsets = null,
            float[]? memberBodyHeights = null,
            Quaternion[]? memberRelativeRotations = null,
            Vector3[]? memberObjectScales = null)
        {
            var newMembers = new List<string>();
            var oldMembersOrdered = new List<string>();
            foreach (var kvp in oldMemberRelToNewRel)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                oldMembersOrdered.Add(NormalizeStorageKey(kvp.Key));
                newMembers.Add(NormalizeStorageKey(kvp.Value));
            }

            if (newMembers.Count == 0) return;

            var importedOffsets = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var importedHeights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var importedScales = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var importedRotations = new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
            if (memberRelativeOffsets != null && memberRelativeOffsets.Length > 0)
            {
                for (int i = 0; i < newMembers.Count && i < memberRelativeOffsets.Length; i++)
                {
                    if (i == 0)
                        continue;
                    var offset = memberRelativeOffsets[i];
                    if (offset.sqrMagnitude < 1e-12f)
                        continue;
                    importedOffsets[newMembers[i]] = offset;
                }
            }
            else
            {
                foreach (var kvp in group.MemberRelativeOffsets)
                {
                    string oldKey = NormalizeStorageKey(kvp.Key);
                    int idx = oldMembersOrdered.FindIndex(p =>
                        string.Equals(p, oldKey, StringComparison.OrdinalIgnoreCase));
                    if (idx > 0 && idx < newMembers.Count)
                        importedOffsets[newMembers[idx]] = kvp.Value;
                }
            }

            if (memberBodyHeights != null && memberBodyHeights.Length > 0)
            {
                for (int i = 0; i < newMembers.Count && i < memberBodyHeights.Length; i++)
                    importedHeights[newMembers[i]] = memberBodyHeights[i];
            }
            else
            {
                foreach (var kvp in group.MemberBodyHeights)
                {
                    string oldKey = NormalizeStorageKey(kvp.Key);
                    int idx = oldMembersOrdered.FindIndex(p =>
                        string.Equals(p, oldKey, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0 && idx < newMembers.Count)
                        importedHeights[newMembers[idx]] = kvp.Value;
                }
            }

            if (memberObjectScales != null && memberObjectScales.Length > 0)
            {
                for (int i = 0; i < newMembers.Count && i < memberObjectScales.Length; i++)
                    importedScales[newMembers[i]] = memberObjectScales[i];
            }
            else
            {
                foreach (var kvp in group.MemberObjectScales)
                {
                    string oldKey = NormalizeStorageKey(kvp.Key);
                    int idx = oldMembersOrdered.FindIndex(p =>
                        string.Equals(p, oldKey, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0 && idx < newMembers.Count)
                        importedScales[newMembers[idx]] = kvp.Value;
                }
            }

            if (memberRelativeRotations != null && memberRelativeRotations.Length > 0)
            {
                for (int i = 0; i < newMembers.Count && i < memberRelativeRotations.Length; i++)
                {
                    if (i == 0)
                        continue;
                    var rot = memberRelativeRotations[i];
                    if (PoseBrowserCharacterApply.IsNearIdentityRelativeRotation(rot))
                        continue;
                    importedRotations[newMembers[i]] = rot;
                }
            }
            else
            {
                foreach (var kvp in group.MemberRelativeRotations)
                {
                    string oldKey = NormalizeStorageKey(kvp.Key);
                    int idx = oldMembersOrdered.FindIndex(p =>
                        string.Equals(p, oldKey, StringComparison.OrdinalIgnoreCase));
                    if (idx > 0 && idx < newMembers.Count)
                        importedRotations[newMembers[idx]] = kvp.Value;
                }
            }

            var imported = new PoseGroup
            {
                Id = string.IsNullOrEmpty(group.Id) ? Guid.NewGuid().ToString("N") : group.Id,
                Name = group.Name,
                Tags = new HashSet<string>(group.Tags, StringComparer.OrdinalIgnoreCase),
                MemberRelativePaths = newMembers,
                MemberRelativeOffsets = importedOffsets,
                MemberBodyHeights = importedHeights,
                MemberObjectScales = importedScales,
                MemberRelativeRotations = importedRotations
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
            if (StringEx.IsNullOrWhiteSpace(rel)) return "";
            return rel.Replace('\\', '/').TrimStart('/');
        }

        private static string NormalizeStorageKey(string rel) => NormalizeMemberPath(rel);

        private static string EscapeTsvCell(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

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
                foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = rawLine.TrimStart('\uFEFF').TrimEnd('\r');
                    if (line.Length == 0) continue;
                    if (line.StartsWith("HS2SANDBOX_POSE_GROUPS", StringComparison.Ordinal)) continue;

                    string[] parts = line.Split('\t');
                    if (parts.Length < 5 || !string.Equals(parts[0], "group", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string id = parts[1];
                    if (string.IsNullOrEmpty(id)) continue;

                    var memberPaths = ParseDelimited(parts[4], MemberDelimiter)
                        .Select(NormalizeStorageKey)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var offsets = parts.Length >= 6
                        ? ParseMemberOffsetsColumn(parts[5], memberPaths)
                        : new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
                    var heights = parts.Length >= 7
                        ? ParseMemberBodyHeightsColumn(parts[6], memberPaths)
                        : new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    var rotations = parts.Length >= 8
                        ? ParseMemberRelativeRotationsColumn(parts[7], memberPaths)
                        : new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
                    var scales = parts.Length >= 9
                        ? ParseMemberObjectScalesColumn(parts[8], memberPaths)
                        : new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);

                    var group = new PoseGroup
                    {
                        Id = id,
                        Name = parts[2] ?? "",
                        Tags = new HashSet<string>(ParseDelimited(parts[3], TagDelimiter), StringComparer.OrdinalIgnoreCase),
                        MemberRelativePaths = memberPaths,
                        MemberRelativeOffsets = offsets,
                        MemberBodyHeights = heights,
                        MemberObjectScales = scales,
                        MemberRelativeRotations = rotations
                    };

                    if (group.MemberRelativePaths.Count == 0) continue;

                    if (_groupsById.ContainsKey(group.Id))
                    {
                        SandboxServices.Log.LogWarning($"PoseGroupDatabase: duplicate group id \"{group.Id}\"; skipping.");
                        continue;
                    }

                    _groupsById[group.Id] = group;
                    imported++;
                }

                RebuildPathToGroupIdFromGroups();
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
            return col.Split(delimiter).Where(s => !StringEx.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        }

        private static Dictionary<string, Vector3> ParseMemberOffsetsColumn(
            string? col,
            IList<string> memberPaths)
        {
            var result = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(col) || memberPaths.Count == 0)
                return result;

            var tokens = col.Split(OffsetDelimiter);
            for (int i = 0; i < memberPaths.Count && i < tokens.Length; i++)
            {
                if (i == 0)
                    continue;
                string token = tokens[i].Trim();
                if (token.Length == 0)
                    continue;
                if (!TryParseOffsetTriple(token, out var offset))
                    continue;
                if (offset.sqrMagnitude < 1e-12f)
                    continue;
                result[memberPaths[i]] = offset;
            }

            return result;
        }

        private static bool TryParseOffsetTriple(string token, out Vector3 offset)
        {
            offset = Vector3.zero;
            string[] xyz = token.Split(',');
            if (xyz.Length != 3)
                return false;
            if (!float.TryParse(xyz[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                return false;
            if (!float.TryParse(xyz[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                return false;
            if (!float.TryParse(xyz[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                return false;
            offset = new Vector3(x, y, z);
            return true;
        }

        private static Dictionary<string, float> ParseMemberBodyHeightsColumn(
            string? col,
            IList<string> memberPaths)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(col) || memberPaths.Count == 0)
                return result;

            var tokens = col.Split(OffsetDelimiter);
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < memberPaths.Count && i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.Length == 0)
                    continue;
                if (!float.TryParse(token, NumberStyles.Float, inv, out float h))
                    continue;
                result[memberPaths[i]] = h;
            }

            return result;
        }

        private static string FormatMemberBodyHeightsColumn(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0 || group.MemberBodyHeights.Count == 0)
                return "";

            var parts = new List<string>(group.MemberRelativePaths.Count);
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                string rel = group.MemberRelativePaths[i];
                if (!group.MemberBodyHeights.TryGetValue(rel, out float h))
                {
                    parts.Add("");
                    continue;
                }

                parts.Add(h.ToString("R", inv));
            }

            return string.Join(OffsetDelimiter.ToString(), parts.ToArray());
        }

        private static Dictionary<string, Vector3> ParseMemberObjectScalesColumn(
            string? col,
            IList<string> memberPaths)
        {
            var result = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(col) || memberPaths.Count == 0)
                return result;

            var tokens = col.Split(OffsetDelimiter);
            for (int i = 0; i < memberPaths.Count && i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                if (token.Length == 0)
                    continue;
                if (!TryParseOffsetTriple(token, out var scale))
                    continue;
                result[memberPaths[i]] = scale;
            }

            return result;
        }

        private static string FormatMemberObjectScalesColumn(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0 || group.MemberObjectScales.Count == 0)
                return "";

            var parts = new List<string>(group.MemberRelativePaths.Count);
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                string rel = group.MemberRelativePaths[i];
                if (!group.MemberObjectScales.TryGetValue(rel, out Vector3 scale))
                {
                    parts.Add("");
                    continue;
                }

                parts.Add(string.Join(",", new[]
                {
                    scale.x.ToString("R", inv),
                    scale.y.ToString("R", inv),
                    scale.z.ToString("R", inv)
                }));
            }

            return string.Join(OffsetDelimiter.ToString(), parts.ToArray());
        }

        private static Dictionary<string, Quaternion> ParseMemberRelativeRotationsColumn(
            string? col,
            IList<string> memberPaths)
        {
            var result = new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(col) || memberPaths.Count == 0)
                return result;

            var tokens = col.Split(OffsetDelimiter);
            for (int i = 0; i < memberPaths.Count && i < tokens.Length; i++)
            {
                if (i == 0)
                    continue;
                string token = tokens[i].Trim();
                if (token.Length == 0)
                    continue;
                if (!TryParseRelativeRotationToken(token, out var rot))
                    continue;
                if (PoseBrowserCharacterApply.IsNearIdentityRelativeRotation(rot))
                    continue;
                result[memberPaths[i]] = rot;
            }

            return result;
        }

        /// <summary>Quaternion <c>x,y,z,w</c>; legacy TSV tokens with three values are treated as Euler degrees.</summary>
        private static bool TryParseRelativeRotationToken(string token, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            string[] parts = token.Split(',');
            var inv = CultureInfo.InvariantCulture;
            if (parts.Length >= 4)
            {
                if (!float.TryParse(parts[0], NumberStyles.Float, inv, out float x) ||
                    !float.TryParse(parts[1], NumberStyles.Float, inv, out float y) ||
                    !float.TryParse(parts[2], NumberStyles.Float, inv, out float z) ||
                    !float.TryParse(parts[3], NumberStyles.Float, inv, out float w))
                    return false;
                rotation = new Quaternion(x, y, z, w);
                return true;
            }

            if (parts.Length == 3 &&
                float.TryParse(parts[0], NumberStyles.Float, inv, out float ex) &&
                float.TryParse(parts[1], NumberStyles.Float, inv, out float ey) &&
                float.TryParse(parts[2], NumberStyles.Float, inv, out float ez))
            {
                rotation = Quaternion.Euler(ex, ey, ez);
                return true;
            }

            return false;
        }

        private static string FormatMemberRelativeRotationsColumn(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0 || group.MemberRelativeRotations.Count == 0)
                return "";

            var parts = new List<string>(group.MemberRelativePaths.Count);
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                if (i == 0)
                {
                    parts.Add("");
                    continue;
                }

                string rel = group.MemberRelativePaths[i];
                if (!group.MemberRelativeRotations.TryGetValue(rel, out var rot) ||
                    PoseBrowserCharacterApply.IsNearIdentityRelativeRotation(rot))
                {
                    parts.Add("");
                    continue;
                }

                parts.Add(string.Format(inv, "{0:R},{1:R},{2:R},{3:R}", rot.x, rot.y, rot.z, rot.w));
            }

            return string.Join(OffsetDelimiter.ToString(), parts.ToArray());
        }

        private static string FormatMemberOffsetsColumn(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0)
                return "";

            var parts = new List<string>(group.MemberRelativePaths.Count);
            var inv = CultureInfo.InvariantCulture;
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                if (i == 0)
                {
                    parts.Add("");
                    continue;
                }

                string rel = group.MemberRelativePaths[i];
                if (!group.MemberRelativeOffsets.TryGetValue(rel, out var offset) || offset.sqrMagnitude < 1e-12f)
                {
                    parts.Add("");
                    continue;
                }

                parts.Add(string.Format(inv, "{0:R},{1:R},{2:R}", offset.x, offset.y, offset.z));
            }

            return string.Join(OffsetDelimiter.ToString(), parts.ToArray());
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
                        Tags = new HashSet<string>(e.tags ?? new string[0], StringComparer.OrdinalIgnoreCase),
                        MemberRelativePaths = (e.members ?? new string[0])
                            .Select(NormalizeStorageKey)
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()
                    };
                    if (group.MemberRelativePaths.Count == 0) continue;
                    _groupsById[group.Id] = group;
                    imported++;
                }

                RebuildPathToGroupIdFromGroups();
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
                            : string.Join(TagDelimiter.ToString(), group.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray());
                        string membersCol = string.Join(
                            MemberDelimiter.ToString(),
                            group.MemberRelativePaths.ToArray());
                        string offsetsCol = FormatMemberOffsetsColumn(group);
                        string heightsCol = FormatMemberBodyHeightsColumn(group);
                        string rotationsCol = FormatMemberRelativeRotationsColumn(group);
                        string scalesCol = FormatMemberObjectScalesColumn(group);
                        sw.WriteLine(
                            $"group\t{group.Id}\t{EscapeTsvCell(group.Name)}\t{tagsCol}\t{membersCol}\t{offsetsCol}\t{heightsCol}\t{rotationsCol}\t{scalesCol}");
                    }
                }

                FileEx.CommitTempFile(tempPath, _storagePath);

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

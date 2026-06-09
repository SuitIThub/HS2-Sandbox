using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HS2SandboxPlugin
{
    /// <summary>Neutral → exclude (hide) → include-only, same cycle as per-tag filters.</summary>
    internal enum PoseDisplayFilterMode
    {
        Off = 0,
        Exclude = 1,
        IncludeOnly = 2
    }

    internal sealed class PoseBrowserDisplayEntry
    {
        public PoseGridItem Item { get; }
        public bool IsDimmed { get; }

        public PoseBrowserDisplayEntry(PoseGridItem item, bool isDimmed)
        {
            Item = item;
            IsDimmed = isDimmed;
        }
    }

    internal sealed class PoseBrowserGroupSegment
    {
        public string GroupId { get; }
        public string GroupName { get; }
        public IList<string> GroupTags { get; }
        public List<PoseBrowserDisplayEntry> Poses { get; } = new List<PoseBrowserDisplayEntry>();
        public bool ShowHeader { get; set; } = true;
        public bool ShowTags { get; set; } = true;
        /// <summary>True when this segment continues the same group on the next grid row.</summary>
        public bool IsContinuation { get; set; }

        public PoseBrowserGroupSegment(string groupId, string groupName, IList<string> groupTags)
        {
            GroupId = groupId;
            GroupName = groupName;
            GroupTags = groupTags;
        }
    }

    internal enum PoseBrowserGridCellKind
    {
        Pose,
        GroupSegment
    }

    internal sealed class PoseBrowserGridCell
    {
        public PoseBrowserGridCellKind Kind { get; }
        public PoseBrowserDisplayEntry? Pose { get; }
        public PoseBrowserGroupSegment? GroupSegment { get; }

        public static PoseBrowserGridCell ForPose(PoseBrowserDisplayEntry pose) =>
            new PoseBrowserGridCell(PoseBrowserGridCellKind.Pose, pose, null);

        public static PoseBrowserGridCell ForGroup(PoseBrowserGroupSegment segment) =>
            new PoseBrowserGridCell(PoseBrowserGridCellKind.GroupSegment, null, segment);

        private PoseBrowserGridCell(PoseBrowserGridCellKind kind, PoseBrowserDisplayEntry? pose, PoseBrowserGroupSegment? segment)
        {
            Kind = kind;
            Pose = pose;
            GroupSegment = segment;
        }
    }

    internal sealed class PoseBrowserGridRow
    {
        public List<PoseBrowserGridCell> Cells { get; } = new List<PoseBrowserGridCell>();
    }

    internal enum PoseSortMode
    {
        LastUsed = 0,
        LastUpdated = 1,
        LastCreated = 2,
        Name = 3
    }

    internal static class PoseBrowserGridLayout
    {
        public static List<PoseBrowserDisplayEntry> BuildFilteredDisplayList(
            IList<PoseGridItem> allItems,
            PoseGroupDatabase groupDb,
            string poseRoot,
            string searchText,
            bool searchUseRegex,
            ref string searchRegexError,
            HashSet<string> includeTagFilters,
            HashSet<string> excludeTagFilters,
            bool tagFilterAndMode,
            bool showFavoritesOnly,
            PoseDisplayFilterMode groupsFilter = PoseDisplayFilterMode.Off,
            PoseDisplayFilterMode thumbnailFilter = PoseDisplayFilterMode.Off)
        {
            searchRegexError = "";
            var groupVisible = new Dictionary<string, bool>(StringComparer.Ordinal);
            var groupById = groupDb.GroupsById;

            Regex? searchRx = null;
            if (searchUseRegex && !StringEx.IsNullOrWhiteSpace(searchText))
            {
                try
                {
                    searchRx = new Regex(searchText, RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    searchRegexError = "Regex: " + ex.Message;
                }
            }

            var poseContentMatch = new Dictionary<PoseGridItem, bool>(ReferenceEqualityComparer.Instance);
            foreach (var item in allItems)
            {
                var effectiveTags = CollectEffectiveFilterTags(item, groupById);
                poseContentMatch[item] = PosePassesContentFilters(
                    item, searchText, searchRx, effectiveTags, includeTagFilters, tagFilterAndMode, showFavoritesOnly);
            }

            foreach (var group in groupById.Values)
                groupVisible[group.Id] = GroupMetadataPassesFilters(group, searchText, searchRx, includeTagFilters, excludeTagFilters, tagFilterAndMode);

            var itemByRel = new Dictionary<string, PoseGridItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in allItems)
            {
                string rel = PoseGroupDatabase.NormalizeMemberPath(item.RelativePath(poseRoot));
                if (!string.IsNullOrEmpty(rel))
                    itemByRel[rel] = item;
            }

            foreach (var group in groupById.Values)
            {
                if (groupVisible[group.Id]) continue;
                foreach (var item in allItems)
                {
                    if (item.GroupId == group.Id && poseContentMatch[item])
                    {
                        groupVisible[group.Id] = true;
                        break;
                    }
                }
            }

            var included = new HashSet<PoseGridItem>(ReferenceEqualityComparer.Instance);
            var result = new List<PoseBrowserDisplayEntry>();

            foreach (var item in allItems)
            {
                string? gid = item.GroupId;
                if (!string.IsNullOrEmpty(gid) && groupById.ContainsKey(gid))
                {
                    if (groupsFilter == PoseDisplayFilterMode.Exclude)
                        continue;
                    if (!groupVisible.TryGetValue(gid, out bool vis) || !vis) continue;
                    if (included.Contains(item)) continue;
                    var group = groupById[gid];
                    var emitted = new List<PoseGridItem>();
                    foreach (var rel in group.MemberRelativePaths)
                    {
                        string normRel = PoseGroupDatabase.NormalizeMemberPath(rel);
                        if (!itemByRel.TryGetValue(normRel, out var member)) continue;
                        if (emitted.Contains(member)) continue;
                        emitted.Add(member);
                    }

                    foreach (var member in allItems)
                    {
                        if (member.GroupId != gid || emitted.Contains(member)) continue;
                        emitted.Add(member);
                    }

                    foreach (var member in emitted)
                    {
                        if (included.Contains(member)) continue;
                        if (!PassesThumbnailFilter(member, thumbnailFilter))
                            continue;
                        included.Add(member);
                        var memberTags = CollectEffectiveFilterTags(member, groupById);
                        bool dimmed = !poseContentMatch[member] ||
                            HasAnyExcludedTag(memberTags, excludeTagFilters);
                        result.Add(new PoseBrowserDisplayEntry(member, dimmed));
                    }
                }
                else if (groupsFilter != PoseDisplayFilterMode.IncludeOnly
                         && poseContentMatch[item]
                         && !HasAnyExcludedTag(CollectEffectiveFilterTags(item, groupById), excludeTagFilters)
                         && PassesThumbnailFilter(item, thumbnailFilter))
                {
                    if (included.Contains(item)) continue;
                    included.Add(item);
                    result.Add(new PoseBrowserDisplayEntry(item, false));
                }
            }

            return result;
        }

        private static bool PassesThumbnailFilter(PoseGridItem item, PoseDisplayFilterMode mode) =>
            mode switch
            {
                PoseDisplayFilterMode.Exclude => item.IsPng,
                PoseDisplayFilterMode.IncludeOnly => !item.IsPng,
                _ => true
            };

        public static void SortDisplayEntries(
            List<PoseBrowserDisplayEntry> entries,
            PoseGroupDatabase groupDb,
            string poseRootPath,
            PoseSortMode sortMode,
            bool ascending,
            IDictionary<string, PoseGroup>? groupsOverride = null)
        {
            var blocks = new List<SortBlock>();
            var seenGroup = new HashSet<string>(StringComparer.Ordinal);

            foreach (var e in entries)
            {
                string? gid = e.Item.GroupId;
                if (!string.IsNullOrEmpty(gid) && ResolveGroup(groupDb, gid, groupsOverride) is PoseGroup group)
                {
                    if (!seenGroup.Add(gid)) continue;
                    var members = entries.Where(x => x.Item.GroupId == gid).ToList();
                    OrderGroupMembersByGroupDefinition(members, group, poseRootPath);
                    blocks.Add(SortBlock.ForGroup(gid, members));
                }
                else
                {
                    blocks.Add(SortBlock.ForPose(e));
                }
            }

            blocks.Sort((a, b) => CompareBlocks(a, b, groupDb, sortMode, ascending));

            entries.Clear();
            foreach (var block in blocks)
            {
                if (block.IsGroup)
                    entries.AddRange(block.Members!);
                else
                    entries.Add(block.Single!);
            }
        }

        public static List<PoseBrowserDisplayEntry> SliceByPoseCount(
            IList<PoseBrowserDisplayEntry> entries, int skip, int take)
        {
            if (skip <= 0 && take <= 0) return entries.ToList();
            var result = new List<PoseBrowserDisplayEntry>();
            int index = 0;
            foreach (var e in entries)
            {
                if (index < skip) { index++; continue; }
                if (take > 0 && result.Count >= take) break;
                result.Add(e);
                index++;
            }

            return result;
        }

        public static List<PoseBrowserGridRow> BuildGridRows(
            IList<PoseBrowserDisplayEntry> entries,
            PoseGroupDatabase groupDb,
            int columns,
            IDictionary<string, PoseGroup>? groupsOverride = null)
        {
            var rows = new List<PoseBrowserGridRow>();
            if (columns < 1) columns = 1;

            int col = 0;
            PoseBrowserGridRow? row = null;

            void EnsureRow()
            {
                if (row == null || col >= columns)
                {
                    row = new PoseBrowserGridRow();
                    rows.Add(row);
                    col = 0;
                }
            }

            int i = 0;
            while (i < entries.Count)
            {
                var e = entries[i];
                string? gid = e.Item.GroupId;
                var group = !string.IsNullOrEmpty(gid) ? ResolveGroup(groupDb, gid, groupsOverride) : null;

                if (group == null)
                {
                    EnsureRow();
                    row!.Cells.Add(PoseBrowserGridCell.ForPose(e));
                    col++;
                    i++;
                    continue;
                }

                bool firstSegmentForGroup = true;
                bool showTagsOnNextSegment = false;
                while (i < entries.Count && entries[i].Item.GroupId == gid)
                {
                    EnsureRow();
                    int room = columns - col;
                    if (room <= 0)
                    {
                        col = columns;
                        continue;
                    }

                    var segmentPoses = new List<PoseBrowserDisplayEntry>();
                    while (i < entries.Count && entries[i].Item.GroupId == gid && segmentPoses.Count < room)
                    {
                        segmentPoses.Add(entries[i]);
                        i++;
                    }

                    int remainingAfter = 0;
                    for (int j = i; j < entries.Count && entries[j].Item.GroupId == gid; j++)
                        remainingAfter++;

                    var tags = group.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
                    var segment = new PoseBrowserGroupSegment(gid!, group.Name, tags)
                    {
                        ShowHeader = true,
                        IsContinuation = !firstSegmentForGroup,
                        ShowTags = showTagsOnNextSegment || firstSegmentForGroup
                    };
                    showTagsOnNextSegment = false;
                    segment.Poses.AddRange(segmentPoses);

                    if (firstSegmentForGroup)
                        showTagsOnNextSegment = ShouldDeferTagsToNextSegment(room, segmentPoses.Count, remainingAfter);

                    if (showTagsOnNextSegment)
                        segment.ShowTags = false;

                    row!.Cells.Add(PoseBrowserGridCell.ForGroup(segment));
                    col += segmentPoses.Count;
                    firstSegmentForGroup = false;

                    if (i < entries.Count && entries[i].Item.GroupId == gid)
                        col = columns;
                }
            }

            return rows;
        }

        public static int RowColumnSpan(PoseBrowserGridRow row)
        {
            int n = 0;
            foreach (var cell in row.Cells)
            {
                n += cell.Kind == PoseBrowserGridCellKind.GroupSegment
                    ? cell.GroupSegment!.Poses.Count
                    : 1;
            }

            return n;
        }

        private static PoseGroup? ResolveGroup(
            PoseGroupDatabase groupDb,
            string groupId,
            IDictionary<string, PoseGroup>? groupsOverride)
        {
            if (groupsOverride != null && groupsOverride.TryGetValue(groupId, out var g))
                return g;
            return groupDb.TryGetGroup(groupId);
        }

        private static bool ShouldDeferTagsToNextSegment(int roomOnRow, int segmentPoseCount, int remainingInGroup)
        {
            if (remainingInGroup <= 0) return false;
            if (segmentPoseCount > 1) return false;
            if (roomOnRow > 1) return false;
            int nextLineCount = Math.Min(remainingInGroup, roomOnRow);
            return nextLineCount > 1;
        }

        private sealed class SortBlock
        {
            public bool IsGroup { get; private set; }
            public string? GroupId { get; private set; }
            public PoseBrowserDisplayEntry? Single { get; private set; }
            public List<PoseBrowserDisplayEntry>? Members { get; private set; }

            public static SortBlock ForPose(PoseBrowserDisplayEntry e) =>
                new SortBlock { IsGroup = false, Single = e };

            public static SortBlock ForGroup(string groupId, List<PoseBrowserDisplayEntry> members) =>
                new SortBlock { IsGroup = true, GroupId = groupId, Members = members };
        }

        private static int CompareBlocks(
            SortBlock a,
            SortBlock b,
            PoseGroupDatabase groupDb,
            PoseSortMode sortMode,
            bool ascending)
        {
            int c;
            if (a.IsGroup && b.IsGroup)
                c = CompareGroupBlocks(a.Members!, b.Members!, sortMode, ascending);
            else if (!a.IsGroup && !b.IsGroup)
                c = CompareItems(a.Single!.Item, b.Single!.Item, sortMode, ascending);
            else if (a.IsGroup)
                c = CompareGroupToPose(a.Members!, b.Single!.Item, sortMode, ascending);
            else
                c = -CompareGroupToPose(b.Members!, a.Single!.Item, sortMode, ascending);

            if (c != 0) return c;
            string ta = a.IsGroup ? a.GroupId! : a.Single!.Item.FilePath;
            string tb = b.IsGroup ? b.GroupId! : b.Single!.Item.FilePath;
            return string.Compare(ta, tb, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareGroupBlocks(
            List<PoseBrowserDisplayEntry> a,
            List<PoseBrowserDisplayEntry> b,
            PoseSortMode sortMode,
            bool ascending)
        {
            return CompareItems(GetGroupRepresentative(a, sortMode, ascending).Item,
                GetGroupRepresentative(b, sortMode, ascending).Item, sortMode, ascending);
        }

        private static int CompareGroupToPose(
            List<PoseBrowserDisplayEntry> groupMembers,
            PoseGridItem pose,
            PoseSortMode sortMode,
            bool ascending)
        {
            return CompareItems(GetGroupRepresentative(groupMembers, sortMode, ascending).Item, pose, sortMode, ascending);
        }

        private static PoseBrowserDisplayEntry GetGroupRepresentative(
            List<PoseBrowserDisplayEntry> members,
            PoseSortMode sortMode,
            bool ascending)
        {
            var rep = members[0];
            for (int i = 1; i < members.Count; i++)
            {
                if (CompareItems(members[i].Item, rep.Item, sortMode, ascending) < 0)
                    rep = members[i];
            }

            return rep;
        }

        /// <summary>
        /// Keeps group members in persisted group order (first member = anchor). Grid sort must not reorder within a group.
        /// </summary>
        private static void OrderGroupMembersByGroupDefinition(
            List<PoseBrowserDisplayEntry> members,
            PoseGroup group,
            string poseRootPath)
        {
            var byRel = new Dictionary<string, PoseBrowserDisplayEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in members)
            {
                string rel = PoseGroupDatabase.NormalizeMemberPath(entry.Item.RelativePath(poseRootPath));
                if (string.IsNullOrEmpty(rel) || byRel.ContainsKey(rel))
                    continue;
                byRel[rel] = entry;
            }

            var ordered = new List<PoseBrowserDisplayEntry>(members.Count);
            var used = new HashSet<PoseBrowserDisplayEntry>();
            foreach (var rel in group.MemberRelativePaths)
            {
                string norm = PoseGroupDatabase.NormalizeMemberPath(rel);
                if (byRel.TryGetValue(norm, out var entry))
                {
                    ordered.Add(entry);
                    used.Add(entry);
                }
            }

            foreach (var entry in members)
            {
                if (!used.Contains(entry))
                    ordered.Add(entry);
            }

            members.Clear();
            members.AddRange(ordered);
        }

        private static int CompareItems(PoseGridItem a, PoseGridItem b, PoseSortMode sortMode, bool ascending)
        {
            int c = sortMode switch
            {
                PoseSortMode.Name => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
                PoseSortMode.LastUsed => a.LastUsedUtc.CompareTo(b.LastUsedUtc),
                PoseSortMode.LastUpdated => a.LastWriteTime.CompareTo(b.LastWriteTime),
                PoseSortMode.LastCreated => a.CreationTimeUtc.CompareTo(b.CreationTimeUtc),
                _ => 0
            };
            if (!ascending) c = -c;
            if (c != 0) return c;
            return string.Compare(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasAnyExcludedTag(IEnumerable<string> tags, HashSet<string> excludeTagFilters)
        {
            if (excludeTagFilters.Count == 0) return false;
            foreach (var t in tags)
            {
                if (excludeTagFilters.Contains(t))
                    return true;
            }

            return false;
        }

        private static HashSet<string> CollectEffectiveFilterTags(
            PoseGridItem item,
            IDictionary<string, PoseGroup> groupById)
        {
            var tags = new HashSet<string>(item.Tags, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(item.GroupId) &&
                groupById.TryGetValue(item.GroupId, out var group))
            {
                foreach (var t in group.Tags)
                    tags.Add(t);
            }

            return tags;
        }

        private static bool PosePassesContentFilters(
            PoseGridItem item,
            string searchText,
            Regex? searchRx,
            HashSet<string> effectiveTags,
            HashSet<string> includeTagFilters,
            bool tagFilterAndMode,
            bool showFavoritesOnly)
        {
            if (showFavoritesOnly && !item.IsFavorite)
                return false;

            if (!string.IsNullOrEmpty(searchText))
            {
                if (searchRx != null)
                {
                    try
                    {
                        if (!searchRx.IsMatch(item.DisplayName))
                            return false;
                    }
                    catch
                    {
                        return false;
                    }
                }
                else if (item.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            if (includeTagFilters.Count > 0)
            {
                if (tagFilterAndMode)
                {
                    if (!includeTagFilters.All(t => effectiveTags.Contains(t)))
                        return false;
                }
                else if (!includeTagFilters.Any(t => effectiveTags.Contains(t)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool GroupMetadataPassesFilters(
            PoseGroup group,
            string searchText,
            Regex? searchRx,
            HashSet<string> includeTagFilters,
            HashSet<string> excludeTagFilters,
            bool tagFilterAndMode)
        {
            if (HasAnyExcludedTag(group.Tags, excludeTagFilters))
                return false;

            bool searchActive = !string.IsNullOrEmpty(searchText);
            bool tagsActive = includeTagFilters.Count > 0;
            if (!searchActive && !tagsActive)
                return false;

            if (searchActive)
            {
                bool nameHit;
                if (searchRx != null)
                {
                    try { nameHit = searchRx.IsMatch(group.Name); }
                    catch { nameHit = false; }
                }
                else
                    nameHit = group.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

                if (nameHit) return true;
            }

            if (tagsActive)
            {
                if (tagFilterAndMode)
                {
                    if (includeTagFilters.All(t => group.Tags.Contains(t)))
                        return true;
                }
                else if (includeTagFilters.Any(t => group.Tags.Contains(t)))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<PoseGridItem>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public bool Equals(PoseGridItem? x, PoseGridItem? y) => ReferenceEquals(x, y);
            public int GetHashCode(PoseGridItem obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}

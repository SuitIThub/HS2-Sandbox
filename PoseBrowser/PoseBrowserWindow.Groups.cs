using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
        private PoseGroupDatabase _groupDb = null!;
        private List<PoseBrowserDisplayEntry> _displayEntries = new List<PoseBrowserDisplayEntry>();
        private GUIStyle? _dimmedCardStyle;
        private GUIStyle? _groupCardStyle;
        private GUIStyle? _groupCardSelectedStyle;
        private GUIStyle? _groupTitleStyle;
        private GUIStyle? _groupInnerCardStyle;
        private GUIStyle? _actionBarSeparatorStyle;

        private bool _showGroupNamePopup;
        private string _groupNamePopupText = "";
        private enum GroupNamePopupMode { None, Create, Rename }
        private GroupNamePopupMode _groupNamePopupMode = GroupNamePopupMode.None;
        private List<PoseGridItem>? _groupNamePopupMembers;

        private bool _tagWindowForGroup;
        private string? _tagWindowGroupId;
        private string? _renameGroupTargetId;

        /// <summary>Group entities selected in the grid (independent of pose <see cref="PoseGridItem.IsSelected"/>).</summary>
        private readonly HashSet<string> _selectedGroupIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Pack groups shown during import preview (not persisted until commit).</summary>
        private readonly Dictionary<string, PoseGroup> _importPreviewGroupsById =
            new Dictionary<string, PoseGroup>(StringComparer.Ordinal);

        private void InitGroupStyles()
        {
            if (_groupCardStyle != null) return;
            var skinBox = GUI.skin.box;
            _groupCardStyle = new GUIStyle(skinBox) { padding = new RectOffset(4, 4, 4, 4), margin = new RectOffset(0, 0, 0, 0) };
            _groupCardSelectedStyle = CardTintStyle(skinBox, new Color(0.22f, 0.48f, 0.98f, 0.88f));
            _groupCardSelectedStyle.padding = new RectOffset(4, 4, 4, 4);
            _groupCardSelectedStyle.margin = new RectOffset(0, 0, 0, 0);
            _groupInnerCardStyle = CardTintStyle(skinBox, new Color(0.32f, 0.32f, 0.32f, 0.55f));
            _groupInnerCardStyle.margin = new RectOffset(0, 0, 0, 0);
            _groupInnerCardStyle.padding = new RectOffset(0, 0, 2, 2);
            _groupTitleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _dimmedCardStyle = new GUIStyle(GUI.skin.box);
            var dimTex = MakeTintTexture(new Color(0.45f, 0.45f, 0.45f, 0.35f));
            _dimmedCardStyle.normal.background = dimTex;

            _actionBarSeparatorStyle = new GUIStyle
            {
                normal = { background = MakeTintTexture(new Color(0.55f, 0.55f, 0.55f, 0.85f)) },
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
        }

        private void DrawActionBarSeparator()
        {
            InitGroupStyles();
            GUILayout.Space(5f);
            GUILayout.Box(GUIContent.none, _actionBarSeparatorStyle!, GUILayout.ExpandWidth(true), GUILayout.Height(2f));
            GUILayout.Space(5f);
        }

        private void DrawActionBarVerticalSeparator(float height)
        {
            InitGroupStyles();
            GUILayout.Space(8f);
            GUILayout.Box(GUIContent.none, _actionBarSeparatorStyle!, GUILayout.Width(2f), GUILayout.Height(height));
            GUILayout.Space(8f);
        }

        private void DrawGroupEntityActionBar(PoseGroup group, IReadOnlyList<PoseGridItem> members, float barBtnH, float barBtnMinW)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Group: {group.Name}", GUILayout.MinWidth(120f), GUILayout.Height(barBtnH));
            GUILayout.Label($"({members.Count} poses)", GUILayout.Width(72f), GUILayout.Height(barBtnH));
            GUILayout.Space(8f);

            if (GUILayout.Button("Rename group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
            {
                _groupNamePopupText = group.Name;
                _groupNamePopupMode = GroupNamePopupMode.Rename;
                _groupNamePopupMembers = null;
                _renameGroupTargetId = group.Id;
                _showGroupNamePopup = true;
            }

            if (GUILayout.Button("Group tags…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
            {
                _tagWindowForGroup = true;
                _tagWindowGroupId = group.Id;
                OpenTagAssignWindow();
            }

            if (GUILayout.Button("Ungroup", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                UngroupEntity(group);

            if (GUILayout.Button("Export group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
                ExportGroupToDisk(group);

            if (GUILayout.Button("Move group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
            {
                _pendingFolderOp = PendingFolderOperation.MovePoses;
                _pendingFolderDestPath = SaveTargetFolderPath;
            }

            if (GUILayout.Button("Copy group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
            {
                _pendingFolderOp = PendingFolderOperation.CopyPoses;
                _pendingFolderDestPath = SaveTargetFolderPath;
            }

            DrawMultiCharacterApplyButton(barBtnH, barBtnMinW);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawPoseGroupingActions(IReadOnlyList<PoseGridItem> librarySelected, float barBtnH, float barBtnMinW, bool hideUngroup)
        {
            bool anyGrouped = SelectionHasGroupedPose(librarySelected);

            GUILayout.Label("Grouping", GUILayout.Width(64f), GUILayout.Height(barBtnH));

            GUI.enabled = librarySelected.Count >= 2 && librarySelected.All(s => string.IsNullOrEmpty(s.GroupId));
            if (GUILayout.Button("Group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
            {
                _groupNamePopupMembers = librarySelected.ToList();
                _groupNamePopupText = librarySelected[0].DisplayName;
                _groupNamePopupMode = GroupNamePopupMode.Create;
                _showGroupNamePopup = true;
            }

            GUI.enabled = anyGrouped;
            if (GUILayout.Button("Remove from group", GUILayout.Height(barBtnH), GUILayout.MinWidth(110f)))
                RemoveSelectedFromGroups(librarySelected);

            if (!hideUngroup)
            {
                if (GUILayout.Button("Ungroup", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                    UngroupSelected(librarySelected);
            }

            GUI.enabled = true;
        }

        private void ExportGroupToDisk(PoseGroup group)
        {
            var members = GetGroupMemberItems(group.Id);
            if (members.Count == 0) return;
            foreach (var it in members)
                _tagDb.ApplyToItem(it);

            string extNoDot = PosePackExchange.ZipExtension.TrimStart('.');
            string filter =
                $"HS2 Sandbox pose export (*.zip)\0*.zip\0All files (*.*)\0*.*\0";
            string? path = NativeFileDialog.SaveFile($"Export group \"{group.Name}\" (ZIP)", extNoDot, filter, _dataService.PoseRootPath);
            if (string.IsNullOrEmpty(path)) return;
            if (!path.EndsWith(PosePackExchange.ZipExtension, StringComparison.OrdinalIgnoreCase))
                path += PosePackExchange.ZipExtension;

            var relToZip = PosePackExchange.MapItemsToFlatZipPaths(_dataService.PoseRootPath, members);
            var groups = BuildExportGroupsForItems(members, relToZip);
            PosePackExchange.TryExportPosePack(path, _dataService.PoseRootPath, members, groups);
        }

        private bool TryGetDisplayGroup(string groupId, out PoseGroup? group)
        {
            if (ImportPreviewActive)
                return _importPreviewGroupsById.TryGetValue(groupId, out group);
            group = _groupDb.TryGetGroup(groupId);
            return group != null;
        }

        private List<PoseGridItem> GetGroupMemberItems(string groupId)
        {
            var list = new List<PoseGridItem>();
            if (ImportPreviewActive)
            {
                if (_importPreviewGroupsById.TryGetValue(groupId, out var importGroup))
                {
                    foreach (var entryId in importGroup.MemberRelativePaths)
                    {
                        var item = _allItems.FirstOrDefault(i =>
                            string.Equals(i.ImportPackEntryId, entryId, StringComparison.Ordinal));
                        if (item != null && !list.Contains(item))
                            list.Add(item);
                    }
                }

                foreach (var e in _displayEntries)
                {
                    if (e.Item.GroupId == groupId && !list.Contains(e.Item))
                        list.Add(e.Item);
                }

                return list;
            }

            foreach (var e in _displayEntries)
            {
                if (e.Item.GroupId == groupId)
                    list.Add(e.Item);
            }

            return list;
        }

        private bool IsGroupHeaderChecked(string groupId)
        {
            if (ImportPreviewActive)
            {
                var members = GetGroupMemberItems(groupId);
                return members.Count > 0 && members.All(m => m.IsSelected);
            }

            return IsGroupEntitySelected(groupId);
        }

        private bool IsGroupCardHighlighted(string groupId)
        {
            if (ImportPreviewActive)
            {
                var members = GetGroupMemberItems(groupId);
                return members.Count > 0 && members.All(m => m.IsSelected);
            }

            return IsGroupEntitySelected(groupId);
        }

        private bool IsGroupEntitySelected(string groupId) => _selectedGroupIds.Contains(groupId);

        private void ClearGroupSelection() => _selectedGroupIds.Clear();

        private void ClearPoseSelection()
        {
            foreach (var it in _filteredItems)
                it.IsSelected = false;
        }

        private void ClearAllSelection()
        {
            ClearPoseSelection();
            ClearGroupSelection();
        }

        private bool TryGetSingleSelectedGroup(out PoseGroup? group)
        {
            group = null;
            if (_selectedGroupIds.Count != 1) return false;
            group = _groupDb.TryGetGroup(_selectedGroupIds.First());
            return group != null;
        }

        private void PruneSelectedGroups()
        {
            _selectedGroupIds.RemoveWhere(id => _groupDb.TryGetGroup(id) == null);
        }

        /// <summary>Some member poses selected, but the group entity is not.</summary>
        private bool IsGroupMemberPoseSelectionPartial(string groupId)
        {
            var members = GetGroupMemberItems(groupId);
            if (members.Count == 0 || IsGroupEntitySelected(groupId)) return false;
            int n = members.Count(m => m.IsSelected);
            return n > 0 && n < members.Count;
        }

        private bool IsGroupMemberPoseSelectionAny(string groupId)
        {
            if (IsGroupEntitySelected(groupId)) return false;
            return GetGroupMemberItems(groupId).Any(m => m.IsSelected);
        }

        private void HandleImportGroupHeaderClick(string groupId, int anchorDisplayIndex)
        {
            Event e = Event.current;
            int globalIdx = DisplayIndexToGlobal(anchorDisplayIndex);
            var members = GetGroupMemberItems(groupId);
            if (members.Count == 0) return;

            if (e != null && e.control)
            {
                bool turnOn = !members.All(m => m.IsSelected);
                foreach (var m in members)
                    m.IsSelected = turnOn;
                _lastClickedGlobalIndex = globalIdx;
                return;
            }

            bool onlyThisGroupOn = members.All(m => m.IsSelected) &&
                !_filteredItems.Any(i => i.IsSelected && i.GroupId != groupId);
            if (onlyThisGroupOn)
            {
                foreach (var m in members)
                    m.IsSelected = false;
            }
            else
            {
                foreach (var it in _filteredItems)
                    it.IsSelected = false;
                foreach (var m in members)
                    m.IsSelected = true;
            }

            _lastClickedGlobalIndex = globalIdx;
        }

        private void HandleGroupHeaderClick(string groupId, int anchorDisplayIndex)
        {
            Event e = Event.current;
            int globalIdx = DisplayIndexToGlobal(anchorDisplayIndex);
            if (!TryGetDisplayGroup(groupId, out _))
                return;

            if (ImportPreviewActive)
            {
                HandleImportGroupHeaderClick(groupId, anchorDisplayIndex);
                return;
            }

            if (e != null && e.control)
            {
                if (_selectedGroupIds.Contains(groupId))
                    _selectedGroupIds.Remove(groupId);
                else
                {
                    ClearPoseSelection();
                    _selectedGroupIds.Add(groupId);
                }

                _lastClickedGlobalIndex = globalIdx;
                return;
            }

            bool onlyThisGroup = IsGroupEntitySelected(groupId) && _selectedGroupIds.Count == 1;
            if (onlyThisGroup)
                _selectedGroupIds.Remove(groupId);
            else
            {
                _selectedGroupIds.Clear();
                _selectedGroupIds.Add(groupId);
                ClearPoseSelection();
            }

            _lastClickedGlobalIndex = globalIdx;
        }

        private static Texture2D MakeTintTexture(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void RebuildDisplayList()
        {
            if (ImportPreviewActive)
            {
                RebuildImportPreviewDisplayList();
                return;
            }

            _displayEntries = PoseBrowserGridLayout.BuildFilteredDisplayList(
                _allItems,
                _groupDb,
                _dataService.PoseRootPath,
                _searchText,
                _searchUseRegex,
                ref _searchRegexError,
                _tagFiltersInclude,
                _tagFiltersExclude,
                _tagFilterAndMode,
                _showFavoritesOnly);
            PoseBrowserGridLayout.SortDisplayEntries(_displayEntries, _groupDb, _poseSortMode, _sortAscending);
            SyncFilteredItemsFromDisplay();
            PruneSelectedGroups();
        }

        private void ClearImportPreviewGroups()
        {
            _importPreviewGroupsById.Clear();
        }

        private void AssignImportPackGroups(PosePackExchange.PosePackReadResult result)
        {
            ClearImportPreviewGroups();
            foreach (var it in _allItems)
                it.GroupId = null;

            var zipToEntryId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _importEntryById)
            {
                string zip = PoseGroupDatabase.NormalizeMemberPath(kvp.Value.ZipInternalPath);
                if (!string.IsNullOrEmpty(zip))
                    zipToEntryId[zip] = kvp.Key;
            }

            int fallbackIdx = 0;
            foreach (var pg in result.Groups)
            {
                string groupId = !string.IsNullOrEmpty(pg.Id)
                    ? "import:" + pg.Id
                    : "import:" + fallbackIdx++;
                var group = new PoseGroup
                {
                    Id = groupId,
                    Name = string.IsNullOrWhiteSpace(pg.Name) ? "Group" : pg.Name.Trim(),
                    Tags = new HashSet<string>(pg.Tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                };

                foreach (var zipPath in pg.MemberZipPaths)
                {
                    string norm = PoseGroupDatabase.NormalizeMemberPath(zipPath);
                    if (!zipToEntryId.TryGetValue(norm, out var entryId)) continue;
                    var item = _allItems.FirstOrDefault(i =>
                        string.Equals(i.ImportPackEntryId, entryId, StringComparison.Ordinal));
                    if (item == null) continue;
                    item.GroupId = groupId;
                    group.MemberRelativePaths.Add(entryId);
                }

                if (group.MemberRelativePaths.Count > 0)
                    _importPreviewGroupsById[groupId] = group;
            }
        }

        private void RebuildImportPreviewDisplayList()
        {
            var result = new List<PoseBrowserDisplayEntry>();
            var included = new HashSet<PoseGridItem>();

            foreach (var group in _importPreviewGroupsById.Values)
            {
                foreach (var entryId in group.MemberRelativePaths)
                {
                    var item = _allItems.FirstOrDefault(i =>
                        string.Equals(i.ImportPackEntryId, entryId, StringComparison.Ordinal));
                    if (item == null || included.Contains(item)) continue;
                    included.Add(item);
                    result.Add(new PoseBrowserDisplayEntry(item, false));
                }

                foreach (var item in _allItems)
                {
                    if (item.GroupId != group.Id || included.Contains(item)) continue;
                    included.Add(item);
                    result.Add(new PoseBrowserDisplayEntry(item, false));
                }
            }

            foreach (var item in _allItems)
            {
                if (!string.IsNullOrEmpty(item.GroupId) || included.Contains(item)) continue;
                included.Add(item);
                result.Add(new PoseBrowserDisplayEntry(item, false));
            }

            _displayEntries = result;
            PoseBrowserGridLayout.SortDisplayEntries(
                _displayEntries, _groupDb, _poseSortMode, _sortAscending, _importPreviewGroupsById);
            SyncFilteredItemsFromDisplay();
        }

        private void SyncFilteredItemsFromDisplay()
        {
            _filteredItems = _displayEntries.Select(e => e.Item).ToList();
        }

        private List<PoseBrowserDisplayEntry> GetVisibleDisplayEntries()
        {
            if (_itemsPerPage <= 0)
                return _displayEntries;
            int skip = (_currentPage - 1) * _itemsPerPage;
            return PoseBrowserGridLayout.SliceByPoseCount(_displayEntries, skip, _itemsPerPage);
        }

        private int CountDisplayPoses() => _displayEntries.Count;

        private bool SelectionIsExactlyOneFullGroup(IReadOnlyList<PoseGridItem> selected, out PoseGroup? group)
        {
            group = null;
            if (selected.Count == 0) return false;
            string? gid = selected[0].GroupId;
            if (string.IsNullOrEmpty(gid)) return false;
            group = _groupDb.TryGetGroup(gid);
            if (group == null) return false;
            if (selected.Count != group.MemberRelativePaths.Count) return false;
            foreach (var rel in group.MemberRelativePaths)
            {
                bool found = selected.Any(s => string.Equals(s.RelativePath(_dataService.PoseRootPath), rel, StringComparison.OrdinalIgnoreCase));
                if (!found) return false;
            }

            return true;
        }

        private bool CanMoveCopyAsWholeGroup(IReadOnlyList<PoseGridItem> librarySelected, out PoseGroup? group)
        {
            if (TryGetSingleSelectedGroup(out group) && librarySelected.Count == 0)
                return true;
            return SelectionIsExactlyOneFullGroup(librarySelected, out group);
        }

        private bool SelectionHasGroupedPose(IReadOnlyList<PoseGridItem> selected)
        {
            return selected.Any(s => !string.IsNullOrEmpty(s.GroupId));
        }

        private void UngroupSelected(IReadOnlyList<PoseGridItem> selected)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in selected)
            {
                if (!string.IsNullOrEmpty(it.GroupId))
                    ids.Add(it.GroupId);
            }

            foreach (var gid in ids)
            {
                _groupDb.DissolveGroup(gid);
                _selectedGroupIds.Remove(gid);
            }

            foreach (var it in selected)
                it.GroupId = null;
            RebuildDisplayList();
        }

        private void UngroupEntity(PoseGroup group)
        {
            var members = GetGroupMemberItems(group.Id);
            _groupDb.DissolveGroup(group.Id);
            _selectedGroupIds.Remove(group.Id);
            foreach (var m in members)
                m.GroupId = null;
            RebuildDisplayList();
        }

        private void RemoveSelectedFromGroups(IReadOnlyList<PoseGridItem> selected)
        {
            foreach (var it in selected)
            {
                if (string.IsNullOrEmpty(it.GroupId)) continue;
                string rel = it.RelativePath(_dataService.PoseRootPath);
                _groupDb.RemoveMember(it.GroupId, rel);
                it.GroupId = null;
            }

            RebuildDisplayList();
        }

        private void ConfirmGroupNamePopup()
        {
            if (_groupNamePopupMode == GroupNamePopupMode.Create && _groupNamePopupMembers != null)
            {
                var g = _groupDb.CreateGroup(_groupNamePopupText, _groupNamePopupMembers);
                foreach (var it in _groupNamePopupMembers)
                    it.GroupId = g.Id;
            }
            else if (_groupNamePopupMode == GroupNamePopupMode.Rename && !string.IsNullOrEmpty(_renameGroupTargetId))
            {
                _groupDb.SetGroupName(_renameGroupTargetId, _groupNamePopupText);
                _renameGroupTargetId = null;
            }

            _showGroupNamePopup = false;
            _groupNamePopupMode = GroupNamePopupMode.None;
            _groupNamePopupMembers = null;
            RebuildDisplayList();
        }

        private void DrawGroupNamePopup()
        {
            if (!_showGroupNamePopup) return;
            GUILayout.BeginVertical(GUI.skin.box);
            string title = _groupNamePopupMode == GroupNamePopupMode.Create ? "New group name" : "Rename group";
            GUILayout.Label(title);
            _groupNamePopupText = GUILayout.TextField(_groupNamePopupText ?? "");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK", GUILayout.Width(80f)))
                ConfirmGroupNamePopup();
            if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
            {
                _showGroupNamePopup = false;
                _groupNamePopupMode = GroupNamePopupMode.None;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private List<PosePackExchange.PoseZipGroupJson> BuildExportGroupsForItems(
            IReadOnlyList<PoseGridItem> items,
            IReadOnlyDictionary<string, string> relToZipPath)
        {
            var relToZipNorm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in relToZipPath)
            {
                string key = PoseGroupDatabase.NormalizeMemberPath(kvp.Key);
                if (!string.IsNullOrEmpty(key))
                    relToZipNorm[key] = kvp.Value;
            }

            var rels = items
                .Select(i => PoseGroupDatabase.NormalizeMemberPath(i.RelativePath(_dataService.PoseRootPath)))
                .Where(r => !string.IsNullOrEmpty(r))
                .ToList();
            var groups = _groupDb.GetGroupsFullyContainedIn(rels);
            var result = new List<PosePackExchange.PoseZipGroupJson>();
            foreach (var g in groups)
            {
                var members = new List<string>();
                foreach (var rel in g.MemberRelativePaths)
                {
                    string normRel = PoseGroupDatabase.NormalizeMemberPath(rel);
                    if (relToZipNorm.TryGetValue(normRel, out var zipPath))
                        members.Add(PoseGroupDatabase.NormalizeMemberPath(zipPath));
                }

                if (members.Count == 0) continue;
                result.Add(new PosePackExchange.PoseZipGroupJson
                {
                    id = g.Id,
                    name = g.Name,
                    tags = g.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray(),
                    members = members.ToArray()
                });
            }

            return result;
        }

        private void ImportGroupsFromPack(
            IReadOnlyList<PosePackExchange.PosePackReadGroup> packGroups,
            IReadOnlyDictionary<string, string> zipPathToNewRel)
        {
            var zipToRelNorm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in zipPathToNewRel)
            {
                string key = PoseGroupDatabase.NormalizeMemberPath(kvp.Key);
                if (string.IsNullOrEmpty(key)) continue;
                string val = PoseGroupDatabase.NormalizeMemberPath(kvp.Value);
                if (!string.IsNullOrEmpty(val))
                    zipToRelNorm[key] = val;
            }

            foreach (var pg in packGroups)
            {
                var oldToNew = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var zipPath in pg.MemberZipPaths)
                {
                    string norm = PoseGroupDatabase.NormalizeMemberPath(zipPath);
                    if (string.IsNullOrEmpty(norm)) continue;
                    if (zipToRelNorm.TryGetValue(norm, out var newRel) && !string.IsNullOrEmpty(newRel))
                        oldToNew[norm] = newRel;
                }

                if (oldToNew.Count == 0) continue;
                var group = new PoseGroup
                {
                    Id = string.IsNullOrEmpty(pg.Id) ? Guid.NewGuid().ToString("N") : pg.Id,
                    Name = pg.Name,
                    Tags = new HashSet<string>(pg.Tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
                };
                _groupDb.ImportGroup(group, oldToNew);
            }
        }

        private void MoveGroupToFolder(PoseGroup group, string destFolder)
        {
            var members = new List<PoseGridItem>();
            foreach (var rel in group.MemberRelativePaths)
            {
                var item = _allItems.FirstOrDefault(i =>
                    string.Equals(i.RelativePath(_dataService.PoseRootPath), rel, StringComparison.OrdinalIgnoreCase));
                if (item != null) members.Add(item);
            }

            foreach (var it in members)
            {
                string oldPath = it.FilePath;
                string oldRel = it.RelativePath(_dataService.PoseRootPath);
                if (_dataService.MovePoseFileToFolder(it, destFolder, _tagDb))
                {
                    _groupDb.OnItemPathChanged(oldRel, it);
                    NotifyLibraryCachePoseMoved(oldPath, it);
                }
            }
        }

        private void CopyGroupToFolder(PoseGroup group, string destFolder)
        {
            var copies = new List<PoseGridItem>();
            foreach (var rel in group.MemberRelativePaths)
            {
                var item = _allItems.FirstOrDefault(i =>
                    string.Equals(i.RelativePath(_dataService.PoseRootPath), rel, StringComparison.OrdinalIgnoreCase));
                if (item == null) continue;
                var copy = _dataService.CopyPoseFileToFolder(item, destFolder, _tagDb);
                if (copy != null) copies.Add(copy);
            }

            if (copies.Count > 0)
            {
                _groupDb.CreateGroup(group.Name, copies, group.Tags);
                foreach (var c in copies)
                    NotifyLibraryCachePoseCopied(c);
            }
        }

        private void DrawTagWindowAssignGroupBody(PoseGroup group, string searchNormFold)
        {
            GUILayout.Label($"Group: {group.Name}", GUILayout.Height(20f));
            var union = group.Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            var allTags = _tagDb.GetAllKnownTags().Union(union).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
            var visible = string.IsNullOrEmpty(searchNormFold)
                ? allTags
                : allTags.Where(t => t.IndexOf(searchNormFold, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            _tagWindowScroll = GUILayout.BeginScrollView(_tagWindowScroll, GUILayout.ExpandHeight(true));
            foreach (var tag in visible)
            {
                bool has = group.Tags.Contains(tag);
                bool nv = GUILayout.Toggle(has, tag, GUILayout.Height(22f));
                if (nv != has)
                {
                    if (nv) group.Tags.Add(tag);
                    else group.Tags.Remove(tag);
                    _groupDb.SetGroupTags(group.Id, group.Tags);
                }
            }
            GUILayout.EndScrollView();
        }

        private List<(int start, int end)> BuildGroupSpansInDisplay()
        {
            var spans = new List<(int start, int end)>();
            int i = 0;
            while (i < _displayEntries.Count)
            {
                string? gid = _displayEntries[i].Item.GroupId;
                if (string.IsNullOrEmpty(gid) || _groupDb.TryGetGroup(gid) == null)
                {
                    i++;
                    continue;
                }

                int start = i;
                while (i < _displayEntries.Count && _displayEntries[i].Item.GroupId == gid)
                    i++;
                spans.Add((start, i - 1));
            }

            return spans;
        }

        private bool CompactPoseIndexInGroup(out int spanStart, out int spanEnd, out string? groupName)
        {
            spanStart = spanEnd = -1;
            groupName = null;
            if (_compactPoseIndex < 0 || _compactPoseIndex >= _displayEntries.Count) return false;
            string? gid = _displayEntries[_compactPoseIndex].Item.GroupId;
            if (string.IsNullOrEmpty(gid)) return false;
            var g = _groupDb.TryGetGroup(gid);
            if (g == null) return false;
            groupName = g.Name;
            for (int i = 0; i < _displayEntries.Count; i++)
            {
                if (_displayEntries[i].Item.GroupId != gid) continue;
                if (spanStart < 0) spanStart = i;
                spanEnd = i;
            }

            return spanStart >= 0;
        }

        private void AdvanceCompactGroup(int delta)
        {
            if (!CompactPoseIndexInGroup(out int spanStart, out int spanEnd, out _))
                return;
            int target = delta < 0 ? spanStart - 1 : spanEnd + 1;
            if (target < 0 || target >= _displayEntries.Count) return;
            _compactPoseIndex = target;
            ApplyPoseToSelectedWithUsage(_displayEntries[_compactPoseIndex].Item);
        }

        private void DrawGroupSegmentCell(
            PoseBrowserGroupSegment segment,
            float cellW,
            float slotW,
            ref int displayIndex)
        {
            int poseCount = segment.Poses.Count;
            float innerCellW = Mathf.Min(cellW, Mathf.Clamp(slotW - PoseCardHorizontalMarginBudget(), MinCardSize, MaxCardSize));
            float segmentW = poseCount > 0 ? poseCount * innerCellW : innerCellW;

            var cardStyle = IsGroupCardHighlighted(segment.GroupId) ? _groupCardSelectedStyle! : _groupCardStyle!;
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(segmentW), GUILayout.MaxWidth(segmentW), GUILayout.ExpandWidth(false));

            if (segment.ShowHeader)
            {
                int anchorIdx = displayIndex;
                var headerRect = GUILayoutUtility.GetRect(segmentW, 22f, GUILayout.Width(segmentW), GUILayout.MaxWidth(segmentW));
                if (Event.current.type == EventType.Repaint)
                {
                    bool mixed = IsGroupMemberPoseSelectionPartial(segment.GroupId);
                    bool groupOn = IsGroupHeaderChecked(segment.GroupId);
                    var cbRect = new Rect(headerRect.x + 2f, headerRect.y + 2f, 16f, 16f);
                    GUI.Toggle(cbRect, groupOn, "");
                    if (mixed || IsGroupMemberPoseSelectionAny(segment.GroupId))
                    {
                        var prev = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, 0.85f);
                        GUI.Label(new Rect(cbRect.x + 3f, cbRect.y + 1f, 12f, 12f), "◪");
                        GUI.color = prev;
                    }

                    string prefix = segment.IsContinuation ? "→ " : "";
                    string title = prefix + segment.GroupName;
                    var titleStyle = _groupTitleStyle!;
                    GUI.Label(new Rect(cbRect.xMax + 4f, headerRect.y, headerRect.width - cbRect.width - 8f, headerRect.height), title, titleStyle);
                }

                Event evHdr = Event.current;
                if (evHdr.type == EventType.MouseDown && evHdr.button == 0 && headerRect.Contains(evHdr.mousePosition))
                {
                    HandleGroupHeaderClick(segment.GroupId, anchorIdx);
                    evHdr.Use();
                }

                if (segment.ShowTags && segment.GroupTags.Count > 0)
                {
                    string tagStr = string.Join(" · ", segment.GroupTags);
                    var tagStyle = _tagWrapStyle!;
                    float tagH = MeasureTagBlockHeight(tagStr, tagStyle, segmentW);
                    GUILayout.Label(tagStr, tagStyle, GUILayout.Width(segmentW), GUILayout.MaxWidth(segmentW), GUILayout.Height(tagH));
                }

                GUILayout.Space(2f);
            }

            GUILayout.BeginHorizontal(GUILayout.Width(segmentW), GUILayout.MaxWidth(segmentW), GUILayout.ExpandWidth(false));
            for (int p = 0; p < segment.Poses.Count; p++)
            {
                DrawGridCell(segment.Poses[p], displayIndex, innerCellW, _groupInnerCardStyle);
                displayIndex++;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}

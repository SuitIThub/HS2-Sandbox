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
        private GUIStyle? _groupCardStyle;
        private GUIStyle? _groupCardSelectedStyle;
        private GUIStyle? _groupTitleStyle;
        private GUIStyle? _groupInnerCardChromeBase;
        private GUIStyle? _groupInnerCardStyle;
        private GUIStyle? _groupInnerCardSelectedStyle;
        private GUIStyle? _groupInnerCardFavoriteStyle;
        private GUIStyle? _groupInnerCardDimmedStyle;
        private GUIStyle? _actionBarSeparatorStyle;

        private bool _showGroupNamePopup;
        private string _groupNamePopupText = "";
        private enum GroupNamePopupMode { None, Create, Rename }
        private GroupNamePopupMode _groupNamePopupMode = GroupNamePopupMode.None;
        private List<PoseGridItem>? _groupNamePopupMembers;

        private bool _tagWindowForGroup;
        private string? _tagWindowGroupId;
        private readonly List<string> _tagWindowGroupIds = new List<string>();
        private string? _renameGroupTargetId;

        /// <summary>Group entities selected in the grid (independent of pose <see cref="PoseGridItem.IsSelected"/>).</summary>
        private readonly HashSet<string> _selectedGroupIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Pack groups shown during import preview (not persisted until commit).</summary>
        private readonly Dictionary<string, PoseGroup> _importPreviewGroupsById =
            new Dictionary<string, PoseGroup>(StringComparer.Ordinal);

        private void InitGroupStyles()
        {
            if (_groupCardStyle != null) return;
            InitStyles();

            var groupChrome = CreatePoseCardChromeTemplate();
            groupChrome.padding = new RectOffset(4, 4, 4, 4);
            groupChrome.margin = new RectOffset(0, 0, 0, 0);
            _groupCardStyle = CardTintStyle(groupChrome, GroupCardBaseTint);
            _groupCardSelectedStyle = CardTintStyle(_groupCardStyle, new Color(0.22f, 0.48f, 0.98f, 0.88f));

            _groupInnerCardChromeBase = new GUIStyle(_poseCardBaseStyle!)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 2, 2)
            };
            _groupInnerCardStyle = CardTintStyle(_groupInnerCardChromeBase, PoseCardBaseTint);
            _groupInnerCardSelectedStyle = CardTintStyle(_groupInnerCardChromeBase, new Color(0.22f, 0.48f, 0.98f, 0.88f));
            _groupInnerCardFavoriteStyle = CardTintStyle(_groupInnerCardChromeBase, new Color(0.95f, 0.82f, 0.22f, 0.72f));
            _groupInnerCardDimmedStyle = CardTintStyle(_groupInnerCardChromeBase, new Color(0.45f, 0.45f, 0.45f, 0.35f));

            _groupTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

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
                OpenGroupTagsForGroupIds(new[] { group.Id });

            if (GUILayout.Button("Ungroup", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                UngroupEntity(group);

            if (GUILayout.Button("Export group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
                ExportGroupToDisk(group);

            if (GUILayout.Button("Move group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
                BeginFolderOpForGroupEntities(PendingFolderOperation.MovePoses);

            if (GUILayout.Button("Copy group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(100f)))
                BeginFolderOpForGroupEntities(PendingFolderOperation.CopyPoses);

            DrawMultiCharacterApplyButton(barBtnH, barBtnMinW);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawMultiGroupEntityActionBar(float barBtnH, float barBtnMinW)
        {
            var groups = GetSelectedGroupEntities();
            var members = CollectMemberItemsFromSelectedGroups();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Groups: {groups.Count}", GUILayout.MinWidth(88f), GUILayout.Height(barBtnH));
            GUILayout.Label($"({members.Count} poses)", GUILayout.Width(72f), GUILayout.Height(barBtnH));
            GUILayout.Space(8f);

            if (GUILayout.Button("Group tags…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                OpenGroupTagsForSelectedGroupEntities();

            if (GUILayout.Button("Ungroup", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                UngroupSelectedEntities();

            if (GUILayout.Button("Export…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
            {
                string title = groups.Count == 1
                    ? $"Export group \"{groups[0].Name}\" (ZIP)"
                    : $"Export {groups.Count} groups ({members.Count} poses, ZIP)";
                ExportItemsToDisk(members, title);
            }

            if (GUILayout.Button("Move to folder…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                BeginFolderOpForGroupEntities(PendingFolderOperation.MovePoses);

            if (GUILayout.Button("Copy to folder…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
                BeginFolderOpForGroupEntities(PendingFolderOperation.CopyPoses);

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private List<PoseGroup> GetSelectedGroupEntities()
        {
            var list = new List<PoseGroup>();
            foreach (var gid in _selectedGroupIds)
            {
                var g = _groupDb.TryGetGroup(gid);
                if (g != null)
                    list.Add(g);
            }

            return list;
        }

        private List<PoseGridItem> CollectMemberItemsFromSelectedGroups()
        {
            var list = new List<PoseGridItem>();
            var seen = new HashSet<PoseGridItem>();
            foreach (var gid in _selectedGroupIds)
            {
                foreach (var item in GetGroupMemberItems(gid))
                {
                    if (seen.Add(item))
                        list.Add(item);
                }
            }

            return list;
        }

        private void OpenGroupTagsForSelectedGroupEntities()
        {
            OpenGroupTagsForGroupIds(_selectedGroupIds);
        }

        private void OpenGroupTagsForGroupIds(IEnumerable<string> groupIds)
        {
            _tagWindowGroupIds.Clear();
            foreach (var gid in groupIds)
            {
                if (_groupDb.TryGetGroup(gid) != null)
                    _tagWindowGroupIds.Add(gid);
            }

            if (_tagWindowGroupIds.Count == 0)
                return;

            _tagWindowForGroup = true;
            _tagWindowGroupId = _tagWindowGroupIds.Count == 1 ? _tagWindowGroupIds[0] : null;
            OpenTagAssignWindow();
        }

        private void UngroupSelectedEntities()
        {
            foreach (var gid in _selectedGroupIds.ToList())
            {
                var group = _groupDb.TryGetGroup(gid);
                if (group != null)
                    UngroupEntity(group);
            }
        }

        private void DrawPoseGroupingActions(IReadOnlyList<PoseGridItem> librarySelected, float barBtnH, float barBtnMinW, bool hideUngroup)
        {
            bool anyGrouped = SelectionHasGroupedPose(librarySelected);

            GUILayout.Label("Grouping", GUILayout.Width(64f), GUILayout.Height(barBtnH));

            GUI.enabled = librarySelected.Count >= 2 && librarySelected.All(s => string.IsNullOrEmpty(s.GroupId));
            if (GUILayout.Button("Group…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW)))
            {
                _groupNamePopupMembers = librarySelected.ToList();
                _groupNamePopupText = PoseGroupNameSuggest.Suggest(
                    librarySelected.Select(p => p.DisplayName).ToList());
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
            ExportItemsToDisk(members, $"Export group \"{group.Name}\" (ZIP)");
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

        /// <summary>Distinct group ids for poses in the current folder/library scope (<see cref="_allItems"/>).</summary>
        private HashSet<string> CollectGroupIdsInCurrentFolderView()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in _allItems)
            {
                if (!string.IsNullOrEmpty(it.GroupId))
                    ids.Add(it.GroupId);
            }

            return ids;
        }

        private void SelectAllGroupEntitiesInView()
        {
            ClearPoseSelection();
            foreach (var it in _allItems)
                it.IsSelected = false;
            ClearGroupSelection();

            if (ImportPreviewActive)
            {
                foreach (var gid in CollectVisibleGroupIdsInDisplay())
                {
                    foreach (var m in GetGroupMemberItems(gid))
                        m.IsSelected = true;
                }

                return;
            }

            foreach (var gid in CollectVisibleGroupIdsInDisplay())
            {
                if (_groupDb.TryGetGroup(gid) != null)
                    _selectedGroupIds.Add(gid);
            }
        }

        private void DeselectAllGroupsInCurrentFolderView()
        {
            if (ImportPreviewActive)
            {
                foreach (var gid in _importPreviewGroupsById.Keys)
                {
                    foreach (var m in GetGroupMemberItems(gid))
                        m.IsSelected = false;
                }

                return;
            }

            ClearGroupSelection();
        }

        private void ClearPoseSelection()
        {
            foreach (var it in _filteredItems)
                it.IsSelected = false;
        }

        private void ClearAllSelection()
        {
            foreach (var it in _allItems)
                it.IsSelected = false;
            ClearGroupSelection();
        }

        private HashSet<string> CollectVisibleGroupIdsInDisplay()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in _displayEntries)
            {
                if (!string.IsNullOrEmpty(e.Item.GroupId))
                    ids.Add(e.Item.GroupId);
            }

            return ids;
        }

        private void SelectAllStandalonePosesInView()
        {
            ClearGroupSelection();
            foreach (var it in _allItems)
                it.IsSelected = false;
            foreach (var e in _displayEntries)
            {
                if (e.IsDimmed || !string.IsNullOrEmpty(e.Item.GroupId))
                    continue;
                e.Item.IsSelected = true;
            }
        }

        private void SelectAllGroupedPosesInView()
        {
            ClearGroupSelection();
            foreach (var it in _allItems)
                it.IsSelected = false;
            foreach (var e in _displayEntries)
            {
                if (e.IsDimmed || string.IsNullOrEmpty(e.Item.GroupId))
                    continue;
                e.Item.IsSelected = true;
            }
        }

        private bool HasGroupEntitySelection() => _selectedGroupIds.Count > 0;

        private bool HasPoseCheckboxSelection()
        {
            foreach (var it in _allItems)
            {
                if (it.IsSelected)
                    return true;
            }

            return false;
        }

        private bool CanInvertSelection() =>
            HasGroupEntitySelection() || HasPoseCheckboxSelection();

        private void InvertSelectionInView()
        {
            if (HasGroupEntitySelection())
            {
                InvertGroupEntitySelectionInView();
                return;
            }

            if (HasPoseCheckboxSelection())
                InvertPoseCheckboxSelectionInView();
        }

        private void InvertGroupEntitySelectionInView()
        {
            foreach (var gid in CollectVisibleGroupIdsInDisplay())
            {
                if (_groupDb.TryGetGroup(gid) == null)
                    continue;
                if (_selectedGroupIds.Contains(gid))
                    _selectedGroupIds.Remove(gid);
                else
                    _selectedGroupIds.Add(gid);
            }
        }

        private void InvertPoseCheckboxSelectionInView()
        {
            foreach (var e in _displayEntries)
            {
                if (e.IsDimmed)
                    continue;
                e.Item.IsSelected = !e.Item.IsSelected;
            }
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
                _showFavoritesOnly,
                _tagFilterGroupsMode,
                _tagFilterThumbnailMode);
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
            group = null;
            if (librarySelected.Count == 0 && _selectedGroupIds.Count > 0)
                return true;
            if (TryGetSingleSelectedGroup(out group) && librarySelected.Count == 0)
                return true;
            return SelectionIsExactlyOneFullGroup(librarySelected, out group);
        }

        private void MoveCopyGroupsById(IReadOnlyList<string> groupIds, string destFolder, bool copy)
        {
            foreach (var gid in groupIds)
            {
                var group = _groupDb.TryGetGroup(gid);
                if (group == null) continue;
                if (copy)
                    CopyGroupToFolder(group, destFolder);
                else
                    MoveGroupToFolder(group, destFolder);
            }
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
            GUI.SetNextControlName("PoseBrowserGroupNameField");
            _groupNamePopupText = GUILayout.TextField(_groupNamePopupText ?? "");
            if (GUI.GetNameOfFocusedControl() == "PoseBrowserGroupNameField" &&
                Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                ConfirmGroupNamePopup();
                Event.current.Use();
            }

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
            var members = GetGroupMemberItems(group.Id);
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
            foreach (var item in GetGroupMemberItems(group.Id))
            {
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

        private void ApplyTagToGroups(IReadOnlyList<PoseGroup> groups, string tag, bool add)
        {
            if (groups.Count == 0) return;
            foreach (var g in groups)
            {
                if (add)
                    g.Tags.Add(tag);
                else
                    g.Tags.Remove(tag);
                _groupDb.SetGroupTags(g.Id, g.Tags);
            }

            ApplyFilters();
        }

        private void DrawTagWindowAssignMultiGroupBody(string searchNormFold)
        {
            var groups = new List<PoseGroup>();
            foreach (var gid in _tagWindowGroupIds)
            {
                var g = _groupDb.TryGetGroup(gid);
                if (g != null)
                    groups.Add(g);
            }

            if (groups.Count == 0)
            {
                GUILayout.Label("No groups selected — closing.");
                CloseTagWindow();
                return;
            }

            var hintStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(
                $"Groups: {groups.Count} — toggling a tag updates every selected group.",
                hintStyle,
                GUILayout.Height(36f));

            if (!string.IsNullOrEmpty(searchNormFold))
            {
                bool alreadyKnown = GetAllLibraryTagNames().Any(t =>
                    string.Equals(t, searchNormFold, StringComparison.OrdinalIgnoreCase));
                if (!alreadyKnown &&
                    GUILayout.Button($"Add new tag \"{searchNormFold}\" to all selected groups", GUILayout.Height(26f)))
                {
                    ApplyTagToGroups(groups, searchNormFold, add: true);
                }
            }

            var allTags = GetAllLibraryTagNames();
            var visible = string.IsNullOrEmpty(searchNormFold)
                ? allTags
                : allTags.Where(t => t.IndexOf(searchNormFold, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            GUILayout.Space(4f);
            if (visible.Count == 0)
            {
                GUILayout.Label("No tags match the search.");
                return;
            }

            _tagWindowScroll = GUILayout.BeginScrollView(_tagWindowScroll, GUILayout.ExpandHeight(true));
            foreach (var tag in visible)
            {
                int withTag = groups.Count(g => g.Tags.Contains(tag));
                bool allOn = withTag == groups.Count;
                bool mixed = withTag > 0 && !allOn;
                bool nv = GUILayout.Toggle(allOn, mixed ? $"◪ {tag}" : tag, GUILayout.Height(22f));
                if (nv == allOn && !mixed)
                    continue;

                ApplyTagToGroups(groups, tag, add: nv);
            }

            GUILayout.EndScrollView();
        }

        private void DrawTagWindowAssignGroupBody(PoseGroup group, string searchNormFold)
        {
            GUILayout.Label($"Group: {group.Name}", GUILayout.Height(20f));

            var groups = new List<PoseGroup> { group };
            if (!string.IsNullOrEmpty(searchNormFold))
            {
                bool alreadyKnown = GetAllLibraryTagNames().Any(t =>
                    string.Equals(t, searchNormFold, StringComparison.OrdinalIgnoreCase));
                if (!alreadyKnown &&
                    GUILayout.Button($"Add new tag \"{searchNormFold}\" to group", GUILayout.Height(26f)))
                {
                    ApplyTagToGroups(groups, searchNormFold, add: true);
                }
            }

            var allTags = GetAllLibraryTagNames();
            var visible = string.IsNullOrEmpty(searchNormFold)
                ? allTags
                : allTags.Where(t => t.IndexOf(searchNormFold, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            GUILayout.Space(4f);
            if (visible.Count == 0)
            {
                GUILayout.Label("No tags match the search.");
                return;
            }

            _tagWindowScroll = GUILayout.BeginScrollView(_tagWindowScroll, GUILayout.ExpandHeight(true));
            foreach (var tag in visible)
            {
                bool has = group.Tags.Contains(tag);
                bool nv = GUILayout.Toggle(has, tag, GUILayout.Height(22f));
                if (nv != has)
                    ApplyTagToGroups(groups, tag, add: nv);
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
            _compactSelectedGroupId = null;
            _compactPoseIndex = target;
            ApplyPoseToSelectedWithUsage(_displayEntries[_compactPoseIndex].Item);
        }

        private void DrawGroupSegmentCell(
            PoseBrowserGroupSegment segment,
            float cellInnerW,
            float columnFootprintW,
            float uniformPoseCardOuterH,
            float uniformTagBlockH,
            float uniformGroupTagBlockH,
            float rowOuterH,
            ref int displayIndex)
        {
            const float innerCardGap = 4f;
            int poseCount = segment.Poses.Count;

            var cardStyle = IsGroupCardHighlighted(segment.GroupId) ? _groupCardSelectedStyle! : _groupCardStyle!;
            int groupHPad = cardStyle.padding.left + cardStyle.padding.right;

            // Width reserved for this segment in the grid row (aligns with placeholders). Inner cards use cellInnerW,
            // so the framed group should hug that width — not the full N × column footprint.
            float tightInnerW = poseCount * cellInnerW + Mathf.Max(0, poseCount - 1) * innerCardGap;
            float tightSegmentW = tightInnerW + groupHPad;
            float innerContentW = Mathf.Max(40f, tightInnerW);

            GUILayout.BeginVertical(
                cardStyle,
                GUILayout.Width(tightSegmentW),
                GUILayout.MaxWidth(tightSegmentW),
                GUILayout.MinHeight(rowOuterH),
                GUILayout.Height(rowOuterH),
                GUILayout.ExpandWidth(false));

            if (segment.ShowHeader)
            {
                int anchorIdx = displayIndex;
                var headerRect = GUILayoutUtility.GetRect(innerContentW, 22f, GUILayout.Width(innerContentW), GUILayout.MaxWidth(innerContentW));
                var headerCbRect = new Rect(headerRect.x + 2f, headerRect.y + 2f, 16f, 16f);
                Event evHdr = Event.current;
                string prefix = segment.IsContinuation ? "→ " : "";
                string fullTitle = prefix + segment.GroupName;
                float titleW = Mathf.Max(20f, headerRect.width - headerCbRect.width - 8f);
                var titleRect = new Rect(headerCbRect.xMax + 4f, headerRect.y, titleW, headerRect.height);
                string shownTitle = TruncateWithEllipsis(fullTitle, _groupTitleStyle!, titleW);
                GUI.Label(titleRect, new GUIContent(shownTitle, fullTitle), _groupTitleStyle!);

                if (evHdr.type == EventType.Repaint)
                {
                    bool mixed = IsGroupMemberPoseSelectionPartial(segment.GroupId);
                    bool groupOn = IsGroupHeaderChecked(segment.GroupId);
                    DrawCheckboxVisual(headerCbRect, groupOn);
                    if (mixed || IsGroupMemberPoseSelectionAny(segment.GroupId))
                    {
                        var prev = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, 0.85f);
                        GUI.Label(new Rect(headerCbRect.x + 3f, headerCbRect.y + 1f, 12f, 12f), "◪");
                        GUI.color = prev;
                    }
                }
                else if (evHdr.type == EventType.MouseDown && evHdr.button == 0 && headerRect.Contains(evHdr.mousePosition))
                {
                    HandleGroupHeaderClick(segment.GroupId, anchorIdx);
                    evHdr.Use();
                }

                if (segment.ShowTags && uniformGroupTagBlockH > 0f)
                {
                    var tagRect = GUILayoutUtility.GetRect(
                        innerContentW,
                        uniformGroupTagBlockH,
                        GUILayout.Width(innerContentW),
                        GUILayout.MaxWidth(innerContentW),
                        GUILayout.ExpandWidth(false));
                    if (segment.GroupTags.Count > 0 && Event.current.type == EventType.Repaint)
                    {
                        string tagStr = string.Join(" · ", segment.GroupTags);
                        GUI.Label(tagRect, tagStr, _tagWrapStyle!);
                    }
                }

                GUILayout.Space(2f);
            }

            GUILayout.BeginHorizontal(GUILayout.Width(innerContentW), GUILayout.MaxWidth(innerContentW), GUILayout.ExpandWidth(false));
            for (int p = 0; p < segment.Poses.Count; p++)
            {
                if (p > 0)
                    GUILayout.Space(innerCardGap);
                DrawGridCell(
                    segment.Poses[p],
                    displayIndex,
                    cellInnerW,
                    uniformPoseCardOuterH,
                    uniformTagBlockH,
                    _groupInnerCardStyle);
                displayIndex++;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}

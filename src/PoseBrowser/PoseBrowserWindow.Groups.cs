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

        private void DrawGroupEntityActionBar(
            PoseGroup group,
            IReadOnlyList<PoseGridItem> members,
            float barBtnH,
            float barBtnMinW,
            float wrapWidth)
        {
            var wrap = new ActionBarWrapLayout();
            wrap.Begin(wrapWidth);

            string groupTitle = group.Name;
            float titleW = wrap.MeasureLabel(groupTitle, 48f);
            wrap.Add(titleW, () => GUILayout.Label(groupTitle, GUILayout.Width(titleW), GUILayout.Height(barBtnH)));

            string poseCount = $"({members.Count} poses)";
            float countW = wrap.MeasureLabel(poseCount, 48f);
            wrap.Add(countW, () => GUILayout.Label(poseCount, GUILayout.Width(countW), GUILayout.Height(barBtnH)));

            wrap.AddButton("Rename…", barBtnH, barBtnMinW, () =>
            {
                _groupNamePopupText = group.Name;
                _groupNamePopupMode = GroupNamePopupMode.Rename;
                _groupNamePopupMembers = null;
                _renameGroupTargetId = group.Id;
                _showGroupNamePopup = true;
            });

            wrap.AddButton("Tags…", barBtnH, barBtnMinW, () => OpenGroupTagsForGroupIds(new[] { group.Id }));
            wrap.AddButton("Ungroup", barBtnH, barBtnMinW, () => UngroupEntity(group));
            wrap.AddButton("Export…", barBtnH, barBtnMinW, () => ExportGroupToDisk(group));
            wrap.AddButton("Move…", barBtnH, barBtnMinW, () => BeginFolderOpForGroupEntities(PendingFolderOperation.MovePoses));
            wrap.AddButton("Copy…", barBtnH, barBtnMinW, () => BeginFolderOpForGroupEntities(PendingFolderOperation.CopyPoses));

            DrawMultiCharacterApplyButton(barBtnH, barBtnMinW, wrap, group.Id);
            DrawSaveGroupRelativePositionsButton(group, members, barBtnH, barBtnMinW, wrap);
            DrawClearGroupRelativePositionsButton(group, barBtnH, barBtnMinW, wrap);
            DrawApplyGroupRelativePositionsToggle(group, barBtnH, wrap);
            DrawApplyGroupRelativeHeightsToggle(group, barBtnH, wrap);
            DrawApplyGroupRelativeObjectScaleToggle(group, barBtnH, wrap);
            wrap.End();
        }

        private static bool GroupHasStoredRelativePositions(PoseGroup group) =>
            group.MemberRelativeOffsets.Count > 0 || group.MemberRelativeRotations.Count > 0;

        private static bool GroupHasStoredBodyHeights(PoseGroup group) =>
            group.MemberBodyHeights.Count > 0;

        private static bool GroupHasStoredObjectScales(PoseGroup group) =>
            group.MemberObjectScales.Count > 0;

        private void DrawApplyGroupRelativePositionsToggle(
            PoseGroup group,
            float barBtnH,
            ActionBarWrapLayout wrap)
        {
            if (!GroupHasStoredRelativePositions(group))
                return;

            string label = "Apply relative positions";
            float toggleW = wrap.MeasureLabel(label, 140f) + 22f;
            wrap.Add(toggleW, () =>
            {
                bool nv = GUILayout.Toggle(
                    _applyGroupRelativePositions,
                    label,
                    GUILayout.Height(barBtnH),
                    GUILayout.Width(toggleW));
                if (nv != _applyGroupRelativePositions)
                {
                    _applyGroupRelativePositions = nv;
                    if (!nv)
                    {
                        _applyGroupRelativeHeights = false;
                        _applyGroupRelativeObjectScales = false;
                    }
                    SavePersistedOptions();
                }
            });
        }

        private void DrawApplyGroupRelativeHeightsToggle(
            PoseGroup group,
            float barBtnH,
            ActionBarWrapLayout wrap)
        {
            if (!GroupHasStoredBodyHeights(group) || !GroupHasStoredRelativePositions(group))
                return;

            string label = "Adjust for body height";
            float toggleW = wrap.MeasureLabel(label, 150f) + 22f;
            wrap.Add(toggleW, () =>
            {
                GUI.enabled = _applyGroupRelativePositions;
                bool nv = GUILayout.Toggle(
                    _applyGroupRelativeHeights,
                    new GUIContent(label, "Applies the full saved relative position (first pose = anchor), then scales saved offset.y by current vs saved body-height ratios (no fixed meter constant)."),
                    GUILayout.Height(barBtnH),
                    GUILayout.Width(toggleW));
                GUI.enabled = true;
                if (nv != _applyGroupRelativeHeights)
                {
                    _applyGroupRelativeHeights = nv;
                    SavePersistedOptions();
                }
            });
        }

        private void DrawApplyGroupRelativeObjectScaleToggle(
            PoseGroup group,
            float barBtnH,
            ActionBarWrapLayout wrap)
        {
            if (!GroupHasStoredObjectScales(group) || !GroupHasStoredRelativePositions(group))
                return;

            string label = "Adjust for object scale";
            float toggleW = wrap.MeasureLabel(label, 150f) + 22f;
            wrap.Add(toggleW, () =>
            {
                GUI.enabled = _applyGroupRelativePositions;
                bool nv = GUILayout.Toggle(
                    _applyGroupRelativeObjectScales,
                    new GUIContent(label, "Applies the full saved relative position (first pose = anchor), then scales saved offset X/Y/Z by current vs saved Studio object-scale ratios (same spread logic as body height). Relative rotation is unchanged."),
                    GUILayout.Height(barBtnH),
                    GUILayout.Width(toggleW));
                GUI.enabled = true;
                if (nv != _applyGroupRelativeObjectScales)
                {
                    _applyGroupRelativeObjectScales = nv;
                    SavePersistedOptions();
                }
            });
        }

        private void DrawClearGroupRelativePositionsButton(
            PoseGroup group,
            float barBtnH,
            float barBtnMinW,
            ActionBarWrapLayout? wrap = null)
        {
            if (ImportPreviewActive) return;

            if (wrap != null)
            {
                bool canClear = GroupHasStoredRelativePositions(group);
                wrap.AddButton("Clear positions", barBtnH, barBtnMinW, () =>
                {
                    if (canClear)
                        ClearGroupRelativePositions(group);
                }, canClear);
            }
            else
            {
                GUI.enabled = GroupHasStoredRelativePositions(group);
                if (GUILayout.Button("Clear positions", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW + 40f)))
                    ClearGroupRelativePositions(group);
                GUI.enabled = true;
            }
        }

        private void ClearGroupRelativePositions(PoseGroup group)
        {
            if (!GroupHasStoredRelativePositions(group) && !GroupHasStoredBodyHeights(group) &&
                !GroupHasStoredObjectScales(group))
                return;
            _groupDb.ClearMemberRelativeOffsets(group.Id);
            SandboxServices.Log.LogMessage($"PoseBrowser: Cleared relative positions for group \"{group.Name}\".");
        }

        private void DrawSaveGroupRelativePositionsButton(
            PoseGroup group,
            IReadOnlyList<PoseGridItem> members,
            float barBtnH,
            float barBtnMinW,
            ActionBarWrapLayout? wrap = null)
        {
            bool canSave = CanSaveGroupRelativePositions(group, members, out string? tip);

            if (wrap != null)
            {
                wrap.AddButton(
                    "Save positions…",
                    barBtnH,
                    barBtnMinW,
                    () => SaveGroupRelativePositions(group, members),
                    canSave,
                    tip ?? "");
            }
            else
            {
                GUI.enabled = canSave;
                if (GUILayout.Button(
                        new GUIContent("Save positions…", tip ?? ""),
                        GUILayout.Height(barBtnH),
                        GUILayout.MinWidth(barBtnMinW + 60f)))
                    SaveGroupRelativePositions(group, members);
                GUI.enabled = true;
            }
        }

        private bool CanSaveGroupRelativePositions(
            PoseGroup group,
            IReadOnlyList<PoseGridItem> members,
            out string? disableReason)
        {
            disableReason = null;
            if (ImportPreviewActive)
            {
                disableReason = "Not available during import preview.";
                return false;
            }

            if (_anyPoseAppliedSinceLastGroupApply)
            {
                disableReason = "Apply this group's poses again without applying other poses first.";
                return false;
            }

            if (!string.Equals(_lastAppliedGroupId, group.Id, StringComparison.Ordinal))
            {
                disableReason = "Apply this group's poses to characters first (Apply to characters… or compact group apply).";
                return false;
            }

            var chars = _dataService.GetSelectedCharacters().ToList();
            if (chars.Count != members.Count)
            {
                disableReason =
                    $"Select exactly {members.Count} character(s) in Studio (currently {chars.Count} selected).";
                return false;
            }

            var poses = GetGroupMemberItemsInDisplayOrder(group.Id);
            if (!PoseBrowserCharacterApply.CanApplyPosesOneToOne(_characterConfig, poses, chars))
            {
                disableReason =
                    "Selected characters must match group pose genders (male/female tags and Chars priority list).";
                return false;
            }

            return true;
        }

        private void SaveGroupRelativePositions(PoseGroup group, IReadOnlyList<PoseGridItem> members)
        {
            if (!CanSaveGroupRelativePositions(group, members, out string? reason))
            {
                SandboxServices.Log.LogMessage($"PoseBrowser: Cannot save relative positions — {reason}");
                return;
            }

            var poses = GetGroupMemberItemsInDisplayOrder(group.Id);
            var chars = _dataService.GetSelectedCharacters().ToList();
            if (!PoseBrowserCharacterApply.TryBuildPoseCharacterAssignments(
                    _characterConfig, poses, chars, out var assignments) ||
                assignments == null || assignments.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Cannot save relative positions — assignment failed.");
                return;
            }

            if (!PoseDataService.TryGetCharacterWorldPosition(assignments[0].character, out Vector3 anchorPos))
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Cannot read anchor character world position.");
                return;
            }

            if (!PoseDataService.TryGetCharacterWorldRotation(assignments[0].character, out Quaternion anchorRot))
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Cannot read anchor character rotation.");
                return;
            }

            var offsets = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var heights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var scales = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var rotations = new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
            string anchorRel = assignments[0].pose.RelativePath(_dataService.PoseRootPath);
            if (!string.IsNullOrEmpty(anchorRel) &&
                PoseDataService.TryGetCharacterBodyHeight(assignments[0].character, out float anchorH))
                heights[anchorRel] = anchorH;
            if (!string.IsNullOrEmpty(anchorRel) &&
                PoseDataService.TryGetCharacterObjectScale(assignments[0].character, out Vector3 anchorScale))
                scales[anchorRel] = anchorScale;

            for (int i = 1; i < assignments.Count; i++)
            {
                if (!PoseDataService.TryGetCharacterWorldPosition(assignments[i].character, out Vector3 pos))
                {
                    SandboxServices.Log.LogMessage(
                        $"PoseBrowser: Cannot read world position for {PoseDataService.GetOCICharDisplayName(assignments[i].character)}.");
                    return;
                }

                if (!PoseDataService.TryGetCharacterWorldRotation(assignments[i].character, out Quaternion memberRot))
                {
                    SandboxServices.Log.LogMessage(
                        $"PoseBrowser: Cannot read rotation for {PoseDataService.GetOCICharDisplayName(assignments[i].character)}.");
                    return;
                }

                string rel = assignments[i].pose.RelativePath(_dataService.PoseRootPath);
                if (string.IsNullOrEmpty(rel))
                    continue;
                offsets[rel] = PoseBrowserCharacterApply.RelativePositionOffset(anchorRot, anchorPos, pos);
                Quaternion relativeRot = PoseBrowserCharacterApply.RelativeRotation(anchorRot, memberRot);
                if (!PoseBrowserCharacterApply.IsNearIdentityRelativeRotation(relativeRot))
                    rotations[rel] = relativeRot;
                if (PoseDataService.TryGetCharacterBodyHeight(assignments[i].character, out float h))
                    heights[rel] = h;
                if (PoseDataService.TryGetCharacterObjectScale(assignments[i].character, out Vector3 s))
                    scales[rel] = s;
            }

            _groupDb.SetMemberRelativeLayout(group.Id, offsets, heights, rotations, scales);
            SandboxServices.Log.LogMessage(
                $"PoseBrowser: Saved relative layout for group \"{group.Name}\" ({offsets.Count} offset(s), {rotations.Count} rotation(s), {heights.Count} height(s), {scales.Count} scale(s); anchor: {Path.GetFileName(anchorRel)}).");
        }

        private void RecordGroupMultiApply(string groupId)
        {
            _lastAppliedGroupId = groupId;
            _anyPoseAppliedSinceLastGroupApply = false;
        }

        private void RecordNonGroupPoseApply()
        {
            _anyPoseAppliedSinceLastGroupApply = true;
        }

        private bool TryGetGroupIdForExactPoseList(IReadOnlyList<PoseGridItem> poses, out string? groupId)
        {
            groupId = null;
            if (poses.Count == 0)
                return false;

            string gid = poses[0].GroupId ?? "";
            if (string.IsNullOrEmpty(gid))
                return false;

            for (int i = 1; i < poses.Count; i++)
            {
                if (!string.Equals(poses[i].GroupId, gid, StringComparison.Ordinal))
                    return false;
            }

            var ordered = GetGroupMemberItemsInDisplayOrder(gid);
            if (ordered.Count != poses.Count)
                return false;

            for (int i = 0; i < poses.Count; i++)
            {
                if (!ReferenceEquals(poses[i], ordered[i]) &&
                    !string.Equals(poses[i].FilePath, ordered[i].FilePath, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            groupId = gid;
            return true;
        }

        private void DrawMultiGroupEntityActionBar(float barBtnH, float barBtnMinW, float wrapWidth)
        {
            var groups = GetSelectedGroupEntities();
            var members = CollectMemberItemsFromSelectedGroups();
            var wrap = new ActionBarWrapLayout();
            wrap.Begin(wrapWidth);

            string groupsLabel = $"Groups: {groups.Count}";
            float groupsW = wrap.MeasureLabel(groupsLabel, 72f);
            wrap.Add(groupsW, () => GUILayout.Label(groupsLabel, GUILayout.Width(groupsW), GUILayout.Height(barBtnH)));

            string poseCount = $"({members.Count} poses)";
            float countW = wrap.MeasureLabel(poseCount, 48f);
            wrap.Add(countW, () => GUILayout.Label(poseCount, GUILayout.Width(countW), GUILayout.Height(barBtnH)));

            wrap.AddButton("Group tags…", barBtnH, barBtnMinW, OpenGroupTagsForSelectedGroupEntities);
            wrap.AddButton("Ungroup", barBtnH, barBtnMinW, UngroupSelectedEntities);
            wrap.AddButton("Export…", barBtnH, barBtnMinW, () =>
            {
                string title = groups.Count == 1
                    ? $"Export group \"{groups[0].Name}\" (ZIP)"
                    : $"Export {groups.Count} groups ({members.Count} poses, ZIP)";
                ExportItemsToDisk(members, title);
            });
            wrap.AddButton("Move to folder…", barBtnH, barBtnMinW, () => BeginFolderOpForGroupEntities(PendingFolderOperation.MovePoses));
            wrap.AddButton("Copy to folder…", barBtnH, barBtnMinW, () => BeginFolderOpForGroupEntities(PendingFolderOperation.CopyPoses));
            wrap.End();
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

        private void DrawPoseGroupingActions(
            IReadOnlyList<PoseGridItem> librarySelected,
            float barBtnH,
            float barBtnMinW,
            bool hideUngroup,
            ActionBarWrapLayout wrap)
        {
            bool anyGrouped = SelectionHasGroupedPose(librarySelected);
            float groupingW = wrap.MeasureLabel("Grouping", 56f);
            wrap.Add(groupingW, () => GUILayout.Label("Grouping", GUILayout.Width(groupingW), GUILayout.Height(barBtnH)));

            bool canGroup = librarySelected.Count >= 2 && librarySelected.All(s => string.IsNullOrEmpty(s.GroupId));
            wrap.AddButton("Group…", barBtnH, barBtnMinW, () =>
            {
                _groupNamePopupMembers = librarySelected.ToList();
                _groupNamePopupText = PoseGroupNameSuggest.Suggest(
                    librarySelected.Select(p => p.DisplayName).ToList());
                _groupNamePopupMode = GroupNamePopupMode.Create;
                _showGroupNamePopup = true;
            }, canGroup);

            wrap.AddButton("Remove from group", barBtnH, 96f, () => RemoveSelectedFromGroups(librarySelected), anyGrouped);

            if (!hideUngroup)
                wrap.AddButton("Ungroup", barBtnH, barBtnMinW, () => UngroupSelected(librarySelected), anyGrouped);
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
            PoseBrowserGridLayout.SortDisplayEntries(
                _displayEntries, _groupDb, _dataService.PoseRootPath, _poseSortMode, _sortAscending);
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
                _displayEntries, _groupDb, _dataService.PoseRootPath, _poseSortMode, _sortAscending, _importPreviewGroupsById);
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
                    members = members.ToArray(),
                    memberRelativeOffsets = BuildExportGroupMemberOffsets(g),
                    memberBodyHeights = BuildExportGroupMemberBodyHeights(g),
                    memberRelativeRotations = BuildExportGroupMemberRotations(g),
                    memberObjectScales = BuildExportGroupMemberObjectScales(g)
                });
            }

            return result;
        }

        private static float[]? BuildExportGroupMemberBodyHeights(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0 || group.MemberBodyHeights.Count == 0)
                return null;

            var rows = new float[group.MemberRelativePaths.Count];
            bool any = false;
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                string rel = group.MemberRelativePaths[i];
                if (group.MemberBodyHeights.TryGetValue(rel, out float h))
                {
                    rows[i] = h;
                    any = true;
                }
            }

            return any ? rows : null;
        }

        private static float[][]? BuildExportGroupMemberObjectScales(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0 || group.MemberObjectScales.Count == 0)
                return null;

            bool any = false;
            var rows = new float[group.MemberRelativePaths.Count][];
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                string rel = group.MemberRelativePaths[i];
                if (group.MemberObjectScales.TryGetValue(rel, out Vector3 scale))
                {
                    rows[i] = new float[] { scale.x, scale.y, scale.z };
                    any = true;
                }
                else
                {
                    rows[i] = new float[] { 1f, 1f, 1f };
                }
            }

            return any ? rows : null;
        }

        private static float[][]? BuildExportGroupMemberOffsets(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0 || group.MemberRelativeOffsets.Count == 0)
                return null;

            bool any = false;
            var rows = new float[group.MemberRelativePaths.Count][];
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                if (i == 0)
                {
                    rows[i] = new float[] { 0f, 0f, 0f };
                    continue;
                }

                string rel = group.MemberRelativePaths[i];
                if (group.MemberRelativeOffsets.TryGetValue(rel, out var offset))
                {
                    rows[i] = new float[] { offset.x, offset.y, offset.z };
                    if (offset.sqrMagnitude >= 1e-12f)
                        any = true;
                }
                else
                {
                    rows[i] = new float[] { 0f, 0f, 0f };
                }
            }

            return any ? rows : null;
        }

        private static float[][]? BuildExportGroupMemberRotations(PoseGroup group)
        {
            if (group.MemberRelativePaths.Count == 0 || group.MemberRelativeRotations.Count == 0)
                return null;

            bool any = false;
            var rows = new float[group.MemberRelativePaths.Count][];
            for (int i = 0; i < group.MemberRelativePaths.Count; i++)
            {
                if (i == 0)
                {
                    rows[i] = new float[] { 0f, 0f, 0f, 1f };
                    continue;
                }

                string rel = group.MemberRelativePaths[i];
                if (group.MemberRelativeRotations.TryGetValue(rel, out var rot) &&
                    !PoseBrowserCharacterApply.IsNearIdentityRelativeRotation(rot))
                {
                    rows[i] = new float[] { rot.x, rot.y, rot.z, rot.w };
                    any = true;
                }
                else
                {
                    rows[i] = new float[] { 0f, 0f, 0f, 1f };
                }
            }

            return any ? rows : null;
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
                _groupDb.ImportGroup(group, oldToNew, pg.MemberRelativeOffsets, pg.MemberBodyHeights, pg.MemberRelativeRotations, pg.MemberObjectScales);
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
                var newGroup = _groupDb.CreateGroup(group.Name, copies, group.Tags);
                CopyGroupRelativeOffsets(group, GetGroupMemberItems(group.Id), copies, newGroup);
                foreach (var c in copies)
                    NotifyLibraryCachePoseCopied(c);
            }
        }

        private void CopyGroupRelativeOffsets(
            PoseGroup sourceGroup,
            IReadOnlyList<PoseGridItem> sourceMembers,
            IReadOnlyList<PoseGridItem> copyMembers,
            PoseGroup destGroup)
        {
            if ((sourceGroup.MemberRelativeOffsets.Count == 0 &&
                 sourceGroup.MemberBodyHeights.Count == 0 &&
                 sourceGroup.MemberObjectScales.Count == 0 &&
                 sourceGroup.MemberRelativeRotations.Count == 0) ||
                sourceMembers.Count != copyMembers.Count)
                return;

            var offsets = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var heights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var scales = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var rotations = new Dictionary<string, Quaternion>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < copyMembers.Count; i++)
            {
                string oldRel = sourceMembers[i].RelativePath(_dataService.PoseRootPath);
                string newRel = copyMembers[i].RelativePath(_dataService.PoseRootPath);
                if (string.IsNullOrEmpty(newRel))
                    continue;

                if (i > 0 && sourceGroup.MemberRelativeOffsets.TryGetValue(oldRel, out var offset))
                    offsets[newRel] = offset;

                if (i > 0 && sourceGroup.MemberRelativeRotations.TryGetValue(oldRel, out var rot))
                    rotations[newRel] = rot;

                if (sourceGroup.MemberBodyHeights.TryGetValue(oldRel, out float h))
                    heights[newRel] = h;

                if (sourceGroup.MemberObjectScales.TryGetValue(oldRel, out Vector3 scale))
                    scales[newRel] = scale;
            }

            if (offsets.Count > 0 || heights.Count > 0 || scales.Count > 0 || rotations.Count > 0)
                _groupDb.SetMemberRelativeLayout(destGroup.Id, offsets, heights, rotations, scales);
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
                const float applyBtnW = 24f;
                float titleW = Mathf.Max(20f, headerRect.width - headerCbRect.width - applyBtnW - 10f);
                var titleRect = new Rect(headerCbRect.xMax + 4f, headerRect.y, titleW, headerRect.height);
                var applyBtnRect = new Rect(headerRect.xMax - applyBtnW - 2f, headerRect.y + 2f, applyBtnW, headerRect.height - 4f);
                string shownTitle = TruncateWithEllipsis(fullTitle, _groupTitleStyle!, titleW);
                GUI.Label(titleRect, new GUIContent(shownTitle, fullTitle), _groupTitleStyle!);

                bool canGroupApply = !ImportPreviewActive &&
                    _dataService.GetSelectedCharacters().Any() && segment.Poses.Count > 0;
                string? applyTooltip = BuildGroupApplyAssignmentTooltip(segment.GroupId);
                GUI.enabled = canGroupApply;
                if (GUI.Button(applyBtnRect, new GUIContent("▶", applyTooltip ?? "")))
                    ApplyGroupMembersToSelectedCharacters(segment.GroupId);
                GUI.enabled = true;

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
                else if (evHdr.type == EventType.MouseDown && evHdr.button == 0 &&
                         headerRect.Contains(evHdr.mousePosition) &&
                         !applyBtnRect.Contains(evHdr.mousePosition))
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

        /// <summary>Wraps IMGUI toolbar controls onto multiple rows when the window is narrow.</summary>
        private sealed class ActionBarWrapLayout
        {
            private const float ButtonPad = 12f;
            private const float LabelPad = 4f;

            private float _wrapWidth;
            private float _used;
            private bool _rowOpen;
            private float _gap;
            private readonly GUIStyle _buttonStyle;
            private readonly GUIStyle _labelStyle;

            public ActionBarWrapLayout()
            {
                _buttonStyle = GUI.skin.button;
                _labelStyle = GUI.skin.label;
            }

            public void Begin(float wrapWidth, float gap = 6f)
            {
                _wrapWidth = Mathf.Max(80f, wrapWidth);
                _gap = gap;
                _used = 0f;
                _rowOpen = false;
            }

            public float MeasureLabel(string text, float minWidth = 0f) =>
                Mathf.Max(minWidth, _labelStyle.CalcSize(new GUIContent(text)).x + LabelPad);

            public float MeasureButton(string text, float minWidth) =>
                Mathf.Max(minWidth, _buttonStyle.CalcSize(new GUIContent(text)).x + ButtonPad);

            public void AddButton(
                string text,
                float height,
                float minWidth,
                Action onClick,
                bool enabled = true,
                string? tooltip = null)
            {
                float width = MeasureButton(text, minWidth);
                var content = new GUIContent(text, tooltip ?? "");
                Add(width, () =>
                {
                    GUI.enabled = enabled;
                    if (GUILayout.Button(content, GUILayout.Height(height), GUILayout.Width(width)))
                        onClick();
                    GUI.enabled = true;
                });
            }

            public void Add(float width, Action draw)
            {
                float reserve = (_rowOpen ? _gap : 0f) + width;
                if (_rowOpen && _used + reserve > _wrapWidth + 0.5f)
                    EndRow();

                if (!_rowOpen)
                {
                    GUILayout.BeginHorizontal(GUILayout.MaxWidth(_wrapWidth), GUILayout.ExpandWidth(false));
                    _rowOpen = true;
                    _used = 0f;
                }
                else
                {
                    GUILayout.Space(_gap);
                    _used += _gap;
                }

                draw();
                _used += Mathf.Max(width, GUILayoutUtility.GetLastRect().width);
            }

            public void End() => EndRow();

            private void EndRow()
            {
                if (!_rowOpen)
                    return;
                GUILayout.EndHorizontal();
                _rowOpen = false;
                _used = 0f;
            }
        }
    }
}

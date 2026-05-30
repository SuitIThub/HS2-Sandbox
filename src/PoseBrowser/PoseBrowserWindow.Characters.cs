using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
        private readonly PoseBrowserCharacterConfig _characterConfig = new PoseBrowserCharacterConfig();
        private bool _showCharacterConfigPane;
        private Rect _characterConfigWindowRect;
        private Vector2 _characterConfigScroll;
        private int _selectedSlotIndex = -1;

        private void DrawCharacterConfigWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label(
                "Priority list for multi-character pose apply. Top = highest priority.",
                GUILayout.Height(32f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load characters", GUILayout.Height(26f)))
            {
                _characterConfig.LoadNewFromScene(_dataService.GetSceneCharacters());
                _selectedSlotIndex = -1;
            }

            if (GUILayout.Button("Remove missing", GUILayout.Height(26f)))
            {
                int removed = _characterConfig.RemoveSlotsNotInScene();
                if (removed > 0)
                    _selectedSlotIndex = -1;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            var slots = _characterConfig.Priority;
            var selectedInStudio = new HashSet<OCIChar>(_dataService.GetSelectedCharacters());
            _characterConfigScroll = GUILayout.BeginScrollView(_characterConfigScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                int slotIndex = i;
                bool inScene = PoseBrowserCharacterSlot.TryResolveInScene(slot, out var oci);
                bool studioSelected = inScene && selectedInStudio.Contains(oci);
                bool rowOn = _selectedSlotIndex == i;
                Color prev = GUI.color;
                if (!inScene)
                    GUI.color = new Color(1f, 0.75f, 0.55f, 1f);
                else if (studioSelected)
                    GUI.color = new Color(0.55f, 1f, 0.65f, 1f);

                GUILayout.BeginHorizontal();
                string genderLabel = slot.IsFemale ? "f" : "m";
                if (GUILayout.Button(genderLabel, GUILayout.Width(24f), GUILayout.Height(22f)))
                    _characterConfig.ToggleSlotGender(i);

                string label = $"{i + 1}. {slot.DisplayName}";
                if (GUILayout.Toggle(rowOn, label, GUI.skin.button, GUILayout.Height(22f), GUILayout.ExpandWidth(true)))
                    _selectedSlotIndex = i;
                else if (rowOn)
                    _selectedSlotIndex = -1;

                if (GUILayout.Button(
                        new GUIContent("✕", "Remove this character from the priority list."),
                        GUILayout.Width(28f),
                        GUILayout.Height(22f)))
                {
                    _characterConfig.RemoveSlot(slotIndex);
                    if (_selectedSlotIndex == slotIndex)
                        _selectedSlotIndex = -1;
                    else if (_selectedSlotIndex > slotIndex)
                        _selectedSlotIndex--;
                }

                GUILayout.EndHorizontal();
                GUI.color = prev;
            }

            GUILayout.EndScrollView();

            GUI.enabled = _selectedSlotIndex >= 0 && _selectedSlotIndex < slots.Count;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("↑", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                _characterConfig.MoveSlot(_selectedSlotIndex, -1);
                _selectedSlotIndex = Math.Max(0, _selectedSlotIndex - 1);
            }

            if (GUILayout.Button("↓", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                _characterConfig.MoveSlot(_selectedSlotIndex, 1);
                _selectedSlotIndex = Math.Min(slots.Count - 1, _selectedSlotIndex + 1);
            }

            if (GUILayout.Button("✕", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                _characterConfig.RemoveSlot(_selectedSlotIndex);
                _selectedSlotIndex = -1;
            }

            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(6f);
            if (GUILayout.Button("Close", GUILayout.Height(26f)))
                _showCharacterConfigPane = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private bool CanShowMultiCharacterApply()
        {
            if (ImportPreviewActive) return false;
            int poseCount = GetPosesForMultiCharacterApply().Count;
            bool groupOnly = TryGetSingleSelectedGroup(out _) &&
                !_filteredItems.Any(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId));
            if (groupOnly)
                return poseCount >= 1;
            return poseCount >= 2;
        }

        private List<PoseGridItem> GetPosesForMultiCharacterApply()
        {
            bool groupOnly = TryGetSingleSelectedGroup(out var group) &&
                !_filteredItems.Any(i => i.IsSelected && string.IsNullOrEmpty(i.ImportPackEntryId));
            if (groupOnly && group != null)
                return GetGroupMemberItemsInDisplayOrder(group.Id);

            var list = new List<PoseGridItem>();
            foreach (var e in _displayEntries)
            {
                if (e.Item.IsSelected && string.IsNullOrEmpty(e.Item.ImportPackEntryId))
                    list.Add(e.Item);
            }

            return list;
        }

        private List<PoseGridItem> GetGroupMemberItemsInDisplayOrder(string groupId)
        {
            var list = new List<PoseGridItem>();
            foreach (var e in _displayEntries)
            {
                if (e.Item.GroupId == groupId)
                    list.Add(e.Item);
            }

            return list;
        }

        private void ApplyPosesToCharactersMulti()
        {
            if (TryGetSingleSelectedGroup(out var group))
            {
                ApplyPosesListToSelectedCharacters(GetGroupMemberItemsInDisplayOrder(group!.Id), group.Id);
                return;
            }

            ApplyPosesListToSelectedCharacters(GetPosesForMultiCharacterApply());
        }

        private void ApplyGroupMembersToSelectedCharacters(string groupId)
        {
            ApplyPosesListToSelectedCharacters(GetGroupMemberItemsInDisplayOrder(groupId), groupId);
        }

        private void ApplyPosesListToSelectedCharacters(IReadOnlyList<PoseGridItem> poses, string? knownGroupId = null)
        {
            PoseGroup? layoutGroup = null;
            if (!string.IsNullOrEmpty(knownGroupId))
            {
                RecordGroupMultiApply(knownGroupId);
                layoutGroup = _groupDb.TryGetGroup(knownGroupId);
            }
            else if (TryGetGroupIdForExactPoseList(poses, out string? appliedGroupId) && !string.IsNullOrEmpty(appliedGroupId))
            {
                RecordGroupMultiApply(appliedGroupId);
                layoutGroup = _groupDb.TryGetGroup(appliedGroupId);
            }
            else
                RecordNonGroupPoseApply();

            foreach (var pose in poses)
                _tagDb.ApplyToItem(pose);

            var chars = _dataService.GetSelectedCharacters().ToList();
            RecordPoseHistoryBeforeMultiApply(poses, chars);
            if (poses.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: No poses selected for multi-character apply.");
                return;
            }

            if (chars.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Select one or more characters in Studio first.");
                return;
            }

            int applied = PoseBrowserCharacterApply.ApplyPosesToSelectedCharacters(
                _dataService,
                _characterConfig,
                poses,
                chars,
                pose =>
                {
                    _tagDb.RecordLastUsed(pose);
                });

            int layoutMoved = 0;
            bool heightAdjust = _applyGroupRelativeHeights && _applyGroupRelativePositions;
            bool scaleAdjust = _applyGroupRelativeObjectScales && _applyGroupRelativePositions;
            if (_applyGroupRelativePositions &&
                layoutGroup != null &&
                (layoutGroup.MemberRelativeOffsets.Count > 0 || layoutGroup.MemberRelativeRotations.Count > 0))
            {
                layoutMoved = PoseBrowserCharacterApply.ApplyGroupRelativePositions(
                    layoutGroup,
                    _characterConfig,
                    poses,
                    chars,
                    _dataService.PoseRootPath,
                    heightAdjust,
                    scaleAdjust);
            }

            RecordPoseHistoryAfterMultiApply(poses, chars);

            if (_poseSortMode == PoseSortMode.LastUsed)
            {
                ResortPoseItemsInPlace();
                ApplyFilters();
            }

            if (layoutMoved > 0)
            {
                string adjustNote = "";
                if (heightAdjust && scaleAdjust)
                    adjustNote = " (body-height Y + object-scale XYZ adjustment on)";
                else if (heightAdjust)
                    adjustNote = " (body-height Y adjustment on)";
                else if (scaleAdjust)
                    adjustNote = " (object-scale XYZ adjustment on)";
                SandboxServices.Log.LogMessage(
                    $"PoseBrowser: Applied {applied} pose(s) to {chars.Count} character(s) and {layoutMoved} relative layout change(s) from group \"{layoutGroup!.Name}\"{adjustNote}.");
            }
            else if (layoutGroup != null &&
                     (layoutGroup.MemberRelativeOffsets.Count > 0 || layoutGroup.MemberRelativeRotations.Count > 0))
            {
                SandboxServices.Log.LogMessage(
                    "PoseBrowser: Applied pose(s); relative layout was not applied — the anchor pose (first in the group) must be assigned to a selected character (gender tags and Chars priority).");
            }
            else
            {
                SandboxServices.Log.LogMessage(
                    $"PoseBrowser: Applied {applied} pose(s) to {chars.Count} selected character(s) using the priority list.");
            }
        }

        private bool TryGetGroupIdForApplyTooltip(out string? groupId)
        {
            groupId = null;
            if (TryGetSingleSelectedGroup(out var group) && group != null)
            {
                groupId = group.Id;
                return true;
            }

            var poses = GetPosesForMultiCharacterApply();
            if (poses.Count > 0 &&
                TryGetGroupIdForExactPoseList(poses, out string? exactGroupId) &&
                !string.IsNullOrEmpty(exactGroupId))
            {
                groupId = exactGroupId;
                return true;
            }

            return false;
        }

        private string? BuildGroupApplyAssignmentTooltip(string groupId)
        {
            var poses = GetGroupMemberItemsInDisplayOrder(groupId);
            if (poses.Count == 0)
                return null;

            var chars = _dataService.GetSelectedCharacters().ToList();
            if (chars.Count == 0)
                return "Select characters in Studio to preview assignments.";

            var plan = PoseBrowserCharacterApply.BuildFullPlannedPoseAssignmentPlan(_characterConfig, poses, chars);
            var sb = new StringBuilder(poses.Count * 40);
            for (int i = 0; i < plan.Count; i++)
            {
                var (pose, characters) = plan[i];
                if (i > 0)
                    sb.Append('\n');
                string poseLabel = GetPoseLabelForTooltip(pose);
                sb.Append(poseLabel).Append(" → ");
                if (characters.Count == 0)
                    sb.Append("(none)");
                else
                {
                    for (int c = 0; c < characters.Count; c++)
                    {
                        if (c > 0)
                            sb.Append(", ");
                        sb.Append(PoseDataService.GetOCICharDisplayName(characters[c]));
                    }
                }
            }

            return sb.ToString();
        }

        private string GetPoseLabelForTooltip(PoseGridItem pose)
        {
            if (!string.IsNullOrWhiteSpace(pose.DisplayName))
                return pose.DisplayName.Trim();
            string rel = pose.RelativePath(_dataService.PoseRootPath);
            if (!string.IsNullOrEmpty(rel))
                return rel;
            return pose.FilePath;
        }

        private void DrawMultiCharacterApplyButton(
            float barBtnH,
            float barBtnMinW,
            ActionBarWrapLayout? wrap = null,
            string? groupIdForTooltip = null)
        {
            if (!CanShowMultiCharacterApply()) return;

            bool canApply = _dataService.GetSelectedCharacters().Any();
            if (string.IsNullOrEmpty(groupIdForTooltip))
                TryGetGroupIdForApplyTooltip(out groupIdForTooltip);
            string? tooltip = !string.IsNullOrEmpty(groupIdForTooltip)
                ? BuildGroupApplyAssignmentTooltip(groupIdForTooltip)
                : null;
            if (wrap != null)
            {
                wrap.AddButton(
                    "Apply to characters…",
                    barBtnH,
                    barBtnMinW,
                    ApplyPosesToCharactersMulti,
                    canApply,
                    tooltip);
            }
            else
            {
                GUI.enabled = canApply;
                if (GUILayout.Button(
                        new GUIContent("Apply to characters…", tooltip ?? ""),
                        GUILayout.Height(barBtnH),
                        GUILayout.MinWidth(barBtnMinW + 24f)))
                    ApplyPosesToCharactersMulti();
                GUI.enabled = true;
            }
        }
    }
}

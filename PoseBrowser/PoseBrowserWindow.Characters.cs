using System;
using System.Collections.Generic;
using System.Linq;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
        private readonly PoseBrowserCharacterConfig _characterConfig = new PoseBrowserCharacterConfig();
        private bool _showCharacterConfigPane;
        private Rect _characterConfigWindowRect;
        private Vector2 _characterConfigScrollMale;
        private Vector2 _characterConfigScrollFemale;
        private int _selectedMaleSlotIndex = -1;
        private int _selectedFemaleSlotIndex = -1;

        private void DrawCharacterConfigWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label(
                "Priority lists for multi-character pose apply. Top = highest priority.",
                GUILayout.Height(32f));

            if (GUILayout.Button("Load characters from scene", GUILayout.Height(26f)))
            {
                _characterConfig.LoadNewFromScene(_dataService.GetSceneCharacters());
                _selectedMaleSlotIndex = -1;
                _selectedFemaleSlotIndex = -1;
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawCharacterConfigListColumn(
                "Male",
                PoseBrowserCharacterListKind.Male,
                _characterConfig.Male,
                ref _characterConfigScrollMale,
                ref _selectedMaleSlotIndex);
            GUILayout.Space(8f);
            DrawCharacterConfigListColumn(
                "Female",
                PoseBrowserCharacterListKind.Female,
                _characterConfig.Female,
                ref _characterConfigScrollFemale,
                ref _selectedFemaleSlotIndex);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            if (GUILayout.Button("Close", GUILayout.Height(26f)))
                _showCharacterConfigPane = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawCharacterConfigListColumn(
            string title,
            PoseBrowserCharacterListKind kind,
            IReadOnlyList<PoseBrowserCharacterSlot> slots,
            ref Vector2 scroll,
            ref int selectedIndex)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(136f), GUILayout.ExpandHeight(true));
            GUILayout.Label(title, GUILayout.Height(20f));
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool inScene = PoseBrowserCharacterSlot.TryResolveInScene(slot, out _);
                bool rowOn = selectedIndex == i;
                Color prev = GUI.color;
                if (!inScene)
                    GUI.color = new Color(1f, 0.75f, 0.55f, 1f);

                GUILayout.BeginHorizontal();
                string label = $"{i + 1}. {slot.DisplayName}";
                if (GUILayout.Toggle(rowOn, label, GUI.skin.button, GUILayout.Height(22f), GUILayout.ExpandWidth(true)))
                    selectedIndex = i;
                else if (rowOn)
                    selectedIndex = -1;
                GUILayout.EndHorizontal();
                GUI.color = prev;
            }

            GUILayout.EndScrollView();

            GUI.enabled = selectedIndex >= 0 && selectedIndex < slots.Count;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("↑", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                _characterConfig.MoveSlot(kind, selectedIndex, -1);
                selectedIndex = Math.Max(0, selectedIndex - 1);
            }

            if (GUILayout.Button("↓", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                _characterConfig.MoveSlot(kind, selectedIndex, 1);
                selectedIndex = Math.Min(slots.Count - 1, selectedIndex + 1);
            }

            if (GUILayout.Button("⇄", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                _characterConfig.TransferSlot(kind, selectedIndex);
                selectedIndex = -1;
            }

            if (GUILayout.Button("✕", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                _characterConfig.RemoveSlot(kind, selectedIndex);
                selectedIndex = -1;
            }

            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.EndVertical();
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
            ApplyPosesListToSelectedCharacters(GetPosesForMultiCharacterApply());
        }

        private void ApplyGroupMembersToSelectedCharacters(string groupId)
        {
            ApplyPosesListToSelectedCharacters(GetGroupMemberItemsInDisplayOrder(groupId));
        }

        private void ApplyPosesListToSelectedCharacters(IReadOnlyList<PoseGridItem> poses)
        {
            foreach (var pose in poses)
                _tagDb.ApplyToItem(pose);

            var chars = _dataService.GetSelectedCharacters().ToList();
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

            if (_poseSortMode == PoseSortMode.LastUsed)
            {
                ResortPoseItemsInPlace();
                ApplyFilters();
            }

            SandboxServices.Log.LogMessage(
                $"PoseBrowser: Applied {applied} pose(s) to {chars.Count} selected character(s) using priority lists.");
        }

        private void DrawMultiCharacterApplyButton(float barBtnH, float barBtnMinW)
        {
            if (!CanShowMultiCharacterApply()) return;
            GUI.enabled = _dataService.GetSelectedCharacters().Any();
            if (GUILayout.Button("Apply to characters…", GUILayout.Height(barBtnH), GUILayout.MinWidth(barBtnMinW + 24f)))
                ApplyPosesToCharactersMulti();
            GUI.enabled = true;
        }
    }
}

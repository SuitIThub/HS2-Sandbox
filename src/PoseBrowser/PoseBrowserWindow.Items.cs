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
        private const float ItemAssociationPaneDefaultWidthBase = 340f;
        private float ItemAssociationPaneDefaultWidth => PoseBrowserScale.Px(ItemAssociationPaneDefaultWidthBase);
        private const float ItemPaneLiveStateRefreshSeconds = 0.25f;
        private const float ItemPaneIconButtonWidthBase = 22f;
        private float ItemPaneIconButtonWidth => PoseBrowserScale.Px(ItemPaneIconButtonWidthBase);
        private const float ItemPaneCheckboxWidthBase = 20f;
        private float ItemPaneCheckboxWidth => PoseBrowserScale.Px(ItemPaneCheckboxWidthBase);

        private PoseItemDatabase _itemDb = null!;
        private bool _showItemAssociationPane;
        private Rect _itemAssociationWindowRect;
        private Vector2 _itemAssociationScroll;
        private PoseGridItem? _itemAssociationPose;
        private readonly Dictionary<int, bool> _itemAssociationRowChecked = new Dictionary<int, bool>();
        private readonly Dictionary<int, string?> _itemAssociationRowWarnings = new Dictionary<int, string?>();
        private readonly HashSet<string> _itemAssociationCandidateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private PauseCtrl.FileInfo? _itemAssociationCachedPoseFileInfo;
        private bool _itemAssociationCachedPoseFileInfoValid;
        private float _itemPaneNextLiveRefreshTime;
        private readonly List<OCIChar> _itemPaneCachedChars = new List<OCIChar>();
        private readonly List<OCIItem> _itemPaneCachedStudioItems = new List<OCIItem>();
        private bool _itemPaneCachedPoseMismatch;
        private IList<PoseAssociatedItemRecord> _itemPaneCachedRecords = new PoseAssociatedItemRecord[0];

        private bool _itemLoadPosition = true;
        private bool _itemLoadRotation = true;
        private bool _itemLoadScale = true;
        private bool _itemLoadForceFree;

        private int _itemRenameIndex = -1;
        private string _itemRenameText = "";
        private Action? _itemPaneDeferredGuiAction;

        private GUIStyle? _itemPaneWarnLabelStyle;
        private GUIStyle? _itemPaneSectionLabelStyle;
        private GUIStyle? _itemPaneStoredNameButtonStyle;
        private GUIStyle? _itemPaneStoredNameSelectedButtonStyle;
        private GUIStyle? _itemPaneIconButtonStyle;

        private void InitItemPaneStyles()
        {
            if (_itemPaneWarnLabelStyle != null) return;
            _itemPaneWarnLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _itemPaneSectionLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _itemPaneStoredNameButtonStyle = new GUIStyle(GUI.skin.button);
            _itemPaneStoredNameSelectedButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            _itemPaneIconButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(2, 2, 2, 2),
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OpenItemAssociationPane(PoseGridItem pose)
        {
            _itemAssociationPose = pose;
            _showItemAssociationPane = true;
            _itemAssociationScroll = Vector2.zero;
            _itemPaneNextLiveRefreshTime = 0f;
            _itemRenameIndex = -1;
            CachePoseFileInfoForItemPane(pose);
            RefreshItemAssociationRowChecks();
            RefreshItemAssociationLiveState(force: true);
        }

        private void CloseItemAssociationPane()
        {
            _showItemAssociationPane = false;
            _itemAssociationPose = null;
            _itemAssociationRowChecked.Clear();
            _itemAssociationRowWarnings.Clear();
            _itemAssociationCandidateKeys.Clear();
            _itemAssociationCachedPoseFileInfo = null;
            _itemAssociationCachedPoseFileInfoValid = false;
            _itemPaneCachedChars.Clear();
            _itemPaneCachedStudioItems.Clear();
            _itemPaneCachedRecords = new PoseAssociatedItemRecord[0];
            _itemRenameIndex = -1;
            _itemPaneDeferredGuiAction = null;
        }

        private void CachePoseFileInfoForItemPane(PoseGridItem pose)
        {
            _itemAssociationCachedPoseFileInfo = null;
            _itemAssociationCachedPoseFileInfoValid = false;
            if (PoseBrowserItemService.TryLoadPoseFileInfo(pose, out var fromFile))
            {
                _itemAssociationCachedPoseFileInfo = fromFile;
                _itemAssociationCachedPoseFileInfoValid = true;
            }
        }

        private void RefreshItemAssociationRowChecks()
        {
            _itemAssociationRowChecked.Clear();
            _itemAssociationRowWarnings.Clear();
            if (_itemAssociationPose == null) return;
            var items = _itemDb.GetItems(_itemAssociationPose);
            for (int i = 0; i < items.Count; i++)
                _itemAssociationRowChecked[i] = true;
            _itemPaneCachedRecords = items;
        }

        private void RefreshItemAssociationLiveState(bool force)
        {
            if (!force && Time.realtimeSinceStartup < _itemPaneNextLiveRefreshTime)
                return;

            _itemPaneNextLiveRefreshTime = Time.realtimeSinceStartup + ItemPaneLiveStateRefreshSeconds;

            _itemPaneCachedChars.Clear();
            foreach (var c in GetCachedStudioSelectedCharacters())
                _itemPaneCachedChars.Add(c);

            _itemPaneCachedStudioItems.Clear();
            foreach (var i in PoseBrowserItemService.GetSelectedItems())
                _itemPaneCachedStudioItems.Add(i);

            _itemAssociationCandidateKeys.Clear();
            foreach (var item in _itemPaneCachedStudioItems)
            {
                try
                {
                    var info = item.itemInfo;
                    _itemAssociationCandidateKeys.Add(PoseAssociatedItemRecord.FormatCatalogKey(
                        info.group, info.category, info.no));
                }
                catch
                {
                    // ignored
                }
            }

            if (_itemAssociationPose != null)
                _itemPaneCachedRecords = _itemDb.GetItems(_itemAssociationPose);

            _itemPaneCachedPoseMismatch = false;
            if (_itemPaneCachedChars.Count == 1 && _itemAssociationCachedPoseFileInfoValid &&
                _itemAssociationCachedPoseFileInfo != null)
            {
                try
                {
                    var current = new PauseCtrl.FileInfo(_itemPaneCachedChars[0]);
                    _itemPaneCachedPoseMismatch = !PoseBrowserItemService.PoseFileInfoMatches(
                        _itemAssociationCachedPoseFileInfo, current);
                }
                catch
                {
                    _itemPaneCachedPoseMismatch = true;
                }
            }
        }

        private PoseItemLoadOptions BuildItemLoadOptions() => new PoseItemLoadOptions
        {
            LoadPosition = _itemLoadPosition,
            LoadRotation = _itemLoadRotation,
            LoadScale = _itemLoadScale,
            ForceFreePlacement = _itemLoadForceFree
        };

        private void DrawItemAssociationDockedPane()
        {
            string title = _itemAssociationPose != null
                ? $"Pose Browser · Items — {_itemAssociationPose.DisplayName}"
                : "Pose Browser · Items";
            DrawDockedPaneWindow(
                ItemAssociationWindowId,
                ref _itemAssociationWindowRect,
                DrawItemAssociationWindowContent,
                title,
                ItemAssociationPaneDefaultWidth);
        }

        private void RunDeferredItemPaneGuiActions()
        {
            if (_itemPaneDeferredGuiAction == null)
                return;

            Action action = _itemPaneDeferredGuiAction;
            _itemPaneDeferredGuiAction = null;
            action();
        }

        private void ScheduleItemPaneGuiAction(Action action) =>
            _itemPaneDeferredGuiAction = action;

        private void DrawItemAssociationWindowContent(int winId)
        {
            InitItemPaneStyles();
            RunDeferredItemPaneGuiActions();

            if (Event.current.type == EventType.Layout)
                RefreshItemAssociationLiveState(force: false);

            if (_itemAssociationPose == null)
            {
                GUILayout.Label("No pose selected.");
                if (GUILayout.Button("Close"))
                    CloseItemAssociationPane();
                return;
            }

            GUILayout.BeginVertical();
            DrawItemAssociationPoseWarning(_itemPaneCachedPoseMismatch);
            DrawItemAssociationLoadToggles();
            DrawItemAssociationAddSection(_itemPaneCachedChars, _itemPaneCachedStudioItems);

            GUILayout.Space(4f);
            GUILayout.Label("Stored items", _itemPaneSectionLabelStyle);

            _itemAssociationScroll = GUILayout.BeginScrollView(_itemAssociationScroll);
            var records = _itemPaneCachedRecords;
            if (records.Count == 0)
                GUILayout.Label("No items registered for this pose yet.");
            else
            {
                for (int i = 0; i < records.Count; i++)
                    DrawItemAssociationStoredRow(records, i);
            }

            GUILayout.EndScrollView();

            if (records.Count > 0)
            {
                GUILayout.Space(4f);
                DrawItemAssociationBulkLoadButtons(records, _itemPaneCachedChars);
            }

            DrawItemRenamePopup(records);
            GUILayout.EndVertical();
        }

        private void DrawItemAssociationPoseWarning(bool poseMismatch)
        {
            if (!poseMismatch)
                return;

            var prev = GUI.color;
            GUI.color = new Color(1f, 0.92f, 0.2f);
            GUILayout.Label(
                new GUIContent(
                    "⚠ Selected character does not have this pose applied",
                    "Saving is still allowed; layout is relative to the current character pose."),
                _itemPaneWarnLabelStyle);
            GUI.color = prev;
            GUILayout.Space(2f);
        }

        private void DrawItemAssociationLoadToggles()
        {
            GUILayout.Label("Load options", _itemPaneSectionLabelStyle);
            GUILayout.BeginHorizontal();
            _itemLoadPosition = GUILayout.Toggle(_itemLoadPosition, "Position", GUILayout.ExpandWidth(true));
            _itemLoadRotation = GUILayout.Toggle(_itemLoadRotation, "Rotation", GUILayout.ExpandWidth(true));
            _itemLoadScale = GUILayout.Toggle(_itemLoadScale, "Scale", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            _itemLoadForceFree = GUILayout.Toggle(
                _itemLoadForceFree,
                new GUIContent(
                    "Load as free (ignore body-part attach)",
                    "Spawn without workspace tree parenting even when the item was saved attached to a body part."));
            GUILayout.Space(4f);
        }

        private void DrawItemAssociationAddSection(IList<OCIChar> chars, IList<OCIItem> studioItems)
        {
            GUILayout.Label(BuildAddCandidatesLabel(chars, studioItems));
            bool canAdd = _itemAssociationPose != null && chars.Count == 1 && studioItems.Count > 0;
            GUI.enabled = canAdd;
            if (GUILayout.Button("Add selected item(s)", PoseBrowserScale.H(26f)) && canAdd)
            {
                OCIChar anchor = chars[0];
                var itemsCopy = studioItems.ToList();
                ScheduleItemPaneGuiAction(() => AddSelectedWorkspaceItemsToPose(anchor, itemsCopy));
            }
            GUI.enabled = true;
            GUILayout.Space(4f);
        }

        private static string BuildAddCandidatesLabel(IList<OCIChar> chars, IList<OCIItem> studioItems)
        {
            if (chars.Count != 1)
                return "Select exactly one character in Studio to add items.";

            if (studioItems.Count == 0)
                return "Select workspace item(s) in Studio to add.";

            var names = new List<string>(studioItems.Count);
            foreach (var item in studioItems)
                names.Add(PoseBrowserItemService.GetItemDisplayName(item));

            var sb = new StringBuilder();
            sb.Append("Will add: ");
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0)
                    sb.Append(i == names.Count - 1 ? " and " : ", ");
                sb.Append(names[i]);
            }

            return sb.ToString();
        }

        private void DrawItemAssociationStoredRow(IList<PoseAssociatedItemRecord> records, int index)
        {
            var record = records[index];
            bool isCandidate = _itemAssociationCandidateKeys.Contains(record.CatalogKey);

            GUILayout.BeginHorizontal(GUI.skin.box);

            if (!_itemAssociationRowChecked.ContainsKey(index))
                _itemAssociationRowChecked[index] = true;

            _itemAssociationRowChecked[index] = GUILayout.Toggle(
                _itemAssociationRowChecked[index],
                GUIContent.none,
                GUILayout.Width(ItemPaneCheckboxWidth));

            string displayName = StringEx.IsNullOrWhiteSpace(record.DisplayName) ? "Item" : record.DisplayName.Trim();
            var nameStyle = isCandidate
                ? _itemPaneStoredNameSelectedButtonStyle!
                : _itemPaneStoredNameButtonStyle!;
            if (GUILayout.Button(displayName, nameStyle, PoseBrowserScale.MinW(60f), GUILayout.ExpandWidth(true)))
            {
                int loadIndex = index;
                ScheduleItemPaneGuiAction(() =>
                    LoadAssociatedItems(_itemAssociationPose!, new[] { loadIndex }, ignoreCheckboxes: true));
            }

            _itemAssociationRowWarnings.TryGetValue(index, out string? warn);
            bool showWarn = !string.IsNullOrEmpty(warn);
            {
                var prev = GUI.color;
                if (showWarn)
                    GUI.color = new Color(1f, 0.55f, 0.1f);
                GUILayout.Label(
                    showWarn ? new GUIContent("⚠", warn) : GUIContent.none,
                    PoseBrowserScale.W(16f));
                GUI.color = prev;
            }

            if (GUILayout.Button(
                    new GUIContent("✎", "Rename item label"),
                    _itemPaneIconButtonStyle!,
                    GUILayout.Width(ItemPaneIconButtonWidth),
                    PoseBrowserScale.H(20f)))
            {
                int renameIndex = index;
                string renameText = displayName;
                ScheduleItemPaneGuiAction(() =>
                {
                    _itemRenameIndex = renameIndex;
                    _itemRenameText = renameText;
                });
            }

            if (GUILayout.Button(
                    new GUIContent("X", "Remove from pose"),
                    _itemPaneIconButtonStyle!,
                    GUILayout.Width(ItemPaneIconButtonWidth),
                    PoseBrowserScale.H(20f)))
            {
                int removeIndex = index;
                ScheduleItemPaneGuiAction(() => RemoveStoredItemAt(removeIndex));
            }

            GUILayout.EndHorizontal();
        }

        private void DrawItemRenamePopup(IList<PoseAssociatedItemRecord> records)
        {
            if (_itemRenameIndex < 0 || _itemAssociationPose == null || _itemRenameIndex >= records.Count)
                return;

            GUILayout.Space(6f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Rename item");
            _itemRenameText = GUILayout.TextField(_itemRenameText);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK", PoseBrowserScale.H(22f)))
                ScheduleItemPaneGuiAction(CommitItemRename);

            if (GUILayout.Button("Cancel", PoseBrowserScale.H(22f)))
                ScheduleItemPaneGuiAction(() => _itemRenameIndex = -1);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void AddSelectedWorkspaceItemsToPose(OCIChar anchor, IList<OCIItem> studioItems)
        {
            if (_itemAssociationPose == null) return;

            var toAdd = new List<PoseAssociatedItemRecord>();
            foreach (var item in studioItems)
            {
                var record = PoseBrowserItemService.TryCreateRecordFromWorkspaceItem(item, anchor, out string? err);
                if (record == null)
                {
                    if (!string.IsNullOrEmpty(err))
                        SandboxServices.Log.LogMessage($"PoseBrowser: {err}");
                    continue;
                }

                toAdd.Add(record);
            }

            if (toAdd.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: No items could be saved.");
                return;
            }

            _itemDb.AddItems(_itemAssociationPose, toAdd);
            RefreshItemAssociationRowChecks();
            RefreshItemAssociationLiveState(force: true);
            SandboxServices.Log.LogMessage(
                $"PoseBrowser: Added {toAdd.Count} item(s) to pose \"{_itemAssociationPose.DisplayName}\".");
        }

        private void DrawItemAssociationBulkLoadButtons(
            IList<PoseAssociatedItemRecord> records,
            IList<OCIChar> chars)
        {
            bool canLoad = chars.Count == 1;
            GUI.enabled = canLoad;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Selection", PoseBrowserScale.H(26f)) && canLoad)
            {
                var indices = new List<int>();
                for (int i = 0; i < records.Count; i++)
                {
                    if (_itemAssociationRowChecked.TryGetValue(i, out bool on) && on)
                        indices.Add(i);
                }

                var copy = indices;
                ScheduleItemPaneGuiAction(() =>
                    LoadAssociatedItems(_itemAssociationPose!, copy, ignoreCheckboxes: false));
            }

            if (GUILayout.Button("Load All", PoseBrowserScale.H(26f)) && canLoad)
            {
                int count = records.Count;
                ScheduleItemPaneGuiAction(() =>
                    LoadAssociatedItems(
                        _itemAssociationPose!,
                        Enumerable.Range(0, count).ToList(),
                        ignoreCheckboxes: true));
            }

            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Label(
                canLoad
                    ? GUIContent.none
                    : new GUIContent("Select exactly one character in Studio to load items."),
                GUILayout.MinHeight(canLoad ? 0f : 20f));
        }

        private void CommitItemRename()
        {
            if (_itemRenameIndex < 0 || _itemAssociationPose == null)
                return;

            if (_itemDb.TrySetItemDisplayNameAt(_itemAssociationPose, _itemRenameIndex, _itemRenameText))
                RefreshItemAssociationLiveState(force: true);
            _itemRenameIndex = -1;
        }

        private void RemoveStoredItemAt(int index)
        {
            if (_itemAssociationPose == null)
                return;

            _itemDb.RemoveItemAt(_itemAssociationPose, index);
            if (_itemRenameIndex == index)
                _itemRenameIndex = -1;
            else if (_itemRenameIndex > index)
                _itemRenameIndex--;
            RefreshItemAssociationRowChecks();
            RefreshItemAssociationLiveState(force: true);
        }

        private void LoadAssociatedItems(
            PoseGridItem pose,
            IEnumerable<int> indices,
            bool ignoreCheckboxes)
        {
            if (_itemPaneCachedChars.Count != 1)
                RefreshItemAssociationLiveState(force: true);
            if (_itemPaneCachedChars.Count != 1)
                return;

            OCIChar anchor = _itemPaneCachedChars[0];
            var records = _itemDb.GetItems(pose);
            var options = BuildItemLoadOptions();
            int loaded = 0;

            foreach (int index in indices)
            {
                if (index < 0 || index >= records.Count)
                    continue;
                if (!ignoreCheckboxes &&
                    _itemAssociationRowChecked.TryGetValue(index, out bool on) && !on)
                    continue;

                var record = records[index];
                if (PoseBrowserItemService.TryApplyRecordToCharacter(
                        record,
                        anchor,
                        adjustForBodyHeight: true,
                        adjustForObjectScale: true,
                        options,
                        out string? warn))
                {
                    loaded++;
                    _itemAssociationRowWarnings[index] = warn;
                }
            }

            if (loaded > 0)
            {
                RefreshItemAssociationLiveState(force: true);
                SandboxServices.Log.LogMessage($"PoseBrowser: Loaded {loaded} item(s) for \"{pose.DisplayName}\".");
            }
        }
    }
}

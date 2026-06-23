using System;
using System.Collections.Generic;
using System.Linq;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
        private const int HistoryWindowId = SandboxImguiWindowIds.PoseBrowser.History;
        private const float HistoryPaneDefaultWidthBase = 400f;
        private float HistoryPaneDefaultWidth => PoseBrowserScale.Px(HistoryPaneDefaultWidthBase);

        private readonly PoseBrowserHistory _poseHistory = new PoseBrowserHistory();
        private bool _showHistoryPane;
        private Rect _historyWindowRect;
        private Vector2 _historyScroll;
        private bool _historyRestorePose = true;
        private bool _historyRestorePosition = true;
        private bool _historyRestoreRotation = true;

        private int HistoryMaxEntries =>
            Mathf.Max(1, PoseBrowserConfig.HistoryMaxEntries?.Value ?? PoseBrowserHistory.DefaultMaxEntriesPerCharacter);

        private void InitPoseHistory()
        {
            _poseHistory.LoadFromDisk();
            _poseHistory.TrimAllTimelines(HistoryMaxEntries);
            _historyWindowRect = new Rect(windowRect.xMax + 6f, windowRect.y, HistoryPaneDefaultWidth, windowRect.height);
        }

        private void SavePoseHistory() => _poseHistory.SaveToDiskIfDirty();

        private void DrawHistoryUndoRedoButtons(float height = 24f, float buttonWidth = 48f)
        {
            // Draw path: use the cached Studio selection (refreshed ~5×/s) to avoid enumerating
            // Studio and allocating a list every frame. The undo/redo actions still read it live.
            var selected = GetCachedStudioSelectedCharacters();
            GUI.enabled = _poseHistory.CanUndo(selected);
            if (GUILayout.Button(
                    new GUIContent("Undo", "Undo last pose change for Studio-selected characters"),
                    PoseBrowserScale.W(buttonWidth),
                    PoseBrowserScale.H(height)))
                PerformPoseHistoryUndo();
            GUI.enabled = _poseHistory.CanRedo(selected);
            if (GUILayout.Button(
                    new GUIContent("Redo", "Redo pose change for Studio-selected characters"),
                    PoseBrowserScale.W(buttonWidth),
                    PoseBrowserScale.H(height)))
                PerformPoseHistoryRedo();
            GUI.enabled = true;
        }

        private void DrawHistoryPaneToggleButton(float height = 24f, float width = 72f)
        {
            if (GUILayout.Button(
                    _showHistoryPane ? "History ▶" : "History",
                    PoseBrowserScale.W(width),
                    PoseBrowserScale.H(height)))
                _showHistoryPane = !_showHistoryPane;
        }

        private void DrawHistoryTopBarButtons()
        {
            DrawHistoryUndoRedoButtons();
            DrawHistoryPaneToggleButton();
            DrawStashTopBarButton();
        }

        private void DrawHistoryCompactListHeaderButtons()
        {
            DrawHistoryUndoRedoButtons(24f, 44f);
            DrawHistoryPaneToggleButton(24f, 64f);
        }

        private void DrawHistoryMiniHeaderButtons() => DrawHistoryUndoRedoButtons(24f, 40f);

        private void PerformPoseHistoryUndo()
        {
            var selected = _dataService.GetSelectedCharacters().ToList();
            if (selected.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Select one or more characters in Studio to undo.");
                return;
            }

            if (!_poseHistory.CanUndo(selected))
                return;

            _poseHistory.Undo(_dataService, selected, HistoryMaxEntries);
            _poseHistory.SaveToDiskIfDirty();
#if HS2 || AI
            ResetHeelzOverridesForCharacters(selected);
#endif
        }

        private void PerformPoseHistoryRedo()
        {
            var selected = _dataService.GetSelectedCharacters().ToList();
            if (selected.Count == 0)
            {
                SandboxServices.Log.LogMessage("PoseBrowser: Select one or more characters in Studio to redo.");
                return;
            }

            if (!_poseHistory.CanRedo(selected))
                return;

            _poseHistory.Redo(_dataService, selected, HistoryMaxEntries);
            _poseHistory.SaveToDiskIfDirty();
#if HS2 || AI
            ResetHeelzOverridesForCharacters(selected);
#endif
        }

#if HS2 || AI
        private static void ResetHeelzOverridesForCharacters(IEnumerable<Studio.OCIChar> characters)
        {
            foreach (var oci in characters)
            {
                if (oci?.charInfo != null)
                    HeelzControlService.SetOverride(oci.charInfo, HeelzOverride.Default);
            }
        }
#endif

        private void RecordPoseHistoryBeforeSingleApply(PoseGridItem item)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed)
                return;

            try
            {
                var chars = _dataService.GetSelectedCharacters().ToList();
                if (chars.Count == 0)
                    return;

                _poseHistory.RecordBeforePoseApply(chars, item.DisplayName);
                _poseHistory.TrimAllTimelines(HistoryMaxEntries);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: History capture skipped before apply: {ex.Message}");
            }
        }

        private void RecordPoseHistoryAfterSingleApply(PoseGridItem item)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed)
                return;

            try
            {
                var chars = _dataService.GetSelectedCharacters().ToList();
                if (chars.Count == 0)
                    return;

                _poseHistory.RecordAfterPoseApply(chars, item.DisplayName);
                _poseHistory.TrimAllTimelines(HistoryMaxEntries);
                _poseHistory.SaveToDiskIfDirty();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: History capture skipped after apply: {ex.Message}");
            }
        }

        private void RecordPoseHistoryBeforeMultiApply(IList<PoseGridItem> poses, IList<OCIChar> chars)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed || poses.Count == 0 || chars.Count == 0)
                return;

            try
            {
                var plan = PoseBrowserCharacterApply.BuildFullPlannedPoseAssignmentPlan(_characterConfig, poses, chars);
                var assignments = new List<OciLabelPair>();
                foreach (PoseCharListPair planEntry in plan)
                {
                    foreach (var c in planEntry.Characters)
                        assignments.Add(new OciLabelPair(c, planEntry.Pose.DisplayName));
                }

                if (assignments.Count == 0)
                    return;

                _poseHistory.RecordBeforePoseApplyPlan(assignments);
                _poseHistory.TrimAllTimelines(HistoryMaxEntries);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: History capture skipped before multi-apply: {ex.Message}");
            }
        }

        private void RecordPoseHistoryAfterMultiApply(IList<PoseGridItem> poses, IList<OCIChar> chars)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed || poses.Count == 0 || chars.Count == 0)
                return;

            try
            {
                var plan = PoseBrowserCharacterApply.BuildFullPlannedPoseAssignmentPlan(_characterConfig, poses, chars);
                var assignments = new List<OciLabelPair>();
                foreach (PoseCharListPair planEntry in plan)
                {
                    foreach (var c in planEntry.Characters)
                        assignments.Add(new OciLabelPair(c, planEntry.Pose.DisplayName));
                }

                if (assignments.Count == 0)
                    return;

                _poseHistory.RecordAfterPoseApplyPlan(assignments);
                _poseHistory.TrimAllTimelines(HistoryMaxEntries);
                _poseHistory.SaveToDiskIfDirty();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: History capture skipped after multi-apply: {ex.Message}");
            }
        }

        private void DrawHistoryWindowContent(int id)
        {
            var selected = GetCachedStudioSelectedCharacters();
            GUILayout.Label(
                "History for Studio-selected characters. Entries store absolute pose, position, and rotation.",
                GUI.skin.label);

            GUILayout.BeginHorizontal();
            GUI.enabled = _poseHistory.CanUndo(selected);
            if (GUILayout.Button("Undo", PoseBrowserScale.W(64f), PoseBrowserScale.H(24f)))
                PerformPoseHistoryUndo();
            GUI.enabled = _poseHistory.CanRedo(selected);
            if (GUILayout.Button("Redo", PoseBrowserScale.W(64f), PoseBrowserScale.H(24f)))
                PerformPoseHistoryRedo();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("When applying a history entry:", GUI.skin.label);
            _historyRestorePose = GUILayout.Toggle(_historyRestorePose, "Pose data");
            _historyRestorePosition = GUILayout.Toggle(_historyRestorePosition, "Position");
            _historyRestoreRotation = GUILayout.Toggle(_historyRestoreRotation, "Rotation");

            if (selected.Count == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Select one or more characters in Studio to view their history.");
            }
            else
            {
                DrawHistoryTimelineForSelected(selected);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close panel", PoseBrowserScale.H(26f)))
                _showHistoryPane = false;

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawHistoryTimelineForSelected(IList<OCIChar> selected)
        {
            var timelines = _poseHistory.GetTimelinesForSelected(selected);
            _historyScroll = GUILayout.BeginScrollView(_historyScroll, GUILayout.ExpandHeight(true));

            if (timelines.Count == 0)
            {
                GUILayout.Label("No pose history yet for the selected character(s).");
            }
            else
            {
                foreach (var tl in timelines)
                {
                    GUILayout.Label($"<b>{tl.DisplayName}</b>", GetHistoryRichLabelStyle());
                    if (tl.Entries.Count == 0)
                    {
                        GUILayout.Label("  (no entries)");
                        GUILayout.Space(6f);
                        continue;
                    }

                    if (!TryResolveTimelineCharacter(tl, selected, out var oci))
                    {
                        GUILayout.Label("  (character not in current selection)");
                        GUILayout.Space(6f);
                        continue;
                    }

                    // Entries are appended in chronological order and only pruned at the ends, so
                    // iterating backwards yields newest-first without sorting/allocating every frame.
                    // `index` stays the original list index that JumpToEntry expects.
                    for (int index = tl.Entries.Count - 1; index >= 0; index--)
                    {
                        var entry = tl.Entries[index];
                        bool isCurrent = index == tl.CursorIndex;
                        GUIContent content = isCurrent
                            ? new GUIContent("▶ " + entry.GetDisplayBody(), HistoryEntryApplyTooltip)
                            : entry.GetDisplayContent(HistoryEntryApplyTooltip);
                        GUILayout.BeginVertical(isCurrent ? GetHistoryEntryCurrentBoxStyle() : GetHistoryEntryBoxStyle());
                        if (GUILayout.Button(
                                content,
                                GetHistoryEntryButtonStyle(),
                                GUILayout.ExpandWidth(true)))
                        {
                            _poseHistory.JumpToEntry(
                                _dataService,
                                oci,
                                index,
                                _historyRestorePose,
                                _historyRestorePosition,
                                _historyRestoreRotation,
                                HistoryMaxEntries);
                            _poseHistory.SaveToDiskIfDirty();
                        }

                        GUILayout.EndVertical();
                    }

                    GUILayout.Space(8f);
                }
            }

            GUILayout.EndScrollView();
        }

        private static bool TryResolveTimelineCharacter(
            PoseBrowserCharacterTimeline tl,
            IList<OCIChar> selected,
            out OCIChar oci)
        {
            oci = null;
            foreach (var c in selected)
            {
                if (PoseDataService.TryGetDicKey(c, out int key) && key == tl.DicKey)
                {
                    oci = c;
                    return true;
                }
            }

            return false;
        }

        private const string HistoryEntryApplyTooltip = "Apply this snapshot to the character (uses checkboxes above).";

        private GUIStyle? _historyRichLabelStyle;
        private GUIStyle? _historyEntryBoxStyle;
        private GUIStyle? _historyEntryCurrentBoxStyle;
        private GUIStyle? _historyEntryButtonStyle;

        private GUIStyle GetHistoryRichLabelStyle()
        {
            if (_historyRichLabelStyle == null)
            {
                _historyRichLabelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold };
            }

            return _historyRichLabelStyle;
        }

        private GUIStyle GetHistoryEntryBoxStyle()
        {
            if (_historyEntryBoxStyle == null)
                _historyEntryBoxStyle = CreateHistoryEntryBoxStyle(new Color(0.11f, 0.11f, 0.13f, 1f));
            return _historyEntryBoxStyle;
        }

        private GUIStyle GetHistoryEntryCurrentBoxStyle()
        {
            if (_historyEntryCurrentBoxStyle == null)
                _historyEntryCurrentBoxStyle = CreateHistoryEntryBoxStyle(new Color(0.14f, 0.17f, 0.22f, 1f));
            return _historyEntryCurrentBoxStyle;
        }

        private static GUIStyle CreateHistoryEntryBoxStyle(Color background)
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 4, 4),
                wordWrap = true
            };
            style.normal.background = MakeTex(8, 8, background);
            style.border = GUI.skin.box.border;
            return style;
        }

        private GUIStyle GetHistoryEntryButtonStyle()
        {
            if (_historyEntryButtonStyle != null)
                return _historyEntryButtonStyle;

            _historyEntryButtonStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(2, 2, 2, 2),
                stretchWidth = true,
                richText = false
            };
            _historyEntryButtonStyle.normal.textColor = new Color(0.92f, 0.92f, 0.94f, 1f);
            _historyEntryButtonStyle.hover.textColor = new Color(0.75f, 0.88f, 1f, 1f);
            _historyEntryButtonStyle.active.textColor = new Color(0.65f, 0.82f, 1f, 1f);
            _historyEntryButtonStyle.hover.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.06f));
            _historyEntryButtonStyle.active.background = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.1f));
            return _historyEntryButtonStyle;
        }
    }
}

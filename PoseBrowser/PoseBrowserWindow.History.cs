using System.Collections.Generic;
using System.Linq;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public partial class PoseBrowserWindow
    {
        private const int HistoryWindowId = 2027;
        private const float HistoryPaneDefaultWidth = 400f;

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
            var selected = _dataService.GetSelectedCharacters().ToList();
            GUI.enabled = _poseHistory.CanUndo(selected);
            if (GUILayout.Button(
                    new GUIContent("Undo", "Undo last pose change for Studio-selected characters"),
                    GUILayout.Width(buttonWidth),
                    GUILayout.Height(height)))
                PerformPoseHistoryUndo();
            GUI.enabled = _poseHistory.CanRedo(selected);
            if (GUILayout.Button(
                    new GUIContent("Redo", "Redo pose change for Studio-selected characters"),
                    GUILayout.Width(buttonWidth),
                    GUILayout.Height(height)))
                PerformPoseHistoryRedo();
            GUI.enabled = true;
        }

        private void DrawHistoryPaneToggleButton(float height = 24f, float width = 72f)
        {
            if (GUILayout.Button(
                    _showHistoryPane ? "History ▶" : "History",
                    GUILayout.Width(width),
                    GUILayout.Height(height)))
                _showHistoryPane = !_showHistoryPane;
        }

        private void DrawHistoryTopBarButtons()
        {
            DrawHistoryUndoRedoButtons();
            DrawHistoryPaneToggleButton();
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
        }

        private void RecordPoseHistoryBeforeSingleApply(PoseGridItem item)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed)
                return;

            var chars = _dataService.GetSelectedCharacters().ToList();
            if (chars.Count == 0)
                return;

            _poseHistory.RecordBeforePoseApply(chars, item.DisplayName);
            _poseHistory.TrimAllTimelines(HistoryMaxEntries);
        }

        private void RecordPoseHistoryAfterSingleApply(PoseGridItem item)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed)
                return;

            var chars = _dataService.GetSelectedCharacters().ToList();
            if (chars.Count == 0)
                return;

            _poseHistory.RecordAfterPoseApply(chars, item.DisplayName);
            _poseHistory.TrimAllTimelines(HistoryMaxEntries);
            _poseHistory.SaveToDiskIfDirty();
        }

        private void RecordPoseHistoryBeforeMultiApply(IReadOnlyList<PoseGridItem> poses, IReadOnlyList<OCIChar> chars)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed || poses.Count == 0 || chars.Count == 0)
                return;

            var plan = PoseBrowserCharacterApply.BuildFullPlannedPoseAssignmentPlan(_characterConfig, poses, chars);
            var assignments = new List<(OCIChar character, string toPoseLabel)>();
            foreach (var (pose, characters) in plan)
            {
                foreach (var c in characters)
                    assignments.Add((c, pose.DisplayName));
            }

            if (assignments.Count == 0)
                return;

            _poseHistory.RecordBeforePoseApplyPlan(assignments);
            _poseHistory.TrimAllTimelines(HistoryMaxEntries);
        }

        private void RecordPoseHistoryAfterMultiApply(IReadOnlyList<PoseGridItem> poses, IReadOnlyList<OCIChar> chars)
        {
            if (_thumbCapture.IsActive || _poseHistory.IsSuppressed || poses.Count == 0 || chars.Count == 0)
                return;

            var plan = PoseBrowserCharacterApply.BuildFullPlannedPoseAssignmentPlan(_characterConfig, poses, chars);
            var assignments = new List<(OCIChar character, string appliedPoseLabel)>();
            foreach (var (pose, characters) in plan)
            {
                foreach (var c in characters)
                    assignments.Add((c, pose.DisplayName));
            }

            if (assignments.Count == 0)
                return;

            _poseHistory.RecordAfterPoseApplyPlan(assignments);
            _poseHistory.TrimAllTimelines(HistoryMaxEntries);
            _poseHistory.SaveToDiskIfDirty();
        }

        private void DrawHistoryWindowContent(int id)
        {
            var selected = _dataService.GetSelectedCharacters().ToList();
            GUILayout.Label(
                "History for Studio-selected characters. Entries store absolute pose, position, and rotation.",
                GUI.skin.label);

            GUILayout.BeginHorizontal();
            GUI.enabled = _poseHistory.CanUndo(selected);
            if (GUILayout.Button("Undo", GUILayout.Width(64f), GUILayout.Height(24f)))
                PerformPoseHistoryUndo();
            GUI.enabled = _poseHistory.CanRedo(selected);
            if (GUILayout.Button("Redo", GUILayout.Width(64f), GUILayout.Height(24f)))
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
            if (GUILayout.Button("Close panel", GUILayout.Height(26f)))
                _showHistoryPane = false;

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void DrawHistoryTimelineForSelected(List<OCIChar> selected)
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

                    var sorted = tl.Entries
                        .Select((entry, index) => (entry, index))
                        .OrderByDescending(x => x.entry.UtcTicks)
                        .ToList();

                    if (!TryResolveTimelineCharacter(tl, selected, out var oci))
                    {
                        GUILayout.Label("  (character not in current selection)");
                        GUILayout.Space(6f);
                        continue;
                    }

                    foreach (var (entry, index) in sorted)
                    {
                        bool isCurrent = index == tl.CursorIndex;
                        string prefix = isCurrent ? "▶ " : "";
                        string posText = entry.Snapshot.HasPosition
                            ? $"\npos ({entry.Snapshot.Position.x:F2}, {entry.Snapshot.Position.y:F2}, {entry.Snapshot.Position.z:F2})"
                            : "";
                        string rotText = entry.Snapshot.HasRotation
                            ? $"\nrot ({entry.Snapshot.Rotation.eulerAngles.x:F0}°, {entry.Snapshot.Rotation.eulerAngles.y:F0}°, {entry.Snapshot.Rotation.eulerAngles.z:F0}°)"
                            : "";

                        string line = $"{prefix}{entry.FormatTimestampLocal()}  {entry.SummaryLine}{posText}{rotText}";
                        GUILayout.BeginVertical(isCurrent ? GetHistoryEntryCurrentBoxStyle() : GetHistoryEntryBoxStyle());
                        if (GUILayout.Button(
                                new GUIContent(line, "Apply this snapshot to the character (uses checkboxes above)."),
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
            List<OCIChar> selected,
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

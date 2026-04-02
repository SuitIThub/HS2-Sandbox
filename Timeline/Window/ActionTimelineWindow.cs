using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using KKAPI.Utilities;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class ActionTimeline : SubWindow, IOverlayDrawable
    {
        private readonly List<TimelineCommand> _commands = new();
        // View stack for subtimeline navigation — _activeCommands is what the UI shows
        private List<TimelineCommand> _activeCommands = null!; // bound in Awake to root list; sub-view replaces with SubCommands
        private readonly Stack<(List<TimelineCommand> cmds, string title, SubTimelineCommand? owningSub)> _viewStack = new();
        /// <summary>Subtimeline row whose inner list is currently shown (null at root). Synced when opening/closing sub views.</summary>
        private SubTimelineCommand? _currentSubTimelineOwner;
        /// <summary>Definition id → marked as reusable template (name-based instance linking).</summary>
        private readonly Dictionary<string, bool> _subTimelineTemplateFlags = new Dictionary<string, bool>(StringComparer.Ordinal);
        private string _activeTitle = "";
        /// <summary>Queued from IMGUI during row draw — applied at end of DrawWindowContent so _activeCommands/_frameValidationErrors are not swapped mid-loop.</summary>
        private SubTimelineCommand? _pendingOpenSubTimeline;
        private bool _pendingCloseSubTimeline;
        // Running stack: each level pushed during RunCommandList (cmds=list, idx=current index in that list).
        // Uses a class so the index can be mutated in-place without popping/repushing.
        private sealed class RunFrame { public List<TimelineCommand> Cmds = null!; public int Idx; }
        private readonly List<RunFrame> _runningStack = new();
        private Vector2 _scrollPosition;
        private bool _isRunning;
        private bool _stopRequested;
        private bool _isPaused;
        private TimelineContext? _runContext;
        private bool _showMousePositions;
        private bool _showVariablesWindow;
        private bool _autoscrollDuringRun = true;
        private Rect _variablesWindowRect;
        private const int VariablesWindowID = 2004;
        /// <summary>Persistent variables when timeline is not running. Seeded into run when timeline starts.</summary>
        private readonly TimelineVariableStore _designTimeVariables = new TimelineVariableStore();
        private int _newVarType; // 0=string, 1=int, 2=bool, 3=list, 4=dict
        private string _newVarName = "";
        private string _newVarValue = "";
        private bool _newVarBool;
        private readonly List<string> _newVarList = new List<string>();
        private readonly Dictionary<string, string> _newVarDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _listEditorOpen;
        private string _listEditorText = "";
        private Action<string[]>? _listEditorOnApply;
        private Rect _listEditorWindowRect;
        private const int ListEditorWindowID = 2005;
        private bool _dictEditorOpen;
        private string _dictEditorText = "";
        private Action<Dictionary<string, string>>? _dictEditorOnApply;
        private Rect _dictEditorWindowRect;
        private const int DictEditorWindowID = 2007;
        // ── Persistent variables ─────────────────────────────────────────────
        private string _persistVarsPath = "";
        private readonly TimelineVariableStore _persistentVariables = new TimelineVariableStore();
        private bool _showPersistentVarsWindow;
        private Rect _persistentVarsWindowRect;
        private Vector2 _persistentVarsScrollPosition;
        private const int PersistentVarsWindowID = 2008;
        private int _newPVarType; // 0=string, 1=int, 2=bool, 3=list, 4=dict
        private string _newPVarName = "";
        private string _newPVarValue = "";
        private bool _newPVarBool;
        private readonly List<string> _newPVarList = new List<string>();
        private readonly Dictionary<string, string> _newPVarDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _persistentVarsDirty;
        private float _persistentVarsDirtyTime;
        private const float PersistentVarsSaveDelay = 3f;
        private TimelineCommand? _recordingMouseFor;
        private CopyScriptApiClient? _apiClient;
        private float _timelineStartTime; // realtimeSinceStartup when timeline started
        private float _totalPausedDuration; // accumulated seconds paused this run
        private float _pauseStartTime; // realtimeSinceStartup when current pause started (0 if not paused)
        private float _lastRunElapsedSeconds = -1f; // last run duration to show after stop (-1 = none)
        private int _startFromIndex; // when starting, begin at this command index
        private static readonly string[] CategoryNames = { "CopyScript Controls", "CopyScript Checks", "CopyScript Config", "Input", "VNGE", "Studio", "Screenshot", "Simple Variables", "Advanced Variables", "Nav", "Misc", "Video", "FashionLine" };
        private int _selectedCategory;
        private bool _categoryWindowVisible;
        private Rect _categoryWindowRect;
        private const int CategoryWindowID = 2006;

        /// <summary>Unique color per command type; same category = similar hue.</summary>
        private static readonly Dictionary<string, Color> CommandColors = new()
        {
            // CopyScript Controls (green family)
            ["start_tracking"] = new Color(0.25f, 0.75f, 0.35f),
            ["stop_tracking"] = new Color(0.2f, 0.65f, 0.4f),
            ["copy_rename"] = new Color(0.35f, 0.85f, 0.4f),
            ["clear_tracked"] = new Color(0.3f, 0.6f, 0.45f),
            // CopyScript Checks (yellow/amber)
            ["wait_screenshot"] = new Color(0.95f, 0.75f, 0.25f),
            ["wait_empty_screenshots"] = new Color(0.9f, 0.7f, 0.35f),
            ["resolve_on_issue"] = new Color(0.92f, 0.8f, 0.3f),
            ["resolve_on_count"] = new Color(0.88f, 0.65f, 0.4f),
            // CopyScript Config (blue family)
            ["set_source_path"] = new Color(0.35f, 0.55f, 0.95f),
            ["set_destination_path"] = new Color(0.4f, 0.6f, 0.9f),
            ["set_name_pattern"] = new Color(0.3f, 0.5f, 1f),
            ["set_rule_counter"] = new Color(0.45f, 0.65f, 0.85f),
            ["set_rule_list"] = new Color(0.5f, 0.7f, 0.9f),
            ["set_rule_batch"] = new Color(0.4f, 0.58f, 0.92f),
            // Input (red/orange)
            ["simulate_key"] = new Color(0.95f, 0.4f, 0.35f),
            ["simulate_mouse"] = new Color(0.9f, 0.35f, 0.4f),
            ["move_mouse"] = new Color(0.92f, 0.45f, 0.38f),
            ["scroll"] = new Color(0.88f, 0.5f, 0.42f),
            ["confirm"] = new Color(0.93f, 0.38f, 0.32f),
            // VNGE (purple/magenta)
            ["vnge_scene_next"] = new Color(0.75f, 0.4f, 0.9f),
            ["vnge_scene_prev"] = new Color(0.7f, 0.35f, 0.85f),
            ["vnge_next_scene"] = new Color(0.8f, 0.45f, 0.88f),
            ["vnge_prev_scene"] = new Color(0.72f, 0.38f, 0.82f),
            ["vnge_load_scene"] = new Color(0.76f, 0.42f, 0.9f),
            // Studio (teal/cyan)
            ["pose_library"] = new Color(0.35f, 0.8f, 0.75f),
            ["clothing_state"] = new Color(0.4f, 0.75f, 0.7f),
            ["accessory_state"] = new Color(0.45f, 0.78f, 0.72f),
            ["screenshot"] = new Color(0.42f, 0.7f, 0.72f),
            ["screenshot_alpha"] = new Color(0.4f, 0.68f, 0.74f),
            ["screenshot_resolution"] = new Color(0.4f, 0.68f, 0.74f),
            ["screenshot_save_path"] = new Color(0.4f, 0.68f, 0.74f),
            ["screenshot_alt_path_var"] = new Color(0.4f, 0.68f, 0.74f),
            ["set_camera_by_name"] = new Color(0.3f, 0.88f, 0.9f),
            ["select_object_by_name"] = new Color(0.32f, 0.84f, 0.88f),
            // FashionLine (mint/spring green)
            ["outfit_rotate"] = new Color(0.3f, 0.9f, 0.55f),
            ["outfit_by_name"] = new Color(0.28f, 0.86f, 0.52f),
            ["get_fashion"] = new Color(0.25f, 0.92f, 0.6f),
            // Variables (slate / blue-gray)
            ["set"] = new Color(0.5f, 0.6f, 0.85f),
            ["set_string"] = new Color(0.5f, 0.6f, 0.85f),
            ["str_replace"] = new Color(0.52f, 0.63f, 0.87f),
            ["set_integer"] = new Color(0.45f, 0.58f, 0.88f),
            ["get"] = new Color(0.42f, 0.55f, 0.8f),
            ["set_list"] = new Color(0.48f, 0.58f, 0.82f),
            ["calc"] = new Color(0.55f, 0.62f, 0.9f),
            ["if"] = new Color(0.52f, 0.58f, 0.88f),
            ["list"] = new Color(0.48f, 0.6f, 0.86f),
            ["dict_set"] = new Color(0.42f, 0.55f, 0.78f),
            ["dict_get"] = new Color(0.38f, 0.52f, 0.75f),
            ["list_apply_dict"] = new Color(0.44f, 0.57f, 0.80f),
            ["list_insert"] = new Color(0.46f, 0.62f, 0.84f),
            ["list_remove"] = new Color(0.50f, 0.58f, 0.80f),
            // Misc (warm orange/brown)
            ["checkpoint"] = new Color(0.85f, 0.55f, 0.3f),
            ["jump"] = new Color(0.82f, 0.5f, 0.35f),
            ["loop"] = new Color(0.88f, 0.6f, 0.38f),
            ["pause"] = new Color(0.9f, 0.58f, 0.32f),
            ["sound"] = new Color(0.86f, 0.52f, 0.4f),
            ["label"] = Color.black,
            ["sub_timeline"] = new Color(0.25f, 0.38f, 0.65f),
            ["sub_timeline_param"] = new Color(0.22f, 0.42f, 0.68f),
            ["return"] = new Color(0.75f, 0.35f, 0.25f),
            // Video (cinematic red/burgundy)
            ["video_record"] = new Color(0.8f, 0.2f, 0.3f),
        };

        private static Color GetCommandColor(string typeId)
        {
            return CommandColors.TryGetValue(typeId, out var c) ? c : new Color(0.5f, 0.5f, 0.55f);
        }

        /// <summary>Representative typeId per category for category color (same order as CategoryNames).</summary>
        private static readonly string[] CategoryRepresentativeTypeIds = { "start_tracking", "wait_screenshot", "set_source_path", "simulate_key", "vnge_scene_next", "pose_library", "screenshot", "set", "set_list", "checkpoint", "pause", "video_record", "outfit_rotate" };

        private static Color GetCategoryColor(int categoryIndex)
        {
            if (categoryIndex >= 0 && categoryIndex < CategoryRepresentativeTypeIds.Length)
                return GetCommandColor(CategoryRepresentativeTypeIds[categoryIndex]);
            return new Color(0.5f, 0.5f, 0.55f);
        }

        private void DrawAddButton(string label, string typeId, float width, float height)
        {
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = GetCommandColor(typeId);
            if (GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(height)))
                AddCommand(typeId);
            GUI.backgroundColor = prevBg;
        }

        private static readonly Color[] CrossColors =
        [
            new(1f, 0.3f, 0.3f),
            new(0.3f, 0.8f, 0.3f),
            new(0.3f, 0.5f, 1f),
            new(1f, 0.85f, 0.2f),
            new(0.2f, 0.9f, 0.9f),
            new(1f, 0.4f, 0.8f),
            new(1f, 0.6f, 0.2f),
            new(0.7f, 0.4f, 1f),
            new(0.9f, 0.2f, 0.5f),
            new(0.4f, 1f, 0.5f),
            new(0.2f, 0.6f, 1f),
            new(1f, 0.7f, 0.4f),
            new(0.3f, 0.95f, 0.95f),
            new(1f, 0.5f, 0.9f),
            new(0.95f, 0.5f, 0.2f),
            new(0.6f, 0.3f, 1f),
            new(1f, 0.25f, 0.25f),
            new(0.25f, 0.9f, 0.35f),
            new(0.35f, 0.4f, 1f),
            new(1f, 0.9f, 0.3f),
            new(0.25f, 0.85f, 0.85f),
            new(0.95f, 0.35f, 0.75f),
            new(1f, 0.55f, 0.15f),
            new(0.65f, 0.35f, 0.95f),
            new(0.85f, 0.2f, 0.4f),
            new(0.45f, 1f, 0.4f),
            new(0.2f, 0.5f, 0.95f),
            new(1f, 0.75f, 0.35f),
            new(0.35f, 0.9f, 0.9f),
            new(0.9f, 0.45f, 0.85f)
        ];
        private string _persistPath = "";

        private void Awake()
        {
            // Must run before Start: if Start throws (e.g. API client), we still need a valid list for DrawWindow/Update.
            _activeCommands = _commands;
        }

        protected override void Start()
        {
            base.Start();
            windowID = 2002;
            windowTitle = "Action Timeline";
            windowRect = new Rect(400, 50, 806, 420);
            _persistPath = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "timeline.json");
            _persistVarsPath = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "persistent_vars.json");
            _apiClient = new CopyScriptApiClient();
            _activeCommands ??= _commands;
            LoadTimeline();
            LoadPersistentVars();
        }

        private const float WindowMinWidth = 606f;
        private const float WindowMaxWidth = 806f;
        private const float WindowMinHeight = 280f;
        private const float WindowMaxHeight = 1300f;

        // Layout heights (must match DrawWindowContent order: toolbar → space → control row → space → scroll → space → close)
        /// <summary>Toolbar row: matches <c>btnH</c> (28) — horizontal row height is max of children.</summary>
        private const float ToolbarRowHeight = 28f;
        private const float SpaceAfterToolbar = 6f;
        private const float ControlRowHeight = 26f;
        private const float SpaceAfterControlRow = 6f;
        private const float SpaceBeforeClose = 6f;
        /// <summary>Must match <c>GUILayout.Button("Close", GUILayout.Height(24))</c>.</summary>
        private const float CloseButtonHeight = 24f;
        /// <summary>Extra px added to auto-sized outer height so IMGUI/scroll rounding doesn’t clip the last row.</summary>
        private const float LayoutHeightSafetyMargin = 8f;
        /// <summary>Height of everything except the scroll list (toolbar…close + gaps). Not window title/border.</summary>
        private const float LayoutFixedHeight = ToolbarRowHeight + SpaceAfterToolbar + ControlRowHeight + SpaceAfterControlRow + SpaceBeforeClose + CloseButtonHeight;
        /// <summary>Cached window chrome (read in DrawWindow). <c>windowRect.height</c> is outer; client = outer − top − bottom.</summary>
        private float _windowTopInsetCached = 20f;
        private float _windowBottomInsetCached = 8f;
        private float _verticalGroupPadCached;

        /// <summary>Top + bottom insets from <c>GUI.skin.window</c> only (title bar + bottom border/padding).</summary>
        private static void GetWindowVerticalChrome(out float top, out float bottom)
        {
            top = 20f;
            bottom = 8f;
            try
            {
                GUIStyle? s = GUI.skin?.window;
                if (s != null)
                {
                    top = Mathf.Clamp(s.border.top + s.padding.top, 14f, 40f);
                    bottom = Mathf.Clamp(s.border.bottom + s.padding.bottom, 4f, 28f);
                }
            }
            catch { /* ignored */ }
        }

        /// <summary><c>GUILayout.BeginVertical</c> skin padding (top+bottom). Older Unity has no <c>GUISkin.verticalGroup</c>.</summary>
        private static float GetVerticalGroupVerticalPadding()
        {
            try
            {
                GUIStyle? vg = GUI.skin?.FindStyle("VerticalGroup");
                if (vg != null)
                    return vg.padding.top + vg.padding.bottom;
            }
            catch { /* ignored */ }
            return 0f;
        }

        /// <summary>
        /// IMGUI inserts this much space between each vertical child. HS2’s Unity build does not expose
        /// <c>GUISkin.verticalSpacing</c> on the referenced assemblies; default skin is ~3–4px — must match real layout.
        /// </summary>
        private const float ImGuiVerticalChildSpacing = 4f;

        private static float GetVerticalGroupSpacing() => ImGuiVerticalChildSpacing;

        /// <summary>Extra vertical padding inside the scroll view client (skin-dependent).</summary>
        private static float GetScrollViewPaddingVertical()
        {
            try
            {
                GUIStyle? st = GUI.skin?.FindStyle("scrollView");
                if (st != null)
                    return st.padding.top + st.padding.bottom;
            }
            catch { /* ignored */ }
            return 0f;
        }

        /// <summary>
        /// Total height of the scrollable command list content. Each row uses a 0-height placeholder <c>Box</c>
        /// plus a <see cref="ListRowHeight"/> row — IMGUI inserts <see cref="ImGuiVerticalChildSpacing"/> between <b>every</b> child,
        /// so we need (2·rows − 1) gaps, not (rows − 1).
        /// </summary>
        private static float ScrollListContentHeight(int commandCount, int extraBackRows)
        {
            int rows = commandCount + extraBackRows;
            float pad = GetScrollViewPaddingVertical();
            if (rows <= 0)
                return pad;
            float sp = GetVerticalGroupSpacing();
            int childCount = rows * 2; // placeholder box + row vertical per command/back row
            return rows * ListRowHeight + (childCount - 1) * sp + pad;
        }

        /// <summary>Vertical center of row <paramref name="rowIndex"/> (0 = first row in scroll content) for autoscroll.</summary>
        private static float ScrollListRowCenterY(int rowIndex)
        {
            if (rowIndex < 0)
                return 0f;
            float sp = GetVerticalGroupSpacing();
            float h = ListRowHeight;
            float rowTopY = rowIndex * (h + 2f * sp) + sp;
            return rowTopY + h * 0.5f;
        }

        private void OnDisable()
        {
            SaveTimeline();
            SavePersistentVars();
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                ClothingStateCache.EnsureFetched(this);
                AccessoryStateCache.EnsureFetched(this);
            }
        }

        public override void DrawWindow()
        {
            if (isVisible)
            {
                _activeCommands ??= _commands;
                // +1 row when in sub-view for the Back row at the top of the list
                int extraRows = _viewStack.Count > 0 ? 1 : 0;
                GetWindowVerticalChrome(out float winTop, out float winBottom);
                _windowTopInsetCached = winTop;
                _windowBottomInsetCached = winBottom;
                _verticalGroupPadCached = GetVerticalGroupVerticalPadding();
                // Inner column = fixed rows + scroll rows + GUILayout verticalGroup padding; outer adds window chrome + small safety margin
                float innerStackHeight = LayoutFixedHeight + ScrollListContentHeight(_activeCommands.Count, extraRows) + _verticalGroupPadCached;
                float desiredHeight = Mathf.Clamp(
                    innerStackHeight + _windowTopInsetCached + _windowBottomInsetCached + LayoutHeightSafetyMargin,
                    WindowMinHeight, WindowMaxHeight);
                windowRect.height = desiredHeight;
                // Do not pass Min/MaxHeight on the window — they fight fixed scroll sizing and cause flicker
                windowRect = GUILayout.Window(windowID, windowRect, DrawWindowContent, windowTitle);
                windowRect.width = Mathf.Clamp(windowRect.width, WindowMinWidth, WindowMaxWidth);
                // Snap to computed height so GUILayout return value can't oscillate frame-to-frame
                windowRect.height = desiredHeight;
                // Clamp position so window stays on screen
                float margin = 8f;
                windowRect.x = Mathf.Clamp(windowRect.x, margin, Mathf.Max(margin, Screen.width - windowRect.width - margin));
                windowRect.y = Mathf.Clamp(windowRect.y, margin, Mathf.Max(margin, Screen.height - windowRect.height - margin));

                if (_showVariablesWindow)
                {
                    _variablesWindowRect = GUILayout.Window(VariablesWindowID, _variablesWindowRect, DrawVariablesWindowContent, "Variables",
                        GUILayout.MinWidth(280f), GUILayout.MinHeight(180f), GUILayout.MaxHeight(750f));
                    _variablesWindowRect.x = Mathf.Clamp(_variablesWindowRect.x, margin, Mathf.Max(margin, Screen.width - _variablesWindowRect.width - margin));
                    _variablesWindowRect.y = Mathf.Clamp(_variablesWindowRect.y, margin, Mathf.Max(margin, Screen.height - _variablesWindowRect.height - margin));
                }

                if (_listEditorOpen)
                {
                    _listEditorWindowRect = GUILayout.Window(ListEditorWindowID, _listEditorWindowRect, DrawListEditorWindowContent, "Edit list",
                        GUILayout.MinWidth(300f), GUILayout.MinHeight(200f), GUILayout.MaxHeight(500f));
                    _listEditorWindowRect.x = Mathf.Clamp(_listEditorWindowRect.x, margin, Mathf.Max(margin, Screen.width - _listEditorWindowRect.width - margin));
                    _listEditorWindowRect.y = Mathf.Clamp(_listEditorWindowRect.y, margin, Mathf.Max(margin, Screen.height - _listEditorWindowRect.height - margin));
                }

                if (_dictEditorOpen)
                {
                    _dictEditorWindowRect = GUILayout.Window(DictEditorWindowID, _dictEditorWindowRect, DrawDictEditorWindowContent, "Edit dict",
                        GUILayout.MinWidth(300f), GUILayout.MinHeight(200f), GUILayout.MaxHeight(500f));
                    _dictEditorWindowRect.x = Mathf.Clamp(_dictEditorWindowRect.x, margin, Mathf.Max(margin, Screen.width - _dictEditorWindowRect.width - margin));
                    _dictEditorWindowRect.y = Mathf.Clamp(_dictEditorWindowRect.y, margin, Mathf.Max(margin, Screen.height - _dictEditorWindowRect.height - margin));
                }

                if (_showPersistentVarsWindow)
                {
                    _persistentVarsWindowRect = GUILayout.Window(PersistentVarsWindowID, _persistentVarsWindowRect, DrawPersistentVarsWindowContent, "Global Variables",
                        GUILayout.MinWidth(280f), GUILayout.MinHeight(180f), GUILayout.MaxHeight(750f));
                    _persistentVarsWindowRect.x = Mathf.Clamp(_persistentVarsWindowRect.x, margin, Mathf.Max(margin, Screen.width - _persistentVarsWindowRect.width - margin));
                    _persistentVarsWindowRect.y = Mathf.Clamp(_persistentVarsWindowRect.y, margin, Mathf.Max(margin, Screen.height - _persistentVarsWindowRect.height - margin));
                }

                if (_categoryWindowVisible)
                {
                    _categoryWindowRect = GUILayout.Window(CategoryWindowID, _categoryWindowRect, DrawCategoryWindowContent, "Category",
                        GUILayout.MinWidth(160f), GUILayout.MinHeight(180f));
                    // Anchor left and top of main timeline window
                    _categoryWindowRect.x = windowRect.xMin + 10f;
                    _categoryWindowRect.y = windowRect.yMin + 40f;
                }
            }
        }

        private void DrawCategoryWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            for (int i = 0; i < CategoryNames.Length; i++)
            {
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = GetCategoryColor(i);
                if (GUILayout.Button(CategoryNames[i], GUILayout.ExpandWidth(true)))
                {
                    _selectedCategory = i;
                    _categoryWindowVisible = false;
                }
                GUI.backgroundColor = prevBg;
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _categoryWindowRect.width, 20));
        }

        private void DrawListEditorWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("One value per line:", GUILayout.ExpandWidth(false));
            _listEditorText = GUILayout.TextArea(_listEditorText ?? "", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Done", GUILayout.Width(80)))
            {
                string[] lines = (_listEditorText ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.None);
                var trimmed = new List<string>();
                foreach (string line in lines)
                {
                    string t = line.Trim();
                    if (t.Length > 0)
                        trimmed.Add(t);
                }
                _listEditorOnApply?.Invoke(trimmed.ToArray());
                _listEditorOpen = false;
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                _listEditorOpen = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _listEditorWindowRect.width, 20));
            IMGUIUtils.EatInputInRect(_listEditorWindowRect);
        }

        private void DrawDictEditorWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("One key=value per line:", GUILayout.ExpandWidth(false));
            _dictEditorText = GUILayout.TextArea(_dictEditorText ?? "", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Done", GUILayout.Width(80)))
            {
                _dictEditorOnApply?.Invoke(ParseDictEditorText(_dictEditorText));
                _dictEditorOpen = false;
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                _dictEditorOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _dictEditorWindowRect.width, 20));
            IMGUIUtils.EatInputInRect(_dictEditorWindowRect);
        }

        private static Dictionary<string, string> ParseDictEditorText(string text)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) return dict;
            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                int eq = trimmed.IndexOf('=');
                if (eq < 0) continue;
                string k = trimmed.Substring(0, eq).Trim();
                string v = trimmed.Substring(eq + 1);
                if (k.Length > 0) dict[k] = v;
            }
            return dict;
        }

        private static string SerializeDictEditorText(Dictionary<string, string> dict)
        {
            if (dict == null || dict.Count == 0) return "";
            var lines = new System.Text.StringBuilder();
            foreach (var kv in dict)
            {
                if (lines.Length > 0) lines.Append('\n');
                lines.Append(kv.Key).Append('=').Append(kv.Value);
            }
            return lines.ToString();
        }

        private Vector2 _variablesScrollPosition;

        private void DrawVariablesWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            TimelineVariableStore store = _isRunning && _runContext != null ? _runContext.Variables : _designTimeVariables;
            string storeLabel = _isRunning ? "Run (cleared when timeline ends)" : "Design-time (persistent)";
            GUILayout.Label(storeLabel, GUILayout.ExpandWidth(false));

            var snapshot = store.GetSnapshotForDisplay();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(72));
            GUILayout.Label("Type", GUILayout.Width(36));
            GUILayout.Label("Value", GUILayout.ExpandWidth(true));
            GUILayout.Space(4);
            GUILayout.Label("", GUILayout.Width(24)); // Del column
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _variablesScrollPosition = GUILayout.BeginScrollView(_variablesScrollPosition, GUILayout.ExpandWidth(true));
            foreach (var (name, type, value) in snapshot)
            {
                string n = name ?? "";
                if (string.IsNullOrEmpty(n)) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(n, GUILayout.Width(72));
                GUILayout.Label(type ?? "", GUILayout.Width(36));
                if (type == "string")
                {
                    string current = store.GetString(n);
                    string newVal = GUILayout.TextField(current ?? "", GUILayout.ExpandWidth(true));
                    if (newVal != current)
                        store.SetString(n, newVal);
                }
                else if (type == "int")
                {
                    int current = store.GetInt(n);
                    string s = GUILayout.TextField(current.ToString(), GUILayout.ExpandWidth(true));
                    if (int.TryParse(s, out int nVal))
                        store.SetInt(n, nVal);
                }
                else if (type == "bool")
                {
                    bool current = store.GetBool(n);
                    bool next = GUILayout.Toggle(current, current ? "True" : "False", GUILayout.ExpandWidth(true));
                    if (next != current)
                        store.SetBoolExclusive(n, next);
                }
                else if (type == "list")
                {
                    string displayValue = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
                    GUILayout.Label(displayValue ?? "", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Edit", GUILayout.Width(36)))
                    {
                        _listEditorText = string.Join("\n", store.GetList(n));
                        string captureName = n;
                        TimelineVariableStore captureStore = store;
                        _listEditorOnApply = arr => captureStore.SetList(captureName, arr);
                        _listEditorWindowRect = new Rect(_variablesWindowRect.xMax + 8f, _variablesWindowRect.yMin, 320f, 280f);
                        _listEditorOpen = true;
                    }
                }
                else if (type == "dict")
                {
                    string displayValue = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
                    GUILayout.Label(displayValue ?? "", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Edit", GUILayout.Width(36)))
                    {
                        _dictEditorText = SerializeDictEditorText(store.GetDict(n));
                        string captureName = n;
                        TimelineVariableStore captureStore = store;
                        _dictEditorOnApply = d => captureStore.SetDict(captureName, d);
                        _dictEditorWindowRect = new Rect(_variablesWindowRect.xMax + 8f, _variablesWindowRect.yMin, 320f, 280f);
                        _dictEditorOpen = true;
                    }
                }
                if (GUILayout.Button("X", GUILayout.Width(24)))
                    store.Remove(n);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.Label("Add variable:", GUILayout.ExpandWidth(false));
            GUILayout.BeginHorizontal();
            _newVarType = GUILayout.Toolbar(_newVarType, new[] { "String", "Int", "Bool", "List", "Dict" }, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(36));
            _newVarName = GUILayout.TextField(_newVarName ?? "", GUILayout.Width(100));
            if (_newVarType == 0)
            {
                GUILayout.Label("Value", GUILayout.Width(32));
                _newVarValue = GUILayout.TextField(_newVarValue ?? "", GUILayout.ExpandWidth(true));
            }
            else if (_newVarType == 1)
            {
                GUILayout.Label("Value", GUILayout.Width(32));
                _newVarValue = GUILayout.TextField(_newVarValue ?? "", GUILayout.ExpandWidth(true));
            }
            else if (_newVarType == 2)
            {
                GUILayout.Label("Value", GUILayout.Width(32));
                _newVarBool = GUILayout.Toggle(_newVarBool, _newVarBool ? "True" : "False", GUILayout.ExpandWidth(true));
            }
            else if (_newVarType == 3)
            {
                if (GUILayout.Button("Edit list...", GUILayout.Width(72)))
                {
                    _listEditorText = string.Join("\n", _newVarList);
                    _listEditorOnApply = arr =>
                    {
                        _newVarList.Clear();
                        if (arr != null)
                            _newVarList.AddRange(arr);
                    };
                    _listEditorWindowRect = new Rect(_variablesWindowRect.xMax + 8f, _variablesWindowRect.yMin, 320f, 280f);
                    _listEditorOpen = true;
                }
                GUILayout.Label(_newVarList.Count > 0 ? $"{_newVarList.Count} items" : "(empty)", GUILayout.MinWidth(50));
            }
            else
            {
                if (GUILayout.Button("Edit dict...", GUILayout.Width(72)))
                {
                    _dictEditorText = SerializeDictEditorText(_newVarDict);
                    _dictEditorOnApply = d =>
                    {
                        _newVarDict.Clear();
                        foreach (var kv in d)
                            _newVarDict[kv.Key] = kv.Value;
                    };
                    _dictEditorWindowRect = new Rect(_variablesWindowRect.xMax + 8f, _variablesWindowRect.yMin, 320f, 280f);
                    _dictEditorOpen = true;
                }
                GUILayout.Label(_newVarDict.Count > 0 ? $"{_newVarDict.Count} entries" : "(empty)", GUILayout.MinWidth(50));
            }
            if (GUILayout.Button("Add", GUILayout.Width(40)))
            {
                string name = (_newVarName ?? "").Trim();
                if (name.Length > 0)
                {
                    if (_newVarType == 0)
                        store.SetString(name, _newVarValue ?? "");
                    else if (_newVarType == 1)
                        store.SetInt(name, int.TryParse(_newVarValue, out int n) ? n : 0);
                    else if (_newVarType == 2)
                        store.SetBoolExclusive(name, _newVarBool);
                    else if (_newVarType == 3)
                        store.SetList(name, _newVarList);
                    else
                        store.SetDict(name, _newVarDict);
                    _newVarName = "";
                    _newVarValue = "";
                    _newVarList.Clear();
                    _newVarDict.Clear();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _variablesWindowRect.width, 20));
            IMGUIUtils.EatInputInRect(_variablesWindowRect);
        }

        /// <summary>Must match row <c>GUILayout.Height</c> and background textures; IMGUI rows often need ~30–32px with default skin.</summary>
        private const float ListRowHeight = 32f;

        private void MarkPersistentVarsDirty()
        {
            _persistentVarsDirty = true;
            _persistentVarsDirtyTime = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            _activeCommands ??= _commands;
            if (_persistentVarsDirty && Time.realtimeSinceStartup - _persistentVarsDirtyTime >= PersistentVarsSaveDelay)
            {
                _persistentVarsDirty = false;
                SavePersistentVars();
            }
            // Autoscroll: find which index in _activeCommands is currently executing
            int autoscrollIdx = GetRunningIndexInActiveView();
            if (_isRunning && _autoscrollDuringRun && autoscrollIdx >= 0 && autoscrollIdx < _activeCommands.Count)
            {
                // Same formula as DrawWindowContent: scroll view height = window - overhead
                int backRowOffset = _viewStack.Count > 0 ? 1 : 0; // Back row at top adds one slot
                float listViewHeight = Mathf.Max(100f,
                    windowRect.height - LayoutFixedHeight - _windowTopInsetCached - _windowBottomInsetCached
                    - _verticalGroupPadCached + LayoutHeightSafetyMargin);
                float totalListHeight = ScrollListContentHeight(_activeCommands.Count, backRowOffset);
                float maxScrollY = Mathf.Max(0, totalListHeight - listViewHeight);
                // +backRowOffset shifts the executing row down by one slot to account for the Back row
                float rowCenterY = ScrollListRowCenterY(autoscrollIdx + backRowOffset);
                float scrollY = rowCenterY - listViewHeight * 0.5f;
                _scrollPosition.y = Mathf.Clamp(scrollY, 0, maxScrollY);
            }
            if (_recordingMouseFor != null)
            {
                int btn = -1;
                if (Input.GetMouseButtonDown(0)) btn = 0;
                else if (Input.GetMouseButtonDown(1)) btn = 1;
                else if (Input.GetMouseButtonDown(2)) btn = 2;
                if (btn >= 0 && _recordingMouseFor is SimulateMouseCommand mouseCmd)
                {
                    float x = Input.mousePosition.x;
                    float y = Input.mousePosition.y;
                    int screenY = Screen.height - (int)y;
                    mouseCmd.SetRecorded(btn, (int)x, screenY);
                    _recordingMouseFor = null;
                }
                else if (btn >= 0 && _recordingMouseFor is MoveMouseCommand moveCmd)
                {
                    float x = Input.mousePosition.x;
                    float y = Input.mousePosition.y;
                    int screenY = Screen.height - (int)y;
                    moveCmd.SetRecordedPosition((int)x, screenY);
                    _recordingMouseFor = null;
                }
            }
        }

        protected override void DrawWindowContent(int windowID)
        {
            _activeCommands ??= _commands;

            // Single validation pass — incremental O(n) store simulation shared by the toolbar and every row.
            // This guarantees the Start/Resume button state and row highlights always agree.
            if (_frameValidationErrors == null || _frameValidationErrors.Length != _activeCommands.Count)
                _frameValidationErrors = new string?[_activeCommands.Count];
            {
                var vs = new TimelineVariableStore();
                vs.CopyFrom(_persistentVariables);
                vs.CopyFrom(_designTimeVariables);
                // When inside a subtimeline view, simulate parent commands up to the subtimeline to get correct variable state
                if (_viewStack.Count > 0)
                {
                    foreach (var (parentCmds, _, _) in _viewStack)
                    {
                        if (parentCmds == null) continue;
                        foreach (var parentCmd in parentCmds)
                        {
                            if (parentCmd == null) continue;
                            parentCmd.SimulateVariableEffects(vs);
                        }
                    }
                }
                // Pre-scan the entire root timeline to register all checkpoint names before
                // validating any command, so forward jumps (jump → checkpoint later in list)
                // and cross-subtimeline references all validate correctly.
                CollectCheckpointNames(_commands, vs);
                _frameAnyInvalidEnabled = false;
                for (int i = 0; i < _activeCommands.Count; i++)
                {
                    var cmd = _activeCommands[i];
                    if (cmd == null)
                    {
                        _frameValidationErrors[i] = "Invalid row (null command)";
                        _frameAnyInvalidEnabled = true;
                        continue;
                    }
                    string? err = cmd.GetValidationError(vs);
                    if (err == null && cmd.HasInvalidConfiguration())
                        err = "Invalid configuration";
                    _frameValidationErrors[i] = err;
                    if (cmd.Enabled && err != null)
                        _frameAnyInvalidEnabled = true;
                    cmd.SimulateVariableEffects(vs);
                }
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            bool inSubView = _viewStack.Count > 0;

            var subTlFirstByDefId = new Dictionary<string, SubTimelineCommand>(StringComparer.Ordinal);
            var subTlCountByDefId = new Dictionary<string, int>(StringComparer.Ordinal);
            {
                var ordered = new List<SubTimelineCommand>();
                CollectSubTimelineCommandsRecursive(_commands, ordered);
                foreach (var st in ordered)
                {
                    string id = st.Id;
                    if (!subTlFirstByDefId.ContainsKey(id))
                        subTlFirstByDefId[id] = st;
                    subTlCountByDefId[id] = (subTlCountByDefId.TryGetValue(id, out int n) ? n : 0) + 1;
                }
            }

            // Add command toolbar — always visible; adds to _activeCommands (which is the subtimeline list when in sub-view)
            float btnW = 74f;
            float btnH = 28f;
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = GetCategoryColor(_selectedCategory);
            if (GUILayout.Button(CategoryNames[_selectedCategory] + " \u25bc", GUILayout.Width(100), GUILayout.Height(btnH)))
            {
                _categoryWindowVisible = !_categoryWindowVisible;
                if (_categoryWindowVisible)
                    _categoryWindowRect = new Rect(windowRect.xMin + 10f, windowRect.yMin + 40f, 180f, 260f);
            }
            GUI.backgroundColor = prevBg;
            GUILayout.Space(8);
            switch (_selectedCategory)
            {
                case 0: // CopyScript Controls
                    DrawAddButton("Start", "start_tracking", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Stop", "stop_tracking", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Copy", "copy_rename", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Clean", "clear_tracked", btnW, btnH);
                    break;
                case 1: // CopyScript Checks
                    DrawAddButton("Wait SS", "wait_screenshot", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Wait 0", "wait_empty_screenshots", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Resolve", "resolve_on_issue", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Resolve #", "resolve_on_count", btnW, btnH);
                    break;
                case 2: // CopyScript Config
                    DrawAddButton("Source", "set_source_path", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Dest", "set_destination_path", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Pattern", "set_name_pattern", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Rule C", "set_rule_counter", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Rule L", "set_rule_list", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Rule B", "set_rule_batch", btnW, btnH);
                    break;
                case 3: // Input
                    DrawAddButton("Key", "simulate_key", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Mouse", "simulate_mouse", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Move", "move_mouse", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Scroll", "scroll", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Confirm", "confirm", btnW, btnH);
                    break;
                case 4: // VNGE
                    DrawAddButton("Load", "vnge_load_scene", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Next", "vnge_scene_next", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Prev", "vnge_scene_prev", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("NextSc", "vnge_next_scene", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("PrevSc", "vnge_prev_scene", btnW, btnH);
                    break;
                case 5: // Studio
                    DrawAddButton("Pose", "pose_library", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Clothing", "clothing_state", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Accessory", "accessory_state", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Camera", "set_camera_by_name", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Sel Object", "select_object_by_name", btnW, btnH);
                    break;
                case 6: // Screenshot
                    DrawAddButton("Screenshot", "screenshot", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("SS alpha", "screenshot_alpha", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("SS size", "screenshot_resolution", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("SS path", "screenshot_save_path", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("SS alt var", "screenshot_alt_path_var", btnW, btnH);
                    break;
                case 7: // Simple Variables
                    DrawAddButton("Set", "set", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Get", "get", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Str Repl", "str_replace", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Calc", "calc", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("If", "if", btnW, btnH);
                    break;
                case 8: // Advanced Variables
                    DrawAddButton("Set List", "set_list", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("List", "list", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("List Insert", "list_insert", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("List Remove", "list_remove", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Dict Set", "dict_set", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Dict Get", "dict_get", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("List+Dict", "list_apply_dict", btnW, btnH);
                    break;
                case 9: // Nav
                    DrawAddButton("Check", "checkpoint", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Jump", "jump", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Loop", "loop", btnW, btnH);
                    break;
                case 10: // Misc
                    DrawAddButton("Pause", "pause", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Sound", "sound", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Label", "label", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Sub", "sub_timeline", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Return", "return", btnW, btnH);
                    if (inSubView)
                    {
                        GUILayout.Space(2);
                        DrawAddButton("Param", "sub_timeline_param", btnW, btnH);
                    }
                    break;
                case 11: // Video
                    DrawAddButton("Record", "video_record", btnW, btnH);
                    break;
                case 12: // FashionLine
                    DrawAddButton("Outfit", "outfit_rotate", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Outfit name", "outfit_by_name", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Get Fashion", "get_fashion", btnW, btnH);
                    break;
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            // Control buttons (Clear, Save, Load, Import, Start/Stop)
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            bool prevEnabledCtrl = GUI.enabled;
            if (inSubView) GUI.enabled = false; // Clear/Save/Load/Import disabled in subtimeline view
            if (GUILayout.Button("Clear", GUILayout.Width(58), GUILayout.Height(26)))
            {
                _activeCommands.Clear();
                SaveTimeline();
            }
            GUILayout.Space(4);
            if (GUILayout.Button("Save", GUILayout.Width(58), GUILayout.Height(26)))
                SaveTimelineToFile();
            GUILayout.Space(4);
            if (GUILayout.Button("Load", GUILayout.Width(58), GUILayout.Height(26)))
                LoadTimelineFromFile();
            GUILayout.Space(4);
            if (GUILayout.Button("Import", GUILayout.Width(58), GUILayout.Height(26)))
                ImportTimeline();
            GUI.enabled = prevEnabledCtrl;
            GUILayout.Space(8);
            if (_isRunning)
            {
                if (GUILayout.Button("Stop", GUILayout.Width(58), GUILayout.Height(26)))
                    _stopRequested = true;
                GUILayout.Space(4);
                if (_isPaused)
                {
                    bool hadInvalid = _frameAnyInvalidEnabled;
                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = !hadInvalid;
                    if (GUILayout.Button("Resume", GUILayout.Width(58), GUILayout.Height(26)))
                    {
                        _totalPausedDuration += Time.realtimeSinceStartup - _pauseStartTime;
                        _pauseStartTime = 0f;
                        _isPaused = false;
                    }
                    GUI.enabled = prevEnabled;
                    _startButtonHoverTooltip = "";
                    if (hadInvalid && Event.current != null && Event.current.type == EventType.Repaint)
                    {
                        Rect btnRect = GUILayoutUtility.GetLastRect();
                        if (btnRect.Contains(Event.current.mousePosition))
                            _startButtonHoverTooltip = CollectValidationErrors();
                    }
                }
                else if (GUILayout.Button("Pause", GUILayout.Width(58), GUILayout.Height(26)))
                {
                    _pauseStartTime = Time.realtimeSinceStartup;
                    _isPaused = true;
                }
            }
            else
            {
                bool hadInvalid = _frameAnyInvalidEnabled || inSubView;
                bool prevEnabled = GUI.enabled;
                GUI.enabled = !hadInvalid;
                if (GUILayout.Button("Start", GUILayout.Width(58), GUILayout.Height(26)))
                    StartTimeline(0);
                GUI.enabled = prevEnabled;
                _startButtonHoverTooltip = "";
                if (hadInvalid && !inSubView && Event.current != null && Event.current.type == EventType.Repaint)
                {
                    Rect btnRect = GUILayoutUtility.GetLastRect();
                    if (btnRect.Contains(Event.current.mousePosition))
                        _startButtonHoverTooltip = CollectValidationErrors();
                }
            }
            GUILayout.Space(4);
            bool showCrosses = GUILayout.Toggle(_showMousePositions, "Crosses", GUILayout.Width(62), GUILayout.Height(26));
            if (showCrosses != _showMousePositions)
                _showMousePositions = showCrosses;
            GUILayout.Space(4);
            bool showVars = GUILayout.Toggle(_showVariablesWindow, "Variables", GUILayout.Width(68), GUILayout.Height(26));
            if (showVars != _showVariablesWindow)
            {
                _showVariablesWindow = showVars;
                if (_showVariablesWindow)
                    _variablesWindowRect = new Rect(windowRect.xMax + 10f, windowRect.yMin, 320f, 420f);
            }
            GUILayout.Space(4);
            bool showPersist = GUILayout.Toggle(_showPersistentVarsWindow, "Globals", GUILayout.Width(60), GUILayout.Height(26));
            if (showPersist != _showPersistentVarsWindow)
            {
                _showPersistentVarsWindow = showPersist;
                if (_showPersistentVarsWindow)
                    _persistentVarsWindowRect = new Rect(windowRect.xMax + 10f, windowRect.yMin + 440f, 320f, 420f);
            }
            GUILayout.Space(4);
            bool autoscroll = GUILayout.Toggle(_autoscrollDuringRun, "Autoscroll", GUILayout.Width(72), GUILayout.Height(26));
            if (autoscroll != _autoscrollDuringRun)
                _autoscrollDuringRun = autoscroll;
            float displayElapsed = GetDisplayElapsedSeconds();
            if (displayElapsed >= 0f)
            {
                int mins = (int)(displayElapsed / 60f);
                float secs = displayElapsed - mins * 60f;
                string timeStr = $"{mins:00}:{secs:00}";
                GUILayout.Space(8);
                GUILayout.Label(timeStr, GUILayout.Width(48), GUILayout.Height(26));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // List — use fixed height so scroll math in Update matches the actual visible area
            // Outer − window chrome − verticalGroup padding − fixed chrome = scroll (must match DrawWindow / Update)
            float listViewHeight = Mathf.Max(100f,
                windowRect.height - LayoutFixedHeight - _windowTopInsetCached - _windowBottomInsetCached
                - _verticalGroupPadCached + LayoutHeightSafetyMargin);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandWidth(true), GUILayout.Height(listViewHeight));
            var ctx = new InlineDrawContext
            {
                OpenListEditor = (getValues, setValues) =>
                {
                    _listEditorText = string.Join("\n", getValues());
                    _listEditorOnApply = arr =>
                    {
                        setValues(arr);
                        SaveTimeline();
                    };
                    _listEditorWindowRect = new Rect(windowRect.xMin + 20f, windowRect.yMin + 40f, 320f, 280f);
                    _listEditorOpen = true;
                },
                // Allow opening nested views while running; execution does not depend on the editor view.
                OpenSubTimeline = sub => { if (sub != null) _pendingOpenSubTimeline = sub; },
                IsInSubTimeline = inSubView,
                OnSubTimelineTitleCommitted = TryResolveSubTimelineTemplateByTitle
            };
            int mouseCommandColorIndex = 0;
            float savedRowW = 2000f; // first row uses large width; updated from actual layout after each row
            int currentRunningIdx = GetRunningIndexInActiveView();

            // Back row — first item in the list when inside a subtimeline view
            if (inSubView)
            {
                GUILayout.Box("", GUIStyle.none, GUILayout.Width(0), GUILayout.Height(0));
                Rect backRowStartRect = GUILayoutUtility.GetLastRect();
                if (Event.current != null && Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color(0.15f, 0.22f, 0.38f);
                    GUI.DrawTexture(new Rect(backRowStartRect.xMin, backRowStartRect.yMin, savedRowW, ListRowHeight), GetCrossTexture());
                    GUI.color = Color.white;
                }
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.Height(ListRowHeight));
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUI.contentColor = Color.white;
                if (GUILayout.Button("\u2190 Back", GUILayout.Width(58), GUILayout.Height(22)))
                    _pendingCloseSubTimeline = true;
                var backTitleStyle = new GUIStyle(GuiSkinHelper.SafeLabelStyle()) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, fontSize = 13 };
                GUILayout.Label(BuildActivePathLabel(), backTitleStyle, GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                if (_currentSubTimelineOwner != null)
                {
                    bool isTpl = _subTimelineTemplateFlags.TryGetValue(_currentSubTimelineOwner.Id, out bool tpl) && tpl;
                    bool newTpl = GUILayout.Toggle(isTpl, "Template", GUILayout.Width(78), GUILayout.Height(22));
                    if (newTpl != isTpl)
                    {
                        _subTimelineTemplateFlags[_currentSubTimelineOwner.Id] = newTpl;
                        SaveTimeline();
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                Rect backRowEndRect = GUILayoutUtility.GetLastRect();
                savedRowW = backRowEndRect.xMax - backRowStartRect.xMin;
            }

            // Re-check size: toolbar buttons above may have added commands to _activeCommands
            if (_frameValidationErrors == null || _frameValidationErrors.Length != _activeCommands.Count)
                _frameValidationErrors = new string?[_activeCommands.Count];

            for (int i = 0; i < _activeCommands.Count; i++)
            {
                TimelineCommand cmd = _activeCommands[i];
                if (cmd == null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{i + 1}.", GUILayout.Width(30));
                    GUILayout.Label("(null row)", GUILayout.ExpandWidth(true));
                    if (!_isRunning && GUILayout.Button("X", GUILayout.Width(22)))
                    {
                        _activeCommands.RemoveAt(i);
                        SaveTimeline();
                        i--;
                    }
                    GUILayout.EndHorizontal();
                    continue;
                }
                bool isCurrent = _isRunning && i == currentRunningIdx;
                int? mouseColorIndex = null;
                if (cmd is SimulateMouseCommand mouseCmd && mouseCmd.HasValue)
                {
                    mouseColorIndex = mouseCommandColorIndex++;
                }
                else if (cmd is MoveMouseCommand moveCmd && moveCmd.HasValue)
                {
                    mouseColorIndex = mouseCommandColorIndex++;
                }
                // Placeholder to get row start position in scroll content space
                GUILayout.Box("", GUIStyle.none, GUILayout.Width(0), GUILayout.Height(0));
                Rect rowStartRect = GUILayoutUtility.GetLastRect();
                float rowStartX = rowStartRect.xMin;
                bool isLabelLike = cmd is LabelCommand || cmd is SubTimelineCommand;
                if (Event.current != null && Event.current.type == EventType.Repaint)
                {
                    Color cmdColor = GetCommandColor(cmd.TypeId);
                    var rowRect = new Rect(rowStartX, rowStartRect.yMin, savedRowW, ListRowHeight);
                    if (cmd is LabelCommand lc)
                    {
                        float gray = Mathf.Min(0.70f, lc.Level * 0.12f);
                        GUI.color = new Color(gray, gray, gray);
                        GUI.DrawTexture(rowRect, GetCrossTexture());
                        GUI.DrawTexture(new Rect(rowStartX, rowStartRect.yMin, 5, ListRowHeight), GetCrossTexture());
                        GUI.color = Color.white;
                    }
                    else if (cmd is SubTimelineCommand)
                    {
                        // Solid dark blue-gray fill — similar to label but distinct
                        GUI.color = new Color(0.15f, 0.22f, 0.38f);
                        GUI.DrawTexture(rowRect, GetCrossTexture());
                        GUI.color = Color.white;
                    }
                    else
                    {
                        Color tint = new Color(cmdColor.r, cmdColor.g, cmdColor.b, 0.14f);
                        GUI.color = tint;
                        GUI.DrawTexture(rowRect, GetCrossTexture());
                        GUI.color = cmdColor;
                        GUI.DrawTexture(new Rect(rowStartX, rowStartRect.yMin, 5, ListRowHeight), GetCrossTexture());
                        GUI.color = Color.white;
                    }
                }
                string? validationError = (_frameValidationErrors != null && i < _frameValidationErrors.Length)
                    ? _frameValidationErrors[i]
                    : null;
                bool isInvalid = validationError != null;
                GUIStyle rowStyle = GUIStyle.none;
                if (isCurrent)
                    rowStyle = GetCurrentRowHighlightStyle();
                else if (isInvalid)
                    rowStyle = GetInvalidRowHighlightStyle();
                GUILayout.BeginVertical(rowStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ListRowHeight));
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                Color? labelRowContentColor = isLabelLike ? (Color?)GUI.contentColor : null;
                if (isLabelLike)
                    GUI.contentColor = Color.white;
                bool newEnabled = GUILayout.Toggle(cmd.Enabled, "", GUILayout.Width(18));
                if (newEnabled != cmd.Enabled)
                {
                    cmd.Enabled = newEnabled;
                    SaveTimeline();
                }
                GUILayout.Label($"{i + 1}.", GUILayout.Width(30));
                if (!_isRunning && !inSubView && GUILayout.Button("\u25b6", GUILayout.Width(20)))
                {
                    StartTimeline(i);
                }
                if (!_isRunning)
                {
                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = i > 0;
                    if (GUILayout.Button("\u25b2", GUILayout.Width(20)))
                    {
                        int step = GetMoveStepFromEvent();
                        int newIndex = Mathf.Max(0, i - step);
                        if (newIndex != i)
                        {
                            var moved = _activeCommands[i];
                            _activeCommands.RemoveAt(i);
                            _activeCommands.Insert(newIndex, moved);
                            SaveTimeline();
                        }
                    }
                    GUI.enabled = i < _activeCommands.Count - 1;
                    if (GUILayout.Button("\u25bc", GUILayout.Width(20)))
                    {
                        int step = GetMoveStepFromEvent();
                        int newIndex = Mathf.Min(_activeCommands.Count - 1, i + step);
                        if (newIndex != i)
                        {
                            var moved = _activeCommands[i];
                            _activeCommands.RemoveAt(i);
                            _activeCommands.Insert(newIndex, moved);
                            SaveTimeline();
                        }
                    }
                    GUI.enabled = prevEnabled;
                }
                string label = cmd.GetDisplayLabel(_runContext);
                string tipText = isInvalid ? (validationError ?? "Invalid configuration") : "";
                var labelContent = new GUIContent(label, tipText);
                if (_showMousePositions && mouseColorIndex.HasValue && mouseColorIndex.Value < CrossColors.Length)
                {
                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = CrossColors[mouseColorIndex.Value];
                    GUILayout.Label(labelContent, GUILayout.Width(120));
                    GUI.contentColor = prevContent;
                }
                else
                {
                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = Color.white;
                    GUILayout.Label(labelContent, GUILayout.Width(120));
                    GUI.contentColor = prevContent;
                }
                if (cmd is SubTimelineCommand stBadges)
                    DrawSubTimelineSharingBadges(stBadges, subTlFirstByDefId, subTlCountByDefId);
                if (cmd is SubTimelineCommand stParam)
                    DrawSubTimelineParamStrip(stParam);

                var rtc = _runContext;
                bool pendingOnAncestorSub = cmd is SubTimelineCommand stPending && RunningLeafIsInsideSubTimeline(stPending);

                if (_isRunning && rtc?.PendingConfirmCallback != null)
                {
                    bool showConfirm = (isCurrent && cmd is ConfirmCommand) || pendingOnAncestorSub;
                    if (showConfirm && GUILayout.Button("Confirm", GUILayout.Width(60)))
                    {
                        var cb = rtc.PendingConfirmCallback;
                        rtc.PendingConfirmCallback = null;
                        cb?.Invoke();
                    }
                }
                if (_isRunning && rtc?.PendingScreenshotAdvanceCallback != null)
                {
                    bool showScreenshotContinue = (isCurrent && cmd is ScreenshotCommand) || pendingOnAncestorSub;
                    if (showScreenshotContinue && GUILayout.Button("Continue", GUILayout.Width(60)))
                    {
                        var cb = rtc.PendingScreenshotAdvanceCallback;
                        rtc.PendingScreenshotAdvanceCallback = null;
                        cb?.Invoke();
                    }
                }
                if (_isRunning && rtc?.PendingResolveCallback != null)
                {
                    bool showResolve = isCurrent || pendingOnAncestorSub;
                    if (showResolve && GUILayout.Button("Resolve", GUILayout.Width(60)))
                    {
                        var cb = rtc.PendingResolveCallback;
                        rtc.PendingResolveCallback = null;
                        cb?.Invoke();
                    }
                }
                if (_isRunning && rtc?.PendingRetryCallback != null)
                {
                    bool showRetry = isCurrent || pendingOnAncestorSub;
                    if (showRetry && GUILayout.Button("Retry", GUILayout.Width(50)))
                    {
                        var cb = rtc.PendingRetryCallback;
                        rtc.PendingRetryCallback = null;
                        cb?.Invoke();
                    }
                }
                ctx.RecordMouse = () => StartRecordMouse(cmd);
                cmd.DrawInlineConfig(ctx);
                if (!_isRunning && GUILayout.Button("X", GUILayout.Width(22)))
                {
                    _activeCommands.RemoveAt(i);
                    SaveTimeline();
                    if (_recordingMouseFor == cmd) _recordingMouseFor = null;
                    i--;
                }
                if (labelRowContentColor.HasValue)
                    GUI.contentColor = labelRowContentColor.Value;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                Rect rowEndRect = GUILayoutUtility.GetLastRect();
                savedRowW = rowEndRect.xMax - rowStartX;
            }
            GUILayout.EndScrollView();

            string activeTooltip = GUI.tooltip != "" ? GUI.tooltip : _startButtonHoverTooltip;
            if (activeTooltip != "")
                DrawValidationTooltip(activeTooltip, windowRect);

            GUILayout.Space(6);
            if (GUILayout.Button("Close", GUILayout.Height(24)))
            {
                var sandboxGUI = FindObjectOfType<SandboxGUI>();
                if (sandboxGUI != null)
                    sandboxGUI.SetWindowVisible(SandboxWindowKeys.Timeline, false);
                else
                    SetVisible(false);
            }

            GUILayout.EndVertical();

            FlushPendingSubTimelineNavigation();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private static Texture2D? _crossTexture;
        private static Texture2D? _currentRowHighlightTexture;
        private static GUIStyle? _currentRowHighlightStyle;
        private static GUIStyle? _invalidRowHighlightStyle;
        private static GUIStyle? _subTimelineParamLabelStyle;

        private static GUIStyle GetSubTimelineParamLabelStyle()
        {
            if (_subTimelineParamLabelStyle != null) return _subTimelineParamLabelStyle;
            _subTimelineParamLabelStyle = new GUIStyle(GuiSkinHelper.SafeLabelStyle())
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            return _subTimelineParamLabelStyle;
        }

        private static Texture2D GetCurrentRowHighlightTexture()
        {
            if (_currentRowHighlightTexture != null) return _currentRowHighlightTexture;
            _currentRowHighlightTexture = new Texture2D(1, 1);
            _currentRowHighlightTexture.SetPixel(0, 0, new Color(0.2f, 0.7f, 0.2f, 0.45f));
            _currentRowHighlightTexture.Apply();
            return _currentRowHighlightTexture;
        }

        private static GUIStyle GetCurrentRowHighlightStyle()
        {
            if (_currentRowHighlightStyle != null) return _currentRowHighlightStyle;
            var tex = GetCurrentRowHighlightTexture();
            _currentRowHighlightStyle = new GUIStyle(GUIStyle.none)
            {
                fixedHeight = ListRowHeight,
                normal = { background = tex },
                hover = { background = tex },
                active = { background = tex }
            };
            return _currentRowHighlightStyle;
        }

        private static GUIStyle GetInvalidRowHighlightStyle()
        {
            if (_invalidRowHighlightStyle != null) return _invalidRowHighlightStyle;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0.7f, 0.2f, 0.2f, 0.45f));
            tex.Apply();
            _invalidRowHighlightStyle = new GUIStyle(GUIStyle.none)
            {
                fixedHeight = ListRowHeight,
                normal = { background = tex },
                hover = { background = tex },
                active = { background = tex }
            };
            return _invalidRowHighlightStyle;
        }

        private static GUIStyle? _tooltipStyle;

        private static int GetMoveStepFromEvent()
        {
            Event? e = Event.current;
            if (e == null) return 1;
            if (e.shift) return 5;
            if (e.control) return 10;
            return 1;
        }

        /// <summary>GUI.skin can be null on some frames / early IMGUI; avoid NRE in window chrome.</summary>
        private static void DrawValidationTooltip(string text, Rect windowRect)
        {
            if (_tooltipStyle == null)
            {
                var bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.12f, 0.95f));
                bg.Apply();
                _tooltipStyle = new GUIStyle(GuiSkinHelper.SafeLabelStyle())
                {
                    normal = { background = bg, textColor = new Color(1f, 0.82f, 0.82f) },
                    padding = new RectOffset(6, 6, 4, 4),
                    wordWrap = true,
                    fontSize = 11,
                    richText = false
                };
            }

            // CalcSize gives single-line width; CalcHeight correctly handles wrapped multi-line text.
            float boxW = Mathf.Min(_tooltipStyle.CalcSize(new GUIContent(text)).x + 12f, 300f);
            float boxH = _tooltipStyle.CalcHeight(new GUIContent(text), boxW) + 8f;

            // Position just above the cursor, clamped inside the window
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            float x = Mathf.Clamp(mouse.x, 0f, windowRect.width - boxW);
            float y = Mathf.Clamp(mouse.y - boxH - 4f, 0f, windowRect.height - boxH);
            GUI.Label(new Rect(x, y, boxW, boxH), text, _tooltipStyle);
        }

        private static Texture2D GetCrossTexture()
        {
            if (_crossTexture == null)
            {
                _crossTexture = new Texture2D(1, 1);
                _crossTexture.SetPixel(0, 0, Color.white);
                _crossTexture.Apply();
            }
            return _crossTexture;
        }

        /// <summary>
        /// Called from SandboxGUI.OnGUI after all windows so crosses draw on top. Draws crosses at each mouse command position.
        /// </summary>
        public void DrawCrossesOverlay()
        {
            if (!_showMousePositions) return;
            Texture2D tex = GetCrossTexture();
            int colorIndex = 0;
            const int crossHalf = 6;
            const int crossThick = 2;
            Color prevColor = GUI.color;
            foreach (TimelineCommand cmd in _commands)
            {
                int x, y;
                if (cmd is SimulateMouseCommand mouseCmd && mouseCmd.HasValue)
                { x = mouseCmd.ScreenX; y = mouseCmd.ScreenY; }
                else if (cmd is MoveMouseCommand moveCmd && moveCmd.HasValue)
                { x = moveCmd.ScreenX; y = moveCmd.ScreenY; }
                else continue;
                if (colorIndex >= CrossColors.Length) break;
                GUI.color = CrossColors[colorIndex];
                GUI.DrawTexture(new Rect(x - crossHalf, y - crossThick / 2, crossHalf * 2, crossThick), tex);
                GUI.DrawTexture(new Rect(x - crossThick / 2, y - crossHalf, crossThick, crossHalf * 2), tex);
                colorIndex++;
            }
            GUI.color = prevColor;
        }

        void IOverlayDrawable.DrawOverlay() => DrawCrossesOverlay();

        /// <summary>Simulated variable store after executing commands 0..index-1. Used for interpolation validation.</summary>
        private TimelineVariableStore GetVariablesAtIndex(int index)
        {
            var store = new TimelineVariableStore();
            store.CopyFrom(_persistentVariables);
            store.CopyFrom(_designTimeVariables);
            CollectCheckpointNames(_commands, store);
            for (int j = 0; j < index && j < _commands.Count; j++)
                _commands[j].SimulateVariableEffects(store);
            return store;
        }

        /// <summary>
        /// Returns true if any enabled command is currently marked as invalid
        /// (based on simulated variables at each index).
        /// </summary>
        private bool HasAnyInvalidCommands()
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                var cmd = _commands[i];
                if (!cmd.Enabled) continue;
                if (cmd.HasInvalidConfiguration(GetVariablesAtIndex(i)))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Builds a multi-line summary of every enabled invalid command from the frame cache,
        /// in the form "Row N (Label): message". Used as the tooltip on the disabled Start/Resume button.
        /// </summary>
        private string CollectValidationErrors()
        {
            if (_frameValidationErrors == null || _activeCommands == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _activeCommands.Count && i < _frameValidationErrors.Length; i++)
            {
                var rowCmd = _activeCommands[i];
                if (rowCmd == null) continue;
                if (!rowCmd.Enabled) continue;
                string? error = _frameValidationErrors[i];
                if (error != null)
                    sb.AppendLine($"Row {i + 1} ({rowCmd.GetDisplayLabel()}): {error}");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Per-frame validation error per command index (null = valid). Rebuilt at start of DrawWindowContent.</summary>
        private string?[]? _frameValidationErrors;
        private bool _frameAnyInvalidEnabled;
        private string _startButtonHoverTooltip = "";

        private void StartRecordMouse(TimelineCommand cmd)
        {
            _recordingMouseFor = cmd;
        }

        private void DrawSubTimelineParamStrip(SubTimelineCommand sub)
        {
            SubTimelineParamCommand? pcmd = SubTimelineParamCommand.FindFirst(sub);
            if (pcmd == null) return;
            SubTimelineParamInputs inp = sub.ParamInputs;
            GUILayout.BeginHorizontal();
            string paramLabel = string.IsNullOrWhiteSpace(pcmd.VariableName) ? "In" : pcmd.VariableName.Trim();
            GUILayout.Label(paramLabel, GetSubTimelineParamLabelStyle(), GUILayout.Width(56), GUILayout.ExpandWidth(false));
            int uid = sub.ParamRowInstanceId;
            switch (pcmd.Kind)
            {
                case SubTimelineParamKind.String:
                {
                    GUI.SetNextControlName($"stparam_{uid}_s");
                    string ns = GUILayout.TextField(inp.StringText ?? "", GUILayout.MinWidth(70), GUILayout.ExpandWidth(true));
                    if (ns != inp.StringText) { inp.StringText = ns; SaveTimeline(); }
                    break;
                }
                case SubTimelineParamKind.Int:
                {
                    GUI.SetNextControlName($"stparam_{uid}_i");
                    string ni = GUILayout.TextField(inp.IntText ?? "0", GUILayout.Width(56));
                    if (ni != inp.IntText) { inp.IntText = ni; SaveTimeline(); }
                    break;
                }
                case SubTimelineParamKind.Bool:
                {
                    GUI.SetNextControlName($"stparam_{uid}_b");
                    bool nb = GUILayout.Toggle(inp.BoolValue, "");
                    if (nb != inp.BoolValue) { inp.BoolValue = nb; SaveTimeline(); }
                    break;
                }
                case SubTimelineParamKind.List:
                {
                    GUI.SetNextControlName($"stparam_{uid}_listbtn");
                    if (GUILayout.Button("List…", GUILayout.Width(40), GUILayout.Height(18)))
                        OpenSubTimelineParamListEditor(sub);
                    GUILayout.Label(PreviewSubTimelineParamList(inp.ListItems), GUILayout.MinWidth(48), GUILayout.ExpandWidth(true));
                    break;
                }
                case SubTimelineParamKind.Dict:
                {
                    GUI.SetNextControlName($"stparam_{uid}_dictbtn");
                    if (GUILayout.Button("Dict…", GUILayout.Width(40), GUILayout.Height(18)))
                        OpenSubTimelineParamDictEditor(sub);
                    GUILayout.Label(PreviewSubTimelineParamDict(inp.Dict), GUILayout.MinWidth(48), GUILayout.ExpandWidth(true));
                    break;
                }
            }
            GUILayout.EndHorizontal();
        }

        private static string PreviewSubTimelineParamList(List<string> items)
        {
            if (items == null || items.Count == 0) return "(empty)";
            string j = string.Join("; ", items);
            return j.Length <= 36 ? j : j.Substring(0, 33) + "...";
        }

        private static string PreviewSubTimelineParamDict(Dictionary<string, string> d)
        {
            if (d == null || d.Count == 0) return "(empty)";
            return $"{d.Count} keys";
        }

        private void OpenSubTimelineParamListEditor(SubTimelineCommand sub)
        {
            SubTimelineParamInputs inp = sub.ParamInputs;
            _listEditorText = string.Join("\n", inp.ListItems);
            _listEditorOnApply = arr =>
            {
                inp.SetListFromArray(arr);
                SaveTimeline();
            };
            _listEditorWindowRect = new Rect(windowRect.xMin + 24f, windowRect.yMin + 120f, 320f, 280f);
            _listEditorOpen = true;
        }

        private void OpenSubTimelineParamDictEditor(SubTimelineCommand sub)
        {
            SubTimelineParamInputs inp = sub.ParamInputs;
            _dictEditorText = SubTimelineParamInputs.SerializeDictLines(inp.Dict);
            _dictEditorOnApply = d =>
            {
                inp.Dict.Clear();
                foreach (var kv in d)
                    inp.Dict[kv.Key] = kv.Value;
                SaveTimeline();
            };
            _dictEditorWindowRect = new Rect(windowRect.xMin + 24f, windowRect.yMin + 120f, 320f, 280f);
            _dictEditorOpen = true;
        }

        private void AddCommand(string typeId)
        {
            try
            {
                if (typeId == "sub_timeline_param")
                {
                    if (_viewStack.Count == 0)
                    {
                        SandboxServices.Log.LogWarning("Param can only be added inside a subtimeline.");
                        return;
                    }
                    foreach (var c in _activeCommands)
                    {
                        if (c is SubTimelineParamCommand)
                        {
                            SandboxServices.Log.LogWarning("This subtimeline already has a Param command.");
                            return;
                        }
                    }
                }
                _activeCommands.Add(TimelineCommandFactory.Create(typeId));
                SaveTimeline();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Add command failed: {ex.Message}");
            }
        }

        /// <summary>Elapsed seconds to show in the timer (excludes paused time when running). Returns -1 if nothing to display.</summary>
        private float GetDisplayElapsedSeconds()
        {
            if (_isRunning)
            {
                float totalPaused = _totalPausedDuration + (_isPaused ? (Time.realtimeSinceStartup - _pauseStartTime) : 0f);
                return (Time.realtimeSinceStartup - _timelineStartTime) - totalPaused;
            }
            return _lastRunElapsedSeconds;
        }

        // ── SubTimeline view navigation ──────────────────────────────────────

        private void FlushPendingSubTimelineNavigation()
        {
            if (_pendingCloseSubTimeline)
            {
                _pendingCloseSubTimeline = false;
                CloseSubTimelineView();
            }
            if (_pendingOpenSubTimeline != null)
            {
                var sub = _pendingOpenSubTimeline;
                _pendingOpenSubTimeline = null;
                OpenSubTimelineView(sub);
            }
        }

        private void OpenSubTimelineView(SubTimelineCommand sub)
        {
            _viewStack.Push((_activeCommands, _activeTitle, _currentSubTimelineOwner));
            _activeCommands = sub.SubCommands;
            _activeTitle = sub.Title;
            _currentSubTimelineOwner = sub;
            _scrollPosition = Vector2.zero;
            _frameValidationErrors = null; // force rebuild for new view
        }

        private void CloseSubTimelineView()
        {
            if (_viewStack.Count == 0) return;
            var (parentCmds, parentTitle, parentOwner) = _viewStack.Pop();
            _activeCommands = parentCmds;
            _activeTitle = parentTitle;
            _currentSubTimelineOwner = parentOwner;
            _scrollPosition = Vector2.zero;
            _frameValidationErrors = null;
        }

        /// <summary>
        /// Builds the breadcrumb path shown in the back row, e.g. "Sub1 › Sub2 › Sub3".
        /// Parent titles come from the view stack (bottom = closest to root, top = direct parent).
        /// </summary>
        private string BuildActivePathLabel()
        {
            var parts = new System.Collections.Generic.List<string>();
            // Stack enumerates top→bottom; reverse to get root→current order
            var stackItems = _viewStack.ToArray();
            for (int i = stackItems.Length - 1; i >= 0; i--)
            {
                string t = stackItems[i].title;
                if (!string.IsNullOrEmpty(t))
                    parts.Add(t);
            }
            if (!string.IsNullOrEmpty(_activeTitle))
                parts.Add(_activeTitle);
            return string.Join(" \u203a ", parts); // › separator
        }

        private static void CollectSubTimelineCommandsRecursive(List<TimelineCommand> list, List<SubTimelineCommand> acc)
        {
            foreach (var c in list)
            {
                if (c is SubTimelineCommand st)
                {
                    acc.Add(st);
                    CollectSubTimelineCommandsRecursive(st.SubCommands, acc);
                }
            }
        }

        private void DrawSubTimelineSharingBadges(
            SubTimelineCommand st,
            Dictionary<string, SubTimelineCommand> firstByDefId,
            Dictionary<string, int> countByDefId)
        {
            if (!firstByDefId.TryGetValue(st.Id, out SubTimelineCommand? first))
                return;
            int n = countByDefId.TryGetValue(st.Id, out int c) ? c : 0;
            bool isFirst = ReferenceEquals(first, st);
            bool showTpl = isFirst && _subTimelineTemplateFlags.TryGetValue(st.Id, out bool isTpl) && isTpl;
            bool showRef = n > 1 && !isFirst;
            if (!showTpl && !showRef) return;

            var baseStyle = new GUIStyle(GuiSkinHelper.SafeLabelStyle())
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            if (showTpl)
            {
                var tplStyle = new GUIStyle(baseStyle) { normal = { textColor = new Color(0.55f, 0.9f, 0.6f) } };
                GUILayout.Label(new GUIContent("Tpl", "This definition is marked as a reusable template (link other rows by matching name)."), tplStyle, GUILayout.Width(30));
            }
            if (showRef)
            {
                var refStyle = new GUIStyle(baseStyle) { normal = { textColor = new Color(0.55f, 0.78f, 0.95f) } };
                GUILayout.Label(new GUIContent("Ref", "This row shares the same subtimeline body as another row (template instance)."), refStyle, GUILayout.Width(30));
            }
        }

        private void TryResolveSubTimelineTemplateByTitle(SubTimelineCommand sub)
        {
            string title = sub.Title?.Trim() ?? "";
            if (string.IsNullOrEmpty(title)) return;
            SubTimelineCommand? match = null;
            foreach (var st in EnumerateAllSubTimelineCommands())
            {
                if (ReferenceEquals(st, sub)) continue;
                if (!_subTimelineTemplateFlags.TryGetValue(st.Id, out bool isT) || !isT) continue;
                if (!string.Equals(st.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase)) continue;
                match = st;
                break;
            }
            if (match == null || match.Id == sub.Id) return;
            sub.RebindToSharedDefinition(match.Id, match.SubCommands);
            SaveTimeline();
        }

        private IEnumerable<SubTimelineCommand> EnumerateAllSubTimelineCommands()
        {
            var acc = new List<SubTimelineCommand>();
            CollectSubTimelineCommandsRecursive(_commands, acc);
            return acc;
        }

        private List<SavedSubTimelineDefinition> BuildSubTimelineDefinitionsForSave()
        {
            var acc = new List<SubTimelineCommand>();
            CollectSubTimelineCommandsRecursive(_commands, acc);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var defs = new List<SavedSubTimelineDefinition>();
            foreach (var st in acc)
            {
                if (!seen.Add(st.Id)) continue;
                var entries = new SavedTimelineEntry[st.SubCommands.Count];
                for (int i = 0; i < st.SubCommands.Count; i++)
                {
                    var c = st.SubCommands[i];
                    entries[i] = new SavedTimelineEntry { typeId = c.TypeId, payload = c.SerializePayload(), enabled = c.Enabled };
                }
                defs.Add(new SavedSubTimelineDefinition
                {
                    id = st.Id,
                    title = st.Title ?? "",
                    template = _subTimelineTemplateFlags.TryGetValue(st.Id, out bool t) && t,
                    entries = entries
                });
            }
            return defs;
        }

        private void PruneSubTimelineTemplateFlags(HashSet<string> liveDefinitionIds)
        {
            var keys = new List<string>(_subTimelineTemplateFlags.Keys);
            foreach (string k in keys)
            {
                if (!liveDefinitionIds.Contains(k))
                    _subTimelineTemplateFlags.Remove(k);
            }
        }

        private static void FillSubTimelineDefinitionList(Dictionary<string, List<TimelineCommand>> bodies, string id, SavedTimelineEntry[] rawEntries)
        {
            if (!bodies.TryGetValue(id, out var list)) return;
            list.Clear();
            foreach (var e in rawEntries)
            {
                if (string.IsNullOrEmpty(e.typeId)) continue;
                try
                {
                    string typeId = e.typeId;
                    string payload = e.payload ?? "";
                    MigrateLegacyCommand(ref typeId, ref payload);
                    var cmd = TimelineCommandFactory.Create(typeId);
                    cmd.DeserializePayload(payload);
                    cmd.Enabled = e.enabled;
                    if (cmd is SubTimelineCommand st && bodies.TryGetValue(st.Id, out var childList))
                        st.SubCommands = childList;
                    list.Add(cmd);
                }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogWarning($"SubTimeline definition {id}: load command {e.typeId} failed: {ex.Message}");
                }
            }
        }

        private void LoadCommandListFromEntries(List<SavedTimelineEntry> entries, Dictionary<string, List<TimelineCommand>>? bodies, List<TimelineCommand> target)
        {
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.typeId)) continue;
                try
                {
                    string typeId = e.typeId;
                    string payload = e.payload ?? "";
                    MigrateLegacyCommand(ref typeId, ref payload);
                    var cmd = TimelineCommandFactory.Create(typeId);
                    cmd.DeserializePayload(payload);
                    cmd.Enabled = e.enabled;
                    if (cmd is SubTimelineCommand st && bodies != null && bodies.TryGetValue(st.Id, out var subList))
                        st.SubCommands = subList;
                    target.Add(cmd);
                }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogWarning($"Load command {e.typeId} failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Returns the currently-executing index within _activeCommands by scanning _runningStack,
        /// or -1 if execution is not on the currently-viewed list.
        /// </summary>
        private int GetRunningIndexInActiveView()
        {
            foreach (var frame in _runningStack)
            {
                if (frame.Cmds == _activeCommands)
                    return frame.Idx;
            }
            return -1;
        }

        /// <summary>
        /// True if the innermost running command list is this sub's body or any nested subtimeline under it.
        /// Used to show Confirm/Resolve/Retry on ancestor Sub rows while execution waits inside.
        /// </summary>
        private bool RunningLeafIsInsideSubTimeline(SubTimelineCommand sub)
        {
            if (_runningStack.Count == 0 || sub == null) return false;
            var leaf = _runningStack[_runningStack.Count - 1];
            return IsCommandListUnderSubTimeline(leaf.Cmds, sub);
        }

        private static bool IsCommandListUnderSubTimeline(List<TimelineCommand>? list, SubTimelineCommand sub)
        {
            if (list == null || sub.SubCommands == null) return false;
            if (ReferenceEquals(list, sub.SubCommands)) return true;
            foreach (var c in sub.SubCommands)
            {
                if (c is SubTimelineCommand nested && IsCommandListUnderSubTimeline(list, nested))
                    return true;
            }
            return false;
        }

        private void StartTimeline(int startFrom = 0)
        {
            if (_commands.Count == 0) return;
            // Do not allow starting when any enabled command is invalid.
            if (HasAnyInvalidCommands())
            {
                SandboxServices.Log.LogWarning("Cannot start timeline: at least one command is marked as invalid.");
                return;
            }
            _startFromIndex = Mathf.Clamp(startFrom, 0, _commands.Count - 1);
            _isRunning = true;
            _timelineStartTime = Time.realtimeSinceStartup;
            _totalPausedDuration = 0f;
            _pauseStartTime = 0f;
            _lastRunElapsedSeconds = -1f;
            _stopRequested = false;
            _isPaused = false;
            StartCoroutine(RunTimeline());
            // Workaround: click at (0,0) so the host program reliably loses focus from this window
            WindowsInput.SimulateMouseClickAt(0, 0, 0);
        }

        private IEnumerator RunTimeline()
        {
            _runContext = new TimelineContext
            {
                ApiClient = _apiClient,
                Runner = this
            };
            var ctx = _runContext;
            ctx.Variables.CopyFrom(_persistentVariables);
            ctx.Variables.CopyFrom(_designTimeVariables);

            if (_apiClient != null)
            {
                TrackedFilesResponse? initialResult = null;
                yield return _apiClient.GetTrackedFilesAsync(1, r => initialResult = r);
                if (initialResult != null && initialResult.success && initialResult.returned_count >= 1 && initialResult.files != null && initialResult.files.Length >= 1)
                    ctx.LastScreenshotName = initialResult.files[0].original_name ?? "";
            }

            // Pre-register all checkpoints so forward jumps (to a checkpoint later in the list)
            // work even before execution reaches them. Dynamic re-registration during execution
            // handles checkpoints whose names depend on runtime variables.
            PreScanCheckpoints(_commands, ctx);
            // Also populate ctx.Variables with checkpoint names so HasInvalidConfiguration
            // checks on Jump/Loop/If during execution don't false-positive as invalid.
            CollectCheckpointNames(_commands, ctx.Variables);

            _runningStack.Clear();
            yield return StartCoroutine(RunCommandList(_commands, ctx, _startFromIndex));
            _runningStack.Clear();

            // Store last run duration so it stays visible after stop
            float totalPaused = _totalPausedDuration + (_isPaused ? (Time.realtimeSinceStartup - _pauseStartTime) : 0f);
            _lastRunElapsedSeconds = (Time.realtimeSinceStartup - _timelineStartTime) - totalPaused;
            _isRunning = false;
            _stopRequested = false;
            _isPaused = false;
            if (_runContext != null)
            {
                _runContext.PendingConfirmCallback = null;
                _runContext.PendingScreenshotAdvanceCallback = null;
                _runContext.PendingResolveCallback = null;
                _runContext.PendingRetryCallback = null;
                _runContext.SubTimelineParamRuntime = null;
                _runContext = null;
            }
            // If execution was inside a subtimeline view, close it so root is shown again
            while (_viewStack.Count > 0) CloseSubTimelineView();
        }

        /// <summary>
        /// Executes a flat list of commands sequentially. Called recursively for subtimelines.
        /// Checkpoints are registered at every nesting level. Cross-level jumps bubble up through
        /// ancestor coroutines, and can also dive into a direct-child subtimeline via PendingSubEntry.
        /// </summary>
        private IEnumerator RunCommandList(List<TimelineCommand> cmds, TimelineContext ctx, int startIndex)
        {
            var frame = new RunFrame { Cmds = cmds, Idx = startIndex };
            _runningStack.Add(frame);

            int index = startIndex;
            while (index >= 0 && index < cmds.Count && !_stopRequested)
            {
                TimelineCommand cmd = cmds[index];
                if (!cmd.Enabled)
                {
                    index++;
                    frame.Idx = index;
                    continue;
                }

                // If the current command is (now) invalid, pause and wait until fixed.
                if (cmd.HasInvalidConfiguration(ctx.Variables))
                {
                    if (!_isPaused)
                    {
                        _pauseStartTime = Time.realtimeSinceStartup;
                        _isPaused = true;
                        SandboxServices.Log.LogWarning($"Timeline paused: command at index {index + 1} is marked invalid.");
                    }
                    while (!_stopRequested && cmd.HasInvalidConfiguration(ctx.Variables))
                        yield return null;
                    if (_stopRequested) break;
                }

                frame.Idx = index;
                ctx.JumpTarget = null; // Clear stale target before executing this command

                if (cmd is SubTimelineCommand sub)
                {
                    // If a cross-level jump is targeting this subtimeline, start from that index
                    int subStart = ctx.PendingSubEntry ?? 0;
                    ctx.PendingSubEntry = null;
                    ctx.SubTimelineParamRuntime = SubTimelineParamRuntime.FromCommand(
                        SubTimelineParamCommand.FindFirstEnabled(sub), sub.ParamInputs);
                    if (_autoscrollDuringRun) OpenSubTimelineView(sub);
                    yield return StartCoroutine(RunCommandList(sub.SubCommands, ctx, subStart));
                    ctx.SubTimelineParamRuntime = null;
                    if (_autoscrollDuringRun) CloseSubTimelineView();
                }
                else
                {
                    bool done = false;
                    cmd.Execute(ctx, () => done = true);
                    while (!done && !_stopRequested)
                        yield return null;
                    if (_stopRequested) break;
                }

                // Register checkpoint from any nesting level so cross-level jumps can reach it
                if (cmd is CheckpointCommand cp)
                {
                    string name = cp.GetCheckpointName(ctx);
                    if (!string.IsNullOrEmpty(name))
                        ctx.CheckpointRegistry[name] = (cmds, index);
                }

                while (_isPaused && !_stopRequested)
                    yield return null;
                if (_stopRequested) break;

                if (ctx.ReturnRequested)
                {
                    ctx.ReturnRequested = false;
                    break; // Exit this command list; parent continues after the SubTimelineCommand, or RunTimeline ends the run
                }

                if (ctx.JumpTarget.HasValue)
                {
                    var target = ctx.JumpTarget.Value;
                    if (target.List == cmds)
                    {
                        // Same-level jump: update index and continue
                        index = target.Idx;
                        ctx.JumpTarget = null;
                        frame.Idx = index;
                        continue;
                    }

                    // Different level: check if the target lives inside a direct-child subtimeline
                    bool resolved = false;
                    for (int j = 0; j < cmds.Count; j++)
                    {
                        if (cmds[j] is SubTimelineCommand subCmd && subCmd.SubCommands == target.List)
                        {
                            ctx.PendingSubEntry = target.Idx;
                            ctx.JumpTarget = null;
                            index = j;
                            frame.Idx = index;
                            resolved = true;
                            break;
                        }
                    }

                    if (!resolved)
                        break; // Target not in this level — bubble up to parent coroutine
                }
                else
                {
                    index++;
                    frame.Idx = index;
                }
            }

            _runningStack.Remove(frame);
        }

        /// <summary>
        /// Recursively registers all checkpoints (including inside subtimelines) into
        /// ctx.CheckpointRegistry before execution starts, enabling forward jumps.
        /// Uses the variable state at call time for name interpolation.
        /// </summary>
        private static void PreScanCheckpoints(List<TimelineCommand> cmds, TimelineContext ctx)
        {
            for (int i = 0; i < cmds.Count; i++)
            {
                if (cmds[i] == null || !cmds[i].Enabled) continue;
                if (cmds[i] is CheckpointCommand cp)
                {
                    string name = cp.GetCheckpointName(ctx);
                    if (!string.IsNullOrEmpty(name))
                        ctx.CheckpointRegistry[name] = (cmds, i);
                }
                else if (cmds[i] is SubTimelineCommand sub)
                    PreScanCheckpoints(sub.SubCommands, ctx);
            }
        }

        /// <summary>
        /// Recursively collects all enabled checkpoint names into vs.CheckpointNames for
        /// design-time validation (allows jump/loop/if to flag unknown checkpoint targets).
        /// </summary>
        private static void CollectCheckpointNames(List<TimelineCommand> cmds, TimelineVariableStore vs)
        {
            foreach (var cmd in cmds)
            {
                if (cmd == null || !cmd.Enabled) continue;
                if (cmd is CheckpointCommand cp)
                {
                    string name = vs.Interpolate(cp.Name ?? "").Trim();
                    if (!string.IsNullOrEmpty(name))
                        vs.RegisterCheckpoint(name);
                }
                else if (cmd is SubTimelineCommand sub)
                    CollectCheckpointNames(sub.SubCommands, vs);
            }
        }

        // ── Persistent variable window ────────────────────────────────────────

        private void DrawPersistentVarsWindowContent(int id)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("Saved to disk — visible to all timelines as read-only seeds.", GUILayout.ExpandWidth(false));

            var snapshot = _persistentVariables.GetSnapshotForDisplay();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(72));
            GUILayout.Label("Type", GUILayout.Width(36));
            GUILayout.Label("Value", GUILayout.ExpandWidth(true));
            GUILayout.Space(4);
            GUILayout.Label("", GUILayout.Width(24));
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _persistentVarsScrollPosition = GUILayout.BeginScrollView(_persistentVarsScrollPosition, GUILayout.ExpandWidth(true));
            foreach (var (name, type, value) in snapshot)
            {
                string n = name ?? "";
                if (string.IsNullOrEmpty(n)) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(n, GUILayout.Width(72));
                GUILayout.Label(type ?? "", GUILayout.Width(36));
                if (type == "string")
                {
                    string current = _persistentVariables.GetString(n);
                    string newVal = GUILayout.TextField(current ?? "", GUILayout.ExpandWidth(true));
                    if (newVal != current) { _persistentVariables.SetString(n, newVal); MarkPersistentVarsDirty(); }
                }
                else if (type == "int")
                {
                    int current = _persistentVariables.GetInt(n);
                    string s = GUILayout.TextField(current.ToString(), GUILayout.ExpandWidth(true));
                    if (int.TryParse(s, out int nVal) && nVal != current) { _persistentVariables.SetInt(n, nVal); MarkPersistentVarsDirty(); }
                }
                else if (type == "bool")
                {
                    bool current = _persistentVariables.GetBool(n);
                    bool next = GUILayout.Toggle(current, current ? "True" : "False", GUILayout.ExpandWidth(true));
                    if (next != current) { _persistentVariables.SetBoolExclusive(n, next); MarkPersistentVarsDirty(); }
                }
                else if (type == "list")
                {
                    string displayValue = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
                    GUILayout.Label(displayValue ?? "", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Edit", GUILayout.Width(36)))
                    {
                        _listEditorText = string.Join("\n", _persistentVariables.GetList(n));
                        string captureName = n;
                        _listEditorOnApply = arr => { _persistentVariables.SetList(captureName, arr); MarkPersistentVarsDirty(); };
                        _listEditorWindowRect = new Rect(_persistentVarsWindowRect.xMax + 8f, _persistentVarsWindowRect.yMin, 320f, 280f);
                        _listEditorOpen = true;
                    }
                }
                else if (type == "dict")
                {
                    string displayValue = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
                    GUILayout.Label(displayValue ?? "", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Edit", GUILayout.Width(36)))
                    {
                        _dictEditorText = SerializeDictEditorText(_persistentVariables.GetDict(n));
                        string captureName = n;
                        _dictEditorOnApply = d => { _persistentVariables.SetDict(captureName, d); MarkPersistentVarsDirty(); };
                        _dictEditorWindowRect = new Rect(_persistentVarsWindowRect.xMax + 8f, _persistentVarsWindowRect.yMin, 320f, 280f);
                        _dictEditorOpen = true;
                    }
                }
                if (GUILayout.Button("X", GUILayout.Width(24))) { _persistentVariables.Remove(n); MarkPersistentVarsDirty(); }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.Label("Add global variable:", GUILayout.ExpandWidth(false));
            GUILayout.BeginHorizontal();
            _newPVarType = GUILayout.Toolbar(_newPVarType, new[] { "String", "Int", "Bool", "List", "Dict" }, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(36));
            _newPVarName = GUILayout.TextField(_newPVarName ?? "", GUILayout.Width(100));
            if (_newPVarType == 0)
            {
                GUILayout.Label("Value", GUILayout.Width(32));
                _newPVarValue = GUILayout.TextField(_newPVarValue ?? "", GUILayout.ExpandWidth(true));
            }
            else if (_newPVarType == 1)
            {
                GUILayout.Label("Value", GUILayout.Width(32));
                _newPVarValue = GUILayout.TextField(_newPVarValue ?? "", GUILayout.ExpandWidth(true));
            }
            else if (_newPVarType == 2)
            {
                GUILayout.Label("Value", GUILayout.Width(32));
                _newPVarBool = GUILayout.Toggle(_newPVarBool, _newPVarBool ? "True" : "False", GUILayout.ExpandWidth(true));
            }
            else if (_newPVarType == 3)
            {
                if (GUILayout.Button("Edit list...", GUILayout.Width(72)))
                {
                    _listEditorText = string.Join("\n", _newPVarList);
                    _listEditorOnApply = arr => { _newPVarList.Clear(); if (arr != null) _newPVarList.AddRange(arr); };
                    _listEditorWindowRect = new Rect(_persistentVarsWindowRect.xMax + 8f, _persistentVarsWindowRect.yMin, 320f, 280f);
                    _listEditorOpen = true;
                }
                GUILayout.Label(_newPVarList.Count > 0 ? $"{_newPVarList.Count} items" : "(empty)", GUILayout.MinWidth(50));
            }
            else
            {
                if (GUILayout.Button("Edit dict...", GUILayout.Width(72)))
                {
                    _dictEditorText = SerializeDictEditorText(_newPVarDict);
                    _dictEditorOnApply = d => { _newPVarDict.Clear(); foreach (var kv in d) _newPVarDict[kv.Key] = kv.Value; };
                    _dictEditorWindowRect = new Rect(_persistentVarsWindowRect.xMax + 8f, _persistentVarsWindowRect.yMin, 320f, 280f);
                    _dictEditorOpen = true;
                }
                GUILayout.Label(_newPVarDict.Count > 0 ? $"{_newPVarDict.Count} entries" : "(empty)", GUILayout.MinWidth(50));
            }
            if (GUILayout.Button("Add", GUILayout.Width(40)))
            {
                string pname = (_newPVarName ?? "").Trim();
                if (pname.Length > 0)
                {
                    if (_newPVarType == 0)        _persistentVariables.SetString(pname, _newPVarValue ?? "");
                    else if (_newPVarType == 1)   _persistentVariables.SetInt(pname, int.TryParse(_newPVarValue, out int nv) ? nv : 0);
                    else if (_newPVarType == 2)   _persistentVariables.SetBoolExclusive(pname, _newPVarBool);
                    else if (_newPVarType == 3)   _persistentVariables.SetList(pname, _newPVarList);
                    else                          _persistentVariables.SetDict(pname, _newPVarDict);
                    MarkPersistentVarsDirty();
                    _newPVarName = "";
                    _newPVarValue = "";
                    _newPVarList.Clear();
                    _newPVarDict.Clear();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _persistentVarsWindowRect.width, 20));
            IMGUIUtils.EatInputInRect(_persistentVarsWindowRect);
        }

        private void SavePersistentVars()
        {
            try
            {
                string dir = Path.GetDirectoryName(_persistVarsPath) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var variables = BuildVariablesList(_persistentVariables);
                string json = BuildTimelineJson(Array.Empty<SavedTimelineEntry>(), variables);
                File.WriteAllText(_persistVarsPath, json);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Save persistent vars failed: {ex.Message}");
            }
        }

        private void LoadPersistentVars()
        {
            try
            {
                if (!File.Exists(_persistVarsPath)) return;
                string json = File.ReadAllText(_persistVarsPath);
                _persistentVariables.Clear();
                ApplySavedVariables(json, replace: true, target: _persistentVariables);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Load persistent vars failed: {ex.Message}");
            }
        }

        // ── Timeline save / load ─────────────────────────────────────────────

        private void SaveTimeline()
        {
            try
            {
                string dir = Path.GetDirectoryName(_persistPath) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var entries = new SavedTimelineEntry[_commands.Count];
                for (int i = 0; i < _commands.Count; i++)
                    entries[i] = new SavedTimelineEntry { typeId = _commands[i].TypeId, payload = _commands[i].SerializePayload(), enabled = _commands[i].Enabled };
                var variables = BuildVariablesList(_designTimeVariables);
                var subDefs = BuildSubTimelineDefinitionsForSave();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var d in subDefs)
                    seen.Add(d.id);
                PruneSubTimelineTemplateFlags(seen);
                string json = BuildTimelineJson(entries, variables, subDefs.Count > 0 ? subDefs : null);
                File.WriteAllText(_persistPath, json);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Save timeline failed: {ex.Message}");
            }
        }

        private static string EscapeJsonString(string? s) => TimelineJsonHelper.EscapeJsonString(s);

        private const char SavedVariablesListSeparator = '\u0002';
        private const char SavedVariablesDictKvSeparator = '\u0003';

        private static string BuildTimelineJson(SavedTimelineEntry[] entries, List<(string name, string type, string value)>? variables = null, List<SavedSubTimelineDefinition>? subtimelines = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\"entries\":");
            TimelineJsonHelper.AppendSavedEntriesArray(sb, entries);
            if (variables != null && variables.Count > 0)
            {
                sb.Append(",\"variables\":[");
                for (int i = 0; i < variables.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var v = variables[i];
                    sb.Append("{\"name\":\"").Append(EscapeJsonString(v.name))
                        .Append("\",\"type\":\"").Append(EscapeJsonString(v.type))
                        .Append("\",\"value\":\"").Append(EscapeJsonString(v.value)).Append("\"}");
                }
                sb.Append("]");
            }
            if (subtimelines != null && subtimelines.Count > 0)
            {
                sb.Append(",\"subtimelines\":[");
                for (int i = 0; i < subtimelines.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    TimelineJsonHelper.AppendSubTimelineDefinitionObject(sb, subtimelines[i]);
                }
                sb.Append(']');
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static List<(string name, string type, string value)> BuildVariablesList(TimelineVariableStore store)
        {
            var list = new List<(string name, string type, string value)>();
            foreach (var (n, v) in store.GetAllStrings())
                list.Add((n, "string", v ?? ""));
            foreach (var (n, v) in store.GetAllInts())
                list.Add((n, "int", v.ToString()));
            foreach (var (n, v) in store.GetAllBools())
                list.Add((n, "bool", v ? "True" : "False"));
            foreach (var (n, listVal) in store.GetAllLists())
                list.Add((n, "list", string.Join(SavedVariablesListSeparator.ToString(), listVal ?? new List<string>())));
            foreach (var (n, dict) in store.GetAllDicts())
            {
                var parts = new System.Text.StringBuilder();
                foreach (var kv in dict)
                {
                    if (parts.Length > 0) parts.Append(SavedVariablesListSeparator);
                    parts.Append(kv.Key.Replace(SavedVariablesDictKvSeparator.ToString(), ""))
                         .Append(SavedVariablesDictKvSeparator)
                         .Append(kv.Value);
                }
                list.Add((n, "dict", parts.ToString()));
            }
            return list;
        }

        private void LoadTimeline()
        {
            try
            {
                if (!File.Exists(_persistPath)) return;
                string json = File.ReadAllText(_persistPath);
                LoadTimelineFromJson(json, replace: true);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Load timeline failed: {ex.Message}");
            }
        }

        /// <summary>Maps legacy command type ids to current equivalents (e.g. vnge_first -> vnge_load_scene with payload 1).</summary>
        private static void MigrateLegacyCommand(ref string typeId, ref string payload)
        {
            if (typeId == "vnge_first")
            {
                typeId = "vnge_load_scene";
                payload = "1";
            }
        }

        private void LoadTimelineFromJson(string json, bool replace = true)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            if (replace)
            {
                _commands.Clear();
                ClearDesignTimeVariables();
                _subTimelineTemplateFlags.Clear();
                _viewStack.Clear();
                _activeCommands = _commands;
                _activeTitle = "";
                _currentSubTimelineOwner = null;
            }
            if (TimelineJsonHelper.TryParseSubTimelinesArray(json, out List<SavedSubTimelineDefinition>? subDefs) && subDefs != null && subDefs.Count > 0)
            {
                var bodies = new Dictionary<string, List<TimelineCommand>>(StringComparer.Ordinal);
                foreach (var d in subDefs)
                {
                    if (string.IsNullOrEmpty(d.id)) continue;
                    bodies[d.id] = new List<TimelineCommand>();
                    _subTimelineTemplateFlags[d.id] = d.template;
                }
                foreach (var d in subDefs)
                {
                    if (string.IsNullOrEmpty(d.id) || !bodies.ContainsKey(d.id)) continue;
                    FillSubTimelineDefinitionList(bodies, d.id, d.entries ?? Array.Empty<SavedTimelineEntry>());
                }
                if (TryParseTimelineJson(json, out List<SavedTimelineEntry>? entries) && entries != null)
                    LoadCommandListFromEntries(entries, bodies, _commands);
                ApplySavedVariables(json, replace);
                return;
            }
            List<SavedTimelineEntry>? entriesLegacy = null;
            if (TryParseTimelineJson(json, out entriesLegacy) && entriesLegacy != null && entriesLegacy.Count > 0)
            {
                LoadCommandListFromEntries(entriesLegacy, bodies: null, _commands);
                ApplySavedVariables(json, replace);
                return;
            }
            var wrapper = JsonUtility.FromJson<SavedTimelineWrapper>(json);
            if (wrapper?.entries != null && wrapper.entries.Length > 0)
                LoadCommandListFromEntries(new List<SavedTimelineEntry>(wrapper.entries), bodies: null, _commands);
            ApplySavedVariables(json, replace);
        }

        private void ApplySavedVariables(string json, bool replace, TimelineVariableStore? target = null)
        {
            target ??= _designTimeVariables;
            if (!TryParseVariablesJson(json, out List<(string name, string type, string value)>? variables) || variables == null || variables.Count == 0)
                return;
            foreach (var (name, type, value) in variables)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                string n = name.Trim();
                if (!replace)
                {
                    if (target.HasString(n) || target.HasInt(n) || target.HasBool(n) || target.HasList(n) || target.HasDict(n))
                        continue;
                }
                if (type == "string")
                    target.SetString(n, value ?? "");
                else if (type == "int")
                    target.SetInt(n, int.TryParse(value, out int iv) ? iv : 0);
                else if (type == "bool")
                    target.SetBool(n, TimelineVariableStore.TryParseBoolText(value ?? "", out bool bv) ? bv : false);
                else if (type == "list")
                {
                    var list = new List<string>();
                    if (!string.IsNullOrEmpty(value))
                    {
                        foreach (string part in value.Split(SavedVariablesListSeparator))
                            list.Add(part ?? "");
                    }
                    target.SetList(n, list);
                }
                else if (type == "dict")
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(value))
                    {
                        foreach (string entry in value.Split(SavedVariablesListSeparator))
                        {
                            int sep = entry.IndexOf(SavedVariablesDictKvSeparator);
                            if (sep < 0) continue;
                            string k = entry.Substring(0, sep).Trim();
                            string v = entry.Substring(sep + 1);
                            if (k.Length > 0) dict[k] = v;
                        }
                    }
                    target.SetDict(n, dict);
                }
            }
        }

        private void ClearDesignTimeVariables()
        {
            _designTimeVariables.Clear();
        }

        private static bool TryParseVariablesJson(string json, out List<(string name, string type, string value)>? variables)
        {
            variables = null;
            int i = json.IndexOf("\"variables\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            i = json.IndexOf('[', i);
            if (i < 0) return false;
            variables = new List<(string name, string type, string value)>();
            i++;
            while (i < json.Length)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart < 0) break;
                int objEnd = TimelineJsonHelper.IndexOfMatchingBrace(json, objStart);
                if (objEnd < 0) break;
                string obj = json.Substring(objStart, objEnd - objStart + 1);
                if (TryParseVariableEntry(obj, out string? name, out string? type, out string? value))
                    variables.Add((name ?? "", type ?? "string", value ?? ""));
                i = objEnd + 1;
                int next = json.IndexOf('{', i);
                if (next < 0) break;
                i = next;
            }
            return variables.Count >= 0;
        }

        private static bool TryParseVariableEntry(string obj, out string? name, out string? type, out string? value)
        {
            name = null;
            type = null;
            value = null;
            if (!TimelineJsonHelper.TryParseJsonStringValue(obj, "name", out name)) return false;
            if (!TimelineJsonHelper.TryParseJsonStringValue(obj, "type", out type)) type = "string";
            if (!TimelineJsonHelper.TryParseJsonStringValue(obj, "value", out value)) value = "";
            return true;
        }

        private static bool TryParseTimelineJson(string json, out List<SavedTimelineEntry>? entries)
            => TimelineJsonHelper.TryParseTimelineJson(json, out entries);

        private void SaveTimelineToFile()
        {
            if (_isRunning) return;
            string? path = NativeFileDialog.SaveFile("Save Timeline", "json", "JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0");
            if (string.IsNullOrEmpty(path)) return;
            string savePath = path!;
            try
            {
                var entries = new SavedTimelineEntry[_commands.Count];
                for (int i = 0; i < _commands.Count; i++)
                    entries[i] = new SavedTimelineEntry { typeId = _commands[i].TypeId, payload = _commands[i].SerializePayload(), enabled = _commands[i].Enabled };
                var variables = BuildVariablesList(_designTimeVariables);
                var subDefs = BuildSubTimelineDefinitionsForSave();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var d in subDefs)
                    seen.Add(d.id);
                PruneSubTimelineTemplateFlags(seen);
                string json = BuildTimelineJson(entries, variables, subDefs.Count > 0 ? subDefs : null);
                if (!savePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    savePath += ".json";
                File.WriteAllText(savePath, json);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Save timeline failed: {ex.Message}");
            }
        }

        private void LoadTimelineFromFile()
        {
            if (_isRunning) return;
            string? path = NativeFileDialog.OpenFile("Load Timeline", "JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                string json = File.ReadAllText(path!);
                LoadTimelineFromJson(json, replace: true);
                SaveTimeline();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Load timeline failed: {ex.Message}");
            }
        }

        private void ImportTimeline()
        {
            if (_isRunning) return;
            string? path = NativeFileDialog.OpenFile("Import Timeline", "JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                string json = File.ReadAllText(path!);
                LoadTimelineFromJson(json, replace: false);
                SaveTimeline();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogError($"Import timeline failed: {ex.Message}");
            }
        }
    }
}


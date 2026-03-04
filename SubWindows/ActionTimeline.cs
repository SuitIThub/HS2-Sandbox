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
    public class ActionTimeline : SubWindow
    {
        private readonly List<TimelineCommand> _commands = new();
        private Vector2 _scrollPosition;
        private bool _isRunning;
        private bool _stopRequested;
        private bool _isPaused;
        private int _runningIndex = -1;
        private TimelineContext? _runContext;
        private bool _showMousePositions;
        private bool _showVariablesWindow;
        private bool _autoscrollDuringRun = true;
        private Rect _variablesWindowRect;
        private const int VariablesWindowID = 2004;
        /// <summary>Persistent variables when timeline is not running. Seeded into run when timeline starts.</summary>
        private readonly TimelineVariableStore _designTimeVariables = new TimelineVariableStore();
        private int _newVarType; // 0=string, 1=int, 2=list
        private string _newVarName = "";
        private string _newVarValue = "";
        private readonly List<string> _newVarList = new List<string>();
        private bool _listEditorOpen;
        private string _listEditorText = "";
        private Action<string[]>? _listEditorOnApply;
        private Rect _listEditorWindowRect;
        private const int ListEditorWindowID = 2005;
        private TimelineCommand? _recordingMouseFor;
        private CopyScriptApiClient? _apiClient;
        private float _timelineStartTime; // realtimeSinceStartup when timeline started
        private float _totalPausedDuration; // accumulated seconds paused this run
        private float _pauseStartTime; // realtimeSinceStartup when current pause started (0 if not paused)
        private float _lastRunElapsedSeconds = -1f; // last run duration to show after stop (-1 = none)
        private int _startFromIndex; // when starting, begin at this command index
        private static readonly string[] CategoryNames = { "CopyScript Controls", "CopyScript Checks", "CopyScript Config", "Input", "VNGE", "Studio", "Variables", "Misc" };
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
            ["outfit_rotate"] = new Color(0.38f, 0.85f, 0.78f),
            ["outfit_by_name"] = new Color(0.36f, 0.82f, 0.76f),
            ["screenshot"] = new Color(0.42f, 0.7f, 0.72f),
            // Variables (slate / blue-gray)
            ["set_string"] = new Color(0.5f, 0.6f, 0.85f),
            ["set_integer"] = new Color(0.45f, 0.58f, 0.88f),
            ["set_list"] = new Color(0.48f, 0.58f, 0.82f),
            ["calc"] = new Color(0.55f, 0.62f, 0.9f),
            ["if"] = new Color(0.52f, 0.58f, 0.88f),
            ["list"] = new Color(0.48f, 0.6f, 0.86f),
            // Misc (warm orange/brown)
            ["checkpoint"] = new Color(0.85f, 0.55f, 0.3f),
            ["jump"] = new Color(0.82f, 0.5f, 0.35f),
            ["loop"] = new Color(0.88f, 0.6f, 0.38f),
            ["pause"] = new Color(0.9f, 0.58f, 0.32f),
            ["sound"] = new Color(0.86f, 0.52f, 0.4f),
            ["label"] = Color.black,
        };

        private static Color GetCommandColor(string typeId)
        {
            return CommandColors.TryGetValue(typeId, out var c) ? c : new Color(0.5f, 0.5f, 0.55f);
        }

        /// <summary>Representative typeId per category for category color (same order as CategoryNames).</summary>
        private static readonly string[] CategoryRepresentativeTypeIds = { "start_tracking", "wait_screenshot", "set_source_path", "simulate_key", "vnge_scene_next", "pose_library", "set_string", "checkpoint" };

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

        protected override void Start()
        {
            base.Start();
            windowID = 2002;
            windowTitle = "Action Timeline";
            windowRect = new Rect(400, 50, 806, 420);
            _persistPath = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "timeline.json");
            _apiClient = new CopyScriptApiClient();
            LoadTimeline();
        }

        private const float WindowMinWidth = 606f;
        private const float WindowMaxWidth = 806f;
        private const float WindowMinHeight = 280f;
        private const float WindowMaxHeight = 1300f;

        // Layout heights (must match DrawWindowContent order: toolbar → space → control row → space → scroll → space → close)
        private const float ToolbarRowHeight = 32f;
        private const float SpaceAfterToolbar = 6f;
        private const float ControlRowHeight = 26f;
        private const float SpaceAfterControlRow = 6f;
        private const float SpaceBeforeClose = 6f;
        private const float CloseButtonHeight = 72f;
        /// <summary>Height of everything except the scroll list (toolbar + control row + space before close + close button). Used for scroll height and desired window height.</summary>
        private const float LayoutFixedHeight = ToolbarRowHeight + SpaceAfterToolbar + ControlRowHeight + SpaceAfterControlRow + SpaceBeforeClose + CloseButtonHeight;

        private void OnDisable()
        {
            SaveTimeline();
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
                float contentHeight = LayoutFixedHeight + _commands.Count * ListRowHeight;
                float desiredHeight = Mathf.Clamp(contentHeight, WindowMinHeight, WindowMaxHeight);
                windowRect.height = desiredHeight;
                windowRect = GUILayout.Window(windowID, windowRect, DrawWindowContent, windowTitle,
                    GUILayout.MinHeight(desiredHeight), GUILayout.MaxHeight(WindowMaxHeight));
                windowRect.width = Mathf.Clamp(windowRect.width, WindowMinWidth, WindowMaxWidth);
                // Keep window at desired height so scroll only appears when content exceeds 1300px
                windowRect.height = Mathf.Clamp(Mathf.Max(windowRect.height, desiredHeight), WindowMinHeight, WindowMaxHeight);
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
                else
                {
                    string displayValue = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
                    GUILayout.Label(displayValue ?? "", GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Edit", GUILayout.Width(36)))
                    {
                        _listEditorText = string.Join("\n", store.GetList(n));
                        string captureName = n;
                        TimelineVariableStore captureStore = store;
                        _listEditorOnApply = arr =>
                        {
                            captureStore.SetList(captureName, arr);
                        };
                        _listEditorWindowRect = new Rect(_variablesWindowRect.xMax + 8f, _variablesWindowRect.yMin, 320f, 280f);
                        _listEditorOpen = true;
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
            _newVarType = GUILayout.Toolbar(_newVarType, new[] { "String", "Int", "List" }, GUILayout.ExpandWidth(false));
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
            else
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
            if (GUILayout.Button("Add", GUILayout.Width(40)))
            {
                string name = (_newVarName ?? "").Trim();
                if (name.Length > 0)
                {
                    if (_newVarType == 0)
                        store.SetString(name, _newVarValue ?? "");
                    else if (_newVarType == 1)
                        store.SetInt(name, int.TryParse(_newVarValue, out int n) ? n : 0);
                    else
                        store.SetList(name, _newVarList);
                    _newVarName = "";
                    _newVarValue = "";
                    _newVarList.Clear();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _variablesWindowRect.width, 20));
            IMGUIUtils.EatInputInRect(_variablesWindowRect);
        }

        private const float ListRowHeight = 28f;

        private void Update()
        {
            if (_isRunning && _autoscrollDuringRun && _runningIndex >= 0 && _runningIndex < _commands.Count)
            {
                // Same formula as DrawWindowContent: scroll view height = window - overhead
                float listViewHeight = Mathf.Max(100f, windowRect.height - LayoutFixedHeight - 120f);
                float totalListHeight = _commands.Count * ListRowHeight;
                float maxScrollY = Mathf.Max(0, totalListHeight - listViewHeight);
                // Center the executing row in the view: scroll so row center aligns with view center
                float rowCenterY = _runningIndex * ListRowHeight + ListRowHeight * 0.5f;
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
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // Add command toolbar — category dropdown + buttons for selected category
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
                    _categoryWindowRect = new Rect(windowRect.xMin + 10f, windowRect.yMin + 40f, 180f, 220f);
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
                    DrawAddButton("Outfit", "outfit_rotate", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Outfit name", "outfit_by_name", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Accessory", "accessory_state", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Screenshot", "screenshot", btnW, btnH);
                    break;
                case 6: // Variables
                    DrawAddButton("Set Str", "set_string", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Set Int", "set_integer", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Set List", "set_list", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Calc", "calc", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("If", "if", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("List", "list", btnW, btnH);
                    break;
                default: // Misc
                    DrawAddButton("Check", "checkpoint", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Jump", "jump", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Loop", "loop", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Pause", "pause", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Sound", "sound", btnW, btnH);
                    GUILayout.Space(2);
                    DrawAddButton("Label", "label", btnW, btnH);
                    break;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Control buttons (Clear, Save, Load, Import, Start/Stop)
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            if (GUILayout.Button("Clear", GUILayout.Width(58), GUILayout.Height(26)))
            {
                _commands.Clear();
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
            GUILayout.Space(8);
            if (_isRunning)
            {
                if (GUILayout.Button("Stop", GUILayout.Width(58), GUILayout.Height(26)))
                    _stopRequested = true;
                GUILayout.Space(4);
                if (_isPaused)
                {
                    if (GUILayout.Button("Resume", GUILayout.Width(58), GUILayout.Height(26)))
                    {
                        _totalPausedDuration += Time.realtimeSinceStartup - _pauseStartTime;
                        _pauseStartTime = 0f;
                        _isPaused = false;
                    }
                }
                else if (GUILayout.Button("Pause", GUILayout.Width(58), GUILayout.Height(26)))
                {
                    _pauseStartTime = Time.realtimeSinceStartup;
                    _isPaused = true;
                }
            }
            else if (GUILayout.Button("Start", GUILayout.Width(58), GUILayout.Height(26)))
                StartTimeline(0);
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
            float listViewHeight = Mathf.Max(100f, windowRect.height - LayoutFixedHeight);
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
                }
            };
            int mouseCommandColorIndex = 0;
            float savedRowW = 2000f; // first row uses large width; updated from actual layout after each row
            for (int i = 0; i < _commands.Count; i++)
            {
                TimelineCommand cmd = _commands[i];
                bool isCurrent = _isRunning && i == _runningIndex;
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
                if (Event.current.type == EventType.Repaint)
                {
                    Color cmdColor = GetCommandColor(cmd.TypeId);
                    var rowRect = new Rect(rowStartX, rowStartRect.yMin, savedRowW, ListRowHeight);
                    if (cmd is LabelCommand)
                    {
                        GUI.color = Color.black;
                        GUI.DrawTexture(rowRect, GetCrossTexture());
                        GUI.DrawTexture(new Rect(rowStartX, rowStartRect.yMin, 5, ListRowHeight), GetCrossTexture());
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
                if (isCurrent && Event.current.type == EventType.Repaint)
                {
                    var rowRect = new Rect(rowStartRect.xMin, rowStartRect.yMin, savedRowW, ListRowHeight);
                    GUI.DrawTexture(rowRect, GetCurrentRowHighlightTexture());
                }
                if (isCurrent)
                {
                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.Height(ListRowHeight));
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                }
                else if (cmd.HasInvalidConfiguration(GetVariablesAtIndex(i)))
                {
                    GUILayout.BeginVertical(GetInvalidRowHighlightStyle(), GUILayout.ExpandWidth(true), GUILayout.Height(ListRowHeight));
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                }
                else
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                Color? labelRowContentColor = (cmd is LabelCommand) ? (Color?)GUI.contentColor : null;
                if (cmd is LabelCommand)
                    GUI.contentColor = Color.white;
                bool newEnabled = GUILayout.Toggle(cmd.Enabled, "", GUILayout.Width(18));
                if (newEnabled != cmd.Enabled)
                {
                    cmd.Enabled = newEnabled;
                    SaveTimeline();
                }
                GUILayout.Label($"{i + 1}.", GUILayout.Width(30));
                if (!_isRunning && GUILayout.Button("\u25b6", GUILayout.Width(20)))
                {
                    StartTimeline(i);
                }
                if (!_isRunning)
                {
                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = i > 0;
                    if (GUILayout.Button("\u25b2", GUILayout.Width(20)))
                    {
                        int step = Event.current.shift ? 5 : (Event.current.control ? 10 : 1);
                        int newIndex = Mathf.Max(0, i - step);
                        if (newIndex != i)
                        {
                            var moved = _commands[i];
                            _commands.RemoveAt(i);
                            _commands.Insert(newIndex, moved);
                            SaveTimeline();
                        }
                    }
                    GUI.enabled = i < _commands.Count - 1;
                    if (GUILayout.Button("\u25bc", GUILayout.Width(20)))
                    {
                        int step = Event.current.shift ? 5 : (Event.current.control ? 10 : 1);
                        int newIndex = Mathf.Min(_commands.Count - 1, i + step);
                        if (newIndex != i)
                        {
                            var moved = _commands[i];
                            _commands.RemoveAt(i);
                            _commands.Insert(newIndex, moved);
                            SaveTimeline();
                        }
                    }
                    GUI.enabled = prevEnabled;
                }
                string label = cmd.GetDisplayLabel(_runContext);
                if (_showMousePositions && mouseColorIndex.HasValue && mouseColorIndex.Value < CrossColors.Length)
                {
                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = CrossColors[mouseColorIndex.Value];
                    GUILayout.Label(label, GUILayout.Width(120));
                    GUI.contentColor = prevContent;
                }
                else
                {
                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = Color.white;
                    GUILayout.Label(label, GUILayout.Width(120));
                    GUI.contentColor = prevContent;
                }
                if (_isRunning && isCurrent && cmd is ConfirmCommand && _runContext?.PendingConfirmCallback != null)
                {
                    if (GUILayout.Button("Confirm", GUILayout.Width(60)))
                    {
                        var cb = _runContext.PendingConfirmCallback;
                        _runContext.PendingConfirmCallback = null;
                        cb?.Invoke();
                    }
                }
                if (_isRunning && isCurrent && _runContext?.PendingResolveCallback != null)
                {
                    if (GUILayout.Button("Resolve", GUILayout.Width(60)))
                    {
                        var cb = _runContext.PendingResolveCallback;
                        _runContext.PendingResolveCallback = null;
                        cb?.Invoke();
                    }
                }
                if (_isRunning && isCurrent && _runContext?.PendingRetryCallback != null)
                {
                    if (GUILayout.Button("Retry", GUILayout.Width(50)))
                    {
                        var cb = _runContext.PendingRetryCallback;
                        _runContext.PendingRetryCallback = null;
                        cb?.Invoke();
                    }
                }
                ctx.RecordMouse = () => StartRecordMouse(cmd);
                cmd.DrawInlineConfig(ctx);
                if (!_isRunning && GUILayout.Button("X", GUILayout.Width(22)))
                {
                    _commands.RemoveAt(i);
                    SaveTimeline();
                    if (_recordingMouseFor == cmd) _recordingMouseFor = null;
                    i--;
                }
                if (labelRowContentColor.HasValue)
                    GUI.contentColor = labelRowContentColor.Value;
                if (isCurrent || cmd.HasInvalidConfiguration(GetVariablesAtIndex(i)))
                {
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
                else
                    GUILayout.EndHorizontal();
                Rect rowEndRect = GUILayoutUtility.GetLastRect();
                savedRowW = rowEndRect.xMax - rowStartX;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            if (GUILayout.Button("Close", GUILayout.Height(24)))
            {
                SetVisible(false);
                var sandboxGUI = FindObjectOfType<SandboxGUI>();
                if (sandboxGUI != null)
                    sandboxGUI.SetSubwindowState("Window2", false);
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private static Texture2D? _crossTexture;
        private static Texture2D? _currentRowHighlightTexture;
        private static GUIStyle? _currentRowHighlightStyle;
        private static GUIStyle? _invalidRowHighlightStyle;

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

        /// <summary>Simulated variable store after executing commands 0..index-1. Used for interpolation validation.</summary>
        private TimelineVariableStore GetVariablesAtIndex(int index)
        {
            var store = new TimelineVariableStore();
            store.CopyFrom(_designTimeVariables);
            for (int j = 0; j < index && j < _commands.Count; j++)
                _commands[j].SimulateVariableEffects(store);
            return store;
        }

        private void StartRecordMouse(TimelineCommand cmd)
        {
            _recordingMouseFor = cmd;
        }

        private void AddCommand(string typeId)
        {
            try
            {
                _commands.Add(TimelineCommandFactory.Create(typeId));
                SaveTimeline();
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Add command failed: {ex.Message}");
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

        private void StartTimeline(int startFrom = 0)
        {
            if (_commands.Count == 0) return;
            _startFromIndex = Mathf.Clamp(startFrom, 0, _commands.Count - 1);
            _isRunning = true;
            _timelineStartTime = Time.realtimeSinceStartup;
            _totalPausedDuration = 0f;
            _pauseStartTime = 0f;
            _lastRunElapsedSeconds = -1f;
            _stopRequested = false;
            _isPaused = false;
            _runningIndex = -1;
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
            ctx.Variables.CopyFrom(_designTimeVariables);

            if (_apiClient != null)
            {
                TrackedFilesResponse? initialResult = null;
                yield return _apiClient.GetTrackedFilesAsync(1, r => initialResult = r);
                if (initialResult != null && initialResult.success && initialResult.returned_count >= 1 && initialResult.files != null && initialResult.files.Length >= 1)
                    ctx.LastScreenshotName = initialResult.files[0].original_name ?? "";
            }

            int index = _startFromIndex;
            while (index >= 0 && index < _commands.Count && !_stopRequested)
            {
                TimelineCommand cmd = _commands[index];
                if (!cmd.Enabled)
                {
                    index++;
                    continue;
                }
                _runningIndex = index;
                ctx.NextIndex = null;
                bool done = false;
                cmd.Execute(ctx, () => done = true);
                while (!done && !_stopRequested)
                    yield return null;
                if (_stopRequested) break;
                if (cmd is CheckpointCommand cp)
                {
                    string name = cp.GetCheckpointName(ctx);
                    if (!string.IsNullOrEmpty(name))
                        ctx.CheckpointIndices[name] = index;
                }
                while (_isPaused && !_stopRequested)
                    yield return null;
                if (_stopRequested) break;
                index = ctx.NextIndex ?? (index + 1);
            }
            // Store last run duration so it stays visible after stop
            float totalPaused = _totalPausedDuration + (_isPaused ? (Time.realtimeSinceStartup - _pauseStartTime) : 0f);
            _lastRunElapsedSeconds = (Time.realtimeSinceStartup - _timelineStartTime) - totalPaused;
            _isRunning = false;
            _runningIndex = -1;
            _stopRequested = false;
            _isPaused = false;
            if (_runContext != null)
            {
                _runContext.PendingConfirmCallback = null;
                _runContext.PendingResolveCallback = null;
                _runContext.PendingRetryCallback = null;
                _runContext = null;
            }
        }

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
                string json = BuildTimelineJson(entries, variables);
                File.WriteAllText(_persistPath, json);
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Save timeline failed: {ex.Message}");
            }
        }

        private static string EscapeJsonString(string? s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private const char SavedVariablesListSeparator = '\u0002';

        private static string BuildTimelineJson(SavedTimelineEntry[] entries, List<(string name, string type, string value)>? variables = null)
        {
            var sb = new StringBuilder();
            sb.Append("{\"entries\":[");
            for (int i = 0; i < entries.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var e = entries[i];
                sb.Append("{\"typeId\":\"").Append(EscapeJsonString(e.typeId))
                    .Append("\",\"payload\":\"").Append(EscapeJsonString(e.payload))
                    .Append("\",\"enabled\":").Append(e.enabled ? "true" : "false").Append('}');
            }
            sb.Append("]");
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
            foreach (var (n, listVal) in store.GetAllLists())
                list.Add((n, "list", string.Join(SavedVariablesListSeparator.ToString(), listVal ?? new List<string>())));
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
                HS2SandboxPlugin.Log.LogError($"Load timeline failed: {ex.Message}");
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
            }
            List<SavedTimelineEntry>? entries = null;
            if (TryParseTimelineJson(json, out entries) && entries != null && entries.Count > 0)
            {
                foreach (SavedTimelineEntry e in entries)
                {
                    try
                    {
                        string typeId = e.typeId;
                        string payload = e.payload ?? "";
                        MigrateLegacyCommand(ref typeId, ref payload);
                        var cmd = TimelineCommandFactory.Create(typeId);
                        cmd.DeserializePayload(payload);
                        cmd.Enabled = e.enabled;
                        _commands.Add(cmd);
                    }
                    catch (Exception ex)
                    {
                        HS2SandboxPlugin.Log.LogWarning($"Load command {e.typeId} failed: {ex.Message}");
                    }
                }
                ApplySavedVariables(json, replace);
                return;
            }
            var wrapper = JsonUtility.FromJson<SavedTimelineWrapper>(json);
            if (wrapper?.entries != null && wrapper.entries.Length > 0)
            {
                foreach (SavedTimelineEntry e in wrapper.entries)
                {
                    try
                    {
                        string typeId = e.typeId;
                        string payload = e.payload ?? "";
                        MigrateLegacyCommand(ref typeId, ref payload);
                        var cmd = TimelineCommandFactory.Create(typeId);
                        cmd.DeserializePayload(payload);
                        cmd.Enabled = e.enabled;
                        _commands.Add(cmd);
                    }
                    catch (Exception ex)
                    {
                        HS2SandboxPlugin.Log.LogWarning($"Load command {e.typeId} failed: {ex.Message}");
                    }
                }
            }
            ApplySavedVariables(json, replace);
        }

        private void ApplySavedVariables(string json, bool replace)
        {
            if (!TryParseVariablesJson(json, out List<(string name, string type, string value)>? variables) || variables == null || variables.Count == 0)
                return;
            foreach (var (name, type, value) in variables)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                string n = name.Trim();
                if (!replace)
                {
                    if (_designTimeVariables.HasString(n) || _designTimeVariables.HasInt(n) || _designTimeVariables.HasList(n))
                        continue;
                }
                if (type == "string")
                    _designTimeVariables.SetString(n, value ?? "");
                else if (type == "int")
                    _designTimeVariables.SetInt(n, int.TryParse(value, out int iv) ? iv : 0);
                else if (type == "list")
                {
                    var list = new List<string>();
                    if (!string.IsNullOrEmpty(value))
                    {
                        foreach (string part in value.Split(SavedVariablesListSeparator))
                            list.Add(part ?? "");
                    }
                    _designTimeVariables.SetList(n, list);
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
                int objEnd = IndexOfMatchingBrace(json, objStart);
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
            if (!TryParseJsonStringValue(obj, "name", out name)) return false;
            if (!TryParseJsonStringValue(obj, "type", out type)) type = "string";
            if (!TryParseJsonStringValue(obj, "value", out value)) value = "";
            return true;
        }

        /// <summary>
        /// Parses our timeline JSON format {"entries":[{...},{...}]} without relying on JsonUtility (which fails on arrays).
        /// </summary>
        private static bool TryParseTimelineJson(string json, out List<SavedTimelineEntry>? entries)
        {
            entries = null;
            int i = json.IndexOf("\"entries\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return false;
            i = json.IndexOf('[', i);
            if (i < 0) return false;
            entries = new List<SavedTimelineEntry>();
            i++;
            while (i < json.Length)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart < 0) break;
                int objEnd = IndexOfMatchingBrace(json, objStart);
                if (objEnd < 0) break;
                string obj = json.Substring(objStart, objEnd - objStart + 1);
                if (TryParseEntry(obj, out SavedTimelineEntry? entry) && entry != null)
                    entries.Add(entry);
                i = objEnd + 1;
                int next = json.IndexOf('{', i);
                if (next < 0) break;
                i = next;
            }
            return entries.Count >= 0;
        }

        private static int IndexOfMatchingBrace(string s, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            char quote = '\0';
            for (int i = openIndex; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (escape) { escape = false; continue; }
                    if (c == '\\') { escape = true; continue; }
                    if (c == quote) { inString = false; continue; }
                    continue;
                }
                if (c == '"' || c == '\'') { inString = true; quote = c; continue; }
                if (c == '{') { depth++; continue; }
                if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static bool TryParseEntry(string obj, out SavedTimelineEntry? entry)
        {
            entry = null;
            if (!TryParseJsonStringValue(obj, "typeId", out string? typeId)) typeId = "";
            if (!TryParseJsonStringValue(obj, "payload", out string? payload)) payload = "";
            if (!TryParseJsonBoolValue(obj, "enabled", out bool enabled)) enabled = true;
            entry = new SavedTimelineEntry { typeId = typeId ?? "", payload = payload ?? "", enabled = enabled };
            return true;
        }

        private static bool TryParseJsonStringValue(string json, string key, out string? value)
        {
            value = null;
            string keyPattern = "\"" + key + "\"";
            int ki = json.IndexOf(keyPattern, StringComparison.OrdinalIgnoreCase);
            if (ki < 0) return false;
            int colon = json.IndexOf(':', ki);
            if (colon < 0) return false;
            int start = json.IndexOf('"', colon);
            if (start < 0) return false;
            start++;
            var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    if (next == '"') { sb.Append('"'); i++; continue; }
                    if (next == '\\') { sb.Append('\\'); i++; continue; }
                    if (next == 'n') { sb.Append('\n'); i++; continue; }
                    if (next == 'r') { sb.Append('\r'); i++; continue; }
                    if (next == 't') { sb.Append('\t'); i++; continue; }
                    if (next == 'u' && i + 5 < json.Length)
                    {
                        string hex = json.Substring(i + 2, 4);
                        if (hex.Length == 4 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                        {
                            sb.Append((char)codePoint);
                            i += 5;
                            continue;
                        }
                    }
                }
                if (c == '"') break;
                sb.Append(c);
            }
            value = sb.ToString();
            return true;
        }

        private static bool TryParseJsonBoolValue(string json, string key, out bool value)
        {
            value = true;
            string keyPattern = "\"" + key + "\"";
            int ki = json.IndexOf(keyPattern, StringComparison.OrdinalIgnoreCase);
            if (ki < 0) return false;
            int colon = json.IndexOf(':', ki);
            if (colon < 0) return false;
            colon++;
            while (colon < json.Length && char.IsWhiteSpace(json[colon])) colon++;
            if (colon + 4 <= json.Length && string.Compare(json, colon, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            { value = true; return true; }
            if (colon + 5 <= json.Length && string.Compare(json, colon, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            { value = false; return true; }
            return false;
        }

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
                string json = BuildTimelineJson(entries, variables);
                if (!savePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    savePath += ".json";
                File.WriteAllText(savePath, json);
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Save timeline failed: {ex.Message}");
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
                HS2SandboxPlugin.Log.LogError($"Load timeline failed: {ex.Message}");
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
                HS2SandboxPlugin.Log.LogError($"Import timeline failed: {ex.Message}");
            }
        }
    }
}


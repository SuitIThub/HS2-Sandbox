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
        private readonly List<TimelineCommand> _commands = new List<TimelineCommand>();
        private Vector2 _scrollPosition;
        private bool _isRunning;
        private bool _stopRequested;
        private bool _isPaused;
        private int _runningIndex = -1;
        private TimelineContext? _runContext;
        private bool _showMousePositions;
        private TimelineCommand? _recordingMouseFor;
        private CopyScriptApiClient? _apiClient;

        private static readonly Color[] CrossColors =
        {
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 0.8f, 0.3f),
            new Color(0.3f, 0.5f, 1f),
            new Color(1f, 0.85f, 0.2f),
            new Color(0.2f, 0.9f, 0.9f),
            new Color(1f, 0.4f, 0.8f),
            new Color(1f, 0.6f, 0.2f),
            new Color(0.7f, 0.4f, 1f)
        };
        private string _persistPath = "";

        protected override void Start()
        {
            base.Start();
            windowID = 2002;
            windowTitle = "Action Timeline";
            windowRect = new Rect(400, 350, 430, 420);
            _persistPath = Path.Combine(Paths.ConfigPath, "com.hs2.sandbox", "timeline.json");
            _apiClient = new CopyScriptApiClient();
            LoadTimeline();
        }

        private const float WindowMinWidth = 430f;
        private const float WindowMaxWidth = 630f;
        private const float WindowMinHeight = 280f;
        private const float WindowMaxHeight = 1300f;
        private const float WindowContentOverhead = 270f;

        private void OnDisable()
        {
            SaveTimeline();
        }

        public override void DrawWindow()
        {
            if (isVisible)
            {
                float contentHeight = WindowContentOverhead + _commands.Count * ListRowHeight;
                float desiredHeight = Mathf.Clamp(contentHeight, WindowMinHeight, WindowMaxHeight);
                windowRect.height = desiredHeight;
                windowRect = GUILayout.Window(windowID, windowRect, DrawWindowContent, windowTitle,
                    GUILayout.MinHeight(desiredHeight), GUILayout.MaxHeight(WindowMaxHeight));
                windowRect.width = Mathf.Clamp(windowRect.width, WindowMinWidth, WindowMaxWidth);
                // Keep window at desired height so scroll only appears when content exceeds 1300px
                windowRect.height = Mathf.Clamp(Mathf.Max(windowRect.height, desiredHeight), WindowMinHeight, WindowMaxHeight);
            }
        }

        private const float ListRowHeight = 24f;

        private void Update()
        {
            if (_isRunning && _runningIndex >= 0 && _runningIndex < _commands.Count)
            {
                float listViewHeight = Mathf.Max(100f, windowRect.height - 200f);
                float rowY = _runningIndex * ListRowHeight;
                if (rowY < _scrollPosition.y)
                    _scrollPosition.y = Mathf.Max(0, rowY);
                else if (rowY + ListRowHeight > _scrollPosition.y + listViewHeight)
                    _scrollPosition.y = Mathf.Max(0, Mathf.Min(_commands.Count * ListRowHeight - listViewHeight, rowY + ListRowHeight - listViewHeight));
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
            }
        }

        protected override void DrawWindowContent(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // Add command toolbar (two rows)
            float btnW = 74f;
            float btnH = 28f;
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            if (GUILayout.Button("Key", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("simulate_key");
            GUILayout.Space(2);
            if (GUILayout.Button("Mouse", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("simulate_mouse");
            GUILayout.Space(2);
            if (GUILayout.Button("Pause", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("pause");
            GUILayout.Space(2);
            if (GUILayout.Button("Wait SS", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("wait_screenshot");
            GUILayout.Space(2);
            if (GUILayout.Button("Check", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("checkpoint");
            GUILayout.Space(2);
            if (GUILayout.Button("Jump", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("jump");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            if (GUILayout.Button("Loop", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("loop");
            GUILayout.Space(2);
            if (GUILayout.Button("Confirm", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("confirm");
            GUILayout.Space(2);
            if (GUILayout.Button("Wait 0", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("wait_empty_screenshots");
            GUILayout.Space(2);
            if (GUILayout.Button("Resolve", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("resolve_on_issue");
            GUILayout.Space(2);
            if (GUILayout.Button("Resolve #", GUILayout.Width(btnW), GUILayout.Height(btnH))) AddCommand("resolve_on_count");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Control buttons (Clear, Export, Import, Start/Stop)
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            if (GUILayout.Button("Clear", GUILayout.Width(58), GUILayout.Height(26)))
            {
                _commands.Clear();
                SaveTimeline();
            }
            GUILayout.Space(4);
            if (GUILayout.Button("Export", GUILayout.Width(58), GUILayout.Height(26)))
                ExportTimeline();
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
                        _isPaused = false;
                }
                else if (GUILayout.Button("Pause", GUILayout.Width(58), GUILayout.Height(26)))
                    _isPaused = true;
            }
            else if (GUILayout.Button("Start", GUILayout.Width(58), GUILayout.Height(26)))
                StartTimeline();
            GUILayout.Space(4);
            bool showCrosses = GUILayout.Toggle(_showMousePositions, "Crosses", GUILayout.Width(62), GUILayout.Height(26));
            if (showCrosses != _showMousePositions)
                _showMousePositions = showCrosses;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // List
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var ctx = new InlineDrawContext();
            int mouseCommandColorIndex = 0;
            for (int i = 0; i < _commands.Count; i++)
            {
                TimelineCommand cmd = _commands[i];
                bool isCurrent = _isRunning && i == _runningIndex;
                int? mouseColorIndex = null;
                if (cmd is SimulateMouseCommand mouseCmd && mouseCmd.HasValue)
                {
                    mouseColorIndex = mouseCommandColorIndex++;
                }
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (!_isRunning)
                {
                    bool newEnabled = GUILayout.Toggle(cmd.Enabled, "", GUILayout.Width(18));
                    if (newEnabled != cmd.Enabled)
                    {
                        cmd.Enabled = newEnabled;
                        SaveTimeline();
                    }
                }
                else
                    GUILayout.Toggle(cmd.Enabled, "", GUILayout.Width(18));
                GUILayout.Label($"{i + 1}.", GUILayout.Width(22));
                if (!_isRunning)
                {
                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = i > 0;
                    if (GUILayout.Button("\u25b2", GUILayout.Width(20)))
                    {
                        (_commands[i], _commands[i - 1]) = (_commands[i - 1], _commands[i]);
                        SaveTimeline();
                    }
                    GUI.enabled = i < _commands.Count - 1;
                    if (GUILayout.Button("\u25bc", GUILayout.Width(20)))
                    {
                        (_commands[i], _commands[i + 1]) = (_commands[i + 1], _commands[i]);
                        SaveTimeline();
                    }
                    GUI.enabled = prevEnabled;
                }
                string label = cmd.GetDisplayLabel(_runContext);
                if (isCurrent)
                {
                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = new Color(0.2f, 0.7f, 0.2f);
                    GUILayout.Label(label, GUILayout.Width(120));
                    GUI.contentColor = prevContent;
                }
                else if (_showMousePositions && mouseColorIndex.HasValue && mouseColorIndex.Value < CrossColors.Length)
                {
                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = CrossColors[mouseColorIndex.Value];
                    GUILayout.Label(label, GUILayout.Width(120));
                    GUI.contentColor = prevContent;
                }
                else
                    GUILayout.Label(label, GUILayout.Width(120));
                if (_isRunning && isCurrent && cmd is ConfirmCommand && _runContext?.PendingConfirmCallback != null)
                {
                    if (GUILayout.Button("Confirm", GUILayout.Width(60)))
                    {
                        var cb = _runContext.PendingConfirmCallback;
                        _runContext.PendingConfirmCallback = null;
                        cb?.Invoke();
                    }
                }
                if (_isRunning && isCurrent && (cmd is ResolveOnIssueCommand || cmd is ResolveOnCountCommand) && _runContext?.PendingResolveCallback != null)
                {
                    if (GUILayout.Button("Resolve", GUILayout.Width(60)))
                    {
                        var cb = _runContext.PendingResolveCallback;
                        _runContext.PendingResolveCallback = null;
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
                GUILayout.EndHorizontal();
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
                if (!(cmd is SimulateMouseCommand mouseCmd) || !mouseCmd.HasValue) continue;
                if (colorIndex >= CrossColors.Length) break;
                int x = mouseCmd.ScreenX;
                int y = mouseCmd.ScreenY;
                GUI.color = CrossColors[colorIndex];
                GUI.DrawTexture(new Rect(x - crossHalf, y - crossThick / 2, crossHalf * 2, crossThick), tex);
                GUI.DrawTexture(new Rect(x - crossThick / 2, y - crossHalf, crossThick, crossHalf * 2), tex);
                colorIndex++;
            }
            GUI.color = prevColor;
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

        private void StartTimeline()
        {
            if (_commands.Count == 0) return;
            _isRunning = true;
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
            for (int i = 0; i < _commands.Count; i++)
                if (_commands[i] is CheckpointCommand cp && !string.IsNullOrWhiteSpace(cp.Name))
                    ctx.CheckpointIndices[cp.Name.Trim()] = i;

            if (_apiClient != null)
            {
                TrackedFilesResponse? initialResult = null;
                yield return _apiClient.GetTrackedFilesAsync(1, r => initialResult = r);
                if (initialResult != null && initialResult.success && initialResult.returned_count >= 1 && initialResult.files != null && initialResult.files.Length >= 1)
                    ctx.LastScreenshotName = initialResult.files[0].original_name ?? "";
            }

            int index = 0;
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
                while (_isPaused && !_stopRequested)
                    yield return null;
                if (_stopRequested) break;
                index = ctx.NextIndex ?? (index + 1);
            }
            _isRunning = false;
            _runningIndex = -1;
            _stopRequested = false;
            _isPaused = false;
            if (_runContext != null)
            {
                _runContext.PendingConfirmCallback = null;
                _runContext.PendingResolveCallback = null;
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
                string json = BuildTimelineJson(entries);
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

        private static string BuildTimelineJson(SavedTimelineEntry[] entries)
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
            sb.Append("]}");
            return sb.ToString();
        }

        private void LoadTimeline()
        {
            try
            {
                if (!File.Exists(_persistPath)) return;
                string json = File.ReadAllText(_persistPath);
                LoadTimelineFromJson(json);
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Load timeline failed: {ex.Message}");
            }
        }

        private void LoadTimelineFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            List<SavedTimelineEntry>? entries = null;
            if (TryParseTimelineJson(json, out entries) && entries != null && entries.Count > 0)
            {
                _commands.Clear();
                foreach (SavedTimelineEntry e in entries)
                {
                    try
                    {
                        var cmd = TimelineCommandFactory.Create(e.typeId);
                        cmd.DeserializePayload(e.payload ?? "");
                        cmd.Enabled = e.enabled;
                        _commands.Add(cmd);
                    }
                    catch (Exception ex)
                    {
                        HS2SandboxPlugin.Log.LogWarning($"Load command {e.typeId} failed: {ex.Message}");
                    }
                }
                return;
            }
            var wrapper = JsonUtility.FromJson<SavedTimelineWrapper>(json);
            if (wrapper?.entries != null && wrapper.entries.Length > 0)
            {
                _commands.Clear();
                foreach (SavedTimelineEntry e in wrapper.entries)
                {
                    try
                    {
                        var cmd = TimelineCommandFactory.Create(e.typeId);
                        cmd.DeserializePayload(e.payload ?? "");
                        cmd.Enabled = e.enabled;
                        _commands.Add(cmd);
                    }
                    catch (Exception ex)
                    {
                        HS2SandboxPlugin.Log.LogWarning($"Load command {e.typeId} failed: {ex.Message}");
                    }
                }
            }
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

        private void ExportTimeline()
        {
            if (_isRunning) return;
            string? path = NativeFileDialog.SaveFile("Export Timeline", "json", "JSON files (*.json)\0*.json\0All files (*.*)\0*.*\0");
            if (string.IsNullOrEmpty(path)) return;
            string exportPath = path!;
            try
            {
                var entries = new SavedTimelineEntry[_commands.Count];
                for (int i = 0; i < _commands.Count; i++)
                    entries[i] = new SavedTimelineEntry { typeId = _commands[i].TypeId, payload = _commands[i].SerializePayload(), enabled = _commands[i].Enabled };
                string json = BuildTimelineJson(entries);
                if (!exportPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    exportPath += ".json";
                File.WriteAllText(exportPath, json);
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Export timeline failed: {ex.Message}");
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
                LoadTimelineFromJson(json);
                SaveTimeline();
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Import timeline failed: {ex.Message}");
            }
        }
    }
}


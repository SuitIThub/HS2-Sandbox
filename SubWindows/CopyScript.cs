using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using KKAPI.Utilities;

namespace HS2SandboxPlugin
{
    public class CopyScript : SubWindow
    {
        private CopyScriptApiClient? apiClient;
        private string sourcePath = string.Empty;
        private string destinationPath = string.Empty;
        private string namePattern = string.Empty;
        private TrackedFile[] trackedFiles = [];
        private CopyScriptRule[] rules = [];
        private string[] ruleTagKeys = []; // tag_name used for API URL (current on server)
        private bool showAddRuleUI = false;
        private string newRuleTagName = "";
        private int newRuleTypeIndex = 0; // 0=counter, 1=list, 2=batch
        private bool isTracking = false;
        private bool isLoading = false;
        private bool apiAvailable = false;
        private string statusMessage = string.Empty;
        private float lastRefreshTime = 0f;
        private const float REFRESH_INTERVAL = 2f; // Refresh every 2 seconds
        private float lastHealthCheckTime = 0f;
        private const float HEALTHCHECK_INTERVAL = 5f; // Health check every 5 seconds when unavailable
        private bool isRefreshingTrackedFiles = false;
        private float connectionAttemptStartTime = 0f;
        private bool gaveUpOnConnection = false;
        private const float CONNECTION_TIMEOUT = 120f; // Give up auto-reconnect after 2 minutes

        private const float WindowMinWidth = 506f;
        private const float WindowMaxWidth = 706f;

        // List editor window state
        private bool _listEditorOpen;
        private string _listEditorText = "";
        private Action<string[]>? _listEditorOnApply;
        private Rect _listEditorWindowRect;
        private const int ListEditorWindowID = 2007;

        public override void DrawWindow()
        {
            base.DrawWindow();
            windowRect.width = Mathf.Clamp(windowRect.width, WindowMinWidth, WindowMaxWidth);

            if (_listEditorOpen && isVisible)
            {
                float margin = 8f;
                _listEditorWindowRect = GUILayout.Window(ListEditorWindowID, _listEditorWindowRect, DrawListEditorWindowContent, "Edit list",
                    GUILayout.MinWidth(300f), GUILayout.MinHeight(200f), GUILayout.MaxHeight(500f));
                _listEditorWindowRect.x = Mathf.Clamp(_listEditorWindowRect.x, margin, Mathf.Max(margin, Screen.width - _listEditorWindowRect.width - margin));
                _listEditorWindowRect.y = Mathf.Clamp(_listEditorWindowRect.y, margin, Mathf.Max(margin, Screen.height - _listEditorWindowRect.height - margin));
            }
        }

        protected override void Start()
        {
            base.Start();
            windowID = 2001;
            windowTitle = "CopyScript Control";
            windowRect = new Rect(400, 100, WindowMaxWidth, 480);
            
            apiClient = new CopyScriptApiClient();
            if (apiClient != null)
            {
                StartCoroutine(InitializeAsync());
            }
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            base.OnVisibilityChanged(visible);
            if (visible && apiAvailable && apiClient != null && !isRefreshingTrackedFiles)
            {
                StartCoroutine(RefreshTrackedFilesAsync());
            }
        }

        private IEnumerator InitializeAsync()
        {
            if (apiClient == null) yield break;

            isLoading = true;
            connectionAttemptStartTime = Time.time;
            gaveUpOnConnection = false;

            bool healthOk = false;
            yield return StartCoroutine(apiClient.HealthCheckAsync(result => { healthOk = result; }));

            apiAvailable = healthOk;

            if (healthOk)
            {
                yield return StartCoroutine(RefreshTrackedFilesAsync());
            }
            else
            {
                statusMessage = "Trying to connect to CopyScript API...";
            }

            isLoading = false;
        }

        private IEnumerator LoadSettingsAsync()
        {
            if (apiClient == null) yield break;
            StatusResponse? statusResult = null;
            yield return StartCoroutine(apiClient.GetStatusAsync((result) => { statusResult = result; }));
            
            if (statusResult != null)
            {
                sourcePath = statusResult.source_path ?? string.Empty;
                destinationPath = statusResult.destination_path ?? string.Empty;
                namePattern = statusResult.name_pattern ?? string.Empty;
                isTracking = statusResult.is_tracking;
            }
        }

        private IEnumerator LoadRulesAsync()
        {
            if (apiClient == null) yield break;
            RulesListResponse? result = null;
            yield return StartCoroutine(apiClient.GetRulesAsync((r) => { result = r; }));
            if (result != null && result.success && result.rules != null)
            {
                rules = result.rules;
                ruleTagKeys = new string[rules.Length];
                for (int i = 0; i < rules.Length; i++)
                    ruleTagKeys[i] = rules[i].tag_name ?? "";
            }
        }

        private IEnumerator RefreshTrackedFilesAsync()
        {
            if (apiClient == null || isRefreshingTrackedFiles) yield break;
            isRefreshingTrackedFiles = true;
            try
            {
                yield return StartCoroutine(FetchTrackedFilesOnlyAsync());
            }
            finally
            {
                isRefreshingTrackedFiles = false;
            }
        }

        /// <summary>Fetches only tracked files from the API (no rules/settings).</summary>
        private IEnumerator FetchTrackedFilesOnlyAsync()
        {
            if (apiClient == null) yield break;
            TrackedFilesResponse? filesResult = null;
            yield return StartCoroutine(apiClient.GetTrackedFilesAsync(5, (result) => { filesResult = result; }));
            if (filesResult != null && filesResult.success)
            {
                trackedFiles = filesResult.files ?? [];
            }
        }

        /// <summary>Called from timeline when a config command succeeds. Fetches rules, settings, and tracked files if the window is open.</summary>
        public void RefreshFromTimeline()
        {
            if (isVisible && apiClient != null && !isRefreshingTrackedFiles)
                StartCoroutine(RefreshRulesSettingsAndFilesAsync());
        }

        /// <summary>Fetches rules, settings, and tracked files. Called only when the user presses the Refresh button.</summary>
        private IEnumerator RefreshRulesSettingsAndFilesAsync()
        {
            if (apiClient == null || isRefreshingTrackedFiles) yield break;
            isRefreshingTrackedFiles = true;
            try
            {
                yield return StartCoroutine(LoadSettingsAsync());
                yield return StartCoroutine(LoadRulesAsync());
                yield return StartCoroutine(FetchTrackedFilesOnlyAsync());
            }
            finally
            {
                isRefreshingTrackedFiles = false;
            }
        }

        private void Update()
        {
            if (isLoading)
                return;

            // When API is available, periodically refresh tracked files (skip if a refresh is already in progress)
            if (apiAvailable && !isRefreshingTrackedFiles)
            {
                if (Time.time - lastRefreshTime > REFRESH_INTERVAL)
                {
                    lastRefreshTime = Time.time;
                    StartCoroutine(RefreshTrackedFilesAsync());
                }
            }
            // When API is not available and we haven't given up yet, periodically try a lightweight health check
            else if (!gaveUpOnConnection)
            {
                // Check if we've exceeded the 2 minute timeout
                if (Time.time - connectionAttemptStartTime > CONNECTION_TIMEOUT)
                {
                    gaveUpOnConnection = true;
                    statusMessage = "CopyScript API is not reachable. Please start the CopyScript manager and use 'Try reconnect'.";
                    HS2SandboxPlugin.Log.LogError("Failed to connect to CopyScript API after 2 minutes. Manual reconnect required.");
                }
                else if (Time.time - lastHealthCheckTime > HEALTHCHECK_INTERVAL)
                {
                    lastHealthCheckTime = Time.time;
                    StartCoroutine(HealthCheckOnlyAsync());
                }
            }
        }

        protected override void DrawWindowContent(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            // If the API is not reachable, show an info message and the reconnect/start buttons
            if (!apiAvailable)
            {
                GUILayout.Label("CopyScript manager is not running or the API cannot be reached.", GUILayout.Height(40));

                GUILayout.Space(10);

                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Try reconnect", GUILayout.Height(25), GUILayout.ExpandWidth(true)))
                {
                    StartCoroutine(TryReconnectAsync());
                }
                if (GUILayout.Button("Start CopyScript", GUILayout.Height(25), GUILayout.ExpandWidth(true)))
                {
                    StartCopyScriptProcess();
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    GUILayout.Label(statusMessage, GUILayout.Height(20));
                }

                GUILayout.Space(3);

                if (GUILayout.Button("Close", GUILayout.Height(22)))
                {
                    SetVisible(false);
                    HS2SandboxPlugin._copyToolbarToggle.Value = false;
                    var sandboxGUI = UnityEngine.Object.FindObjectOfType<SandboxGUI>();
                    if (sandboxGUI != null)
                    {
                        sandboxGUI.SetSubwindowState("Window1", false);
                    }
                }

                GUILayout.EndVertical();

                // Make window draggable and prevent mouse passthrough
                GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
                IMGUIUtils.EatInputInRect(windowRect);
                return;
            }

            // Row 1: Source Folder
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Source:", GUILayout.Width(70));
            sourcePath = GUILayout.TextField(sourcePath, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Set", GUILayout.Width(45), GUILayout.Height(20)))
            {
                StartCoroutine(SetSourcePathAsync());
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // Row 2: Destination Folder
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Dest:", GUILayout.Width(70));
            destinationPath = GUILayout.TextField(destinationPath, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Set", GUILayout.Width(45), GUILayout.Height(20)))
            {
                StartCoroutine(SetDestinationPathAsync());
            }
            // Show Create Folder button if path doesn't exist
            if (!string.IsNullOrEmpty(destinationPath) && apiClient != null && !apiClient.DirectoryExists(destinationPath))
            {
                if (GUILayout.Button("Create", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    if (apiClient != null && apiClient.CreateDirectory(destinationPath))
                    {
                        statusMessage = "Directory created successfully";
                    }
                    else
                    {
                        statusMessage = "Failed to create directory";
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // Row 3: Naming Pattern
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Pattern:", GUILayout.Width(70));
            namePattern = GUILayout.TextField(namePattern, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Set", GUILayout.Width(45), GUILayout.Height(20)))
            {
                StartCoroutine(SetNamePatternAsync());
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Row 4: Buttons
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Start", GUILayout.Height(25), GUILayout.ExpandWidth(true)))
            {
                StartCoroutine(StartTrackingAsync());
            }
            if (GUILayout.Button("Stop", GUILayout.Height(25), GUILayout.ExpandWidth(true)))
            {
                StartCoroutine(StopTrackingAsync());
            }
            if (GUILayout.Button("Copy", GUILayout.Height(25), GUILayout.ExpandWidth(true)))
            {
                StartCoroutine(CopyAndRenameAsync());
            }
            if (GUILayout.Button("Clear", GUILayout.Height(25), GUILayout.ExpandWidth(true)))
            {
                StartCoroutine(ClearTrackedFilesAsync());
            }
            GUILayout.EndHorizontal();

            // Tracking status
            if (isTracking)
            {
                GUI.color = Color.green;
                GUILayout.Label("● Active", GUILayout.Height(20));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.gray;
                GUILayout.Label("○ Inactive", GUILayout.Height(20));
                GUI.color = Color.white;
            }

            GUILayout.Space(3);

            // Rules section (above tracked files)
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Rules:", GUILayout.Height(20));
            if (GUILayout.Button("Add rule", GUILayout.Width(70), GUILayout.Height(20)))
            {
                showAddRuleUI = !showAddRuleUI;
                if (!showAddRuleUI) newRuleTagName = "";
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical("box");
            if (rules != null && rules.Length > 0)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    DrawRuleRow(i);
                }
            }
            else if (!showAddRuleUI)
            {
                GUILayout.Label("No rules", GUILayout.Height(18));
            }
            if (showAddRuleUI)
            {
                DrawAddRuleRow();
            }
            GUILayout.EndVertical();

            GUILayout.Space(3);

            // Status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Label(statusMessage, GUILayout.Height(20));
            }

            GUILayout.Space(3);

            // Rows 5-9: Last 5 tracked files
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Last 5 Files:", GUILayout.Height(20));
            if (GUILayout.Button("Refresh", GUILayout.Width(60), GUILayout.Height(20)) && !isRefreshingTrackedFiles && apiClient != null)
            {
                StartCoroutine(RefreshRulesSettingsAndFilesAsync());
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical("box");
            
            if (trackedFiles != null && trackedFiles.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(5, trackedFiles.Length); i++)
                {
                    var file = trackedFiles[i];
                    if (file != null)
                    {
                        GUILayout.BeginHorizontal();
                        
                        // Color indicator based on state
                        Color originalColor = GUI.color;
                        if (file.state == "duplicate")
                        {
                            GUI.color = Color.red;
                        }
                        else if (file.state == "exists")
                        {
                            GUI.color = Color.blue;
                        }
                        else
                        {
                            GUI.color = Color.green;
                        }
                        
                        GUILayout.Label("●", GUILayout.Width(12), GUILayout.Height(20));
                        GUI.color = originalColor;
                        
                        // File name - truncate if too long
                        string displayName = !string.IsNullOrEmpty(file.new_name) ? file.new_name : file.original_name;
                        if (displayName.Length > 35)
                        {
                            displayName = displayName.Substring(0, 32) + "...";
                        }
                        GUILayout.Label(displayName, GUILayout.Height(20), GUILayout.ExpandWidth(true));
                        
                        GUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                GUILayout.Label("No tracked files", GUILayout.Height(20));
            }
            
            GUILayout.EndVertical();

            GUILayout.Space(3);

            if (GUILayout.Button("Close", GUILayout.Height(22)))
            {
                SetVisible(false);
                var sandboxGUI = FindObjectOfType<SandboxGUI>();
                if (sandboxGUI != null)
                {
                    sandboxGUI.SetSubwindowState("Window1", false);
                }
            }

            GUILayout.EndVertical();

            // Make window draggable and prevent mouse passthrough
            GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private IEnumerator SetSourcePathAsync()
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.SetSourcePathAsync(sourcePath, (result) => { success = result; }));
            
            if (success)
            {
                statusMessage = "Source path updated";
            }
            else
            {
                statusMessage = "Failed to update source path";
            }
            isLoading = false;
        }

        private IEnumerator SetDestinationPathAsync()
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.SetDestinationPathAsync(destinationPath, (result) => { success = result; }));
            
            if (success)
            {
                statusMessage = "Destination path updated";
            }
            else
            {
                statusMessage = "Failed to update destination path";
            }
            isLoading = false;
        }

        private IEnumerator SetNamePatternAsync()
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.SetNamePatternAsync(namePattern, (result) => { success = result; }));
            
            if (success)
            {
                statusMessage = "Naming pattern updated";
            }
            else
            {
                statusMessage = "Failed to update naming pattern";
            }
            isLoading = false;
        }

        private IEnumerator StartTrackingAsync()
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.StartTrackingAsync((result) => { success = result; }));
            
            if (success)
            {
                isTracking = true;
                statusMessage = "Tracking started";
                yield return StartCoroutine(RefreshRulesSettingsAndFilesAsync());
            }
            else
            {
                statusMessage = "Failed to start tracking";
            }
            isLoading = false;
        }

        private IEnumerator StopTrackingAsync()
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.StopTrackingAsync((result) => { success = result; }));
            
            if (success)
            {
                isTracking = false;
                statusMessage = "Tracking stopped";
                yield return StartCoroutine(RefreshRulesSettingsAndFilesAsync());
            }
            else
            {
                statusMessage = "Failed to stop tracking";
            }
            isLoading = false;
        }

        private IEnumerator CopyAndRenameAsync()
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.CopyAndRenameAsync((result) => { success = result; }));
            
            if (success)
            {
                statusMessage = "Copy and rename initiated";
                yield return StartCoroutine(RefreshRulesSettingsAndFilesAsync());
            }
            else
            {
                statusMessage = "Failed to copy and rename";
            }
            isLoading = false;
        }

        private IEnumerator ClearTrackedFilesAsync()
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.ClearTrackedFilesAsync((result) => { success = result; }));
            
            if (success)
            {
                statusMessage = "Tracked files cleared";
                yield return StartCoroutine(RefreshRulesSettingsAndFilesAsync());
            }
            else
            {
                statusMessage = "Failed to clear tracked files";
            }
            isLoading = false;
        }

        private IEnumerator HealthCheckOnlyAsync()
        {
            if (apiClient == null) yield break;

            bool healthOk = false;
            yield return StartCoroutine(apiClient.HealthCheckAsync(result => { healthOk = result; }));

            if (healthOk && !apiAvailable)
            {
                apiAvailable = true;
                gaveUpOnConnection = false;
                statusMessage = "Connection to CopyScript established.";
                yield return StartCoroutine(RefreshTrackedFilesAsync());
            }
            else if (!healthOk && !gaveUpOnConnection)
            {
                float elapsedSeconds = Time.time - connectionAttemptStartTime;
                int remainingSeconds = Mathf.Max(0, (int)(CONNECTION_TIMEOUT - elapsedSeconds));
                statusMessage = $"Trying to connect to CopyScript API... ({remainingSeconds}s remaining)";
            }
        }

        private IEnumerator TryReconnectAsync()
        {
            if (apiClient == null) yield break;

            isLoading = true;
            statusMessage = "Trying to reconnect to CopyScript API...";
            
            // Reset connection attempt tracking for manual reconnect
            connectionAttemptStartTime = Time.time;
            gaveUpOnConnection = false;

            bool healthOk = false;
            yield return StartCoroutine(apiClient.HealthCheckAsync(result => { healthOk = result; }));

            apiAvailable = healthOk;

            if (healthOk)
            {
                statusMessage = "Connection established.";
                yield return StartCoroutine(RefreshTrackedFilesAsync());
            }
            else
            {
                statusMessage = "Could not reach CopyScript API. Will keep trying for 2 minutes...";
            }

            isLoading = false;
        }

        private static readonly string[] RuleTypeOptions = ["counter", "list", "batch"];

        private void DrawAddRuleRow()
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Tag:", GUILayout.Width(28), GUILayout.Height(18));
            newRuleTagName = GUILayout.TextField(newRuleTagName, GUILayout.Width(120), GUILayout.Height(18));
            GUILayout.Label("Type:", GUILayout.Width(32), GUILayout.Height(18));
            newRuleTypeIndex = GUILayout.SelectionGrid(newRuleTypeIndex, RuleTypeOptions, 3, GUILayout.Width(180), GUILayout.Height(20));
            if (GUILayout.Button("Apply", GUILayout.Width(50), GUILayout.Height(20)))
            {
                string tag = (newRuleTagName ?? "").Trim();
                if (string.IsNullOrEmpty(tag))
                {
                    statusMessage = "Enter a tag name";
                }
                else
                {
                    StartCoroutine(CreateRuleAsync(tag, newRuleTypeIndex));
                }
            }
            GUILayout.EndHorizontal();
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
                var trimmed = new System.Collections.Generic.List<string>();
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

        private static string GetValuesPreview(string[]? values, int maxLength = 40)
        {
            if (values == null || values.Length == 0) return "(empty)";
            string joined = string.Join("; ", values);
            if (joined.Length <= maxLength) return joined;
            return joined.Substring(0, maxLength - 3) + "...";
        }

        private void DrawRuleRow(int i)
        {
            if (rules == null || i < 0 || i >= rules.Length) return;
            var rule = rules[i];
            string typeLabel = string.IsNullOrEmpty(rule.type) ? "?" : rule.type;
            string tagLabel = string.IsNullOrEmpty(rule.tag_name) ? "?" : "{" + rule.tag_name + "}";

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            GUILayout.Label(typeLabel, GUILayout.Width(52), GUILayout.Height(18));
            GUILayout.Label(tagLabel, GUILayout.Width(48), GUILayout.Height(18));

            if (rule.type == "list")
            {
                int ruleIndex = i;
                if (GUILayout.Button("Edit list...", GUILayout.Width(70), GUILayout.Height(18)))
                {
                    _listEditorText = rule.values != null ? string.Join("\n", rule.values) : "";
                    _listEditorOnApply = arr =>
                    {
                        if (ruleIndex >= 0 && ruleIndex < rules.Length)
                            rules[ruleIndex].values = arr ?? [];
                    };
                    _listEditorWindowRect = new Rect(windowRect.xMax + 10f, windowRect.yMin, 320f, 280f);
                    _listEditorOpen = true;
                }
                string preview = GetValuesPreview(rule.values, 30);
                GUILayout.Label(preview, GUILayout.ExpandWidth(true), GUILayout.Height(18));
                GUILayout.Label("Step", GUILayout.Width(28), GUILayout.Height(18));
                string stepStr = GUILayout.TextField(rule.step.ToString(), GUILayout.Width(28), GUILayout.Height(18));
                if (int.TryParse(stepStr, out int stepVal) && stepVal >= 0)
                    rule.step = stepVal;
            }
            else if (rule.type == "counter" || rule.type == "batch")
            {
                GUILayout.Label("Start", GUILayout.Width(32), GUILayout.Height(18));
                string startStr = GUILayout.TextField(rule.start_value.ToString(), GUILayout.Width(40), GUILayout.Height(18));
                if (int.TryParse(startStr, out int startVal))
                    rule.start_value = startVal;
                GUILayout.Label("Inc", GUILayout.Width(22), GUILayout.Height(18));
                string incStr = GUILayout.TextField(rule.increment.ToString(), GUILayout.Width(32), GUILayout.Height(18));
                if (int.TryParse(incStr, out int incVal))
                    rule.increment = incVal;
                GUILayout.Label("Step", GUILayout.Width(28), GUILayout.Height(18));
                string stepStr = GUILayout.TextField(rule.step.ToString(), GUILayout.Width(28), GUILayout.Height(18));
                if (int.TryParse(stepStr, out int stepVal) && stepVal >= 0)
                    rule.step = stepVal;
                rule.use_max_value = GUILayout.Toggle(rule.use_max_value, "Max", GUILayout.Width(28), GUILayout.Height(18));
                if (rule.use_max_value)
                {
                    string maxStr = GUILayout.TextField(rule.max_value.ToString(), GUILayout.Width(40), GUILayout.Height(18));
                    if (int.TryParse(maxStr, out int maxVal))
                        rule.max_value = maxVal;
                }
            }

            if (GUILayout.Button("Set", GUILayout.Width(36), GUILayout.Height(18)))
            {
                string urlTag = i < ruleTagKeys.Length ? ruleTagKeys[i] : rule.tag_name;
                if (!string.IsNullOrEmpty(urlTag))
                    StartCoroutine(SetRuleAsync(urlTag, rule));
            }

            GUILayout.EndHorizontal();
        }

        private IEnumerator CreateRuleAsync(string tagName, int typeIndex)
        {
            if (apiClient == null) yield break;
            if (typeIndex < 0 || typeIndex >= RuleTypeOptions.Length) yield break;

            var rule = new CopyScriptRule
            {
                tag_name = tagName,
                type = RuleTypeOptions[typeIndex]
            };
            if (rule.type == "list")
            {
                rule.values = [];
                rule.step = 1;
            }
            else
            {
                rule.start_value = 0;
                rule.increment = 1;
                rule.step = 1;
                rule.use_max_value = false;
            }

            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.CreateRuleAsync(rule, (result) => { success = result; }));
            if (success)
            {
                statusMessage = "Rule created";
                showAddRuleUI = false;
                newRuleTagName = "";
                yield return StartCoroutine(LoadRulesAsync());
            }
            else
            {
                statusMessage = "Failed to create rule (tag may already exist)";
            }
            isLoading = false;
        }

        private IEnumerator SetRuleAsync(string urlTagName, CopyScriptRule rule)
        {
            if (apiClient == null) yield break;
            isLoading = true;
            bool success = false;
            yield return StartCoroutine(apiClient.UpdateRuleAsync(urlTagName, rule, (result) => { success = result; }));
            if (success)
            {
                statusMessage = "Rule updated";
                yield return StartCoroutine(LoadRulesAsync());
            }
            else
            {
                statusMessage = "Failed to update rule";
            }
            isLoading = false;
        }

        private void StartCopyScriptProcess()
        {
            const string scriptPath = @"D:\Honey Select\UserData\CopyScript\run_file_manager.bat";

            try
            {
                if (!File.Exists(scriptPath))
                {
                    statusMessage = "CopyScript start file not found.";
                    HS2SandboxPlugin.Log.LogError($"CopyScript start file not found at path: {scriptPath}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? string.Empty
                };

                Process.Start(startInfo);
                
                // Reset connection tracking to start auto-retry for the new process
                connectionAttemptStartTime = Time.time;
                gaveUpOnConnection = false;
                statusMessage = "CopyScript start command executed. Attempting to connect...";
            }
            catch (Exception ex)
            {
                statusMessage = "Failed to start CopyScript script.";
                HS2SandboxPlugin.Log.LogError($"Failed to start CopyScript script: {ex}");
            }
        }

        private void OnDestroy()
        {
            // Cleanup if needed
        }
    }
}


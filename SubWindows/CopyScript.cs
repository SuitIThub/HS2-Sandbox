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
        private TrackedFile[] trackedFiles = new TrackedFile[0];
        private bool isTracking = false;
        private bool isLoading = false;
        private bool apiAvailable = false;
        private string statusMessage = string.Empty;
        private float lastRefreshTime = 0f;
        private const float REFRESH_INTERVAL = 2f; // Refresh every 2 seconds
        private float lastHealthCheckTime = 0f;
        private const float HEALTHCHECK_INTERVAL = 5f; // Health check every 5 seconds when unavailable
        private bool isRefreshingTrackedFiles = false;

        protected override void Start()
        {
            base.Start();
            windowID = 2001;
            windowTitle = "CopyScript Control";
            windowRect = new Rect(400, 100, 420, 480);
            
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

            bool healthOk = false;
            yield return StartCoroutine(apiClient.HealthCheckAsync(result => { healthOk = result; }));

            apiAvailable = healthOk;

            if (healthOk)
            {
                yield return StartCoroutine(LoadSettingsAsync());
                yield return StartCoroutine(RefreshTrackedFilesAsync());
            }
            else
            {
                statusMessage = "CopyScript API is not reachable. Please start the CopyScript manager.";
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

        private IEnumerator RefreshTrackedFilesAsync()
        {
            if (apiClient == null || isRefreshingTrackedFiles) yield break;
            isRefreshingTrackedFiles = true;
            try
            {
                TrackedFilesResponse? filesResult = null;
                yield return StartCoroutine(apiClient.GetTrackedFilesAsync(5, (result) => { filesResult = result; }));

                if (filesResult != null && filesResult.success)
                {
                    trackedFiles = filesResult.files ?? new TrackedFile[0];
                    if (filesResult.files == null && filesResult.total_count > 0)
                    {
                        HS2SandboxPlugin.Log.LogWarning("Tracked files response had success but null files array (total_count=" + filesResult.total_count + "). Check API response format.");
                    }
                }
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
            // When API is not available, periodically try a lightweight health check
            else
            {
                if (Time.time - lastHealthCheckTime > HEALTHCHECK_INTERVAL)
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
                StartCoroutine(RefreshTrackedFilesAsync());
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
                // Refresh tracked files immediately after starting tracking
                yield return StartCoroutine(RefreshTrackedFilesAsync());
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
                yield return StartCoroutine(RefreshTrackedFilesAsync());
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
                trackedFiles = new TrackedFile[0];
                statusMessage = "Tracked files cleared";
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
                statusMessage = "Connection to CopyScript established.";

                // Load initial settings and tracked files once the API becomes available
                yield return StartCoroutine(LoadSettingsAsync());
                yield return StartCoroutine(RefreshTrackedFilesAsync());
            }
        }

        private IEnumerator TryReconnectAsync()
        {
            if (apiClient == null) yield break;

            isLoading = true;
            statusMessage = "Trying to reconnect to CopyScript API...";

            bool healthOk = false;
            yield return StartCoroutine(apiClient.HealthCheckAsync(result => { healthOk = result; }));

            apiAvailable = healthOk;

            if (healthOk)
            {
                statusMessage = "Connection established.";
                yield return StartCoroutine(LoadSettingsAsync());
                yield return StartCoroutine(RefreshTrackedFilesAsync());
            }
            else
            {
                statusMessage = "Could not reach CopyScript API. Make sure the CopyScript manager is running.";
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
                statusMessage = "CopyScript start command executed. After it has started, click 'Try reconnect'.";
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


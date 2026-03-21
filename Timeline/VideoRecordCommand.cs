using System;
using System.Collections;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Starts a video recording using the VideoExport plugin and monitors progress until complete.
    /// Requires: [BepInPlugin("com.joan6694.illusionplugins.videoexport", "VideoExport", "2.0.3")]
    /// </summary>
    public class VideoRecordCommand : TimelineCommand
    {
        private const string VideoExportGuid = "com.joan6694.illusionplugins.videoexport";

        private static BaseUnityPlugin? _videoExportPlugin;
        private static System.Reflection.MethodInfo? _recordVideoMethod;
        private static System.Reflection.MethodInfo? _getProgressMethod;
        private static bool _pluginLookupDone;

        private string _statusText = "";
        private float _progressPercent;
        private long _elapsedMs;
        private long _remainingMs;
        private int _currentFrame;
        private int _totalFrames;
        private bool _isRecording;
        private bool _isGenerating;

        public override string TypeId => "video_record";

        public override string GetDisplayLabel() => "Video Record";

        public override bool HasInvalidConfiguration()
        {
            EnsurePluginLookup();
            return _videoExportPlugin == null;
        }

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            EnsurePluginLookup();
            if (_videoExportPlugin == null)
            {
                GUILayout.Label("VideoExport plugin not found!", GUILayout.ExpandWidth(true));
            }
            else if (_isRecording || _isGenerating)
            {
                var parts = new System.Collections.Generic.List<string>();
                parts.Add(_statusText);

                if (_totalFrames > 0)
                    parts.Add($"{_currentFrame}/{_totalFrames}");
                else if (_currentFrame > 0)
                    parts.Add($"f{_currentFrame}");

                if (_progressPercent > 0)
                    parts.Add($"{(_progressPercent * 100):F0}%");

                if (_elapsedMs > 0)
                    parts.Add($"[{FormatTime(_elapsedMs)}]");

                if (_remainingMs > 0)
                    parts.Add($"~{FormatTime(_remainingMs)}");

                GUILayout.Label(string.Join(" ", parts), GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.Label("Start recording (VideoExport)", GUILayout.ExpandWidth(true));
            }
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            EnsurePluginLookup();
            if (_videoExportPlugin == null || _recordVideoMethod == null || _getProgressMethod == null)
            {
                HS2SandboxPlugin.Log.LogError("VideoExport plugin not available");
                onComplete();
                return;
            }

            ctx.Runner.StartCoroutine(RecordAndMonitor(onComplete));
        }

        private IEnumerator RecordAndMonitor(Action onComplete)
        {
            _statusText = "Starting...";
            _progressPercent = 0;
            _elapsedMs = 0;
            _remainingMs = 0;
            _currentFrame = 0;
            _totalFrames = 0;
            _isRecording = false;
            _isGenerating = false;

            try
            {
                _recordVideoMethod!.Invoke(_videoExportPlugin, null);
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Failed to start recording: {ex.Message}");
                _statusText = "Start failed";
                onComplete();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);

            bool wasRecording = false;
            bool wasGenerating = false;

            while (true)
            {
                string? progressStr = null;
                try
                {
                    progressStr = _getProgressMethod!.Invoke(_videoExportPlugin, null) as string;
                }
                catch (Exception ex)
                {
                    HS2SandboxPlugin.Log.LogWarning($"GetRecordingProgress failed: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(progressStr))
                {
                    ParseProgress(progressStr!);
                }

                if (_isRecording)
                    wasRecording = true;
                if (_isGenerating)
                    wasGenerating = true;

                if (wasRecording && !_isRecording && !_isGenerating)
                {
                    _statusText = "Done";
                    break;
                }

                if (wasGenerating && !_isGenerating && !_isRecording)
                {
                    _statusText = "Done";
                    break;
                }

                yield return new WaitForSeconds(0.25f);
            }

            _isRecording = false;
            _isGenerating = false;
            onComplete();
        }

        private void ParseProgress(string progressStr)
        {
            string[] parts = progressStr.Split('|');
            if (parts.Length < 27) return;

            bool.TryParse(parts[0], out _isRecording);
            bool.TryParse(parts[1], out _isGenerating);
            int.TryParse(parts[3], out _currentFrame);
            int.TryParse(parts[4], out int totalFrames);
            _totalFrames = totalFrames > 0 ? totalFrames : 0;
            long.TryParse(parts[8], out _elapsedMs);
            long.TryParse(parts[9], out _remainingMs);
            float.TryParse(parts[10], out _progressPercent);

            string statusMsg = parts.Length > 26 ? parts[26].Replace("\\|", "|") : "";

            if (_isRecording)
            {
                _statusText = string.IsNullOrEmpty(statusMsg) ? "Recording" : TruncateStatus(statusMsg);
            }
            else if (_isGenerating)
            {
                _statusText = string.IsNullOrEmpty(statusMsg) ? "Encoding" : TruncateStatus(statusMsg);
            }
            else
            {
                _statusText = string.IsNullOrEmpty(statusMsg) ? "Idle" : TruncateStatus(statusMsg);
            }
        }

        private static string TruncateStatus(string s)
        {
            if (s.Length > 20)
                return s.Substring(0, 17) + "...";
            return s;
        }

        private static string FormatTime(long ms)
        {
            if (ms <= 0) return "";
            long totalSeconds = ms / 1000;
            long minutes = totalSeconds / 60;
            long seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:D2}";
        }

        private static void EnsurePluginLookup()
        {
            if (_pluginLookupDone) return;
            _pluginLookupDone = true;

            if (Chainloader.PluginInfos.TryGetValue(VideoExportGuid, out var pluginInfo))
            {
                _videoExportPlugin = pluginInfo.Instance;
                if (_videoExportPlugin != null)
                {
                    var type = _videoExportPlugin.GetType();
                    _recordVideoMethod = type.GetMethod("RecordVideo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    _getProgressMethod = type.GetMethod("GetRecordingProgress", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (_recordVideoMethod == null)
                        HS2SandboxPlugin.Log.LogWarning("VideoExport.RecordVideo method not found");
                    if (_getProgressMethod == null)
                        HS2SandboxPlugin.Log.LogWarning("VideoExport.GetRecordingProgress method not found");
                }
            }
            else
            {
                HS2SandboxPlugin.Log.LogInfo("VideoExport plugin not loaded");
            }
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}

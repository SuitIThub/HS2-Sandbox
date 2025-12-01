using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HS2SandboxPlugin
{
    public class CopyScriptApiClient
    {
        private readonly string _baseUrl = "http://127.0.0.1:5000";

        public CopyScriptApiClient()
        {
        }

        // Health check
        public IEnumerator HealthCheckAsync(Action<bool> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{_baseUrl}/api/health"))
            {
                yield return request.SendWebRequest();
                callback(!request.isNetworkError && !request.isHttpError);
            }
        }

        // Start tracking
        public IEnumerator StartTrackingAsync(Action<bool> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Post($"{_baseUrl}/api/tracking/start", ""))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    callback(result != null && result.success);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Start tracking failed: {request.error}");
                    callback(false);
                }
            }
        }

        // Stop tracking
        public IEnumerator StopTrackingAsync(Action<bool> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Post($"{_baseUrl}/api/tracking/stop", ""))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    callback(result != null && result.success);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Stop tracking failed: {request.error}");
                    callback(false);
                }
            }
        }

        // Get source path
        public IEnumerator GetSourcePathAsync(Action<string> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{_baseUrl}/api/source_path"))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<PathResponse>(request.downloadHandler.text);
                    callback(result?.path ?? string.Empty);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Get source path failed: {request.error}");
                    callback(string.Empty);
                }
            }
        }

        // Set source path
        public IEnumerator SetSourcePathAsync(string path, Action<bool> callback)
        {
            var json = JsonUtility.ToJson(new PathRequest { path = path });
            using (UnityWebRequest request = new UnityWebRequest($"{_baseUrl}/api/source_path", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    callback(result != null && result.success);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Set source path failed: {request.error}");
                    callback(false);
                }
            }
        }

        // Get destination path
        public IEnumerator GetDestinationPathAsync(Action<string> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{_baseUrl}/api/destination_path"))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<PathResponse>(request.downloadHandler.text);
                    callback(result?.path ?? string.Empty);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Get destination path failed: {request.error}");
                    callback(string.Empty);
                }
            }
        }

        // Set destination path
        public IEnumerator SetDestinationPathAsync(string path, Action<bool> callback)
        {
            var json = JsonUtility.ToJson(new PathRequest { path = path });
            using (UnityWebRequest request = new UnityWebRequest($"{_baseUrl}/api/destination_path", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    callback(result != null && result.success);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Set destination path failed: {request.error}");
                    callback(false);
                }
            }
        }

        // Get name pattern
        public IEnumerator GetNamePatternAsync(Action<string> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{_baseUrl}/api/name_pattern"))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<PatternResponse>(request.downloadHandler.text);
                    callback(result?.pattern ?? string.Empty);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Get name pattern failed: {request.error}");
                    callback(string.Empty);
                }
            }
        }

        // Set name pattern
        public IEnumerator SetNamePatternAsync(string pattern, Action<bool> callback)
        {
            var json = JsonUtility.ToJson(new PatternRequest { pattern = pattern });
            using (UnityWebRequest request = new UnityWebRequest($"{_baseUrl}/api/name_pattern", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    callback(result != null && result.success);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Set name pattern failed: {request.error}");
                    callback(false);
                }
            }
        }

        // Get tracked files
        public IEnumerator GetTrackedFilesAsync(int count, Action<TrackedFilesResponse?> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{_baseUrl}/api/tracking?count={count}"))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    TrackedFilesResponse? result = null;
                    try
                    {
                        result = JsonUtility.FromJson<TrackedFilesResponse>(request.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        HS2SandboxPlugin.Log.LogError($"Failed to parse tracked files response: {ex.Message}");
                    }
                    callback(result);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Get tracked files failed: {request.error}");
                    callback(null);
                }
            }
        }

        // Clear tracked files
        public IEnumerator ClearTrackedFilesAsync(Action<bool> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Delete($"{_baseUrl}/api/tracking"))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    callback(result != null && result.success);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Clear tracked files failed: {request.error}");
                    callback(false);
                }
            }
        }

        // Copy and rename
        public IEnumerator CopyAndRenameAsync(Action<bool> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Post($"{_baseUrl}/api/copy_rename", ""))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    var result = JsonUtility.FromJson<ApiResponse>(request.downloadHandler.text);
                    callback(result != null && result.success);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Copy and rename failed: {request.error}");
                    callback(false);
                }
            }
        }

        // Get status
        public IEnumerator GetStatusAsync(Action<StatusResponse?> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{_baseUrl}/api/status"))
            {
                yield return request.SendWebRequest();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    StatusResponse? result = null;
                    try
                    {
                        result = JsonUtility.FromJson<StatusResponse>(request.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        HS2SandboxPlugin.Log.LogError($"Failed to parse status response: {ex.Message}");
                    }
                    callback(result);
                }
                else
                {
                    HS2SandboxPlugin.Log.LogError($"Get status failed: {request.error}");
                    callback(null);
                }
            }
        }

        // Check if directory exists
        public bool DirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        // Create directory
        public bool CreateDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"Create directory failed: {ex.Message}");
                return false;
            }
        }

    }

    // Request models
    [Serializable]
    public class PathRequest
    {
        public string path = "";
    }

    [Serializable]
    public class PatternRequest
    {
        public string pattern = "";
    }

    // Response models
    [Serializable]
    public class ApiResponse
    {
        public bool success;
        public string message = "";
        public string error = "";
    }

    [Serializable]
    public class PathResponse
    {
        public bool success;
        public string path = "";
    }

    [Serializable]
    public class PatternResponse
    {
        public bool success;
        public string pattern = "";
    }

    [Serializable]
    public class TrackedFile
    {
        public string original_path = "";
        public string original_name = "";
        public string new_name = "";
        public string state = ""; // "normal", "duplicate", or "exists"
    }

    [Serializable]
    public class TrackedFilesResponse
    {
        public bool success;
        public TrackedFile[] files = new TrackedFile[0];
        public int total_count;
        public int returned_count;
    }

    [Serializable]
    public class StatusResponse
    {
        public bool success;
        public bool is_tracking;
        public int tracked_files_count;
        public string source_path = "";
        public string destination_path = "";
        public string name_pattern = "";
    }
}


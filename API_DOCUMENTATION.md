# File Manager API Documentation

This document describes the REST API endpoints available for interacting with the File Manager application.

## Base URL

All API endpoints are available at:
```
http://127.0.0.1:5000
```

The API server starts automatically when the File Manager application is running.

## Response Format

All responses include a `success` boolean field. The structure of successful responses varies by endpoint:

**Common Success Response Pattern:**
- All responses include `"success": true`
- Additional fields depend on the endpoint (see individual endpoint documentation below)

**Error Response:**
```json
{
  "success": false,
  "error": "Error message description"
}
```

**Note:** Each endpoint returns different fields based on its purpose. For example:
- GET endpoints return the requested data directly (e.g., `path`, `pattern`, `files`)
- POST/DELETE endpoints typically return a `message` field
- The `/api/tracking` GET endpoint returns `files`, `total_count`, and `returned_count`
- The `/api/issues` GET endpoint returns `files`, `duplicate_count`, `exists_count`, and `issues_count`
- The `/api/status` endpoint returns multiple status fields



## Endpoints

### 1. Health Check

Check if the API server is running and healthy.

**Endpoint:** `GET /api/health`

**Request:** No parameters required

**Response:**
```json
{
  "status": "healthy",
  "service": "FileManagerAPI"
}
```

**Example:**
```bash
curl http://127.0.0.1:5000/api/health
```

**Note:** This endpoint always returns a 200 status code when the API server is running. It does not include the `success` field as it's a simple health check endpoint.




### 2. Start Tracking

Start monitoring the source folder for new files.

**Endpoint:** `POST /api/tracking/start`

**Request Body:** None

**Response:**
```json
{
  "success": true,
  "message": "Tracking started"
}
```

**Example:**
```bash
curl -X POST http://127.0.0.1:5000/api/tracking/start
```



### 3. Stop Tracking

Stop monitoring the source folder for new files.

**Endpoint:** `POST /api/tracking/stop`

**Request Body:** None

**Response:**
```json
{
  "success": true,
  "message": "Tracking stopped"
}
```

**Example:**
```bash
curl -X POST http://127.0.0.1:5000/api/tracking/stop
```



### 4. Get/Set Source Path

Get or set the source folder path that is being monitored.

**Endpoint:** `GET /api/source_path` | `POST /api/source_path`

**GET Request:**
- No parameters required

**GET Response:**
```json
{
  "success": true,
  "path": "C:\\MyFolder\\Source"
}
```

**POST Request Body:**
```json
{
  "path": "C:\\MyFolder\\Source"
}
```

**POST Response:**
```json
{
  "success": true,
  "message": "Source path updated"
}
```

**Examples:**
```bash
# Get source path
curl http://127.0.0.1:5000/api/source_path

# Set source path
curl -X POST http://127.0.0.1:5000/api/source_path \
  -H "Content-Type: application/json" \
  -d '{"path": "C:\\MyFolder\\Source"}'
```



### 5. Get/Set Destination Path

Get or set the destination folder path where files will be copied.

**Endpoint:** `GET /api/destination_path` | `POST /api/destination_path`

**GET Request:**
- No parameters required

**GET Response:**
```json
{
  "success": true,
  "path": "C:\\MyFolder\\Destination"
}
```

**POST Request Body:**
```json
{
  "path": "C:\\MyFolder\\Destination"
}
```

**POST Response:**
```json
{
  "success": true,
  "message": "Destination path updated"
}
```

**Examples:**
```bash
# Get destination path
curl http://127.0.0.1:5000/api/destination_path

# Set destination path
curl -X POST http://127.0.0.1:5000/api/destination_path \
  -H "Content-Type: application/json" \
  -d '{"path": "C:\\MyFolder\\Destination"}'
```



### 6. Get/Set Name Pattern

Get or set the naming pattern used for renaming files.

**Endpoint:** `GET /api/name_pattern` | `POST /api/name_pattern`

**GET Request:**
- No parameters required

**GET Response:**
```json
{
  "success": true,
  "pattern": "file_{counter}"
}
```

**POST Request Body:**
```json
{
  "pattern": "photo_{counter}_{batch}"
}
```

**POST Response:**
```json
{
  "success": true,
  "message": "Name pattern updated"
}
```

**Examples:**
```bash
# Get name pattern
curl http://127.0.0.1:5000/api/name_pattern

# Set name pattern
curl -X POST http://127.0.0.1:5000/api/name_pattern \
  -H "Content-Type: application/json" \
  -d '{"pattern": "photo_{counter}_{batch}"}'
```



### 7. Get Tracked Files / Clear Tracked Files

Get the latest tracked files with their state information, or clear all tracked files.

**Endpoint:** `GET /api/tracking` | `DELETE /api/tracking`

**GET Request Parameters:**
- `count` (optional, integer, default: 10) - Number of latest files to retrieve (1-1000)

**GET Response:**
```json
{
  "success": true,
  "files": [
    {
      "original_path": "C:\\Source\\file1.jpg",
      "original_name": "file1.jpg",
      "new_name": "renamed_file1.jpg",
      "state": "normal"
    },
    {
      "original_path": "C:\\Source\\file2.jpg",
      "original_name": "file2.jpg",
      "new_name": "renamed_file1.jpg",
      "state": "duplicate"
    },
    {
      "original_path": "C:\\Source\\file3.jpg",
      "original_name": "file3.jpg",
      "new_name": "existing_file.jpg",
      "state": "exists"
    }
  ],
  "total_count": 3,
  "returned_count": 3
}
```

**File State Values:**
- `"normal"` - No conflicts, file is ready to be copied
- `"duplicate"` - The new filename collides with another tracked file (red indicator in UI)
- `"exists"` - The new filename already exists in the destination folder (blue indicator in UI)

**DELETE Request:**
- No parameters required

**DELETE Response:**
```json
{
  "success": true,
  "message": "Tracked files cleared"
}
```

**Examples:**
```bash
# Get latest 5 tracked files
curl "http://127.0.0.1:5000/api/tracking?count=5"

# Get all tracked files (up to 1000)
curl "http://127.0.0.1:5000/api/tracking?count=1000"

# Clear all tracked files
curl -X DELETE http://127.0.0.1:5000/api/tracking
```



### 8. Get Issues

Get all tracked files that have naming issues: either a duplicate preview name (collision with another tracked file) or a preview name that already exists in the destination folder. The `files` array uses the same structure as the tracking GET response.

**Endpoint:** `GET /api/issues`

**Request:** No parameters required

**Response:**
```json
{
  "success": true,
  "files": [
    {
      "original_path": "C:\\Source\\file2.jpg",
      "original_name": "file2.jpg",
      "new_name": "renamed_file1.jpg",
      "state": "duplicate"
    },
    {
      "original_path": "C:\\Source\\file3.jpg",
      "original_name": "file3.jpg",
      "new_name": "existing_file.jpg",
      "state": "exists"
    }
  ],
  "duplicate_count": 1,
  "exists_count": 1,
  "issues_count": 2
}
```

**Response Fields:**
- `files` - Array of file entries with issues only (same shape as tracking: `original_path`, `original_name`, `new_name`, `state`)
- `duplicate_count` - Number of tracked files whose preview name collides with another tracked file
- `exists_count` - Number of tracked files whose preview name already exists in the destination folder
- `issues_count` - Total number of issues (`duplicate_count` + `exists_count`)

**File State Values in `files`:**
- `"duplicate"` - The new filename collides with another tracked file
- `"exists"` - The new filename already exists in the destination folder

**Example:**
```bash
curl http://127.0.0.1:5000/api/issues
```



### 9. Copy and Rename

Copy all tracked files to the destination folder with new names based on the naming pattern.

**Endpoint:** `POST /api/copy_rename`

**Request Body:** None

**Response:**
```json
{
  "success": true,
  "message": "Copy and rename initiated"
}
```

**Note:** This operation may show confirmation dialogs in the GUI if there are existing files or missing rules. The operation runs asynchronously.

**Example:**
```bash
curl -X POST http://127.0.0.1:5000/api/copy_rename
```



### 10. Get Application Status

Get the current status of the application including tracking state, file counts, and current settings.

**Endpoint:** `GET /api/status`

**Request:** No parameters required

**Response:**
```json
{
  "success": true,
  "is_tracking": true,
  "tracked_files_count": 5,
  "source_path": "C:\\MyFolder\\Source",
  "destination_path": "C:\\MyFolder\\Destination",
  "name_pattern": "file_{counter}"
}
```

**Example:**
```bash
curl http://127.0.0.1:5000/api/status
```



## C# Example Usage

Here's an example of how to use the API from C#:

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class FileManagerApiClient
{
    private readonly HttpClient _client;
    private readonly string _baseUrl = "http://127.0.0.1:5000";

    public FileManagerApiClient()
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl)
        };
    }

    // Health check
    public async Task<bool> HealthCheckAsync()
    {
        var response = await _client.GetAsync("/api/health");
        return response.IsSuccessStatusCode;
    }

    // Start tracking
    public async Task<bool> StartTrackingAsync()
    {
        var response = await _client.PostAsync("/api/tracking/start", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse>(content);
        return result.Success;
    }

    // Stop tracking
    public async Task<bool> StopTrackingAsync()
    {
        var response = await _client.PostAsync("/api/tracking/stop", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse>(content);
        return result.Success;
    }

    // Get source path
    public async Task<string> GetSourcePathAsync()
    {
        var response = await _client.GetAsync("/api/source_path");
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<PathResponse>(content);
        return result.Path;
    }

    // Set source path
    public async Task<bool> SetSourcePathAsync(string path)
    {
        var json = JsonConvert.SerializeObject(new { path });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/source_path", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse>(responseContent);
        return result.Success;
    }

    // Get tracked files
    public async Task<TrackedFilesResponse> GetTrackedFilesAsync(int count = 10)
    {
        var response = await _client.GetAsync($"/api/tracking?count={count}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TrackedFilesResponse>(content);
    }

    // Clear tracked files
    public async Task<bool> ClearTrackedFilesAsync()
    {
        var response = await _client.DeleteAsync("/api/tracking");
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse>(content);
        return result.Success;
    }

    // Get issues (files with duplicate or exists state)
    public async Task<IssuesResponse> GetIssuesAsync()
    {
        var response = await _client.GetAsync("/api/issues");
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<IssuesResponse>(content);
    }

    // Copy and rename
    public async Task<bool> CopyAndRenameAsync()
    {
        var response = await _client.PostAsync("/api/copy_rename", null);
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<ApiResponse>(content);
        return result.Success;
    }

    // Get status
    public async Task<StatusResponse> GetStatusAsync()
    {
        var response = await _client.GetAsync("/api/status");
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<StatusResponse>(content);
    }
}

// Response models
public class ApiResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }
}

public class PathResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; }
}

public class TrackedFile
{
    [JsonProperty("original_path")]
    public string OriginalPath { get; set; }

    [JsonProperty("original_name")]
    public string OriginalName { get; set; }

    [JsonProperty("new_name")]
    public string NewName { get; set; }

    [JsonProperty("state")]
    public string State { get; set; } // "normal", "duplicate", or "exists"
}

public class TrackedFilesResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("files")]
    public TrackedFile[] Files { get; set; }

    [JsonProperty("total_count")]
    public int TotalCount { get; set; }

    [JsonProperty("returned_count")]
    public int ReturnedCount { get; set; }
}

public class IssuesResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("files")]
    public TrackedFile[] Files { get; set; }

    [JsonProperty("duplicate_count")]
    public int DuplicateCount { get; set; }

    [JsonProperty("exists_count")]
    public int ExistsCount { get; set; }

    [JsonProperty("issues_count")]
    public int IssuesCount { get; set; }
}

public class StatusResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("is_tracking")]
    public bool IsTracking { get; set; }

    [JsonProperty("tracked_files_count")]
    public int TrackedFilesCount { get; set; }

    [JsonProperty("source_path")]
    public string SourcePath { get; set; }

    [JsonProperty("destination_path")]
    public string DestinationPath { get; set; }

    [JsonProperty("name_pattern")]
    public string NamePattern { get; set; }
}
```



## Error Handling

All endpoints return appropriate HTTP status codes:

- `200 OK` - Request successful
- `400 Bad Request` - Invalid request parameters
- `500 Internal Server Error` - Server error occurred

Always check the `success` field in the JSON response to determine if the operation was successful. If `success` is `false`, check the `error` field for details.



## Notes

- The API server runs in a background thread and does not block the GUI
- All GUI operations are executed thread-safely using the main thread
- The API server starts automatically when the File Manager application launches
- The server runs on `127.0.0.1:5000` by default (localhost only)
- File paths should use proper Windows path format (e.g., `C:\\Folder\\Subfolder`)


---
name: build-bepinex-plugin
description: Build HS2SandboxPlugin modules with dotnet, verify output, then deploy to the game BepInEx folder (interactive vs background rules). Use when the user asks to build, compile, deploy, or test the plugin.
---

# Build & deploy BepInEx plugin modules

## When to use

- User asks to build, compile, create, deploy, or test the plugin.
- After code changes that should be run in-game.
- If a build was not requested, ask once whether to build before doing so.

## Interaction mode

| Mode | Build | After successful build | During deploy |
|------|--------|-------------------------|---------------|
| **Normal** | Run `dotnet build` | **Ask** whether to start deployment. If no, stop after verify. | Ask when the skill requires a user choice (overwrite vs deactivate DLL, launch game, watch log). |
| **Background** (user said run in background / no interruptions) | Run build | **Do not ask**—proceed with deployment automatically. | No confirmation prompts; still backup existing DLL before replacing. |

Always use the **Ask Question** tool (or equivalent user prompt) everywhere when the skill lists a choice and the session is not in background mode.

## Paths (constants)

| Role | Path |
|------|------|
| Solution / repo root | Workspace root (where `HS2-Sandbox.sln` lives) |
| Module projects | `targets/HS2/<Module>/HS2Sandbox.<Module>.csproj` |
| Built DLLs | `targets/HS2/<Module>/bin/Release/HS2Sandbox.<Module>.dll` |
| Game exe | `D:/Honey Select/StudioNEOV2.exe` |
| Plugin deploy folder | `D:/Honey Select/BepInEx/plugins/HS2-Sandbox/` |
| Game log | `D:/Honey Select/output_log.txt` |

Available modules: `CopyScript`, `Timeline`, `SearchBarManager`, `SonScale`, `WorkspaceTreeLock`, `Notebook`, `PoseBrowser`, `AnimBrowser`

Run shell commands from the **repo root** unless a step uses absolute paths.

---

## 1. Build

Generate assembly file metadata from `PluginVersion` constants (Windows Explorer "File version"), then build:

```powershell
python .github/scripts/generate_plugin_versions_props.py
dotnet build HS2-Sandbox.sln -c Release
```

Or build a single module:

```powershell
dotnet build targets/HS2/AnimBrowser/HS2Sandbox.AnimBrowser.csproj -c Release
```

Verify the file exists (example for Timeline):

```powershell
Test-Path .\targets\HS2\Timeline\bin\Release\HS2Sandbox.Timeline.dll
```

If the build failed, fix errors before any deploy step.

---

## 2. Start deployment (gate)

- **Normal:** Ask: deploy the fresh DLL to `BepInEx/plugins/HS2-Sandbox` now? If **no**, stop.
- **Background:** Skip the question; continue with §3.

---

## 3. Deployment steps (order matters)

Each step has a minimal PowerShell example. Use these examples to carry out each step.

### 3.1 Ensure StudioNeoV2 is not running

Check:

```powershell
Get-Process -Name "StudioNeoV2" -ErrorAction SilentlyContinue
```

If it is running, close it (user may prefer to save first in normal mode so ask first):

```powershell
Stop-Process -Name "StudioNeoV2" -Force -ErrorAction SilentlyContinue
```

### 3.2 Ensure plugin folder exists

```powershell
New-Item -ItemType Directory -Force -Path "D:\Honey Select\BepInEx\plugins\HS2-Sandbox" | Out-Null
```

### 3.3 If the module DLL already exists at the target

- **Normal:** Ask: **overwrite** in place, or **deactivate** the old file (rename extension to `.dl_`) then copy the new DLL?
- **Background:** Backup then replace (see next); optionally deactivate instead if that matches prior user preference—default to **backup + overwrite** for a single clear path.

Backup existing DLL before overwrite (recommended, required in background):

```powershell
$dll = "D:\Honey Select\BepInEx\plugins\HS2-Sandbox\HS2Sandbox.Timeline.dll"
if (Test-Path $dll) { Copy-Item $dll "$dll.bak" -Force }
```

### 3.4 Copy built DLL to the game

```powershell
Copy-Item -Path ".\targets\HS2\Timeline\bin\Release\HS2Sandbox.Timeline.dll" -Destination "D:\Honey Select\BepInEx\plugins\HS2-Sandbox\HS2Sandbox.Timeline.dll" -Force
```

---

## 4. After deploy (optional)

- **Normal:** Ask whether to start StudioNeoV2. If yes:

```powershell
Start-Process "D:\Honey Select\StudioNEOV2.exe"
```

- **Normal:** Ask whether to monitor the log. Useful tail (adjust `-Tail` as needed):

```powershell
Get-Content "D:\Honey Select\output_log.txt" -Tail 120 -Wait
```

Loading heuristic: when the log contains a line like `[Info   :Advanced Item Search] AdvancedItemSearch Initialized.`, treat StudioNeoV2 as finished loading for smoke-check purposes.

If log lines show **errors** mentioning this plugin, summarize them and ask whether to fix the code.

---

## Project layout (reference)

```
HS2-Sandbox/
├── HS2-Sandbox.sln
├── Directory.Build.props          (common settings)
├── src/
│   ├── Core/                      (shared infrastructure)
│   ├── Timeline/                  (timeline source)
│   ├── PoseBrowser/               (pose browser source)
│   ├── AnimBrowser/               (anim browser source)
│   ├── ...                        (other modules)
├── targets/
│   ├── HS2/
│   │   ├── Directory.Build.props  (HS2 NuGet refs)
│   │   ├── Timeline/
│   │   │   ├── Plugin.cs
│   │   │   └── HS2Sandbox.Timeline.csproj
│   │   └── ...
│   └── KK/                        (Koikatsu targets)
├── assets/                        (icons)
└── .cursor/skills/
```

## References

- `BepInEx.Core`, `UnityEngine.Modules`, IllusionLibs (`IllusionLibs.HoneySelect2.*`), and `IllusionModdingAPI.HS2API` via `Directory.Build.props` hierarchy and `nuget.config`.

## Troubleshooting

- **Missing references:** Run `dotnet restore` from the repo root; ensure `nuget.config` includes the IllusionLibs feed. No local game `HS2_Data/Managed` path is required.
- **SDK:** `dotnet --list-sdks` — need an SDK that can build the project (e.g. .NET 6+ SDK for `net472` targets).

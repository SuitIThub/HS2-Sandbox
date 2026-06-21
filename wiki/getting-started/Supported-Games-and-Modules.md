# Supported games & modules

## Overview

| Module | HS2 | KKS | KK | Sidebar icon |
|--------|:---:|:---:|:--:|:------------:|
| CopyScript | ✓ | — | — | ✓ |
| Timeline | ✓ | — | — | ✓ |
| SearchBarManager | ✓ | — | — | — |
| Son Scale | ✓ | — | — | ✓ |
| Workspace Tree Lock | ✓ | — | — | — |
| Notebook | ✓ | — | — | ✓ |
| Pose Browser | ✓ | ✓ | ✓ | ✓ |
| Anim Browser | ✓ | ✓ | ✓ | ✓ |

## DLL names & plugin GUIDs

### HS2 (`StudioNEOV2.exe`, .NET 4.7.2)

| Module | DLL | GUID |
|--------|-----|------|
| All-in-one | `HS2SandboxPlugin.dll` | `com.hs2.sandbox` |
| CopyScript | `HS2Sandbox.CopyScript.dll` | `com.hs2.sandbox.copyscript` |
| Timeline | `HS2Sandbox.Timeline.dll` | `com.hs2.sandbox.timeline` |
| SearchBarManager | `HS2Sandbox.SearchBarManager.dll` | `com.hs2.sandbox.searchbarmanager` |
| Son Scale | `HS2Sandbox.SonScale.dll` | `com.hs2.sandbox.sonscale` |
| Workspace Tree Lock | `HS2Sandbox.WorkspaceTreeLock.dll` | `com.hs2.sandbox.workspacetreelock` |
| Notebook | `HS2Sandbox.Notebook.dll` | `com.hs2.sandbox.notebook` |
| Pose Browser | `HS2Sandbox.PoseBrowser.dll` | `com.hs2.sandbox.posebrowser` |
| Anim Browser | `HS2Sandbox.AnimBrowser.dll` | `com.hs2.sandbox.animbrowser` |

### KKS (`CharaStudio.exe`, .NET 4.7.2)

| Module | DLL | GUID |
|--------|-----|------|
| Pose Browser | `KKSSandbox.PoseBrowser.dll` | `com.kks.sandbox.posebrowser` |
| Anim Browser | `KKSSandbox.AnimBrowser.dll` | `com.kks.sandbox.animbrowser` |

### KK (`CharaStudio.exe`, .NET 3.5)

| Module | DLL | GUID |
|--------|-----|------|
| Pose Browser | `KKSandbox.PoseBrowser.dll` | `com.kk.sandbox.posebrowser` |
| Anim Browser | `KKSandbox.AnimBrowser.dll` | `com.kk.sandbox.animbrowser` |

## `versions.json` keys (in-game update check)

| Key | Module |
|-----|--------|
| `poseBrowser` / `poseBrowserDownload` | Pose Browser HS2 |
| `poseBrowserKks` / `poseBrowserKksDownload` | Pose Browser KKS |
| `poseBrowserKk` / `poseBrowserKkDownload` | Pose Browser KK |
| `animBrowser` / `animBrowserDownload` | Anim Browser HS2 |
| `animBrowserKks` / `animBrowserKksDownload` | Anim Browser KKS |
| `animBrowserKk` / `animBrowserKkDownload` | Anim Browser KK |
| `copyScript`, `timeline`, `sonScale`, … | Corresponding HS2 modules |

## Game-specific differences

### Pose Browser

- **Shared code:** `src/PoseBrowser/` — compile-time defines `HS2`, `KKS`, `KK`
- **HS2 only:** HS2Heelz integration (`HeelzControlService`)
- **PE compat (all games):** **HS2PE** on HS2; **KKPE** on KK and KKS — Advanced Mode breast/butt data embedded in poses when the matching plugin is installed

### Anim Browser

- **Shared code:** `src/AnimBrowser/`
- **HS2 only:** **Hover preview** (`AnimPreviewStage`, embedded rig + GL renderer; configurable camera in Options)
- **All games:** **Thumbnail capture** → `UserData/com.hs2.sandbox/anim_thumbnails/`
- **KK/KKS:** No preview stage; use captured PNGs or catalog placeholders

## Default deploy paths (development)

| Game | Target folder (`build.ps1` default) |
|------|-------------------------------------|
| HS2 | `…/BepInEx/plugins/HS2-Sandbox/` |
| KKS | `…/BepInEx/plugins/KKS-Sandbox/` |
| KK | `…/BepInEx/plugins/KK-Sandbox/` |

→ [Installation](Installation) · [Building from source](Building-from-Source)

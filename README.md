# HS2 Sandbox Plugin

[![License: MIT](https://img.shields.io/github/license/SuitIThub/HS2-Sandbox?style=flat-square)](LICENSE)
[![CI](https://github.com/SuitIThub/HS2-Sandbox/actions/workflows/main.yml/badge.svg?branch=main)](https://github.com/SuitIThub/HS2-Sandbox/actions/workflows/main.yml)

[![All-in-one](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=All-in-one&query=%24.allInOne&style=flat-square&color=0366d6)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/HS2SandboxPlugin.cs)
[![CopyScript](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=CopyScript&query=%24.copyScript&style=flat-square&color=2ea043)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/CopyScript/CopyScriptModulePlugin.cs)
[![Timeline](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Timeline&query=%24.timeline&style=flat-square&color=8957e5)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/Timeline/TimelineModulePlugin.cs)
[![SearchBarManager](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=SearchBarManager&query=%24.searchBarManager&style=flat-square&color=bc4c00)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/SearchBarManager/SearchBarManagerModulePlugin.cs)
[![Son scale](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Son+scale&query=%24.sonScale&style=flat-square&color=6e40c9)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/SonScale/SonScaleModulePlugin.cs)
[![Workspace tree lock](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Workspace+tree+lock&query=%24.workspaceTreeLock&style=flat-square&color=1f6feb)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/WorkspaceTreeLock/WorkspaceTreeLockModulePlugin.cs)
[![Notebook](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Notebook&query=%24.notebook&style=flat-square&color=8b5cf6)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/Notebook/NotebookModulePlugin.cs)
[![Pose Browser](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Pose+Browser&query=%24.poseBrowser&style=flat-square&color=0d9488)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/PoseBrowser/PoseBrowserModulePlugin.cs)

BepInEx plugins for **Honey Select 2** that add quality-of-life tools to **StudioNeoV2**: automation helpers, a pose library, notes, search bars on long lists, and finer control over character scaling.

---

## Table of contents

- [What you get](#what-you-get)
  - [CopyScript](#hs2-sandbox--copyscript-hs2sandboxcopyscriptdll)
  - [Timeline](#hs2-sandbox--timeline-hs2sandboxtimelinedll)
  - [Search bars](#hs2-sandbox--searchbarmanager-hs2sandboxsearchbarmanagerdll)
  - [Son scale](#hs2-sandbox--son-scale-hs2sandboxsonscaledll)
  - [Workspace tree lock](#hs2-sandbox--workspace-tree-lock-hs2sandboxworkspacetreelockdll)
  - [Notebook](#hs2-sandbox--notebook-hs2sandboxnotebookdll)
  - [Pose Browser](#hs2-sandbox--pose-browser-hs2sandboxposebrowserdll)
- [All-in-one build](#all-in-one-build)
- [Requirements](#requirements)
- [Installation](#installation)
- [For developers](#for-developers)
  - [What ships in this repo](#what-ships-in-this-repo)
  - [Plugin reference](#plugin-reference)
  - [Building from source](#building-from-source)
  - [Technical notes](#technical-notes)
  - [Troubleshooting](#troubleshooting)
- [License](#license)
- [Contributing](#contributing)

---

## What you get

You can install **one DLL that includes everything**, or pick **individual modules** if you only want specific features. After installation, most tools appear as **buttons on the Studio left sidebar**; open a window from there and work as usual in Studio.

Each module below has a **Download** link to the latest release build.

### HS2 Sandbox — CopyScript (`HS2Sandbox.CopyScript.dll`)
[Download CopyScript](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-7b8d4a7644041b03247048c2b11a40ecad637664/HS2Sandbox.CopyScript.dll)

Connects Studio to an external **CopyScript** service on your PC. From the **CopyScript Control** window you can work with tracked files, counters, lists, and batch rules without leaving the game.

**Typical use:** run your CopyScript server, open the window from the sidebar, and point it at the correct host/port. If nothing connects, check firewall settings and that the server is actually running.

### HS2 Sandbox — Timeline (`HS2Sandbox.Timeline.dll`)
[Download Timeline](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-7b8d4a7644041b03247048c2b11a40ecad637664/HS2Sandbox.Timeline.dll)

An **action timeline** for Studio: ordered steps (waits, screenshots, Studio actions, CopyScript calls, variables, and more), with run / pause / stop. Handy for repeatable scene setup or light automation.

Many timeline commands that talk to **other plugins** (for example VNGE, FashionLine, or similar) expect a **modified build of that plugin** with extra hooks or APIs exposed. Those manipulated versions are **not** included in this repository—you need to obtain and install them separately if you use those commands.

**Typical use:** build a timeline in the window, then play it when you want the sequence to run. Stick to Studio-native steps unless you already have the matching modified plugins installed.

### HS2 Sandbox — SearchBarManager (`HS2Sandbox.SearchBarManager.dll`)
[Download SearchBarManager](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-7b8d4a7644041b03247048c2b11a40ecad637664/HS2Sandbox.SearchBarManager.dll)

Adds **search fields** on long Studio lists (for example wear/custom categories) so you can filter items quickly instead of scrolling forever.

**Typical use:** install this module if you only want search bars, or use the all-in-one build. There is no separate sidebar button—the bars appear on the panels they attach to.

### HS2 Sandbox — Son scale (`HS2Sandbox.SonScale.dll`)
[Download Son scale](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-540780726abd2c94d745ff12f54cc72521d6684b/HS2Sandbox.SonScale.dll)

**Separate sliders** for overall size, length, and girth on the selected character’s Son (member), under **Manipulate → Chara → State**. Works with or without **Studio Better Penetration**; with BP installed, length scaling integrates more cleanly.

**Typical use:** select a character in the workspace, open **Son scale** from the sidebar, enable split scaling, then adjust sliders in the Manipulate panel.

### HS2 Sandbox — Workspace tree lock (`HS2Sandbox.WorkspaceTreeLock.dll`)
[Download Workspace tree lock](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-745349e6470d993a35181964caccc8839106d6ea/HS2Sandbox.WorkspaceTreeLock.dll)

In the Studio **object list**, **middle-click** a nested row to **pin** it. Pinned rows stay visible when you collapse parent groups (they get a cyan border). Middle-click again to unpin.

**Typical use:** pin deep items you need to reach often so collapsing the tree does not hide them.

### HS2 Sandbox — Notebook (`HS2Sandbox.Notebook.dll`)
[Download Notebook](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-f316b62761d9722b82c601ae6944dfc71d4650e1/HS2Sandbox.Notebook.dll)

A simple **in-game notepad** for session notes—ideas, shot lists, reminders—opened from the sidebar.

**Typical use:** keep the window open while you work; content is local to your current plugin session.

### HS2 Sandbox — Pose Browser (`HS2Sandbox.PoseBrowser.dll`)
[Download Pose Browser](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-805e9813c67cd46e9f7c5fa3f6ef64ed878cab7e/HS2Sandbox.PoseBrowser.dll)

Browse, tag, favorite, save, and apply poses from your **`UserData/studio/pose`** folder. Folder tree, thumbnails, search, and file operations (move, copy, delete with backup) from one window.

**Typical use:** open from the sidebar, pick a folder, filter or tag poses, apply to selected characters. Optional **[HS2Wiki](https://github.com/SuitIThub/HS2Wiki)** adds extra help on **F3** when installed. More detail: [`docs/PoseBrowser-HS2Wiki-Manual.md`](docs/PoseBrowser-HS2Wiki-Manual.md).

---

## All-in-one build
[Download All-in-one](https://github.com/SuitIThub/HS2-Sandbox/releases/download/release-805e9813c67cd46e9f7c5fa3f6ef64ed878cab7e/HS2SandboxPlugin.dll)

**`HS2SandboxPlugin.dll`** bundles every feature above in a single plugin: sidebar toggles for CopyScript, Timeline, Son scale, Notebook, and Pose Browser, plus search bars and workspace tree lock built in.

Choose this if you want the full set without managing several DLLs. See [Installation](#installation) for the important rule: **do not** load the all-in-one DLL together with split modules that duplicate the same features.

---

## Requirements

- **Honey Select 2** with **StudioNeoV2** (for Studio features).
- **BepInEx 5** (this project targets **5.4.21**; newer 5.x usually works).
- **HS2API** (KKAPI for HS2) is expected in most mod packs—load it before these plugins, as with other HS2 mods.

**CopyScript** needs a running CopyScript server on your PC. **Timeline** steps that target other plugins often need **modified builds** of those plugins (not shipped here)—see the [Timeline](#hs2-sandbox--timeline-hs2sandboxtimelinedll) section above.

---

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Honey Select 2 if needed.
2. Install **HS2API** from your usual HS2 mod channels if your pack does not already include it.
3. Copy **either**:
   - **`HS2SandboxPlugin.dll`** (all-in-one), **or**
   - The individual **`HS2Sandbox.*.dll`** files you want  
   into `BepInEx/plugins/` (subfolders are fine).
4. Place the matching **PNG icons** next to each DLL that needs them (builds copy them to output):
   - **All-in-one** beside `HS2SandboxPlugin.dll`: `copy-icon.png`, `timeline-icon.png`, `sonscale-icon.png`, `notes-icon.png`, `pose-icon.png`.
   - Split modules ship their own icons (e.g. Pose Browser / Notebook use `pose-icon.png` / `notes-icon.png`).

### Do not mix all-in-one with duplicate modules

**Never** install **`HS2SandboxPlugin.dll`** together with split modules for the **same** feature (for example all-in-one + `HS2Sandbox.SonScale.dll` or all-in-one + `HS2Sandbox.PoseBrowser.dll`). That can register components twice, duplicate Harmony patches, or run logic twice.

**SearchBarManager** is the usual exception: you may combine it with other split modules if you want search bars without the full package—just avoid loading the same DLL twice, and still avoid all-in-one + any module that duplicates a feature it already includes.

---

## For developers

Version badges read [`versions.json`](versions.json); CI updates that file when `PluginVersion` constants change. Release **download** links in [What you get](#what-you-get) are refreshed automatically after each publish.

### What ships in this repo

| Output | Project | Description |
|--------|---------|-------------|
| `HS2SandboxPlugin.dll` | `HS2SandboxPlugin.csproj` | **All-in-one**: every feature in a single plugin. |
| `HS2Sandbox.CopyScript.dll` | `Modules/CopyScript` | CopyScript window + toolbar only. |
| `HS2Sandbox.Timeline.dll` | `Modules/Timeline` | Action timeline window + toolbar only. |
| `HS2Sandbox.SearchBarManager.dll` | `Modules/SearchBarManager` | Search bar injection only (no sandbox toolbar). |
| `HS2Sandbox.SonScale.dll` | `Modules/SonScale` | Son scale UI + applier + optional BP hooks only. |
| `HS2Sandbox.WorkspaceTreeLock.dll` | `Modules/WorkspaceTreeLock` | Middle-click pins in the Studio workspace tree. |
| `HS2Sandbox.Notebook.dll` | `Modules/Notebook` | Notebook window and sidebar toggle only. |
| `HS2Sandbox.PoseBrowser.dll` | `Modules/PoseBrowser` | Pose library browser and sidebar toggle only. |

Shared code lives under `Shared/`, `CopyScript/`, `Timeline/`, `SearchBarManager/`, `SonScale/`, `WorkspaceTreeLock/`, `Notebook/`, and `PoseBrowser/`. The all-in-one project compiles a superset via `HS2SandboxPlugin.csproj`.

### Plugin reference

Each split module is its own BepInEx plugin (`BepInPlugin`) with a stable GUID.

| Module | GUID | BepIn dependencies | Notes |
|--------|------|-------------------|--------|
| All-in-one | `com.hs2.sandbox` | Soft: Studio Better Penetration | Registers all features; config **`Search Bars` → `Additional Parent Paths`**. |
| CopyScript | `com.hs2.sandbox.copyscript` | None | Requires running CopyScript HTTP API. |
| Timeline | `com.hs2.sandbox.timeline` | None | External-plugin commands need modified plugin builds (not in this repo). |
| SearchBarManager | `com.hs2.sandbox.searchbarmanager` | None | Extra UI roots via **`Additional Parent Paths`**. |
| Son scale | `com.hs2.sandbox.sonscale` | Soft: Studio Better Penetration | Harmony on BP `DanAgent.SetDanTarget` when present. |
| Workspace tree lock | `com.hs2.sandbox.workspacetreelock` | None | Harmony on `Studio.TreeNodeObject` visibility helpers. |
| Notebook | `com.hs2.sandbox.notebook` | None | Do not load with all-in-one. |
| Pose Browser | `com.hs2.sandbox.posebrowser` | None | ZIP exchange format: [`Modules/PoseBrowser/POSE_ZIP_FORMAT.md`](Modules/PoseBrowser/POSE_ZIP_FORMAT.md). |

### Building from source

1. Clone the repository.
2. Keep `nuget.config` in place (IllusionLibs feed required).
3. From the repo root:

   ```bash
   dotnet restore HS2-Sandbox.sln
   dotnet build HS2-Sandbox.sln -c Release
   ```

4. Outputs:
   - All-in-one: `bin/Release/HS2SandboxPlugin.dll`
   - Modules: `Modules/<Name>/bin/Release/HS2Sandbox.<Name>.dll`

Restore may show **NU1603** warnings; builds usually still succeed.

**Compile-time references** (from [`Directory.Build.props`](Directory.Build.props), via [`nuget.config`](nuget.config)): BepInEx.Core 5.4.21, IllusionModdingAPI.HS2API, IllusionLibs HoneySelect2 assemblies, Unity UI modules, BepInEx.Harmony 2.9.0 (Son scale, workspace tree lock), IronPython (Timeline VNGE-related paths).

### Technical notes

**Son scale and Better Penetration**

- Without Studio BP: legacy chain scaling (localPosition / axis + root XY girth).
- With Studio BP: length/overall via `m_baseDanLength` during `SetDanTarget`; girth once on dan root `(girth, girth, 1)` for uniform thickness (per-segment XY would compound and look conical).

**CopyScript / Timeline**

- CopyScript expects a **local HTTP service**; configure host/port in the window.
- Timeline commands that call into other plugins need those plugins’ **manipulated** builds; vanilla releases are often insufficient.

**SearchBarManager**

- Default paths plus **`Additional Parent Paths`**; periodic refresh; failed `GameObject.Find` means no bar for that path.

**Pose Browser exchange ZIP (v2)**

- Documented layout (`manifest.json`, `metadata.json`, `poses/`): [`Modules/PoseBrowser/POSE_ZIP_FORMAT.md`](Modules/PoseBrowser/POSE_ZIP_FORMAT.md).

### Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| Duplicate windows / double actions | All-in-one **and** split modules for the same feature loaded together. |
| Son scale does nothing | No Studio selection; Son scale disabled; wrong character. |
| BP length integration silent | BP missing, wrong GUID, or API changed—check log for `Son scale:`. |
| CopyScript always “offline” | API not running, wrong port, or firewall. |
| Timeline step does nothing | Target plugin missing or not the modified build Timeline expects. |
| Search bars missing | UI hierarchy path changed; update **`Additional Parent Paths`**. |
| Pinned workspace rows odd after update | `TreeNodeObject` internals changed; Harmony targets may need updates. |
| Build restore fails | Missing `nuget.config` or no IllusionLibs feed access. |
| CS0618 warnings when building | Deprecated KKAPI toolbar overloads; cosmetic for now. |

---

## License

This project is licensed under the [MIT License](LICENSE).

## Contributing

For forks, update badge URLs in this README if they still point at `SuitIThub/HS2-Sandbox` on GitHub.

# HS2 Sandbox Plugin

[![All-in-one](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=All-in-one&query=%24.allInOne&style=flat-square&color=0366d6)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/HS2SandboxPlugin.cs)
[![CopyScript](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=CopyScript&query=%24.copyScript&style=flat-square&color=2ea043)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/CopyScript/CopyScriptModulePlugin.cs)
[![Timeline](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Timeline&query=%24.timeline&style=flat-square&color=8957e5)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/Timeline/TimelineModulePlugin.cs)
[![SearchBarManager](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=SearchBarManager&query=%24.searchBarManager&style=flat-square&color=bc4c00)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/SearchBarManager/SearchBarManagerModulePlugin.cs)
[![Son scale](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Son+scale&query=%24.sonScale&style=flat-square&color=6e40c9)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/SonScale/SonScaleModulePlugin.cs)

*Version badges read [`versions.json`](versions.json); CI regenerates it when `PluginVersion` constants change.*

BepInEx plugins for **Honey Select 2** that extend **StudioNeoV2** with a shared sidebar UI, optional automation (CopyScript / Timeline), search bars on manipulate panels, and split **Son** (member) scaling.

---

## Table of contents

- [What ships in this repo](#what-ships-in-this-repo)
- [Requirements](#requirements)
- [Installation](#installation)
- [Modules (split builds)](#modules-split-builds)
- [All-in-one build](#all-in-one-build)
- [Shared technical dependencies](#shared-technical-dependencies)
- [Building from source](#building-from-source)
- [Usage overview](#usage-overview)
- [Son scale and Better Penetration](#son-scale-and-better-penetration)
- [CopyScript and Timeline](#copyscript-and-timeline)
- [SearchBarManager](#searchbarmanager)
- [Troubleshooting and known issues](#troubleshooting-and-known-issues)

---

## What ships in this repo

| Output | Project | Description |
|--------|---------|-------------|
| `HS2SandboxPlugin.dll` | `HS2SandboxPlugin.csproj` | **All-in-one**: every feature in a single plugin. |
| `HS2Sandbox.CopyScript.dll` | `Modules/CopyScript` | CopyScript window + toolbar only. |
| `HS2Sandbox.Timeline.dll` | `Modules/Timeline` | Action timeline window + toolbar only. |
| `HS2Sandbox.SearchBarManager.dll` | `Modules/SearchBarManager` | Search bar injection only (no sandbox toolbar). |
| `HS2Sandbox.SonScale.dll` | `Modules/SonScale` | Son scale UI + applier + optional BP hooks only. |

Shared code lives under `Shared/`, `CopyScript/`, `Timeline/`, `SearchBarManager/`, and `SonScale/`. The all-in-one project compiles a superset of these via `HS2SandboxPlugin.csproj`.

---

## Requirements

### Game and loader

- **Honey Select 2** (StudioNeoV2 for Studio features).
- **BepInEx 5** (project targets **5.4.21**; newer 5.x builds are usually fine).

### Typical player setup

Most users already have **HS2API** (KKAPI for HS2) and BepInEx installed. This plugin references HS2API at compile time; at runtime your game should load HS2API (or equivalent) **before** these plugins, as with other HS2 mods.

---

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Honey Select 2 if you have not already.
2. Install **HS2API** from the usual HS2 mod channels if required by your pack.
3. Copy **either**:
   - **`HS2SandboxPlugin.dll`** (all-in-one) **or**
   - The individual **`HS2Sandbox.*.dll`** modules you want  
   into `BepInEx/plugins/` (any subfolder is fine).
4. Ensure **`copy-icon.png`**, **`timeline-icon.png`**, and **`sonscale-icon.png`** sit next to the DLL that needs them (the build copies them to each project’s output; the all-in-one build expects all three beside `HS2SandboxPlugin.dll`).

### Critical: do not double-load features

**Do not** install the **all-in-one** DLL **together** with the **same** split modules (e.g. `HS2SandboxPlugin.dll` + `HS2Sandbox.SonScale.dll`). You would register duplicate components, duplicate Harmony IDs, or run the same logic twice.

**SearchBarManager** is safe to combine with split **CopyScript** / **Timeline** / **Son scale** plugins if you want search bars without the all-in-one package—just avoid duplicating the same DLL twice.

---

## Modules (split builds)

Each split module is its own BepInEx plugin (`BepInPlugin`). GUIDs are stable; other mods or configs can reference them.

### HS2 Sandbox — CopyScript (`HS2Sandbox.CopyScript.dll`)

| | |
|--|--|
| **GUID** | `com.hs2.sandbox.copyscript` |
| **Declared BepIn dependencies** | None (beyond BepInEx). |
| **Purpose** | Adds a Studio sidebar toggle and **CopyScript Control** window. Talks to an external **CopyScript HTTP API** (`CopyScriptApiClient`) for tracked files, rules (counter / list / batch), and remote operations. |
| **Runtime needs** | A running CopyScript server (or compatible API) on the configured host/port. Without it, the window shows connection errors and limited functionality. |
| **Typical issues** | Firewall / wrong URL; API version skew; giving up after the built-in connection timeout (see `CopyScriptWindow` constants). |

### HS2 Sandbox — Timeline (`HS2Sandbox.Timeline.dll`)

| | |
|--|--|
| **GUID** | `com.hs2.sandbox.timeline` |
| **Declared BepIn dependencies** | None. |
| **Purpose** | **Action timeline** UI: ordered command lists, categories (CopyScript, Studio, VNGE, screenshots, variables, navigation, etc.), run/pause/stop, optional **mouse position overlay**, persistent variables, sub-timelines, and optional **CopyScript** integration when the API is available. |
| **Runtime needs** | Studio. Individual commands may require other mods (e.g. VNGE, FashionLine) depending on what you put in a timeline. |
| **Typical issues** | Large/complex timelines and reflection-based Studio hooks can break on game updates; external command targets missing. |

### HS2 Sandbox — SearchBarManager (`HS2Sandbox.SearchBarManager.dll`)

| | |
|--|--|
| **GUID** | `com.hs2.sandbox.searchbarmanager` |
| **Declared BepIn dependencies** | None. |
| **Purpose** | Periodically finds configured UI roots (default: wear/custom category panel) and **injects search bars** to filter long lists. Extra roots come from BepInEx config **`Additional Parent Paths`** (shared entry name with other modules). |
| **Runtime needs** | Studio; valid **hierarchy paths** that still exist after Illusion patches. |
| **Typical issues** | Game or mod updates rename or move `GameObject` paths → bindings never attach until paths are updated. |

### HS2 Sandbox — Son scale (`HS2Sandbox.SonScale.dll`)

| | |
|--|--|
| **GUID** | `com.hs2.sandbox.sonscale` |
| **Declared BepIn dependencies** | **Soft:** `com.animal42069.studiobetterpenetration` (Studio **Better Penetration**). The plugin loads without BP; BP only enables integrated length scaling. |
| **Purpose** | **Split Son scaling** in Studio: **overall** (master), **length**, and **girth** via injected sliders under **Manipulate → Chara → State** (cloned from the vanilla Son length row). Applies scales to the resolved **dan** bone chain on **selected** Studio characters. Uses **Harmony** to patch BP’s `DanAgent.SetDanTarget` when the Studio BP assembly is present (length via `m_baseDanLength`; girth as a **single dan-root XY multiply** so it stays uniform—per-segment XY would compound down the chain). |
| **Runtime needs** | Studio; character selected in the workspace for scaling to apply. |
| **Typical issues** | BP fork with renamed types/methods can break reflection/Harmony (check BepInEx log for “BP integration” messages). BP’s own girth/scale UI may stack visually with Son scale if both are used aggressively. UI injection path must still match `SonScaleManipulateUi` constants after game updates. |

---

## All-in-one build

| | |
|--|--|
| **GUID** | `com.hs2.sandbox` |
| **Declared BepIn dependencies** | **Soft:** `com.animal42069.studiobetterpenetration` (same as Son scale module). |
| **Purpose** | Single plugin that registers **SandboxGUI**, **CopyScript**, **ActionTimeline**, **Son scale** (applier + Manipulate UI + BP integration), and **MultiPathSearchBarManager**, with sidebar toggles for the three windows. |
| **Config** | Search bar extra paths: **`Search Bars` → `Additional Parent Paths`**. |

Use this if you want every feature without juggling multiple DLLs.

---

## Shared technical dependencies

These are **compile-time** references from [`Directory.Build.props`](Directory.Build.props) (NuGet). You do not copy them into `plugins/` manually; they are embedded or resolved by the game/BepInEx stack.

| Package / reference | Role |
|---------------------|------|
| `BepInEx.Core` 5.4.21 | Plugin base, logging, config. |
| `IllusionModdingAPI.HS2API` | Studio API, toolbar utilities, etc. |
| `IllusionLibs.HoneySelect2.Assembly-CSharp` | Game assemblies (compile-only). |
| `IllusionLibs.HoneySelect2.UnityEngine.UI` | uGUI for injected sliders/search UI. |
| `UnityEngine.Modules` | Unity 2018.4 API surface. |
| `IllusionLibs.BepInEx.Harmony` 2.9.0 | Harmony used by **Son scale** BP integration. |
| `IronPython` | Used by **Timeline** code paths that host/script via `Microsoft.Scripting` (e.g. VNGE-related interop in `VngePython.cs`). Irrelevant if you never run those commands. |

Feeds are listed in [`nuget.config`](nuget.config): **nuget.org**, **BepInEx**, **IllusionLibs** (Azure DevOps).

---

## Building from source

1. Clone the repository.
2. Ensure `nuget.config` is present (IllusionLibs feed required).
3. From the repo root:

   ```bash
   dotnet restore HS2-Sandbox.sln
   dotnet build HS2-Sandbox.sln -c Release
   ```

4. Outputs:
   - All-in-one: `bin/Release/HS2SandboxPlugin.dll` (repo root project output).
   - Modules: `Modules/<Name>/bin/Release/HS2Sandbox.<Name>.dll`.

**Note:** Restore may emit **NU1603** warnings (e.g. ExtensibleSaveFormat version resolution). Builds usually still succeed; update package versions only if you maintain the dependency graph.

---

## Usage overview

1. Start **StudioNeoV2**.
2. Use the **left sidebar** toggles added by the all-in-one or by CopyScript / Timeline / Son scale modules (SearchBarManager has no toolbar button).
3. **CopyScript** / **Timeline** windows are IMGUI subwindows; Timeline can draw a **screen overlay** for mouse positions when enabled.
4. **Son scale**: select a character in the Studio tree/workspace, open **Son scale**, enable **split scaling**, and use **Manipulate → Chara → State** sliders (**Overall size**, **Penis Length**, **Penis Girth**).

---

## Son scale and Better Penetration

- **Without Studio Better Penetration:** multi-segment rigs use **legacy** scaling (chain `localPosition` / dominant axis + root XY girth). Single-bone rigs use root scale including length on Z.
- **With Studio BP** (soft dependency, assembly name contains `BetterPenetration`, Studio `SetDanTarget` with `CollisionAgent`):  
  - **Length / overall** scale BP’s reference length (`m_baseDanLength`) while `SetDanTarget` runs.  
  - **Girth** is applied once on the **dan root** `(girth, girth, 1)` so thickness stays **uniform** along the chain. Per-segment girth on nested bones would multiply in world space and look **conical** (megaphone toward the tip).

If BP integration fails at startup, check the BepInEx log for lines starting with **`Son scale:`**.

---

## CopyScript and Timeline

- **CopyScript** is built around a **local HTTP service**. Configure the client in the CopyScript window to match your server. No server → health checks fail and most actions are unavailable.
- **Timeline** can orchestrate CopyScript commands, Studio actions, waits, variables, and more. Command availability depends on your installed mods and the game state.

---

## SearchBarManager

- Binds search UI under hard-coded default paths and any extra lines from **`Additional Parent Paths`**.
- Refreshes on an interval; destroyed parents are cleaned up automatically.
- If a path does not resolve (`GameObject.Find` fails), no bar appears for that entry—verify paths with UnityExplorer or similar after game updates.

---

## Troubleshooting and known issues

| Symptom | Likely cause |
|---------|----------------|
| Duplicate windows / double actions | All-in-one **and** one or more split modules loaded together. |
| Son scale does nothing | No Studio selection; or **Son scale** disabled in its window; or wrong character. |
| BP length integration silent | BP not installed, wrong plugin GUID, or BP assembly/API changed (Harmony patch not applied—see log). |
| CopyScript always “offline” | API not running, wrong port, firewall, or URL mismatch. |
| Search bars missing | UI hierarchy path changed; update config or defaults in `MultiPathSearchBarManager`. |
| Build restore fails | Missing `nuget.config` or no access to **IllusionLibs** feed. |
| Yellow CS0618 warnings when building | KKAPI toolbar APIs deprecated in favor of newer overloads; cosmetic for now. |

---

## License and contributing

Add or adjust a `LICENSE` file in the repo if you publish builds; this README does not substitute for project licensing. For forks, update badge URLs in this file if they still point at `SuitIThub/HS2-Sandbox` on GitHub.

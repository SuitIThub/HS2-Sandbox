# HS2-Sandbox Wiki

Welcome to the official wiki for **[HS2-Sandbox](https://github.com/SuitIThub/HS2-Sandbox)** — a collection of **BepInEx 5 plugins** that extend **Chara Studio** in Illusion games.

## What is HS2-Sandbox?

HS2-Sandbox bundles Studio workflow tools: pose and animation libraries, automation, notes, search bars on long lists, finer character scaling, and practical UI helpers. Each feature is installed **individually** as a module DLL.

## Supported games

| Game | Studio exe | Available modules |
|------|------------|-------------------|
| **Honey Select 2 (HS2)** | `StudioNEOV2.exe` | All 10 modules |
| **Koikatsu Sunshine (KKS)** | `CharaStudio.exe` | Pose Browser, Anim Browser |
| **Koikatsu (KK)** | `CharaStudio.exe` | Pose Browser, Anim Browser |

## Quick start

1. Check [Requirements](Requirements) (BepInEx, HS2API/KKAPI/KKSAPI)
2. Follow [Installation](Installation) — copy DLLs to `BepInEx/plugins/`
3. Launch Studio — most modules appear as **icons on the left sidebar**
4. Open the module guide you need (see below)

## Module overview

Each plugin module has a **start page with a table of contents**. Detail pages end with **Navigation** links (`← Previous · Module home · Next →`) so you can read each guide cover to cover.

Wiki sources are grouped by folder: `getting-started/`, `hs2/`, `pose-browser/`, `anim-browser/`, `reference/`, `developers/`. Screenshots go in `images/pose-browser/` and `images/anim-browser/`.

### HS2 only

| Module | Description | Manual |
|--------|-------------|--------|
| **CopyScript** | Connection to an external CopyScript HTTP server | [CopyScript](CopyScript) |
| **Timeline** | Action timeline with run/pause/stop | [Timeline](Timeline) |
| **SearchBarManager** | Search fields on long Studio lists | [SearchBarManager](SearchBarManager) |
| **Son Scale** | Separate Son scaling (overall/length/girth) | [Son Scale](Son-Scale) |
| **Workspace Tree Lock** | Pin workspace rows with middle-click | [Workspace Tree Lock](Workspace-Tree-Lock) |
| **Notebook** | In-game notepad with auto-save | [Notebook](Notebook) |

### Multi-game (HS2 / KKS / KK)

| Module | Description | Manual |
|--------|-------------|--------|
| **Pose Browser** | Pose library under `UserData/studio/pose` | [Pose Browser](Pose-Browser) · [screenshot checklist](images/SCREENSHOTS.md) |
| **Anim Browser** | Animation catalog, grouping, playback, thumbnails & HS2 hover preview | [Anim Browser](Anim-Browser) · [screenshot checklist](images/SCREENSHOTS.md) |

## More wiki pages

- [Supported games & DLL names](Supported-Games-and-Modules)
- [Architecture & project layout](Architecture)
- [Building from source](Building-from-Source)
- [Configuration & data files](Configuration-and-Data-Files)
- [Keyboard shortcuts](Keyboard-Shortcuts)
- [Plugin compatibility](Plugin-Compatibility) (HS2Wiki, Better Penetration, HS2Heelz, HS2PE/KKPE, …)
- [Pose ZIP format](Pose-ZIP-Format) (for modders)
- [Timeline commands (reference)](Timeline-Commands-Reference)
- [Troubleshooting](Troubleshooting)
- [Contributing & CI/releases](Contributing-and-CI)

## Downloads

Current versions and download URLs are in [`versions.json`](https://github.com/SuitIThub/HS2-Sandbox/blob/main/versions.json) and on the [Releases page](https://github.com/SuitIThub/HS2-Sandbox/releases).

## License

[Creative Commons BY 4.0](https://github.com/SuitIThub/HS2-Sandbox/blob/main/LICENSE) — original author: **Suit-Ji**.

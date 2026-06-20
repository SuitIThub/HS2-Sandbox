# Requirements

## Games & Studio

| Game | Studio | Notes |
|------|--------|-------|
| **Honey Select 2** | `StudioNEOV2.exe` | All HS2-Sandbox modules |
| **Koikatsu Sunshine** | `CharaStudio.exe` | Pose Browser & Anim Browser only |
| **Koikatsu** | `CharaStudio.exe` | Pose Browser & Anim Browser only (.NET 3.5) |

## Required dependencies

### BepInEx 5

- Recommended: **BepInEx 5.4.21** (as referenced by this project)
- Newer 5.x releases usually work as well
- Download: [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases)

### Modding API (per game)

| Game | Package | Role |
|------|---------|------|
| HS2 | **HS2API** (KKAPI for HS2) | Toolbar, Studio interop |
| KKS | **KKSAPI** | Toolbar, Studio interop |
| KK | **KKAPI** | Toolbar, Studio interop |

Most Better Repacks already include these APIs. Sandbox plugins should load **after** the API (default `plugins` folder is usually fine).

## Module-specific dependencies

| Module | Additional requirement |
|--------|------------------------|
| **CopyScript** | Running **CopyScript HTTP server** on your PC (host/port configurable in the window) |
| **Timeline** | None required; many commands for **VNGE**, FashionLine, screenshot plugins, etc. need **modified plugin builds** (not shipped in this repo) |
| **Son Scale** | Optional: **Studio Better Penetration** for cleaner length integration |
| **Pose Browser (HS2)** | Optional: **HS2Heelz**, **HS2PE/Pose Tools** (PE compat), **HS2Wiki** (F3 help) |
| **Anim Browser (HS2)** | Optional: **HS2Wiki**; **hover preview** is HS2-only (embedded rig) |
| **All window modules** | Optional: **Configuration Manager** for BepInEx keyboard shortcuts |

## What you do **not** need

- No local path to `HS2_Data/Managed` — the project uses **NuGet IllusionLibs**
- No Visual Studio (build works with `dotnet` CLI)
- No all-in-one **and** split modules for the same feature at the same time

## Next step

→ [Installation](Installation)

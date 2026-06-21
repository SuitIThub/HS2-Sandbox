# Building from source

## Prerequisites

- **.NET SDK 6+** ([dotnet.microsoft.com](https://dotnet.microsoft.com/download))
- **`nuget.config`** in the repo root (IllusionLibs feed — Azure DevOps, IllusionMods)
- Network access for NuGet restore

> You do **not** need a local path to `HS2_Data/Managed`. References come from NuGet (`Directory.Build.props`).

## Quick build

```powershell
cd path/to/HS2-Sandbox

# Optional: file versions for release metadata
python .github/scripts/generate_plugin_versions_props.py

dotnet restore HS2-Sandbox.sln
dotnet build HS2-Sandbox.sln -c Release
```

Or interactively:

```powershell
.\build.ps1
# or build.bat
```

`build.ps1` offers: module picker, game picker, deploy to BepInEx, optional Studio launch.

## Solution

**`HS2-Sandbox.sln`** — 11 projects:

- 8 HS2 modules under `targets/HS2/`
- Pose Browser + Anim Browser for HS2, KKS, KK

All-in-one (`_deprecated/HS2SandboxPlugin.csproj`) is **not** in the solution.

## Build outputs

| Target | Path |
|--------|------|
| HS2 module | `targets/HS2/<Name>/bin/Release/HS2Sandbox.<Name>.dll` |
| KKS Pose/Anim | `targets/KKS/<Name>/bin/Release/KKSSandbox.<Name>.dll` |
| KK Pose/Anim | `targets/KK/<Name>/bin/Release/KKSandbox.<Name>.dll` |

Icons are copied next to the DLL automatically (`CopyToOutputDirectory=Always`).

## Deploy (development)

Typical target folders (configurable in `build.ps1` / build skill):

| Game | Deploy folder |
|------|---------------|
| HS2 | `D:/Honey Select/BepInEx/plugins/HS2-Sandbox/` |
| KKS | `…/BepInEx/plugins/KKS-Sandbox/` |
| KK | `…/BepInEx/plugins/KK-Sandbox/` |

After copying, launch **`StudioNEOV2.exe`** or **`CharaStudio.exe`**.

## NuGet feeds (`nuget.config`)

| Feed | Contents |
|------|----------|
| nuget.org | UnityEngine.Modules, IronPython, … |
| bepinex.dev | BepInEx.Core 5.4.21, Harmony |
| IllusionMods (Azure) | Assembly-CSharp, HS2API, KKSAPI, … |

**Restore fails?** → Check IllusionLibs feed access; run `dotnet restore` from repo root.

## MSBuild hierarchy

| File | Role |
|------|------|
| `Directory.Build.props` (root) | LangVersion, BepInEx/Harmony versions |
| `targets/HS2/Directory.Build.props` | `HS2` define, net472 |
| `targets/KKS/Directory.Build.props` | `KKS` define, net472 |
| `targets/KK/Directory.Build.props` | `KK` define, net35 |

## Visual Studio

1. Open `HS2-Sandbox.sln`
2. Select **Release**
3. **Build Solution** (`Ctrl+Shift+B`)

## Known build notes

| Note | Meaning |
|------|---------|
| **NU1603** | Version warnings — build usually still succeeds |
| **CS0618** | Deprecated KKAPI toolbar overloads — cosmetic |
| IronPython | Timeline project only (VNGE paths) |

## Adding a new module (maintainers)

1. Source under `src/<Module>/`
2. `targets/HS2/<Module>/Plugin.cs` + `.csproj`
3. Entry in `HS2-Sandbox.sln`
4. Entry in `.github/plugins.manifest.json`
5. Run `python .github/scripts/sync_workflow_rerelease_inputs.py`

→ [Contributing & CI](Contributing-and-CI) · [Architecture](Architecture)

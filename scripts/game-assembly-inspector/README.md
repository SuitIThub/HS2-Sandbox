# Game Assembly Inspector

Small dotnet tool to compare Illusion Studio types across **HS2**, **KK**, and **KKS** using the NuGet `IllusionLibs.*` references from this repo.

## Prerequisites

- `dotnet` SDK on PATH
- Game packages restored at least once (`dotnet build` on any PoseBrowser target)

## Quick start

From repo root:

```powershell
.\scripts\inspect-game-assemblies.ps1
```

Single game:

```powershell
.\scripts\inspect-game-assemblies.ps1 -Game KK
```

Simple-color / monocolor comparison (OCIChar, OCICharFemale/Male, OICharInfo, ChaControl):

```powershell
.\scripts\inspect-game-assemblies.ps1 -Game All -SimpleColor
```

Custom types and keywords:

```powershell
dotnet run --project scripts/game-assembly-inspector -c Release -- `
  --game KKS `
  --type Studio.OCIChar `
  --type ChaControl `
  --keywords "simple,draw,material"
```

Manual DLL paths:

```powershell
dotnet run --project scripts/game-assembly-inspector -c Release -- `
  --dll "C:\path\to\Assembly-CSharp.dll" `
  --unity-dir "C:\path\to\lib" `
  --type Studio.OCIChar
```

## Notes

- Each game loads in a **separate process** when using `inspect-game-assemblies.ps1 -Game All` (via sequential `dotnet run` calls).
- Unity dependencies are resolved from the matching `IllusionLibs.*.UnityEngine*` package `lib/` folder.
- Use `-Verbose` on the PowerShell wrapper to see assembly resolve paths.

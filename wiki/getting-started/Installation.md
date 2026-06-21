# Installation

## 1. BepInEx & API

1. Install **BepInEx 5** for your game if not already present.
2. Ensure **HS2API** / **KKSAPI** / **KKAPI** is in the `BepInEx/plugins/` folder.
3. Launch the game once so BepInEx creates its folder structure.

## 2. Download modules

Latest builds are on the [GitHub Releases page](https://github.com/SuitIThub/HS2-Sandbox/releases). Versions and direct URLs are also in [`versions.json`](https://github.com/SuitIThub/HS2-Sandbox/blob/main/versions.json).

### Recommended: Individual modules (split)

Copy only the DLLs you need, for example:

```
BepInEx/plugins/HS2-Sandbox/
  HS2Sandbox.PoseBrowser.dll
  pose-icon.png
  HS2Sandbox.AnimBrowser.dll
  anim-icon.png
```

Subfolders are fine — BepInEx loads recursively.

### Alternative: All-in-one (HS2, legacy)

**`HS2SandboxPlugin.dll`** bundles CopyScript, Timeline, Son Scale, Notebook, Pose Browser, SearchBarManager, and Workspace Tree Lock in **one** DLL.

→ See [All-in-one vs split modules](All-in-One-vs-Split-Modules) — do **not** load it together with split DLLs for the same features.

## 3. Icons (PNG)

Toolbar icons must sit **next to the matching DLL**:

| Icon file | Module |
|-----------|--------|
| `copy-icon.png` | CopyScript |
| `timeline-icon.png` | Timeline |
| `sonscale-icon.png` | Son Scale |
| `notes-icon.png` | Notebook |
| `pose-icon.png` | Pose Browser |
| `anim-icon.png` | Anim Browser |

Release builds copy icons to the output folder automatically. Missing icons mean no sidebar button.

## 4. Game-specific DLL names

| Game | Pose Browser | Anim Browser |
|------|--------------|--------------|
| HS2 | `HS2Sandbox.PoseBrowser.dll` | `HS2Sandbox.AnimBrowser.dll` |
| KKS | `KKSSandbox.PoseBrowser.dll` | `KKSSandbox.AnimBrowser.dll` |
| KK | `KKSandbox.PoseBrowser.dll` | `KKSandbox.AnimBrowser.dll` |

**Never** use the HS2 DLL in KKS/KK (or vice versa).

Details: [Supported games & modules](Supported-Games-and-Modules)

## 5. First launch

1. Start **Studio** (`StudioNEOV2.exe` or `CharaStudio.exe`).
2. Use the **left toolbar** — click icons for installed modules.
3. If something fails, check `BepInEx/LogOutput.log`.

### Modules without a sidebar button

| Module | Behavior |
|--------|----------|
| **SearchBarManager** | Search bars appear automatically on configured Studio panels |
| **Workspace Tree Lock** | Middle-click in the workspace tree — no separate window |

## 6. Configuration (optional)

- **BepInEx Configuration Manager** — UI scale, hotkeys, auto-capture delay, etc.
- Config files under `BepInEx/config/com.hs2.sandbox/` — see [Configuration & data files](Configuration-and-Data-Files)

## Checklist

- [ ] BepInEx 5 installed
- [ ] Correct game API present
- [ ] Correct DLL(s) for your game
- [ ] PNG icons next to DLLs
- [ ] No all-in-one + duplicate split modules
- [ ] CopyScript server running (only if using CopyScript)

→ Module manuals: [Home](Home)

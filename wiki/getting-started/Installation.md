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
  HS2Sandbox.AnimBrowser.dll
```

Subfolders are fine — BepInEx loads recursively. Toolbar icons ship inside each DLL — nothing extra to copy.

## 3. Game-specific DLL names

| Game | Pose Browser | Anim Browser |
|------|--------------|--------------|
| HS2 | `HS2Sandbox.PoseBrowser.dll` | `HS2Sandbox.AnimBrowser.dll` |
| KKS | `KKSSandbox.PoseBrowser.dll` | `KKSSandbox.AnimBrowser.dll` |
| KK | `KKSandbox.PoseBrowser.dll` | `KKSandbox.AnimBrowser.dll` |

**Never** use the HS2 DLL in KKS/KK (or vice versa).

Details: [Supported games & modules](Supported-Games-and-Modules)

## 4. First launch

1. Start **Studio** (`StudioNEOV2.exe` or `CharaStudio.exe`).
2. Use the **left toolbar** — click icons for installed modules.
3. If something fails, check `BepInEx/LogOutput.log`.

### Modules without a sidebar button

| Module | Behavior |
|--------|----------|
| **SearchBarManager** | Search bars appear automatically on configured Studio panels |
| **Workspace Tree Lock** | Middle-click in the workspace tree — no separate window |

## 5. Configuration (optional)

- **BepInEx Configuration Manager** — UI scale, hotkeys, auto-capture delay, etc.
- Config files under `BepInEx/config/com.hs2.sandbox/` — see [Configuration & data files](Configuration-and-Data-Files)

## Checklist

- [ ] BepInEx 5 installed
- [ ] Correct game API present
- [ ] Correct DLL(s) for your game
- [ ] CopyScript server running (only if using CopyScript)

→ Module manuals: [Home](Home)

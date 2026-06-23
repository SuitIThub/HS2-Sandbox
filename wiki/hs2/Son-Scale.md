# Son Scale

**DLL:** `HS2Sandbox.SonScale.dll` · **GUID:** `com.hs2.sandbox.sonscale` · **Game:** HS2 only

**Separate sliders** for overall size, length, and girth on the selected character's Son (member). Controls appear under **Manipulate → Chara → State** and in a dedicated sidebar window.

## Table of contents

1. [Opening](#opening)
2. [Features](#features)
3. [Studio Better Penetration (optional)](#studio-better-penetration-optional)
4. [Typical workflow](#typical-workflow)
5. [Troubleshooting](#troubleshooting)

## Opening

1. Install Son Scale
2. Select a **character** in the Studio workspace
3. Click **Son scale** on the left toolbar
4. Enable split scaling and adjust sliders in the Manipulate panel

## Features

| Component | Role |
|-----------|------|
| `SonScaleWindow` | Sidebar window |
| `SonScaleManipulateUi` | Injects into Manipulate → Chara → State |
| `SonScaleApplier` | Applies scaling to selected characters |
| Timeline integration | Bootstrap for timeline commands |

## Studio Better Penetration (optional)

Soft dependency on **Studio Better Penetration** (`com.animal42069.studiobetterpenetration`).

| Mode | Behavior |
|------|----------|
| **Without BP** | Legacy chain scaling (localPosition / axis + root XY girth) |
| **With BP** | Length/overall via `m_baseDanLength` during `SetDanTarget`; uniform girth on dan root |

Harmony ID: `com.hs2.sandbox.sonscale.betterpenetration`

Details: [Plugin compatibility](Plugin-Compatibility)

## Typical workflow

1. Select character in workspace tree
2. Open Son scale sidebar → enable feature
3. Adjust **overall**, **length**, **girth** in Manipulate panel
4. Changes apply to current Studio selection

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Nothing happens | No character selected; Son scale disabled |
| BP integration silent | BP missing or API changed — check log for `Son scale:` |

---

**Navigation:** [← SearchBarManager](SearchBarManager) · **Son Scale** · [Next: Workspace Tree Lock →](Workspace-Tree-Lock)

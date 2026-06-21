# Anim Browser — characters & options

![Characters priority pane and Options with UI scale](images/anim-browser/ab-14-characters-options.png)

> **Options** (left): UI scale **1.00×**, card **280 px**, **Hover animation preview** on, preview camera **Front-side (45°)**, pitch **10°**, thumbnail capture buttons greyed (**0 listed**). **Characters** (right): **Luna Clark**, **Seraphina Clark**, **John** with **f** / **m** slot tags.

## Characters pane

Top bar shows Studio selection: none / one name / *n* selected.

**Characters** panel sets **priority order** for multi-character apply:

- **Top = highest priority**
- Reorder with arrows; **Load characters from scene** syncs the list with workspace selection
- Decides who gets m/f roles on paired animations
- Saved in **`studio_character_priority.json`** (shared with Pose Browser under `com.hs2.sandbox`)

## Options panel

### UI & display

| Setting | Meaning |
|---------|---------|
| **UI scale** | Enlarges whole browser (4K/DPI); mirrors BepInEx → Anim Browser → UI scale |
| **Card size** | Minimum grid card / column width (80–320 px) |
| **Hide non-Studio animation lists** | Hide H-scene-only catalog entries (e.g. Group 101 / Category 2018); default **on** |

### HS2 hover preview

| Setting | Meaning |
|---------|---------|
| **Hover animation preview** | Live stick-figure in hovered grid card (embedded rig; no scene character needed) |
| **Preview camera angle** | Full frontal, front-side 45°, side 90°, rotating, or rotating with dwell at 0°/45°/90° |
| **Rotation speed** | Orbit speed for rotating modes (10–240 °/s) |
| **Camera pitch** | Vertical camera tilt (−90° … +90°) |

Not available on KKS/KK.

### Thumbnails

| Control | Meaning |
|---------|---------|
| **Capture thumbnails (*n* listed)…** | Screen-capture PNG for every visible entry in the current sub-category |
| **Capture missing only…** | Skip entries that already have a file in `anim_thumbnails/` |

See [Thumbnails](Thumbnails) for the overlay workflow and content-bar capture.

### Controls & shortcuts

| Setting | Meaning |
|---------|---------|
| **Group controls by proximity** | Merge playback UI only for characters within **3.5** world units (same animation group) |
| **Keyboard shortcuts** | Read-only — assign in Configuration Manager |

### Debug

| Control | Meaning |
|---------|---------|
| **Dissolve all groups…** | Remove all display groups and tree merges (confirmation) |
| **Dump preview skeleton data…** | Write embedded-rig diagnostics to `anim_preview_diagnostic.txt` (HS2 preview tuning) |

### BepInEx only (not in Options pane)

| Key | Meaning |
|-----|---------|
| **Auto translate names** | Translate animation and category names via XUnity Auto Translator when installed (default on) |

## Data files

| File / folder | Contents |
|---------------|----------|
| `anim_browser_options.json` | Window geometry (per grid/list view), panel state, card size, catalog filter, hover preview & camera, controls preferences |
| `anim_browser_groups.json` | Renames, grouped cards, tree merges, bucket aliases |
| `UserData/com.hs2.sandbox/anim_thumbnails/` | Captured grid PNGs (`s_…` singles, `g_…` groups) |
| `studio_character_priority.json` | Character priority list (shared) |
| `anim_preview_diagnostic.txt` | Optional HS2 preview debug dump |

Back up before hand-editing.

## Minimize

Main window can minimize to draggable **AB** chip. Geometry saved separately for grid and list view modes.

---

**Navigation:** [← Thumbnails](Thumbnails) · [Anim Browser](Anim-Browser) · [Next: Home →](Home)

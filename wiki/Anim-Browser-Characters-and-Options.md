# Anim Browser — characters & options

## Characters pane

Top bar shows Studio selection: none / one name / *n* selected.

**Characters** panel sets **priority order** for multi-character apply:

- **Top = highest priority**
- Reorder with arrows
- Decides who gets m/f roles on paired animations
- Saved in **`studio_character_priority.json`** (shared path under `com.hs2.sandbox`)

## Options panel

| Setting | Meaning |
|---------|---------|
| **UI scale** | Enlarges whole browser (4K/DPI); mirrors BepInEx Anim Browser → UI scale |
| **Card size** | Minimum grid thumbnail width |
| **Hide non-Studio animation lists** | Hide H-scene-only catalog entries (default on) |
| **Group controls by proximity** | Merge playback UI only for nearby characters |
| **Keyboard shortcuts** | Read-only — assign in Configuration Manager |
| **Dissolve all groups** | Remove all merges and display groups (confirmation) |

## Data files

| File | Contents |
|------|----------|
| `anim_browser_options.json` | Window geometry, view mode, panel state, preferences |
| `anim_browser_groups.json` | Renames, grouped cards, tree merges, bucket aliases |

Back up before hand-editing.

## Minimize

Main window can minimize to draggable **AB** chip. Geometry saved per view mode.

→ [Configuration & data files](Configuration-and-Data-Files) · [Anim Browser](Anim-Browser)

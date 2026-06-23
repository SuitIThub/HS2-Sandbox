# Configuration & data files

All persistent Sandbox data lives under:

```
<GameRoot>/BepInEx/config/com.hs2.sandbox/
```

The folder name is **`com.hs2.sandbox`** on all games (HS2, KKS, KK).

## BepInEx plugin configs (auto-generated)

One `.cfg` per plugin GUID, e.g.:

| File | Module |
|------|--------|
| `com.hs2.sandbox.posebrowser.cfg` | Pose Browser |
| `com.hs2.sandbox.animbrowser.cfg` | Anim Browser |
| `com.hs2.sandbox.sonscale.cfg` | Son Scale |

Use **Configuration Manager** or edit these files while Studio is closed.

## JSON / TSV data files

| File | Module | Format |
|------|--------|--------|
| `notebook.json` | Notebook | Hand-written JSON |
| `timeline.json` | Timeline | JSON |
| `persistent_vars.json` | Timeline | JSON (persistent variables) |
| `pose_browser_options.json` | Pose Browser | Hand-written JSON |
| `pose_browser_history.json` | Pose Browser | JSON |
| `pose_stash.json` | Pose Browser | JSON |
| `pose_browser_filter_presets.json` | Pose Browser | JSON |
| `pose_browser_character_config.json` | Pose Browser | JSON (Chars lists) |
| `studio_character_priority.json` | Shared | JSON (Anim Browser Chars) |
| `pose_tags.tsv` | Pose Browser | TSV v3 |
| `pose_groups.tsv` | Pose Browser | TSV v5 |
| `pose_items.tsv` | Pose Browser | TSV v5 |
| `anim_browser_options.json` | Anim Browser | Hand-written JSON |
| `anim_browser_groups.json` | Anim Browser | JSON |
| `anim_preview_diagnostic.txt` | Anim Browser | Text (HS2 preview debug) |

### Legacy (one-time migration)

| File | Notes |
|------|-------|
| `pose_tags.json` | Imported once into TSV if present |
| `pose_groups.json` | Imported once into TSV if present |

## Game UserData (read/write via browsers)

| Path | Used by |
|------|---------|
| `UserData/studio/pose/` | Pose Browser library root |
| `UserData/com.hs2.sandbox/anim_thumbnails/` | Anim Browser captured grid PNGs |
| `UserData/chara/` | Timeline `ReplaceCharaCardCommand` |
| `UserData/coordinate/female\|male/` | Timeline `LoadCoordinateCardCommand` |
| `UserData/Timeline/` | Default timeline folder hint |
| `UserData/CopyScript/` | CopyScript batch path reference |

## Shared BepInEx config (SearchBarManager)

Section **`Search Bars`** → **`Additional Parent Paths`**

- One Unity `GameObject` path per line
- Bound in the SearchBarManager plugin config
- See [SearchBarManager](SearchBarManager)

## Backup recommendations

Before manual edits or major imports:

1. Copy the whole `com.hs2.sandbox/` folder
2. Back up `UserData/studio/pose/` when moving large libraries

## Pose Browser config keys (BepInEx)

Section **`Pose Browser`** (examples):

| Key | Meaning |
|-----|---------|
| UI scale | Window/card scaling |
| Card width | Grid minimum card width |
| Items per page | Pagination (0 = all) |
| Auto capture delay | Thumbnail auto-capture pause |
| History max entries | Per-character undo depth |
| Freeze animation on apply | Pause anim when applying pose |
| Compact thumbnail width | List/Mini layouts |

Full list: Configuration Manager → Pose Browser.

## Anim Browser config keys (BepInEx)

Section **`Anim Browser`**:

| Key | Meaning |
|-----|---------|
| UI scale | Whole-browser scaling |
| Card column width (px) | Grid card minimum width |
| Auto translate names | XUnity Auto Translator for animation/category names |

### `anim_browser_options.json` (selected keys)

| Key | Meaning |
|-----|---------|
| `viewMode` | Grid vs list |
| `gridWindow*` / `listWindow*` | Separate window geometry per view |
| `cardCellSize` | Grid card width |
| `hideNonStudioCatalogAnimations` | Filter H-scene-only catalog entries |
| `enableHoverPreview` | HS2 stick-figure hover preview |
| `previewCameraMode` | 0 = frontal … 4 = rotating with dwell |
| `previewCameraRotateSpeed` | Orbit speed (°/s) |
| `previewCameraPitch` | Camera vertical tilt |
| `controlsGroupByProximity` | Merge playback boxes by world distance |
| `controlsPreferUndocked` | Open Controls floating by default |
| `showControlsPane` / `showCharacterConfigPane` / … | Docked panel visibility |

→ [Keyboard shortcuts](Keyboard-Shortcuts) · [Anim Browser — options](Characters-and-Options) · [Pose ZIP format](Pose-ZIP-Format)

# Pose Browser — options & data

![Options pane and plugin compatibility section](images/pose-browser/pb-17-options-pane.png)

> **Options** panel: UI scale **1.00×**, card width **280 px**, relative-layout toggles, and **HS2Heelz** tag rules (**Sitting** → heels off, **Standing** → heels on).

## Options panel

| Setting | Meaning |
|---------|---------|
| **Card width** | Minimum card width; grid fills row |
| **Items per page** | 0 = no pagination |
| **Apply stored relative positions** | Global toggle for group layout on apply |
| **Adjust for body height** | Scale saved Y offsets; needs relative positions on |
| **Keyboard shortcuts** | Read-only list — assign in Configuration Manager |
| **Select all filtered / Deselect all** | Bulk selection in current filtered list |
| **Plugin compatibility** | **HS2Heelz** (HS2 only); **HS2PE** / **KKPE** embed toggle when the game’s PE plugin is detected |

## Data files

All under `BepInEx/config/com.hs2.sandbox/`:

| File | Contents |
|------|----------|
| `pose_browser_options.json` | Layout tier, window rects, sort, global toggles |
| `pose_tags.tsv` | Per-pose tags and favorites |
| `pose_groups.tsv` | Groups, tags, offsets, heights |
| `pose_items.tsv` | Workspace items per pose |
| `pose_browser_character_config.json` | Chars priority lists |
| `pose_stash.json` | Stash entries |
| `pose_browser_history.json` | Per-character undo/redo |
| `pose_browser_filter_presets.json` | Saved filter presets |

Prefer backups before hand-editing TSV while Studio runs.

## Save Pose (top bar)

Prompts for name; writes to:

- Selected folder when browsing a folder
- **Pose root** when **All poses** or **★ Favorites** active

## History

**Undo / Redo / History** — per-character snapshots when applying from browser. Max entries configurable in BepInEx.

---

**Navigation:** [← Thumbnails](Thumbnails) · [Pose Browser](Pose-Browser) · [Next: Anim Browser →](Anim-Browser)

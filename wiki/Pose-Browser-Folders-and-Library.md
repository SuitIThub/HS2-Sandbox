# Pose Browser — folders & library

## Paths

| Path | Role |
|------|------|
| `UserData/studio/pose` | Pose library root (game UserData) |
| `BepInEx/config/com.hs2.sandbox/` | Tags, groups, options, history, stash |

## Tree modes

| Mode | Behavior |
|------|----------|
| **All poses** | Recursive — every pose in subfolders (subject to filters) |
| **★ Favorites** | Virtual view of all favorited poses library-wide |
| **Root only** | Files directly in pose root only |
| **Folder node** | Click folder **name** for non-recursive view of that folder |

**Save Pose** while **★ Favorites** or **All poses** is active targets the **pose root**.

## Refresh & expansion

- **↻** — rescan filesystem (after external folder changes)
- **► / ▼** — expand/collapse folders (cosmetic; does not change grid until you click a folder mode)

## Footer actions (under tree)

| Scope | Actions |
|-------|---------|
| **Library root** | **New folder…**; **Export library tree…** (Full layout only) |
| **Selected folder** | **Rename…**, **New folder…**, **Delete folder…** (empty only); **Export branch…** (Full) |

Delete folder only works when **empty**. Errors show in red in the footer.

## Move / copy destination mode

After **Move…** or **Copy…**:

1. Footer shows **Apply** / **Cancel** at top
2. Click **📁 Root only** or a **folder name** as destination
3. Highlighted row = destination (grid does not reload during pick)
4. **All poses** is grayed until Apply/Cancel
5. **Import** uses the same footer flow

→ [Pose Browser](Pose-Browser) · [Import/export ZIP](Pose-Browser-Import-Export-ZIP)

# Pose Browser — pose stash

Temporary **FK/IK clipboard** — does not create library files. Separate from **History** (automatic per-character undo).

## Opening

| Control | Behavior |
|---------|----------|
| **Stash** (top bar / character row) | Toggle stash UI; remembers docked vs float |
| **Docked pane** | Closes when main Pose Browser closes |
| **Float** | Independent window |
| Hotkey **Toggle undocked pose stash** | Toggle floating stash (works when browser closed) |

Floating window: move (title bar), resize (◢). Geometry in **`pose_browser_options.json`**.

## Capture & apply

1. Select **exactly one** character → **Stash selected character**
2. Entries: `Character name  yyyy-MM-dd HH:mm:ss` (newest first)
3. FK/IK pose data only (not world position/rotation)
4. Select one or more characters → **click entry** to apply to all
5. **Auto-delete after apply** — optional

## Delete

- **x** on row → Yes/No
- **Clear entire stash** → confirmation

## Persistence

| File | Contents |
|------|----------|
| `pose_stash.json` | Entries (base64 blobs), auto-delete flag |
| `pose_browser_options.json` | Float rect, stashPreferUndocked |

→ [Keyboard shortcuts](Keyboard-Shortcuts) · [Pose Browser](Pose-Browser)

# Pose Browser — pose groups

A **pose group** is a named set of library poses in **`pose_groups.tsv`**, shown as a bordered grid segment with header (`▦` name + optional group tags).

## Creating & editing

1. Select **two or more ungrouped** poses
2. **Group…** → enter name
3. **Ungroup** removes membership (files stay on disk)
4. Click **group header** for entity actions: rename, tags, export, **Apply to characters…**, **Group thumbnails…**, layout save/clear

## Selection modes

| Mode | How | Used for |
|------|-----|----------|
| **Group entity** | Click **group header** | Rename, tags, export, multi-apply, thumbnails, layout |
| **Pose members** | Card checkboxes | Tag, move, copy, delete, partial export |

## Relative positions

Optional layout when applying a **whole group** with **Apply to characters…**:

- **Anchor** = first pose in display order (not moved on apply)
- Other poses use saved offset + rotation vs anchor
- **Adjust for body height** scales Y from saved body-height ratios
- Toggles: group bar + **Options** (global)
- Stored in **`pose_groups.tsv`** and v5+ ZIP metadata

**Save positions…** requirements:

- Last apply was this group (re-apply if needed)
- Character count = pose count in group
- Gender pairing matches **Male** / **Female** tags + **Chars** lists
- Not during import preview

Workflow: apply group → arrange characters → **Save positions…**

## Group thumbnails

**Group thumbnails…** on group bar:

- Apply all poses once with multi-character rules
- Capture one PNG per member; non-focus characters in Studio **simple color**
- Same character count and gender rules as multi-apply

→ [Thumbnails](Pose-Browser-Thumbnails) · [Multi-character apply](Pose-Browser-Multi-Character-Apply)

## Move / copy / export

- **Move… / Copy…** — ungrouped poses, or **one full group** (all members)
- **Export…** from group bar includes group metadata (offsets, heights when saved)

→ [Pose Browser](Pose-Browser)

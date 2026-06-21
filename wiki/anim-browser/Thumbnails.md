# Anim Browser — thumbnails

![Thumbnail capture overlay with green frame over the animation](images/anim-browser/ab-15-thumbnail-capture.png)

> **Character → Walking & Running**: **Walking 1** applied to **Luna Clark** in the viewport; green crop frame with dimmed surround — **Capture 1 / 1: Walking 1** bar with **Capture**, **Skip**, **Auto-capture**, and **Cancel** (from **Options → Capture thumbnails…** or **Capture thumbnail…** in the content bar).

Grid cards can show **captured PNG thumbnails** (static) or, on **HS2**, a **live stick-figure hover preview** when enabled. Captured images are stored separately from the game catalog — animation bundles are read-only.

## Stored thumbnails

| Location | Role |
|----------|------|
| `UserData/com.hs2.sandbox/anim_thumbnails/` | One PNG per animation or grouped card (`s_…` / `g_…` keys) |

When a stored thumbnail exists, the grid shows it on the card. **Hover preview** (HS2, when enabled) replaces the static image while the cursor stays on the card.

Placeholders from the catalog are used when neither a stored PNG nor hover preview is available.

## Capture from Options

**Options → Thumbnails**

| Button | Effect |
|--------|--------|
| **Capture thumbnails (*n* listed)…** | Capture every visible entry in the current sub-category |
| **Capture missing only…** | Skip entries that already have a PNG on disk |

For each queued entry:

1. The animation is **applied to the selected Studio character(s)**
2. A **green capture frame** overlay appears over the viewport
3. **Capture** saves a 256×256 PNG and advances; **Skip** leaves that entry unchanged
4. **Auto-capture** chains the rest (2 s delay between shots)
5. **Cancel** exits; already saved files are kept

Hover preview is suspended while capture runs.

## Capture from selection

1. Check **checkboxes** on one or more cards (or one grouped card)
2. **Capture thumbnail…** / **Capture thumbnails…** in the content action bar below the grid

Same overlay workflow as Options capture, but only for the selected entries.

## HS2 hover preview (alternative to capture)

**Options → Hover animation preview** — live embedded-rig stick figures in the card thumbnail while hovering (no scene character required). Configure **Preview camera angle**, **Rotation speed**, and **Camera pitch** under the same toggle.

Not available on KKS/KK builds.

---

**Navigation:** [← Review panel](Review-Panel) · [Anim Browser](Anim-Browser) · [Next: Characters & options →](Characters-and-Options)

# Anim Browser — browsing & search

![Grid view of Walking and Running animations](images/anim-browser/ab-05-grid.png)

> **Grid** view, **Walking & Running**: thumbnail cards for **Running 1** / **Running 2** and **Walking 1**–**Walking 4** (umbrella on **Walking 3**, serving tray on **Walking 4**); **Walking 1** selected.

![List view of the same Walking and Running category](images/anim-browser/ab-05-list.png)

> **List** view, same category: eight named rows (**Running 1** … **Walking 6**); **Walking 1** selected; top bar shows **List** instead of **Grid**.

![Search highlighting matching animation names](images/anim-browser/ab-06-search-highlight.png)

> Search **Walk** under **hooh Animations 2020 → Actions**: three matches (**Happy Walk**, **Walk**, **Walking**) with empty placeholder cards; **1 selected**.

## Category tree

Structure: **groups → sub-categories → animations**

| Control | Action |
|---------|--------|
| **► / ▼** | Expand/collapse group |
| **Click sub-category** | Show animations in main panel |
| **↻** | Reload catalog from Studio (progress bar during load) |
| **Ctrl+click** | Multi-select tree nodes (for merges) |

Tree shows merged nodes and residual entries from partial unmerges.

## Grid vs list

| View | Best for |
|------|----------|
| **Grid** | Visual browsing; card size in Options; stored PNGs or HS2 hover preview on thumbnails |
| **List** | Scanning names quickly; HS2 hover preview on row hover |

Each view saves its own window geometry.

## Search

Filters selected sub-category by animation name. If **sub-category name** matches, its animations show too (dimmed when only category matched). Clear search box to reset.

Category and animation names can be **auto-translated** when XUnity Auto Translator is installed (BepInEx → Anim Browser → **Auto translate names**, default on).

## Thumbnails on cards

Grid cards show, in order of preference:

1. **HS2 hover preview** while the cursor is on the card (if enabled)
2. **Captured PNG** from `UserData/com.hs2.sandbox/anim_thumbnails/` (if present)
3. Catalog placeholder image

Capture workflow: [Thumbnails](Thumbnails).

## Hide non-Studio lists

Options → **Hide non-Studio animation lists** (default **on**) hides odd H-scene-only entries (e.g. Group 101 / Category 2018). Turn off to see everything.

---

**Navigation:** [← Getting started](Getting-Started) · [Anim Browser](Anim-Browser) · [Next: Applying animations →](Applying-Animations)

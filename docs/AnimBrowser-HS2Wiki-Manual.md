# HS2 Sandbox — Anim Browser manual (HS2Wiki)

This document is the **authoritative long-form manual** for the Anim Browser. A shorter version appears in-game via the **Help** button on the Anim Browser window. When [HS2Wiki](https://github.com/SuitIThub/HS2Wiki) is installed, the same material is exposed under **HS2 Sandbox → Anim Browser** in the wiki (**F3** by default), with cross-page navigation, rich text, and an embedded toolbar icon.

Pages are registered via reflection in `src/Core/AnimBrowserWikiRegistration.cs`, so the module still loads if HS2Wiki is absent. The in-game Help pane (`src/AnimBrowser/AnimBrowserWindow.Help.cs`) carries the concise version and links into these pages.

> **Design split:** the in-game **Help** panel is the *quick reference* — just enough to use the Anim Browser well. This wiki manual is the *complete* guide.

---

## 1. What the Anim Browser is

The Anim Browser is a Studio utility that:

- Lists **every animation Studio knows about** in a **category tree** (groups → sub-categories → animations) with a **thumbnail grid** or a compact **list**.
- **Applies** an animation to the **character(s) currently selected in Studio** with one click.
- Provides **playback controls** (speed, pause, scrub, force loop, restart) for the applied animation, docked or as a floating window.
- Lets you **reorganize** the catalog non-destructively: **rename** anything, bundle related animations into one **grouped card**, and **merge** sub-categories or whole groups.
- Remembers your selection priority for **multi-character apply**.
- Persists your organization and preferences in JSON under the BepInEx config folder.

It is opened from the Studio **left toolbar** (anim icon).

All reorganization is a **display layer** over the real Studio catalog. The catalog is never modified, and every merge / group / rename can be undone.

---

## 2. Layout overview

| Area | Role |
|------|------|
| **Top bar** | Minimize / Close, **Grid/List** view toggle, **Controls**, **Options**, **Help**, search box, character summary |
| **Category tree (left)** | Groups and sub-categories; expand/collapse; **↻** refresh; tree action bar (Rename / Merge / Unmerge) |
| **Main panel** | Thumbnail grid or list of the selected sub-category; content action bar (Group / Ungroup / Rename / Clear) |
| **Controls pane** | Playback controls for the selected character(s); docked or floating |
| **Characters pane** | Priority list for multi-character apply |
| **Options pane** | UI scale, card size, display & control toggles, hotkeys, reset |
| **Help pane** | Short in-game reference + links to this wiki |
| **Review pane** | Opens during a group/merge to confirm and adjust before applying |

Panels dock in a chain to the right of the main window and follow it when it moves. The main window can be **moved** (title bar) and **resized** (bottom-right **◢** grip), and **minimized** to a small draggable **AB** chip. Geometry and which panels are open are saved in `anim_browser_options.json`.

---

## 3. Getting started

1. In the Studio workspace tree, **select the character(s)** you want to animate.
2. In the Anim Browser, click a **sub-category** on the left to list its animations.
3. **Click an animation** — it is applied to every selected character at once.

**If nothing happens on click:** you have no character selected in Studio, or the selected object is a prop/accessory (these are ignored). The character summary in the top bar shows the current target(s).

---

## 4. Category tree

The left pane mirrors Studio's animation catalog: **groups** contain **sub-categories**, and only sub-categories contain animations.

- **► / ▼** — expand or collapse a group (groups start collapsed the first time the catalog loads).
- **Click a sub-category** — show its animations in the main panel.
- **↻** — reload the catalog from Studio (after installing mods or adding animations). A background progress bar shows catalog/tree/cache phases while loading.
- **Ctrl+click** — add/remove nodes from a multi-selection (used by the merge actions).

The tree also shows your **merged** nodes and any **residual** entries left when you pull part of a merge back out.

---

## 5. Views and search

### 5.1 Grid vs list

The **Grid / List** button switches between a thumbnail grid and a compact text list:

- **Grid** — a preview image per animation; size set by **Card size** in Options.
- **List** — faster to scan when you know the names; no thumbnails.

Each view remembers its own window size/position.

### 5.2 Search

Type in the search box to filter the selected sub-category by animation name. Matching animations are highlighted; if a **sub-category name** matches, its animations are shown too (dimmed when only the category matched). Clear the box to show everything again.

---

## 6. Applying animations

Clicking an animation applies it to **every** character currently selected in Studio.

### 6.1 Grouped cards

Some tiles are **grouped cards** that bundle related animations under one preview. Small buttons on the card pick which one to apply:

- **m / f** (and **m2, f2**…) — the male / female version, or each participant in a paired animation.
- **in / loop / out** — the intro, looping, or outro part of a sequence.
- **1 / 2 / 3**… — numbered variants when there is no gender or phase to label.

Grouped cards come from your own grouping (see §8) or from merge proposals (see §9).

### 6.2 Selecting without applying

Each card has a small **checkbox** in the corner. Use it to select animations (for grouping) without applying them. The content action bar shows how many are selected and the available actions.

---

## 7. Playback controls

Open the **Controls** panel (top bar). It shows content once a selected character has an animation.

| Control | Effect |
|---------|--------|
| **Speed** | Slider or typed value; slow down / speed up. |
| **Pause / Play** | Freeze and resume playback. |
| **Time** | Scrub to a specific moment in the clip. |
| **Force loop** | Keep a one-shot animation repeating. |
| **Restart animation** | Replay the current animation from the start. |
| **Restart all in scene** | Replay every applied animation in the scene. |
| **Show items** | Reveal the individual animations loaded on the character. |

### 7.1 Docked or floating

The Controls panel can be **docked** beside the main window or **floated** as an independent window. The floating panel stays usable **even when the main Anim Browser window is closed**, so you can keep just the playback controls on screen. There is an optional **keyboard shortcut** to toggle the floating controls (assign in BepInEx).

### 7.2 Grouping controls by proximity

When several characters share an animation, their controls merge into one box. The Options toggle **Group controls by proximity** only merges characters that are physically close together in the scene.

---

## 8. Grouping animations into one card

Bundle related animations into a single tile.

1. Tick the **checkbox** on two or more animation cards.
2. Click **Group selected…** in the content action bar.
3. The **review panel** opens (see §10) — confirm the bundle and adjust each animation's **gender** and **phase** role.
4. **Confirm**. The animations now share one card with role buttons (§6.1).

**Ungroup:** select a grouped card and click **Ungroup** to split it back into separate animations. The animations themselves are never deleted; ungrouping only removes the card.

Role inference (gender m/f, phase in/loop/out) is guessed from names and is always editable in the review.

---

## 9. Merging categories & groups

Merging reshapes the **category tree** so related content lives together. It is a non-destructive display layer — nothing is deleted, and everything can be undone. Select tree nodes (Ctrl+click for several), then use the buttons in the **tree action bar**.

Every merge opens the **review panel** (§10) before anything is applied.

### 9.1 Merge categories (sub-categories of one group)

- Select two or more sub-categories of the **same group**, then **Merge categories…**. They become a single sub-category node.
- Select an existing merged entry plus another sub-category to **extend** the merge.
- Select two merged entries to combine them into one.

If the button is **disabled**, hover it for the reason. The most common one: the selected sub-categories are in **different top-level groups**, which requires a group merge first (see §9.2). This is shown as a clear message instead of doing nothing.

### 9.2 Merge groups (whole top-level groups)

- Select two or more **groups**, then **Merge groups…**. Sub-categories with the same name are lined up into shared buckets automatically.
- Select a merged group plus another group and use **Add to group merge…** to add it **without** un-merging and rebuilding.

### 9.3 Joining & splitting sub-categories (inside a group merge)

Inside a merged group, sub-categories from the different source groups can be combined even when their names differ:

- **Join subcategories…** — select the sub-categories that should be one entry (e.g. "Cowgirl" and "Cow girl"). They merge into a single sub-category within the group merge.
- **Split subcategories** — on a joined entry, separates the parts again; they remain inside the merged group.

If the selected sub-categories all belong to the **same** source group, the action is treated as a real **category merge** instead of a name-bucket join — the result is the same single entry, and the review pairs the animations normally.

### 9.4 Undoing merges

- **Unmerge** — removes a merge and restores the original groups / categories.
- **Unmerge subcategory** — pulls a single sub-category back out of a group merge; the rest stays merged. The pulled-out content reappears under its original group.
- **Dissolve all groups** (Options) — removes every display group and tree merge you have created (full reset).

### 9.5 Renaming

Select any node — group, sub-category, merged entry, grouped card, or single animation — and use **Rename…** (tree action bar, or the content action bar for cards/animations). Names are remembered and only change what you see; the original Studio name is never lost and is still searchable via the tooltip.

---

## 10. The review panel

Whenever you group animations or merge categories / groups, a **review panel** docks to the right so you can check and adjust the result **before** it is applied. Nothing touches your saved organization until you press **Confirm**; **Cancel** discards everything.

| Control | Effect |
|---------|--------|
| **Gender button** (m / f / m2…) | Set or correct each animation's participant/gender role. |
| **Phase button** (in / loop / out / —) | Set or correct the sequence phase. |
| **Skip** | Keep that single animation at its original category instead of grouping it. |
| **Skip all** | Do not create this proposed card; keep its animations where they are. |
| **As singles** | Show these animations individually inside the merged category, without bundling them into a card. |
| **Restore group** | Undo a Skip-all / As-singles choice for that proposal. |
| **Confirm** | Apply the whole review at once. |
| **Cancel** | Discard the review; no changes are made. |

### 10.1 Sections

Proposed cards are grouped under **headings** by their original sub-category. Click a heading to collapse it while reviewing a long list.

### 10.2 Empty review

An **empty** review means no animations could be auto-bundled into cards — the merge / join itself is still valid. Click **Confirm** and the categories are combined; you simply get no grouped cards. (When you join sub-categories whose contents *can* pair up — e.g. a male sub-category and a female sub-category — the review applies the pending join first, so those cards do appear.)

---

## 11. Characters & priority

The character section in the top bar summarizes Studio selection (none / one name / *n* selected). Props and accessories are ignored.

Open the **Characters** panel to set a **priority order** used when several characters are selected at once:

- **Top = highest priority**; reorder with the arrows.
- Priority decides which character receives which role of a paired (e.g. male/female) animation.
- The list is saved between sessions.

---

## 12. Options panel

| Setting | Meaning |
|---------|---------|
| **UI scale** | Enlarges the whole browser (text, buttons, panels, cards) for 4K / high-DPI. Same value as BepInEx → Anim Browser → UI scale. |
| **Card size** | Minimum width of grid thumbnails; the grid adds columns / stretches cards to fill the row. |
| **Hide non-Studio animation lists** | Hides odd *Group 101 / Category 2018* entries that come from H-scene-only lists. **On by default**; turn off to see everything. |
| **Group controls by proximity** | Only merge playback controls for characters within a small world-space radius of each other. |
| **Keyboard shortcuts** | Read-only overview; assign keys in BepInEx Configuration Manager → **Anim Browser · Keyboard shortcuts**. |
| **Dissolve all groups** | Remove every display group and tree merge you have made (reset; confirmation required). |

---

## 13. Data files

All under `BepInEx/config/com.hs2.sandbox/`:

| File | Contents |
|------|----------|
| **`anim_browser_options.json`** | Window geometry per view mode, panel widths, view mode, card size, UI preferences, which panels are open. |
| **`anim_browser_groups.json`** | Your renames (display-name overrides), grouped cards, and category / group merges (with excluded sources and subcategory bucket aliases). |

These are plain files; keep a backup before editing by hand.

---

## 14. HS2Wiki integration (for users)

1. Install **BepInEx** and **[HS2Wiki](https://github.com/SuitIThub/HS2Wiki/releases)** per its instructions.
2. Start Studio (HS2 Sandbox / the Anim Browser module loads its pages on startup).
3. Press **F3** (or your configured wiki key).
4. Open category **HS2 Sandbox → Anim Browser**:
   - **Overview** — navigation hub with the toolbar icon.
   - Pages: **Getting started**, **Browsing & search**, **Applying animations**, **Playback controls**, **Characters & priority**, **Grouping animations**, **Merging categories & groups**, **The review panel**, **Options & data files**, **Troubleshooting**.

If pages do not appear, confirm HS2Wiki is installed and restart Studio (the startup log shows *"Registered Anim Browser pages with HS2Wiki"*).

---

## 15. HS2Wiki integration (for maintainers)

- Registration lives in **`src/Core/AnimBrowserWikiRegistration.cs`**.
- Uses reflection: `HS2Wiki.WikiPlugin, HS2Wiki` → static **`PublicAPI`** → **`RegisterPage(string, string, Action)`**, **`OpenPage`**, **`OpenImage`**. No compile-time reference to HS2Wiki; safe when the assembly is missing.
- `TryRegister` is called from the Anim Browser plugin's `Start()` and is idempotent (`_registerSucceeded`).
- The in-game Help pane calls `AnimBrowserWikiRegistration.DrawHelpWikiSection` to link into the wiki (or prompt to install it).

To add a new wiki page:

1. Add a `DrawWiki…` method using `GUILayout` and rich-text labels.
2. Add a page constant and an `InvokeRegister(...)` call in `TryRegister`.
3. Link from other pages with `NavButton`.
4. Mirror the change in this markdown file so in-repo docs stay in sync.

---

## 16. Troubleshooting

| Issue | Things to check |
|-------|------------------|
| Clicking an animation does nothing | Select a **character** in Studio (not a prop/accessory); check the top-bar character summary. |
| Empty / missing list | Clear the search box; check **Hide non-Studio animation lists** in Options; press **↻** to reload. |
| Merge button greyed out | Hover it for the reason; cross-group sub-category merges need a **group merge** first. |
| Review panel is empty | Normal when no cards can be auto-built — click **Confirm** and the merge still applies. |
| Wrong character in a paired animation | Set order in the **Characters** panel (top = first); use the **m / f** buttons on the card. |
| Want to start over | **Dissolve all groups** in Options removes all merges and grouped cards. |
| Playback controls vanish when the window closes | Only the **docked** controls close; use the floating window (or its hotkey) to keep them. |
| Wiki pages missing | HS2Wiki installed? Startup log shows the registration line? Restart Studio after installing HS2Wiki. |

---

## License / credits

HS2 Sandbox Anim Browser is part of this repository. HS2Wiki is a separate project under its own license; follow its documentation for wiki-specific behavior and updates.

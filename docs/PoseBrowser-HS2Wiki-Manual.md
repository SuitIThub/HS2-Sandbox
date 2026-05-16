# HS2 Sandbox — Pose Browser manual (HS2Wiki)

This document is the **authoritative long-form manual** for the Pose Browser. A shorter version appears in-game via **Help** on the Pose Browser window. When [HS2Wiki](https://github.com/SuitIThub/HS2Wiki) is installed, the same material is exposed under **HS2 Sandbox → Pose Browser** in the wiki (**F3** by default), with cross-page navigation, rich text, an embedded toolbar icon, and **OpenImage** integration for `pose-icon.png`.

For plugin authors, the public registration API is described in the [HS2Wiki README](https://raw.githubusercontent.com/SuIT-pub/HS2Wiki/refs/heads/main/README.md) (`RegisterPage`, `OpenPage`, `OpenImage`). This project registers pages via reflection so the sandbox still loads if HS2Wiki is absent.

---

## 1. What the Pose Browser is

The Pose Browser is a Studio utility that:

- Lists pose files under **`UserData/studio/pose`** in a folder tree and a thumbnail grid.
- Lets you **search**, **filter by tags**, **favorite** poses, and **apply** stored poses to **one or more selected characters** in Studio.
- Supports **saving** the current scene pose, **updating** an existing file, **renaming**, **moving**, **copying**, and **deleting** (with backup).
- Stores **tags and favorites** in a config-side database (**TSV**), separate from game files.
- Offers optional **thumbnail capture** for selected poses.

It is opened from the Sandbox **left toolbar** (pose icon) when using the full HS2 Sandbox plugin, or the standalone Pose Browser module.

---

## 2. Layout overview

| Area | Role |
|------|------|
| **Top bar (Poses)** | Search, regex, favorites-only, AND/OR tag mode, tag filter dropdown, **Save Pose** |
| **Character row** | Shows Studio character selection (`Character: …`); tooltip lists names when multiple |
| **Folders (left)** | Tree under `studio/pose`, **All poses** / **Root only**, refresh, folder footer actions |
| **Grid** | Thumbnail cards, selection, pagination (if enabled in Options) |
| **Bottom bar** | Actions on current selection (update, rename, tags, fav, thumbs, move, copy, delete) |
| **Window** | **Help** — compact manual; **Options** — card width, pagination, bulk select |

The main window can be **resized** from the bottom-right grip. **Help** and **Options** open as separate IMGUI windows docked to the right of the main window (Help shifts right if Options is also open).

---

## 3. Folders and library scope

### 3.1 Paths

- **Pose root**: `UserData/studio/pose` (internal type `PoseDataService` / `UserData.Path`).
- **Config** (BepInEx): `BepInEx/config/com.hs2.sandbox/`
  - `pose_browser_options.json` — card width, items per page.
  - `pose_tags.tsv` — tags and favorites (see §9).

### 3.2 Tree modes

- **All poses** — Recursive enumeration from the pose root: every supported pose file in subfolders appears in the grid (subject to search/tags).
- **Root only** — Only files directly in the pose root (no subfolders).
- **Folder node** — Click the folder **name** for a **non-recursive** view of that folder only.

### 3.3 Refresh and expansion

- **↻** rescans the filesystem tree (use after adding/removing folders outside the browser).
- **► / ▼** toggles expansion for folders that have children (cosmetic organization only; does not change which poses load until you click a folder or All poses / Root only).

### 3.4 Footer actions (under the tree)

| Scope | Actions |
|--------|---------|
| **Library root** (no folder selected, not in “All poses”) | **New folder…** — creates a child under the pose root. |
| **Selected folder** | **Rename…**, **New folder…**, **Delete folder…** (only if the folder is **empty**). |

Deleting a folder updates the tree and reloads the appropriate view. Errors (e.g. non-empty delete) appear in red in the footer.

### 3.5 Move / Copy destination mode

After **Move…** or **Copy…** in the selection bar, the folder panel enters destination-pick mode:

- **Apply** and **Cancel** appear at the **top** of the folder footer; the footer is visible even when **All poses** is the current grid scope (so you can pick a target without losing the wide listing).
- Click **📁 Root only** to target the pose root, or click a **folder name** to target that folder. The **highlighted** row is the destination (the grid does **not** reload while picking, so card selection stays valid).
- **All poses** is **grayed out** until you Apply or Cancel, to avoid reloading the grid mid-operation.
- **New folder…** still creates folders under the current footer scope (root or selected folder) and sets the new folder as destination when created.
- **Cancel** restores the normal tree + grid sync. **Rename** / **delete folder** clears an in-progress Move/Copy.

---

## 4. Search and filters

### 4.1 Text search

- Filters the **current grid source** (folder / root only / all poses) by display name and path context.
- Toggle **.\*** for **case-insensitive regex** (`RegexOptions.IgnoreCase`). Invalid patterns show a **red error line** under the search bar.

### 4.2 Favorites (★)

- The **★** toggle restricts the grid to poses marked as favorites (see **Fav Selected** in §7).

### 4.3 Tags

- **Tags (n)** opens a scrollable list of all tags known to the database.
- Toggling tags updates **active tag filters** (not the tags stored on files).
- **AND / OR** controls whether a pose must match **all** selected tags or **any** one.
- **Clear All** inside the dropdown clears **filters**, not assignments on disk.

Filters are reapplied whenever search text, regex mode, favorites-only, tag set, or AND/OR mode changes.

---

## 5. Grid, thumbnails, and pagination

### 5.1 Card layout

Each card shows:

- A **thumbnail** (or placeholder if none),
- A **name** (and **★** if favorited),
- A **tag line** when tags exist.

The layout algorithm uses a **minimum card width** from **Options**; the grid **adds columns** or **stretches** cards up to a maximum so extra horizontal space is not wasted.

### 5.2 Pagination

If **max items per page** in Options is **> 0**, the grid shows **one page** at a time with **◀ / ▶** and a `Page x/y · n poses` label. **0** means **all** filtered items in one scroll view.

### 5.3 Mouse and selection

| Input | Effect |
|--------|--------|
| **Checkbox** (on card) | Toggle selection **without** applying a pose. |
| **Left-click** thumbnail | Clear others, select this card, **apply pose** to selected Studio **characters**. |
| **Ctrl+click** | Toggle this card in the selection. |
| **Shift+click** | Range-select between last anchor and this index in the **filtered** list. |
| **Right-click** thumbnail | **Apply pose only**; selection unchanged. |

Apply uses whatever characters are currently selected in Studio **that count as characters** (accessories/props are ignored for that logic).

---

## 6. Character selection and pose apply

The **Character** row summarizes Studio selection:

- **none** — tooltip explains to select characters in Studio.
- **one** — name in the label; tooltip repeats it.
- **n selected** — count in the label; tooltip lists **newline-separated** names.

**Save Pose** and **apply** operations use **all** qualifying selected characters where the implementation supports it (multi-apply on load).

---

## 7. Selection bar (bottom)

Visible when **at least one** card is selected.

| Control | Purpose |
|---------|---------|
| **Selection: n** | Count of selected items |
| **Update Pose** | One item only: overwrite file from scene; optional thumbnail refresh |
| **Rename…** | One item: display name; optional rename file to safe name |
| **Tag Selected** | Mass add/remove tags via popup |
| **Fav Selected** | Toggle favorite flag for each selected item |
| **Thumbs…** | Start thumbnail capture overlay for selection |
| **Move…** / **Copy…** | Start destination pick: choose folder (or **Root only**) in the left tree — grid stays put so selection is kept — then **Apply** or **Cancel** in the folder footer. **All poses** is disabled while picking. |
| **Delete…** | Confirms; copies to **`!_AutoBackup`** then deletes files; refreshes data |
| **Deselect** | Clears selection on filtered list |

---

## 8. Save and update workflows

### 8.1 Save Pose (top bar)

- Prompts for a **name**.
- Writes into the **current save folder**:
  - **Selected folder** when browsing a specific folder.
  - **Pose root** when **All poses** is active (since no single folder is selected).

### 8.2 Update Pose (bottom bar, single selection)

- Overwrites the pose file from the current scene.
- Allows **keeping** or **regenerating** the thumbnail according to the overlay/UI flow.

---

## 9. Tags, favorites, and persistence

### 9.1 Primary store

- **`pose_tags.tsv`** in `BepInEx/config/com.hs2.sandbox/`
- Keys are **stable relative paths** into the pose library so renames/moves can update metadata via the browser.

### 9.2 Legacy import

- If **`pose_tags.json`** exists, it may be **imported once** (Unity `JsonUtility` limitations motivate the TSV migration).

### 9.3 In-browser behavior

- **Tag Selected** edits the database for all selected items.
- **Fav Selected** toggles favorite bits used by the **★** filter.

Details of the TSV format (headers, delimiters) are implementation-specific; treat the file as **do not hand-edit unless you know the format**, and prefer backups before manual edits.

---

## 10. Thumbnail capture

**Thumbs…** opens the thumbnail capture **overlay** (full-screen style interaction managed by `PoseThumbnailCapture`):

- Frame the shot and confirm or cancel from the controls shown in the overlay.
- On success, thumbnails refresh for affected poses.

If capture is cancelled, files stay unchanged.

---

## 11. Options panel

| Setting | Meaning |
|---------|---------|
| **Card width slider** | Minimum width per card; grid fills row / adds columns within min/max bounds. |
| **Items per page** | **0** = no pagination; **> 0** = cap and use page buttons. |
| **Select all filtered** | Select every item in the **current filtered** list. |
| **Deselect all** | Clear selection in that list. |
| **Close panel** | Closes Options; changes save through the same persistence path as sliders / Apply. |

---

## 12. HS2Wiki integration (for users)

1. Install **BepInEx** and **[HS2Wiki](https://github.com/SuitIThub/HS2Wiki/releases)** per its instructions.
2. Start Studio; ensure **HS2 Sandbox** (or the **Pose Browser** module) loads **after** HS2Wiki if you hit load-order edge cases (usually both work from default `plugins` folder).
3. Press **F3** (or your configured wiki key).
4. Open category **HS2 Sandbox → Pose Browser**:
   - **Overview** — navigation hub; click the **pose icon** to open **`OpenImage`** viewer if `pose-icon.png` sits beside the DLL.
   - Thematic pages: **Folders & library**, **Search & filters**, **Grid & selection**, **Pose files & actions**, **Thumbnails**, **Options & data files**.
   - **Advanced → Tag storage & migration** — TSV vs JSON.

Wiki pages use **IMGUI** and support **rich text**, **buttons** (`OpenPage` navigation), and **`OpenImage`** as in the upstream README.

---

## 13. HS2Wiki integration (for maintainers)

- Registration lives in **`Shared/PoseBrowserWikiRegistration.cs`**.
- Uses reflection: `HS2Wiki.WikiPlugin, HS2Wiki` → static **`PublicAPI`** → **`RegisterPage(string, string, Action)`**, **`OpenPage`**, **`OpenImage`**.
- No compile-time reference to HS2Wiki; safe when the assembly is missing.
- Duplicate registration on reload is avoided with **`_registerSucceeded`**; if wiki is added mid-session, a full game restart may be needed to see pages (same as most BepInEx plugins).

To add a new wiki page:

1. Add a `DrawWiki…` method using `GUILayout` and rich-text labels.
2. Call `InvokeRegister` with a category (`HS2 Sandbox/Pose Browser/…`) and unique page name.
3. Link from other pages with `NavButton` / `TryOpenWikiPage`.
4. Extend this markdown file so in-repo docs stay in sync.

---

## 14. Troubleshooting

| Issue | Things to check |
|--------|------------------|
| Empty grid | Folder mode (**All poses** vs folder); search/tags too strict; **↻** refresh. |
| Pose does not apply | **Character** row shows valid selection; click thumbnail (left or right) as intended. |
| Tags lost | Prefer **`pose_tags.tsv`** backup; avoid editing JSON/TSV while the game runs. |
| Wiki pages missing | HS2Wiki installed? Log line *“Registered Pose Browser pages with HS2Wiki”* on startup? Restart after installing HS2Wiki. |
| Image button does nothing | `pose-icon.png` must be next to the **same** DLL you run (or embedded — wiki **OpenImage** needs a **file path**, so the loose PNG next to the DLL is preferred). |

---

## License / credits

HS2 Sandbox Pose Browser is part of this repository. HS2Wiki is a separate project under its own license; follow its documentation for wiki-specific behavior and updates.

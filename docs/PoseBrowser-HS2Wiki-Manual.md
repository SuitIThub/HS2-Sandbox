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
- In **2.0.0** (all-in-one **HS2 Sandbox**): **Full / List / Mini** layouts, **Sort** panel (including **Last used** tracking), **★ Favorites** library view, docked tag filter window, optional **keyboard shortcuts** (BepInEx Configuration Manager), and expanded **`pose_browser_options.json`** (per-layout window geometry and sort).
- In **2.1.0+**: **v2/v3 pose pack ZIP** **Import…** / **Export…**, branch/tree exports, modder docs **`Modules/PoseBrowser/POSE_ZIP_FORMAT.md`** (stored-only ZIP requirement).
- In **3.0.0** (split **Pose Browser** module — same sources ship inside the all-in-one build):
- **Pose groups** — named sets of poses shown as grid segments, **group tags** for filtering, **pose_groups.tsv** persistence, and **groups[]** in v3 ZIP metadata.
- **Tag filter include/exclude** — tri-state per-tag filters; exclude dims grouped members instead of hiding whole groups.
- **Multi-character apply** — **Chars** priority lists (male/female), **Apply to characters…** for multiple poses or a whole group, driven by **Male** / **Female** pose tags and list order.

### 1.1 Version note

The **all-in-one** plugin’s `PluginVersion` may read **2.0.0** while the **Pose Browser module** reads **3.0.1**; both builds compile the same Pose Browser code path.

It is opened from the Sandbox **left toolbar** (pose icon) when using the full HS2 Sandbox plugin, or the standalone Pose Browser module.

---

## 2. Layout overview

| Area | Role |
|------|------|
| **Top bar (Poses)** | Search, regex, favorites-only, **Tags** (dock **Tag filter**: include/exclude per tag, AND/OR inside pane), **Sort**, **Save Pose**, **Import…** |
| **Character row** | Studio selection summary; **Chars** (priority lists); **Apply to characters…** when 2+ poses or one group entity is selected |
| **Folders (left)** | Tree under `studio/pose`, **All poses** / **★ Favorites** / **Root only**, refresh, folder footer actions |
| **Grid** | Thumbnail cards, selection, pagination (if enabled in Options); **import preview** replaces the grid while a ZIP is open |
| **Bottom bar** | Pose actions, **Grouping** (Group… / Ungroup), or **group entity** bar (rename, group tags, export group); import preview hints |
| **Window** | **View (Full/List/Mini)** — cycles compact layout modes; **Help** — compact manual; **Options** — card width, pagination, bulk select, hotkey listing |

The main window can be **resized** from the bottom-right grip. In **Full** layout, **Help**, **Options**, **Tag filter**, **Chars**, and **Sort** open as docked IMGUI panes to the right of the main window (laid out in a single chain so they do not overlap). Compact **List** and **Mini** modes hide those side panels and remember their own **position and size** in **`pose_browser_options.json`**. **List** keeps the folder tree with a text list of filtered poses (no thumbnails). **Mini** is a minimal strip with **Folder** / **Pose** navigation arrows and **Reapply**—see the in-game **Help** panel for the exact stepping order.

---

## 3. Folders and library scope

### 3.1 Paths

- **Pose root**: `UserData/studio/pose` (internal type `PoseDataService` / `UserData.Path`).
- **Config** (BepInEx): `BepInEx/config/com.hs2.sandbox/`
  - `pose_browser_options.json` — card width, items per page, **layout tier** (Full/List/Mini) with separate saved window rects, **sort mode** and direction.
  - `pose_tags.tsv` — per-pose tags and favorites (see §10).
  - `pose_groups.tsv` — pose groups (membership, names, group tags).
  - `pose_browser_character_config.json` — male/female priority lists for multi-character apply.

### 3.2 Tree modes

- **All poses** — Recursive enumeration from the pose root: every supported pose file in subfolders appears in the grid (subject to search/tags).
- **★ Favorites** — Virtual view of **all favorited poses** (library-wide, same file scope as All poses). Not a disk folder; **Save Pose** while this row is active targets the **pose root**.
- **Root only** — Only files directly in the pose root (no subfolders).
- **Folder node** — Click the folder **name** for a **non-recursive** view of that folder only.

### 3.3 Refresh and expansion

- **↻** rescans the filesystem tree (use after adding/removing folders outside the browser).
- **► / ▼** toggles expansion for folders that have children (cosmetic organization only; does not change which poses load until you click a folder or All poses / Root only).

### 3.4 Footer actions (under the tree)

| Scope | Actions |
|--------|---------|
| **Library root** (no folder selected, not in “All poses”) | **New folder…** — creates a child under the pose root. In **Full** layout: **Export library tree…** exports the whole library as a v2 branch ZIP. |
| **Selected folder** | **Rename…**, **New folder…**, **Delete folder…** (only if the folder is **empty**). In **Full** layout: **Export branch…** writes a v2 **tree-branch** ZIP of that subtree. |

Deleting a folder updates the tree and reloads the appropriate view. Errors (e.g. non-empty delete) appear in red in the footer. For **`manifest.json`**, **`metadata.json`**, and the **stored-only** ZIP requirement, see **`Modules/PoseBrowser/POSE_ZIP_FORMAT.md`**.

### 3.5 Move / Copy destination mode

After **Move…** or **Copy…** in the selection bar, the folder panel enters destination-pick mode:

- **Apply** and **Cancel** appear at the **top** of the folder footer; the footer is visible even when **All poses** is the current grid scope (so you can pick a target without losing the wide listing).
- Click **📁 Root only** to target the pose root, or click a **folder name** to target that folder. The **highlighted** row is the destination (the grid does **not** reload while picking, so card selection stays valid).
- **All poses** is **grayed out** until you Apply or Cancel, to avoid reloading the grid mid-operation.
- **New folder…** still creates folders under the current footer scope (root or selected folder) and sets the new folder as destination when created.
- **Cancel** restores the normal tree + grid sync. **Rename** / **delete folder** clears an in-progress Move/Copy.

- **Import** also uses this footer: after **Import…**, pick **Root only** or a folder, then **Apply** / **Cancel** at the top of the footer (see §8).

---

## 4. Search and filters

### 4.1 Text search

- Filters the **current grid source** (folder / root only / **All poses** / **★ Favorites**) by display name and path context.
- Toggle **.\*** for **case-insensitive regex** (`RegexOptions.IgnoreCase`). Invalid patterns show a **red error line** under the search bar.

### 4.2 Favorites (★)

- The **★** toggle restricts the grid to poses marked as favorites (see **Fav Selected** in §7).

### 4.3 Tags

- **Tags** opens a docked **Tag filter** window: search box, **AND / OR** for **include** rules, and a scrollable tag list.
- Click each tag to cycle: **neutral** → **+ include** → **− exclude** → neutral. The top-bar label shows counts, e.g. `Tags (+2 −1)`.
- **Include (+)** — a pose must match include rules (AND = every + tag; OR = any + tag). **Group tags** on a segment count for group-level filter tests.
- **Exclude (−)** — **ungrouped** poses with an excluded tag are **hidden**. Inside a **visible group**, all members **remain** in the grid; poses with excluded tags are **dimmed** and matching tag names render in **red** on the card.
- **Clear active filters** resets include/exclude only (not on-disk tag assignments).
- **Tag Selected** / **Group tags…** use a separate docked window in **assign** mode (not the filter pane).

### 4.4 Sort

- **Sort** opens a docked panel: **Last used** (updates when you apply a pose from the browser), **Last updated** / **Last created** (file timestamps), **Name**.
- First click on a row selects that criterion; clicking the **same** row again toggles ascending / descending (↑ / ↓).

Filters and sort work together: search/tags narrow the list; sort order applies to the filtered results.

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

### 5.4 Pose groups

A **pose group** is a named collection of library poses stored in **`pose_groups.tsv`**. Grouped poses appear in the grid inside a **bordered segment** with a header row (`▦` name, optional **group tag** line).

#### Creating and editing

1. Select **two or more ungrouped** poses (bottom bar **Grouping** section).
2. Click **Group…**, enter a name.
3. **Ungroup** removes selected poses from their groups (files stay on disk).
4. With a **group entity** selected (header click), use **Rename group**, **Group tags…**, or **Export group…** on the group action bar.

#### Two selection modes

| Mode | How | Used for |
|------|-----|----------|
| **Group entity** | Click the **group header** | Rename, group tags, export group, **Apply to characters…** (all members in display order) |
| **Pose members** | Card checkboxes / thumbnail selection | Tag selected, move, copy, delete, partial export |

- **Ctrl+click** / **Shift+click** on group headers work like pose selection (range within the filtered list).
- During **import preview**, the group header toggles **import checkboxes** for all members.

#### Filters and layout

- **Group tags** affect whether the **segment** passes search/tag filters; **pose tags** still apply per card (e.g. Male/Female for multi-apply).
- Sort treats each group as a **block**; large groups may span multiple grid rows (continuation headers).

#### Move / copy / export

- **Move…** / **Copy…** — ungrouped poses, or exactly **one full group** (every member selected).
- **Export…** — includes group metadata in v3 ZIP when a full group is in the selection; **Export group…** from the group bar exports that group alone.

### 5.5 Import preview (after **Import…**)

The grid lists poses **from the ZIP**, not from disk. **Thumbnail click** toggles whether each pose is **checked** for import (the checkbox and **Ctrl+click** / **Shift+click** behave like normal). The bottom bar shows **Cancel import** and reminds you to **Apply** in the folder footer once a destination is chosen.

---

## 6. Character selection and pose apply

The **Character** row summarizes Studio selection:

- **none** — tooltip explains to select characters in Studio.
- **one** — name in the label; tooltip repeats it.
- **n selected** — count in the label; tooltip lists **newline-separated** names.

Non-character Studio selections (props, accessories, etc.) are ignored.

### 6.1 Single-pose apply (thumbnail click)

- **Left-click** thumbnail — select one pose and **apply it to every** selected Studio character.
- **Right-click** thumbnail — apply without changing the grid selection.

This is independent of the **Chars** priority lists.

### 6.2 Multi-character apply

Use when you want **different poses on different characters** in one step (e.g. male/female pair poses, or a batch of untagged poses across a cast).

#### When **Apply to characters…** appears

| Selection | Button |
|-----------|--------|
| **2+** library poses (member checkboxes) | Yes |
| **One group entity** (header only, all members implied) | Yes |
| **1** pose only | No (use thumbnail apply) |
| Import preview | No |

You must also have **at least one character** selected in Studio.

#### Chars window (priority lists)

1. Click **Chars** (docked pane).
2. **Load characters from scene** — fills **Male** and **Female** columns from the scene.
3. **↑ / ↓** — priority within a column (**top = first**).
4. **⇄** — move slot to the other column; **✕** — remove from lists.
5. Persisted in **`pose_browser_character_config.json`**.

Only characters that are **both** in Studio selection **and** on a list (or unlisted but selected) participate; matching rules depend on pose tags below.

#### Assignment rules

Poses are processed in **list order** (grid **display order** for a group). **Each character receives at most one pose per apply** — a later pose never overwrites an earlier one on the same character.

**Male / Female pose tags** (case-insensitive on the **pose**, not the group):

| Pose tags | Target |
|-----------|--------|
| **Male** only | Next free character from the **Male** list (priority order) |
| **Female** only | Next free character from the **Female** list |
| Both or neither | Treated as **untagged** |

**Untagged poses:**

1. Build order: interleave lists by rank (1st male, 1st female, 2nd male, …), then selected characters not on either list.
2. **First pass:** pose 1 → first free slot, pose 2 → second, … Extra poses with no free character are **skipped**.
3. **Second pass:** selected characters still without a pose may receive one by **cycling** through the pose list (only if eligible for that pose’s gender tag).

#### Example scenarios

| Scenario | Result |
|----------|--------|
| Group: Male-tagged + Female-tagged pose, 1 male + 1 female selected | Each pose goes to the matching list’s next character |
| 5 untagged poses, 3 selected characters | First three priority characters get poses 1–3; poses 4–5 skipped |
| 2 untagged poses, 4 selected characters | Two posed in pass 1; pass 2 may assign poses 1–2 to the remaining two |
| 2 Male poses, 1 male on list | First pose applies; second skipped (no overwrite) |

**Save Pose** still uses the current folder rules; it does not use multi-character mapping.

---

## 7. Selection bar (bottom)

### 7.1 Library selection actions

Visible when **at least one** selected card refers to an **on-disk** pose in your library (not during ZIP import preview).

| Control | Purpose |
|---------|---------|
| **Selection: n** | Count of selected items |
| **Update Pose** | One item only: overwrite file from scene; optional thumbnail refresh |
| **Rename…** | One item: display name; optional rename file to safe name |
| **Group…** / **Ungroup** | Create a group from 2+ ungrouped poses, or remove membership |
| **Tag Selected** | Mass add/remove **pose** tags via assign window |
| **Fav Selected** | Toggle favorite flag for each selected item |
| **Thumbs…** | Start thumbnail capture overlay for selection |
| **Export…** | Save selected poses to a **v3 .zip** (tags, favorites, **groups** when fully selected) |
| **Move…** / **Copy…** | Ungrouped poses, or **one full group**; pick destination in the folder tree, then **Apply** / **Cancel** in the footer |
| **Delete…** | Confirms; copies to **`!_AutoBackup`** then deletes files; refreshes data |
| **Deselect** | Clears selection on filtered list |

### 7.2 Import preview mode

After **Import…**, the bar shows import-specific text and **Cancel import**. Choose poses in the grid, pick **Root only** or a folder in **Folders**, then **Apply** / **Cancel** at the **top** of the folder footer (**§3.5**). **Tree branch** packs create one new subfolder under the destination you select.

---

## 8. Import and export (ZIP v2 / v3)

Pose Browser reads and writes **`.zip`** packs with `manifest.json`, `metadata.json`, and pose binaries under **`poses/`**. **v3** adds optional **`groups[]`** in metadata (id, name, group tags, member paths). **v2** packs without groups still import. The runtime reader only accepts **compression method 0 (stored)**; archives produced with Deflate will **fail** to import—see **`Modules/PoseBrowser/POSE_ZIP_FORMAT.md`** for tool guidance.

### 8.1 Import… (top bar)

1. Open a compatible **`.zip`**.
2. The **preview grid** lists pack entries; select which poses to import.
3. Navigate the folder tree: click **Root only** or a **folder name** so the footer shows **Into:** your destination path.
4. **Apply** writes files into that folder. **Tree** packs create a subfolder (name from the manifest) inside the destination.

### 8.2 Export… (selection bar)

Select library poses, then **Export…** to write a **flat** v3 pack (group metadata when a complete group is selected).

### 8.3 Export branch / library tree (folder footer, Full layout)

**Export branch…** (selected folder) and **Export library tree…** (library root) produce **tree-branch** v2 packs. Only available in **Full** view (**§3.4**).

---

## 9. Save and update workflows

### 9.1 Save Pose (top bar)

- Prompts for a **name**.
- Writes into the **current save folder**:
  - **Selected folder** when browsing a specific folder.
  - **Pose root** when **All poses** or **★ Favorites** is active (no single subfolder selected).

### 9.2 Update Pose (bottom bar, single selection)

- Overwrites the pose file from the current scene.
- Allows **keeping** or **regenerating** the thumbnail according to the overlay/UI flow.

---

## 10. Tags, favorites, and persistence

### 10.1 Primary stores

| File | Contents |
|------|----------|
| **`pose_tags.tsv`** | Per-pose tags and favorites |
| **`pose_groups.tsv`** | Group id, name, group tags, member relative paths |
| **`pose_browser_character_config.json`** | Male/female priority slot lists (`dicKey`, display name) |

All live under `BepInEx/config/com.hs2.sandbox/`. Keys use **stable relative paths** into the pose library so renames/moves can update metadata via the browser.

### 10.2 Legacy import

- If **`pose_tags.json`** exists, it may be **imported once** (Unity `JsonUtility` limitations motivate the TSV migration).

### 10.3 In-browser behavior

- **Tag Selected** edits the database for all selected items.
- **Fav Selected** toggles favorite bits used by the **★** filter.

Details of the TSV format (headers, delimiters) are implementation-specific; treat the file as **do not hand-edit unless you know the format**, and prefer backups before manual edits.

---

## 11. Thumbnail capture

**Thumbs…** opens the thumbnail capture **overlay** (full-screen style interaction managed by `PoseThumbnailCapture`):

- Frame the shot and confirm or cancel from the controls shown in the overlay.
- On success, thumbnails refresh for affected poses.

If capture is cancelled, files stay unchanged.

---

## 12. Options panel

| Setting | Meaning |
|---------|---------|
| **Card width slider** | Minimum width per card; grid fills row / adds columns within min/max bounds. |
| **Items per page** | **0** = no pagination; **> 0** = cap and use page buttons. |
| **Keyboard shortcuts** | Read-only list; assign **Next/Previous pose** and **Next/Previous browse target** in Configuration Manager → **Pose Browser · Keyboard shortcuts** (active while Pose Browser is open **unless** an IMGUI text field has keyboard focus). |
| **Select all filtered** | Select every item in the **current filtered** list. |
| **Deselect all** | Clear selection in that list. |
| **Close panel** | Closes Options; changes save through the same persistence path as sliders / Apply. |

---

## 13. HS2Wiki integration (for users)

1. Install **BepInEx** and **[HS2Wiki](https://github.com/SuitIThub/HS2Wiki/releases)** per its instructions.
2. Start Studio; ensure **HS2 Sandbox** (or the **Pose Browser** module) loads **after** HS2Wiki if you hit load-order edge cases (usually both work from default `plugins` folder).
3. Press **F3** (or your configured wiki key).
4. Open category **HS2 Sandbox → Pose Browser**:
   - **Overview** — navigation hub; click the **pose icon** to open **`OpenImage`** viewer if `pose-icon.png` sits beside the DLL.
   - Thematic pages: **Folders & library**, **Search & filters**, **Grid & selection**, **Pose groups**, **Multi-character apply**, **Pose files & actions**, **Import & export (ZIP)**, **Thumbnails**, **Options & data files**.
   - **Advanced → Tag storage & migration** — TSV vs JSON.

Wiki pages use **IMGUI** and support **rich text**, **buttons** (`OpenPage` navigation), and **`OpenImage`** as in the upstream README.

---

## 14. HS2Wiki integration (for maintainers)

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

## 15. Troubleshooting

| Issue | Things to check |
|--------|------------------|
| Empty grid | Folder mode (**All poses** vs folder vs **★ Favorites**); search/tags too strict; **↻** refresh. |
| Pose does not apply | **Character** row shows valid selection; click thumbnail (left or right) as intended. |
| Multi-apply wrong pairing | Set **Male** / **Female** on poses; load/reorder **Chars** lists; select characters in Studio; use **group header** for whole groups. |
| Tag + Chars panes missing | Open both from **Full** layout; panes dock in a chain to the right of the browser (recent builds fix overlap). |
| Group not in export | Select **all** members or use **Export group…**; v3 metadata required. |
| Tags lost | Prefer **`pose_tags.tsv`** / **`pose_groups.tsv`** backup; avoid editing TSV while the game runs. |
| ZIP import fails or errors | Pack must use **stored** (uncompressed) ZIP entries; verify **v2/v3** `manifest.json` per **`POSE_ZIP_FORMAT.md`**. |
| Wiki pages missing | HS2Wiki installed? Log line *“Registered Pose Browser pages with HS2Wiki”* on startup? Restart after installing HS2Wiki. |
| Image button does nothing | `pose-icon.png` must be next to the **same** DLL you run (or embedded — wiki **OpenImage** needs a **file path**, so the loose PNG next to the DLL is preferred). |

---

## License / credits

HS2 Sandbox Pose Browser is part of this repository. HS2Wiki is a separate project under its own license; follow its documentation for wiki-specific behavior and updates.

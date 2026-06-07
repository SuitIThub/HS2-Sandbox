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
- Offers optional **thumbnail capture** for selected poses and **group thumbnail capture** for whole pose groups (monocolor isolation per pose).
- In **2.0.0** (all-in-one **HS2 Sandbox**): **Full / List / Mini** layouts, **Sort** panel (including **Last used** tracking), **★ Favorites** library view, docked tag filter window, optional **keyboard shortcuts** (BepInEx Configuration Manager), and expanded **`pose_browser_options.json`** (per-layout window geometry and sort).
- In **2.1.0+**: **v2/v3 pose pack ZIP** **Import…** / **Export…**, branch/tree exports, modder docs **`Modules/PoseBrowser/POSE_ZIP_FORMAT.md`** (stored-only ZIP requirement).
- In **3.0.0** (split **Pose Browser** module — same sources ship inside the all-in-one build):
- **Pose groups** — named sets of poses shown as grid segments, **group tags** for filtering, **pose_groups.tsv** persistence (relative offsets + body heights per pose), and **groups[]** / **memberRelativeOffsets** / **memberBodyHeights** in v5 ZIP metadata (v2–v4 import; layout optional).
- **Tag filter include/exclude** — tri-state per-tag filters; exclude dims grouped members instead of hiding whole groups.
- **Multi-character apply** — **Chars** priority lists (male/female), **Apply to characters…** for multiple poses or a whole group, driven by **Male** / **Female** pose tags and list order.
- In **3.1.0**: **Auto-capture** for batch thumbnails (configurable pause in Options / BepInEx), grid layout and window-resize fixes, multi-group action bar improvements.
- In **3.2.0+**: **Group relative positions** — save offsets from an anchor pose (first in display order) plus **maker body height** per pose; re-apply with the group; stored in **pose_groups.tsv** and v5 ZIP; global **Apply relative positions** and **Adjust for body height** toggles (group bar + Options).
- **Group thumbnails** — **Group thumbnails…** on the group action bar: apply all group poses, then capture one preview PNG per member with other assigned characters in Studio **simple color** (monocolor); same character-count and gender rules as one-to-one multi-apply.
- In **5.0.0+**: **Pose items** — register Studio **workspace items** per pose; docked **Items** pane with add/load, optional body-part attach, load toggles (position / rotation / scale / free placement), character scale and body-height adjustment on load; **pose_items.tsv** (v5).
- **Pose stash** — temporary FK/IK clipboard: stash from one character, apply to any selection; docked side pane or independent floating window (persists when the main browser closes); **pose_stash.json**; hotkey for floating window.

### 1.1 Version note

The **all-in-one** plugin’s `PluginVersion` may read **2.0.0** while the **Pose Browser module** reads **3.1.0**; both builds compile the same Pose Browser code path.

It is opened from the Sandbox **left toolbar** (pose icon) when using the full HS2 Sandbox plugin, or the standalone Pose Browser module.

---

## 2. Layout overview

| Area | Role |
|------|------|
| **Top bar (Poses)** | Search, regex, favorites-only, **Tags** (dock **Tag filter**: include/exclude per tag, AND/OR inside pane), **Sort**, **Save Pose**, **Import…**, **Undo** / **Redo** / **History**, **Stash** |
| **Character row** | Studio selection summary; **Chars** (priority lists); **Stash** (compact List/Mini); **Apply to characters…** when 2+ poses or one group entity is selected |
| **Folders (left)** | Tree under `studio/pose`, **All poses** / **★ Favorites** / **Root only**, refresh, folder footer actions |
| **Grid** | Thumbnail cards, selection, pagination (if enabled in Options); **import preview** replaces the grid while a ZIP is open |
| **Bottom bar** | Pose actions, **Items** (one pose), **Grouping** (Group… / Ungroup), or **group entity** bar; import preview hints |
| **Items pane** | Docked when **Items** is open: register workspace items for the selected pose, load options, stored list |
| **Stash pane** | Docked when **Stash** is open (or floating via **Float** / hotkey): temporary pose clipboard |
| **Window** | **View (Full/List/Mini)** — cycles compact layout modes; **Help** — compact manual; **Options** — card width, pagination, bulk select, hotkey listing |

The main window can be **resized** from the bottom-right grip. In **Full** layout, **Help**, **Options**, **Tag filter**, **Chars**, **Sort**, **History**, and **Stash** open as docked IMGUI panes to the right of the main window (laid out in a single chain so they do not overlap). Compact **List** and **Mini** modes hide those side panels and remember their own **position and size** in **`pose_browser_options.json`**. **List** keeps the folder tree with a text list of filtered poses (no thumbnails). **Mini** is a minimal strip with **Folder** / **Pose** navigation arrows and **Reapply**—see the in-game **Help** panel for the exact stepping order. The **floating stash** window is independent: it can stay open when the main browser is closed.

---

## 3. Folders and library scope

### 3.1 Paths

- **Pose root**: `UserData/studio/pose` (internal type `PoseDataService` / `UserData.Path`).
- **Config** (BepInEx): `BepInEx/config/com.hs2.sandbox/`
  - `pose_browser_options.json` — card width, items per page, **layout tier** (Full/List/Mini) with separate saved window rects, **sort mode** and direction.
  - `pose_tags.tsv` — per-pose tags and favorites (see §10).
  - `pose_groups.tsv` — pose groups (membership, names, group tags, relative offsets, body heights).
  - `pose_items.tsv` — workspace items linked to each pose (catalog paths, layout, attach data; v5).
  - `pose_browser_character_config.json` — male/female priority lists for multi-character apply.
  - `pose_stash.json` — pose stash entries (FK/IK snapshots, auto-delete preference).
  - `pose_browser_history.json` — per-character pose history (undo/redo).

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
4. With a **group entity** selected (header click), use **Rename…**, **Tags…**, **Export…**, or **Group thumbnails…** on the group action bar.

#### Two selection modes

| Mode | How | Used for |
|------|-----|----------|
| **Group entity** | Click the **group header** | Rename, tags, export, **Apply to characters…**, **Group thumbnails…**, **Save positions…** / **Clear positions**, **Apply relative positions**, **Adjust for body height** |
| **Pose members** | Card checkboxes / thumbnail selection | Tag selected, move, copy, delete, partial export |

- **Ctrl+click** / **Shift+click** on group headers work like pose selection (range within the filtered list).
- During **import preview**, the group header toggles **import checkboxes** for all members.

#### Group thumbnails

Capture **one preview PNG per pose** in a group while all assigned characters stay posed together in the scene.

**Requirements** (same one-to-one rules as **Save positions…**, except poses need **not** be applied beforehand):

| Requirement | Detail |
|-------------|--------|
| **Selection** | **Group entity** selected (header click). |
| **Character count** | Exactly **as many characters selected in Studio as poses** in the group. |
| **Gender pairing** | **Male** / **Female** pose tags and **Chars** priority must allow a full one-to-one assignment (see **§6.2**). |
| **Not import preview** | Disabled while a ZIP import preview is open or another capture overlay is active. |

**Workflow:**

1. Select characters in Studio; click the **group header**.
2. Click **Group thumbnails…** on the group action bar.
3. All group poses are applied at once (**Apply to characters…** rules, including **Apply relative positions** / **Adjust for body height** / **Adjust for object scale** when those global toggles are on).
4. The capture **overlay** opens — drag/resize the green frame to compose the shot.
5. For each pose in **display order**, characters assigned to **other** poses render in Studio **simple color** (monocolor) so the thumbnail highlights one character at a time.
6. **Capture** writes that pose's PNG; **Skip** leaves it unchanged; **Auto-capture** chains the rest (configurable delay in Options / BepInEx); **Cancel** exits and restores normal character rendering.

**Example:** Two-pose group; characters A and B assigned by priority (A → pose 1, B → pose 2). While capturing pose 1, B is monocolor; while capturing pose 2, A is monocolor.

Use **Thumbnails…** on individual pose checkboxes for ordinary single-pose capture (re-applies each pose before its shot). See **§11**.

#### Relative positions (save & apply)

Optional layout when a **whole group** is applied with **Apply to characters…** (or compact **▦** group apply). Data is keyed by **pose path** and **assignment order** from **Chars** priority (grid display order) — not by which Studio character you used at save time.

**Anchor (first pose):** The first pose in display order is the anchor. At save, its world position and body height are recorded on that path. At apply, the anchor character is **not moved**; every other pose uses offsets from the **current** anchor position.

Offsets and **maker body height** (slider value per pose) are stored in **pose_groups.tsv** (v3) and in v5 ZIP **`memberRelativeOffsets`** / **`memberBodyHeights`**. Both apply toggles are **global** (group bar when that group has saved data, and **Options**).

**Before Save positions… is enabled**, all of the following must be true:

| Requirement | Detail |
|-------------|--------|
| **Last apply was this group** | You applied this group with **Apply to characters…** (or compact group apply) and have **not** applied any other pose since. If needed, apply the group again. |
| **Character count** | Exactly **as many characters selected in Studio as poses** in the group (e.g. 3 poses → 3 characters). |
| **Gender pairing** | Poses must map **one-to-one** to characters using the same rules as multi-character apply: **Male** / **Female** pose tags → characters on the matching **Chars** list; untagged poses use interleaved male/female priority order. Example: a group with one male-tagged and two female-tagged poses needs **one male and two female** selected characters that the matcher can assign. |
| **Not import preview** | Saving is disabled while a ZIP import preview is open. |

**Save workflow:**

1. Configure **Chars** (load lists, **Male** / **Female** pose tags as needed) — see **§6.2**.
2. Select the correct characters in Studio.
3. Select the **group header** → **Apply to characters…** (first pose → anchor character).
4. Move characters in the scene to the layout you want.
5. Select the group header again → **Save positions…** (stores local **offset** and **rotation** vs anchor per other pose, plus **body height** on every pose path; hover the button if disabled).

**Apply workflow:** Use the same apply path and assignment order. After poses are applied:

- **Apply relative positions** — each non-anchor character moves to **anchor position + anchor rotation × saved local offset** (orbits with the anchor).
- **Adjust for body height** (requires relative positions on) — same full offset, but **offset.y** is scaled from saved vs current body-height ratios on each pose path (spread ratio when heights differed at save; otherwise anchor or averaged scale; no fixed meter constant).
- Uncheck **Apply relative positions** to apply poses only (saved layout is kept; turning it off also disables height adjust).
- **Clear positions** removes stored offsets **and** heights for that group.

#### Filters and layout

- **Group tags** affect whether the **segment** passes search/tag filters; **pose tags** still apply per card (e.g. Male/Female for multi-apply).
- Sort treats each group as a **block**; large groups may span multiple grid rows (continuation headers).

#### Move / copy / export

- **Move…** / **Copy…** — ungrouped poses, or exactly **one full group** (every member selected).
- **Export…** — includes group metadata (**memberRelativeOffsets**, **memberBodyHeights** when present) in v5 ZIP when a full group is in the selection; **Export…** from the group bar exports that group alone.

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

When the group has **saved relative positions** and **Apply relative positions** is on (global; **Options** or group bar), the same apply restores character spacing after poses are applied; **Adjust for body height** scales **offset.y** from saved heights (see **§5.4**).

#### Chars window (priority lists)

1. Click **Chars** (docked pane).
2. **Load characters from scene** — fills **Male** and **Female** columns from the scene.
3. **↑ / ↓** — priority within a column (**top = first**).
4. **⇄** — move slot to the other column; **✕** — remove from lists.
5. **Male before female** / **Female before male** — for **untagged** poses, which gender is picked first at the same list rank (default: male first).
6. Persisted in **`pose_browser_character_config.json`**.

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

1. Build order: interleave lists by rank (male/female order from **Chars**; default 1st male, 1st female, 2nd male, …), then selected characters not on either list.
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

## 6.3 Pose stash

The **pose stash** is a **temporary clipboard** for character FK/IK poses. It does **not** create library files under `UserData/studio/pose` and is separate from **History** (automatic undo timeline per character).

### Opening and closing

| Control | Behavior |
|---------|----------|
| **Stash** (top bar in Full; character row in List/Mini) | Toggles stash UI. If open (docked or floating), closes. If closed, opens in the **last mode** used (docked or floating). |
| **Docked pane** | Side panel like **History**; closes when the main Pose Browser window closes. |
| **Float** (in docked stash) | Undocks to an independent window. |
| **Dock** (floating stash) | Re-attaches beside the browser; if the browser is hidden, closes the float. |
| **×** (floating stash) | Closes only the floating window. |
| Hotkey **Toggle undocked pose stash** | Opens/closes the **floating** stash (Configuration Manager → **Pose Browser · Keyboard shortcuts**). Works while Pose Browser is loaded even if the main window is closed. |

The floating window can be **moved** (title bar) and **resized** (bottom-right ◢). Size, position, and docked-vs-float preference are saved in **`pose_browser_options.json`**.

### Capture and apply

1. Select **exactly one** character in Studio → **Stash selected character**.
2. Entries appear as **`Character name  yyyy-MM-dd HH:mm:ss`** (newest first). Only **FK/IK pose data** is stored (not world position/rotation).
3. Select one or more characters → **click an entry** to apply that pose to **all** of them.
4. Optional **Auto-delete after apply** removes the entry after a successful apply.

### Delete

- **x** on a row → **Yes** / **No** confirmation.
- **Clear entire stash** at the bottom → confirm required.

### Persistence

| File | Contents |
|------|----------|
| **`pose_stash.json`** | Stash entries (base64 pose blobs), auto-delete flag |
| **`pose_browser_options.json`** | Floating stash rect, **stashPreferUndocked** preference |

---

## 7. Selection bar (bottom)

### 7.1 Library selection actions

Visible when **at least one** selected card refers to an **on-disk** pose in your library (not during ZIP import preview).

| Control | Purpose |
|---------|---------|
| **Selection: n** | Count of selected items |
| **Items** | Exactly **one** pose: open docked **Items** pane (workspace item registration — see **§7.4**) |
| **Update Pose** | One item only: overwrite file from scene; optional thumbnail refresh |
| **Rename…** | One item: display name; optional rename file to safe name |
| **Group…** / **Ungroup** | Create a group from 2+ ungrouped poses, or remove membership |
| **Tag Selected** | Mass add/remove **pose** tags via assign window |
| **Fav Selected** | Toggle favorite flag for each selected item |
| **Thumbnails…** | Start thumbnail capture overlay for selection |
| **Export…** | Save selected poses to a **v3 .zip** (tags, favorites, **groups** when fully selected) |
| **Move…** / **Copy…** | Ungrouped poses, or **one full group**; pick destination in the folder tree, then **Apply** / **Cancel** in the footer |
| **Delete…** | Confirms; copies to **`!_AutoBackup`** then deletes files; refreshes data |
| **Deselect** | Clears selection on filtered list |

### 7.2 Group entity bar

Shown above the pose selection bar when exactly **one group header** is selected (click the **▦** row). Includes rename, tags, ungroup, export, move/copy, **Apply to characters…**, **Group thumbnails…**, save/clear relative positions, and layout toggles. See **§5.4**.

| Control | Purpose |
|---------|---------|
| **Group thumbnails…** | Apply all group poses, then capture one preview PNG per member (monocolor on non-focus characters). See **§11.2**. |
| **Save positions…** | Store relative layout after a prior group apply (stricter requirements — see **§5.4**). |

### 7.3 Import preview mode

After **Import…**, the bar shows import-specific text and **Cancel import**. Choose poses in the grid, pick **Root only** or a folder in **Folders**, then **Apply** / **Cancel** at the **top** of the folder footer (**§3.5**). **Tree branch** packs create one new subfolder under the destination you select.

### 7.4 Pose items (Items pane)

Register Studio **workspace items** (props, accessories, etc.) against **one library pose** so they can be respawned and repositioned when you use that pose again.

#### Opening the pane

1. Select **exactly one** on-disk pose in the grid (not during ZIP import preview).
2. Click **Items** in the bottom selection bar.
3. The **Items** pane docks in the same side-panel chain as Help, Options, Tags, Chars, and Sort.

#### Adding entries

| Requirement | Detail |
|-------------|--------|
| Studio character | **Exactly one** character selected |
| Workspace items | One or more **OCIItem** selections (workspace tree and/or item guide) |
| UI | Label **Will add: …** lists names; **Add selected item(s)** registers them |

Each saved entry includes catalog slot ids, **bundle / asset / manifest** paths (so items respawn correctly after a new Studio session), display name, item scale, anchor-relative position and rotation, saved character **object scale** and **body height**, optional **body-part** tree path and name, and (v5) Studio **attach** `changeAmount` offsets when the item was parented in the workspace tree.

A **yellow** banner appears if the selected character does **not** currently have this pose applied; you can still add and load items (layout is relative to the character as posed now).

#### Stored list

| Control | Action |
|---------|--------|
| **☑** | Include in **Load Selection** |
| **Name (button)** | Load that entry immediately |
| **✎** | Rename display label |
| **X** | Remove from this pose |
| **Bold name** | Same catalog item is selected in Studio (still a button) |

**Load Selection** loads checked rows. **Load All** loads every row. Both need **one** Studio character selected.

#### Load options

| Toggle | Effect |
|--------|--------|
| **Position** | Apply saved layout position (off = keep spawn default) |
| **Rotation** | Apply saved rotation |
| **Scale** | Apply saved item scale (adjusted for current character object scale) |
| **Load as free** | Skip workspace tree parenting even when saved on a body part; same world layout relative to the character |

Position and scale on load are adjusted for the character’s current **Studio object scale** and **body height** (same ratio rules as **group relative positions**). Attached items normally reparent to the saved body-part row via the workspace tree; **Load as free** keeps world placement without that parent link.

An **orange ⚠** on a row shows the last load warning (for example body part not found — item may be placed freely).

#### Persistence

- File: **`pose_items.tsv`** (header `HS2SANDBOX_POSE_ITEMS` tab **5**) under `BepInEx/config/com.hs2.sandbox/`.
- Keys: pose path relative to the library root (updated when poses are moved/renamed through the browser).
- Not embedded in pose `.png` / `.dat` files; copy/move pose files does not copy item lists unless you duplicate entries manually.

---

## 8. Import and export (ZIP v2 / v3)

Pose Browser reads and writes **`.zip`** packs with `manifest.json`, `metadata.json`, and pose binaries under **`poses/`**. **v3** adds optional **`groups[]`** in metadata (id, name, group tags, member paths). **v2** packs without groups still import. The runtime reader only accepts **compression method 0 (stored)**; archives produced with Deflate will **fail** to import—see **`Modules/PoseBrowser/POSE_ZIP_FORMAT.md`** for tool guidance.

### 8.1 Import… (top bar)

1. Open a compatible **`.zip`**.
2. The **preview grid** lists pack entries; select which poses to import.
3. Navigate the folder tree: click **Root only** or a **folder name** so the footer shows **Into:** your destination path.
4. **Apply** writes files into that folder. **Tree** packs create a subfolder (name from the manifest) inside the destination.

### 8.2 Export… (selection bar)

Select library poses, then **Export…** to write a **flat** v5 pack (group metadata with **memberRelativeOffsets** and **memberBodyHeights** when a complete group with saved layout is selected; v2–v4 packs still import).

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
| **`pose_groups.tsv`** | Group id, name, group tags, member paths, relative offsets, body heights per pose (v3 TSV) |
| **`pose_items.tsv`** | Per-pose workspace item list: catalog paths, transforms, attach paths, attach offsets (v5 TSV) |
| **`pose_browser_options.json`** | Global **applyGroupRelativePositions** and **applyGroupRelativeHeights** toggles (options v12+) |
| **`pose_browser_character_config.json`** | Male/female priority slot lists (`dicKey`, display name) |
| **`pose_stash.json`** | Pose stash entries and auto-delete-after-apply flag |
| **`pose_browser_history.json`** | Per-character pose history snapshots |

All live under `BepInEx/config/com.hs2.sandbox/`. Keys use **stable relative paths** into the pose library so renames/moves can update metadata via the browser.

### 10.2 Legacy import

- If **`pose_tags.json`** exists, it may be **imported once** (Unity `JsonUtility` limitations motivate the TSV migration).

### 10.3 In-browser behavior

- **Tag Selected** edits the database for all selected items.
- **Fav Selected** toggles favorite bits used by the **★** filter.

Details of the TSV format (headers, delimiters) are implementation-specific; treat the file as **do not hand-edit unless you know the format**, and prefer backups before manual edits.

---

## 11. Thumbnail capture

### 11.1 Single-pose — **Thumbnails…**

Select one or more library poses (member checkboxes), then **Thumbnails…** in the bottom selection bar.

- Each pose in the queue is applied to Studio-selected character(s) before its shot.
- The **overlay** shows a draggable/resizable green frame.
- **Capture** — write PNG into the pose file and advance.
- **Skip** — advance without saving.
- **Auto-capture** — capture the current pose, then all remaining poses automatically (delay: Options / BepInEx **Auto capture delay**).
- **Cancel** — exit; files already captured remain saved.

On success, thumbnails refresh in the grid. If capture is cancelled mid-run, poses not yet captured stay unchanged.

### 11.2 Group — **Group thumbnails…**

When a **group entity** is selected (group header), the group action bar offers **Group thumbnails…**. See **§5.4** for requirements, workflow, and the monocolor example.

Unlike **Thumbnails…**, the group run applies **all** poses once up front, frames the whole scene, then captures each member in order with non-focus characters in Studio **simple color**.

---

## 12. Options panel

| Setting | Meaning |
|---------|---------|
| **Card width slider** | Minimum width per card; grid fills row / adds columns within min/max bounds. |
| **Items per page** | **0** = no pagination; **> 0** = cap and use page buttons. |
| **Apply stored relative positions when applying a group** | Global toggle: when on, group apply restores saved character spacing (anchor + offset) after poses (see **§5.4**). Does not delete saved layout when off. |
| **Adjust relative layout for body height (saved per pose)** | Scales saved **offset.y** from body-height ratios; requires relative positions on. |
| **Keyboard shortcuts** | Read-only list; assign **Next/Previous pose**, **Next/Previous browse target**, **Undo/Redo**, **Toggle undocked pose stash**, etc. in Configuration Manager → **Pose Browser · Keyboard shortcuts** (active while Pose Browser is open unless an IMGUI text field has keyboard focus). |
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
   - Thematic pages: **Folders & library**, **Search & filters**, **Grid & selection**, **Pose groups**, **Multi-character apply**, **Pose stash**, **Pose files & actions**, **Pose items**, **Import & export (ZIP)**, **Thumbnails**, **Options & data files**.
   - **Advanced → Tag storage & migration** — TSV vs JSON.

Wiki pages use **IMGUI** and support **rich text**, **buttons** (`OpenPage` navigation), and **`OpenImage`** as in the upstream README.

---

## 14. HS2Wiki integration (for maintainers)

- Registration lives in **`src/Core/PoseBrowserWikiRegistration.cs`**.
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
| Group not in export | Select **all** members or use **Export…** from the group bar; v5 metadata includes groups (offsets and heights when saved). |
| Tags lost | Prefer **`pose_tags.tsv`** / **`pose_groups.tsv`** backup; avoid editing TSV while the game runs. |
| ZIP import fails or errors | Pack must use **stored** (uncompressed) ZIP entries; verify **v2/v3** `manifest.json` per **`POSE_ZIP_FORMAT.md`**. |
| Wiki pages missing | HS2Wiki installed? Log line *“Registered Pose Browser pages with HS2Wiki”* on startup? Restart after installing HS2Wiki. |
| Items load wrong / spheres | Re-save items after an update so **pose_items.tsv** v5 has bundle paths and attach data; apply the pose on the target character first when possible. |
| Item position off after scale change | Load uses current character object scale and body height; re-save on the reference character if the pose or scale changed a lot. |
| Stash closes when browser closes | Only the **docked** stash closes with the browser; use **Float** or the undocked hotkey for a persistent floating window. |
| Image button does nothing | `pose-icon.png` must be next to the **same** DLL you run (or embedded — wiki **OpenImage** needs a **file path**, so the loose PNG next to the DLL is preferred). |

---

## License / credits

HS2 Sandbox Pose Browser is part of this repository. HS2Wiki is a separate project under its own license; follow its documentation for wiki-specific behavior and updates.

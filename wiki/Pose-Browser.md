# Pose Browser

Browse, tag, favorite, save, apply, and manage poses from **`UserData/studio/pose`**. Available for **HS2**, **KKS**, and **KK**.

| Game | DLL |
|------|-----|
| HS2 | `HS2Sandbox.PoseBrowser.dll` |
| KKS | `KKSSandbox.PoseBrowser.dll` |
| KK | `KKSandbox.PoseBrowser.dll` |

Open from the **pose icon** on the Studio left toolbar.

## What it does

- Folder tree + thumbnail grid (**Full / List / Mini** layouts)
- Search, tag filters (include/exclude), favorites, sort (including **Last used**)
- Save, update, rename, move, copy, delete poses (with backup)
- **Pose groups** with relative layout and body heights
- **Pose items** — workspace props linked to poses
- **Pose stash** — temporary FK/IK clipboard
- **History** — per-character undo/redo
- **Multi-character apply** via priority lists
- ZIP import/export (v2–v7 metadata)
- In-game update check via `versions.json`
- Optional **HS2Wiki** help pages (F3)

## Layout overview

| Area | Role |
|------|------|
| **Top bar** | Search, tags, sort, Save Pose, Import, Undo/Redo/History, Stash |
| **Character row** | Studio selection; **Chars**; **Apply to characters…** |
| **Folders (left)** | Tree, All poses / ★ Favorites / Root only |
| **Grid** | Thumbnails, selection, pagination |
| **Bottom bar** | Pose actions, Items, Grouping, group entity bar |
| **Side panes** | Help, Options, Tag filter, Chars, Sort, History, Stash, Items |

Window is resizable (bottom-right grip). **Full** layout docks side panels to the right; **List/Mini** are compact modes.

## Detailed guides

| Topic | Page |
|-------|------|
| Folder tree & library scope | [Folders & library](Pose-Browser-Folders-and-Library) |
| Search, tags, sort | [Search, filters & sort](Pose-Browser-Search-Filters-and-Sort) |
| Grid, selection, apply | [Grid & selection](Pose-Browser-Grid-and-Selection) |
| Pose groups & layout | [Pose groups](Pose-Browser-Groups) |
| Multi-character mapping | [Multi-character apply](Pose-Browser-Multi-Character-Apply) |
| Temporary clipboard | [Pose stash](Pose-Browser-Stash) |
| Workspace items | [Pose items](Pose-Browser-Items) |
| ZIP packs | [Import/export ZIP](Pose-Browser-Import-Export-ZIP) |
| Thumbnail capture | [Thumbnails](Pose-Browser-Thumbnails) |
| Options & files | [Options & data](Pose-Browser-Options-and-Data) |

## HS2-only extras

- **HS2Heelz** tag rules and Heelz Control window — [Plugin compatibility](Plugin-Compatibility)
- **PE compat** — embed breast/butt Advanced Mode in pose files

## Keyboard shortcuts

Assign in Configuration Manager → **Pose Browser · Keyboard shortcuts**. See [Keyboard shortcuts](Keyboard-Shortcuts).

## Quick start

1. Select character(s) in Studio workspace
2. Open Pose Browser from sidebar
3. Pick a folder or **All poses**
4. Click a thumbnail to **apply** the pose
5. Use **Save Pose** to capture current scene pose

→ [Troubleshooting](Troubleshooting) (Pose Browser section)

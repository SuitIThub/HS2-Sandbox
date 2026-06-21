# Pose Browser

Browse, tag, favorite, save, apply, and manage poses from **`UserData/studio/pose`**. Available for **HS2**, **KKS**, and **KK**.

| Game | DLL |
|------|-----|
| HS2 | `HS2Sandbox.PoseBrowser.dll` |
| KKS | `KKSSandbox.PoseBrowser.dll` |
| KK | `KKSandbox.PoseBrowser.dll` |

Open from the **pose icon** on the Studio left toolbar.

![Green Pose Browser button on the Studio left toolbar](images/pose-browser/pb-01-toolbar-icon.png)

> Studio left toolbar with module icons; the **green** button with a falling-person silhouette (right column, row above Wiki) opens Pose Browser — not the white female head icon beside it.

## Table of contents

Read this guide in order, or jump to a topic:

1. [Overview](#what-it-does) (this page)
2. [Folders & library](Folders-and-Library)
3. [Search, filters & sort](Search-Filters-and-Sort)
4. [Grid & selection](Grid-and-Selection)
5. [Pose groups](Groups)
6. [Multi-character apply](Multi-Character-Apply)
7. [Pose stash](Stash)
8. [Pose items](Items)
9. [Import/export ZIP](Import-Export-ZIP)
10. [Thumbnails](Thumbnails)
11. [Options & data](Options-and-Data)

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

![Pose Browser Full layout with labeled regions](images/pose-browser/pb-02-full-layout.png)

> **Full** layout: folder tree on the left (`vible standing v0.4` selected), pose thumbnail grid in the center, bottom action bar, and the **Options** panel docked on the right (UI scale, card width, HS2Heelz compatibility).

![Pose Browser List layout](images/pose-browser/pb-03-list.png)

> **List** layout: folder tree and scrollable pose name list with pagination; a separate preview window shows the selected pose (`vible001`).

![Pose Browser Mini layout](images/pose-browser/pb-03-mini.png)

> **Mini** layout: compact window with **Folder** / **Pose** arrow navigation, character strip, and **Reapply** button.

## Plugin compatibility (all games)

- **HS2PE** (HS2) / **KKPE** (KK, KKS) — embed breast/butt Advanced Mode gravity & force in pose files when the matching plugin is installed — [Plugin compatibility](Plugin-Compatibility)

## HS2-only extras

- **HS2Heelz** tag rules and Heelz Control window — [Plugin compatibility](Plugin-Compatibility)

## Keyboard shortcuts

Assign in Configuration Manager → **Pose Browser · Keyboard shortcuts**. See [Keyboard shortcuts](Keyboard-Shortcuts).

## Quick start

1. Select character(s) in Studio workspace
2. Open Pose Browser from sidebar
3. Pick a folder or **All poses**
4. Click a thumbnail to **apply** the pose
5. Use **Save Pose** to capture current scene pose

---

**Navigation:** [← Notebook](Notebook) · **Pose Browser** · [Next: Folders & library →](Folders-and-Library)

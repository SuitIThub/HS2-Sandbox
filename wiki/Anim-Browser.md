# Anim Browser

Browse Studio's **animation catalog**, apply animations, reorganize categories, and control playback. Available for **HS2**, **KKS**, and **KK**.

| Game | DLL | Preview |
|------|-----|---------|
| HS2 | `HS2Sandbox.AnimBrowser.dll` | Hover preview (embedded rig) |
| KKS | `KKSSandbox.AnimBrowser.dll` | No preview |
| KK | `KKSandbox.AnimBrowser.dll` | No preview |

Open from the **anim icon** on the Studio left toolbar.

## What it does

- Category tree: **groups → sub-categories → animations**
- Grid and list views with search
- One-click apply to selected Studio characters
- Playback controls (speed, pause, scrub, loop, restart)
- **Non-destructive reorganization**: rename, group animations into cards, merge categories/groups
- Character priority for multi-character apply
- Optional auto-translate (XUnity)
- Persists organization in JSON under BepInEx config

> All reorganization is a **display layer** over the real Studio catalog. The catalog is never modified; merges and groups can be undone.

## Layout overview

| Area | Role |
|------|------|
| **Top bar** | Grid/List, Controls, Options, Help, search, character summary |
| **Category tree** | Groups and sub-categories; ↻ refresh; merge action bar |
| **Main panel** | Grid or list; content action bar (Group/Ungroup/Rename) |
| **Controls pane** | Playback — docked or floating |
| **Characters pane** | Priority list |
| **Review pane** | Confirm merges/groups before applying |

## Detailed guides

| Topic | Page |
|-------|------|
| First steps | [Getting started](Anim-Browser-Getting-Started) |
| Tree & search | [Browsing & search](Anim-Browser-Browsing-and-Search) |
| Click to apply | [Applying animations](Anim-Browser-Applying-Animations) |
| Speed, loop, float | [Playback controls](Anim-Browser-Playback-Controls) |
| Bundle into cards | [Grouping](Anim-Browser-Grouping) |
| Tree merges | [Merging categories](Anim-Browser-Merging-Categories) |
| Pre-confirm UI | [Review panel](Anim-Browser-Review-Panel) |
| Priority & settings | [Characters & options](Anim-Browser-Characters-and-Options) |

## HS2 hover preview

On HS2 only, hovering animation cards can show a **3D preview** via `AnimPreviewStage` (embedded rig + GL renderer). Not available on KKS/KK builds.

## Keyboard shortcuts

- **HotkeyToggleUndockedControls** — floating controls (assign in Configuration Manager)
- **Escape** — deselect in grouping UI

See [Keyboard shortcuts](Keyboard-Shortcuts).

## Quick start

1. Select character(s) in Studio
2. Open Anim Browser
3. Click a sub-category on the left
4. Click an animation to apply

→ [Troubleshooting](Troubleshooting) (Anim Browser section)

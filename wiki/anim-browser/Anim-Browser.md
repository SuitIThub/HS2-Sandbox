# Anim Browser

Browse Studio's **animation catalog**, apply animations, reorganize categories, and control playback. Available for **HS2**, **KKS**, and **KK**.

| Game | DLL | Preview |
|------|-----|---------|
| HS2 | `HS2Sandbox.AnimBrowser.dll` | Hover preview (embedded rig) |
| KKS | `KKSSandbox.AnimBrowser.dll` | No preview |
| KK | `KKSandbox.AnimBrowser.dll` | No preview |

Open from the **anim icon** on the Studio left toolbar.

![Anim Browser toggle on the Studio left toolbar](images/anim-browser/ab-01-toolbar-icon.png)

> Studio left toolbar: **Anim Browser** toggle — white leaping-runner icon on a **green** square when active (second row, right column). Not the green falling-person **Pose Browser** icon further down the bar.

## Table of contents

Read this guide in order, or jump to a topic:

1. [Overview](#what-it-does) (this page)
2. [Getting started](Getting-Started)
3. [Browsing & search](Browsing-and-Search)
4. [Applying animations](Applying-Animations)
5. [Playback controls](Playback-Controls)
6. [Grouping animations](Grouping)
7. [Merging categories & groups](Merging-Categories)
8. [Review panel](Review-Panel)
9. [Thumbnails](Thumbnails)
10. [Characters & options](Characters-and-Options)

## What it does

- Category tree: **groups → sub-categories → animations**
- Grid and list views with search and optional name translation (XUnity)
- One-click apply to selected Studio characters
- Playback controls (speed, pause, scrub, loop, motion/pattern, per-character extras)
- **Non-destructive reorganization**: rename, group animations into cards, merge categories/groups
- **Thumbnail capture** to `UserData/com.hs2.sandbox/anim_thumbnails/` (Options or selection bar)
- Character priority for multi-character apply
- **HS2:** live **hover preview** (embedded stick-figure rig) with configurable camera
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
| **Options pane** | UI scale, card size, catalog filter, hover preview, thumbnail capture |

![Anim Browser main window layout](images/anim-browser/ab-02-full-layout.png)

> Main window with **Character → Walking & Running** selected: eight animation cards (`Running 1`–`Running 2`, `Walking 1`–`Walking 4`, …), top bar (**Grid**, **Controls**, search, **Character: Luna Clark**), and content bar (**1 selected**, **Capture thumbnail…**).

## HS2 hover preview

On **HS2 only**, hovering animation cards in **grid view** can show a **live stick-figure preview** (`AnimPreviewStage` — embedded skeleton + GL renderer). No Studio character is required; the rig samples the animation clip off-screen.

**Options → Hover animation preview** toggles the feature. When enabled:

| Setting | Effect |
|---------|--------|
| **Preview camera angle** | Full frontal (0°), front-side (45°), side (90°), rotating, or rotating with 2 s pauses at 0° / 45° / 90° |
| **Rotation speed** | Orbit speed for rotating modes (10–240 °/s) |
| **Camera pitch** | Vertical tilt (−90° bottom-up … +90° top-down; default ~10°) |

Stored PNG thumbnails (see [Thumbnails](Thumbnails)) show when not hovering. KKS/KK builds have no preview stage.

![HS2 hover preview on an animation card](images/anim-browser/ab-03-hover-preview.gif)

> **Walking & Running** grid (HS2): hovering **Running 1** or **Walking 1** shows a cyan stick-figure preview on the card; other cards keep Luna Clark thumbnails (umbrella on **Walking 3**, serving tray on **Walking 4**) — **Luna Clark** stands in the viewport beside the browser.

## Keyboard shortcuts

- **HotkeyToggleUndockedControls** — floating controls (assign in Configuration Manager)
- **Escape** — deselect in grouping UI

See [Keyboard shortcuts](Keyboard-Shortcuts).

## Quick start

1. Select character(s) in Studio
2. Open Anim Browser
3. Click a sub-category on the left
4. Click an animation to apply

---

**Navigation:** [← Pose Browser — Options & data](Options-and-Data) · **Anim Browser** · [Next: Getting started →](Getting-Started)

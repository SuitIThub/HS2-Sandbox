# Keyboard shortcuts

Most Sandbox hotkeys use BepInEx **`KeyboardShortcut`** entries. Defaults are **`None`** — assign keys in **Configuration Manager**.

Shortcuts are active while the relevant window/module is loaded. Pose Browser shortcuts are suppressed when an IMGUI text field has keyboard focus.

## Pose Browser

Configuration Manager → **Pose Browser · Keyboard shortcuts**

| Config key | Action |
|------------|--------|
| `HotkeyNextPose` | Next pose in filtered list |
| `HotkeyPrevPose` | Previous pose |
| `HotkeyNextBrowse` | Next browse target (folder step in Mini/List) |
| `HotkeyPrevBrowse` | Previous browse target |
| `HotkeyToggleVisible` | Toggle Pose Browser window |
| `HotkeyToggleMinimize` | Toggle minimize (PB chip) |
| `HotkeyUndo` | Undo pose change |
| `HotkeyRedo` | Redo pose change |
| `HotkeyToggleUndockedStash` | Toggle floating pose stash |

### HS2 only — Heelz Control

| Config key | Action |
|------------|--------|
| `HotkeyToggleHeelzControl` | Toggle Heelz Control window |

## Anim Browser

Configuration Manager → **Anim Browser · Keyboard shortcuts**

| Config key | Action |
|------------|--------|
| `HotkeyToggleUndockedControls` | Toggle floating playback controls |

### Hardcoded

| Key | Context | Action |
|-----|---------|--------|
| **Escape** | Grouping UI | Deselect / cancel grouping selection |

## HS2Wiki (separate plugin)

| Key | Action |
|-----|--------|
| **F3** (default) | Open HS2Wiki in-game |

Pose Browser and Anim Browser register help pages under **HS2 Sandbox → …** when HS2Wiki is installed. This is **not** a Pose/Anim Browser hotkey.

## Timeline

Timeline does **not** define global module hotkeys. It can **simulate** key combos via timeline commands (`simulate_key`). See [Timeline commands](Timeline-Commands-Reference).

## Other modules

CopyScript, Notebook, Son Scale, SearchBarManager, and Workspace Tree Lock have **no** default keyboard shortcuts.

Workspace Tree Lock uses **middle mouse button** (mouse button 2) on workspace tree rows — not configurable.

→ [Pose Browser](Pose-Browser) · [Anim Browser](Anim-Browser)

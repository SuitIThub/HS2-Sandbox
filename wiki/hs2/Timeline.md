# Timeline

**DLL:** `HS2Sandbox.Timeline.dll` · **GUID:** `com.hs2.sandbox.timeline` · **Game:** HS2 only

An **action timeline** for Studio: ordered steps with **Run**, **Pause**, and **Stop**. Useful for repeatable scene setup, screenshots, CopyScript batches, variable-driven workflows, and light automation.

## Table of contents

1. [Opening the window](#opening-the-window)
2. [Basic workflow](#basic-workflow)
3. [Persistence](#persistence)
4. [Command categories](#command-categories)
5. [External plugin commands](#external-plugin-commands)
6. [Sub-timelines & variables](#sub-timelines--variables)
7. [Mouse position overlay](#mouse-position-overlay)
8. [Dependencies](#dependencies)

## Opening the window

1. Install Timeline
2. Start Studio
3. Click the **Timeline** icon on the left toolbar

## Basic workflow

1. Add commands to the timeline list (waits, Studio actions, CopyScript, variables, …)
2. Click **Run** to execute from the current position
3. Use **Pause** / **Stop** as needed
4. Timeline and persistent variables save to JSON automatically

## Persistence

| File | Contents |
|------|----------|
| `BepInEx/config/com.hs2.sandbox/timeline.json` | Timeline definition |
| `BepInEx/config/com.hs2.sandbox/persistent_vars.json` | Persistent variables |

Default folder hint for file dialogs: `UserData/Timeline/`

## Command categories

| Category | Examples |
|----------|----------|
| **Input simulation** | `simulate_key`, `simulate_mouse`, `move_mouse`, `scroll` |
| **Flow control** | `pause`, `loop`, `checkpoint`, `jump`, `confirm`, `sub_timeline`, `return` |
| **CopyScript** | `start_tracking`, `copy_rename`, `set_source_path`, rule commands |
| **Screenshots** | `screenshot`, `wait_screenshot`, resolution/path/alpha commands |
| **Studio / scene** | Clothing/accessory state, camera, object visibility, chara/coordinate cards |
| **Variables** | `set`, `get`, `calc`, `if`, `list`, dict commands |
| **VNGE** | Scene next/prev/load (needs modified VNGE + IronPython) |
| **Video** | `video_record` |
| **Fashion** | `get_fashion`, outfit commands |

Full list: [Timeline commands reference](Timeline-Commands-Reference)

## External plugin commands

Many commands that talk to **other plugins** (VNGE, FashionLine, screenshot plugins, etc.) expect **modified plugin builds** with extra hooks. Those builds are **not** included in this repository.

**Typical use:** stick to Studio-native and CopyScript steps unless you already have matching modified plugins installed.

## Sub-timelines & variables

- **Sub-timeline** commands call nested timelines
- **Design-time variables** exist for the current run
- **Persistent variables** survive between sessions (`persistent_vars.json`)
- Variable editor windows available from the Timeline UI

## Mouse position overlay

Timeline implements `IOverlayDrawable` — optional crosshair overlay for mouse-position commands during editing/debug.

## Dependencies

- **IronPython** NuGet package (VNGE-related paths)
- **CopyScript server** for CopyScript commands
- Modified external plugins for their respective commands

---

**Navigation:** [← CopyScript](CopyScript) · **Timeline** · [Next: Timeline commands →](Timeline-Commands-Reference)

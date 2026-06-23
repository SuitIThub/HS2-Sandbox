# CopyScript

**DLL:** `HS2Sandbox.CopyScript.dll` · **GUID:** `com.hs2.sandbox.copyscript` · **Game:** HS2 only

Connects Studio to an external **CopyScript HTTP API** on your PC for file tracking, counters, lists, and batch rename rules — without leaving the game.

## Table of contents

1. [Opening the window](#opening-the-window)
2. [Typical workflow](#typical-workflow)
3. [Features](#features)
4. [Requirements](#requirements)
5. [Timeline integration](#timeline-integration)
6. [Troubleshooting](#troubleshooting)

## Opening the window

1. Install the CopyScript module
2. Start **StudioNEOV2**
3. Click the **CopyScript** icon on the left toolbar

## Typical workflow

1. Start your **CopyScript server** on the PC
2. Open CopyScript Control from the sidebar
3. Set **host** and **port** to match the server
4. Configure source/destination paths, name patterns, and rules
5. Use tracking, counters, and batch operations from the window

## Features

- Connection health check and auto-refresh against the API
- Tracked files list
- Counter and list rules
- Batch rename / copy workflows
- Integration with [Timeline](Timeline) via CopyScript commands

## Requirements

- Running CopyScript HTTP service (local)
- Firewall must allow the configured port
- No other BepInEx plugin required

## Timeline integration

Timeline can call CopyScript via commands such as:

- `start_tracking`, `stop_tracking`
- `copy_rename`, `clear_tracked`
- `set_source_path`, `set_destination_path`, `set_name_pattern`
- `set_rule_counter`, `set_rule_list`, `set_rule_batch`

See [Timeline commands](Timeline-Commands-Reference).

## Troubleshooting

| Issue | Check |
|-------|-------|
| Always offline | Server running? Correct port? Firewall? |
| Wrong files | Source/destination paths in window |

---

**Navigation:** [← Home](Home) · **CopyScript** · [Next: Timeline →](Timeline)

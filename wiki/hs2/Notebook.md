# Notebook

**DLL:** `HS2Sandbox.Notebook.dll` · **GUID:** `com.hs2.sandbox.notebook` · **Game:** HS2 only

A simple **in-game notepad** for shot lists, ideas, and reminders — opened from the sidebar. Notes auto-save to disk.

## Table of contents

1. [Opening](#opening)
2. [Features](#features)
3. [Persistence](#persistence)
4. [Typical workflow](#typical-workflow)
5. [Troubleshooting](#troubleshooting)

## Opening

1. Install Notebook
2. Click the **notes** icon on the left toolbar

## Features

- Resizable window
- Multiple notes with titles (tabs)
- Debounced auto-save (~0.5 seconds after edits)
- Flush on window close and game quit
- Hand-written JSON persistence (no JsonUtility)

## Persistence

```
BepInEx/config/com.hs2.sandbox/notebook.json
```

Back up this file to preserve notes across reinstalls.

## Typical workflow

Keep the window open while working in Studio. Edits save automatically; no manual Save button required.

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Notes not saved | Check file permissions on config folder |
| Lost after crash | Last debounced save may be up to ~0.5s old |

---

**Navigation:** [← Workspace Tree Lock](Workspace-Tree-Lock) · **Notebook** · [Next: Pose Browser →](Pose-Browser)

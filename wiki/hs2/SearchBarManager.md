# SearchBarManager

**DLL:** `HS2Sandbox.SearchBarManager.dll` · **GUID:** `com.hs2.sandbox.searchbarmanager` · **Game:** HS2 only

Injects **search fields** onto long Studio UI lists (wear categories, custom lists, etc.) so you can filter items instead of scrolling.

## Table of contents

1. [How it works](#how-it-works)
2. [Configuration](#configuration)
3. [Typical use](#typical-use)
4. [Troubleshooting](#troubleshooting)

## How it works

- No sidebar button — bars appear automatically on matched Studio panels
- `MultiPathSearchBarManager` polls configured UI paths (~1 second interval)
- Supports flat lists and hierarchical category trees

## Configuration

BepInEx config section **`Search Bars`** → **`Additional Parent Paths`**

- One Unity `GameObject` path per line
- Also available in all-in-one config (`com.hs2.sandbox.cfg`)

Example paths depend on your Studio version and UI mod stack — add paths to parent containers of lists you want searchable.

## Typical use

Install when you want search bars without the full all-in-one package, or alongside other split modules (SearchBarManager does not duplicate a sidebar feature).

**Do not** load SearchBarManager twice (all-in-one + split SearchBarManager).

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Bar missing | UI hierarchy changed — update **Additional Parent Paths** |
| `GameObject.Find` fails | Path wrong; check log; verify exact hierarchy name |

---

**Navigation:** [← Timeline commands](Timeline-Commands-Reference) · **SearchBarManager** · [Next: Son Scale →](Son-Scale)

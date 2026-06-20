# Workspace Tree Lock

**DLL:** `HS2Sandbox.WorkspaceTreeLock.dll` · **GUID:** `com.hs2.sandbox.workspacetreelock` · **Game:** HS2 only

In the Studio **object/workspace tree**, **middle-click** a nested row to **pin** it. Pinned rows stay visible when parent groups collapse (cyan border). Middle-click again to unpin.

## Usage

1. Install the module (no icon needed)
2. In the workspace tree, find a nested item you use often
3. **Middle-click** the row → pinned (cyan border)
4. **Middle-click** again → unpinned

## How it works

- Harmony patches on `TreeNodeObject` visibility helpers
- Input via `WorkspaceTreeLockInput` (mouse button 2 = middle click)
- Registry tracks pinned node IDs across collapse/expand

Harmony ID: `com.hs2.sandbox.workspacetreelock`

## Typical use

Pin deep workspace items (characters, items, cameras) you need to reach often so collapsing the tree does not hide them.

## Configuration

No user config beyond standard BepInEx plugin entry. Middle-click is not rebindable.

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Pin ignored | Use middle-click on a **nested** row |
| Broken after game update | Harmony targets may need update — report issue |

→ [Troubleshooting](Troubleshooting)

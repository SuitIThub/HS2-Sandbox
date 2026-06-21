# Pose Browser — pose items

Register Studio **workspace items** (props, accessories) per library pose for respawn on reload.

![Items pane with registered workspace props for one pose](images/pose-browser/pb-13-items-pane.png)

> **Items** pane for pose **Sit on 3** (`DILDO PLAY` → **ON THE FLOOR**): stored prop **Dildo 2** with Position / Rotation / Scale load toggles and **Load Selection** / **Load All** buttons.

## Opening Items pane

1. Select **exactly one** on-disk pose (not import preview)
2. Click **Items** in bottom selection bar
3. Pane docks in side-panel chain

## Adding entries

| Requirement | Detail |
|-------------|--------|
| Studio | **One** character selected |
| Items | One or more **OCIItem** selections |
| UI | **Will add: …** → **Add selected item(s)** |

Each entry stores catalog paths, transforms, character scale/body height, optional body-part attach path, attach offsets (v5).

**Yellow banner** if pose not currently applied on character — you can still add/load.

## Stored list

| Control | Action |
|---------|--------|
| **☑** | Include in **Load Selection** |
| **Name** button | Load that entry |
| **✎** | Rename label |
| **X** | Remove from pose |

**Load Selection** / **Load All** need one Studio character selected.

## Load options

| Toggle | Effect |
|--------|--------|
| **Position** | Apply saved position |
| **Rotation** | Apply saved rotation |
| **Scale** | Apply saved item scale (adjusted for char scale) |
| **Load as free** | Skip tree parenting; keep world layout |

Position/scale adjusted for current character object scale and body height. **Orange ⚠** = last load warning (e.g. body part not found).

## Persistence

**`pose_items.tsv`** (v5) under `BepInEx/config/com.hs2.sandbox/`. Keys = pose path relative to library root. Not embedded in pose PNG/DAT files.

---

**Navigation:** [← Pose stash](Stash) · [Pose Browser](Pose-Browser) · [Next: Import/export ZIP →](Import-Export-ZIP)

# Pose Browser — import & export (ZIP)

Pose Browser reads/writes **`.zip`** packs with `manifest.json`, `metadata.json`, and binaries under **`poses/`**.

> **Critical:** ZIP entries must use **compression method 0 (stored)**. Deflate packs **fail** to import.

![ZIP import preview grid and folder destination footer](images/pose-browser/pb-14-import-preview.png)

> Import preview: two checked pack entries in the grid; bottom bar reads **Import preview (tree branch): 2 of 2 checked**; folder footer shows **Into: (pose root)** and that the pack will create subfolder **Test**.

Full spec: [Pose ZIP format](Pose-ZIP-Format)

## Import… (top bar)

1. Open compatible `.zip`
2. **Preview grid** lists pack entries — check poses to import
3. Pick **Root only** or folder in tree (footer **Into:** path)
4. **Apply** writes files; **treeBranch** packs create subfolder from manifest

**Cancel import** in bottom bar aborts preview.

## Export… (selection bar)

Select library poses → **Export…** → flat v5 pack (tags, favorites, groups with offsets/heights when complete group selected).

## Export branch / library tree (Full layout)

| Action | Scope |
|--------|-------|
| **Export branch…** | Selected folder subtree |
| **Export library tree…** | Entire library (library root footer) |

Tree packs use `kind: "treeBranch"` in manifest.

## Version support

Import accepts manifest **version 2–7**. Export writes **version 5+** with optional groups, offsets, heights, rotations, object scales.

## Common failures

| Error | Cause |
|-------|-------|
| Import rejected | Deflate compression — recreate as stored ZIP |
| Missing manifest | Invalid pack structure |
| Path unsafe | `..` or invalid paths in metadata |

See also: [Pose ZIP format](Pose-ZIP-Format)

---

**Navigation:** [← Pose items](Items) · [Pose Browser](Pose-Browser) · [Next: Thumbnails →](Thumbnails)

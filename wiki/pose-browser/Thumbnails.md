# Pose Browser — thumbnails

![Thumbnail capture overlay with green frame](images/pose-browser/pb-15-thumbnail-overlay.png)

> Thumbnail capture: green crop frame over the character in T-pose; **Capture 1/1: T-Pose** bar with **Capture**, **Skip**, **Auto-capture**, and **Cancel**.

![Group thumbnail capture with monocolor partner character](images/pose-browser/pb-16-group-thumbnail.gif)

> **Group thumbnails…** on **Besties 02**: Luna Clark and Seraphina Clark stay posed together while the capture overlay frames the scene; the non-focus character appears in **magenta simple color**. The bar shows **Group thumb 1 / 2: Besties 02A** (then **Auto 2 / 2: Besties 02B**); **Auto-capture** writes thumbnails for **02A** and **02B** into the grid.

## Single-pose — **Thumbnails…**

Select one or more poses (checkboxes) → **Thumbnails…** in bottom bar.

For each pose in queue:

1. Pose applied to Studio-selected character(s)
2. **Overlay** — drag/resize green frame
3. **Capture** — write PNG, advance
4. **Skip** — advance without save
5. **Auto-capture** — chain rest (delay: Options / BepInEx)
6. **Cancel** — exit; already captured files kept

## Group — **Group thumbnails…**

With **group entity** selected (header):

1. All group poses applied once (multi-character + layout toggles)
2. Overlay frames whole scene
3. Each member captured in order; other characters in **simple color**

Requirements: same as [Pose groups](Groups) multi-apply (character count, gender pairing). Not during import preview.

## Auto-capture delay

Configure in **Options** or BepInEx → Pose Browser → **Auto capture delay**.

## Update Pose

**Update Pose** (single selection) can optionally regenerate thumbnail when overwriting file from scene.

---

**Navigation:** [← Import/export ZIP](Import-Export-ZIP) · [Pose Browser](Pose-Browser) · [Next: Options & data →](Options-and-Data)

# Pose Browser — grid & selection

![Pose grid with thumbnails, favorites, and selection checkboxes](images/pose-browser/pb-07-grid-selection.png)

> Grid in folder `vible standing v0.4`: `vible001` marked as favorite (★) and selected (yellow), `vible006` also selected (blue); pagination shows **Page 1/48**.

![Applying a pose by clicking a thumbnail](images/pose-browser/pb-08-apply-pose.gif)

> Left-click a pose thumbnail in the browser; the selected Studio character (`Luna Clark`) updates from T-pose to the chosen standing pose.

## Card layout

Each card shows:

- Thumbnail (or placeholder)
- Name (+ **★** if favorited)
- Tag line when tags exist

**Options → Card width** sets minimum width; grid adds columns or stretches cards to fill the row.

## Pagination

If **max items per page** > 0: **◀ / ▶** and `Page x/y · n poses`. **0** = all items in one scroll view.

## Mouse & selection

| Input | Effect |
|-------|--------|
| **Checkbox** | Toggle selection without applying |
| **Left-click thumbnail** | Select one, **apply pose** to selected Studio characters |
| **Ctrl+click** | Toggle in selection |
| **Shift+click** | Range select in filtered list |
| **Right-click thumbnail** | Apply only; selection unchanged |

Apply uses Studio characters only (props/accessories ignored).

## Single-pose apply

Independent of **Chars** priority lists — applies same pose to **all** selected characters.

## Selection bar (bottom)

When library poses selected (not import preview):

| Control | Purpose |
|---------|---------|
| **Items** | One pose — open Items pane |
| **Update Pose** | Overwrite file from scene |
| **Rename…** | Display/file name |
| **Group… / Ungroup** | Pose groups |
| **Tag Selected / Fav Selected** | Bulk tag/favorite |
| **Thumbnails…** | Capture overlay |
| **Export…** | v5 ZIP |
| **Move… / Copy…** | Pick destination in tree |
| **Delete…** | Backup to `!_AutoBackup` then delete |
| **Deselect** | Clear selection |

---

**Navigation:** [← Search, filters & sort](Search-Filters-and-Sort) · [Pose Browser](Pose-Browser) · [Next: Pose groups →](Groups)

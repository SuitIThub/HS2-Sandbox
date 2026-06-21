# Pose Browser — search, filters & sort

![Tag filter pane with include and exclude states](images/pose-browser/pb-06-tag-filter.png)

> **Tag filter** pane with **+ Standing** included (green) and **− Posing** excluded (red); main grid shows poses from **All poses** matching the active filters.

## Text search

- Filters the **current grid source** by display name and path
- Toggle **.\*** for case-insensitive **regex**
- Invalid regex shows a **red error** under the search bar

## Favorites (★)

Top-bar **★** toggle restricts grid to favorited poses.

## Tag filter

**Tags** opens docked **Tag filter** pane:

| Click cycle | Meaning |
|-------------|---------|
| Neutral | Tag not used in filter |
| **+ include** | Pose must match (AND/OR mode) |
| **− exclude** | Hide untagged poses; dim grouped members |

Top bar shows counts: `Tags (+2 −1)`.

- **Group tags** count for group-level filter tests
- **Clear active filters** resets filter state only (not on-disk tags)
- **Tag Selected** / **Group tags…** use separate **assign** window

## Sort

**Sort** panel options:

- **Last used** (updates on apply from browser)
- **Last updated** / **Last created** (file timestamps)
- **Name**

First click selects criterion; second click on same row toggles ↑ / ↓.

Filters and sort combine: search/tags narrow the list; sort orders filtered results.

---

**Navigation:** [← Folders & library](Folders-and-Library) · [Pose Browser](Pose-Browser) · [Next: Grid & selection →](Grid-and-Selection)

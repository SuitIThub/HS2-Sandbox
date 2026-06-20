# Anim Browser — merging categories & groups

Merging reshapes the **category tree** — non-destructive display layer. Nothing deleted; undo via **Unmerge** or **Dissolve all groups**.

Select tree nodes (**Ctrl+click** for multiple) → tree action bar.

Every merge opens the [Review panel](Anim-Browser-Review-Panel) before saving.

## Merge categories (same group)

Select two+ **sub-categories of the same group** → **Merge categories…**

- Extend existing merge by selecting merged node + another sub-category
- Combine two merges into one

**Disabled?** Hover for reason — common case: sub-categories in **different top-level groups** (need group merge first).

## Merge groups

Select two+ **top-level groups** → **Merge groups…**

Sub-categories with same name bucket together automatically.

**Add to group merge…** — add another group without rebuilding.

## Join / split subcategories (inside group merge)

| Action | Use |
|--------|-----|
| **Join subcategories…** | Combine differently named buckets (e.g. "Cowgirl" + "Cow girl") |
| **Split subcategories** | Pull joined entry apart |

Same-source-group selection routes to category merge instead of bucket alias.

## Undo merges

| Action | Effect |
|--------|--------|
| **Unmerge** | Restore original groups/categories |
| **Unmerge subcategory** | Pull one sub-category out of group merge |
| **Dissolve all groups** | Options — full reset (confirmation) |

## Rename

**Rename…** on any node or card — display name only; original Studio name kept (searchable via tooltip).

→ [Review panel](Anim-Browser-Review-Panel) · [Anim Browser](Anim-Browser)

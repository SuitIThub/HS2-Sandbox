# AnimBrowser – Merge / Grouping Model

This document describes how category / group / animation merging works in the AnimBrowser
module after the 2026 merge-logic refactor, and which operations are intentionally not yet
surfaced in the UI.

## Layers

The raw Studio catalog is a 3-level tree: **top-level group → category (subcategory) → animation**.
It is never mutated. The displayed tree is rebuilt by [`AnimDisplayCatalog`](../src/AnimBrowser/AnimDisplayCatalog.cs)
by layering user rules from [`AnimGroupStore`](../src/AnimBrowser/AnimGroupStore.cs) on top of it.

There are four user-facing concepts:

| Concept | Rule / data | Node id prefix |
|---|---|---|
| **Category merge** – combine subcategories of one group | `AnimTreeMergeRule` (`Kind = Category`) | `cm:` |
| **Group merge** – combine top-level groups, bucketing subcategories by name | `AnimTreeMergeRule` (`Kind = Group`) | `mg:`, `mgc:` |
| **Subcategory bucket join** – join two buckets inside a group merge | `SubcategoryBucketAliases` on a group-merge rule | reuses `mgc:` |
| **Display group** – several animations shown as one card | `AnimDisplayGroupData` | – |

## Transactional review (the important part)

Every merge/group flow opens the docked **review** pane and is staged through one
[`AnimMergeReviewTransaction`](../src/AnimBrowser/AnimMergeReviewTransaction.cs):

- `BeginTreeMerge` / `BeginGridGroup` only ever write into `_mergeTx` — **never** the store.
- The review pane edits `_mergeTx` and review-only UI state (skip / "as singles").
- **Confirm** → `_mergeTx.Apply(store, …)` applies *all* edits inside a single
  `store.BeginBatch()` scope, so the whole merge persists and rebuilds the tree exactly once.
- **Cancel** (`CloseReview`) → `_mergeTx.Reset()` — a true rollback, because nothing was applied.

This replaces the previous design where reusing an existing rule eagerly mutated (and could
leave half-applied) the stored rule before the user confirmed.

## Action resolution

The tree action bar no longer shows buttons that silently do nothing. Selection is classified
up front by [`AnimBrowserWindow.MergeIntent`](../src/AnimBrowser/AnimBrowserWindow.MergeIntent.cs):

- `ResolveCategoryMergeAvailability` → *Merge categories* / *Join subcategories* / disabled-with-reason.
- `ResolveGroupMergeAvailability` → *Merge groups* / *Add to group merge* / disabled-with-reason.

A cross-group subcategory merge without a group merge (use case **X4**) is shown as a **disabled**
button with an explanation, instead of doing nothing on click.

## Supported operations

| # | Operation | Where |
|---|---|---|
| C1 | Merge subcategories of one group | `BeginTreeMerge(Category)` |
| C2 | Extend an existing category merge | `FindCategoryMergeSubsetOf` + deferred `ReplaceCategoryMergeSources` |
| C3 | Merge two category merges | subset reuse + `SupersedeCategoryMerges` at commit |
| G1 | Merge top-level groups | `BeginTreeMerge(Group)` |
| **G2** | **Add a group to an existing group merge (additive)** | merged-group node + raw groups → `AddSourceGroupsToGroupMerge` |
| S1 | Join two subcategory buckets (across source groups) | `MergeSubcategoryBuckets` |
| **S5** | **Merge same-source-group subcategories inside a group merge** | routed to a category merge (`cm`), not a bucket alias |
| **S2** | **Split previously joined buckets** | single `mgc:` node → `RemoveBucketAliasesTargeting` ("Split subcategories") |
| S3 | Pull one subcategory out of a group merge | `PartialUnmergeSubcategory` |
| S4 | Re-include a pulled-out subcategory | `ReincludeCategories` |
| A1/A2 | Group / ungroup animations | `BeginGridGroup` / `RemoveDisplayGroup` |
| A5/A7 | Skip / "as singles" in review | review pane |

Cross-level robustness:

- Partially-overlapping display groups are **kept intact** during a merge review (their in-scope
  members are excluded from re-detection) instead of being silently dissolved.
- A category renders under at most one `cm:` node even if two overlapping rules reference it.

## Known limitations (not yet surfaced)

The store already has symmetric primitives for these, but there is no dedicated UI yet:

- **G3 – remove one source group from a group merge**: `RemoveSourceGroupFromGroupMerge` exists,
  but there is no per-source-group node to target. Workaround: unmerge and re-merge, or pull the
  group's subcategories out one by one (S3).
- **C4 – remove one source subcategory from a category merge**: a `cm:` node has no child nodes,
  so an individual source can't be selected. Would need the cm node to expose its sources as
  collapsible children.
- **Cross-category display cards** (a card whose members live in different categories) still appear
  under each category node. This is treated as acceptable (the card is reachable from either
  category) rather than a bug.
- `cm` rules support `ExcludedSources` at render time, but no UI populates it (the field is only
  written for group merges). Implementing C4 would close this gap.

The KK / KKS AnimBrowser targets do not build yet (HS2 is the reference build) — see the project
memory note on the AnimBrowser port state.

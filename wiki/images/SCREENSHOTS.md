# Wiki screenshot & GIF assets

Place image files in the module subfolders below (synced with the full `wiki/` tree via `scripts/sync-github-wiki.ps1`).

```
wiki/images/
  pose-browser/    ← pb-*.png, pb-*.gif
  anim-browser/    ← ab-*.png, ab-*.gif
```

Wiki pages reference them as `images/pose-browser/…` or `images/anim-browser/…` (paths relative to the wiki repo root).

## Naming

| Prefix | Module | Folder |
|--------|--------|--------|
| `pb-` | Pose Browser | `images/pose-browser/` |
| `ab-` | Anim Browser | `images/anim-browser/` |

Use lowercase, hyphens, and a short topic slug: `pb-03-tag-filter.png`, `ab-07-group-selected.gif`.

## Formats

| Type | Format | Notes |
|------|--------|-------|
| Static UI | **PNG** | Crop to the relevant window/panel; 1200–1600 px wide is enough |
| Workflow / motion | **GIF** | 3–8 s, 10–15 fps, loop; keep file size under ~5 MB if possible |

## Capture tips

- Use a clean Studio scene (one or two characters, neutral background).
- Hide unrelated mod windows where possible.
- **Full layout** shots: Pose Browser / Anim Browser at a readable UI scale (100–125%).
- Redact or blur personal paths in file dialogs if visible.
- HS2-only features (hover preview, Heelz) should be labeled in the filename or captured on HS2.

## Placeholder convention in wiki pages

Each placeholder uses:

```markdown
![Alt text](images/pose-browser/pb-XX-slug.png)

> Descriptive caption matching what the screenshot shows…
```

Until the file exists, GitHub Wiki shows a broken image icon — add the file under the matching folder and re-sync.

## Checklist (Pose Browser)

| File | Wiki page |
|------|-----------|
| `pb-01-toolbar-icon.png` | [pose-browser/Home](Pose-Browser) |
| `pb-02-full-layout.png` | [pose-browser/Home](Pose-Browser) |
| `pb-03-list.png` | [pose-browser/Home](Pose-Browser) — List layout |
| `pb-03-mini.png` | [pose-browser/Home](Pose-Browser) — Mini layout |
| `pb-04-folder-tree.png` | [pose-browser/Folders-and-Library](Folders-and-Library) |
| `pb-05-move-copy-footer.png` | [pose-browser/Folders-and-Library](Folders-and-Library) |
| `pb-06-tag-filter.png` | [pose-browser/Search-Filters-and-Sort](Search-Filters-and-Sort) |
| `pb-07-grid-selection.png` | [pose-browser/Grid-and-Selection](Grid-and-Selection) |
| `pb-08-apply-pose.gif` | [pose-browser/Grid-and-Selection](Grid-and-Selection) |
| `pb-09-pose-group.png` | [pose-browser/Groups](Groups) |
| `pb-10-chars-pane.png` | [pose-browser/Multi-Character-Apply](Multi-Character-Apply) |
| `pb-11-multi-apply.gif` | [pose-browser/Multi-Character-Apply](Multi-Character-Apply) |
| `pb-12-stash-float.png` | [pose-browser/Stash](Stash) |
| `pb-13-items-pane.png` | [pose-browser/Items](Items) |
| `pb-14-import-preview.png` | [pose-browser/Import-Export-ZIP](Import-Export-ZIP) |
| `pb-15-thumbnail-overlay.png` | [pose-browser/Thumbnails](Thumbnails) |
| `pb-16-group-thumbnail.gif` | [pose-browser/Thumbnails](Thumbnails) |
| `pb-17-options-pane.png` | [pose-browser/Options-and-Data](Options-and-Data) |

## Checklist (Anim Browser)

| File | Wiki page | Shows |
|------|-----------|-------|
| `ab-01-toolbar-icon.png` | [Anim-Browser](Anim-Browser) | Green active Anim Browser icon (white leaping runner) on left toolbar |
| `ab-02-full-layout.png` | [Anim-Browser](Anim-Browser) | Main window, **Walking & Running**, 8 cards, Luna Clark |
| `ab-03-hover-preview.gif` | [Anim-Browser](Anim-Browser) (HS2) | Stick-figure hover preview on **Running 1** / **Walking 1** |
| `ab-04-first-apply.gif` | [Getting-Started](Getting-Started) | T-pose → **Walking 1** → Luna walks |
| `ab-05-grid.png` | [Browsing-and-Search](Browsing-and-Search) | Grid view, **Walking & Running**, props on Walking 3/4 |
| `ab-05-list.png` | [Browsing-and-Search](Browsing-and-Search) | List view, same category, **Walking 1** selected |
| `ab-06-search-highlight.png` | [Browsing-and-Search](Browsing-and-Search) | Search **Walk** in **hooh Animations 2020 → Actions** |
| `ab-07-grouped-card.png` | [Applying-Animations](Applying-Animations) | **Fast Loop** grouped card, **f1** / **f2** / **m** buttons |
| `ab-08-apply-animation.gif` | [Applying-Animations](Applying-Animations) | **Cowgirl** + Chars pane → apply group pose |
| `ab-09-docked-controls.png` | [Playback-Controls](Playback-Controls) | Docked Controls, **Fast Loop**, three characters |
| `ab-10-floating-controls.gif` | [Playback-Controls](Playback-Controls) | Floating Controls at 2.13×, main window closed |
| `ab-11-group-selected.gif` | [Grouping](Grouping) | Two **About To Begin** → Group Review |
| `ab-12-tree-merge.gif` | [Merging-Categories](Merging-Categories) | **Gift Service** + **Male Service** → merge **Service** |
| `ab-13-review-panel.png` | [Review-Panel](Review-Panel) | **Review add to merge: Service**, Behind Handjob groups |
| `ab-14-characters-options.png` | [Characters-and-Options](Characters-and-Options) | Options + Characters panes side by side |
| `ab-15-thumbnail-capture.png` | [Thumbnails](Thumbnails) | Green capture frame, **Walking 1** on Luna Clark (optional — add when captured) |

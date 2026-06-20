# GitHub Wiki sources

This folder contains the **source pages** for the [HS2-Sandbox GitHub Wiki](https://github.com/SuitIThub/HS2-Sandbox/wiki).

Wiki pages are plain Markdown. GitHub uses `Home.md` as the wiki front page and `_Sidebar.md` for the navigation sidebar.

## Publish to GitHub Wiki

### Option A — sync script (recommended)

From the repository root:

```powershell
.\scripts\sync-github-wiki.ps1
```

The script clones or updates the wiki git repository, copies all `wiki/*.md` files, commits, and pushes.

**Requirements:**

- Git installed
- Push access to `SuitIThub/HS2-Sandbox.wiki`
- GitHub Wiki enabled on the repository (Settings → Features → Wikis)

### Option B — manual

```bash
git clone https://github.com/SuitIThub/HS2-Sandbox.wiki.git
cp wiki/*.md HS2-Sandbox.wiki/
cd HS2-Sandbox.wiki
git add -A
git commit -m "Sync wiki from main repo"
git push
```

## Keep docs in sync

When updating module behavior, update:

| Wiki | In-repo long form |
|------|-------------------|
| `Pose-Browser-*.md` | `docs/PoseBrowser-HS2Wiki-Manual.md` |
| `Anim-Browser-*.md` | `docs/AnimBrowser-HS2Wiki-Manual.md` |
| `Pose-ZIP-Format.md` | `docs/POSE_ZIP_FORMAT.md` |

HS2Wiki in-game pages are registered separately via `PoseBrowserWikiRegistration` / `AnimBrowserWikiRegistration`.

## Page naming

GitHub Wiki page titles come from filenames without `.md`:

- `Home.md` → wiki home
- `Pose-Browser.md` → page title "Pose Browser", link `[Pose Browser](Pose-Browser)`

Use hyphens in filenames; spaces become hyphens in URLs.

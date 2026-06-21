# GitHub Wiki sources

This folder contains the **source pages** for the [HS2-Sandbox GitHub Wiki](https://github.com/SuitIThub/HS2-Sandbox/wiki).

GitHub uses `Home.md` as the wiki front page and `_Sidebar.md` for the navigation sidebar. **Folders are for organization only** — GitHub indexes each page by its **filename without `.md`**, not by folder path.

## Folder layout

```
wiki/
  Home.md                 # Wiki front page (must stay at root)
  _Sidebar.md             # Sidebar navigation
  README.md               # This file (maintainers only)
  getting-started/        # Install, requirements, troubleshooting
  hs2/                    # HS2-only modules (CopyScript, Timeline, …)
  pose-browser/           # Pose Browser guide (module home: Pose-Browser.md)
  anim-browser/           # Anim Browser guide (module home: Anim-Browser.md)
  reference/              # Config files, hotkeys, ZIP format, plugin compat
  developers/             # Architecture, build, CI
  images/
    pose-browser/         # pb-*.png / pb-*.gif assets
    anim-browser/         # ab-*.png / ab-*.gif assets
    SCREENSHOTS.md        # Capture checklist
```

## Publish to GitHub Wiki

### Option A — sync script (recommended)

From the repository root:

```powershell
# First time (wiki not on GitHub yet): enable Wikis in repo Settings, then:
.\scripts\sync-github-wiki.ps1 -Init

# Later updates:
.\scripts\sync-github-wiki.ps1
```

The script copies the **entire `wiki/` tree** (pages + `images/`) to the wiki git repository, commits, and pushes.

**If you see "Repository not found":**

1. GitHub → repo **Settings** → **Features** → enable **Wikis**
2. Run `.\scripts\sync-github-wiki.ps1 -Init`
3. Ensure git is authenticated (`gh auth login` or Git Credential Manager) if the repo is private

### Option B — manual

```bash
git clone https://github.com/SuitIThub/HS2-Sandbox.wiki.git
rsync -av --delete wiki/ HS2-Sandbox.wiki/ --exclude .git
cd HS2-Sandbox.wiki
git add -A
git commit -m "Sync wiki from main repo"
git push
```

## Keep docs in sync

| Wiki | In-repo long form |
|------|-------------------|
| `pose-browser/*.md` | `docs/PoseBrowser-HS2Wiki-Manual.md` |
| `anim-browser/*.md` | `docs/AnimBrowser-HS2Wiki-Manual.md` |
| `reference/Pose-ZIP-Format.md` | `docs/POSE_ZIP_FORMAT.md` |
| `images/SCREENSHOTS.md` | Screenshot/GIF checklist |

HS2Wiki in-game pages are registered separately via `PoseBrowserWikiRegistration` / `AnimBrowserWikiRegistration`.

## Page naming & links

**Important:** GitHub Wiki links use the **page basename only**. Paths like `(pose-browser/Home)` or `(hs2/Timeline)` return **404**. Always link by filename:

| File | Wiki URL | Markdown link |
|------|----------|---------------|
| `Home.md` | `/wiki/Home` | `[Home](Home)` |
| `pose-browser/Pose-Browser.md` | `/wiki/Pose-Browser` | `[Pose Browser](Pose-Browser)` |
| `pose-browser/Groups.md` | `/wiki/Groups` | `[Pose groups](Groups)` |
| `hs2/Timeline.md` | `/wiki/Timeline` | `[Timeline](Timeline)` |

Do **not** name subfolder overview pages `Home.md` — that collides with the wiki front page. Use `Pose-Browser.md`, `Anim-Browser.md`, etc.

After reorganizing folders or renaming pages, run `.\scripts\fix-wiki-links.ps1` to rewrite internal links, then sync.

Image paths use the wiki-root form `images/pose-browser/pb-01-toolbar-icon.png` (same from any subfolder page — GitHub Wiki resolves these from the repository root, not from the page folder).

## Adding screenshots

1. Capture per [`images/SCREENSHOTS.md`](images/SCREENSHOTS.md)
2. Save under `wiki/images/pose-browser/` or `wiki/images/anim-browser/`
3. Run `.\scripts\sync-github-wiki.ps1`

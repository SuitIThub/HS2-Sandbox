# Contributing & CI

## License

[CC BY 4.0](https://github.com/SuitIThub/HS2-Sandbox/blob/main/LICENSE) — credit **Suit-Ji** as original author when forking or redistributing.

## Releases & CI

GitHub Actions workflow: `.github/workflows/main.yml`

Manual run modes (Actions → Main → Run workflow):

| Mode | Behavior |
|------|----------|
| **detect** | Compare last two commits for version bumps; release changed modules (default on push) |
| **manual_rerelease** | Publish checked plugins without version bump |
| **force_release** | Publish all release-detect modules at current versions |
| **update_readme_only** | Refresh README links and `versions.json` only |

Details: `.github/WORKFLOW.md`

## Plugin manifest

`.github/plugins.manifest.json` — CI registry for:

- Build paths and DLL names
- `versions.json` keys
- README badge/update targets
- Manual rerelease checkbox groups (multi-game modules grouped)

### Adding a new plugin

1. Add entry to `plugins.manifest.json`
2. For multi-game modules, add `manualRereleaseGroups` entry
3. Run `python .github/scripts/sync_workflow_rerelease_inputs.py`
4. Commit updated `main.yml` and manifest

## Version bumps

Bump `PluginVersion` or version constants in the module's version file (see manifest `versionFile` field). Push to `main` triggers **detect** mode.

`versions.json` is updated **once** after each successful GitHub release.

## Wiki maintenance

Wiki sources live in **`wiki/`** in the main repository. Sync to GitHub Wiki:

```powershell
.\scripts\sync-github-wiki.ps1
```

Or manually clone `https://github.com/SuitIThub/HS2-Sandbox.wiki.git`, copy `wiki/*.md`, commit, push.

Keep in-repo docs in sync:

- `docs/PoseBrowser-HS2Wiki-Manual.md`
- `docs/AnimBrowser-HS2Wiki-Manual.md`
- `docs/POSE_ZIP_FORMAT.md`

## Code conventions

- **No JsonUtility** for new persistence (see `.cursor/rules/no-jsonutility.mdc`)
- **IMGUI performance** — virtualize lists, cache draw data (see `.cursor/rules/imgui-performance.mdc`)
- Match existing naming and partial-class patterns in each module

## Reporting issues

Include:

- Game and module DLL version
- Relevant section of `BepInEx/LogOutput.log`
- Steps to reproduce
- Which module DLLs are loaded

→ [Building from source](Building-from-Source) · [Architecture](Architecture)

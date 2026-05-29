# Main workflow (manual runs)

Use **Actions → Main → Run workflow** on `main`.

## Run mode (dropdown)

| Value | What happens |
|--------|----------------|
| **detect** | Compare last two commits for `PluginVersion` bumps; release only changed modules (default). |
| **manual_rerelease** | Publish the DLLs you tick below (no version bump required). |
| **force_release** | Publish all release-detect modules at current versions. |
| **update_readme_only** | Refresh `README.md` download links and `versions.json` only — no build, no GitHub release. |

Push to `main` always uses **detect** (this dropdown is ignored).

## Rerelease plugin checkboxes

Only used when **run_mode** is **manual_rerelease**. Check one or more plugins, then run.

Plugin list is generated from [plugins.manifest.json](plugins.manifest.json) (`releaseDetect: true` entries). Labels match each plugin’s `displayName`.

## Adding a new plugin

1. Add an entry to `plugins.manifest.json`.
2. Run: `python .github/scripts/sync_workflow_rerelease_inputs.py`
3. Commit the updated `main.yml` checkbox section and manifest.

## GitHub vs GitLab

GitHub Actions does not support per-job manual triggers or inputs defined from a JSON file at runtime. The manifest drives CI scripts; checkboxes in the workflow YAML are **generated** from that manifest so the UI stays usable.

#!/usr/bin/env bash
set -euo pipefail

extract_version() {
  grep -oP 'PluginVersion\s*=\s*"\K[^"]+' "$1" | head -n1
}

jq -n \
  --arg allInOne "$(extract_version HS2SandboxPlugin.cs)" \
  --arg copyScript "$(extract_version Modules/CopyScript/CopyScriptModulePlugin.cs)" \
  --arg timeline "$(extract_version Modules/Timeline/TimelineModulePlugin.cs)" \
  --arg searchBarManager "$(extract_version Modules/SearchBarManager/SearchBarManagerModulePlugin.cs)" \
  --arg sonScale "$(extract_version Modules/SonScale/SonScaleModulePlugin.cs)" \
  --arg workspaceTreeLock "$(extract_version Modules/WorkspaceTreeLock/WorkspaceTreeLockModulePlugin.cs)" \
  --arg notebook "$(extract_version Modules/Notebook/NotebookModulePlugin.cs)" \
  --arg poseBrowser "$(extract_version Modules/PoseBrowser/PoseBrowserModulePlugin.cs)" \
  '{schemaVersion: 1, allInOne: $allInOne, copyScript: $copyScript, timeline: $timeline, searchBarManager: $searchBarManager, sonScale: $sonScale, workspaceTreeLock: $workspaceTreeLock, notebook: $notebook, poseBrowser: $poseBrowser}' \
  > versions.json

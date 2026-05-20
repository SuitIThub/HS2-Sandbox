#!/usr/bin/env bash
# Detect PluginVersion bumps between two git refs and decide whether to release.
# Writes outputs to GITHUB_OUTPUT (see workflow detect job).
set -euo pipefail

BEFORE="${1:?before ref required}"
AFTER="${2:?after ref required}"
FORCE="${3:-false}"

extract_version() {
  local ref="$1"
  local path="$2"
  git show "${ref}:${path}" 2>/dev/null | grep -oP '(?:PluginVersion|const string Version)\s*=\s*"\K[^"]+' | head -n1 || true
}

declare -A FILES=(
  [allinone]="HS2SandboxPlugin.cs"
  [copyscript]="Modules/CopyScript/CopyScriptModulePlugin.cs"
  [timeline]="Modules/Timeline/TimelineModulePlugin.cs"
  [searchbarmanager]="Modules/SearchBarManager/SearchBarManagerModulePlugin.cs"
  [sonscale]="Modules/SonScale/SonScaleModulePlugin.cs"
  [workspacetreelock]="Modules/WorkspaceTreeLock/WorkspaceTreeLockModulePlugin.cs"
  [notebook]="Modules/Notebook/NotebookModulePlugin.cs"
  [posebrowser]="PoseBrowser/PoseBrowserVersionInfo.cs"
)

declare -A DLLS=(
  [allinone]="bin/Release/HS2SandboxPlugin.dll"
  [copyscript]="Modules/CopyScript/bin/Release/HS2Sandbox.CopyScript.dll"
  [timeline]="Modules/Timeline/bin/Release/HS2Sandbox.Timeline.dll"
  [searchbarmanager]="Modules/SearchBarManager/bin/Release/HS2Sandbox.SearchBarManager.dll"
  [sonscale]="Modules/SonScale/bin/Release/HS2Sandbox.SonScale.dll"
  [workspacetreelock]="Modules/WorkspaceTreeLock/bin/Release/HS2Sandbox.WorkspaceTreeLock.dll"
  [notebook]="Modules/Notebook/bin/Release/HS2Sandbox.Notebook.dll"
  [posebrowser]="Modules/PoseBrowser/bin/Release/HS2Sandbox.PoseBrowser.dll"
)

CHANGED=()
NOTES=()
CHANGELOG=()
VERSIONS_CHANGED="false"

for key in allinone copyscript timeline searchbarmanager sonscale workspacetreelock notebook posebrowser; do
  f="${FILES[$key]}"
  old_v="$(extract_version "$BEFORE" "$f")"
  new_v="$(extract_version "$AFTER" "$f")"
  if [[ -z "$new_v" ]]; then
    echo "::warning::Could not read PluginVersion from $f at $AFTER"
    continue
  fi
  if [[ "$old_v" != "$new_v" ]]; then
    VERSIONS_CHANGED="true"
    CHANGED+=("$key")
    NOTES+=("- **${key}**: \`${old_v:-∅}\` → \`${new_v}\`")
  fi
done

if git rev-parse "$BEFORE" >/dev/null 2>&1 && git rev-parse "$AFTER" >/dev/null 2>&1; then
  while IFS= read -r line; do
    [[ -z "${line// }" ]] && continue
    CHANGELOG+=("$line")
  done < <(git log --no-merges --pretty=format:'- %s (%h)' "${BEFORE}..${AFTER}")
fi
if [[ ${#CHANGELOG[@]} -eq 0 ]]; then
  fallback_subject="$(git log -1 --pretty=%s "$AFTER" 2>/dev/null || echo "Update release contents")"
  fallback_hash="$(git rev-parse --short "$AFTER" 2>/dev/null || echo "HEAD")"
  CHANGELOG+=("- ${fallback_subject} (${fallback_hash})")
fi

INITIAL_RELEASE="false"
if [[ ${#CHANGED[@]} -eq 0 ]]; then
  REL_JSON=$(curl -sS -H "Authorization: Bearer ${GITHUB_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "https://api.github.com/repos/${GITHUB_REPOSITORY}/releases?per_page=1")
  REL_COUNT=$(echo "$REL_JSON" | jq 'length' 2>/dev/null || echo 1)
  if [[ "$REL_COUNT" =~ ^[0-9]+$ ]] && [[ "$REL_COUNT" -eq 0 ]]; then
    INITIAL_RELEASE="true"
    echo "No GitHub releases exist on this repository yet; will publish an initial release."
  fi
fi

SHOULD_RELEASE="false"
if [[ "$FORCE" == "true" || ${#CHANGED[@]} -gt 0 || "$INITIAL_RELEASE" == "true" ]]; then
  SHOULD_RELEASE="true"
fi

if [[ "$FORCE" == "true" && ${#CHANGED[@]} -eq 0 ]]; then
  CHANGED=(copyscript timeline searchbarmanager sonscale workspacetreelock notebook posebrowser)
  NOTES+=("- **manual force**: including all module DLLs plus all-in-one")
elif [[ "$INITIAL_RELEASE" == "true" && ${#CHANGED[@]} -eq 0 ]]; then
  CHANGED=(copyscript timeline searchbarmanager sonscale workspacetreelock notebook posebrowser)
  NOTES+=("- **initial GitHub release**: repository had no prior releases; publishing all module DLLs plus all-in-one")
fi

{
  echo "versions_changed=$VERSIONS_CHANGED"
  echo "should_release=$SHOULD_RELEASE"
  echo "initial_release=$INITIAL_RELEASE"
} >> "$GITHUB_OUTPUT"

if [[ "$SHOULD_RELEASE" != "true" ]]; then
  echo "No PluginVersion changes between $BEFORE and $AFTER; release not needed."
  exit 0
fi

ASSETS=()
ASSETS+=("${DLLS[allinone]}")
for key in "${CHANGED[@]}"; do
  if [[ "$key" != "allinone" ]]; then
    ASSETS+=("${DLLS[$key]}")
  fi
done

UNIQUE=()
for p in "${ASSETS[@]}"; do
  skip=false
  for u in "${UNIQUE[@]}"; do
    [[ "$u" == "$p" ]] && skip=true && break
  done
  $skip || UNIQUE+=("$p")
done

LIST_FILE="${GITHUB_WORKSPACE}/.release-asset-paths.txt"
printf '%s\n' "${UNIQUE[@]}" > "$LIST_FILE"
echo "asset_list_file=$LIST_FILE" >> "$GITHUB_OUTPUT"
asset_paths_csv="$(IFS=,; echo "${UNIQUE[*]}")"
echo "asset_paths=$asset_paths_csv" >> "$GITHUB_OUTPUT"

{
  echo 'notes<<NOTEEOF'
  echo "### Version changes"
  if [[ ${#NOTES[@]} -gt 0 ]]; then
    printf '%s\n' "${NOTES[@]}"
  fi
  echo ""
  echo "### Artifacts"
  printf '%s\n' "${UNIQUE[@]}" | while read -r line; do
    [[ -z "$line" ]] && continue
    echo "- \`$(basename "$line")\`"
  done
  echo ""
  echo "### Changelog"
  printf '%s\n' "${CHANGELOG[@]}"
  echo 'NOTEEOF'
} >> "$GITHUB_OUTPUT"

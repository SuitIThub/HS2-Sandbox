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

# --- Sortable release tag/title: release-r<date>-<plugin(s)> <version(s)> ---
# "release-r" sorts above legacy release-<git-sha> tags when the releases list is
# ordered by name (descending): 'r' > hex digits.

RELEASE_KEYS=()
if [[ ${#CHANGED[@]} -gt 0 ]]; then
  for key in "${CHANGED[@]}"; do
    RELEASE_KEYS+=("$key")
  done
else
  RELEASE_KEYS=(allinone)
fi
IFS=$'\n' RELEASE_KEYS=($(printf '%s\n' "${RELEASE_KEYS[@]}" | sort -u))
unset IFS

RELEASE_DATE="$(git log -1 --format=%cs "$AFTER" 2>/dev/null || date -u +%Y-%m-%d)"
PLUGIN_SLUG="$(IFS=+; echo "${RELEASE_KEYS[*]}")"

RELEASE_VERSIONS=()
for key in "${RELEASE_KEYS[@]}"; do
  v="$(extract_version "$AFTER" "${FILES[$key]}")"
  [[ -z "$v" ]] && v="unknown"
  RELEASE_VERSIONS+=("$v")
done
VERSION_SLUG="$(IFS=+; echo "${RELEASE_VERSIONS[*]}")"

fetch_releases_page() {
  curl -sS -H "Authorization: Bearer ${GITHUB_TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "https://api.github.com/repos/${GITHUB_REPOSITORY}/releases?per_page=100&page=${1}"
}

# Reuse tag/title if this commit already has a release.
EXISTING_TAG=""
EXISTING_NAME=""
page=1
while [[ "$page" -le 20 ]]; do
  REL_PAGE="$(fetch_releases_page "$page")"
  count=$(echo "$REL_PAGE" | jq 'length' 2>/dev/null || echo 0)
  [[ "$count" -eq 0 ]] && break
  EXISTING_TAG="$(echo "$REL_PAGE" | jq -r --arg sha "$AFTER" \
    '.[] | select(.target_commitish == $sha) | .tag_name' 2>/dev/null | head -n1)"
  if [[ -n "$EXISTING_TAG" && "$EXISTING_TAG" != "null" ]]; then
    EXISTING_NAME="$(echo "$REL_PAGE" | jq -r --arg t "$EXISTING_TAG" \
      '.[] | select(.tag_name == $t) | .name' 2>/dev/null | head -n1)"
    break
  fi
  [[ "$count" -lt 100 ]] && break
  page=$((page + 1))
done

tag_suffix_for_stem() {
  local tag="$1"
  local stem="$2"
  local versions="$3"
  local expected="${stem}-${versions}"
  if [[ "$tag" == "$expected" ]]; then
    echo 1
    return
  fi
  local prefix="${stem}-"
  [[ "$tag" != "${prefix}"* ]] && return
  local rest="${tag#"${prefix}"}"
  if [[ "$rest" =~ ^([0-9]+)- ]]; then
    local inc="${BASH_REMATCH[1]}"
    local ver_part="${rest#"${inc}"-}"
    if [[ "$ver_part" == "$versions" ]]; then
      echo "$inc"
    fi
  fi
}

if [[ -n "$EXISTING_TAG" && "$EXISTING_TAG" != "null" ]]; then
  RELEASE_TAG="$EXISTING_TAG"
  if [[ -n "$EXISTING_NAME" && "$EXISTING_NAME" != "null" ]]; then
    RELEASE_NAME="$EXISTING_NAME"
  else
    RELEASE_NAME="release-r${RELEASE_DATE}-${PLUGIN_SLUG} ${VERSION_SLUG}"
  fi
else
  BASE_STEM="release-r${RELEASE_DATE}-${PLUGIN_SLUG}"
  MAX_INC=0
  page=1
  while [[ "$page" -le 20 ]]; do
    REL_PAGE="$(fetch_releases_page "$page")"
    count=$(echo "$REL_PAGE" | jq 'length' 2>/dev/null || echo 0)
    [[ "$count" -eq 0 ]] && break
    while IFS= read -r tag; do
      [[ -z "$tag" ]] && continue
      inc="$(tag_suffix_for_stem "$tag" "$BASE_STEM" "$VERSION_SLUG")"
      [[ -n "$inc" && "$inc" -gt "$MAX_INC" ]] && MAX_INC="$inc"
    done < <(echo "$REL_PAGE" | jq -r '.[].tag_name' 2>/dev/null)
    [[ "$count" -lt 100 ]] && break
    page=$((page + 1))
  done

  if [[ "$MAX_INC" -eq 0 ]]; then
    INC_STEM="$BASE_STEM"
  else
    INC_STEM="${BASE_STEM}-$((MAX_INC + 1))"
  fi

  RELEASE_NAME="${INC_STEM} ${VERSION_SLUG}"
  RELEASE_TAG="${INC_STEM}-${VERSION_SLUG}"
fi

{
  echo "release_tag=$RELEASE_TAG"
  echo "release_name=$RELEASE_NAME"
} >> "$GITHUB_OUTPUT"
echo "Release tag: $RELEASE_TAG"
echo "Release name: $RELEASE_NAME"

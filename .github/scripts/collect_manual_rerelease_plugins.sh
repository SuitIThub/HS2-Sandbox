#!/usr/bin/env bash
# Map workflow_dispatch boolean inputs to a comma-separated plugin key list.
set -euo pipefail

SELECTED=()

if [[ "${RERELEASE_COPYSCRIPT:-false}" == "true" ]]; then
  SELECTED+=("copyscript")
fi
if [[ "${RERELEASE_TIMELINE:-false}" == "true" ]]; then
  SELECTED+=("timeline")
fi
if [[ "${RERELEASE_SEARCHBARMANAGER:-false}" == "true" ]]; then
  SELECTED+=("searchbarmanager")
fi
if [[ "${RERELEASE_SONSCALE:-false}" == "true" ]]; then
  SELECTED+=("sonscale")
fi
if [[ "${RERELEASE_WORKSPACETREELOCK:-false}" == "true" ]]; then
  SELECTED+=("workspacetreelock")
fi
if [[ "${RERELEASE_NOTEBOOK:-false}" == "true" ]]; then
  SELECTED+=("notebook")
fi
if [[ "${RERELEASE_POSEBROWSER:-false}" == "true" ]]; then
  SELECTED+=("posebrowser")
fi
if [[ "${RERELEASE_POSEBROWSER_KKS:-false}" == "true" ]]; then
  SELECTED+=("posebrowserkks")
fi

if [[ ${#SELECTED[@]} -eq 0 ]]; then
  echo "::error::Manual rerelease enabled but no plugin was selected. Check one or more plugin boxes, then run."
  exit 1
fi

plugins_csv="$(IFS=,; echo "${SELECTED[*]}")"
echo "Selected plugins: $plugins_csv"
echo "plugins=$plugins_csv" >> "${GITHUB_OUTPUT:?GITHUB_OUTPUT must be set}"

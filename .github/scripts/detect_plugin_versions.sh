#!/usr/bin/env bash
# Delegates to detect_plugin_versions.py (plugin list: .github/plugins.manifest.json).
set -euo pipefail
exec python3 "$(dirname "$0")/detect_plugin_versions.py" "$@"

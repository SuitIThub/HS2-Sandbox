#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 .github/scripts/sync_versions_json.py

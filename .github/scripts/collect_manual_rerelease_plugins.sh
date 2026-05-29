#!/usr/bin/env bash
# Deprecated: use validate_rerelease_plugins.py with RERELEASE_PLUGINS env or workflow input.
set -euo pipefail
exec python3 "$(dirname "$0")/validate_rerelease_plugins.py" "${RERELEASE_PLUGINS:-}"

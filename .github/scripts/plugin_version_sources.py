"""Plugin version constants in source and matching release assembly names."""
from __future__ import annotations

import re
from pathlib import Path

from plugin_manifest import (
    project_version_sources,
    read_plugin_version,
    version_files_by_json_key,
)

ROOT = Path(__file__).resolve().parents[2]

VERSION_RE = re.compile(
    r'(?:PluginVersion|public\s+const\s+string\s+Version)\s*=\s*"([^"]+)"'
)

PROJECT_VERSION_SOURCES: dict[str, tuple[Path, int]] = project_version_sources()
VERSION_FILES: dict[str, tuple[Path, int]] = version_files_by_json_key()

__all__ = [
    "PROJECT_VERSION_SOURCES",
    "VERSION_FILES",
    "VERSION_RE",
    "read_plugin_version",
]

"""Plugin version constants in source and matching release assembly names."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

VERSION_RE = re.compile(
    r'(?:PluginVersion|public\s+const\s+string\s+Version)\s*=\s*"([^"]+)"'
)

# .csproj name without extension (MSBuildProjectName) -> C# file with PluginVersion / Version
PROJECT_VERSION_SOURCES: dict[str, Path] = {
    "HS2Sandbox.CopyScript": ROOT / "targets/HS2/CopyScript/Plugin.cs",
    "HS2Sandbox.Timeline": ROOT / "targets/HS2/Timeline/Plugin.cs",
    "HS2Sandbox.SearchBarManager": ROOT / "targets/HS2/SearchBarManager/Plugin.cs",
    "HS2Sandbox.SonScale": ROOT / "targets/HS2/SonScale/Plugin.cs",
    "HS2Sandbox.WorkspaceTreeLock": ROOT / "targets/HS2/WorkspaceTreeLock/Plugin.cs",
    "HS2Sandbox.Notebook": ROOT / "targets/HS2/Notebook/Plugin.cs",
    "HS2Sandbox.PoseBrowser": ROOT / "src/PoseBrowser/PoseBrowserVersionInfo.cs",
}

# versions.json / README keys -> same C# sources (for sync_versions_json.py)
VERSION_FILES: dict[str, Path] = {
    "copyScript": PROJECT_VERSION_SOURCES["HS2Sandbox.CopyScript"],
    "timeline": PROJECT_VERSION_SOURCES["HS2Sandbox.Timeline"],
    "searchBarManager": PROJECT_VERSION_SOURCES["HS2Sandbox.SearchBarManager"],
    "sonScale": PROJECT_VERSION_SOURCES["HS2Sandbox.SonScale"],
    "workspaceTreeLock": PROJECT_VERSION_SOURCES["HS2Sandbox.WorkspaceTreeLock"],
    "notebook": PROJECT_VERSION_SOURCES["HS2Sandbox.Notebook"],
    "poseBrowser": PROJECT_VERSION_SOURCES["HS2Sandbox.PoseBrowser"],
}


def read_plugin_version(path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    match = VERSION_RE.search(text)
    if not match:
        raise ValueError(f"PluginVersion not found in {path}")
    return match.group(1)

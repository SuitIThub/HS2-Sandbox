"""Plugin version constants in source and matching release assembly names."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

VERSION_RE = re.compile(
    r'(?:PluginVersion|public\s+const\s+string\s+Version)\s*=\s*"([^"]+)"'
)

# .csproj name without extension (MSBuildProjectName) -> (C# file, match_index)
# match_index selects which occurrence in files with multiple Version constants.
PROJECT_VERSION_SOURCES: dict[str, tuple[Path, int]] = {
    "HS2Sandbox.CopyScript": (ROOT / "targets/HS2/CopyScript/Plugin.cs", 0),
    "HS2Sandbox.Timeline": (ROOT / "targets/HS2/Timeline/Plugin.cs", 0),
    "HS2Sandbox.SearchBarManager": (ROOT / "targets/HS2/SearchBarManager/Plugin.cs", 0),
    "HS2Sandbox.SonScale": (ROOT / "targets/HS2/SonScale/Plugin.cs", 0),
    "HS2Sandbox.WorkspaceTreeLock": (ROOT / "targets/HS2/WorkspaceTreeLock/Plugin.cs", 0),
    "HS2Sandbox.Notebook": (ROOT / "targets/HS2/Notebook/Plugin.cs", 0),
    "HS2Sandbox.PoseBrowser": (ROOT / "src/PoseBrowser/PoseBrowserVersionInfo.cs", 1),
    "KKSSandbox.PoseBrowser": (ROOT / "src/PoseBrowser/PoseBrowserVersionInfo.cs", 0),
}

# versions.json / README keys -> same C# sources (for sync_versions_json.py)
VERSION_FILES: dict[str, tuple[Path, int]] = {
    "copyScript": PROJECT_VERSION_SOURCES["HS2Sandbox.CopyScript"],
    "timeline": PROJECT_VERSION_SOURCES["HS2Sandbox.Timeline"],
    "searchBarManager": PROJECT_VERSION_SOURCES["HS2Sandbox.SearchBarManager"],
    "sonScale": PROJECT_VERSION_SOURCES["HS2Sandbox.SonScale"],
    "workspaceTreeLock": PROJECT_VERSION_SOURCES["HS2Sandbox.WorkspaceTreeLock"],
    "notebook": PROJECT_VERSION_SOURCES["HS2Sandbox.Notebook"],
    "poseBrowser": PROJECT_VERSION_SOURCES["HS2Sandbox.PoseBrowser"],
    "poseBrowserKks": PROJECT_VERSION_SOURCES["KKSSandbox.PoseBrowser"],
}


def read_plugin_version(source: Path | tuple[Path, int]) -> str:
    if isinstance(source, tuple):
        path, index = source
    else:
        path, index = source, 0
    text = path.read_text(encoding="utf-8")
    matches = VERSION_RE.findall(text)
    if not matches:
        raise ValueError(f"PluginVersion not found in {path}")
    if index >= len(matches):
        raise ValueError(f"PluginVersion match index {index} out of range in {path} ({len(matches)} found)")
    return matches[index]

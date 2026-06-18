"""Load .github/plugins.manifest.json — single source of truth for CI plugin metadata."""
from __future__ import annotations

import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator

ROOT = Path(__file__).resolve().parents[2]
MANIFEST_PATH = Path(__file__).resolve().parent.parent / "plugins.manifest.json"

VERSION_RE = re.compile(
    r'(?:PluginVersion|public\s+const\s+string\s+Version)\s*=\s*"([^"]+)"'
)


@dataclass(frozen=True)
class ManualRereleaseGroup:
    key: str
    display_name: str
    member_keys: tuple[str, ...]


@dataclass(frozen=True)
class PluginEntry:
    key: str
    display_name: str
    version_file: Path
    version_match_index: int
    release_detect: bool
    versions_json_key: str | None
    release_dll_file_name: str | None
    release_artifact_path: Path | None
    project_name: str | None
    readme_heading: str | None
    readme_download_label: str | None


def _parse_entry(raw: dict) -> PluginEntry:
    readme = raw.get("readme") or {}
    version_file = ROOT / raw["versionFile"]
    artifact = raw.get("releaseArtifactPath")
    return PluginEntry(
        key=str(raw["key"]).lower(),
        display_name=str(raw.get("displayName") or raw["key"]),
        version_file=version_file,
        version_match_index=int(raw.get("versionMatchIndex", 0)),
        release_detect=bool(raw.get("releaseDetect", True)),
        versions_json_key=raw.get("versionsJsonKey"),
        release_dll_file_name=raw.get("releaseDllFileName"),
        release_artifact_path=(ROOT / artifact) if artifact else None,
        project_name=raw.get("projectName"),
        readme_heading=readme.get("heading"),
        readme_download_label=readme.get("downloadLabel"),
    )


def load_manifest_raw() -> dict:
    return json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))


def load_manifest() -> list[PluginEntry]:
    data = load_manifest_raw()
    return [_parse_entry(item) for item in data["entries"]]


def manual_rerelease_groups() -> list[ManualRereleaseGroup]:
    data = load_manifest_raw()
    groups: list[ManualRereleaseGroup] = []
    for raw in data.get("manualRereleaseGroups") or []:
        groups.append(
            ManualRereleaseGroup(
                key=str(raw["key"]).lower(),
                display_name=str(raw.get("displayName") or raw["key"]),
                member_keys=tuple(str(k).lower() for k in raw["memberKeys"]),
            )
        )
    return groups


def grouped_release_detect_keys() -> set[str]:
    out: set[str] = set()
    for group in manual_rerelease_groups():
        out.update(group.member_keys)
    return out


def manual_rerelease_singleton_entries() -> list[PluginEntry]:
    grouped = grouped_release_detect_keys()
    return [e for e in release_detect_entries() if e.key not in grouped]


def entry_by_key() -> dict[str, PluginEntry]:
    return {e.key: e for e in load_manifest()}


def release_detect_entries() -> list[PluginEntry]:
    return [e for e in load_manifest() if e.release_detect]


def release_detect_keys() -> list[str]:
    return [e.key for e in release_detect_entries()]


def workflow_input_id(plugin_key: str) -> str:
    """workflow_dispatch boolean input name for manual rerelease."""
    return f"rerelease_{plugin_key}"


def workflow_group_input_id(group_key: str) -> str:
    """workflow_dispatch boolean input name for a grouped manual rerelease."""
    return workflow_input_id(group_key)


def expand_manual_rerelease_selection(selected_keys: list[str]) -> list[str]:
    """Expand grouped workflow input keys to individual release-detect plugin keys."""
    by_key = entry_by_key()
    group_by_key = {g.key: g for g in manual_rerelease_groups()}
    expanded: list[str] = []
    seen: set[str] = set()
    for key in selected_keys:
        group = group_by_key.get(key)
        member_keys = group.member_keys if group else (key,)
        for member in member_keys:
            if member not in by_key or not by_key[member].release_detect:
                raise ValueError(f"Unknown or non-release plugin key in group '{key}': {member}")
            if member in seen:
                continue
            seen.add(member)
            expanded.append(member)
    return expanded


def versions_json_entries() -> list[PluginEntry]:
    return [e for e in load_manifest() if e.versions_json_key]


def readme_entries() -> list[PluginEntry]:
    return [e for e in load_manifest() if e.readme_heading and e.release_dll_file_name and e.readme_download_label]


def project_version_sources() -> dict[str, tuple[Path, int]]:
    out: dict[str, tuple[Path, int]] = {}
    for e in load_manifest():
        if not e.project_name:
            continue
        out[e.project_name] = (e.version_file, e.version_match_index)
    return out


def version_files_by_json_key() -> dict[str, tuple[Path, int]]:
    out: dict[str, tuple[Path, int]] = {}
    for e in versions_json_entries():
        assert e.versions_json_key is not None
        out[e.versions_json_key] = (e.version_file, e.version_match_index)
    return out


def release_dlls_by_json_key() -> dict[str, str]:
    out: dict[str, str] = {}
    for e in versions_json_entries():
        if e.versions_json_key and e.release_dll_file_name:
            out[e.versions_json_key] = e.release_dll_file_name
    return out


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
        raise ValueError(
            f"PluginVersion match index {index} out of range in {path} ({len(matches)} found)"
        )
    return matches[index]


def read_version_at_git_ref(ref: str, entry: PluginEntry) -> str:
    rel = entry.version_file.relative_to(ROOT).as_posix()
    result = subprocess.run(
        ["git", "show", f"{ref}:{rel}"],
        capture_output=True,
        text=True,
        cwd=ROOT,
    )
    if result.returncode != 0:
        return ""
    matches = VERSION_RE.findall(result.stdout)
    if not matches:
        return ""
    idx = entry.version_match_index
    if idx >= len(matches):
        return ""
    return matches[idx]


def parse_plugin_keys_arg(value: str) -> list[str]:
    return [part.strip().lower() for part in value.split(",") if part.strip()]


def validate_release_detect_keys(keys: list[str]) -> list[str]:
    expanded = expand_manual_rerelease_selection(keys)
    known = {e.key for e in release_detect_entries()}
    invalid = [k for k in expanded if k not in known]
    if invalid:
        raise ValueError(f"Unknown plugin key(s): {', '.join(invalid)}")
    return expanded


def list_release_detect_keys() -> None:
    for entry in manual_rerelease_singleton_entries():
        print(f"  {entry.key} - {entry.display_name}")
    for group in manual_rerelease_groups():
        members = ", ".join(group.member_keys)
        print(f"  {group.key} - {group.display_name} [{members}]")


def main() -> None:
    if len(sys.argv) > 1 and sys.argv[1] == "--list-keys":
        print("Plugin keys (manual rerelease, comma-separated):")
        list_release_detect_keys()
        return
    print("Usage: plugin_manifest.py --list-keys", file=sys.stderr)
    sys.exit(1)


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Detect PluginVersion bumps between two git refs and decide whether to release.

Writes outputs to GITHUB_OUTPUT (see workflow detect job).
Plugin list and paths come from .github/plugins.manifest.json.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import urllib.request
from pathlib import Path

from plugin_manifest import (
    entry_by_key,
    parse_plugin_keys_arg,
    read_version_at_git_ref,
    release_detect_entries,
    release_detect_keys,
    validate_release_detect_keys,
)

ROOT = Path(__file__).resolve().parents[2]


def git_rev_parse(ref: str) -> bool:
    r = subprocess.run(
        ["git", "rev-parse", ref],
        capture_output=True,
        cwd=ROOT,
    )
    return r.returncode == 0


def git_log_changelog(before: str, after: str) -> list[str]:
    if not (git_rev_parse(before) and git_rev_parse(after)):
        return []
    r = subprocess.run(
        ["git", "log", "--no-merges", "--pretty=format:- %s (%h)", f"{before}..{after}"],
        capture_output=True,
        text=True,
        cwd=ROOT,
    )
    lines = [ln for ln in r.stdout.splitlines() if ln.strip()]
    if lines:
        return lines
    r2 = subprocess.run(
        ["git", "log", "-1", "--pretty=%s", after],
        capture_output=True,
        text=True,
        cwd=ROOT,
    )
    subject = r2.stdout.strip() or "Update release contents"
    r3 = subprocess.run(
        ["git", "rev-parse", "--short", after],
        capture_output=True,
        text=True,
        cwd=ROOT,
    )
    short = r3.stdout.strip() or "HEAD"
    return [f"- {subject} ({short})"]


def http_json(url: str, token: str) -> object:
    req = urllib.request.Request(url)
    req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.loads(resp.read().decode())


def release_count(repo: str, token: str) -> int:
    url = f"https://api.github.com/repos/{repo}/releases?per_page=1"
    data = http_json(url, token)
    if isinstance(data, list):
        return len(data)
    return 1


def fetch_releases_page(repo: str, token: str, page: int) -> list[dict]:
    url = f"https://api.github.com/repos/{repo}/releases?per_page=100&page={page}"
    data = http_json(url, token)
    if isinstance(data, list):
        return data
    return []


def tag_suffix_for_stem(tag: str, stem: str, versions: str) -> int | None:
    expected = f"{stem}-{versions}"
    if tag == expected:
        return 1
    prefix = f"{stem}-"
    if not tag.startswith(prefix):
        return None
    rest = tag[len(prefix) :]
    m = re.match(r"^(\d+)-", rest)
    if not m:
        return None
    inc = int(m.group(1))
    ver_part = rest[len(m.group(0)) :]
    if ver_part == versions:
        return inc
    return None


def write_github_output(name: str, value: str) -> None:
    path = os.environ.get("GITHUB_OUTPUT")
    if not path:
        raise RuntimeError("GITHUB_OUTPUT is not set")
    with open(path, "a", encoding="utf-8") as f:
        f.write(f"{name}={value}\n")


def write_github_output_multiline(name: str, body: str) -> None:
    path = os.environ.get("GITHUB_OUTPUT")
    if not path:
        raise RuntimeError("GITHUB_OUTPUT is not set")
    with open(path, "a", encoding="utf-8") as f:
        f.write(f"{name}<<NOTEEOF\n{body}NOTEEOF\n")


def main() -> None:
    if len(sys.argv) < 3:
        print(
            "Usage: detect_plugin_versions.py BEFORE AFTER [force] [plugins_csv]",
            file=sys.stderr,
        )
        sys.exit(2)

    before = sys.argv[1]
    after = sys.argv[2]
    force = len(sys.argv) > 3 and sys.argv[3].lower() == "true"
    selected_raw = sys.argv[4] if len(sys.argv) > 4 else ""

    token = os.environ.get("GITHUB_TOKEN", "")
    repo = os.environ.get("GITHUB_REPOSITORY", "")
    entries = {e.key: e for e in release_detect_entries()}
    all_keys = release_detect_keys()

    changed: list[str] = []
    notes: list[str] = []
    versions_changed = "false"

    for key in all_keys:
        entry = entries[key]
        old_v = read_version_at_git_ref(before, entry)
        new_v = read_version_at_git_ref(after, entry)
        if not new_v:
            print(f"::warning::Could not read PluginVersion from {entry.version_file} at {after}")
            continue
        if old_v != new_v:
            versions_changed = "true"
            changed.append(key)
            notes.append(f"- **{key}**: `{old_v or '∅'}` → `{new_v}`")

    changelog = git_log_changelog(before, after)

    initial_release = "false"
    if not changed and repo and token:
        try:
            if release_count(repo, token) == 0:
                initial_release = "true"
                print(
                    "No GitHub releases exist on this repository yet; will publish an initial release."
                )
        except Exception as exc:
            print(f"::warning::Could not query releases: {exc}")

    should_release = force or bool(changed) or initial_release == "true"

    if selected_raw.strip():
        keys = validate_release_detect_keys(parse_plugin_keys_arg(selected_raw))
        if not keys:
            print("::error::Manual rerelease requested but no valid plugin keys were provided.")
            sys.exit(1)
        changed = keys
        notes = []
        for key in changed:
            v = read_version_at_git_ref(after, entries[key]) or "unknown"
            notes.append(f"- **{key}**: `{v}` (manual rerelease)")
        notes.append("- **trigger**: manual rerelease via workflow_dispatch")
        should_release = True
    elif force and not changed:
        changed = list(all_keys)
        notes.append("- **manual force**: including all module DLLs")
    elif initial_release == "true" and not changed:
        changed = list(all_keys)
        notes.append(
            "- **initial GitHub release**: repository had no prior releases; publishing all module DLLs"
        )

    write_github_output("versions_changed", versions_changed)
    write_github_output("should_release", "true" if should_release else "false")
    write_github_output("initial_release", initial_release)

    if not should_release:
        print(f"No PluginVersion changes between {before} and {after}; release not needed.")
        return

    assets: list[str] = []
    for key in changed:
        path = entries[key].release_artifact_path
        if path is None:
            print(f"::error::No releaseArtifactPath for plugin {key}", file=sys.stderr)
            sys.exit(1)
        assets.append(path.relative_to(ROOT).as_posix())

    unique: list[str] = []
    seen: set[str] = set()
    for p in assets:
        if p not in seen:
            seen.add(p)
            unique.append(p)

    list_file = Path(os.environ.get("GITHUB_WORKSPACE", ROOT)) / ".release-asset-paths.txt"
    list_file.write_text("\n".join(unique) + ("\n" if unique else ""), encoding="utf-8")
    write_github_output("asset_list_file", str(list_file))
    write_github_output("asset_paths", ",".join(unique))

    artifact_lines = "\n".join(f"- `{Path(p).name}`" for p in unique)
    notes_body = "\n".join(notes)
    changelog_body = "\n".join(changelog)
    notes_block = (
        f"### Version changes\n{notes_body}\n\n"
        f"### Artifacts\n{artifact_lines}\n\n"
        f"### Changelog\n{changelog_body}\n"
    )
    write_github_output_multiline("notes", notes_block)

    release_keys = sorted(set(changed)) if changed else sorted(all_keys)
    r = subprocess.run(
        ["git", "log", "-1", "--format=%cs", after],
        capture_output=True,
        text=True,
        cwd=ROOT,
    )
    release_date = r.stdout.strip() or subprocess.check_output(
        ["date", "-u", "+%Y-%m-%d"], text=True
    ).strip()
    plugin_slug = "+".join(release_keys)
    release_versions = [
        read_version_at_git_ref(after, entries[k]) or "unknown" for k in release_keys
    ]
    version_slug = "+".join(release_versions)

    existing_tag = ""
    existing_name = ""
    if repo and token:
        page = 1
        while page <= 20:
            rel_page = fetch_releases_page(repo, token, page)
            if not rel_page:
                break
            for rel in rel_page:
                if rel.get("target_commitish") == after:
                    existing_tag = str(rel.get("tag_name") or "")
                    existing_name = str(rel.get("name") or "")
                    break
            if existing_tag:
                break
            if len(rel_page) < 100:
                break
            page += 1

    if existing_tag:
        release_tag = existing_tag
        release_name = (
            existing_name
            if existing_name
            else f"release-r{release_date}-{plugin_slug} {version_slug}"
        )
    else:
        base_stem = f"release-r{release_date}-{plugin_slug}"
        max_inc = 0
        page = 1
        while page <= 20:
            rel_page = fetch_releases_page(repo, token, page)
            if not rel_page:
                break
            for rel in rel_page:
                tag = str(rel.get("tag_name") or "")
                inc = tag_suffix_for_stem(tag, base_stem, version_slug)
                if inc is not None and inc > max_inc:
                    max_inc = inc
            if len(rel_page) < 100:
                break
            page += 1

        inc_stem = base_stem if max_inc == 0 else f"{base_stem}-{max_inc + 1}"
        release_name = f"{inc_stem} {version_slug}"
        release_tag = f"{inc_stem}-{version_slug}"

    write_github_output("release_tag", release_tag)
    write_github_output("release_name", release_name)
    print(f"Release tag: {release_tag}")
    print(f"Release name: {release_name}")


if __name__ == "__main__":
    main()

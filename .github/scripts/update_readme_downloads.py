#!/usr/bin/env python3
"""
Resolve newest GitHub Release asset URL per DLL (grouped releases may omit unchanged modules),
then ensure README.md has one \"Download …\" markdown link per module/all-in-one section.
"""
from __future__ import annotations

import json
import os
import re
import sys
import urllib.error
import urllib.request


SECTIONS: list[tuple[str, str, str]] = [
    (
        "### HS2 Sandbox — CopyScript (`HS2Sandbox.CopyScript.dll`)",
        "HS2Sandbox.CopyScript.dll",
        "CopyScript",
    ),
    (
        "### HS2 Sandbox — Timeline (`HS2Sandbox.Timeline.dll`)",
        "HS2Sandbox.Timeline.dll",
        "Timeline",
    ),
    (
        "### HS2 Sandbox — SearchBarManager (`HS2Sandbox.SearchBarManager.dll`)",
        "HS2Sandbox.SearchBarManager.dll",
        "SearchBarManager",
    ),
    (
        "### HS2 Sandbox — Son scale (`HS2Sandbox.SonScale.dll`)",
        "HS2Sandbox.SonScale.dll",
        "Son scale",
    ),
    (
        "### HS2 Sandbox — Workspace tree lock (`HS2Sandbox.WorkspaceTreeLock.dll`)",
        "HS2Sandbox.WorkspaceTreeLock.dll",
        "Workspace tree lock",
    ),
    (
        "### HS2 Sandbox — Notebook (`HS2Sandbox.Notebook.dll`)",
        "HS2Sandbox.Notebook.dll",
        "Notebook",
    ),
    (
        "### HS2 Sandbox — Pose Browser (`HS2Sandbox.PoseBrowser.dll`)",
        "HS2Sandbox.PoseBrowser.dll",
        "Pose Browser",
    ),
    ("## All-in-one build", "HS2SandboxPlugin.dll", "All-in-one"),
]


def _http_json(url: str, token: str) -> object:
    req = urllib.request.Request(url)
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.loads(resp.read().decode())


def fetch_latest_urls_per_dll(repo: str, token: str) -> dict[str, str]:
    """Newest release first per GitHub API; record first sighting of each DLL basename."""
    want = {dll for _, dll, _ in SECTIONS}
    found: dict[str, str] = {}
    page = 1
    while page <= 20 and len(found) < len(want):
        url = f"https://api.github.com/repos/{repo}/releases?per_page=100&page={page}"
        try:
            data = _http_json(url, token)
        except urllib.error.HTTPError as e:
            print(f"::error::GitHub API failed ({e.code}) while listing releases", file=sys.stderr)
            raise
        if not isinstance(data, list) or len(data) == 0:
            break
        for rel in data:
            assets = rel.get("assets") or []
            for a in assets:
                name = a.get("name")
                dl = a.get("browser_download_url")
                if not name or not dl or name not in want:
                    continue
                if name not in found:
                    found[name] = dl
        if len(found) >= len(want):
            break
        page += 1
    return found


def patch_readme(text: str, dll_urls: dict[str, str]) -> tuple[str, bool]:
    out = text
    changed = False
    for heading_line, dll_name, label in SECTIONS:
        url = dll_urls.get(dll_name)
        if not url:
            print(f"::warning::No release asset yet for {dll_name}; skipping README link for \"{label}\".")
            continue
        escaped_label = re.escape(label)
        link_md = f"[Download {label}]({url})"
        block_re = re.compile(
            rf"({re.escape(heading_line)}\n)"
            rf"(?:\[Download {escaped_label}\]\([^)]*\)\n)?"
            rf"(?:\n)?",
            re.MULTILINE,
        )

        def repl(m: re.Match[str]) -> str:
            return f"{m.group(1)}{link_md}\n\n"

        new_out, n = block_re.subn(repl, out, count=1)
        if n != 1:
            print(f"::error::Could not find heading block for {heading_line!r}", file=sys.stderr)
            sys.exit(1)
        if new_out != out:
            changed = True
        out = new_out
    return out, changed


def main() -> None:
    repo = os.environ.get("GITHUB_REPOSITORY")
    if not repo:
        print("::error::GITHUB_REPOSITORY is not set", file=sys.stderr)
        sys.exit(1)
    token = os.environ.get("GITHUB_TOKEN", "")
    readme_path = os.environ.get("README_PATH", "README.md")

    dll_urls = fetch_latest_urls_per_dll(repo, token)
    with open(readme_path, encoding="utf-8") as f:
        content = f.read()

    new_content, changed = patch_readme(content, dll_urls)
    if not changed:
        print("README download links already up to date.")
        return

    with open(readme_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_content)
    print("Updated README download links.")


if __name__ == "__main__":
    main()

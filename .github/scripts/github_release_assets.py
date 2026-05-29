"""Resolve newest GitHub Release asset URL per DLL basename (shared by CI scripts)."""
from __future__ import annotations

import json
import sys
import urllib.error
import urllib.request

# (versions.json version key suffix -> DLL basename on release assets)
RELEASE_DLLS: dict[str, str] = {
    "allInOne": "HS2SandboxPlugin.dll",
    "copyScript": "HS2Sandbox.CopyScript.dll",
    "timeline": "HS2Sandbox.Timeline.dll",
    "searchBarManager": "HS2Sandbox.SearchBarManager.dll",
    "sonScale": "HS2Sandbox.SonScale.dll",
    "workspaceTreeLock": "HS2Sandbox.WorkspaceTreeLock.dll",
    "notebook": "HS2Sandbox.Notebook.dll",
    "poseBrowser": "HS2Sandbox.PoseBrowser.dll",
    "poseBrowserKks": "KKSSandbox.PoseBrowser.dll",
}


def http_json(url: str, token: str) -> object:
    req = urllib.request.Request(url)
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.loads(resp.read().decode())


def release_timestamp(rel: dict) -> str:
    return str(rel.get("published_at") or rel.get("created_at") or "")


def fetch_latest_urls_per_dll(repo: str, token: str) -> dict[str, str]:
    """Newest release (by published_at) that contains each DLL -> browser_download_url."""
    want = set(RELEASE_DLLS.values())
    releases: list[dict] = []
    page = 1
    while page <= 20:
        url = f"https://api.github.com/repos/{repo}/releases?per_page=100&page={page}"
        try:
            data = http_json(url, token)
        except urllib.error.HTTPError as e:
            print(f"::error::GitHub API failed ({e.code}) while listing releases", file=sys.stderr)
            raise
        if not isinstance(data, list) or not data:
            break
        releases.extend(data)
        if len(data) < 100:
            break
        page += 1

    releases.sort(key=release_timestamp, reverse=True)

    found: dict[str, str] = {}
    for rel in releases:
        for asset in rel.get("assets") or []:
            name = asset.get("name")
            dl = asset.get("browser_download_url")
            if not name or not dl or name not in want:
                continue
            if name not in found:
                found[name] = dl
        if len(found) >= len(want):
            break
    return found

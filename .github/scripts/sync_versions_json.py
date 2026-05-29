#!/usr/bin/env python3
"""Write versions.json from PluginVersion constants + GitHub release download URLs.

Run only after a release is published (see update-readme job in main.yml) so version
strings and *Download URLs stay aligned. Missing assets get an empty download URL.

Plugin keys and paths come from .github/plugins.manifest.json.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

from github_release_assets import fetch_latest_urls_per_dll
from plugin_manifest import read_plugin_version, versions_json_entries

ROOT = Path(__file__).resolve().parents[2]


def main() -> None:
    repo = os.environ.get("GITHUB_REPOSITORY", "SuitIThub/HS2-Sandbox")
    token = os.environ.get("GITHUB_TOKEN", "")

    dll_urls = fetch_latest_urls_per_dll(repo, token)

    doc: dict[str, object] = {"schemaVersion": 2}
    for entry in versions_json_entries():
        key = entry.versions_json_key
        assert key is not None
        doc[key] = read_plugin_version((entry.version_file, entry.version_match_index))
        dll = entry.release_dll_file_name
        download_key = f"{key}Download"
        if dll and dll in dll_urls:
            doc[download_key] = dll_urls[dll]
        else:
            doc[download_key] = ""
            if dll:
                print(
                    f"::warning::No release asset yet for {dll}; {download_key} left empty.",
                    file=sys.stderr,
                )

    out_path = ROOT / "versions.json"
    out_path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {out_path}")


if __name__ == "__main__":
    main()

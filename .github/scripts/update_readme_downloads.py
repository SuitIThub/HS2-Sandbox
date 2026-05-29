#!/usr/bin/env python3
"""
Resolve newest GitHub Release asset URL per DLL (grouped releases may omit unchanged modules),
then ensure README.md has one \"Download …\" markdown link per module/all-in-one section.

Section list comes from .github/plugins.manifest.json.
"""
from __future__ import annotations

import os
import re
import sys

from github_release_assets import fetch_latest_urls_per_dll
from plugin_manifest import readme_entries


def patch_readme(text: str, dll_urls: dict[str, str]) -> tuple[str, bool]:
    out = text
    changed = False
    for entry in readme_entries():
        dll_name = entry.release_dll_file_name
        label = entry.readme_download_label
        heading_line = entry.readme_heading
        assert dll_name and label and heading_line

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

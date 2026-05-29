#!/usr/bin/env python3
"""Validate manual rerelease plugin keys and write GITHUB_OUTPUT plugins=csv."""
from __future__ import annotations

import os
import sys

from plugin_manifest import parse_plugin_keys_arg, validate_release_detect_keys


def main() -> None:
    raw = sys.argv[1] if len(sys.argv) > 1 else ""
    keys = validate_release_detect_keys(parse_plugin_keys_arg(raw))
    if not keys:
        print(
            "::error::Manual rerelease enabled but no plugin keys were provided. "
            "Set rerelease_plugins to a comma-separated list (see .github/plugins.manifest.json). "
            "Run: python3 .github/scripts/plugin_manifest.py --list-keys",
            file=sys.stderr,
        )
        sys.exit(1)

    plugins_csv = ",".join(keys)
    print(f"Selected plugins: {plugins_csv}")
    out = os.environ.get("GITHUB_OUTPUT")
    if not out:
        print("::error::GITHUB_OUTPUT is not set", file=sys.stderr)
        sys.exit(1)
    with open(out, "a", encoding="utf-8") as f:
        f.write(f"plugins={plugins_csv}\n")


if __name__ == "__main__":
    try:
        main()
    except ValueError as exc:
        print(f"::error::{exc}", file=sys.stderr)
        sys.exit(1)

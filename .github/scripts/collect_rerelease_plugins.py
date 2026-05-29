#!/usr/bin/env python3
"""Collect selected rerelease_* checkboxes from workflow_dispatch inputs."""
from __future__ import annotations

import json
import os
import sys

from plugin_manifest import release_detect_entries, workflow_input_id


def _is_checked(value: object) -> bool:
    if isinstance(value, bool):
        return value
    return str(value).lower() == "true"


def collect_from_event(event: dict) -> list[str]:
    inputs = event.get("inputs") or {}
    selected: list[str] = []
    for entry in release_detect_entries():
        if _is_checked(inputs.get(workflow_input_id(entry.key))):
            selected.append(entry.key)
    return selected


def main() -> None:
    raw = os.environ.get("GITHUB_EVENT_JSON", "")
    if not raw:
        print("::error::GITHUB_EVENT_JSON is not set", file=sys.stderr)
        sys.exit(1)
    event = json.loads(raw)
    selected = collect_from_event(event)
    if not selected:
        print(
            "::error::Manual rerelease: check one or more plugin boxes "
            '(run_mode must be "manual_rerelease").',
            file=sys.stderr,
        )
        sys.exit(1)

    plugins_csv = ",".join(selected)
    print(f"Selected plugins: {plugins_csv}")
    out = os.environ.get("GITHUB_OUTPUT")
    if not out:
        print("::error::GITHUB_OUTPUT is not set", file=sys.stderr)
        sys.exit(1)
    with open(out, "a", encoding="utf-8") as f:
        f.write(f"plugins={plugins_csv}\n")


if __name__ == "__main__":
    main()

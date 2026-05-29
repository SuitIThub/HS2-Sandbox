#!/usr/bin/env python3
"""Regenerate workflow_dispatch plugin checkboxes in main.yml from plugins.manifest.json.

Run after adding a plugin with releaseDetect: true:
  python .github/scripts/sync_workflow_rerelease_inputs.py
"""
from __future__ import annotations

import sys
from pathlib import Path

from plugin_manifest import release_detect_entries

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = ROOT / ".github" / "workflows" / "main.yml"
START = "# >> rerelease-inputs:start (generated — run sync_workflow_rerelease_inputs.py)"
END = "# >> rerelease-inputs:end"


def input_id(plugin_key: str) -> str:
    return f"rerelease_{plugin_key}"


def generate_block() -> str:
    lines = [START]
    for entry in release_detect_entries():
        iid = input_id(entry.key)
        lines.append(f"      {iid}:")
        lines.append(f'        description: "Rerelease — {entry.display_name}"')
        lines.append("        required: false")
        lines.append("        default: false")
        lines.append("        type: boolean")
    lines.append(END)
    return "\n".join(lines) + "\n"


def sync_workflow() -> bool:
    text = WORKFLOW_PATH.read_text(encoding="utf-8")
    if START not in text or END not in text:
        print(
            f"::error::{WORKFLOW_PATH} is missing rerelease-input markers.\n"
            f"Expected:\n  {START}\n  ...\n  {END}",
            file=sys.stderr,
        )
        sys.exit(1)

    before, rest = text.split(START, 1)
    _old, after = rest.split(END, 1)
    new_text = before + generate_block() + after
    changed = new_text != text
    if changed:
        WORKFLOW_PATH.write_text(new_text, encoding="utf-8", newline="\n")
        print(f"Updated {WORKFLOW_PATH}")
    else:
        print(f"{WORKFLOW_PATH} rerelease inputs already match manifest.")
    return changed


def main() -> None:
    sync_workflow()


if __name__ == "__main__":
    main()

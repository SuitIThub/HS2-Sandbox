# HS2 Sandbox Pose Browser — exchange ZIP format (v2)

This document describes the **`.zip` files** produced and consumed by **Pose Browser** pose import/export (`PosePackExchange` in the shared `PoseBrowser/` sources). It is meant for **other modders, tool authors, or power users** who want to build compatible packs without using the in-game UI.

## File extension and discovery

- All current exports use the **`.zip`** extension (flat multi-select export and folder/branch export).
- The importer opens a path and expects a normal ZIP archive with a **`manifest.json`** entry at the archive root (see below).

## ZIP technical requirements (important)

Pose Browser reads ZIPs through **`MinimalStoredZip`**, which is intentionally small and **does not use `System.IO.Compression`**. That imposes strict rules:

| Requirement | Reason |
|-------------|--------|
| **Compression method must be `0` (stored)** | Deflate and other methods are **rejected** on read. |
| **CRC-32 and sizes** must match the spec | Same as any ZIP reader; corrupted archives fail. |
| **Entry names** are treated as **UTF-8** when the ZIP general-purpose bit **11** (UTF-8 / EFS) is set on headers — the game’s writer **always** sets this flag. | Use UTF-8 file names when building packs manually. |

**Practical implication:** If you build a ZIP with a tool that defaults to **Deflate**, Pose Browser will **not** import it. Use a ZIP tool that can create **“store only” / uncompressed** entries, or generate the archive with the same constraints as `MinimalStoredZip.Write`.

Examples (conceptual — check your tool’s docs):

- **7-Zip**: prefer a mode that stores without compression for all members.
- **Info-ZIP**: `zip -0` stores without compression.
- **Programmatic .NET**: either duplicate the stored-only layout used in-repo or use a library that can force **stored** entries.

## Top-level layout (v2)

A valid v2 pack contains at least:

| Entry path | Required | Description |
|------------|----------|-------------|
| `manifest.json` | Yes | Declares schema version and **import mode** (`poses` vs `treeBranch`). |
| `metadata.json` | Yes* | Per-file tags, favorite flag, timestamps, and ZIP-internal paths. |
| `poses/…` | Yes | One entry per pose file, under the `poses/` prefix. |

\*The manifest field **`metadata`** names this file; it defaults to `metadata.json`. Avoid path traversal (`..`); use a simple name like `metadata.json`.

All pose **binaries** live under **`poses/`**. Paths use **forward slashes** (`/`). The importer normalizes and matches entries in a case-insensitive dictionary, but you should still use consistent casing.

### `manifest.json` (v2)

UTF-8 JSON object. Parsed with Unity **`JsonUtility`** in-game, so keep it as a **flat** object (no deeply nested custom types). Fields:

| Field | Type | Meaning |
|-------|------|---------|
| `schema` | string | Must be exactly **`HS2Sandbox.poseZip`**. |
| `version` | number | Must be **`2`**. Future versions may change semantics. |
| `kind` | string | **`poses`** (flat pack) or **`treeBranch`** (hierarchical pack). |
| `exportedUtc` | string | ISO-8601 UTC timestamp of export (informative). |
| `branchRoot` | string | **Tree packs only:** single folder name segment (sanitized) matching the first directory under `poses/`. **Empty** for `kind: "poses"`. |
| `metadata` | string | ZIP entry path for the metadata file; default **`metadata.json`**. |

**Example — flat pack:**

```json
{
  "schema": "HS2Sandbox.poseZip",
  "version": 2,
  "kind": "poses",
  "exportedUtc": "2026-05-17T12:00:00.0000000Z",
  "branchRoot": "",
  "metadata": "metadata.json"
}
```

**Example — tree / branch pack:**

```json
{
  "schema": "HS2Sandbox.poseZip",
  "version": 2,
  "kind": "treeBranch",
  "exportedUtc": "2026-05-17T12:00:00.0000000Z",
  "branchRoot": "MyPack",
  "metadata": "metadata.json"
}
```

For `treeBranch`, every file entry under `poses/` must start with **`poses/<branchRoot>/`** where `<branchRoot>` equals `branchRoot` in the manifest (see layout below).

### `metadata.json`

UTF-8 JSON object with an **`items`** array. Each element describes one pose file **inside the ZIP**.

**Note:** Export writes this file with an explicit serializer (not `JsonUtility`) because Unity’s `JsonUtility` mishandles arrays of nested types. Import uses a **small custom JSON parser** that accepts standard JSON (quoted keys/strings, `true`/`false`, optional `\u` escapes, etc.) and allows **any property order** inside each item object.

Shape:

```json
{
  "items": [
    {
      "file": "poses/example.png",
      "tags": ["tag1", "tag2"],
      "favorite": false,
      "lastWriteTimeUtc": "2026-05-17T10:00:00.0000000Z",
      "creationTimeUtc": "2026-05-17T09:00:00.0000000Z"
    }
  ]
}
```

| Field | Type | Meaning |
|-------|------|---------|
| `file` | string | Path of the ZIP entry (slashes `/`), must be under `poses/` and pass safety checks (see below). |
| `tags` | string[] | Tag strings applied when importing into Pose Browser’s tag database. |
| `favorite` | boolean | Favorite flag (`true` / `false`; importer also accepts loose `0` / `1`). |
| `lastWriteTimeUtc` | string | ISO-8601 UTC; used when populating item metadata. |
| `creationTimeUtc` | string | ISO-8601 UTC from the source file’s creation time. |

Every `file` listed in `items` **must** exist as a **stored** ZIP entry with **exactly** that path (after trim/normalization). The importer loads bytes from that entry.

## Pose files under `poses/`

- Studio pose assets are the usual **`.png`** (embedded pose + thumbnail) or **`.dat`** pose files used under `UserData/studio/pose`.
- Files are stored **as-is** (binary identical to what would sit on disk).

### Flat pack (`kind: "poses"`)

- Layout: **`poses/<filename>`** only — **no subdirectories** in the current exporter.
- If multiple poses would share the same name, the exporter adds numeric suffixes: `name-01.png`, `name-02.png`, … (similar spirit to collision avoidance on disk).
- `branchRoot` in `manifest.json` must be empty.

### Tree / branch pack (`kind: "treeBranch"`)

- Layout: **`poses/<branchRoot>/<relative path>/<file>`**.
- `<branchRoot>` is a **single path segment** (no slashes) and matches `manifest.branchRoot`.
- `<relative path>` mirrors the folder structure that lived under the exported tree node (forward slashes). Example:

  - `poses/MyPack/subfolder/pose1.png`

On **import**, Pose Browser creates a folder named from the pack’s `branchRoot` under the user’s chosen parent directory, then recreates relative subfolders and writes files accordingly.

## Path safety rules (v2)

The importer rejects unsafe paths. When authoring packs, ensure:

- No `..` segments, no empty segments, no `.` as a full segment.
- No leading `/` on stored entry names (use `poses/...` not `/poses/...`).
- No `//` in paths.
- File name segments must not contain characters invalid on Windows file names (`Path.GetInvalidFileNameChars()`).
- **`file`** in metadata must start with **`poses/`** (case-insensitive check against the internal prefix).

Violating these will fail import with an explicit error string.

## How to recreate a pack by hand

1. Collect your `.png` / `.dat` pose files.
2. Choose **`kind`**:
   - **Flat:** place files as `poses/YourFile.png`, ensuring unique leaf names.
   - **Tree:** pick `<branchRoot>` (e.g. `MyPack`), place files under `poses/MyPack/...`.
3. Write **`manifest.json`** with `schema`, `version: 2`, `kind`, timestamps, `branchRoot`, and `metadata`.
4. Write **`metadata.json`** with one `items[]` entry per file: matching `file` path, `tags`, `favorite`, timestamps (ISO UTC strings are fine).
5. Build a **ZIP** with **all entries stored (method 0)**, UTF-8 names / EFS bit as appropriate.
6. Name the archive **`.zip`** and import through Pose Browser.

You can also export a small pack from the game once and **replace** the contents of `poses/` and the `items` list while keeping the same JSON structure as a template.

## Legacy v1 packs (still imported)

Older Pose Browser builds used a different manifest (`HS2Sandbox.PosePack` / `HS2Sandbox.PoseTreePack`) and opaque blobs under **`files/0000`**, **`files/0001`**, …. The current importer still **reads** those for backward compatibility, but **new** exports are always v2 **`.zip`** as described here.

For new tooling, **target v2 only**.

## Reference implementation in this repository

| File | Role |
|------|------|
| [`PoseBrowser/PosePackExchange.cs`](../../PoseBrowser/PosePackExchange.cs) | Manifest constants, export, import, v2 + legacy routing, metadata JSON serialization/parsing. |
| [`PoseBrowser/MinimalStoredZip.cs`](../../PoseBrowser/MinimalStoredZip.cs) | Stored-only ZIP reader/writer (no `System.IO.Compression`). |

When in doubt, match the bytes and JSON emitted by `TryExportPosePack` / `TryExportTreePack` and accepted by `TryReadPack`.

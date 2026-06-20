# Pose ZIP format

Specification for **`.zip`** files produced and consumed by **Pose Browser** import/export. For modders and tool authors building compatible packs without the in-game UI.

Authoritative source in repo: `docs/POSE_ZIP_FORMAT.md`

## Critical ZIP requirement

Pose Browser reads ZIPs via **`MinimalStoredZip`** (no `System.IO.Compression`):

| Requirement | Reason |
|-------------|--------|
| **Compression method 0 (stored)** | Deflate and other methods are **rejected** |
| Valid CRC-32 and sizes | Standard ZIP rules |
| UTF-8 entry names (GP bit 11) | Writer always sets EFS flag |

**If your tool defaults to Deflate, import will fail.** Use store-only mode:

- **7-Zip:** store without compression
- **Info-ZIP:** `zip -0`
- **.NET:** force stored entries or match `MinimalStoredZip.Write`

## Top-level layout

| Entry | Required | Description |
|-------|----------|-------------|
| `manifest.json` | Yes | Schema version and import mode |
| `metadata.json` | Yes* | Per-file tags, favorites, timestamps |
| `poses/…` | Yes | One entry per pose file |

## `manifest.json`

| Field | Meaning |
|-------|---------|
| `schema` | Must be **`HS2Sandbox.poseZip`** |
| `version` | Export: **7** (current). Import: **2–7** + legacy v1 |
| `kind` | **`poses`** (flat) or **`treeBranch`** (hierarchical) |
| `exportedUtc` | ISO-8601 UTC (informative) |
| `branchRoot` | Tree packs: single folder segment under `poses/`; empty for flat |
| `metadata` | Path to metadata file (default `metadata.json`) |

### Example — flat pack

```json
{
  "schema": "HS2Sandbox.poseZip",
  "version": 7,
  "kind": "poses",
  "exportedUtc": "2026-05-17T12:00:00.0000000Z",
  "branchRoot": "",
  "metadata": "metadata.json"
}
```

## `metadata.json`

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

Every `file` in `items` must exist as a stored ZIP entry.

## Optional `groups` array (v3+)

```json
{
  "groups": [
    {
      "id": "a1b2c3…",
      "name": "My sequence",
      "tags": ["combo"],
      "members": ["poses/pose1.png", "poses/pose2.png"],
      "memberRelativeOffsets": [[0,0,0], [1.2, 0, -0.5]],
      "memberBodyHeights": [0.5, 0.5],
      "memberRelativeRotations": [[0,0,0,1], [0,0,0,1]],
      "memberObjectScales": [[1,1,1], [1,1,1]]
    }
  ]
}
```

| Field | Version | Meaning |
|-------|---------|---------|
| `memberRelativeOffsets` | v4+ | Position offsets in anchor local frame |
| `memberBodyHeights` | v5+ | Maker body height per member |
| `memberRelativeRotations` | v6+ | Quaternion relative to anchor |
| `memberObjectScales` | v7+ | Studio object scale per member |

## Pose files under `poses/`

- Standard **`.png`** (embedded pose + thumbnail) or **`.dat`** files
- Stored binary-identical to on-disk library files

### Flat pack (`kind: "poses"`)

- Layout: **`poses/<filename>`** only (no subdirs in exporter)
- Name collisions → `name-01.png`, `name-02.png`, …

### Tree pack (`kind: "treeBranch"`)

- Layout: **`poses/<branchRoot>/<relative>/<file>`**
- Import creates `<branchRoot>` folder under chosen destination

## Path safety

Importer rejects:

- `..`, empty segments, leading `/`, `//`
- Invalid Windows filename characters
- Paths not starting with `poses/`

## Build a pack manually

1. Collect `.png` / `.dat` files
2. Choose flat or tree layout under `poses/`
3. Write `manifest.json` and `metadata.json`
4. ZIP with **all entries stored (method 0)**
5. Import via Pose Browser **Import…**

Tip: export a small pack from the game and use it as a JSON/template reference.

## Legacy v1

Older manifests (`HS2Sandbox.PosePack`, blobs under `files/0000`, …) still import. **New tooling should target version 7.**

## Reference code

| File | Role |
|------|------|
| `src/PoseBrowser/PosePackExchange.cs` | Export/import, metadata |
| `src/PoseBrowser/MinimalStoredZip.cs` | Stored-only ZIP I/O |

→ [Import/export ZIP](Pose-Browser-Import-Export-ZIP) · [Pose Browser](Pose-Browser)

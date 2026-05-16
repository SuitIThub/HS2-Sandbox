# HS2 Studio API Analysis

Decompiled from `Assembly-CSharp.dll` in `StudioNEOV2_Data/Managed/`.

## Architecture Overview

The Studio uses a **Singleton pattern** for most managers. The core entry point is `Singleton<Studio.Studio>.Instance`. All objects in the scene are tracked via two dictionaries:

- `dicInfo` — maps `TreeNodeObject` → `ObjectCtrlInfo`
- `dicObjectCtrl` — maps `int` (dicKey) → `ObjectCtrlInfo`

Every scene object (character, item, light, folder, camera, route) is an `ObjectCtrlInfo` subclass with an associated `TreeNodeObject` in the workspace tree and a `GuideObject` for 3D gizmo manipulation.

---

## 1. Pose System (`PauseCtrl`)

The class is `Studio.PauseCtrl` (Japanese naming — "pause" = "pose").

### Pose File Format (.dat)

Binary format with header `【pose】`, version `101`:

| Field | Type | Description |
|-------|------|-------------|
| Identifying code | string | `"【pose】"` |
| Version | int32 | `101` |
| Sex | int32 | 0=male, 1=female |
| Name | string | Pose name |
| FileInfo data | binary | See below |

### PauseCtrl.FileInfo — Pose Data Structure

```csharp
public class FileInfo {
    public int group = -1;       // Animation group
    public int category = -1;    // Animation category
    public int no = -1;          // Animation number
    public float normalizedTime; // Animation playback position

    public bool enableIK;
    public bool[] activeIK = { true, true, true, true, true }; // 5 IK groups
    public Dictionary<int, ChangeAmount> dicIK;                 // IK target positions

    public bool enableFK;
    public bool[] activeFK = { false, true, false, true, false, false, false }; // 7 FK groups
    public Dictionary<int, ChangeAmount> dicFK;                 // FK bone rotations

    public bool[] expression = { true, true, true, true };     // Expression categories
}
```

### Saving a Pose

```csharp
PauseCtrl.Save(OCIChar ociChar, string name);
// Saves to: UserData/studio/pose/{timestamp}.dat
```

### Loading a Pose

```csharp
bool success = PauseCtrl.Load(OCIChar ociChar, string filePath);
```

### Applying a Pose Programmatically

The `FileInfo.Apply(OCIChar)` method does:
1. `_char.LoadAnime(group, category, no, normalizedTime)` — sets the base animation
2. Sets IK targets: iterates `activeIK` and calls `_char.ActiveIK(boneGroup, active, false)`
3. Enables IK mode: `_char.ActiveKinematicMode(KinematicMode.IK, enableIK, true)`
4. Copies IK positions: `_char.oiCharInfo.ikTarget[key].changeAmount.Copy(value)`
5. Sets FK bones: iterates `activeFK` and calls `_char.ActiveFK(FKCtrl.parts[j], active, false)`
6. Enables FK mode: `_char.ActiveKinematicMode(KinematicMode.FK, enableFK, true)`
7. Copies FK rotations: `_char.oiCharInfo.bones[key].changeAmount.Copy(value)`
8. Sets expressions: `_char.EnableExpressionCategory(k, expression[k])`

---

## 2. Getting the Currently Selected Character

### Method A: From TreeNodeCtrl selection

```csharp
TreeNodeObject[] selectedNodes = Singleton<Studio.Studio>.Instance.treeNodeCtrl.selectNodes;
// or single:
TreeNodeObject selectedNode = Singleton<Studio.Studio>.Instance.treeNodeCtrl.selectNode;
```

### Method B: Get ObjectCtrlInfo from selection

```csharp
ObjectCtrlInfo[] selected = Studio.Studio.GetSelectObjectCtrl();
// or:
ObjectCtrlInfo info = Studio.Studio.GetCtrlInfo(treeNodeObject);
```

### Method C: Filter for characters

```csharp
// From the dicObjectCtrl dictionary:
foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl) {
    if (kvp.Value is OCIChar ociChar) {
        // ociChar is a character
    }
}

// Get selected character specifically:
var selected = Studio.Studio.GetSelectObjectCtrl();
if (selected != null && selected.Length > 0 && selected[0] is OCIChar ociChar) {
    // use ociChar
}
```

### Method D: From GuideObjectManager

```csharp
GuideObject selectedGuide = Singleton<GuideObjectManager>.Instance.selectObject;
// Then look up in dicInfo to find the owning ObjectCtrlInfo
```

---

## 3. Character Classes

### ObjectCtrlInfo (abstract base)

```csharp
public abstract class ObjectCtrlInfo {
    public ObjectInfo objectInfo;          // Serializable data
    public TreeNodeObject treeNodeObject;  // UI tree node
    public GuideObject guideObject;        // 3D gizmo
    public ObjectCtrlInfo parentInfo;      // Parent attachment

    public int kind;                       // 0=char, 1=item, 2=light, 3=folder, 4=route, 5=camera
    public virtual float animeSpeed { get; set; }
}
```

### OCIChar — Character Controller

Key members:

```csharp
public class OCIChar : ObjectCtrlInfo {
    public OICharInfo oiCharInfo;          // Serialized character state
    public ChaControl charInfo;            // Character control (mesh, bones, etc.)
    public ChaReference charReference;     // Bone references
    public FKCtrl fkCtrl;                  // FK controller component
    public IKCtrl ikCtrl;                  // IK controller
    public FullBodyBipedIK finalIK;        // FinalIK solver
    public CharAnimeCtrl charAnimeCtrl;    // Animation controller

    public List<BoneInfo> listBones;       // All FK bone info
    public List<IKInfo> listIKTarget;      // All IK targets
    public LookAtInfo lookAtInfo;          // Eye look-at target

    public int sex;                        // 0=male, 1=female

    // Key methods:
    void LoadAnime(int group, int category, int no, float normalizedTime = 0f);
    void ActiveKinematicMode(KinematicMode mode, bool active, bool force);
    void ActiveFK(BoneGroup group, bool active, bool force = false);
    void ActiveIK(BoneGroup group, bool active, bool force = false);
    void ChangeHandAnime(int type, int ptn);
    void ChangeLookEyesPtn(int ptn);
    void ChangeLookNeckPtn(int ptn);
    void ChangeEyesOpen(float value);
    void ChangeBlink(bool value);
    void ChangeMouthPtn(int ptn);
    void ChangeMouthOpen(float value);
    void EnableExpressionCategory(int category, bool value);
    void ChangeChara(string path);     // Replace character model
    void SetClothesState(int id, byte state);
    void LoadClothesFile(string path);
}
```

### OCICharFemale / OCICharMale

Subclasses of `OCIChar` with sex-specific overrides (e.g., `SetNipStand`, `GetSiruFlags`).

---

## 4. Bone / FK / IK System

### OIBoneInfo.BoneGroup (flags enum)

```csharp
public enum BoneGroup {
    Body = 1,
    RightLeg = 2,
    LeftLeg = 4,
    RightArm = 8,
    LeftArm = 16,
    RightHand = 32,
    LeftHand = 64,
    Hair = 128,
    Neck = 256,
    Breast = 512,
    Skirt = 1024
}
```

### FKCtrl.parts — The 7 FK Groups (in order)

```csharp
public static BoneGroup[] parts = {
    BoneGroup.Hair,      // [0]
    BoneGroup.Neck,      // [1]
    BoneGroup.Breast,    // [2]
    BoneGroup.Body,      // [3]
    BoneGroup.RightHand, // [4]
    BoneGroup.LeftHand,  // [5]
    BoneGroup.Skirt      // [6]
};
```

### ChangeAmount — Transform Data

```csharp
public class ChangeAmount {
    public Vector3 pos;   // Position (with change callback)
    public Vector3 rot;   // Euler rotation (with change callback)
    public Vector3 scale; // Scale (with change callback)

    public Action onChangePos;
    public Action onChangeRot;
    public Action<Vector3> onChangeScale;

    void Save(BinaryWriter writer);
    void Load(BinaryReader reader);
    ChangeAmount Clone();
    void Copy(ChangeAmount source, bool pos, bool rot, bool scale);
    void Reset();
    void OnChange(); // Triggers all callbacks
}
```

### OICharInfo.KinematicMode

```csharp
public enum KinematicMode { None, FK, IK }
```

### IK Targets (5 groups)

| Index | BoneGroup | Body Part |
|-------|-----------|-----------|
| 0 | Body (1) | Body/Hips |
| 1 | RightLeg (2) | Right leg |
| 2 | LeftLeg (4) | Left leg |
| 3 | RightArm (8) | Right arm |
| 4 | LeftArm (16) | Left arm |

---

## 5. Studio UI Framework

### Main Entry Points

```csharp
var studio = Singleton<Studio.Studio>.Instance;
studio.treeNodeCtrl       // TreeNodeCtrl — workspace tree
studio.rootButtonCtrl     // RootButtonCtrl — main toolbar (Add/Manipulate/Sound/System)
studio.manipulatePanelCtrl // ManipulatePanelCtrl — manipulation panel
studio.systemButtonCtrl   // SystemButtonCtrl — effects, save/load
studio.cameraCtrl         // CameraControl — camera
studio.gameScreenShot     // GameScreenShot — screenshot system
studio.colorPalette       // ColorPalette — color picker
studio.sceneInfo          // SceneInfo — current scene data
```

### RootButtonCtrl Structure

The main toolbar has 4 sections (index-based):
- 0: **Add** — Add characters, items, lights
- 1: **Manipulate** — Contains `ManipulatePanelCtrl` which shows per-type panels
- 2: **Sound** — BGM, ENV, voice
- 3: **System** — Post-processing effects, save/load/init/exit

```csharp
studio.rootButtonCtrl.OnClick(int kind); // Toggle a panel (-1 = close all)
studio.rootButtonCtrl.objectCtrlInfo = value; // Set active object for manipulation
```

### TreeNodeCtrl — Selection System

```csharp
studio.treeNodeCtrl.selectNode;           // Get/set single selection
studio.treeNodeCtrl.selectNodes;          // Get all selected nodes
studio.treeNodeCtrl.selectObjectCtrl;     // Get ObjectCtrlInfo[] for selected
studio.treeNodeCtrl.SelectSingle(node, deselect); // Select a single node
studio.treeNodeCtrl.AddNode(name, parent);         // Add tree node
studio.treeNodeCtrl.DeleteNode(node);              // Delete tree node
studio.treeNodeCtrl.SetParent(node, parent);       // Reparent node

// Event hooks:
studio.treeNodeCtrl.onSelect        // Action<TreeNodeObject> — single select
studio.treeNodeCtrl.onSelectMultiple // Action — multi-select
studio.treeNodeCtrl.onDeselect      // Action<TreeNodeObject> — deselect
studio.treeNodeCtrl.onDelete        // Action<TreeNodeObject> — node deleted
studio.treeNodeCtrl.onParentage     // Action<TreeNodeObject, TreeNodeObject> — reparent
```

### Adding Objects

```csharp
studio.AddFemale(charFilePath);           // Add female character
studio.AddMale(charFilePath);             // Add male character
studio.AddItem(group, category, no);      // Add item
studio.AddLight(type);                    // Add light
studio.AddFolder();                       // Add folder
studio.AddCamera();                       // Add camera
studio.AddRoute();                        // Add route
```

### Scene Management

```csharp
studio.SaveScene();
studio.LoadScene(path);
studio.ImportScene(path);
studio.InitScene(close);  // Clear scene
```

---

## 6. Screenshot / Capture System (`GameScreenShot`)

```csharp
var screenshot = Singleton<Studio.Studio>.Instance.gameScreenShot;

// Properties:
screenshot.modeARGB = true;   // Enable alpha channel
screenshot.capRate = 2;       // Resolution multiplier (1x, 2x, etc.)
screenshot.capMark = false;   // Watermark toggle

// Capture:
screenshot.Capture();                          // Save with auto filename
screenshot.Capture("path/to/file.png");       // Save to specific path

// Render to byte array:
byte[] pngData = screenshot.CreatePngScreen(
    width,
    height,
    argb: false,  // Alpha channel
    cap: false    // Show watermark
);

// Hooks:
screenshot.captureBeforeFunc = () => { /* before capture */ };
screenshot.captureAfterFunc = () => { /* after capture */ };
```

**CreatePngScreen** workflow:
1. Creates a `Texture2D` at the specified resolution
2. Creates a `RenderTexture` with anti-aliasing matching quality settings
3. Renders all cameras (except Studio/Camera layer) into the render texture
4. Reads pixels from the render texture into the texture
5. Encodes to PNG bytes
6. Cleans up temporary textures

---

## 7. GuideObject — 3D Gizmo System

```csharp
public class GuideObject : MonoBehaviour {
    public Transform transformTarget;     // The target transform being controlled
    public ChangeAmount changeAmount;     // Position/rotation/scale data
    public int dicKey;                    // Unique key

    public bool enablePos;               // Can move
    public bool enableRot;               // Can rotate
    public bool enableScale;             // Can scale
    public bool isActive;                // Currently selected/active

    public Mode mode;                    // Local, LocalIK, or World
    public Transform parent;             // Parent transform (for relative movement)
    public bool nonconnect;              // If true, doesn't parent the transform

    // Methods:
    void SetMode(int mode, bool layer);
    void MoveWorld(Vector3 delta);
    void MoveLocal(Vector3 delta, bool snap, MoveAxis axis);
    void Rotation(Vector3 axis, float angle);
    void SetScale();

    public enum Mode { Local, LocalIK, World }
}
```

### GuideObjectManager

```csharp
var gom = Singleton<GuideObjectManager>.Instance;

gom.selectObject;              // Get/set selected guide object
gom.selectObjects;             // All selected guide objects
gom.mode;                      // 0=Move, 1=Rotate, 2=Scale
gom.operationTarget;           // Currently being dragged
gom.Add(transform, dicKey);    // Create new guide object
gom.Delete(guideObject);       // Remove guide object
gom.SetScale();                // Update all gizmo scales
```

---

## 8. Key Patterns for Plugin Development

### Getting the selected character and applying a pose:

```csharp
var selected = Studio.Studio.GetSelectObjectCtrl();
if (selected != null && selected.Length > 0 && selected[0] is OCIChar ociChar)
{
    // Load from file
    PauseCtrl.Load(ociChar, posePath);

    // Or build programmatically
    var fileInfo = new PauseCtrl.FileInfo(ociChar); // Capture current
    // Modify fileInfo...
    fileInfo.Apply(ociChar); // Apply modified
}
```

### Iterating all characters in the scene:

```csharp
foreach (var kvp in Singleton<Studio.Studio>.Instance.dicObjectCtrl)
{
    if (kvp.Value is OCIChar ociChar)
    {
        // Process character
    }
}
```

### Hooking into selection changes:

```csharp
Singleton<Studio.Studio>.Instance.treeNodeCtrl.onSelect += (TreeNodeObject node) =>
{
    var info = Studio.Studio.GetCtrlInfo(node);
    if (info is OCIChar ociChar)
    {
        // Character was selected
    }
};
```

### Taking a screenshot:

```csharp
var screenshot = Singleton<Studio.Studio>.Instance.gameScreenShot;
byte[] png = screenshot.CreatePngScreen(1920, 1080, argb: true, cap: false);
File.WriteAllBytes("screenshot.png", png);
```

---

## 9. Important Singletons

| Singleton | Purpose |
|-----------|---------|
| `Singleton<Studio.Studio>` | Main studio controller |
| `Singleton<GuideObjectManager>` | Guide/gizmo management |
| `Singleton<Info>` | Game data tables (animations, bones, items) |
| `Singleton<Map>` | Map/environment management |
| `Singleton<UndoRedoManager>` | Undo/redo system |
| `Singleton<Character>` | Character management (ChaControl) |

---

## 10. File Paths

| Content | Path Pattern |
|---------|-------------|
| Poses | `UserData/studio/pose/*.dat` |
| Scenes | `UserData/studio/scene/*.png` |
| Screenshots | `UserData/cap/*.png` |
| Options | `UserData/studio/option.xml` |

---

## 11. Object Kind Values

| Kind | Type | Class |
|------|------|-------|
| 0 | Character | OCIChar (OCICharFemale, OCICharMale) |
| 1 | Item | OCIItem |
| 2 | Light | OCILight |
| 3 | Folder | OCIFolder |
| 4 | Route | OCIRoute |
| 5 | Camera | OCICamera |

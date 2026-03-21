# Create Timeline Command — Reference

## Full Class Architecture

```
TimelineCommand  (abstract, Timeline/TimelineCommand.cs)
└── every command directly — no intermediate bases

SubWindow  (abstract, SubWindows/SubWindow.cs)
└── ActionTimeline  (SubWindows/ActionTimeline.cs)
     owns: List<TimelineCommand> _commands
           TimelineContext _runContext
           TimelineVariableStore _persistentVariables
           TimelineVariableStore _designTimeVariables
```

`TimelineContext` (Timeline/TimelineContext.cs) is the execution state bag created fresh per run:

| Member | Purpose |
|--------|---------|
| `Variables` | Live variable store for this run |
| `Runner` | `MonoBehaviour` — use `ctx.Runner.StartCoroutine(...)` |
| `CheckpointIndices` | `Dictionary<string, int>` — checkpoint name → command index |
| `NextIndex` | Set by Jump/Loop to override linear progression |
| `LastScreenshotName` | Filled from CopyScript API; updated by WaitForScreenshot |
| `LoopCounts` | Per-checkpoint loop iteration counters |
| `PendingConfirmCallback` | Set to pause until user clicks "Confirm" |
| `PendingResolveCallback` | Set to pause with "Resolve"/"Retry" buttons |
| `ApiClient` | `CopyScriptApiClient?` for HTTP calls to the CopyScript server |
| `SetJumpTarget(name)` | Resolves checkpoint name to index, sets `NextIndex` |

---

## Variable Store API (TimelineVariableStore)

Access via `ctx.Variables` during Execute, or `store` in SimulateVariableEffects.

### String variables
```csharp
ctx.Variables.SetString(name, value);
string v = ctx.Variables.GetString(name);   // "" if absent
bool ok  = ctx.Variables.HasString(name);
```

### Integer variables
```csharp
ctx.Variables.SetInt(name, value);
int  v  = ctx.Variables.GetInt(name);       // 0 if absent
bool ok = ctx.Variables.HasInt(name);
```

### List variables
```csharp
ctx.Variables.SetList(name, listOfStrings);
List<string> v = ctx.Variables.GetList(name); // new copy; empty if absent
bool ok = ctx.Variables.HasList(name);
```

### Dictionary variables
```csharp
ctx.Variables.SetDictValue(name, key, value);   // creates dict if absent
ctx.Variables.SetDict(name, dictionary);         // replaces whole dict
string v = ctx.Variables.GetDictValue(name, key); // "" if absent
bool ok  = ctx.Variables.TryGetDictValue(name, key, out string v);
Dictionary<string,string> d = ctx.Variables.GetDict(name); // copy
bool ok = ctx.Variables.HasDict(name);
```

### Interpolation and validation
```csharp
// Replace [varName] with variable value (string or int.ToString())
string r = ctx.Variables.Interpolate("[myVar] and literal");

// True if all [varName] refs exist (for HasInvalidConfiguration)
bool ok = ctx.Variables.IsValidInterpolation(text);

// Resolve int field: accepts int literal, int var name, or [stringVar] that parses as int
bool ok = ctx.Variables.TryResolveIntOperand(text, out int value);
int  v  = ctx.Variables.ResolveIntOperand(text); // returns 0 on failure

// True if TryResolveIntOperand would succeed at design time
bool ok = ctx.Variables.IsValidIntOperand(text);
```

---

## Serialization Conventions

### Separators

| Character | Unicode | Used for |
|-----------|---------|---------|
| `\u0001` | Sep | Fields within a single command payload (most commands) |
| `\u0002` | — | Items within a list value in variable JSON |
| `\u0003` | — | Key↔value separator within a dict entry |

Commands only need `\u0001`. The `\u0002`/`\u0003` are only used by `ActionTimeline`'s JSON save/load.

### Common payload shapes

| Shape | Example command | `SerializePayload` |
|-------|-----------------|--------------------|
| Empty | `StopTrackingCommand` | `return "";` |
| Raw value | `ScrollCommand` | `return _up ? "1" : "0";` |
| Single string | `CheckpointCommand` | `return _name ?? "";` |
| 2–4 `\u0001`-fields | `OutfitByNameCommand` | `return name + Sep + isFile + Sep + reload;` |
| With list items | `SetListCommand` | values joined by `\u0002`, then `name + Sep + joinedValues` |

Always strip `Sep` from user-entered string fields before writing to avoid splitting on load:
```csharp
string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
```

### DeserializePayload pattern

```csharp
public override void DeserializePayload(string payload)
{
    // 1. Reset all fields to safe defaults
    _field1 = ""; _flag = true; _number = 0;
    // 2. Guard
    if (string.IsNullOrEmpty(payload)) return;
    // 3. Split and assign defensively
    string[] p = payload.Split(Sep);
    if (p.Length >= 1) _field1  = p[0];
    if (p.Length >= 2) _flag    = p[1] == "1";
    if (p.Length >= 3 && int.TryParse(p[2], out int v)) _number = v;
}
```

The `p.Length >= N` guards ensure older saved timelines without a field still load safely.

---

## IMGUI Reference for DrawInlineConfig

The command row has a variable-width area to the right of the 120px label column. All IMGUI must go inside this area — no nesting outside `DrawInlineConfig`.

### Common controls

```csharp
// Text field with expanding width
_value = GUILayout.TextField(_value ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));

// Fixed-width text field
_name = GUILayout.TextField(_name ?? "", GUILayout.Width(120));

// Narrow label
GUILayout.Label("Name", GUILayout.Width(40));

// Cycling toggle button (click = advance mode)
if (GUILayout.Button(ModeLabels[_mode], GUILayout.Width(70)))
    _mode = (_mode + 1) % ModeLabels.Length;

// Checkbox
_flag = GUILayout.Toggle(_flag, "Reload");

// Conditional row section
if (_mode == 0)
{
    GUILayout.Label("Index", GUILayout.Width(36));
    _index = GUILayout.TextField(_index ?? "0", GUILayout.MinWidth(50), GUILayout.ExpandWidth(true));
}
```

### InlineDrawContext callbacks (rarely needed)

```csharp
// Trigger key recording (SimulateKeyCommand pattern)
if (GUILayout.Button("Record Key"))
    ctx.RecordKeys?.Invoke();

// Trigger mouse click recording (SimulateMouseCommand pattern)
if (GUILayout.Button("Record Click"))
    ctx.RecordMouse?.Invoke();

// Open the list editor popup (SetListCommand pattern)
if (GUILayout.Button("Edit List"))
    ctx.OpenListEditor?.Invoke(
        () => _values.ToArray(),
        updated => { _values = new List<string>(updated); SaveTimeline(); }
    );
```

The list editor callback is automatically wired by `ActionTimeline`; no extra setup needed.

---

## All Existing TypeIds

Reference for avoiding collisions when choosing a new `TypeId`:

```
simulate_key, simulate_mouse, move_mouse, scroll, pause, sound,
wait_screenshot, wait_empty_screenshots,
checkpoint, jump, loop, confirm, resolve_on_issue, resolve_on_count, label, if,
set_string, set_integer, calc, set_list, list, list_insert, list_apply_dict,
dict_set, dict_get,
start_tracking, stop_tracking, copy_rename, clear_tracked,
set_source_path, set_destination_path, set_name_pattern,
set_rule_counter, set_rule_list, set_rule_batch,
screenshot, pose_library,
clothing_state, accessory_state,
outfit_rotate, outfit_by_name, get_fashion,
video_record, set_camera_by_name, select_object_by_name,
vnge_scene_next, vnge_scene_prev, vnge_next_scene, vnge_prev_scene, vnge_load_scene
```

---

## Run Loop Mechanics

Understanding how the runner calls commands helps write correct Execute implementations.

```
RunTimeline() coroutine (ActionTimeline):
  for each command at index:
    1. skip if !cmd.Enabled
    2. auto-pause if cmd.HasInvalidConfiguration(ctx.Variables) == true
    3. bool done = false
    4. cmd.Execute(ctx, () => done = true)
    5. while (!done) yield return null   ← busy-poll per frame
    6. if (cmd is CheckpointCommand cp) ctx.CheckpointIndices[cp.GetCheckpointName(ctx)] = index
    7. while (_isPaused) yield return null
    8. index = ctx.NextIndex ?? (index + 1)
```

- The `done` flag is set by calling `onComplete()`.
- If `ctx.PendingConfirmCallback` is set, the UI shows a "Confirm" button; clicking it calls the callback and clears the field. The run loop continues waiting because `done` is still false.
- If `ctx.PendingResolveCallback` is set, the UI shows "Resolve" (skip) and "Retry" buttons.
- `ctx.NextIndex` can be set at any point inside `Execute` (or its coroutine) to redirect flow after the command finishes.

---

## Persistence

The timeline autosaves on every mutation (add, remove, reorder, field edit, enable toggle):

- **Timeline file**: `BepInEx/config/com.hs2.sandbox/timeline.json`
- **Persistent variables**: `BepInEx/config/com.hs2.sandbox/persistent_vars.json` (debounced 3s)

Do not call save manually from inside a command — `ActionTimeline` handles all saving.

---

## Variable Scoping at Runtime

```
_persistentVariables (loaded from disk, user editable in the Persistent Vars panel)
    ↓ seeded into run first
_designTimeVariables (saved inside timeline.json, editable in Variables panel)
    ↓ seeded into run second (overwrites persistent on name collision)
ctx.Variables (fresh per run; only these are live during execution)
```

Commands read/write only `ctx.Variables`. The persistent and design-time stores are never modified by commands during a run.

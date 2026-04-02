---
name: create-timeline-command
description: Scaffold a new HS2 Sandbox timeline command. Covers the full workflow: creating the command class in Timeline/, registering it in TimelineCommandFactory, adding a toolbar button in ActionTimeline, and following the exact serialization, IMGUI, and variable-interpolation conventions of the codebase. Use when the user asks to add a new timeline command, action step, or timeline entry type. Also use when asked to implement timeline functionality, add a command to the timeline, or extend the timeline with new behavior.
---

# Create Timeline Command

## Overview

Every command lives in `Timeline/` and directly extends `TimelineCommand` (no intermediate bases). Three files must change to add one:

1. **Create** `Timeline/YourCommand.cs`
2. **Register** in `Timeline/TimelineCommandFactory.cs`
3. **Add button** in `SubWindows/ActionTimeline.cs`

## Checklist

- [ ] Create `Timeline/YourCommand.cs`
- [ ] Add case in `TimelineCommandFactory.Create()` switch
- [ ] Add `DrawAddButton(...)` call in the right category in `ActionTimeline`
- [ ] Optionally add a `CommandColors` entry

---

* Whenever Information is missing use the AskQuestion tool wherever possible

## Step 1 — Command Class

### Minimal template

```csharp
using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class YourCommand : TimelineCommand
    {
        private const char Sep = '\u0001'; // multi-field separator

        public override string TypeId => "your_type_id"; // unique lowercase_with_underscores
        public override string GetDisplayLabel() => "Your Label"; // ≤18 chars shown in 120px column

        private string _field = "";
        private int _number;

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Field", GUILayout.Width(40));
            _field = GUILayout.TextField(_field ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            // Must call onComplete() on every code path — exactly once.
            string resolved = ctx.Variables.Interpolate(_field ?? "");
            // ... your logic ...
            onComplete();
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_field) + Sep + _number;
        }

        public override void DeserializePayload(string payload)
        {
            _field = ""; _number = 0; // always reset first
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _field = p[0];
            if (p.Length >= 2 && int.TryParse(p[1], out int v)) _number = v;
        }
    }
}
```

### Required overrides

| Override | Rules |
|----------|-------|
| `TypeId` | Unique, never change after release (breaks saved files). |
| `GetDisplayLabel()` | Static label for the 120px column. Keep it short. |
| `DrawInlineConfig(ctx)` | IMGUI controls inside the command row. One `GUILayout.BeginHorizontal()` per sub-row. Always null-guard: `_field ?? ""`. |
| `Execute(ctx, onComplete)` | **`onComplete()` must be called exactly once on every code path.** May be sync or via coroutine. |
| `SerializePayload()` | Return state as a string. Use `\u0001` to join multiple fields. |
| `DeserializePayload(payload)` | Reset all fields to defaults first, then parse. Guard with `IsNullOrEmpty`. |

### Optional overrides

| Override | When to use |
|----------|------------|
| `GetValidationError(TimelineVariableStore? vars)` | Return a short error string to show a red row **and** a hover tooltip; return `null` when valid. Preferred over `HasInvalidConfiguration`. |
| `SimulateVariableEffects(TimelineVariableStore store)` | Only when the command *writes* a variable. Powers design-time validation of downstream commands. |
| `GetDisplayLabel(TimelineContext? ctx)` | Show runtime state in the label (e.g. loop iteration count). |

> **Do not** override `HasInvalidConfiguration(TimelineVariableStore? vars)` on new commands — override `GetValidationError` instead. The base `HasInvalidConfiguration` calls `GetValidationError` automatically, so you get both the red row and the tooltip for free.

---

## Step 2 — Factory Registration

In `Timeline/TimelineCommandFactory.cs`, add one line to the switch expression:

```csharp
"your_type_id" => new YourCommand(),
```

---

## Step 3 — Toolbar Button

In `SubWindows/ActionTimeline.cs`, add to `switch (_selectedCategory)`:

```csharp
DrawAddButton("YrLabel", "your_type_id", btnW, btnH);
```

Use a short label (≤8 chars fits well at `btnW = 74f`). Pick the right category:

| Case | Category |
|------|----------|
| 0 | CopyScript Controls |
| 1 | CopyScript Checks |
| 2 | CopyScript Config |
| 3 | Input |
| 4 | VNGE |
| 5 | Studio |
| 6 | Simple Variables |
| 7 | Advanced Variables |
| 8 | Nav |
| 9 | Misc |
| 10 | Video |
| 11 | FashionLine |

Optionally register a row color in the `CommandColors` dictionary (same method, near the top of the class):

```csharp
["your_type_id"] = new Color(0.5f, 0.6f, 0.8f),
```

---

## Key Patterns

### Async execution

When work spans multiple frames (API calls, polling, waiting for a screenshot):

```csharp
public override void Execute(TimelineContext ctx, Action onComplete)
{
    ctx.Runner.StartCoroutine(DoWork(ctx, onComplete));
}

private System.Collections.IEnumerator DoWork(TimelineContext ctx, Action onComplete)
{
    yield return new WaitForSeconds(1f); // or yield return null for one frame
    // ... work ...
    onComplete();
}
```

### Blocking until user confirms

Set `ctx.PendingConfirmCallback` to block the run loop and show a "Confirm" button in the UI:

```csharp
public override void Execute(TimelineContext ctx, Action onComplete)
{
    ctx.PendingConfirmCallback = () => onComplete();
    // Do NOT call onComplete() here
}
```

### Pause on error with retry

Set `ctx.PendingResolveCallback` to pause and show a "Resolve"/"Retry" button:

```csharp
if (failed)
{
    ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
    return; // do not call onComplete
}
```

### Variable interpolation

Replace `[varName]` placeholders at execute time:

```csharp
string resolved = ctx.Variables.Interpolate(_field ?? "");
```

For fields that accept an int literal *or* a variable name:

```csharp
if (!ctx.Variables.TryResolveIntOperand(_countText, out int count))
{
    ctx.PendingResolveCallback = () => Execute(ctx, onComplete);
    return;
}
```

### Validation with variable awareness

Override `GetValidationError` (not `HasInvalidConfiguration`) so the row shows both a red highlight **and** a tooltip on hover:

```csharp
public override string? GetValidationError(TimelineVariableStore? vars)
{
    if (string.IsNullOrWhiteSpace(_field)) return "Field is empty";
    if (vars != null && !vars.IsValidInterpolation(_field)) return "Unknown variable in field";
    return null; // valid
}
```

Return the first error found; the message appears verbatim in the tooltip. Return `null` when there is nothing wrong. Do **not** also override `HasInvalidConfiguration` — the base implementation calls `GetValidationError` and converts the result automatically.

### Writing a variable (SimulateVariableEffects)

```csharp
public override void SimulateVariableEffects(TimelineVariableStore store)
{
    if (!string.IsNullOrEmpty(_targetVar))
        store.SetString(_targetVar, store.Interpolate(_value ?? ""));
}
```

Also call the equivalent logic in `Execute` against `ctx.Variables`.

### Reflection-based plugin access

When calling a plugin you can't hard-reference (e.g. FashionLine, ScreenshotManager):

```csharp
private static Type? FindPluginType()
{
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        try { return asm.GetTypes().FirstOrDefault(t => t.Name == "TargetTypeName"); }
        catch (ReflectionTypeLoadException) { }
    }
    return null;
}
```

Then find instance via `FindObjectOfType` (also by reflection, as the concrete type is unavailable).

---

## Reference Examples

Two canonical implementations to read directly:

- **`Timeline/OutfitByNameCommand.cs`** — Simple sync command with reflection-based plugin access, 3-field `\u0001`-separated payload, interpolation, graceful fallback when plugin is absent.
- **`Timeline/ListInsertCommand.cs`** — Multi-mode command with a cycling button, conditional UI row, `TryResolveIntOperand`, `GetValidationError`, and `SimulateVariableEffects`.

For full variable store API, serialization conventions, and all category details, see [REFERENCE.md](REFERENCE.md).

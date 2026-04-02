using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// A command that contains a nested list of commands (subtimeline).
    /// When the timeline runs and reaches this command, execution jumps into the subtimeline,
    /// runs all its commands, then returns to the parent timeline.
    /// Variables set inside the subtimeline are visible in the parent after it returns.
    /// </summary>
    public class SubTimelineCommand : TimelineCommand
    {
        private static int _nextParamRowInstanceId;

        public override string TypeId => "sub_timeline";

        private string _id = Guid.NewGuid().ToString("N"); // compact 32-char lowercase hex, stable across saves
        private string _title = "Subtimeline";
        private bool _showRenameField;

        /// <summary>Stable unique identifier for this subtimeline body in the root <c>subtimelines</c> store.</summary>
        public string Id => _id;

        public string Title
        {
            get => _title;
            set => _title = value ?? "";
        }

        /// <summary>Nested commands. Multiple <see cref="SubTimelineCommand"/> rows may share the same list when referencing a template.</summary>
        public List<TimelineCommand> SubCommands { get; internal set; } = new List<TimelineCommand>();

        /// <summary>Values edited on the parent row when this subtimeline declares a <see cref="SubTimelineParamCommand"/>.</summary>
        public SubTimelineParamInputs ParamInputs { get; } = new SubTimelineParamInputs();

        /// <summary>
        /// Unique per row instance (template vs reference share the same <see cref="Id"/> but are different objects).
        /// Used so IMGUI param controls do not collide when multiple rows share identical layout.
        /// </summary>
        internal int ParamRowInstanceId { get; } = Interlocked.Increment(ref _nextParamRowInstanceId);

        /// <summary>Rebind this row to another definition id and shared command list (template instance).</summary>
        internal void RebindToSharedDefinition(string definitionId, List<TimelineCommand> sharedList)
        {
            _id = definitionId ?? "";
            SubCommands = sharedList ?? new List<TimelineCommand>();
        }

        public override string GetDisplayLabel() => "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            // Count badge on the left
            GUILayout.Label($"[{SubCommands.Count}]", GUILayout.Width(36));

            // Rename field or title label
            if (_showRenameField)
            {
                _title = GUILayout.TextField(_title ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("OK", GUILayout.Width(28)))
                {
                    _showRenameField = false;
                    ctx.OnSubTimelineTitleCommitted?.Invoke(this);
                }
            }
            else
            {
                var style = new GUIStyle(GuiSkinHelper.SafeLabelStyle())
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                GUILayout.Label(_title ?? "", style, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("\u270e", GUILayout.Width(22)))
                    _showRenameField = true;
            }

            // Open subtimeline button
            if (ctx.OpenSubTimeline != null)
            {
                if (GUILayout.Button("\u2192", GUILayout.Width(22)))
                    ctx.OpenSubTimeline(this);
            }

            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            // Actual execution is handled by ActionTimeline.RunCommandList; this is a no-op fallback.
            onComplete();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_title))
                return "Subtimeline title is empty";

            if (SubTimelineParamCommand.CountAll(this) > 1)
                return "Only one Param command allowed per subtimeline";

            if (vars == null) return null;

            // Incremental validation of sub-commands — apply param row first, then simulate in order
            var simStore = new TimelineVariableStore();
            simStore.CopyFrom(vars);
            var applyParam = SubTimelineParamCommand.FindFirstEnabled(this);
            if (applyParam != null)
                SubTimelineParamRuntime.FromCommand(applyParam, ParamInputs)?.ApplyTo(simStore);
            foreach (var cmd in SubCommands)
            {
                if (cmd == null || !cmd.Enabled) continue;
                if (cmd is SubTimelineParamCommand p)
                {
                    string? err = p.GetValidationError(simStore);
                    if (err == null && p.HasInvalidConfiguration())
                        err = "Invalid configuration";
                    if (err != null)
                        return $"Sub-command '{p.GetDisplayLabel()}': {err}";
                    continue;
                }
                string? err2 = cmd.GetValidationError(simStore);
                if (err2 == null && cmd.HasInvalidConfiguration())
                    err2 = "Invalid configuration";
                if (err2 != null)
                    return $"Sub-command '{cmd.GetDisplayLabel()}': {err2}";
                cmd.SimulateVariableEffects(simStore);
            }
            return null;
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            // Param row values first (same order as runtime), then other sub-commands (param row is no-op).
            var pcmd = SubTimelineParamCommand.FindFirstEnabled(this);
            if (pcmd != null)
            {
                SubTimelineParamRuntime? r = SubTimelineParamRuntime.FromCommand(pcmd, ParamInputs);
                r?.ApplyTo(store);
            }
            foreach (var cmd in SubCommands)
            {
                if (cmd == null || !cmd.Enabled) continue;
                if (cmd is SubTimelineParamCommand) continue;
                cmd.SimulateVariableEffects(store);
            }
        }

        public override string SerializePayload()
        {
            return TimelineJsonHelper.BuildSubTimelineRefJson(_id, _title ?? "", ParamInputs);
        }

        public override void DeserializePayload(string payload)
        {
            _title = "Subtimeline";
            SubCommands = new List<TimelineCommand>();

            if (string.IsNullOrWhiteSpace(payload)) return;

            bool hasEmbeddedEntries = payload.IndexOf("\"entries\"", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!hasEmbeddedEntries)
            {
                if (TimelineJsonHelper.TryParseJsonStringValue(payload, "definitionId", out string? defId) && !string.IsNullOrEmpty(defId))
                    _id = defId!;
                else if (TimelineJsonHelper.TryParseJsonStringValue(payload, "id", out string? sid) && !string.IsNullOrEmpty(sid))
                    _id = sid!;

                if (TimelineJsonHelper.TryParseJsonStringValue(payload, "title", out string? title) && title != null)
                    _title = title;

                ParamInputs.StringText = "";
                ParamInputs.IntText = "0";
                ParamInputs.BoolValue = false;
                ParamInputs.ListItems.Clear();
                ParamInputs.Dict.Clear();
                TimelineJsonHelper.TryMergeSubTimelineParam(payload, ParamInputs);
                return;
            }

            // Legacy: full nested JSON with entries[]
            if (TimelineJsonHelper.TryParseJsonStringValue(payload, "id", out string? savedId) && !string.IsNullOrEmpty(savedId))
                _id = savedId!;

            if (TimelineJsonHelper.TryParseJsonStringValue(payload, "title", out string? title2) && title2 != null)
                _title = title2;

            ParamInputs.StringText = "";
            ParamInputs.IntText = "0";
            ParamInputs.BoolValue = false;
            ParamInputs.ListItems.Clear();
            ParamInputs.Dict.Clear();
            TimelineJsonHelper.TryMergeSubTimelineParam(payload, ParamInputs);

            if (!TimelineJsonHelper.TryParseTimelineJson(payload, out List<SavedTimelineEntry>? entries) || entries == null)
                return;

            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.typeId)) continue;
                try
                {
                    var cmd = TimelineCommandFactory.Create(e.typeId);
                    cmd.DeserializePayload(e.payload ?? "");
                    cmd.Enabled = e.enabled;
                    SubCommands.Add(cmd);
                }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogWarning($"SubTimeline: load command {e.typeId} failed: {ex.Message}");
                }
            }
        }
    }
}

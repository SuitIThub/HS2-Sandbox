using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Declares a single subtimeline parameter: variable name and type. Only valid inside a subtimeline, at most once.
    /// Values are supplied from the parent <see cref="SubTimelineCommand"/> row and applied when this command runs.
    /// </summary>
    public class SubTimelineParamCommand : TimelineCommand
    {
        private const char Sep = '\u0001';
        private static readonly string[] KindLabels = { "str", "int", "bool", "list", "dict" };

        public override string TypeId => "sub_timeline_param";

        private string _variableName = "";
        private int _kindIndex;
        private SubTimelineParamKind _kind = SubTimelineParamKind.String;

        public string VariableName => _variableName ?? "";
        public SubTimelineParamKind Kind => _kind;

        public override string GetDisplayLabel() => "Param";

        /// <summary>First Param command in the subtimeline list (at most one should exist).</summary>
        public static SubTimelineParamCommand? FindFirst(SubTimelineCommand sub)
        {
            foreach (var c in sub.SubCommands)
            {
                if (c == null) continue;
                if (c is SubTimelineParamCommand p) return p;
            }
            return null;
        }

        public static SubTimelineParamCommand? FindFirstEnabled(SubTimelineCommand sub)
        {
            foreach (var c in sub.SubCommands)
            {
                if (c == null || !c.Enabled) continue;
                if (c is SubTimelineParamCommand p) return p;
            }
            return null;
        }

        public static int CountAll(SubTimelineCommand sub)
        {
            int n = 0;
            foreach (var c in sub.SubCommands)
            {
                if (c is SubTimelineParamCommand) n++;
            }
            return n;
        }

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Var", GUILayout.Width(28));
            _variableName = GUILayout.TextField(_variableName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.Label("Type", GUILayout.Width(32));
            if (GUILayout.Button(KindLabels[_kindIndex], GUILayout.Width(36)))
            {
                _kindIndex = (_kindIndex + 1) % KindLabels.Length;
                _kind = (SubTimelineParamKind)_kindIndex;
            }
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            SubTimelineParamRuntime? r = ctx.SubTimelineParamRuntime;
            if (r == null || string.IsNullOrWhiteSpace(r.VariableName))
            {
                onComplete();
                return;
            }
            r.ApplyTo(ctx.Variables);
            onComplete();
        }

        public override void SimulateVariableEffects(TimelineVariableStore store)
        {
            // Actual simulation is applied by SubTimelineCommand before iterating sub-commands.
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_variableName)) return "Variable name is empty";
            return null;
        }

        public override string SerializePayload()
        {
            string Esc(string s) => (s ?? "").Replace(Sep.ToString(), "");
            return Esc(_variableName) + Sep + _kindIndex;
        }

        public override void DeserializePayload(string payload)
        {
            _variableName = "";
            _kindIndex = 0;
            _kind = SubTimelineParamKind.String;
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(Sep);
            if (p.Length >= 1) _variableName = p[0] ?? "";
            if (p.Length >= 2 && int.TryParse(p[1], out int ki) && ki >= 0 && ki < KindLabels.Length)
            {
                _kindIndex = ki;
                _kind = (SubTimelineParamKind)ki;
            }
        }
    }
}

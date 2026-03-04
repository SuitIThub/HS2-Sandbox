using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Applies a pose from the Pose Library via VngePython (posesavestate).
    /// </summary>
    public class PoseLibraryCommand : TimelineCommand
    {
        private const char PayloadSeparator = '\u0001';

        public override string TypeId => "pose_library";

        private string _name = "";
        private string _grp = "None";
        private string _target = "fconsole";

        public override string GetDisplayLabel() => "Pose Library";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(36));
            _name = GUILayout.TextField(_name ?? "", GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
            GUILayout.Label("Group", GUILayout.Width(40));
            _grp = GUILayout.TextField(_grp ?? "None", GUILayout.MinWidth(50));
            GUILayout.Label("Target", GUILayout.Width(40));
            _target = GUILayout.TextField(_target ?? "fconsole", GUILayout.MinWidth(60));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string name = ctx.Variables.Interpolate(_name ?? "");
            string grp = ctx.Variables.Interpolate(_grp ?? "None");
            string target = ctx.Variables.Interpolate(_target ?? "fconsole");
            ApplyPoseFromLib(name, grp, target);
            onComplete();
        }

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (variablesAtThisIndex == null) return false;
            if (!variablesAtThisIndex.IsValidInterpolation(_name ?? "")) return true;
            if (!variablesAtThisIndex.IsValidInterpolation(_grp ?? "")) return true;
            if (!variablesAtThisIndex.IsValidInterpolation(_target ?? "")) return true;
            return false;
        }

        private static void ApplyPoseFromLib(string name, string grp, string target)
        {
            static string PyQuote(string s) => (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'");

            var code =
                "import posesavestate as ps\n" +
                $"ps.ext_apply_and_prep_from_lib('{PyQuote(name)}', '{PyQuote(grp)}', target='{PyQuote(target)}')\n";

            VngePython.Exec(code);
        }

        public override string SerializePayload()
        {
            return (_name ?? "") + PayloadSeparator + (_grp ?? "None") + PayloadSeparator + (_target ?? "fconsole");
        }

        public override void DeserializePayload(string payload)
        {
            _name = "";
            _grp = "None";
            _target = "fconsole";
            if (string.IsNullOrEmpty(payload)) return;
            string[] p = payload.Split(PayloadSeparator);
            if (p.Length >= 1) _name = p[0] ?? "";
            if (p.Length >= 2) _grp = string.IsNullOrEmpty(p[1]) ? "None" : (p[1] ?? "None");
            if (p.Length >= 3) _target = p[2] ?? "fconsole";
        }
    }
}

using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class SimulateKeyCommand : TimelineCommand
    {
        public override string TypeId => "simulate_key";
        private string _keyCombo = "";

        public string KeyCombo
        {
            get => _keyCombo;
            set => _keyCombo = value ?? "";
        }

        public override string GetDisplayLabel() => "Simulate Key";

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars != null && !vars.IsValidInterpolation(_keyCombo ?? ""))
                return "Unknown variable in key combination";
            string resolved = vars?.Interpolate(_keyCombo ?? "") ?? _keyCombo ?? "";
            if (!WindowsInput.ValidateKeyCombos(resolved))
                return "Invalid key combination";
            return null;
        }

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            _keyCombo = GUILayout.TextField(_keyCombo, GUILayout.ExpandWidth(true), GUILayout.MinWidth(80));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolved = ctx.Variables.Interpolate(_keyCombo ?? "");
            var (combos, allValid) = WindowsInput.ParseMultipleKeyCombos(resolved);
            if (allValid)
            {
                foreach (byte[] vks in combos)
                {
                    if (vks != null && vks.Length > 0)
                        WindowsInput.SimulateKeyCombination(vks);
                }
            }
            onComplete();
        }

        public override string SerializePayload()
        {
            return _keyCombo ?? "";
        }

        public override void DeserializePayload(string payload)
        {
            _keyCombo = payload ?? "";
        }
    }
}

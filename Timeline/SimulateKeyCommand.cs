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

        public override bool HasInvalidConfiguration() => !WindowsInput.ValidateKeyCombos(_keyCombo);

        public override bool HasInvalidConfiguration(TimelineVariableStore? variablesAtThisIndex)
        {
            if (variablesAtThisIndex == null) return HasInvalidConfiguration();
            if (!variablesAtThisIndex.IsValidInterpolation(_keyCombo ?? "")) return true;
            string resolved = variablesAtThisIndex.Interpolate(_keyCombo ?? "");
            return !WindowsInput.ValidateKeyCombos(resolved);
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

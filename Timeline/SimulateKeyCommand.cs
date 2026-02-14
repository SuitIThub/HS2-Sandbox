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

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            _keyCombo = GUILayout.TextField(_keyCombo, GUILayout.ExpandWidth(true), GUILayout.MinWidth(80));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            byte[]? vks = WindowsInput.ParseKeyCombo(_keyCombo);
            if (vks != null && vks.Length > 0)
                WindowsInput.SimulateKeyCombination(vks);
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

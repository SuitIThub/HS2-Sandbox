using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class CheckpointCommand : TimelineCommand
    {
        public override string TypeId => "checkpoint";
        private string _name = "Checkpoint";

        public string Name
        {
            get => _name;
            set => _name = value ?? "Checkpoint";
        }

        public override string GetDisplayLabel() => "Checkpoint";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(35));
            _name = GUILayout.TextField(_name, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            onComplete();
        }

        public override string SerializePayload() => _name ?? "";

        public override void DeserializePayload(string payload)
        {
            _name = string.IsNullOrWhiteSpace(payload) ? "Checkpoint" : payload.Trim();
        }
    }
}

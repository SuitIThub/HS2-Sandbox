using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Vnge scene navigation: previous (within scene). Calls VngePython.ScenePrev().
    /// </summary>
    public class VngeScenePrevCommand : TimelineCommand
    {
        public override string TypeId => "vnge_scene_prev";
        public override string GetDisplayLabel() => "Scene Prev";

        public override void DrawInlineConfig(InlineDrawContext ctx) =>
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            VngePython.ScenePrev();
            onComplete();
        }

        public override string SerializePayload() => "";
        public override void DeserializePayload(string payload) { }
    }
}

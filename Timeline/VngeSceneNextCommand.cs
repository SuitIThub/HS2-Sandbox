using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Vnge scene navigation: next (within scene). Calls VngePython.SceneNext().
    /// </summary>
    public class VngeSceneNextCommand : TimelineCommand
    {
        public override string TypeId => "vnge_scene_next";
        public override string GetDisplayLabel() => "Scene Next";

        public override void DrawInlineConfig(InlineDrawContext ctx) =>
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            VngePython.SceneNext();
            onComplete();
        }

        public override string SerializePayload() => "";
        public override void DeserializePayload(string payload) { }
    }
}

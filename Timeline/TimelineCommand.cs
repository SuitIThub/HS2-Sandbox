using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Callbacks passed to commands when drawing inline (e.g. start recording keys/mouse).
    /// </summary>
    public class InlineDrawContext
    {
        public Action? RecordKeys { get; set; }
        public Action? RecordMouse { get; set; }
    }

    /// <summary>
    /// Base type for all timeline commands. Implementations are modular and self-contained.
    /// </summary>
    public abstract class TimelineCommand
    {
        /// <summary>When false, this command is skipped when the timeline runs. Default true.</summary>
        public virtual bool Enabled { get; set; } = true;

        public abstract string TypeId { get; }
        public abstract string GetDisplayLabel();
        /// <summary>Optional run context (e.g. when timeline is running) for commands that show iteration/state in the label.</summary>
        public virtual string GetDisplayLabel(TimelineContext? runContext) => GetDisplayLabel();
        public abstract void DrawInlineConfig(InlineDrawContext ctx);
        public abstract void Execute(TimelineContext ctx, Action onComplete);
        public abstract string SerializePayload();
        public abstract void DeserializePayload(string payload);
    }

    /// <summary>
    /// DTO for saving/loading the timeline. One entry per command.
    /// </summary>
    [Serializable]
    public class SavedTimelineEntry
    {
        public string typeId = "";
        public string payload = "";
        public bool enabled = true;
    }

    [Serializable]
    public class SavedTimeline
    {
        public SavedTimelineEntry[] entries = Array.Empty<SavedTimelineEntry>();
    }

    /// <summary>
    /// Wrapper used so Unity JsonUtility actually serializes the entries array (it often skips arrays on root/direct fields).
    /// Use this for both export and persistent save/load.
    /// </summary>
    [Serializable]
    public class SavedTimelineWrapper
    {
        public SavedTimelineEntry[] entries = Array.Empty<SavedTimelineEntry>();
    }
}

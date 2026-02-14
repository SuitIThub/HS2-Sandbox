using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Context passed to timeline commands during execution.
    /// Used for API access, checkpoint resolution, and jump target.
    /// </summary>
    public class TimelineContext
    {
        public CopyScriptApiClient? ApiClient { get; set; }
        public MonoBehaviour Runner { get; set; } = null!;

        /// <summary>Checkpoint name -> index in the command list. Built before run.</summary>
        public Dictionary<string, int> CheckpointIndices { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>If set by a Jump command, runner will go to this index next instead of index+1.</summary>
        public int? NextIndex { get; set; }

        /// <summary>Last screenshot original_name from API; persists for the whole timeline run. Used by Wait for screenshot.</summary>
        public string LastScreenshotName { get; set; } = "";

        /// <summary>Per-checkpoint loop count: how many times we've jumped to that checkpoint via a Loop command this run.</summary>
        public Dictionary<string, int> LoopCounts { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>When set by a Confirm command, the UI shows a button; when clicked, this callback is invoked then cleared.</summary>
        public Action? PendingConfirmCallback { get; set; }

        /// <summary>When set by a Resolve on issue command (and issues were detected), the UI shows Resolve; when clicked, this callback is invoked then cleared.</summary>
        public Action? PendingResolveCallback { get; set; }

        public void SetJumpTarget(string checkpointName)
        {
            if (CheckpointIndices.TryGetValue(checkpointName, out int idx))
                NextIndex = idx;
        }
    }
}

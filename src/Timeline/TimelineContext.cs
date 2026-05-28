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

        /// <summary>
        /// Checkpoint name → (command list that contains it, index within that list).
        /// Populated dynamically as checkpoints are executed at any nesting level.
        /// The same name across levels is last-write-wins; checkpoint names should be unique.
        /// </summary>
        public Dictionary<string, (List<TimelineCommand> List, int Idx)> CheckpointRegistry { get; }
            = new Dictionary<string, (List<TimelineCommand> List, int Idx)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// When set by a Jump/Loop/If command, the runner navigates to this (list, index) next.
        /// If the target list is the current level, the jump is handled locally.
        /// If not, the coroutine breaks out and the parent checks whether it owns the list,
        /// eventually re-entering the correct subtimeline.
        /// </summary>
        public (List<TimelineCommand> List, int Idx)? JumpTarget { get; set; }

        /// <summary>
        /// When a cross-level jump targets a checkpoint inside a subtimeline, this stores the
        /// start index so the subtimeline begins execution at the right command rather than 0.
        /// </summary>
        public int? PendingSubEntry { get; set; }

        /// <summary>Last screenshot original_name from API; persists for the whole timeline run. Used by Wait for screenshot.</summary>
        public string LastScreenshotName { get; set; } = "";

        /// <summary>Per-checkpoint loop count: how many times we've jumped to that checkpoint via a Loop command this run.</summary>
        public Dictionary<string, int> LoopCounts { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>When set by a Confirm command, the UI shows a button; when clicked, this callback is invoked then cleared.</summary>
        public Action? PendingConfirmCallback { get; set; }

        /// <summary>When set by a Resolve on issue command (and issues were detected), the UI shows Resolve; when clicked, this callback is invoked then cleared.</summary>
        public Action? PendingResolveCallback { get; set; }

        /// <summary>When set by a config command after API failure, the UI shows Retry; when clicked, this callback is invoked then cleared (typically re-runs the command).</summary>
        public Action? PendingRetryCallback { get; set; }

        /// <summary>When set by <see cref="ScreenshotCommand"/> while waiting for plugin disk completion, the UI shows Continue; when clicked, execution proceeds without that confirmation.</summary>
        public Action? PendingScreenshotAdvanceCallback { get; set; }

        /// <summary>Variables (string and int) for this timeline run. Fresh instance per run.</summary>
        public TimelineVariableStore Variables { get; } = new TimelineVariableStore();

        /// <summary>When running a <see cref="SubTimelineCommand"/>, set before child commands run so <see cref="SubTimelineParamCommand"/> can apply parent-row values.</summary>
        public SubTimelineParamRuntime? SubTimelineParamRuntime { get; set; }

        /// <summary>Per-variable current index for List commands (variable name -> next index into that list).</summary>
        public Dictionary<string, int> ListIndices { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Set by a ReturnCommand to exit the current command list.
        /// RunCommandList clears it and breaks when it sees this set, so execution
        /// returns to the caller — either the parent subtimeline or RunTimeline (stopping the run).
        /// </summary>
        public bool ReturnRequested { get; set; }

        public void SetJumpTarget(string checkpointName)
        {
            if (CheckpointRegistry.TryGetValue(checkpointName, out var target))
                JumpTarget = target;
        }
    }
}

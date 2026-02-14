using System;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Creates timeline commands by type id. Extend when adding new command types.
    /// </summary>
    public static class TimelineCommandFactory
    {
        public static TimelineCommand Create(string typeId)
        {
            return typeId switch
            {
                "simulate_key" => new SimulateKeyCommand(),
                "simulate_mouse" => new SimulateMouseCommand(),
                "pause" => new PauseCommand(),
                "wait_screenshot" => new WaitForScreenshotCommand(),
                "wait_empty_screenshots" => new WaitForEmptyScreenshotsCommand(),
                "checkpoint" => new CheckpointCommand(),
                "jump" => new JumpToCheckpointCommand(),
                "loop" => new LoopCommand(),
                "confirm" => new ConfirmCommand(),
                "resolve_on_issue" => new ResolveOnIssueCommand(),
                "resolve_on_count" => new ResolveOnCountCommand(),
                _ => throw new ArgumentException($"Unknown timeline command type: {typeId}", nameof(typeId))
            };
        }
    }
}

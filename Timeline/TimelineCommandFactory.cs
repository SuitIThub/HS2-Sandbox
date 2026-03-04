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
                "move_mouse" => new MoveMouseCommand(),
                "scroll" => new ScrollCommand(),
                "pause" => new PauseCommand(),
                "sound" => new SoundCommand(),
                "wait_screenshot" => new WaitForScreenshotCommand(),
                "wait_empty_screenshots" => new WaitForEmptyScreenshotsCommand(),
                "checkpoint" => new CheckpointCommand(),
                "jump" => new JumpToCheckpointCommand(),
                "loop" => new LoopCommand(),
                "confirm" => new ConfirmCommand(),
                "resolve_on_issue" => new ResolveOnIssueCommand(),
                "resolve_on_count" => new ResolveOnCountCommand(),
                "start_tracking" => new StartTrackingCommand(),
                "stop_tracking" => new StopTrackingCommand(),
                "copy_rename" => new CopyRenameCommand(),
                "clear_tracked" => new ClearTrackedFilesCommand(),
                "pose_library" => new PoseLibraryCommand(),
                "vnge_scene_next" => new VngeSceneNextCommand(),
                "vnge_scene_prev" => new VngeScenePrevCommand(),
                "vnge_next_scene" => new VngeNextSceneCommand(),
                "vnge_prev_scene" => new VngePrevSceneCommand(),
                "vnge_load_scene" => new VngeLoadSceneByIndexCommand(),
                "clothing_state" => new ClothingStateCommand(),
                "accessory_state" => new AccessoryStateCommand(),
                "set_source_path" => new SetSourcePathCommand(),
                "set_destination_path" => new SetDestinationPathCommand(),
                "set_name_pattern" => new SetNamePatternCommand(),
                "set_rule_counter" => new SetCounterRuleCommand(),
                "set_rule_list" => new SetListRuleCommand(),
                "set_rule_batch" => new SetBatchRuleCommand(),
                "screenshot" => new ScreenshotCommand(),
                "outfit_rotate" => new OutfitRotateCommand(),
                "outfit_by_name" => new OutfitByNameCommand(),
                "set_string" => new SetStringCommand(),
                "set_integer" => new SetIntegerCommand(),
                "set_list" => new SetListCommand(),
                "calc" => new CalcCommand(),
                "if" => new IfCommand(),
                "list" => new ListCommand(),
                "label" => new LabelCommand(),
                _ => throw new ArgumentException($"Unknown timeline command type: {typeId}", nameof(typeId))
            };
        }
    }
}

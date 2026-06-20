# Timeline commands reference

Command type IDs are defined in `TimelineCommandFactory.Create()`. Each command serializes to JSON in `timeline.json` with a `"type"` field matching these IDs.

## Input simulation

| Type ID | Command | Purpose |
|---------|---------|---------|
| `simulate_key` | SimulateKeyCommand | Send key combo (Ctrl+A, F5, etc.) |
| `simulate_mouse` | SimulateMouseCommand | Mouse button click |
| `move_mouse` | MoveMouseCommand | Move cursor to position |
| `scroll` | ScrollCommand | Mouse wheel scroll |

## Flow control

| Type ID | Command | Purpose |
|---------|---------|---------|
| `pause` | PauseCommand | Wait for duration |
| `loop` | LoopCommand | Repeat section |
| `checkpoint` | CheckpointCommand | Mark checkpoint |
| `jump` | JumpToCheckpointCommand | Jump to checkpoint |
| `confirm` | ConfirmCommand | Wait for user confirm |
| `resolve_on_issue` | ResolveOnIssueCommand | Branch on issue flag |
| `resolve_on_count` | ResolveOnCountCommand | Branch on counter |
| `sub_timeline` | SubTimelineCommand | Run nested timeline |
| `sub_timeline_param` | SubTimelineParamCommand | Sub-timeline with params |
| `return` | ReturnCommand | Return from sub-timeline |
| `label` | LabelCommand | Label marker |

## CopyScript

| Type ID | Command | Purpose |
|---------|---------|---------|
| `start_tracking` | StartTrackingCommand | Start file tracking |
| `stop_tracking` | StopTrackingCommand | Stop tracking |
| `copy_rename` | CopyRenameCommand | Copy/rename tracked files |
| `clear_tracked` | ClearTrackedFilesCommand | Clear tracked list |
| `set_source_path` | SetSourcePathCommand | Set source path |
| `set_destination_path` | SetDestinationPathCommand | Set destination |
| `set_name_pattern` | SetNamePatternCommand | Set rename pattern |
| `set_rule_counter` | SetCounterRuleCommand | Counter rule |
| `set_rule_list` | SetListRuleCommand | List rule |
| `set_rule_batch` | SetBatchRuleCommand | Batch rule |

## Screenshots

| Type ID | Command | Purpose |
|---------|---------|---------|
| `screenshot` | ScreenshotCommand | Take screenshot |
| `wait_screenshot` | WaitForScreenshotCommand | Wait for screenshot plugin |
| `wait_empty_screenshots` | WaitForEmptyScreenshotsCommand | Wait until queue empty |
| `screenshot_alpha` | ScreenshotAlphaModeCommand | Alpha mode |
| `screenshot_resolution` | ScreenshotResolutionCommand | Resolution |
| `screenshot_save_path` | ScreenshotSavePathCommand | Save path |
| `screenshot_alt_path_var` | ScreenshotAltPathVarCommand | Alt path variable |

## Studio / scene

| Type ID | Command | Purpose |
|---------|---------|---------|
| `clothing_state` | ClothingStateCommand | Clothing on/off states |
| `accessory_state` | AccessoryStateCommand | Accessory states |
| `outfit_rotate` | OutfitRotateCommand | Rotate outfit |
| `outfit_by_name` | OutfitByNameCommand | Select outfit by name |
| `set_camera_by_name` | SetCameraByNameCommand | Select camera |
| `select_object_by_name` | SelectObjectByNameCommand | Select workspace object |
| `set_object_visible_by_name` | SetObjectVisibleByNameCommand | Toggle visibility |
| `replace_chara_card` | ReplaceCharaCardCommand | Load chara from `UserData/chara/` |
| `load_coordinate_card` | LoadCoordinateCardCommand | Load coordinate card |
| `pose_library` | PoseLibraryCommand | Pose library interaction |
| `sound` | SoundCommand | Play sound |

## Variables & logic

| Type ID | Command | Purpose |
|---------|---------|---------|
| `set` | SetVariableCommand | Set variable |
| `set_string` | SetStringCommand | Set string variable |
| `set_integer` | SetIntegerCommand | Set integer |
| `get` | GetVariableCommand | Read variable |
| `calc` | CalcCommand | Arithmetic |
| `if` | IfCommand | Conditional branch |
| `str_replace` | StrReplaceCommand | String replace |
| `list` | ListCommand | List operations |
| `set_list` | SetListCommand | Set list variable |
| `list_insert` | ListInsertCommand | Insert list item |
| `list_remove` | ListRemoveCommand | Remove list item |
| `list_apply_dict` | ListApplyDictCommand | Apply dict to list |
| `dict_set` | DictSetCommand | Dictionary set |
| `dict_get` | DictGetCommand | Dictionary get |

## VNGE (requires modified VNGE)

| Type ID | Command | Purpose |
|---------|---------|---------|
| `vnge_scene_next` | VngeSceneNextCommand | Next scene |
| `vnge_scene_prev` | VngeScenePrevCommand | Previous scene |
| `vnge_next_scene` | VngeNextSceneCommand | Next scene alt |
| `vnge_prev_scene` | VngePrevSceneCommand | Prev scene alt |
| `vnge_load_scene` | VngeLoadSceneByIndexCommand | Load scene by index |

## Other

| Type ID | Command | Purpose |
|---------|---------|---------|
| `video_record` | VideoRecordCommand | Video recording |
| `get_fashion` | GetFashionCommand | FashionLine data |

## Adding new commands (developers)

See `.cursor/skills/create-timeline-command/` in the repository:

1. Create `src/Timeline/YourCommand.cs` extending `TimelineCommand`
2. Register type ID in `TimelineCommandFactory.cs`
3. Add UI in `ActionTimelineWindow` if needed

→ [Timeline](Timeline)

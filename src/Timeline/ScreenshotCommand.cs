using System;
using System.Collections;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Triggers a screenshot via the BepInEx ScreenshotManager plugin (GUID: com.bepis.bepinex.screenshotmanager).
    /// When the plugin exposes <c>TryConsumeLastCompletedScreenshot</c>, the command waits until that reports a finished
    /// disk write, or until the user clicks Continue on the row (timeline proceeds without plugin confirmation).
    /// With Alt path, the save folder is restored after waiting ends (or after Continue).
    /// </summary>
    public class ScreenshotCommand : TimelineCommand
    {
        public override string TypeId => "screenshot";

        private bool _useAltPath;
        private bool _skipScreenshotDiskCompletionWait;

        public override string GetDisplayLabel() => "Screenshot";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            _useAltPath = GUILayout.Toggle(_useAltPath, "Alt path", GUILayout.Width(72), GUILayout.Height(18));
            GUILayout.Label($"[{ScreenshotPluginInterop.AltPathVariable.Name}]", GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (!ScreenshotPluginInterop.IsTakeRenderScreenshotAvailable)
                return "ScreenshotManager plugin not loaded";
            if (_useAltPath && !ScreenshotPluginInterop.IsStaticApiAvailable)
                return "Alt path needs Screenshot plugin path API";
            if (_useAltPath && vars != null && !vars.HasString(ScreenshotPluginInterop.AltPathVariable.Name))
                return $"Set [{ScreenshotPluginInterop.AltPathVariable.Name}] first (SS alt path var)";
            return null;
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            if (!ScreenshotPluginInterop.IsTakeRenderScreenshotAvailable)
            {
                SandboxServices.Log.LogWarning("ScreenshotManager plugin not loaded (GUID: com.bepis.bepinex.screenshotmanager). Skipping screenshot.");
                onComplete();
                return;
            }

            ctx.Runner.StartCoroutine(ExecuteScreenshotCoroutine(ctx, onComplete));
        }

        private IEnumerator ExecuteScreenshotCoroutine(TimelineContext ctx, Action onComplete)
        {
            bool swapped = false;
            string? restorePath = null;

            if (_useAltPath && ScreenshotPluginInterop.IsStaticApiAvailable)
            {
                string alt = ctx.Variables.GetString(ScreenshotPluginInterop.AltPathVariable.Name).Trim();
                if (string.IsNullOrEmpty(alt))
                {
                    SandboxServices.Log.LogWarning(
                        $"Screenshot: Alt path enabled but [{ScreenshotPluginInterop.AltPathVariable.Name}] is empty.");
                }
                else
                {
                    restorePath = ScreenshotPluginInterop.TryGetCurrentScreenshotSaveRelativePath();
                    if (restorePath == null)
                    {
                        SandboxServices.Log.LogWarning("Screenshot: Could not read current save path; alt path not applied.");
                    }
                    else if (ScreenshotPluginInterop.TrySetScreenshotSaveRelativePath(alt))
                    {
                        swapped = true;
                    }
                    else
                    {
                        SandboxServices.Log.LogWarning($"Screenshot: SetScreenshotSaveRelativePath failed for alt path \"{alt}\".");
                    }
                }
            }

            IEnumerator? routine = ScreenshotPluginInterop.TryCreateTakeRenderScreenshotRoutine();
            bool ranCapture = false;
            if (routine == null)
            {
                SandboxServices.Log.LogWarning("ScreenshotManager instance not found in scene. Skipping screenshot.");
            }
            else
            {
                if (ScreenshotPluginInterop.IsScreenshotCompletionApiAvailable)
                    ScreenshotPluginInterop.DiscardPendingScreenshotCompletion();
                yield return routine;
                ranCapture = true;
            }

            if (ranCapture && ScreenshotPluginInterop.IsScreenshotCompletionApiAvailable)
            {
                yield return null;
                _skipScreenshotDiskCompletionWait = false;
                ctx.PendingScreenshotAdvanceCallback = () => { _skipScreenshotDiskCompletionWait = true; };
                try
                {
                    while (!ScreenshotPluginInterop.TryConsumeLastCompletedScreenshot(out _))
                    {
                        if (_skipScreenshotDiskCompletionWait)
                        {
                            SandboxServices.Log.LogWarning(
                                "Screenshot: Continuing without plugin disk completion (Continue clicked on row).");
                            break;
                        }

                        yield return null;
                    }
                }
                finally
                {
                    ctx.PendingScreenshotAdvanceCallback = null;
                }
            }

            if (swapped && restorePath != null)
            {
                if (!ScreenshotPluginInterop.TrySetScreenshotSaveRelativePath(restorePath))
                    SandboxServices.Log.LogWarning("Screenshot: Failed to restore previous save path.");
            }

            onComplete();
        }

        public override string SerializePayload()
        {
            return _useAltPath ? "1" : "0";
        }

        public override void DeserializePayload(string payload)
        {
            _useAltPath = false;
            if (string.IsNullOrEmpty(payload)) return;
            _useAltPath = payload.Trim() == "1";
        }
    }
}

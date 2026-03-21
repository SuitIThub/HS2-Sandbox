using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Triggers a screenshot via the BepInEx ScreenshotManager plugin (GUID: com.bepis.bepinex.screenshotmanager).
    /// Calls ScreenshotManager.TakeRenderScreenshot(in3D: false) which performs capture and full handling.
    /// </summary>
    public class ScreenshotCommand : TimelineCommand
    {
        public override string TypeId => "screenshot";

        private static Type? _screenshotManagerType;
        private static MethodInfo? _takeRenderScreenshotMethod;
        private static MethodInfo? _findObjectOfTypeMethod;

        private static void ResolveScreenshotManager()
        {
            if (_takeRenderScreenshotMethod != null) return;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t.Name != "ScreenshotManager") continue;
                    var method = t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "TakeRenderScreenshot"
                            && m.GetParameters().Length == 1
                            && m.GetParameters()[0].ParameterType == typeof(bool)
                            && typeof(IEnumerator).IsAssignableFrom(m.ReturnType));
                    if (method != null)
                    {
                        _screenshotManagerType = t;
                        _takeRenderScreenshotMethod = method;
                        _findObjectOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new[] { typeof(Type) });
                        return;
                    }
                }
            }
        }

        public override string GetDisplayLabel() => "Screenshot";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            ResolveScreenshotManager();
            GUILayout.Label(" ", GUILayout.ExpandWidth(true));
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            ResolveScreenshotManager();
            if (_takeRenderScreenshotMethod == null)
                return "ScreenshotManager plugin not loaded";
            return null;
        }

        private static IEnumerator RunScreenshotThenComplete(IEnumerator screenshotRoutine, Action onComplete)
        {
            yield return screenshotRoutine;
            onComplete();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            ResolveScreenshotManager();
            if (_takeRenderScreenshotMethod == null || _screenshotManagerType == null || _findObjectOfTypeMethod == null)
            {
                HS2SandboxPlugin.Log.LogWarning("ScreenshotManager plugin not loaded (GUID: com.bepis.bepinex.screenshotmanager). Skipping screenshot.");
                onComplete();
                return;
            }

            try
            {
                var instance = _findObjectOfTypeMethod.Invoke(null, new object?[] { _screenshotManagerType });
                if (instance == null)
                {
                    HS2SandboxPlugin.Log.LogWarning("ScreenshotManager instance not found in scene. Skipping screenshot.");
                    onComplete();
                    return;
                }

                var enumerator = _takeRenderScreenshotMethod.Invoke(instance, new object?[] { false });
                if (enumerator is IEnumerator screenshotRoutine)
                    ctx.Runner.StartCoroutine(RunScreenshotThenComplete(screenshotRoutine, onComplete));
                else
                    onComplete();
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogError($"ScreenshotManager.TakeRenderScreenshot failed: {ex.Message}");
                onComplete();
            }
        }

        public override string SerializePayload() => "";

        public override void DeserializePayload(string payload) { }
    }
}

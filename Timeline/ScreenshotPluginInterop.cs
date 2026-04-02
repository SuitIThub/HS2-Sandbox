using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Reflection bridge to the BepInEx ScreenshotManager plugin (static config API + instance screenshot capture).
    /// </summary>
    public static class ScreenshotPluginInterop
    {
        /// <summary>Timeline variable that holds the relative save path when Screenshot runs with Alt path enabled.</summary>
        public static class AltPathVariable
        {
            public const string Name = "ScreenshotAltRelPath";
        }

        private static bool _resolved;
        private static Type? _managerType;
        private static MethodInfo? _takeRenderScreenshotMethod;
        private static MethodInfo? _findObjectOfTypeMethod;
        private static MethodInfo? _setSaveRelativePath;
        private static MethodInfo? _setResolution;
        private static MethodInfo? _setAlphaMode;
        private static MethodInfo? _tryConsumeLastCompletedScreenshot;
        private static PropertyInfo? _screenshotSaveRelativePathEntryProp;

        private static void EnsureResolved()
        {
            if (_resolved) return;

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
                    MethodInfo? setPath = t.GetMethod("SetScreenshotSaveRelativePath",
                        BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                    if (setPath != null && setPath.ReturnType == typeof(bool))
                    {
                        _setSaveRelativePath = setPath;
                        _setResolution = t.GetMethod("SetScreenshotResolution",
                            BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int), typeof(int) }, null);
                        _setAlphaMode = t.GetMethod("SetCaptureAlphaModeByName",
                            BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                        _screenshotSaveRelativePathEntryProp = t.GetProperty("ScreenshotSaveRelativePath",
                            BindingFlags.Public | BindingFlags.Static);
                        _tryConsumeLastCompletedScreenshot = t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .FirstOrDefault(IsTryConsumeLastCompletedScreenshotSignature);
                        goto FoundStatic;
                    }
                }
            }

        FoundStatic:
            _findObjectOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new[] { typeof(Type) });

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
                    MethodInfo? method = t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "TakeRenderScreenshot"
                            && !m.IsStatic
                            && m.GetParameters().Length == 1
                            && m.GetParameters()[0].ParameterType == typeof(bool)
                            && typeof(IEnumerator).IsAssignableFrom(m.ReturnType));
                    if (method != null)
                    {
                        _managerType = t;
                        _takeRenderScreenshotMethod = method;
                        goto FoundManager;
                    }
                }
            }

        FoundManager:
            _resolved = true;
        }

        private static bool IsTryConsumeLastCompletedScreenshotSignature(MethodInfo m)
        {
            if (m.Name != "TryConsumeLastCompletedScreenshot" || m.ReturnType != typeof(bool))
                return false;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != 1) return false;
            ParameterInfo p = ps[0];
            return p.IsOut && p.ParameterType == typeof(string).MakeByRefType();
        }

        public static bool IsStaticApiAvailable
        {
            get
            {
                EnsureResolved();
                return _setSaveRelativePath != null;
            }
        }

        public static bool IsTakeRenderScreenshotAvailable
        {
            get
            {
                EnsureResolved();
                return _takeRenderScreenshotMethod != null && _managerType != null && _findObjectOfTypeMethod != null;
            }
        }

        /// <summary>True when the plugin exposes <c>TryConsumeLastCompletedScreenshot</c> (disk write completion).</summary>
        public static bool IsScreenshotCompletionApiAvailable
        {
            get
            {
                EnsureResolved();
                return _tryConsumeLastCompletedScreenshot != null;
            }
        }

        /// <summary>
        /// Calls the plugin's <c>TryConsumeLastCompletedScreenshot</c>. When this returns <c>true</c>,
        /// <paramref name="screenshotRelativePath"/> is the relative path that finished writing.
        /// </summary>
        public static bool TryConsumeLastCompletedScreenshot(out string? screenshotRelativePath)
        {
            screenshotRelativePath = null;
            EnsureResolved();
            if (_tryConsumeLastCompletedScreenshot == null) return false;
            object?[] args = new object?[] { null };
            try
            {
                object? r = _tryConsumeLastCompletedScreenshot.Invoke(null, args);
                if (r is bool ok && ok)
                {
                    screenshotRelativePath = args[0] as string;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>Drops a pending completion so the next wait is tied to a subsequent capture.</summary>
        public static void DiscardPendingScreenshotCompletion()
        {
            TryConsumeLastCompletedScreenshot(out _);
        }

        public static string? TryGetCurrentScreenshotSaveRelativePath()
        {
            EnsureResolved();
            if (_screenshotSaveRelativePathEntryProp == null) return null;
            try
            {
                object? entry = _screenshotSaveRelativePathEntryProp.GetValue(null);
                if (entry == null) return null;
                PropertyInfo? valuePi = entry.GetType().GetProperty("Value");
                return valuePi?.GetValue(entry) as string;
            }
            catch
            {
                return null;
            }
        }

        public static bool TrySetScreenshotSaveRelativePath(string relativePath)
        {
            EnsureResolved();
            if (_setSaveRelativePath == null) return false;
            try
            {
                object? r = _setSaveRelativePath.Invoke(null, new object?[] { relativePath ?? "" });
                return r is bool ok && ok;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySetScreenshotResolution(int width, int height)
        {
            EnsureResolved();
            if (_setResolution == null) return false;
            try
            {
                object? r = _setResolution.Invoke(null, new object?[] { width, height });
                return r is bool ok && ok;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySetCaptureAlphaModeByName(string alphaModeName)
        {
            EnsureResolved();
            if (_setAlphaMode == null) return false;
            try
            {
                object? r = _setAlphaMode.Invoke(null, new object?[] { alphaModeName ?? "" });
                return r is bool ok && ok;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Returns an enumerator for TakeRenderScreenshot(in3D: false), or null if the instance or API is missing.</summary>
        public static IEnumerator? TryCreateTakeRenderScreenshotRoutine()
        {
            EnsureResolved();
            if (_takeRenderScreenshotMethod == null || _managerType == null || _findObjectOfTypeMethod == null)
                return null;
            try
            {
                object? instance = _findObjectOfTypeMethod.Invoke(null, new object?[] { _managerType });
                if (instance == null) return null;
                object? enumerator = _takeRenderScreenshotMethod.Invoke(instance, new object?[] { false });
                return enumerator as IEnumerator;
            }
            catch
            {
                return null;
            }
        }
    }
}

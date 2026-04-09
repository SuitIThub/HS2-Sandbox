using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class SonScaleTimelineIntegration
    {
        private const string TimelineAssemblyName = "Timeline";
        private const string TimelineCompatTypeName = "ToolBox.TimelineCompatibility";
        private const string TimelineTypeName = "Timeline.Timeline";
        private const string TimelineInterpolableDelegateTypeName = "Timeline.InterpolableDelegate";
        private const string Action5TypeName = "ToolBox.Extensions.Action`5";
        private const float MinValue = SonScaleManipulateUi.MinMul;
        private const float MaxValue = SonScaleManipulateUi.MaxMul;

        private static bool _registered;
        private static int _attemptCount;
        private static ManualLogSource? _log;

        internal static void TryInstall(ManualLogSource log)
        {
            if (_registered)
                return;

            _log = log;
            _attemptCount++;

            try
            {
                Assembly? timelineAsm = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, TimelineAssemblyName, StringComparison.OrdinalIgnoreCase));
                if (timelineAsm == null)
                {
                    if (_attemptCount == 1)
                        _log.LogWarning("Son scale timeline integration: first attempt, Timeline assembly not yet loaded.");
                    if (_attemptCount == 1 || _attemptCount % 120 == 0)
                        _log.LogInfo("Son scale timeline integration: Timeline.dll not loaded yet, waiting to register keyframes.");
                    return;
                }

                MethodInfo? addDynamic;
                MethodInfo? refresh;
                Type? interpolateDelegateType;
                string apiMode;

                if (TryResolveDirectTimelineApi(timelineAsm, out addDynamic, out refresh, out interpolateDelegateType))
                {
                    apiMode = "direct";
                }
                else if (TryResolveCompatibilityApi(timelineAsm, out addDynamic, out refresh, out interpolateDelegateType))
                {
                    apiMode = "compat";
                }
                else
                {
                    _log.LogWarning("Son scale timeline integration: could not resolve Timeline add/refresh API.");
                    return;
                }

                RegisterFloatInterpolable(
                    addDynamic,
                    interpolateDelegateType,
                    id: "hs2sandbox_sonscale_master",
                    name: "SonScale Overall",
                    get: static () => SonScaleSettings.Master,
                    set: static v => SonScaleSettings.Master = v,
                    interpolateMethod: nameof(InterpolateMaster));

                RegisterFloatInterpolable(
                    addDynamic,
                    interpolateDelegateType,
                    id: "hs2sandbox_sonscale_length",
                    name: "SonScale Length",
                    get: static () => SonScaleSettings.Length,
                    set: static v => SonScaleSettings.Length = v,
                    interpolateMethod: nameof(InterpolateLength));

                RegisterFloatInterpolable(
                    addDynamic,
                    interpolateDelegateType,
                    id: "hs2sandbox_sonscale_girth",
                    name: "SonScale Girth",
                    get: static () => SonScaleSettings.Girth,
                    set: static v => SonScaleSettings.Girth = v,
                    interpolateMethod: nameof(InterpolateGirth));

                RegisterFloatInterpolable(
                    addDynamic,
                    interpolateDelegateType,
                    id: "hs2sandbox_sonscale_balls",
                    name: "SonScale Balls",
                    get: static () => SonScaleSettings.Balls,
                    set: static v => SonScaleSettings.Balls = v,
                    interpolateMethod: nameof(InterpolateBalls));

                refresh.Invoke(null, null);
                _registered = true;
                _log.LogInfo($"Son scale timeline integration: registered keyframes for overall, length, girth, and balls (API={apiMode}).");
            }
            catch (Exception ex)
            {
                _log.LogError($"Son scale timeline integration failed: {ex.Message}");
            }
        }

        private static bool TryResolveDirectTimelineApi(
            Assembly timelineAsm,
            out MethodInfo addDynamic,
            out MethodInfo refresh,
            out Type interpolateDelegateType)
        {
            addDynamic = null!;
            refresh = null!;
            interpolateDelegateType = null!;

            Type? timelineType = timelineAsm.GetType(TimelineTypeName, throwOnError: false);
            Type? interpolableDelegate = timelineAsm.GetType(TimelineInterpolableDelegateTypeName, throwOnError: false);
            if (timelineType == null || interpolableDelegate == null)
                return false;

            addDynamic = timelineType.GetMethod("AddInterpolableModelDynamic", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
            refresh = timelineType.GetMethod("RefreshInterpolablesList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
            if (addDynamic == null || refresh == null)
                return false;

            interpolateDelegateType = interpolableDelegate;
            return true;
        }

        private static bool TryResolveCompatibilityApi(
            Assembly timelineAsm,
            out MethodInfo addDynamic,
            out MethodInfo refresh,
            out Type interpolateDelegateType)
        {
            addDynamic = null!;
            refresh = null!;
            interpolateDelegateType = null!;

            Type? compatType = timelineAsm.GetType(TimelineCompatTypeName, throwOnError: false);
            if (compatType == null)
                return false;

            MethodInfo? init = compatType.GetMethod("Init", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (init?.Invoke(null, null) is not bool ok || !ok)
                return false;

            MethodInfo? add = compatType.GetMethod("AddInterpolableModelDynamic", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo? refList = compatType.GetMethod("RefreshInterpolablesList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Type? action5Open = timelineAsm.GetType(Action5TypeName, throwOnError: false);
            if (add == null || refList == null || action5Open == null)
                return false;

            addDynamic = add;
            refresh = refList;
            interpolateDelegateType = action5Open.MakeGenericType(
                typeof(ObjectCtrlInfo), typeof(object), typeof(object), typeof(object), typeof(float));
            return true;
        }

        private static void RegisterFloatInterpolable(
            MethodInfo addDynamicMethod,
            Type interpolateDelegateType,
            string id,
            string name,
            Func<float> get,
            Action<float> set,
            string interpolateMethod)
        {
            MethodInfo interpolateMi = typeof(SonScaleTimelineIntegration).GetMethod(interpolateMethod, BindingFlags.NonPublic | BindingFlags.Static)!;
            Delegate interpolateDelegate = Delegate.CreateDelegate(interpolateDelegateType, interpolateMi);

            Func<ObjectCtrlInfo, bool> isCompatible = static oci => oci is OCIChar;
            Func<ObjectCtrlInfo, object> getParameter = static _ => null!;
            Func<ObjectCtrlInfo, object, object> getValue = (_, __) => get();
            Func<object, System.Xml.XmlNode, object> readValueFromXml = static (_, node) =>
            {
                if (node == null)
                    return 1f;
                if (float.TryParse(node.InnerText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
                    return Mathf.Clamp(f, MinValue, MaxValue);
                return 1f;
            };
            Action<object, System.Xml.XmlTextWriter, object> writeValueToXml = static (_, writer, value) =>
            {
                float v = ToFloat(value);
                writer.WriteString(v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            };
            Func<ObjectCtrlInfo, System.Xml.XmlNode, object> readParameterFromXml = static (_, __) => null!;
            Action<ObjectCtrlInfo, System.Xml.XmlTextWriter, object> writeParameterToXml = static (_, __, ___) => { };
            Func<ObjectCtrlInfo, object, object, object, bool> checkIntegrity = static (_, __, left, right) =>
                IsNumeric(left) && IsNumeric(right);
            Func<string, ObjectCtrlInfo, object, string> getFinalName = (baseName, _, __) => baseName;
            Func<ObjectCtrlInfo, object, bool> shouldShow = static (_, __) => true;

            object?[] args =
            {
                "SonScale",
                id,
                name,
                interpolateDelegate,
                interpolateDelegate,
                isCompatible,
                getValue,
                readValueFromXml,
                writeValueToXml,
                getParameter,
                readParameterFromXml,
                writeParameterToXml,
                checkIntegrity,
                true,
                getFinalName,
                shouldShow
            };

            addDynamicMethod.Invoke(null, args);

            // Apply current value so timeline scrubbing updates injected UI row captions and slider handles.
            set(Mathf.Clamp(get(), MinValue, MaxValue));
            SonScaleManipulateUi.PushSettingsToSliders();
        }

        private static void InterpolateMaster(ObjectCtrlInfo _, object __, object left, object right, float factor)
        {
            SonScaleSettings.Master = LerpValue(left, right, factor);
            SonScaleManipulateUi.PushSettingsToSliders();
        }

        private static void InterpolateLength(ObjectCtrlInfo _, object __, object left, object right, float factor)
        {
            SonScaleSettings.Length = LerpValue(left, right, factor);
            SonScaleManipulateUi.PushSettingsToSliders();
        }

        private static void InterpolateGirth(ObjectCtrlInfo _, object __, object left, object right, float factor)
        {
            SonScaleSettings.Girth = LerpValue(left, right, factor);
            SonScaleManipulateUi.PushSettingsToSliders();
        }

        private static void InterpolateBalls(ObjectCtrlInfo _, object __, object left, object right, float factor)
        {
            SonScaleSettings.Balls = LerpValue(left, right, factor);
            SonScaleManipulateUi.PushSettingsToSliders();
        }

        private static float LerpValue(object left, object right, float t)
        {
            float a = ToFloat(left);
            float b = ToFloat(right);
            return Mathf.Clamp(Mathf.Lerp(a, b, t), MinValue, MaxValue);
        }

        private static bool IsNumeric(object value) =>
            value is float or double or int or long or short or decimal or byte;

        private static float ToFloat(object value)
        {
            if (value == null)
                return 1f;
            try
            {
                return Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 1f;
            }
        }
    }

    internal sealed class SonScaleTimelineBootstrap : MonoBehaviour
    {
        private ManualLogSource? _log;
        private int _frames;

        internal void Init(ManualLogSource log)
        {
            _log = log;
        }

        private void Update()
        {
            if (_log == null)
                return;

            _frames++;
            if (_frames % 60 != 0)
                return;

            SonScaleTimelineIntegration.TryInstall(_log);
        }
    }
}

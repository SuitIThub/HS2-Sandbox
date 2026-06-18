using System;
using System.Linq;
using System.Reflection;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace HS2SandboxPlugin
{
    /// <summary>Syncs Studio's Manipulate → Character → Animation panel after programmatic changes.</summary>
    internal static class AnimBrowserAnimationUiBridge
    {
        private const string CharaRootPath = "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara";

        private static readonly object Lock = new object();
        private static bool _initialized;

        private static Type? _mpCharCtrlType;
        private static FieldInfo? _animeControlField;
        private static FieldInfo? _mpOciCharField;
        private static PropertyInfo? _animeObjectCtrlInfoProp;
        private static FieldInfo? _animeSliderSpeedField;
        private static FieldInfo? _animeInputSpeedField;
        private static FieldInfo? _animeSliderPatternField;
        private static FieldInfo? _animeInputPatternField;
        private static FieldInfo? _animeToggleLoopField;
        private static MethodInfo? _animeUpdateInfoMethod;

        public static void TryRefreshFirstSelected(
            System.Collections.Generic.IEnumerable<OCIChar> characters,
            bool refreshSpeed = false,
            bool refreshPattern = false,
            bool refreshLoop = false)
        {
            OCIChar? first = null;
            foreach (var oci in characters)
            {
                if (oci != null)
                {
                    first = oci;
                    break;
                }
            }

            if (first == null)
                return;

            TryRefreshAnimationPanel(first, refreshSpeed, refreshPattern, refreshLoop);
        }

        public static void TryRefreshAnimationPanel(
            OCIChar? oci,
            bool refreshSpeed = true,
            bool refreshPattern = true,
            bool refreshLoop = true)
        {
            if (oci == null)
                return;

            try
            {
                EnsureInitialized();
                if (_animeControlField == null)
                    return;

                object? mpCtrl = GetMpCharCtrlComponent();
                if (mpCtrl == null)
                    return;

                object? animeControl = _animeControlField.GetValue(mpCtrl);
                if (animeControl == null)
                    return;

                if (!IsAnimeControlShowingCharacter(animeControl, mpCtrl, oci))
                    return;

                _animeUpdateInfoMethod?.Invoke(animeControl, null);

                if (refreshSpeed)
                    TryPushSpeedWidgets(animeControl, oci.animeSpeed);
                if (refreshPattern)
                    TryPushPatternWidgets(animeControl, oci.animePattern);
                if (refreshLoop)
                    TryPushLoopWidget(animeControl, oci);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimBrowser: Could not refresh animation panel: " + ex.Message);
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (Lock)
            {
                if (_initialized)
                    return;

                try
                {
                    _mpCharCtrlType = FindType("Studio.MPCharCtrl");
                    Type? animeControlType = FindType("Studio.AnimeControl");
                    if (_mpCharCtrlType == null || animeControlType == null)
                        return;

                    _animeControlField = _mpCharCtrlType.GetField(
                        "animeControl",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _mpOciCharField = _mpCharCtrlType.GetField(
                        "m_OCIChar",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeObjectCtrlInfoProp = animeControlType.GetProperty(
                        "objectCtrlInfo",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeSliderSpeedField = animeControlType.GetField(
                        "sliderSpeed",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeInputSpeedField = animeControlType.GetField(
                        "inputSpeed",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeSliderPatternField = animeControlType.GetField(
                        "sliderPattern",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeInputPatternField = animeControlType.GetField(
                        "inputPattern",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeToggleLoopField = animeControlType.GetField(
                        "toggleLoop",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeUpdateInfoMethod = animeControlType.GetMethod(
                        "UpdateInfo",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        private static Type? FindType(string fullName) =>
            AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        return e.Types.Where(t => t != null)!;
                    }
                    catch
                    {
                        return new Type[0];
                    }
                })
                .FirstOrDefault(t => t != null && t.FullName == fullName);

        private static object? GetMpCharCtrlComponent()
        {
            GameObject root = GameObject.Find(CharaRootPath);
            if (root == null || _mpCharCtrlType == null)
                return null;
            return root.GetComponent(_mpCharCtrlType);
        }

        private static bool IsAnimeControlShowingCharacter(object animeControl, object mpCtrl, OCIChar oci)
        {
            if (_animeObjectCtrlInfoProp?.GetValue(animeControl, null) is ObjectCtrlInfo info &&
                ReferenceEquals(info, oci))
                return true;

            if (_mpOciCharField?.GetValue(mpCtrl) is OCIChar mpOci && ReferenceEquals(mpOci, oci))
                return true;

            return false;
        }

        private static void TryPushSpeedWidgets(object animeControl, float speed)
        {
            if (_animeSliderSpeedField?.GetValue(animeControl) is Slider slider && slider != null)
                slider.value = speed;

            if (_animeInputSpeedField?.GetValue(animeControl) is InputField input && input != null)
                input.text = speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void TryPushPatternWidgets(object animeControl, float pattern)
        {
            if (_animeSliderPatternField?.GetValue(animeControl) is Slider slider && slider != null)
                slider.value = pattern;

            if (_animeInputPatternField?.GetValue(animeControl) is InputField input && input != null)
                input.text = pattern.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void TryPushLoopWidget(object animeControl, OCIChar oci)
        {
            if (_animeToggleLoopField?.GetValue(animeControl) is Toggle toggle && toggle != null)
            {
                bool loop = oci.oiCharInfo != null && oci.oiCharInfo.isAnimeForceLoop;
                if (!loop && oci.charAnimeCtrl != null)
                    loop = oci.charAnimeCtrl.isForceLoop;
                toggle.isOn = loop;
            }
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using Studio;
using UnityEngine;
using UnityEngine.UI;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Syncs Studio's Manipulate → Character → Animation panel after programmatic <see cref="ObjectCtrlInfo.animeSpeed"/> changes.
    /// </summary>
    internal static class PoseBrowserAnimationUiBridge
    {
        private const string CharaRootPath = "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara";

        private static readonly object Lock = new object();
        private static bool _initialized;

        private static Type? _mpCharCtrlType;
        private static Type? _animeControlType;
        private static FieldInfo? _animeControlField;
        private static FieldInfo? _mpOciCharField;
        private static PropertyInfo? _animeObjectCtrlInfoProp;
        private static FieldInfo? _animeSliderSpeedField;
        private static FieldInfo? _animeInputSpeedField;
        private static MethodInfo? _animeUpdateInfoMethod;

        public static void TryRefreshAnimationPanel(OCIChar? oci)
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
                TryPushSpeedWidgets(animeControl, oci.animeSpeed);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"PoseBrowser: Could not refresh animation panel: {ex.Message}");
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
                    _animeControlType = FindType("Studio.AnimeControl");
                    if (_mpCharCtrlType == null || _animeControlType == null)
                        return;

                    _animeControlField = _mpCharCtrlType.GetField(
                        "animeControl",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _mpOciCharField = _mpCharCtrlType.GetField(
                        "m_OCIChar",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeObjectCtrlInfoProp = _animeControlType.GetProperty(
                        "objectCtrlInfo",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeSliderSpeedField = _animeControlType.GetField(
                        "sliderSpeed",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeInputSpeedField = _animeControlType.GetField(
                        "inputSpeed",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _animeUpdateInfoMethod = _animeControlType.GetMethod(
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
                        return Array.Empty<Type>();
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
    }
}

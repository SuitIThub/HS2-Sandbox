using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Reflection-based bridge to Studio's MPCharCtrl.stateInfo clothing / accessory API.
    /// Uses the GameObject at "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara".
    /// </summary>
    internal static class StudioCharStateBridge
    {
        private const string CharaRootPath = "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara";

        private static readonly object _lock = new object();
        private static bool _initialized;

        private static Type? _mpCharCtrlType;
        private static FieldInfo? _stateInfoField;
        private static MethodInfo? _onClickCosState;
        private static MethodInfo? _onClickClothingDetails;
        private static MethodInfo? _onClickAccessories;

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                try
                {
                    // Find Studio.MPCharCtrl in any loaded assembly
                    _mpCharCtrlType = AppDomain.CurrentDomain
                        .GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.GetTypes(); }
                            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
                            catch { return Array.Empty<Type>(); }
                        })
                        .FirstOrDefault(t => t != null && t.FullName == "Studio.MPCharCtrl");

                    if (_mpCharCtrlType == null)
                    {
                        _initialized = true;
                        return;
                    }

                    _stateInfoField = _mpCharCtrlType.GetField("stateInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (_stateInfoField == null)
                    {
                        _initialized = true;
                        return;
                    }

                    Type stateInfoType = _stateInfoField.FieldType;
                    _onClickCosState = stateInfoType.GetMethod("OnClickCosState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                    _onClickClothingDetails = stateInfoType.GetMethod("OnClickClothingDetails", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(byte) }, null);
                    _onClickAccessories = stateInfoType.GetMethod("OnClickAccessories", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(bool) }, null);
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        private static object? GetStateInfoInstance()
        {
            EnsureInitialized();
            if (_mpCharCtrlType == null || _stateInfoField == null)
                return null;

            GameObject root = GameObject.Find(CharaRootPath);
            if (root == null)
                return null;

            try
            {
                var ctrl = root.GetComponent(_mpCharCtrlType);
                if (ctrl == null)
                    return null;
                return _stateInfoField.GetValue(ctrl);
            }
            catch
            {
                return null;
            }
        }

        public static bool TrySetOutfitState(int stateIndex)
        {
            // 0 = On, 1 = Half, 2 = Off (per user findings)
            object? stateInfo = GetStateInfoInstance();
            if (stateInfo == null || _onClickCosState == null)
                return false;

            int clamped = Mathf.Clamp(stateIndex, 0, 2);
            try
            {
                _onClickCosState.Invoke(stateInfo, new object[] { clamped });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySetClothingDetailState(int clothingTypeIndex, int stateIndex)
        {
            // clothingTypeIndex: 0-based index over Top, Bottom, Inner Top, Inner Bottom, Stockings, Gloves, Socks, Shoes
            object? stateInfo = GetStateInfoInstance();
            if (stateInfo == null || _onClickClothingDetails == null)
                return false;

            if (clothingTypeIndex < 0)
                return false;

            byte stateByte = (byte)Mathf.Clamp(stateIndex, 0, 2);
            try
            {
                _onClickClothingDetails.Invoke(stateInfo, new object[] { clothingTypeIndex, stateByte });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySetAccessoryState(int slotIndex, bool on)
        {
            // slotIndex: 0..19 for the 20 accessory slots
            object? stateInfo = GetStateInfoInstance();
            if (stateInfo == null || _onClickAccessories == null)
                return false;

            if (slotIndex < 0)
                return false;

            try
            {
                _onClickAccessories.Invoke(stateInfo, new object[] { slotIndex, on });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}


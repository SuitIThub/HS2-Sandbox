using System;
using System.Collections.Generic;
using System.Reflection;
using AIChara;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Studio Better Penetration solves the shaft in world space from <c>DanAgent.m_baseDanLength</c>. We patch
    /// <c>SetDanTarget</c> to scale that reference length (Master×Length). Girth is not applied per DanPoint
    /// here: multiplying XY on every segment in a parented chain compounds toward the tip (effective girth exponent per depth).
    /// <see cref="SonScaleApplier"/> applies a single dan-root <c>(girth,girth,1)</c> for uniform thickness.
    /// </summary>
    internal static class SonScaleBpIntegration
    {
        private const string HarmonyId = "com.hs2.sandbox.sonscale.betterpenetration";

        internal static ManualLogSource? Log { get; set; }

        internal static bool LengthHooksInstalled { get; private set; }

        private static Type? _danAgentType;

        internal static FieldInfo? FiBaseLen;
        internal static FieldInfo? FiDanCharacter;
        private static Type? _bpControllerType;

        internal static void TryInstall()
        {
            if (LengthHooksInstalled)
                return;

            try
            {
                if (!ResolveDanAgentAndFields())
                {
                    Log?.LogInfo(
                        "Son scale: Studio Better Penetration DanAgent.SetDanTarget not found; using legacy shaft scaling.");
                    return;
                }

                MethodInfo? target = FindStudioSetDanTarget();
                if (target == null)
                {
                    Log?.LogWarning("Son scale: DanAgent.SetDanTarget(6 args) missing.");
                    return;
                }

                var harmony = new Harmony(HarmonyId);
                harmony.Patch(
                    target,
                    prefix: new HarmonyMethod(typeof(SonScaleDanAgentHarmonyPatches), nameof(SonScaleDanAgentHarmonyPatches.Prefix)),
                    postfix: new HarmonyMethod(typeof(SonScaleDanAgentHarmonyPatches), nameof(SonScaleDanAgentHarmonyPatches.Postfix)));

                LengthHooksInstalled = true;
                Log?.LogInfo(
                    "Son scale: BP integration on (length = m_baseDanLength × Master×Length; girth = dan root XY via SonScaleApplier).");
            }
            catch (Exception ex)
            {
                Log?.LogError($"Son scale: BP integration failed: {ex.Message}");
            }
        }

        internal static bool IsBpDrivingShaft(ChaControl? cha)
        {
            if (cha == null || _bpControllerType == null)
                return false;

            Component? c = cha.gameObject.GetComponent(_bpControllerType);
            if (c == null)
                return false;

            PropertyInfo? en = _bpControllerType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
            if (en?.GetValue(c) is not true)
                return false;

            FieldInfo? dtv = _bpControllerType.GetField("danTargetsValid", BindingFlags.Instance | BindingFlags.NonPublic);
            return dtv?.GetValue(c) is true;
        }

        internal static bool IsChaStudioSelected(ChaControl cha)
        {
            try
            {
                IEnumerable<OCIChar>? sel = StudioAPI.GetSelectedCharacters();
                if (sel is null)
                    return false;

                foreach (OCIChar? oci in sel)
                {
                    if (oci?.charInfo == cha)
                        return true;
                }
            }
            catch
            {
                // ignored
            }

            return false;
        }

        private static bool ResolveDanAgentAndFields()
        {
            FiBaseLen = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? asmName = asm.GetName().Name;
                if (asmName == null || asmName.IndexOf("BetterPenetration", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                Type? agent = asm.GetType("Core_BetterPenetration.DanAgent");
                if (agent == null)
                    continue;

                if (FindStudioSetDanTargetOn(agent) == null)
                    continue;

                Type? controller = asm.GetType("Core_BetterPenetration.BetterPenetrationController");

                FieldInfo? flBase = agent.GetField("m_baseDanLength", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? flCha = agent.GetField("m_danCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
                if (flBase == null || flCha == null)
                    continue;

                _danAgentType = agent;
                FiBaseLen = flBase;
                FiDanCharacter = flCha;
                _bpControllerType = controller;
                return true;
            }

            return false;
        }

        private static MethodInfo? FindStudioSetDanTarget() =>
            _danAgentType == null ? null : FindStudioSetDanTargetOn(_danAgentType);

        private static MethodInfo? FindStudioSetDanTargetOn(Type agent)
        {
            foreach (MethodInfo m in agent.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "SetDanTarget")
                    continue;

                ParameterInfo[] p = m.GetParameters();
                if (p.Length != 6 || p[2].ParameterType.Name != "CollisionAgent")
                    continue;

                return m;
            }

            return null;
        }

        private sealed class DanTargetPatchState
        {
            internal object? Agent;
            internal float OriginalBaseLen;
            internal bool ScaledBaseLen;
        }

        /// <summary>Harmony target methods must be public for IL patching.</summary>
        internal static class SonScaleDanAgentHarmonyPatches
        {
            public static void Prefix(object __instance, ref object? __state)
            {
                __state = null;
                if (!SonScaleSettings.Enabled || !StudioAPI.StudioLoaded)
                    return;

                if (SonScaleBpIntegration.FiBaseLen == null || SonScaleBpIntegration.FiDanCharacter == null)
                    return;

                if (SonScaleBpIntegration.FiDanCharacter.GetValue(__instance) is not ChaControl cha)
                    return;

                if (!SonScaleBpIntegration.IsChaStudioSelected(cha))
                    return;

                float mul = Mathf.Clamp(SonScaleSettings.Master * SonScaleSettings.Length, 0.05f, 50f);
                float baseLen = (float)SonScaleBpIntegration.FiBaseLen.GetValue(__instance)!;
                bool scaleLen = Mathf.Abs(mul - 1f) >= 1e-5f;

                __state = new DanTargetPatchState
                {
                    Agent = __instance,
                    OriginalBaseLen = baseLen,
                    ScaledBaseLen = scaleLen
                };

                if (scaleLen)
                    SonScaleBpIntegration.FiBaseLen.SetValue(__instance, baseLen * mul);
            }

            public static void Postfix(object __instance, object? __state)
            {
                if (SonScaleBpIntegration.FiBaseLen == null)
                    return;

                if (__state is not DanTargetPatchState st || !ReferenceEquals(st.Agent, __instance))
                    return;

                if (st.ScaledBaseLen)
                    SonScaleBpIntegration.FiBaseLen.SetValue(__instance, st.OriginalBaseLen);
            }
        }
    }
}

#if HS2 || AI
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIChara;
using BepInEx.Configuration;
using HarmonyLib;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public enum HeelzOverride
    {
        Default,
        ForceOff,
        ForceOn
    }

    internal struct HeelzCharacterState
    {
        public string DisplayName;
        public bool ShoesOn;
        public bool HeelzActive;
        public bool IsHovering;
        public float HeelHeight;
        public HeelzOverride Override;
        public bool AutoEnabled;

        // Pre-built display strings (rebuilt on refresh, not per frame)
        public string ShoeLabel;
        public string HeelLabel;
        public string ShoeDisplayText;
        public string HeelDisplayText;
    }

    internal static class HeelzControlService
    {
        private static readonly Dictionary<ChaControl, HeelzOverride> _overrides = new();
        private static readonly Dictionary<ChaControl, bool> _autoEnabled = new();
        private static bool _initialized;
        private static bool _heelzDetected;

        // Reflection handles for HeelsHandler (fields/properties discovered at runtime)
        private static Type? _handlerType;
        private static FieldInfo? _isActiveField;
        private static FieldInfo? _isHoverField;
        private static PropertyInfo? _chaControlProp;
        private static PropertyInfo? _configProp;
        private static FieldInfo? _configRootField;

        // Reflection handle for HeelsController.Handler property
        private static Type? _controllerType;
        private static PropertyInfo? _controllerHandlerProp;

        // Config entries
        public static ConfigEntry<string>? HeelsOffTagsEntry;
        public static ConfigEntry<string>? HeelsOnTagsEntry;
        public static ConfigEntry<KeyboardShortcut>? HotkeyToggleHeelzControl;

        // Cached tag sets (rebuilt only when config changes)
        private static HashSet<string> _heelsOffTags = new(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _heelsOnTags = new(StringComparer.OrdinalIgnoreCase);
        private static string _cachedOffTagLabel = "";
        private static string _cachedOnTagLabel = "";

        public static bool IsHeelzDetected => _heelzDetected;
        public static ICollection<string> HeelsOffTags => _heelsOffTags;
        public static ICollection<string> HeelsOnTags => _heelsOnTags;
        public static string CachedOffTagLabel => _cachedOffTagLabel;
        public static string CachedOnTagLabel => _cachedOnTagLabel;

        // ------------------------------------------------------------------
        // Initialization
        // ------------------------------------------------------------------

        public static void Initialize(ConfigFile cfg)
        {
            if (_initialized) return;
            _initialized = true;

            HeelsOffTagsEntry = cfg.Bind(
                "Heelz Control",
                "Heelz OFF tags",
                "",
                new ConfigDescription(
                    "Comma-separated pose tags that automatically disable heel hover when a matching pose is applied."));

            HeelsOnTagsEntry = cfg.Bind(
                "Heelz Control",
                "Heelz ON tags",
                "",
                new ConfigDescription(
                    "Comma-separated pose tags that automatically enable heel hover when a matching pose is applied."));

            const string windowHk =
                "Uses BepInEx KeyboardShortcut (main key + optional modifiers in Configuration Manager). " +
                "Leave unassigned (None) to disable.";

            HotkeyToggleHeelzControl = cfg.Bind(
                PoseBrowserConfig.KeyboardSection,
                "Toggle Heelz Control window",
                new KeyboardShortcut(KeyCode.None),
                new ConfigDescription(
                    "Open or close the Heelz Control window. " + windowHk));

            RebuildTagSetsFromConfig();
            TryPatchHeelz();
        }

        // ------------------------------------------------------------------
        // Tag-rule config
        // ------------------------------------------------------------------

        public static void RebuildTagSetsFromConfig()
        {
            _heelsOffTags = ParseTagList(HeelsOffTagsEntry?.Value);
            _heelsOnTags = ParseTagList(HeelsOnTagsEntry?.Value);
            RebuildCachedLabels();
        }

        public static void SetHeelsOffTags(HashSet<string> tags)
        {
            _heelsOffTags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            if (HeelsOffTagsEntry != null)
                HeelsOffTagsEntry.Value = string.Join(",", _heelsOffTags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray());
            RebuildCachedLabels();
        }

        public static void SetHeelsOnTags(HashSet<string> tags)
        {
            _heelsOnTags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            if (HeelsOnTagsEntry != null)
                HeelsOnTagsEntry.Value = string.Join(",", _heelsOnTags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray());
            RebuildCachedLabels();
        }

        /// <summary>Returns the union of all OFF + ON rule tags (for surfacing in Pose Browser tag lists).</summary>
        public static HashSet<string> GetAllRuleTags()
        {
            var all = new HashSet<string>(_heelsOffTags, StringComparer.OrdinalIgnoreCase);
            foreach (var t in _heelsOnTags) all.Add(t);
            return all;
        }

        // ------------------------------------------------------------------
        // Per-character override
        // ------------------------------------------------------------------

        public static HeelzOverride GetOverride(ChaControl cha)
        {
            return _overrides.TryGetValue(cha, out var val) ? val : HeelzOverride.Default;
        }

        public static void SetOverride(ChaControl cha, HeelzOverride value)
        {
            if (value == HeelzOverride.Default)
                _overrides.Remove(cha);
            else
                _overrides[cha] = value;

            if (_heelzDetected)
                ApplyOverrideImmediate(cha, value);
        }

        public static bool GetAutoEnabled(ChaControl cha)
        {
            return !_autoEnabled.TryGetValue(cha, out var val) || val;
        }

        public static void SetAutoEnabled(ChaControl cha, bool enabled)
        {
            if (enabled)
                _autoEnabled.Remove(cha);
            else
                _autoEnabled[cha] = false;
        }

        // ------------------------------------------------------------------
        // Tag-rule application (called after pose apply)
        // ------------------------------------------------------------------

        public static void ApplyTagRules(ChaControl cha, HashSet<string>? poseTags)
        {
            if (poseTags == null || poseTags.Count == 0) return;
            if (_heelsOffTags.Count == 0 && _heelsOnTags.Count == 0) return;
            if (!GetAutoEnabled(cha)) return;

            foreach (var tag in poseTags)
            {
                if (_heelsOffTags.Contains(tag))
                {
                    SetOverride(cha, HeelzOverride.ForceOff);
                    return;
                }
            }

            foreach (var tag in poseTags)
            {
                if (_heelsOnTags.Contains(tag))
                {
                    SetOverride(cha, HeelzOverride.ForceOn);
                    return;
                }
            }
        }

        /// <summary>Apply tag rules to all Studio-selected characters (used after single-pose apply).</summary>
        public static void ApplyTagRulesForSelectedCharacters(
            IEnumerable<OCIChar> characters,
            HashSet<string>? poseTags)
        {
            if (poseTags == null || poseTags.Count == 0) return;
            if (_heelsOffTags.Count == 0 && _heelsOnTags.Count == 0) return;

            foreach (var oci in characters)
            {
                if (oci?.charInfo != null)
                    ApplyTagRules(oci.charInfo, poseTags);
            }
        }

        /// <summary>Apply tag rules to all chars using union of tags from multiple poses (used after multi-apply).</summary>
        public static void ApplyTagRulesForMultiApply(
            IList<OCIChar> characters,
            IList<PoseGridItem> poses)
        {
            if (_heelsOffTags.Count == 0 && _heelsOnTags.Count == 0) return;
            if (characters.Count == 0 || poses.Count == 0) return;

            var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pose in poses)
            {
                if (pose.Tags != null)
                    foreach (var t in pose.Tags) allTags.Add(t);
            }

            if (allTags.Count == 0) return;

            foreach (var oci in characters)
            {
                if (oci?.charInfo != null)
                    ApplyTagRules(oci.charInfo, allTags);
            }
        }

        // ------------------------------------------------------------------
        // Character state query (cached per refresh cycle, not per frame)
        // ------------------------------------------------------------------

        public static HeelzCharacterState GetCharacterState(OCIChar oci)
        {
            var state = new HeelzCharacterState();
            ChaControl? cha = oci?.charInfo;
            if (cha == null)
            {
                state.DisplayName = "???";
                state.ShoeLabel = "?";
                state.HeelLabel = "--";
                state.ShoeDisplayText = "Shoes: ?";
                state.HeelDisplayText = "Heel: --";
                return state;
            }

            state.DisplayName = PoseDataService.GetOCICharDisplayName(oci!);
            state.Override = GetOverride(cha);
            state.AutoEnabled = GetAutoEnabled(cha);

            try { state.ShoesOn = cha.fileStatus.clothesState[7] == 0; }
            catch { state.ShoesOn = false; }

            state.ShoeLabel = state.ShoesOn ? "On" : "Off";
            state.ShoeDisplayText = state.ShoesOn ? "Shoes: On" : "Shoes: Off";

            if (!_heelzDetected || _controllerType == null || _controllerHandlerProp == null)
            {
                state.HeelLabel = "--";
                state.HeelDisplayText = "Heel: --";
                return state;
            }

            try
            {
                var controller = cha.gameObject.GetComponent(_controllerType);
                if (controller == null)
                {
                    state.HeelLabel = "--";
                    state.HeelDisplayText = "Heel: --";
                    return state;
                }

                object? handler = _controllerHandlerProp.GetValue(controller);
                if (handler == null)
                {
                    state.HeelLabel = "--";
                    state.HeelDisplayText = "Heel: --";
                    return state;
                }

                state.HeelzActive = _isActiveField != null && (bool)_isActiveField.GetValue(handler);
                state.IsHovering = _isHoverField != null && (bool)_isHoverField.GetValue(handler);

                if (state.HeelzActive && _configProp != null && _configRootField != null)
                {
                    object? config = _configProp.GetValue(handler);
                    if (config != null)
                    {
                        object? root = _configRootField.GetValue(config);
                        if (root is Vector3 v)
                            state.HeelHeight = v.y;
                    }
                }

                if (state.HeelzActive)
                {
                    state.HeelLabel = state.HeelHeight.ToString("F3");
                    state.HeelDisplayText = "Heel: " + state.HeelLabel;
                }
                else
                {
                    state.HeelLabel = "--";
                    state.HeelDisplayText = "Heel: --";
                }
            }
            catch
            {
                state.HeelLabel = "--";
                state.HeelDisplayText = "Heel: --";
            }

            return state;
        }

        /// <summary>Remove entries for destroyed ChaControls from the override and auto maps.</summary>
        public static void CleanupDestroyedCharacters()
        {
            List<ChaControl>? dead = null;
            foreach (var kvp in _overrides)
            {
                if (kvp.Key == null)
                {
                    dead ??= new List<ChaControl>();
                    dead.Add(kvp.Key);
                }
            }
            if (dead != null)
            {
                foreach (var key in dead)
                {
                    _overrides.Remove(key);
                    _autoEnabled.Remove(key);
                }
            }

            dead = null;
            foreach (var kvp in _autoEnabled)
            {
                if (kvp.Key == null)
                {
                    dead ??= new List<ChaControl>();
                    dead.Add(kvp.Key);
                }
            }
            if (dead != null)
                foreach (var key in dead) _autoEnabled.Remove(key);
        }

        // ------------------------------------------------------------------
        // Heelz plugin detection + Harmony patch
        // ------------------------------------------------------------------

        private static void TryPatchHeelz()
        {
            try
            {
                _handlerType = FindType("Heels.Handler.HeelsHandler");
                if (_handlerType == null)
                {
                    _heelzDetected = false;
                    SandboxServices.Log.LogInfo("HeelzControl: HS2Heelz not detected — patching skipped.");
                    return;
                }

                _controllerType = FindType("Heels.Controller.HeelsController");
                if (_controllerType != null)
                    _controllerHandlerProp = _controllerType.GetProperty("Handler", BindingFlags.Public | BindingFlags.Instance);

                _isActiveField = _handlerType.GetField("IsActive", BindingFlags.Public | BindingFlags.Instance);
                _isHoverField = _handlerType.GetField("IsHover", BindingFlags.Public | BindingFlags.Instance);
                _chaControlProp = _handlerType.GetProperty("ChaControl", BindingFlags.Public | BindingFlags.Instance);
                _configProp = _handlerType.GetProperty("Config", BindingFlags.Public | BindingFlags.Instance);

                if (_configProp != null)
                    _configRootField = _configProp.PropertyType.GetField("Root", BindingFlags.Public | BindingFlags.Instance);

                MethodInfo? hoverBody = _handlerType.GetMethod("HoverBody", BindingFlags.Public | BindingFlags.Instance);
                if (hoverBody == null)
                {
                    SandboxServices.Log.LogWarning("HeelzControl: Found HeelsHandler but HoverBody method not found.");
                    _heelzDetected = false;
                    return;
                }

                var harmony = new Harmony("com.hs2.sandbox.posebrowser.heelzcontrol");
                harmony.Patch(
                    hoverBody,
                    prefix: new HarmonyMethod(typeof(HeelzControlService), nameof(HoverBody_Prefix)));

                _heelzDetected = true;
                SandboxServices.Log.LogInfo("HeelzControl: HS2Heelz detected — HoverBody patched.");
            }
            catch (Exception ex)
            {
                _heelzDetected = false;
                SandboxServices.Log.LogWarning($"HeelzControl: Failed to patch HS2Heelz: {ex.Message}");
            }
        }

        private static void ApplyOverrideImmediate(ChaControl cha, HeelzOverride overrideValue)
        {
            if (!_heelzDetected || _controllerType == null || _controllerHandlerProp == null)
                return;

            try
            {
                var controller = cha.gameObject.GetComponent(_controllerType);
                if (controller == null) return;

                object? handler = _controllerHandlerProp.GetValue(controller);
                if (handler == null) return;

                MethodInfo? hoverBody = _handlerType?.GetMethod("HoverBody", BindingFlags.Public | BindingFlags.Instance);
                if (hoverBody == null) return;

                switch (overrideValue)
                {
                    case HeelzOverride.ForceOff:
                        hoverBody.Invoke(handler, new object[] { false });
                        break;
                    case HeelzOverride.ForceOn:
                        hoverBody.Invoke(handler, new object[] { true });
                        break;
                    case HeelzOverride.Default:
                        MethodInfo? updateStatus = _handlerType?.GetMethod("UpdateStatus", BindingFlags.Public | BindingFlags.Instance);
                        updateStatus?.Invoke(handler, null);
                        break;
                }
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"HeelzControl: ApplyOverrideImmediate failed: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Harmony prefix on HeelsHandler.HoverBody(bool hover)
        // ------------------------------------------------------------------

        private static bool HoverBody_Prefix(object __instance, ref bool hover)
        {
            try
            {
                ChaControl? cha = _chaControlProp?.GetValue(__instance) as ChaControl;
                if (cha == null) return true;

                if (!_overrides.TryGetValue(cha, out var overrideVal))
                    return true;

                switch (overrideVal)
                {
                    case HeelzOverride.ForceOff:
                        hover = false;
                        return true;
                    case HeelzOverride.ForceOn:
                        hover = true;
                        return true;
                    default:
                        return true;
                }
            }
            catch
            {
                return true;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static HashSet<string> ParseTagList(string? csv)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (StringEx.IsNullOrWhiteSpace(csv)) return set;
            foreach (string part in csv!.Split(','))
            {
                string tag = part.Trim();
                if (tag.Length > 0)
                    set.Add(tag);
            }
            return set;
        }

        private static void RebuildCachedLabels()
        {
            _cachedOffTagLabel = _heelsOffTags.Count > 0
                ? string.Join(" \u00b7 ", _heelsOffTags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray())
                : "";
            _cachedOnTagLabel = _heelsOnTags.Count > 0
                ? string.Join(" \u00b7 ", _heelsOnTags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToArray())
                : "";
        }

        private static Type? FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type? t = asm.GetType(fullName, throwOnError: false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
#endif

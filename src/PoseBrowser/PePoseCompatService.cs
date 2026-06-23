#if HS2 || AI || KK || KKS
using System;
using System.IO;
using System.Reflection;
using System.Xml;
#if HS2 || AI
using AIChara;
#endif
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExtensibleSaveFormat;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Embeds HS2PE/KKPE Advanced Mode breast/butt gravity and force into pose files via ExtendedSave.
    /// The pose editor plugin itself only hooks scene/card save.
    /// </summary>
    internal static class PePoseCompatService
    {
#if HS2
        internal const string PePluginGuid = "com.joan6694.illusionplugins.poseeditor";
        internal const string PeAssemblyName = "HS2PE";
        internal const string ExtSaveKey = "hs2pe";
        internal const string PluginDisplayName = "HS2PE";
#elif AI
        internal const string PePluginGuid = "com.joan6694.illusionplugins.poseeditor";
        internal const string PeAssemblyName = "AIPE";
        internal const string ExtSaveKey = "aipe";
        internal const string PluginDisplayName = "AIPE";
#elif KK || KKS
        internal const string PePluginGuid = "com.joan6694.kkplugins.kkpe";
        internal const string PeAssemblyName = "KKPE";
        internal const string ExtSaveKey = "kkpe";
        internal const string PluginDisplayName = "KKPE";
#endif

        private const string CharacterInfoDataKey = "characterInfo";

        private static bool _initialized;
        private static bool _detectionComplete;
        private static bool _peDetected;
        private static bool _poseEventsHooked;

        private static Type _charaPoseControllerType;
        private static Type _poseControllerType;
        private static MethodInfo _saveXmlMethod;
        private static MethodInfo _loadXmlMethod;
        private static MethodInfo _scheduleLoadMethod;
        private static FieldInfo _mainWindowSelfField;
        private static string _peVersion = "2.21.4";

        public static ConfigEntry<bool> IncludeBreastButtInPoses;

        public static bool IsPeDetected
        {
            get
            {
                EnsureDetected();
                return _peDetected;
            }
        }

        public static void Initialize(ConfigFile cfg)
        {
            if (_initialized) return;
            _initialized = true;

            IncludeBreastButtInPoses = cfg.Bind(
                "Pose Browser · Plugin compatibility",
                PluginDisplayName + " — save breast/butt gravity & force in poses",
                true,
                new ConfigDescription(
                    "When " + PluginDisplayName + " is installed, embed Advanced Mode breast and butt gravity/force " +
                    "into pose files on save/update (ExtendedSave block in the pose file). " +
                    "Editable in Pose Browser → Options → Plugin compatibility."));

            EnsureDetected();
        }

        internal static void EnsureDetected()
        {
            if (_detectionComplete && _peDetected)
                return;

            TryDetectPe();
            if (_peDetected)
                _detectionComplete = true;
        }

        private static void TryDetectPe()
        {
            if (_peDetected)
                return;

            try
            {
                Assembly peAssembly = null;
                if (!TryResolveFromChainloader(out peAssembly))
                {
                    _poseControllerType = FindType("HSPE.PoseController");
                    _charaPoseControllerType = FindType("HSPE.CharaPoseController");
                    if (_poseControllerType != null)
                        peAssembly = _poseControllerType.Assembly;
                }
                else
                {
                    if (_poseControllerType == null)
                        _poseControllerType = peAssembly.GetType("HSPE.PoseController", false)
                            ?? FindType("HSPE.PoseController");
                    if (_charaPoseControllerType == null)
                        _charaPoseControllerType = peAssembly.GetType("HSPE.CharaPoseController", false)
                            ?? FindType("HSPE.CharaPoseController");
                }

                if (_poseControllerType == null || _charaPoseControllerType == null)
                    return;

                _saveXmlMethod = _poseControllerType.GetMethod(
                    "SaveXml", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _loadXmlMethod = _poseControllerType.GetMethod(
                    "LoadXml", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _scheduleLoadMethod = _poseControllerType.GetMethod(
                    "ScheduleLoad", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                Type mainWindowType = peAssembly != null
                    ? peAssembly.GetType("HSPE.MainWindow", false)
                    : null;
                if (mainWindowType == null)
                    mainWindowType = FindType("HSPE.MainWindow");
                if (mainWindowType != null)
                {
                    _mainWindowSelfField = mainWindowType.GetField(
                        "_self", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                }

                if (_saveXmlMethod == null || _loadXmlMethod == null)
                    return;

                _peDetected = true;
                TryHookPoseEvents();
                SandboxServices.Log.LogInfo(
                    "PoseBrowser: " + PluginDisplayName + " detected — breast/butt pose embedding enabled.");
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("PoseBrowser: " + PluginDisplayName + " detection failed: " + ex.Message);
            }
        }

        private static bool TryResolveFromChainloader(out Assembly peAssembly)
        {
            peAssembly = null;
            if (Chainloader.PluginInfos == null || Chainloader.PluginInfos.Count == 0)
                return false;

            foreach (var entry in Chainloader.PluginInfos)
            {
                if (!string.Equals(entry.Key, PePluginGuid, StringComparison.Ordinal) &&
                    !string.Equals(entry.Value.Metadata.GUID, PePluginGuid, StringComparison.Ordinal))
                    continue;

                if (entry.Value.Metadata.Version != null)
                    _peVersion = entry.Value.Metadata.Version.ToString();

                if (entry.Value.Instance != null)
                    peAssembly = entry.Value.Instance.GetType().Assembly;

                if (peAssembly == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (string.Equals(asm.GetName().Name, PeAssemblyName, StringComparison.OrdinalIgnoreCase))
                        {
                            peAssembly = asm;
                            break;
                        }
                    }
                }
                return peAssembly != null;
            }

            return false;
        }

        private static void TryHookPoseEvents()
        {
            if (_poseEventsHooked || !_peDetected)
                return;

            ExtendedSave.PoseBeingSaved += OnPoseBeingSaved;
            ExtendedSave.PoseBeingLoaded += OnPoseBeingLoaded;
            _poseEventsHooked = true;
        }

        private static bool IncludeInPosesEnabled()
        {
            return IncludeBreastButtInPoses != null && IncludeBreastButtInPoses.Value;
        }

        private static void OnPoseBeingSaved(string poseName, PauseCtrl.FileInfo fileInfo, OCIChar ociChar, ExtendedSave.GameNames gameName)
        {
            EnsureDetected();
            if (!_peDetected || !IncludeInPosesEnabled())
                return;
            if (ociChar == null || ociChar.oiCharInfo.sex != 1)
                return;

            try
            {
                string xml;
                if (!TryBuildCharacterInfoXml(ociChar, out xml) || string.IsNullOrEmpty(xml))
                    return;

                var pluginData = new PluginData { version = 0 };
                pluginData.data[CharacterInfoDataKey] = xml;
                ExtendedSave.SetPoseExtendedDataById(ExtSaveKey, pluginData);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("PoseBrowser: " + PluginDisplayName + " pose save failed: " + ex.Message);
            }
        }

        private static void OnPoseBeingLoaded(string poseName, PauseCtrl.FileInfo fileInfo, OCIChar ociChar, ExtendedSave.GameNames gameName)
        {
            EnsureDetected();
            if (!_peDetected || !IncludeInPosesEnabled())
                return;
            if (ociChar == null || ociChar.oiCharInfo.sex != 1)
                return;

            try
            {
                PluginData pluginData = ExtendedSave.GetPoseExtendedDataById(ExtSaveKey);
                if (pluginData == null || pluginData.data == null)
                    return;

                object rawXml;
                if (!pluginData.data.TryGetValue(CharacterInfoDataKey, out rawXml))
                    return;

                string xml = rawXml as string;
                if (string.IsNullOrEmpty(xml))
                    return;

                var doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNode node = doc.DocumentElement;
                if (node == null) return;

                object controller = GetOrCreatePoseController(ociChar);
                if (controller == null) return;

                // Mirror HS2PE/KKPE's own MainWindow.LoadElement: the Advanced Mode
                // controller must be enabled for LoadXml'd values to take effect. The
                // boobs/dynamic-bone values are applied in PoseController.LateUpdate,
                // a Unity message that only runs while the controller Behaviour is
                // enabled — and the pose editor leaves controllers disabled until the
                // user activates Advanced Mode. Without re-enabling it here, embedded
                // data only applied to characters the user had manually activated.
                bool controllerEnabled = true;
                if (node.Attributes != null && node.Attributes["enabled"] != null)
                    controllerEnabled = XmlConvert.ToBoolean(node.Attributes["enabled"].Value);

                Action<bool> onLoadEnd = _ => SetControllerEnabled(controller, controllerEnabled);

                if (TryScheduleLoad(controller, node, onLoadEnd))
                    return;

                _loadXmlMethod.Invoke(controller, new object[] { node });
                SetControllerEnabled(controller, controllerEnabled);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning("PoseBrowser: " + PluginDisplayName + " pose load failed: " + ex.Message);
            }
        }

        private static bool TryBuildCharacterInfoXml(OCIChar ociChar, out string xml)
        {
            xml = null;
            object controller = GetPoseController(ociChar);
            if (controller == null || _saveXmlMethod == null) return false;

            using (var sw = new StringWriter())
            using (var xw = new XmlTextWriter(sw))
            {
                xw.WriteStartElement("characterInfo");
                xw.WriteAttributeString("version", _peVersion);
                try
                {
#if HS2 || AI
                    xw.WriteAttributeString("name", ((ChaFile)ociChar.charInfo.chaFile).parameter.fullname);
#else
                    xw.WriteAttributeString("name", ociChar.charInfo.chaFile.parameter.fullname);
#endif
                }
                catch
                {
                    xw.WriteAttributeString("name", "");
                }

                var behaviour = controller as Behaviour;
                if (behaviour != null)
                    xw.WriteAttributeString("enabled", XmlConvert.ToString(behaviour.enabled));

                _saveXmlMethod.Invoke(controller, new object[] { xw });
                xw.WriteEndElement();

                xml = sw.ToString();
            }

            return ContainsBreastButtPayload(xml);
        }

        private static bool ContainsBreastButtPayload(string xml)
        {
            return xml.IndexOf("gravityX", StringComparison.Ordinal) >= 0
                || xml.IndexOf("forceX", StringComparison.Ordinal) >= 0
                || xml.IndexOf("<left", StringComparison.Ordinal) >= 0
                || xml.IndexOf("<right", StringComparison.Ordinal) >= 0
                || xml.IndexOf("leftButt", StringComparison.Ordinal) >= 0
                || xml.IndexOf("rightButt", StringComparison.Ordinal) >= 0;
        }

        private static object GetPoseController(OCIChar ociChar)
        {
            if (_poseControllerType == null || ociChar == null || ociChar.guideObject == null ||
                ociChar.guideObject.transformTarget == null)
                return null;
            return ociChar.guideObject.transformTarget.GetComponent(_poseControllerType);
        }

        private static object GetOrCreatePoseController(OCIChar ociChar)
        {
            object controller = GetPoseController(ociChar);
            if (controller != null || _charaPoseControllerType == null || ociChar == null ||
                ociChar.guideObject == null || ociChar.guideObject.transformTarget == null)
                return controller;

            return ociChar.guideObject.transformTarget.gameObject.AddComponent(_charaPoseControllerType);
        }

        private static bool TryScheduleLoad(object controller, XmlNode node, Action<bool> onLoadEnd)
        {
            if (_scheduleLoadMethod == null || _mainWindowSelfField == null)
                return false;
            if (_mainWindowSelfField.GetValue(null) == null)
                return false;

            // ScheduleLoad(XmlNode, Action<bool> onLoadEnd) runs LoadXml on a coroutine
            // owned by MainWindow (so it works even on a disabled controller), then
            // invokes onLoadEnd — where we enable the controller, just like HS2PE does.
            _scheduleLoadMethod.Invoke(controller, new object[] { node, onLoadEnd });
            return true;
        }

        private static void SetControllerEnabled(object controller, bool enabled)
        {
            var behaviour = controller as Behaviour;
            if (behaviour != null)
                behaviour.enabled = enabled;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch
                {
                    // ignore
                }
            }
            return null;
        }
    }
}
#endif

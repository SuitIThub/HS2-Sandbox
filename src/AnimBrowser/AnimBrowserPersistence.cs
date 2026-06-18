using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal sealed class AnimBrowserPersistedOptions
    {
        public int optionsVersion = AnimBrowserConfig.OptionsJsonVersion;
        public float windowX;
        public float windowY;
        public float windowW = 920f;
        public float windowH = 560f;
        public float gridWindowX;
        public float gridWindowY;
        public float gridWindowW;
        public float gridWindowH;
        public float listWindowX;
        public float listWindowY;
        public float listWindowW;
        public float listWindowH;
        public float treePanelWidth = 308f;
        public int viewMode;
        public bool showControlsPane = true;
        public bool showCharacterConfigPane;
        public bool showOptionsPane;
        public float cardCellSize = 120f;
        public float controlsPaneWidth = 280f;
        public float characterConfigPaneWidth = 300f;
        public float optionsPaneWidth = 260f;
        public bool controlsGroupByProximity = true;
        public bool hideNonStudioCatalogAnimations = true;
        public bool controlsPreferUndocked;
        public float controlsFloatingX;
        public float controlsFloatingY;
        public float controlsFloatingW;
        public float controlsFloatingH;
    }

    internal static class AnimBrowserPersistence
    {
        private static string PersistedOptionsPath =>
            PathEx.Combine(Paths.ConfigPath, "com.hs2.sandbox", "anim_browser_options.json");

        public static void Load(AnimBrowserPersistedOptions target)
        {
            try
            {
                string path = PersistedOptionsPath;
                if (!File.Exists(path))
                    return;

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (!TryParse(json, target))
                    SandboxServices.Log.LogWarning("AnimBrowser: Could not parse anim_browser_options.json");
            }
            catch (System.Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimBrowser: Could not load anim_browser_options.json: " + ex.Message);
            }
        }

        public static void Save(AnimBrowserPersistedOptions source)
        {
            try
            {
                string path = PersistedOptionsPath;
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                FileEx.WriteAllTextAtomic(path, BuildJson(source), Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                SandboxServices.Log.LogWarning("AnimBrowser: Could not save anim_browser_options.json: " + ex.Message);
            }
        }

        private static string BuildJson(AnimBrowserPersistedOptions o)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"optionsVersion\":").Append(o.optionsVersion);
            sb.Append(",\"windowX\":").Append(o.windowX.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"windowY\":").Append(o.windowY.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"windowW\":").Append(o.windowW.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"windowH\":").Append(o.windowH.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"gridWindowX\":").Append(o.gridWindowX.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"gridWindowY\":").Append(o.gridWindowY.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"gridWindowW\":").Append(o.gridWindowW.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"gridWindowH\":").Append(o.gridWindowH.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"listWindowX\":").Append(o.listWindowX.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"listWindowY\":").Append(o.listWindowY.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"listWindowW\":").Append(o.listWindowW.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"listWindowH\":").Append(o.listWindowH.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"treePanelWidth\":").Append(o.treePanelWidth.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"viewMode\":").Append(o.viewMode);
            sb.Append(",\"showControlsPane\":").Append(o.showControlsPane ? "true" : "false");
            sb.Append(",\"showCharacterConfigPane\":").Append(o.showCharacterConfigPane ? "true" : "false");
            sb.Append(",\"showOptionsPane\":").Append(o.showOptionsPane ? "true" : "false");
            sb.Append(",\"cardCellSize\":").Append(o.cardCellSize.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"controlsPaneWidth\":").Append(o.controlsPaneWidth.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"characterConfigPaneWidth\":").Append(o.characterConfigPaneWidth.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"optionsPaneWidth\":").Append(o.optionsPaneWidth.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"controlsGroupByProximity\":").Append(o.controlsGroupByProximity ? "true" : "false");
            sb.Append(",\"hideNonStudioCatalogAnimations\":").Append(o.hideNonStudioCatalogAnimations ? "true" : "false");
            sb.Append(",\"controlsPreferUndocked\":").Append(o.controlsPreferUndocked ? "true" : "false");
            sb.Append(",\"controlsFloatingX\":").Append(o.controlsFloatingX.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"controlsFloatingY\":").Append(o.controlsFloatingY.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"controlsFloatingW\":").Append(o.controlsFloatingW.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"controlsFloatingH\":").Append(o.controlsFloatingH.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        private static bool TryParse(string json, AnimBrowserPersistedOptions o)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            o.optionsVersion = ReadInt(json, "optionsVersion", o.optionsVersion);
            o.windowX = ReadFloat(json, "windowX", o.windowX);
            o.windowY = ReadFloat(json, "windowY", o.windowY);
            o.windowW = ReadFloat(json, "windowW", o.windowW);
            o.windowH = ReadFloat(json, "windowH", o.windowH);
            o.gridWindowX = ReadFloat(json, "gridWindowX", o.gridWindowX);
            o.gridWindowY = ReadFloat(json, "gridWindowY", o.gridWindowY);
            o.gridWindowW = ReadFloat(json, "gridWindowW", o.gridWindowW);
            o.gridWindowH = ReadFloat(json, "gridWindowH", o.gridWindowH);
            o.listWindowX = ReadFloat(json, "listWindowX", o.listWindowX);
            o.listWindowY = ReadFloat(json, "listWindowY", o.listWindowY);
            o.listWindowW = ReadFloat(json, "listWindowW", o.listWindowW);
            o.listWindowH = ReadFloat(json, "listWindowH", o.listWindowH);
            o.treePanelWidth = ReadFloat(json, "treePanelWidth", o.treePanelWidth);
            o.viewMode = ReadInt(json, "viewMode", o.viewMode);
            o.showControlsPane = ReadBool(json, "showControlsPane", o.showControlsPane);
            o.showCharacterConfigPane = ReadBool(json, "showCharacterConfigPane", o.showCharacterConfigPane);
            o.showOptionsPane = ReadBool(json, "showOptionsPane", o.showOptionsPane);
            o.cardCellSize = ReadFloat(json, "cardCellSize", o.cardCellSize);
            o.controlsPaneWidth = ReadFloat(json, "controlsPaneWidth", o.controlsPaneWidth);
            o.characterConfigPaneWidth = ReadFloat(json, "characterConfigPaneWidth", o.characterConfigPaneWidth);
            o.optionsPaneWidth = ReadFloat(json, "optionsPaneWidth", o.optionsPaneWidth);
            o.controlsGroupByProximity = ReadBool(json, "controlsGroupByProximity", o.controlsGroupByProximity);
            o.hideNonStudioCatalogAnimations = ReadBool(json, "hideNonStudioCatalogAnimations", true);
            o.controlsPreferUndocked = ReadBool(json, "controlsPreferUndocked", o.controlsPreferUndocked);
            o.controlsFloatingX = ReadFloat(json, "controlsFloatingX", o.controlsFloatingX);
            o.controlsFloatingY = ReadFloat(json, "controlsFloatingY", o.controlsFloatingY);
            o.controlsFloatingW = ReadFloat(json, "controlsFloatingW", o.controlsFloatingW);
            o.controlsFloatingH = ReadFloat(json, "controlsFloatingH", o.controlsFloatingH);
            MigrateLegacyWindowRects(o);
            return true;
        }

        private static void MigrateLegacyWindowRects(AnimBrowserPersistedOptions o)
        {
            bool hasGrid = o.gridWindowW > 10f && o.gridWindowH > 10f;
            bool hasList = o.listWindowW > 10f && o.listWindowH > 10f;
            bool hasLegacy = o.windowW > 10f && o.windowH > 10f;
            if (!hasGrid && hasLegacy)
            {
                o.gridWindowX = o.windowX;
                o.gridWindowY = o.windowY;
                o.gridWindowW = o.windowW;
                o.gridWindowH = o.windowH;
            }

            if (!hasList && hasLegacy)
            {
                o.listWindowX = o.windowX;
                o.listWindowY = o.windowY;
                o.listWindowW = o.windowW;
                o.listWindowH = o.windowH;
            }
        }

        private static int ReadInt(string json, string key, int fallback)
        {
            float f = ReadFloat(json, key, fallback);
            return Mathf.RoundToInt(f);
        }

        private static float ReadFloat(string json, string key, float fallback)
        {
            string token = "\"" + key + "\":";
            int idx = json.IndexOf(token, System.StringComparison.Ordinal);
            if (idx < 0)
                return fallback;
            idx += token.Length;
            int end = idx;
            while (end < json.Length && "0123456789.-eE+".IndexOf(json[end]) >= 0)
                end++;
            if (end <= idx)
                return fallback;
            if (float.TryParse(json.Substring(idx, end - idx), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return value;
            return fallback;
        }

        private static bool ReadBool(string json, string key, bool fallback)
        {
            string token = "\"" + key + "\":";
            int idx = json.IndexOf(token, System.StringComparison.Ordinal);
            if (idx < 0)
                return fallback;
            idx += token.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
                idx++;
            if (json.IndexOf("true", idx, System.StringComparison.Ordinal) == idx)
                return true;
            if (json.IndexOf("false", idx, System.StringComparison.Ordinal) == idx)
                return false;
            return fallback;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using AIChara;
using BepInEx;
using KKAPI.Studio;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Loads a coordinate / clothing card (.png) from UserData onto the workspace-selected character(s).
    /// Game folders are <c>UserData/coordinate/female</c> and <c>.../male</c> (not "coordinates").
    /// </summary>
    public class LoadCoordinateCardCommand : TimelineCommand
    {
        public override string TypeId => "load_coordinate_card";
        public override string GetDisplayLabel() => "Load coord";

        private string _coordinatePath = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Coord PNG", GUILayout.Width(52));
            _coordinatePath = GUILayout.TextField(_coordinatePath ?? "", GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Browse…", GUILayout.Width(58)))
            {
                string coordRoot = Path.Combine(Paths.GameRootPath, "UserData", "coordinate");
                if (!Directory.Exists(coordRoot))
                    coordRoot = Path.Combine(Paths.GameRootPath, "UserData");
                string? picked = NativeFileDialog.OpenFile(
                    "Coordinate card (.png)",
                    "PNG images (*.png)\0*.png\0All files (*.*)\0*.*\0",
                    coordRoot);
                if (picked is { Length: > 0 })
                    _coordinatePath = picked;
            }
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolved = ctx.Variables.Interpolate(_coordinatePath ?? "");
            string fullPath = ResolveCoordinatePath(resolved);

            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                SandboxServices.Log.LogWarning($"LoadCoordinateCard: file not found: '{resolved}'");
                onComplete();
                return;
            }

            if (!StudioAPI.StudioLoaded)
            {
                SandboxServices.Log.LogWarning("LoadCoordinateCard: Studio is not loaded yet.");
                onComplete();
                return;
            }

            IEnumerable<OCIChar>? selected;
            try
            {
                selected = StudioAPI.GetSelectedCharacters();
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"LoadCoordinateCard: GetSelectedCharacters failed. {ex.Message}");
                onComplete();
                return;
            }

            if (selected == null)
            {
                SandboxServices.Log.LogWarning("LoadCoordinateCard: no character selected in the workspace.");
                onComplete();
                return;
            }

            bool any = false;
            foreach (OCIChar oci in selected)
            {
                if (oci != null) { any = true; break; }
            }
            if (!any)
            {
                SandboxServices.Log.LogWarning("LoadCoordinateCard: no character selected in the workspace.");
                onComplete();
                return;
            }

            ChaFileCoordinate coord = new ChaFileCoordinate();
            bool loaded;
            try
            {
                using (FileStream fs = File.OpenRead(fullPath))
                    loaded = coord.LoadFile(fs, 0);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"LoadCoordinateCard: failed to read coordinate file. {ex.Message}");
                onComplete();
                return;
            }

            if (!loaded)
            {
                SandboxServices.Log.LogWarning($"LoadCoordinateCard: coordinate file could not be parsed: '{fullPath}'");
                onComplete();
                return;
            }

            fullPath = Path.GetFullPath(fullPath);
            foreach (OCIChar oci in selected)
            {
                if (oci == null) continue;
                ChaControl? cha = oci.charInfo;
                if (cha == null) continue;
                try
                {
                    cha.ChangeNowCoordinate(coord, true);
                }
                catch (Exception ex)
                {
                    SandboxServices.Log.LogWarning($"LoadCoordinateCard: ChangeNowCoordinate failed. {ex.Message}");
                }
            }

            onComplete();
        }

        /// <summary>
        /// Absolute path, or relative under &lt;GameRoot&gt;/UserData (forward slashes ok).
        /// </summary>
        private static string ResolveCoordinatePath(string raw)
        {
            string t = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(t)) return "";

            if (Path.IsPathRooted(t) && File.Exists(t))
                return Path.GetFullPath(t);

            string userData = Path.Combine(Paths.GameRootPath, "UserData");
            string combined = Path.GetFullPath(Path.Combine(userData, t.TrimStart('/', '\\')));
            if (File.Exists(combined))
                return combined;

            if (File.Exists(t))
                return Path.GetFullPath(t);

            return combined;
        }

        public override string SerializePayload() => _coordinatePath ?? "";

        public override void DeserializePayload(string payload)
        {
            _coordinatePath = payload ?? "";
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (string.IsNullOrWhiteSpace(_coordinatePath)) return "Coordinate path is empty";
            if (vars != null && !vars.IsValidInterpolation(_coordinatePath ?? ""))
                return "Unknown variable in coordinate path";
            return null;
        }
    }
}

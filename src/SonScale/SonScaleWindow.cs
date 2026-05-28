using KKAPI.Studio;
using KKAPI.Utilities;
using Studio;
using UnityEngine;

namespace HS2SandboxPlugin
{
    public class SonScaleWindow : SubWindow
    {
        protected override void Start()
        {
            base.Start();
            windowID = 2012;
            windowTitle = "Son scale";
            windowRect = new Rect(420, 120, 320, 200);
        }

        protected override void DrawWindowContent(int windowID)
        {
            if (!StudioAPI.StudioLoaded)
            {
                GUILayout.Label(
                    "Open Studio and load a scene to use Son scale.",
                    GUILayout.MaxWidth(300f));
                FinishWindowChrome();
                return;
            }

            if (!HasStudioCharacterSelection())
            {
                GUILayout.Label(
                    "Select a character in the Studio workspace (tree or view) to adjust length and girth.",
                    GUILayout.MaxWidth(300f));
                FinishWindowChrome();
                return;
            }

            GUILayout.Label(
                "Manipulate → Chara → State → Son length: first row = overall shaft size (multiplies with Penis Length on BP rigs). " +
                "Penis Girth scales thickness. Balls scale targets bone cm_J_dan_f_top (uniform); when that bone is also the dan root, it multiplies the whole root scale. " +
                "With Studio Better Penetration and valid dan targets, girth is one dan-root XY multiply (uniform); length still uses BP base length. All use " +
                $"{SonScaleManipulateUi.MinMul:0.#}–{SonScaleManipulateUi.MaxMul:0.#}×.",
                GUILayout.MaxWidth(300f));

            GUILayout.Space(6f);

            SonScaleSettings.Enabled = GUILayout.Toggle(SonScaleSettings.Enabled, "Enable split scaling");

            GUILayout.Space(6f);

            if (GUILayout.Button("Reset all sliders to 1.0×"))
            {
                SonScaleSettings.Master = 1f;
                SonScaleSettings.Length = 1f;
                SonScaleSettings.Girth = 1f;
                SonScaleSettings.Balls = 1f;
                SonScaleManipulateUi.PushSettingsToSliders();
            }

            FinishWindowChrome();
        }

        /// <summary>Same pattern as <see cref="CopyScript"/> / <see cref="SubWindow3"/>.</summary>
        private void FinishWindowChrome()
        {
            GUI.DragWindow(new Rect(0, 0, windowRect.width, windowRect.height));
            IMGUIUtils.EatInputInRect(windowRect);
        }

        private static bool HasStudioCharacterSelection()
        {
            try
            {
                var sel = StudioAPI.GetSelectedCharacters();
                if (sel == null)
                    return false;

                foreach (OCIChar? oci in sel)
                {
                    if (oci != null)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}


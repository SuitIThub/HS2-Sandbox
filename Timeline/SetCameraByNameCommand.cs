using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace HS2SandboxPlugin
{
    /// <summary>
    /// Activates a Studio camera by name. Resolves the camera list from the dropdown at
    /// "StudioScene/Canvas System Menu/02_Camera/Image Camera Setting/Dropdown" (component index 3)
    /// each time the command executes, then fires onValueChanged.Invoke(index).
    /// Falls back to index 0 when the name is not found.
    /// </summary>
    public class SetCameraByNameCommand : TimelineCommand
    {
        private const string DropdownPath = "StudioScene/Canvas System Menu/02_Camera/Image Camera Setting/Dropdown";
        private const int DropdownComponentIndex = 3;

        public override string TypeId => "set_camera_by_name";

        private string _cameraName = "";

        public override string GetDisplayLabel() => "Set Camera";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label("Name", GUILayout.Width(40));
            _cameraName = GUILayout.TextField(_cameraName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string resolvedName = ctx.Variables.Interpolate(_cameraName ?? "");

            GameObject? dropdownGo = GameObject.Find(DropdownPath);
            if (dropdownGo == null)
            {
                HS2SandboxPlugin.Log.LogWarning($"SetCameraByName: GameObject not found at path '{DropdownPath}'.");
                onComplete();
                return;
            }

            Component[] components = dropdownGo.GetComponents<Component>();
            if (components.Length <= DropdownComponentIndex)
            {
                HS2SandboxPlugin.Log.LogWarning($"SetCameraByName: Component index {DropdownComponentIndex} out of range (found {components.Length} components).");
                onComplete();
                return;
            }

            Component dropdown = components[DropdownComponentIndex];
            Type dropdownType = dropdown.GetType();

            int index = ResolveIndex(dropdown, dropdownType, resolvedName);

            PropertyInfo? onValueChangedProp = dropdownType.GetProperty("onValueChanged",
                BindingFlags.Public | BindingFlags.Instance);
            object? onValueChangedObj = onValueChangedProp?.GetValue(dropdown);

            if (onValueChangedObj == null)
            {
                HS2SandboxPlugin.Log.LogWarning("SetCameraByName: 'onValueChanged' property not found on dropdown component.");
                onComplete();
                return;
            }

            MethodInfo? invokeMethod = onValueChangedObj.GetType().GetMethod("Invoke", [typeof(int)]);
            if (invokeMethod == null)
            {
                HS2SandboxPlugin.Log.LogWarning("SetCameraByName: 'Invoke(int)' method not found on onValueChanged.");
                onComplete();
                return;
            }

            try
            {
                invokeMethod.Invoke(onValueChangedObj, [index]);
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogWarning($"SetCameraByName: Invoke failed. {ex.Message}");
            }

            onComplete();
        }

        private static int ResolveIndex(Component dropdown, Type dropdownType, string cameraName)
        {
            // Try property first, fall back to field
            object? optionsObj = dropdownType.GetProperty("options", BindingFlags.Public | BindingFlags.Instance)?.GetValue(dropdown)
                              ?? dropdownType.GetField("options", BindingFlags.Public | BindingFlags.Instance)?.GetValue(dropdown);

            if (optionsObj is not IList options)
            {
                HS2SandboxPlugin.Log.LogWarning("SetCameraByName: Could not retrieve options list from dropdown.");
                return 0;
            }

            int match = -1;

            for (int i = 0; i < options.Count; i++)
            {
                object? option = options[i];
                if (option == null) { continue; }

                Type optType = option.GetType();
                string? text = optType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance)?.GetValue(option) as string
                            ?? optType.GetField("text", BindingFlags.Public | BindingFlags.Instance)?.GetValue(option) as string;

                if (match < 0 && string.Equals(text, cameraName, StringComparison.Ordinal))
                    match = i;
            }

            return match >= 0 ? match : 0;
        }

        public override string SerializePayload() => _cameraName ?? "";

        public override void DeserializePayload(string payload)
        {
            _cameraName = payload ?? "";
        }
    }
}

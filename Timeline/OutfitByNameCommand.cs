using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Sets an outfit by name by calling FashionLineController.WearFashionByName(name, isFile, reload)
    /// from the Fashion Line plugin (prolo.fashionline).
    /// </summary>
    public class OutfitByNameCommand : TimelineCommand
    {
        private const string ControllerTypeName = "FashionLineController";
        private const string MethodName = "WearFashionByName";
        private const char PayloadSeparator = '\u0001';

        public override string TypeId => "outfit_by_name";
        public override string GetDisplayLabel() => "Outfit by name";

        private string _name = "";
        private bool _isFile = true;
        private bool _reload = true;

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(40));
            _name = GUILayout.TextField(_name ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            object? controller = GetFashionLineController();
            if (controller == null)
            {
                SandboxServices.Log.LogWarning("Fashion Line plugin not found or FashionLineController not in scene. Install/enable prolo.fashionline.");
                onComplete();
                return;
            }
            MethodInfo? method = controller.GetType().GetMethod(MethodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(bool), typeof(bool) },
                null);
            if (method == null)
            {
                SandboxServices.Log.LogWarning($"FashionLineController.{MethodName} not found.");
                onComplete();
                return;
            }
            string nameToUse = ctx.Variables.Interpolate(_name ?? "");
            try
            {
                method.Invoke(controller, new object[] { nameToUse, _isFile, _reload });
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"Outfit by name ({nameToUse}): {ex.Message}");
            }
            onComplete();
        }

        private static object? GetFashionLineController()
        {
            Type? controllerType = FindFashionLineControllerType();
            if (controllerType == null) return null;
            MethodInfo? findMethod = typeof(UnityEngine.Object)
                .GetMethod("FindObjectOfType", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type) }, null);
            if (findMethod == null) return null;
            return findMethod.Invoke(null, new object?[] { controllerType });
        }

        private static Type? FindFashionLineControllerType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type? t = asm.GetTypes().FirstOrDefault(x =>
                        x.Name == ControllerTypeName &&
                        x.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(bool), typeof(bool) }, null) != null);
                    if (t != null) return t;
                }
                catch (ReflectionTypeLoadException) { }
            }
            return null;
        }

        public override string SerializePayload()
        {
            return (_name ?? "").Replace(PayloadSeparator.ToString(), "") + PayloadSeparator + (_isFile ? "1" : "0") + PayloadSeparator + (_reload ? "1" : "0");
        }

        public override void DeserializePayload(string payload)
        {
            _name = "";
            _isFile = true;
            _reload = true;
            if (string.IsNullOrEmpty(payload)) return;
            string[] parts = payload.Split(PayloadSeparator);
            if (parts.Length >= 1) _name = parts[0];
            if (parts.Length >= 2) _isFile = parts[1] == "1";
            if (parts.Length >= 3) _reload = parts[2] == "1";
        }

        public override string? GetValidationError(TimelineVariableStore? vars)
        {
            if (vars != null && !vars.IsValidInterpolation(_name ?? ""))
                return "Unknown variable in name";
            return null;
        }
    }
}

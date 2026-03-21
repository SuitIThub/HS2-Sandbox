using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Calls FashionLineController.GetOutfitNames() from the Fashion Line plugin (prolo.fashionline)
    /// and stores the returned list in a list variable.
    /// </summary>
    public class GetFashionCommand : TimelineCommand
    {
        private const string ControllerTypeName = "FashionLineController";
        private const string MethodName = "GetOutfitNames";

        public override string TypeId => "get_fashion";
        public override string GetDisplayLabel() => "Get Fashion";

        private string _variableName = "";

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.Label("Store in", GUILayout.Width(52));
            _variableName = GUILayout.TextField(_variableName ?? "", GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
        }

        public override void Execute(TimelineContext ctx, Action onComplete)
        {
            string targetVar = (_variableName ?? "").Trim();
            if (string.IsNullOrEmpty(targetVar))
            {
                HS2SandboxPlugin.Log.LogWarning("GetFashion: variable name is empty.");
                onComplete();
                return;
            }

            object? controller = GetFashionLineController();
            if (controller == null)
            {
                HS2SandboxPlugin.Log.LogWarning("GetFashion: FashionLine plugin not found or FashionLineController not in scene. Install/enable prolo.fashionline.");
                onComplete();
                return;
            }

            MethodInfo? method = controller.GetType().GetMethod(MethodName,
                BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null)
            {
                HS2SandboxPlugin.Log.LogWarning($"GetFashion: FashionLineController.{MethodName}() not found.");
                onComplete();
                return;
            }

            List<string>? names = null;
            try
            {
                names = method.Invoke(controller, null) as List<string>;
            }
            catch (Exception ex)
            {
                HS2SandboxPlugin.Log.LogWarning($"GetFashion: {MethodName}() threw: {ex.Message}");
            }

            ctx.Variables.SetList(targetVar, names ?? new List<string>());
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
                        x.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Instance) != null);
                    if (t != null) return t;
                }
                catch (ReflectionTypeLoadException) { }
            }
            return null;
        }

        public override string SerializePayload() => _variableName ?? "";

        public override void DeserializePayload(string payload)
        {
            _variableName = payload ?? "";
        }
    }
}

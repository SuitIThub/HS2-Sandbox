using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Rotates through outfits by calling FashionLineController.PrevInLine() or NextInLine()
    /// from the Fashion Line plugin (prolo.fashionline).
    /// </summary>
    public class OutfitRotateCommand : TimelineCommand
    {
        private const string ControllerTypeName = "FashionLineController";
        private const string MethodPrev = "PrevInLine";
        private const string MethodNext = "NextInLine";

        public override string TypeId => "outfit_rotate";
        public override string GetDisplayLabel() => _usePrev ? "Outfit Prev" : "Outfit Next";

        private bool _usePrev = true; // true = previous outfit, false = next outfit

        public override void DrawInlineConfig(InlineDrawContext ctx)
        {
            GUILayout.BeginHorizontal();
            bool prev = GUILayout.Toggle(_usePrev, "Previous outfit", GUILayout.ExpandWidth(false));
            bool next = GUILayout.Toggle(!_usePrev, "Next outfit", GUILayout.ExpandWidth(false));
            if (prev && next)
                _usePrev = !_usePrev;
            else if (prev)
                _usePrev = true;
            else if (next)
                _usePrev = false;
            GUILayout.FlexibleSpace();
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
            string methodName = _usePrev ? MethodPrev : MethodNext;
            MethodInfo? method = controller.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null)
            {
                SandboxServices.Log.LogWarning($"FashionLineController.{methodName} not found.");
                onComplete();
                return;
            }
            try
            {
                method.Invoke(controller, null);
            }
            catch (Exception ex)
            {
                SandboxServices.Log.LogWarning($"Outfit rotate ({methodName}): {ex.Message}");
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
                        x.GetMethod(MethodPrev, BindingFlags.Public | BindingFlags.Instance) != null &&
                        x.GetMethod(MethodNext, BindingFlags.Public | BindingFlags.Instance) != null);
                    if (t != null) return t;
                }
                catch (ReflectionTypeLoadException) { }
            }
            return null;
        }

        public override string SerializePayload() => _usePrev ? "prev" : "next";

        public override void DeserializePayload(string payload)
        {
            _usePrev = !string.Equals(payload, "next", StringComparison.OrdinalIgnoreCase);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// Optional bridge to XUnity Auto Translator (reflection, no compile-time dependency).
    /// </summary>
    internal static class StudioAutoTranslation
    {
        private static bool _enabled = true;
        private static bool _scanComplete;
        private static object? _translator;
        private static MethodInfo? _tryTranslateMethod;
        private static MethodInfo? _translateAsyncMethod;
        private static Type? _translationResultType;
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);

        public static event Action? TranslationsUpdated;

        public static bool IsAvailable
        {
            get
            {
                EnsureResolved();
                return _translator != null;
            }
        }

        public static void SetEnabled(bool enabled) => _enabled = enabled;

        public static void RetryResolution()
        {
            if (_translator != null)
                return;
            _scanComplete = false;
            EnsureResolved();
        }

        public static string Resolve(string source)
        {
            if (!_enabled || string.IsNullOrEmpty(source))
                return source ?? string.Empty;

            EnsureResolved();
            if (_translator == null)
                return source;

            lock (_cache)
            {
                if (_cache.TryGetValue(source, out string cached))
                    return cached;
            }

            if (TryTranslate(source, out string sync) &&
                !string.IsNullOrEmpty(sync) &&
                !string.Equals(sync, source, StringComparison.Ordinal))
            {
                StoreTranslation(source, sync, notify: false);
                return sync;
            }

            bool queue;
            lock (_pending)
            {
                queue = _pending.Add(source);
            }

            if (queue)
                QueueTranslateAsync(source);

            return source;
        }

        /// <summary>Returns a cached translation when available; never triggers sync/async lookup.</summary>
        public static bool TryGetCached(string source, out string translated)
        {
            translated = source ?? string.Empty;
            if (!_enabled || string.IsNullOrEmpty(source))
                return false;

            lock (_cache)
            {
                if (_cache.TryGetValue(source, out string cached))
                {
                    translated = cached;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Queues async translation for strings that are not yet cached.</summary>
        public static void Prefetch(IEnumerable<string> sources)
        {
            if (!_enabled || sources == null)
                return;
            foreach (string source in sources)
            {
                if (string.IsNullOrEmpty(source))
                    continue;
                Resolve(source);
            }
        }

        public static void ClearCache()
        {
            lock (_cache)
                _cache.Clear();
            lock (_pending)
                _pending.Clear();
        }

        private static void EnsureResolved()
        {
            if (_translator != null || _scanComplete)
                return;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!TryResolveFromAssembly(asm))
                    continue;

                _scanComplete = true;
                return;
            }

            _scanComplete = true;
        }

        private static bool TryResolveFromAssembly(Assembly asm)
        {
            string? name = asm.GetName().Name;
            if (name == null ||
                name.IndexOf("XUnity.AutoTranslator.Plugin.Core", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (TryResolveTranslator(asm, "XUnity.AutoTranslator.Plugin.Core.AutoTranslator", "Default"))
                return true;

            Type? pluginType = asm.GetType("XUnity.AutoTranslator.Plugin.Core.AutoTranslationPlugin", false);
            if (pluginType == null)
                return false;

            FieldInfo? currentField = pluginType.GetField(
                "Current",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (currentField == null)
                return false;

            object? current = currentField.GetValue(null);
            if (current == null)
                return false;

            return BindTranslatorMethods(asm, current);
        }

        private static bool TryResolveTranslator(Assembly asm, string typeName, string propertyName)
        {
            Type? type = asm.GetType(typeName, false);
            if (type == null)
                return false;

            PropertyInfo? prop = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null)
                return false;

            object? current = prop.GetValue(null, null);
            if (current == null)
                return false;

            return BindTranslatorMethods(asm, current);
        }

        private static bool BindTranslatorMethods(Assembly asm, object current)
        {
            Type? iface = asm.GetType("XUnity.AutoTranslator.Plugin.Core.ITranslator", false);
            Type? resultType = asm.GetType("XUnity.AutoTranslator.Plugin.Core.TranslationResult", false);
            if (iface == null || resultType == null || !iface.IsInstanceOfType(current))
                return false;

            MethodInfo? tryTranslate = null;
            MethodInfo? translateAsync = null;
            foreach (MethodInfo method in iface.GetMethods())
            {
                if (method.Name == "TryTranslate")
                {
                    ParameterInfo[] ps = method.GetParameters();
                    if (ps.Length == 2 &&
                        ps[0].ParameterType == typeof(string) &&
                        ps[1].ParameterType.IsByRef)
                    {
                        tryTranslate = method;
                    }
                }
                else if (method.Name == "TranslateAsync")
                {
                    ParameterInfo[] ps = method.GetParameters();
                    if (ps.Length == 2 &&
                        ps[0].ParameterType == typeof(string) &&
                        ps[1].ParameterType.IsGenericType &&
                        ps[1].ParameterType.GetGenericTypeDefinition() == typeof(Action<>))
                    {
                        translateAsync = method;
                    }
                }
            }

            if (tryTranslate == null || translateAsync == null)
                return false;

            _translator = current;
            _tryTranslateMethod = tryTranslate;
            _translateAsyncMethod = translateAsync;
            _translationResultType = resultType;
            return true;
        }

        private static bool TryTranslate(string source, out string translated)
        {
            translated = source;
            if (_translator == null || _tryTranslateMethod == null)
                return false;

            object?[] args = { source, string.Empty };
            try
            {
                object? result = _tryTranslateMethod.Invoke(_translator, args);
                if (result is bool ok && ok && args[1] is string text && !string.IsNullOrEmpty(text))
                {
                    translated = text;
                    return true;
                }
            }
            catch
            {
                // XUnity not ready or call failed
            }

            return false;
        }

        private static void QueueTranslateAsync(string source)
        {
            if (_translator == null || _translateAsyncMethod == null || _translationResultType == null)
            {
                lock (_pending)
                    _pending.Remove(source);
                return;
            }

            try
            {
                MethodInfo? generic = typeof(StudioAutoTranslation).GetMethod(
                    nameof(InvokeTranslateAsync),
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (generic == null)
                    return;

                generic = generic.MakeGenericMethod(_translationResultType);
                generic.Invoke(null, new object[] { source });
            }
            catch
            {
                lock (_pending)
                    _pending.Remove(source);
            }
        }

        private static void InvokeTranslateAsync<T>(string source) where T : class
        {
            Action<T> callback = result => OnAsyncResult(source, result);
            _translateAsyncMethod!.Invoke(_translator, new object[] { source, callback });
        }

        private static void OnAsyncResult<T>(string source, T result) where T : class
        {
            lock (_pending)
                _pending.Remove(source);

            if (result == null)
                return;

            try
            {
                Type type = result.GetType();
                PropertyInfo? succeededProp = type.GetProperty("Succeeded");
                PropertyInfo? textProp = type.GetProperty("TranslatedText");
                if (succeededProp == null || textProp == null)
                    return;

                object? succeededObj = succeededProp.GetValue(result, null);
                if (!(succeededObj is bool succeeded) || !succeeded)
                    return;

                object? textObj = textProp.GetValue(result, null);
                if (!(textObj is string text) || string.IsNullOrEmpty(text))
                    return;

                StoreTranslation(source, text, notify: true);
            }
            catch
            {
                // ignored
            }
        }

        private static void StoreTranslation(string source, string translated, bool notify)
        {
            lock (_cache)
                _cache[source] = translated;

            if (notify)
                TranslationsUpdated?.Invoke();
        }
    }
}

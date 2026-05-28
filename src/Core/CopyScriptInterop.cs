using System;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class CopyScriptInterop
    {
        /// <summary>
        /// If the CopyScript window type is present (either in the all-in-one DLL or in the CopyScript module),
        /// calls its RefreshFromTimeline() method via reflection.
        /// </summary>
        public static void TryRefreshCopyScriptWindow()
        {
            try
            {
                var copyScriptType =
                    Type.GetType("HS2SandboxPlugin.CopyScript, HS2SandboxPlugin", throwOnError: false)
                    ?? Type.GetType("HS2SandboxPlugin.CopyScript, HS2Sandbox.CopyScript", throwOnError: false);

                if (copyScriptType == null)
                    return;

                var obj = UnityEngine.Object.FindObjectOfType(copyScriptType);
                if (obj == null)
                    return;

                var mi = copyScriptType.GetMethod("RefreshFromTimeline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                mi?.Invoke(obj, null);
            }
            catch
            {
                // best-effort; ignore
            }
        }
    }
}


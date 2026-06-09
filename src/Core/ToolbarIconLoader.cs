using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class ToolbarIconLoader
    {
        /// <summary>
        /// Loads a PNG for a toolbar icon: file next to the executing assembly first, then embedded resource <c>{AssemblyName}.{fileName}</c>.
        /// </summary>
        public static Texture2D LoadPng(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var dir = Path.GetDirectoryName(assembly.Location);

            // Prefer PNG next to the DLL (same deployment as copy/timeline icons); then embedded resource.
            if (!string.IsNullOrEmpty(dir))
            {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                {
                    try
                    {
                        var tex = CreateTexture(File.ReadAllBytes(path));
                        if (tex.width > 2 || tex.height > 2)
                            return tex;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[HS2 Sandbox] Toolbar icon file read failed ({path}): {ex.Message}");
                    }
                }
            }

            var embeddedName = $"{assembly.GetName().Name}.{fileName}";
            using (var stream = assembly.GetManifestResourceStream(embeddedName))
            {
                if (stream != null)
                {
                    var ms = new MemoryStream();
                    var buffer = new byte[4096];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        ms.Write(buffer, 0, read);
                    var tex = CreateTexture(ms.ToArray());
                    if (tex.width > 2 || tex.height > 2)
                        return tex;
                }
            }

            Debug.LogWarning($"[HS2 Sandbox] Toolbar icon not found or invalid (file next to DLL and embedded): {fileName}");
            return new Texture2D(32, 32);
        }

        private static Texture2D CreateTexture(byte[] data)
        {
            var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (data == null || data.Length == 0 || !tex.LoadImage(data))
                Debug.LogWarning("[HS2 Sandbox] Toolbar icon LoadImage failed or empty PNG bytes.");

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}

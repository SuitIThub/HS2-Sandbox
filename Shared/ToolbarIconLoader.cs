using System.IO;
using System.Reflection;
using UnityEngine;

namespace HS2SandboxPlugin
{
    internal static class ToolbarIconLoader
    {
        /// <summary>
        /// Loads a PNG for a toolbar icon: embedded resource <c>{AssemblyName}.{fileName}</c> first,
        /// then a file next to the executing assembly (for local builds with Content copy).
        /// </summary>
        public static Texture2D LoadPng(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var embeddedName = $"{assembly.GetName().Name}.{fileName}";
            using (var stream = assembly.GetManifestResourceStream(embeddedName))
            {
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return CreateTexture(ms.ToArray());
                }
            }

            var dir = Path.GetDirectoryName(assembly.Location);
            if (!string.IsNullOrEmpty(dir))
            {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return CreateTexture(File.ReadAllBytes(path));
            }

            Debug.LogWarning($"[HS2 Sandbox] Toolbar icon not found (embedded or next to DLL): {fileName}");
            return new Texture2D(32, 32);
        }

        private static Texture2D CreateTexture(byte[] data)
        {
            var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            tex.LoadImage(data);
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }
    }
}

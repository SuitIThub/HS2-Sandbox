using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HS2SandboxPlugin
{
    /// <summary>Invalid pose ZIP / pack data (.NET 3.5 has no <see cref="System.IO.InvalidDataException"/>).</summary>
    internal sealed class PackFormatException : Exception
    {
        public PackFormatException(string message) : base(message) { }
    }

    /// <summary>Regex flags safe for each game runtime (KK Mono rejects some .NET 4+ options).</summary>
    internal static class RegexEx
    {
#if KK
        public const RegexOptions DefaultOptions = RegexOptions.None;
#else
        public const RegexOptions DefaultOptions = RegexOptions.Compiled;
#endif

        public static Regex Create(string pattern) =>
#if KK
            new Regex(pattern);
#else
            new Regex(pattern, DefaultOptions);
#endif
    }

    /// <summary>Helpers for APIs missing in .NET 3.5 / Unity-Mono legacy targets.</summary>
    internal static class PathEx
    {
        public static string Combine(string path1, string path2, string path3) =>
            Path.Combine(Path.Combine(path1, path2), path3);
    }

    /// <summary>Atomic config writes with a File.Replace fallback for Mono / locked targets.</summary>
    internal static class FileEx
    {
        public static void WriteAllTextAtomic(string destinationPath, string contents, Encoding encoding)
        {
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tempPath = destinationPath + ".tmp";
            File.WriteAllText(tempPath, contents, encoding);
            CommitTempFile(tempPath, destinationPath);
        }

        public static void CommitTempFile(string tempPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                try
                {
                    File.Replace(tempPath, destinationPath, null);
                    return;
                }
                catch (IOException)
                {
                    // fall through to delete + move
                }
                catch (UnauthorizedAccessException)
                {
                    // fall through to delete + move
                }

                File.Delete(destinationPath);
            }

            File.Move(tempPath, destinationPath);
        }
    }

    internal static class StringEx
    {
        public static bool IsNullOrWhiteSpace(string? value)
        {
            if (value == null)
                return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            }

            return true;
        }
    }
}

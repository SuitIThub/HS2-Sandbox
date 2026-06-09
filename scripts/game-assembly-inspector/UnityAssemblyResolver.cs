using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GameAssemblyInspector
{
    internal sealed class UnityAssemblyResolver
    {
        private readonly string _unityLibDir;
        private readonly bool _verbose;
        private readonly Dictionary<string, Assembly> _loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        public UnityAssemblyResolver(string unityLibDir, bool verbose)
        {
            _unityLibDir = unityLibDir;
            _verbose = verbose;
        }

        public Assembly Resolve(object? sender, ResolveEventArgs args)
        {
            string requested = new AssemblyName(args.Name).Name ?? args.Name;
            if (_loaded.TryGetValue(requested, out var cached))
                return cached;

            // Search the unity lib folder and sibling package lib folders (KKS/HS2 split modules).
            var searchRoots = new List<string> { _unityLibDir };
            string? packagesRoot = FindAncestorPackagesRoot(_unityLibDir);
            if (packagesRoot != null)
            {
                foreach (string dir in Directory.GetDirectories(packagesRoot))
                {
                    if (dir.IndexOf("unityengine", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    foreach (string lib in Directory.GetDirectories(dir, "lib", SearchOption.AllDirectories))
                        searchRoots.Add(lib);
                }
            }

            foreach (string root in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root))
                    continue;

                string? path = Directory.GetFiles(root, requested + ".dll", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (path == null)
                    continue;

                if (_verbose)
                    Console.WriteLine("  resolve: " + requested + " <- " + path);

                var asm = Assembly.LoadFrom(path);
                _loaded[requested] = asm;
                return asm;
            }

            if (_verbose)
                Console.WriteLine("  resolve FAILED: " + requested);

            throw new FileNotFoundException("Could not resolve assembly: " + requested);
        }

        private static string? FindAncestorPackagesRoot(string unityLibDir)
        {
            var dir = new DirectoryInfo(unityLibDir);
            while (dir != null)
            {
                if (string.Equals(dir.Name, "packages", StringComparison.OrdinalIgnoreCase))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }
    }
}

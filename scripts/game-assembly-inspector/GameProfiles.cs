using System;
using System.IO;
using System.Linq;

namespace GameAssemblyInspector
{
    internal sealed class GameProfile
    {
        public string Name { get; }
        public string AssemblyPath { get; }
        public string UnityLibDir { get; }

        public GameProfile(string name, string assemblyPath, string unityLibDir)
        {
            Name = name;
            AssemblyPath = assemblyPath;
            UnityLibDir = unityLibDir;
        }
    }

    internal static class GameProfiles
    {
        public static GameProfile Resolve(string game)
        {
            string nugetRoot = GetNuGetPackagesRoot();
            switch (game.ToUpperInvariant())
            {
                case "KK":
                    return Build(
                        "KK",
                        nugetRoot,
                        "illusionlibs.koikatu.assembly-csharp",
                        new[] { "2019.4.27.4", "2019.4.27" },
                        "illusionlibs.koikatu.unityengine",
                        new[] { "5.6.2.4", "5.6.2" });

                case "KKS":
                    return Build(
                        "KKS",
                        nugetRoot,
                        "illusionlibs.koikatsusunshine.assembly-csharp",
                        new[] { "2021.9.17" },
                        "illusionlibs.koikatsusunshine.unityengine.coremodule",
                        new[] { "2019.4.9" });

                case "HS2":
                    return Build(
                        "HS2",
                        nugetRoot,
                        "illusionlibs.honeyselect2.assembly-csharp",
                        new[] { "2020.5.29.5", "2020.5.29.4", "2020.5.29" },
                        "illusionlibs.honeyselect2.unityengine.coremodule",
                        new[] { "2018.4.30", "2018.4.11.4" });

                default:
                    throw new ArgumentException("Unknown game '" + game + "'. Use HS2, KK, or KKS.");
            }
        }

        private static GameProfile Build(
            string name,
            string nugetRoot,
            string asmPackage,
            string[] asmVersions,
            string unityPackage,
            string[] unityVersions)
        {
            string asmDll = FindDll(nugetRoot, asmPackage, asmVersions, "Assembly-CSharp.dll");
            string unityDir = FindLibDir(nugetRoot, unityPackage, unityVersions);
            return new GameProfile(name, asmDll, unityDir);
        }

        private static string GetNuGetPackagesRoot()
        {
            string? global = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (!string.IsNullOrEmpty(global) && Directory.Exists(global))
                return global;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string fallback = Path.Combine(userProfile, ".nuget", "packages");
            if (!Directory.Exists(fallback))
                throw new DirectoryNotFoundException(
                    "NuGet package cache not found. Restore packages first (dotnet build) or set NUGET_PACKAGES.");

            return fallback;
        }

        private static string FindLibDir(string nugetRoot, string packageId, string[] versions)
        {
            string packageDir = Path.Combine(nugetRoot, packageId.ToLowerInvariant());
            if (!Directory.Exists(packageDir))
                throw new DirectoryNotFoundException("NuGet package not found: " + packageId + " under " + nugetRoot);

            foreach (string version in versions)
            {
                string versionDir = Path.Combine(packageDir, version);
                if (!Directory.Exists(versionDir))
                    continue;

                string? libDir = Directory.GetDirectories(versionDir, "lib", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (libDir != null && Directory.Exists(libDir))
                    return libDir;
            }

            // Fall back to newest version folder that contains lib/
            string? newest = Directory.GetDirectories(packageDir)
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => Directory.GetDirectories(d.FullName, "lib", SearchOption.AllDirectories).FirstOrDefault())
                .FirstOrDefault(d => d != null);

            if (newest != null)
                return newest;

            throw new DirectoryNotFoundException("No lib/ folder found for package " + packageId);
        }

        private static string FindDll(string nugetRoot, string packageId, string[] versions, string dllName)
        {
            string libDir = FindLibDir(nugetRoot, packageId, versions);
            string? dll = Directory.GetFiles(libDir, dllName, SearchOption.AllDirectories).FirstOrDefault();
            if (dll != null)
                return dll;

            throw new FileNotFoundException("Could not find " + dllName + " in " + libDir);
        }
    }
}

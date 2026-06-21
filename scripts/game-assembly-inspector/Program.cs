using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GameAssemblyInspector
{
    internal static class Program
    {
        private static readonly string[] DefaultKeywords =
        {
            "simple", "color", "visible", "draw", "mono", "silhouette"
        };

        private static int Main(string[] args)
        {
            try
            {
                return Run(ParseArgs(args));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (args.Any(a => a == "--verbose"))
                    Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static Options ParseArgs(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--game" && i + 1 < args.Length)
                {
                    options.Game = args[++i];
                    continue;
                }

                if (arg == "--dll" && i + 1 < args.Length)
                {
                    options.AssemblyPath = args[++i];
                    continue;
                }

                if (arg == "--unity-dir" && i + 1 < args.Length)
                {
                    options.UnityLibDir = args[++i];
                    continue;
                }

                if (arg == "--keywords" && i + 1 < args.Length)
                {
                    options.Keywords = args[++i]
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => k.Length > 0)
                        .ToArray();
                    continue;
                }

                if (arg == "--type" && i + 1 < args.Length)
                {
                    options.Types.Add(args[++i]);
                    continue;
                }

                if (arg == "--verbose")
                {
                    options.Verbose = true;
                    continue;
                }

                if (arg == "--help" || arg == "-h")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }

                throw new ArgumentException("Unknown argument: " + arg);
            }

            if (options.Keywords.Length == 0)
                options.Keywords = DefaultKeywords;

            if (options.Types.Count == 0)
            {
                options.Types.Add("Studio.OCIChar");
                options.Types.Add("Studio.OICharInfo");
                options.Types.Add("Studio.ObjectCtrlInfo");
            }

            return options;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Game Assembly Inspector — reflect Illusion Studio types from NuGet game DLLs.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project scripts/game-assembly-inspector -- --game HS2|KK|KKS");
            Console.WriteLine("  dotnet run --project scripts/game-assembly-inspector -- --dll <path> --unity-dir <dir>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --game HS2|KK|KKS   Use built-in NuGet profile (default package cache)");
            Console.WriteLine("  --dll <path>        Assembly-CSharp.dll path");
            Console.WriteLine("  --unity-dir <dir>   Folder containing UnityEngine*.dll refs");
            Console.WriteLine("  --type <fullName>   Type to inspect (repeatable)");
            Console.WriteLine("  --keywords <list>   Comma-separated member name filters");
            Console.WriteLine("  --verbose           Show load/resolve details");
        }

        private static int Run(Options options)
        {
            string assemblyPath;
            string unityLibDir;

            if (!string.IsNullOrEmpty(options.Game))
            {
                var profile = GameProfiles.Resolve(options.Game);
                assemblyPath = profile.AssemblyPath;
                unityLibDir = profile.UnityLibDir;
                Console.WriteLine("Game:      " + profile.Name);
                Console.WriteLine("Assembly:  " + assemblyPath);
                Console.WriteLine("Unity dir: " + unityLibDir);
            }
            else
            {
                if (string.IsNullOrEmpty(options.AssemblyPath) || string.IsNullOrEmpty(options.UnityLibDir))
                    throw new ArgumentException("Provide --game HS2|KK|KKS or both --dll and --unity-dir.");

                assemblyPath = Path.GetFullPath(options.AssemblyPath);
                unityLibDir = Path.GetFullPath(options.UnityLibDir);
                Console.WriteLine("Assembly:  " + assemblyPath);
                Console.WriteLine("Unity dir: " + unityLibDir);
            }

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException("Assembly-CSharp not found.", assemblyPath);
            if (!Directory.Exists(unityLibDir))
                throw new DirectoryNotFoundException("Unity lib directory not found: " + unityLibDir);

            var resolver = new UnityAssemblyResolver(unityLibDir, options.Verbose);
            AppDomain.CurrentDomain.AssemblyResolve += resolver.Resolve;

            var asm = Assembly.LoadFrom(assemblyPath);
            Console.WriteLine("Loaded:    " + asm.GetName().Name + " (" + asm.ImageRuntimeVersion + ")");
            Console.WriteLine("Keywords:  " + string.Join(", ", options.Keywords));
            Console.WriteLine();

            if (Environment.GetEnvironmentVariable("LIST_TYPES") is string lt && lt.Length > 0)
            {
                Console.WriteLine("=== Types matching: " + lt + " ===");
                Type?[] allTypes;
                try
                {
                    allTypes = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    allTypes = ex.Types; // partial list; nulls for types that failed to load
                }

                foreach (var t in allTypes
                    .Where(t => t?.FullName != null && t.FullName.IndexOf(lt, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(t => t!.FullName))
                {
                    Console.WriteLine("  " + t!.FullName);
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .Where(m => m.Name.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        Console.WriteLine("      [static] " + FormatMethod(m));
                    }
                }
                Console.WriteLine();
                return 0;
            }

            foreach (string typeName in options.Types)
            {
                InspectType(asm, typeName, options.Keywords);
                if (typeName.EndsWith("RefObjKey", StringComparison.Ordinal))
                    DumpEnum.Dump(asm, typeName);
                Console.WriteLine();
            }

            return 0;
        }

        private static void InspectType(Assembly asm, string typeName, string[] keywords)
        {
            var type = asm.GetType(typeName, throwOnError: false);
            Console.WriteLine("=== " + typeName + " ===");
            if (type == null)
            {
                Console.WriteLine("  (type not found)");
                return;
            }

            PrintMembers("Methods (declared)", type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), keywords);
            PrintMembers("Properties (declared)", type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), keywords);
            PrintMembers("Fields (declared)", type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), keywords);

            // Also show inherited public members that match — HS2 may define helpers on base types.
            var baseType = type.BaseType;
            while (baseType != null && baseType.FullName != "System.Object")
            {
                var baseMethods = baseType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                var baseProps = baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                var baseFields = baseType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (MatchesAny(baseMethods, keywords) || MatchesAny(baseProps, keywords) || MatchesAny(baseFields, keywords))
                {
                    Console.WriteLine("  -- inherited from " + baseType.FullName + " --");
                    PrintMembers("Methods", baseMethods, keywords);
                    PrintMembers("Properties", baseProps, keywords);
                    PrintMembers("Fields", baseFields, keywords);
                }

                baseType = baseType.BaseType;
            }
        }

        private static bool MatchesAny(IEnumerable<MemberInfo> members, string[] keywords) =>
            members.Any(m => NameMatches(m.Name, keywords));

        private static void PrintMembers(string heading, IEnumerable<MemberInfo> members, string[] keywords)
        {
            var list = members
                .Where(m => NameMatches(m.Name, keywords))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count == 0)
                return;

            Console.WriteLine("  " + heading + ":");
            foreach (var member in list)
            {
                switch (member)
                {
                    case MethodInfo method:
                        Console.WriteLine("    " + FormatMethod(method));
                        break;
                    case PropertyInfo prop:
                        Console.WriteLine("    " + prop.PropertyType.Name + " " + prop.Name + " { get; set; }");
                        break;
                    case FieldInfo field:
                        Console.WriteLine("    " + field.FieldType.Name + " " + field.Name);
                        break;
                }
            }
        }

        private static string FormatMethod(MethodInfo method)
        {
            string parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            return method.ReturnType.Name + " " + method.Name + "(" + parameters + ")";
        }

        private static bool NameMatches(string name, string[] keywords) =>
            keywords.Any(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

        private sealed class Options
        {
            public string? Game;
            public string? AssemblyPath;
            public string? UnityLibDir;
            public string[] Keywords = DefaultKeywords;
            public readonly List<string> Types = new List<string>();
            public bool Verbose;
        }
    }
}

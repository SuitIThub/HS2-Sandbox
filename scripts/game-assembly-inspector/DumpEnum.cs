// One-off helper: dotnet script or compile with game-assembly-inspector
using System;
using System.Reflection;

namespace GameAssemblyInspector
{
    internal static class DumpEnum
    {
        internal static void Dump(Assembly asm, string typeName)
        {
            var type = asm.GetType(typeName, throwOnError: false);
            if (type == null || !type.IsEnum)
            {
                Console.WriteLine(typeName + ": not found or not enum");
                return;
            }

            Console.WriteLine("=== " + typeName + " (" + Enum.GetValues(type).Length + " values) ===");
            foreach (var name in Enum.GetNames(type))
                Console.WriteLine("  " + name + " = " + Convert.ToInt32(Enum.Parse(type, name)));
        }
    }
}

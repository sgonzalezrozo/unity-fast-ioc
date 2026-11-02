#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace FastIoC.Editor.Generation
{
    public static class TypeIdEmitter
    {
        private static bool IsTestType(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;
            return assemblyName.Contains("Test") || assemblyName.Contains("test");
        }

        public static string GenerateTypeIds(HashSet<Type> reachableTypes, out Dictionary<Type, int> typeIdMap)
        {
            StringBuilder sb = new();
            typeIdMap = new Dictionary<Type, int>();

            sb.AppendLine("namespace FastIoCGenerated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Generated type IDs for fast array-based lookup.");
            sb.AppendLine("    /// Regenerated at editor time.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class GeneratedTypeIds");
            sb.AppendLine("    {");

            int currentId = 0;
            foreach (Type type in reachableTypes)
            {
                if (IsTestType(type))
                    continue;

                typeIdMap[type] = currentId;
                string typeName = type.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                sb.AppendLine($"        public static readonly FastIoC.Core.TypeId {typeName} = new({currentId});");
                currentId++;
            }

            sb.AppendLine($"        public static readonly int MaxTypeId = {currentId - 1};");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
#endif

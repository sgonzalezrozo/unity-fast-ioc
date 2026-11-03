#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace FastIoC.Editor.Generation
{
    public static class InstanceRegistryEmitter
    {
        public static string GenerateInstanceRegistry(Dictionary<Type, int> typeIdMap)
        {
            StringBuilder sb = new();

            sb.AppendLine("namespace FastIoCGenerated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Generated registry that stores and retrieves pre-created instances by type ID.");
            sb.AppendLine("    /// Uses array-based O(1) lookup indexed by TypeId (150x faster than Dictionary).");
            sb.AppendLine("    /// Instance bindings are registered during ContextRuntime initialization.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class GeneratedInstanceRegistry");
            sb.AppendLine("    {");
            sb.AppendLine("        // Array-based lookup: O(1) vs Dictionary O(hash). Capacity = MaxTypeId + 1");
            sb.AppendLine("        private static readonly object[] instances = new object[GeneratedTypeIds.MaxTypeId + 1];");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Register a pre-created instance for a given type ID.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static void RegisterInstance(int typeId, object instance)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (typeId >= 0 && typeId <= GeneratedTypeIds.MaxTypeId)");
            sb.AppendLine("                instances[typeId] = instance;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Retrieve a registered instance by type ID.");
            sb.AppendLine("        /// Returns null if typeId is out of bounds or no instance is registered.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static object GetInstance(int typeId)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (typeId >= 0 && typeId <= GeneratedTypeIds.MaxTypeId)");
            sb.AppendLine("                return instances[typeId];");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Remove every registered instance. Called on Play Mode start (also with Domain Reload disabled).");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static void Clear()");
            sb.AppendLine("        {");
            sb.AppendLine("            global::System.Array.Clear(instances, 0, instances.Length);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
#endif



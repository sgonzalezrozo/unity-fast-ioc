#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace FastIoC.Editor.Generation
{
    public static class InjectableMonoBehavioursEmitter
    {
        private static bool IsTestType(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;
            return assemblyName.Contains("Test") || assemblyName.Contains("test");
        }

        public static string GenerateRegistry(HashSet<Type> injectableMonoBehaviourTypes)
        {
            StringBuilder sb = new();

            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace FastIoCGenerated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedInjectableMonoBehaviours");
            sb.AppendLine("    {");

            List<Type> validTypes = new();
            foreach (Type type in injectableMonoBehaviourTypes)
            {
                if (IsTypeValid(type))
                    validTypes.Add(type);
            }

            sb.AppendLine("        public static readonly Type[] Types = new Type[]");
            sb.AppendLine("        {");

            validTypes.Sort((a, b) => (a.FullName ?? "").CompareTo(b.FullName ?? ""));

            foreach (Type type in validTypes)
            {
                string fullName = type.FullName ?? type.Name;
                sb.AppendLine($"            typeof({fullName}),");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Count of injectable types (automatically synced with Types array)");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly int TypeCount = Types.Length;");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Check if a type is in the injectable registry.");
            sb.AppendLine("        /// Used by InjectIfNeeded() helper for runtime instantiation.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static bool IsInjectableType(Type type)");
            sb.AppendLine("        {");
            sb.AppendLine("            foreach (Type injectableType in Types)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (injectableType == type)");
            sb.AppendLine("                    return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Validates that a type still exists and is accessible.
        /// Returns false for deleted types or types that can no longer be reflected on.
        /// </summary>
        private static bool IsTypeValid(Type type)
        {
            if (type == null)
                return false;

            if (IsTestType(type))
                return false;

            try
            {
                // Try to access the type's FullName - this will fail if type was unloaded
                string fullName = type.FullName;
                if (string.IsNullOrEmpty(fullName))
                    return false;

                // Try to get fields - this ensures the type is still loadable
                _ = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                return true;
            }
            catch
            {
                // Type is no longer valid (deleted, unloaded, or broken)
                return false;
            }
        }
    }
}
#endif



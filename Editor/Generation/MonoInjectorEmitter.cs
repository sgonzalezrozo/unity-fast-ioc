#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    public static class MonoInjectorEmitter
    {
        private static bool IsTestType(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;
            return assemblyName.Contains("Test") || assemblyName.Contains("test");
        }

        private static Type FindTypeByFullName(Type searchType, Dictionary<Type, int> map)
        {
            if (searchType == null)
                return null;

            string fullName = searchType.FullName;
            foreach (Type key in map.Keys)
            {
                if (key != null && key.FullName == fullName)
                    return key;
            }

            return null;
        }

        private static Type FindTypeByFullName(Type searchType, Dictionary<Type, Type> map)
        {
            if (searchType == null)
                return null;

            string fullName = searchType.FullName;
            foreach (Type key in map.Keys)
            {
                if (key != null && key.FullName == fullName)
                    return key;
            }

            return null;
        }

        private static Type FindTypeByFullName(Type searchType, Dictionary<Type, bool> map)
        {
            if (searchType == null)
                return null;

            string fullName = searchType.FullName;
            foreach (Type key in map.Keys)
            {
                if (key != null && key.FullName == fullName)
                    return key;
            }

            return null;
        }

        public static string GenerateMonoInjectors(HashSet<Type> reachableTypes, Dictionary<Type, int> typeIdMap, Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, bool> instanceBindings = null)
        {
            instanceBindings ??= new();

            StringBuilder sb = new();

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using FastIoC.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace FastIoCGenerated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Generated MonoBehaviour injectors for `internal` fields.");
            sb.AppendLine("    /// Only for reachable types.");
            sb.AppendLine("    /// Caches FieldInfo objects to avoid repeated reflection calls per type.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class GeneratedMonoInjectors");
            sb.AppendLine("    {");
            // Collect injectable types to count them
            List<Type> injectableTypesList = new();
            foreach (Type type in reachableTypes)
            {
                if (!IsTypeValid(type) || !typeof(MonoBehaviour).IsAssignableFrom(type))
                    continue;

                FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                bool hasInjectableFields = false;

                foreach (FieldInfo field in fields)
                {
                    bool isInternal = (field.Attributes & FieldAttributes.Assembly) != 0;
                    bool isDirectlyReachable = typeIdMap.ContainsKey(field.FieldType) || FindTypeByFullName(field.FieldType, typeIdMap) != null;
                    bool isInterfaceWithImpl = interfaceImplementations.ContainsKey(field.FieldType) || FindTypeByFullName(field.FieldType, interfaceImplementations) != null;
                    bool isInstanceBinding = instanceBindings.ContainsKey(field.FieldType) || FindTypeByFullName(field.FieldType, instanceBindings) != null;

                    if (isInternal && (isDirectlyReachable || isInterfaceWithImpl || isInstanceBinding))
                    {
                        hasInjectableFields = true;
                        break;
                    }
                }

                if (hasInjectableFields)
                    injectableTypesList.Add(type);
            }

            sb.AppendLine("        // Static cache: Type.FullName + \"|\" + FieldName -> FieldInfo");
            sb.AppendLine("        // First instance pays reflection cost, subsequent instances do O(1) cache lookup");
            sb.AppendLine("        // Pre-sized to expected number of types to avoid dictionary resizing");
            int cacheCapacity = Math.Max(16, injectableTypesList.Count * 2);
            sb.AppendLine($"        private static readonly Dictionary<string, FieldInfo> fieldInfoCache = new({cacheCapacity});");
            sb.AppendLine();
            sb.AppendLine("        // Type-based dispatcher: O(1) lookup by type instead of O(n) switch pattern matching");
            sb.AppendLine("        // Dramatically faster for large numbers of injectable types");
            sb.AppendLine($"        private static readonly Dictionary<Type, Action<MonoBehaviour, ContextRuntime>> injectorMap = new({injectableTypesList.Count})");
            sb.AppendLine("        {");

            foreach (Type type in injectableTypesList)
            {
                string fullTypeName = type.FullName ?? "Unknown";
                string methodName = fullTypeName.Replace(".", "_").Replace("+", "_");
                sb.AppendLine($"            {{ typeof({fullTypeName}), (mb, ctx) => Inject_{methodName}(({fullTypeName})mb, ctx) }},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Get FieldInfo with caching. First call reflects, subsequent calls are O(1) lookup.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        private static FieldInfo GetCachedFieldInfo(Type targetType, string fieldName)");
            sb.AppendLine("        {");
            sb.AppendLine("            string cacheKey = targetType.FullName + \"|\" + fieldName;");
            sb.AppendLine("            if (!fieldInfoCache.TryGetValue(cacheKey, out FieldInfo field))");
            sb.AppendLine("            {");
            sb.AppendLine("                field = targetType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);");
            sb.AppendLine("                fieldInfoCache[cacheKey] = field;");
            sb.AppendLine("            }");
            sb.AppendLine("            return field;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static void Inject(MonoBehaviour monoBehaviour, ContextRuntime context)");
            sb.AppendLine("        {");
            sb.AppendLine("            Type mbType = monoBehaviour.GetType();");
            sb.AppendLine("            if (injectorMap.TryGetValue(mbType, out var injector))");
            sb.AppendLine("                injector(monoBehaviour, context);");
            sb.AppendLine("        }");

            foreach (Type type in reachableTypes)
            {
                if (!IsTypeValid(type))
                    continue;

                if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                    continue;

                string methodName = type.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";

                FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                bool hasFields = false;

                foreach (FieldInfo field in fields)
                {
                    bool isInternal = (field.Attributes & FieldAttributes.Assembly) != 0;

                    if (!isInternal || (!typeIdMap.ContainsKey(field.FieldType) && FindTypeByFullName(field.FieldType, typeIdMap) == null && !interfaceImplementations.ContainsKey(field.FieldType) &&
                                        FindTypeByFullName(field.FieldType, interfaceImplementations) == null && !instanceBindings.ContainsKey(field.FieldType) &&
                                        FindTypeByFullName(field.FieldType, instanceBindings) == null)) continue;

                    hasFields = true;
                    break;
                }

                if (!hasFields)
                    continue;

                sb.AppendLine();
                sb.AppendLine($"        public static void Inject_{methodName}({type.FullName} target, ContextRuntime context)");
                sb.AppendLine("        {");

                foreach (FieldInfo field in fields)
                {
                    bool isInternal = (field.Attributes & FieldAttributes.Assembly) != 0;

                    if (!isInternal)
                        continue;

                    Type fieldType = field.FieldType;
                    Type resolveType = fieldType;

                    Type interfaceMapKey = interfaceImplementations.ContainsKey(fieldType) ? fieldType : FindTypeByFullName(fieldType, interfaceImplementations);
                    if (interfaceMapKey != null)
                        resolveType = interfaceImplementations[interfaceMapKey];

                    Type typeIdMapKey = typeIdMap.ContainsKey(resolveType) ? resolveType : FindTypeByFullName(resolveType, typeIdMap);
                    Type instanceBindingKey = instanceBindings.ContainsKey(fieldType) ? fieldType : FindTypeByFullName(fieldType, instanceBindings);

                    if (typeIdMapKey != null || instanceBindingKey != null)
                    {
                        string fieldName = field.Name;
                        string typeName = type.FullName ?? "Unknown";

                        sb.AppendLine($"            var field_{fieldName} = GetCachedFieldInfo(typeof({typeName}), \"{fieldName}\");");
                        sb.AppendLine($"            if (field_{fieldName} != null)");

                        if (typeIdMapKey != null)
                        {
                            string typeIdName = resolveType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                            sb.AppendLine($"                field_{fieldName}.SetValue(target, context.Resolve(GeneratedTypeIds.{typeIdName}));");
                        }
                        else if (instanceBindingKey != null)
                        {
                            string typeIdName = fieldType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                            sb.AppendLine($"                field_{fieldName}.SetValue(target, context.Resolve(GeneratedTypeIds.{typeIdName}));");
                        }
                    }
                    else if (type.Name == "ScreenManager" && isInternal)
                    {
                        bool hasResolve = typeIdMap.ContainsKey(fieldType) || (interfaceImplementations.ContainsKey(fieldType) && typeIdMap.ContainsKey(interfaceImplementations[fieldType])) ||
                                          FindTypeByFullName(fieldType, typeIdMap) != null || FindTypeByFullName(fieldType, interfaceImplementations) != null;
                        Debug.Log(
                            $"[MonoInjectorEmitter] SKIPPING {type.Name}.{field.Name}:{fieldType.Name} - HasResolve:{hasResolve}, InterfaceImpl:{interfaceMapKey != null}, InstanceBinding:{instanceBindingKey != null}");
                    }
                }


                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static bool IsTypeValid(Type type)
        {
            if (type == null)
                return false;

            if (IsTestType(type))
                return false;

            try
            {
                string fullName = type.FullName;
                if (string.IsNullOrEmpty(fullName))
                    return false;

                _ = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif


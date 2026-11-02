#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Tools.FastIoC.Runtime.Core;

namespace Tools.FastIoC.Editor.Generation
{
    public static class ActivatorEmitter
    {
        private static bool IsTestType(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name;
            return assemblyName.Contains("Test") || assemblyName.Contains("test");
        }

        public static string GenerateActivators(HashSet<Type> reachableTypes, Dictionary<Type, int> typeIdMap, Dictionary<Type, Lifetime> typeLifetimes, Dictionary<Type, bool> instanceBindings = null,
            Dictionary<Type, Type> interfaceImplementations = null)
        {
            StringBuilder sb = new();

            sb.AppendLine("using System;");
            sb.AppendLine("using Tools.FastIoC.Runtime.Core;");

            HashSet<string> namespaces = new();
            foreach (Type type in reachableTypes)
            {
                if (IsTestType(type))
                    continue;

                if (!string.IsNullOrEmpty(type.Namespace))
                    namespaces.Add(type.Namespace);

                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                foreach (ConstructorInfo ctor in constructors)
                {
                    foreach (ParameterInfo param in ctor.GetParameters())
                    {
                        if (!string.IsNullOrEmpty(param.ParameterType.Namespace))
                            namespaces.Add(param.ParameterType.Namespace);
                    }
                }
            }

            namespaces.Remove("System");
            namespaces.Remove("Tools.FastIoC.Runtime.Core");

            foreach (string ns in namespaces)
                sb.AppendLine($"using {ns};");

            sb.AppendLine();
            sb.AppendLine("namespace FastIoC.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedActivators");
            sb.AppendLine("    {");

            foreach (Type type in reachableTypes)
            {
                if (IsTestType(type))
                    continue;

                string methodName = type.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                sb.AppendLine();
                sb.AppendLine($"        public static object Activate_{methodName}(ContextRuntime context)");
                sb.AppendLine("        {");

                // Check if this is an instance binding
                bool isInstance = instanceBindings != null && instanceBindings.ContainsKey(type) && instanceBindings[type];

                if (isInstance && typeIdMap.ContainsKey(type))
                {
                    // Instance binding - resolve from registry instead of constructor
                    string typeIdName = type.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                    sb.AppendLine($"            // Instance binding - resolve from registry");
                    sb.AppendLine($"            return GeneratedInstanceRegistry.GetInstance(GeneratedTypeIds.{typeIdName}.Id);");
                }
                else
                {
                    // Regular constructor binding
                    ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                    if (constructors.Length == 0)
                    {
                        // No-arg constructor
                        string typeName = type.Name;
                        sb.AppendLine($"            return new {typeName}();");
                    }
                    else
                    {
                        // Use first public constructor
                        ParameterInfo[] parameters = constructors[0].GetParameters();

                        if (parameters.Length == 0)
                        {
                            string typeName = type.Name;
                            sb.AppendLine($"            return new {typeName}();");
                        }
                        else
                        {
                            // Generate parameter resolution
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                Type paramType = parameters[i].ParameterType;
                                string paramName = $"param{i}";

                                // Special case: if parameter is ContextRuntime, use the context directly
                                if (paramType.Name == "ContextRuntime" && paramType.Namespace == "Tools.FastIoC.Runtime.Core")
                                {
                                    sb.AppendLine($"            var {paramName} = context;");
                                }
                                else if (typeIdMap.ContainsKey(paramType))
                                {
                                    string typeIdName = paramType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                                    string paramTypeName = paramType.Name;
                                    sb.AppendLine($"            var {paramName} = ({paramTypeName})context.Resolve(GeneratedTypeIds.{typeIdName});");
                                }
                                else if (interfaceImplementations != null && interfaceImplementations.ContainsKey(paramType))
                                {
                                    // Interface type - resolve via concrete implementation
                                    Type concreteType = interfaceImplementations[paramType];
                                    if (typeIdMap.ContainsKey(concreteType))
                                    {
                                        string typeIdName = concreteType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                                        string paramTypeName = paramType.Name;
                                        sb.AppendLine($"            var {paramName} = ({paramTypeName})context.Resolve(GeneratedTypeIds.{typeIdName});");
                                    }
                                    else
                                    {
                                        string paramTypeName = paramType.Name;
                                        sb.AppendLine($"            // WARNING: Parameter type {paramType.Name} not in registry");
                                        sb.AppendLine($"            var {paramName} = default({paramTypeName});");
                                    }
                                }
                                else
                                {
                                    string paramTypeName = paramType.Name;
                                    sb.AppendLine($"            // WARNING: Parameter type {paramType.Name} not in registry");
                                    sb.AppendLine($"            var {paramName} = default({paramTypeName});");
                                }
                            }

                            string paramList = "";
                            for (int i = 0; i < parameters.Length; i++)
                                paramList += (i > 0 ? ", " : "") + $"param{i}";

                            string instantiateName = type.Name;
                            sb.AppendLine($"            return new {instantiateName}({paramList});");
                        }
                    }
                }

                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FastIoC.Core;

namespace FastIoC.Editor.Generation
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
            sb.AppendLine("using FastIoC.Core;");

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
            namespaces.Remove("FastIoC.Core");

            foreach (string ns in namespaces)
                sb.AppendLine($"using {ns};");

            sb.AppendLine();
            sb.AppendLine("namespace FastIoCGenerated");
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
                        // The implicit constructor is public, so an empty list means only non-public ones exist.
                        // GenerationCoordinator reports this before emitting; this guards direct callers.
                        throw new InvalidOperationException(FastIoC.Editor.Graph.DependencyDagValidator.NoPublicConstructorMessage(type));
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
                                if (paramType.Name == "ContextRuntime" && paramType.Namespace == "FastIoC.Core")
                                {
                                    sb.AppendLine($"            var {paramName} = context;");
                                }
                                else if (DeferredDependency.Unwrap(paramType, out Type innerType) != DeferredDependency.Kind.None)
                                {
                                    // Lazy<T> / Func<T>: resolve T when the wrapper is used, not now.
                                    Type resolveType = !typeIdMap.ContainsKey(innerType) && interfaceImplementations != null && interfaceImplementations.TryGetValue(innerType, out Type impl)
                                        ? impl
                                        : innerType;
                                    if (typeIdMap.ContainsKey(resolveType))
                                    {
                                        string expression = DeferredDependency.Expression(DeferredDependency.Unwrap(paramType, out _), innerType, "context", CodeGenTypeUtility.GetTypeIdMemberName(resolveType));
                                        sb.AppendLine($"            var {paramName} = {expression};");
                                    }
                                    else
                                    {
                                        sb.AppendLine($"            // WARNING: Parameter type {innerType.Name} not in registry");
                                        sb.AppendLine($"            {CodeGenTypeUtility.GetCSharpName(paramType)} {paramName} = null;");
                                    }
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
                                        EmitUnboundParameter(sb, parameters[i], paramType, paramName);
                                    }
                                }
                                else
                                {
                                    EmitUnboundParameter(sb, parameters[i], paramType, paramName);
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

        private static void EmitUnboundParameter(StringBuilder sb, ParameterInfo parameter, Type paramType, string paramName)
        {
            if (!parameter.HasDefaultValue)
                sb.AppendLine($"            // WARNING: Parameter type {paramType.Name} is not bound and has no default value");

            sb.AppendLine($"            {CodeGenTypeUtility.GetCSharpName(paramType)} {paramName} = {CodeGenTypeUtility.FormatUnboundParameter(parameter)};");
        }
    }
}
#endif

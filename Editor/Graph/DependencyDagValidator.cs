#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using FastIoC.API;

namespace FastIoC.Editor.Graph
{
    public static class DependencyDagValidator
    {
        public class DagNode
        {
            public Type Type { get; set; }
            public List<Type> Dependencies { get; set; } = new();
            public bool Visited { get; set; }
            public bool InStack { get; set; }
        }

        public static Dictionary<Type, DagNode> BuildDag(HashSet<Type> reachableTypes, Dictionary<Type, BindingInfo> bindings)
        {
            Dictionary<Type, DagNode> dag = new();

            foreach (Type type in reachableTypes)
            {
                DagNode node = new() { Type = type };

                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                if (constructors.Length > 0)
                {
                    ParameterInfo[] parameters = constructors[0].GetParameters();
                    foreach (ParameterInfo param in parameters)
                    {
                        Type dependency = ResolveDependency(param.ParameterType, bindings);
                        if (reachableTypes.Contains(dependency))
                            node.Dependencies.Add(dependency);
                    }
                }

                dag[type] = node;
            }

            return dag;
        }

        private static Type ResolveDependency(Type parameterType, Dictionary<Type, BindingInfo> bindings)
        {
            return bindings.TryGetValue(parameterType, out BindingInfo binding)
                ? binding.ImplementationType
                : parameterType;
        }

        public static bool HasCycles(Dictionary<Type, DagNode> dag, out List<string> cycleErrors)
        {
            cycleErrors = new();

            foreach (KeyValuePair<Type, DagNode> kvp in dag)
            {
                kvp.Value.Visited = false;
                kvp.Value.InStack = false;
            }

            foreach (KeyValuePair<Type, DagNode> kvp in dag)
            {
                if (kvp.Value.Visited)
                    continue;

                if (HasCycleDfs(kvp.Value, dag, cycleErrors))
                    return true;
            }

            return false;
        }

        private static bool HasCycleDfs(DagNode node, Dictionary<Type, DagNode> dag, List<string> cycleErrors)
        {
            node.Visited = true;
            node.InStack = true;

            foreach (Type depType in node.Dependencies)
            {
                if (!dag.TryGetValue(depType, out DagNode depNode))
                    continue;

                if (!depNode.Visited)
                {
                    if (HasCycleDfs(depNode, dag, cycleErrors))
                        return true;
                }
                else if (depNode.InStack)
                {
                    cycleErrors.Add($"Cycle detected: {node.Type.Name} -> {depNode.Type.Name}");
                    return true;
                }
            }

            node.InStack = false;
            return false;
        }

        /// <summary>
        /// Checks that every constructor parameter the generated code needs has a binding.
        /// Without this check, the generated activator passes null or a default value.
        /// </summary>
        public static bool ValidateMissingBindings(
            HashSet<Type> reachableTypes,
            Dictionary<Type, BindingInfo> bindings,
            HashSet<Type> frameworkTypes,
            out List<string> errors)
        {
            errors = new();

            HashSet<Type> availableTypes = new(frameworkTypes);
            availableTypes.UnionWith(reachableTypes);
            foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
            {
                availableTypes.Add(binding.Key);
                availableTypes.Add(binding.Value.ImplementationType);
            }

            HashSet<Type> instanceTypes = new();
            foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
            {
                if (binding.Value.IsInstance)
                    instanceTypes.Add(binding.Value.ImplementationType);
            }

            foreach (Type type in reachableTypes)
            {
                if (frameworkTypes.Contains(type) || instanceTypes.Contains(type))
                    continue;

                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                if (constructors.Length == 0)
                    continue;

                foreach (ParameterInfo param in constructors[0].GetParameters())
                {
                    if (param.HasDefaultValue || availableTypes.Contains(param.ParameterType))
                        continue;

                    errors.Add($"{type.Name} requires {param.ParameterType.Name}, which isn't bound in any installer.");
                }
            }

            errors.Sort(StringComparer.Ordinal);
            return errors.Count == 0;
        }
    }
}
#endif

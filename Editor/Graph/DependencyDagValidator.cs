#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tools.FastIoC.Editor.Graph
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

        public static Dictionary<Type, DagNode> BuildDag(HashSet<Type> reachableTypes)
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
                        if (reachableTypes.Contains(param.ParameterType))
                            node.Dependencies.Add(param.ParameterType);
                    }
                }

                dag[type] = node;
            }

            return dag;
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

        public static bool ValidateMissingBindings(Dictionary<Type, DagNode> dag, Dictionary<Type, List<Type>> installerBindings, out List<string> errors)
        {
            errors = new();

            foreach (KeyValuePair<Type, DagNode> kvp in dag)
            {
                Type type = kvp.Key;
                DagNode node = kvp.Value;

                bool isBound = false;
                foreach (KeyValuePair<Type, List<Type>> installerKvp in installerBindings)
                {
                    if (!installerKvp.Value.Contains(type))
                        continue;

                    isBound = true;
                    break;
                }

                if (!isBound && node.Dependencies.Count > 0)
                    errors.Add($"Type {type.Name} is required but not bound in any installer.");
            }

            return errors.Count == 0;
        }
    }
}
#endif

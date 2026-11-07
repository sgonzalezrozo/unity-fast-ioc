#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using FastIoC.Editor.Generation;

namespace FastIoC.Editor.Graph
{
    public static class ReachabilityPruner
    {
        public static HashSet<Type> PruneUnreachableTypes(Dictionary<Type, List<Type>> installerBindings, List<Type> allScannedTypes)
        {
            HashSet<Type> reachable = new();
            Queue<Type> queue = new();

            foreach (KeyValuePair<Type, List<Type>> kvp in installerBindings)
            {
                foreach (Type bindingType in kvp.Value)
                {
                    if (!reachable.Contains(bindingType))
                    {
                        queue.Enqueue(bindingType);
                        reachable.Add(bindingType);
                    }
                }
            }

            while (queue.Count > 0)
            {
                Type current = queue.Dequeue();

                ConstructorInfo[] constructors = current.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                foreach (ConstructorInfo ctor in constructors)
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    foreach (ParameterInfo param in parameters)
                    {
                        // Lazy<T> and Func<T> still need T to be generated.
                        DeferredDependency.Unwrap(param.ParameterType, out Type paramType);

                        if (!reachable.Contains(paramType) && !paramType.IsValueType && !paramType.IsPrimitive)
                        {
                            if (ShouldFollowType(paramType, allScannedTypes))
                            {
                                queue.Enqueue(paramType);
                                reachable.Add(paramType);
                            }
                        }
                    }
                }
            }

            return reachable;
        }

        private static bool ShouldFollowType(Type type, List<Type> scannedTypes)
        {
            // Follow if it's in scanned types (user code)
            return scannedTypes.Contains(type);
        }
    }
}
#endif


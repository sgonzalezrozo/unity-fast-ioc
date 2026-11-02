#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;

namespace Tools.FastIoC.Editor.Generation
{
    public static class CoreFrameworkTypesRegistry
    {
        public struct CoreTypes
        {
            public Type IInstantiator;
            public Type InstantiationFactory;
            public Type IPrefabInstantiator;
            public Type FastIoCPrefabInstantiator;
            public Type ContextRuntime;

            public bool IsValid =>
                IInstantiator != null &&
                InstantiationFactory != null &&
                IPrefabInstantiator != null &&
                FastIoCPrefabInstantiator != null &&
                ContextRuntime != null;
        }

        public static CoreTypes GetCoreFrameworkTypes()
        {
            CoreTypes types = new()
            {
                IInstantiator = ReflectionUtility.ResolveType(FastIoCConstants.IINSTANTIATOR_TYPE),
                InstantiationFactory = ReflectionUtility.ResolveType(FastIoCConstants.INSTANTIATION_FACTORY_TYPE),
                IPrefabInstantiator = ReflectionUtility.ResolveType(FastIoCConstants.IPREFAB_INSTANTIATOR_TYPE),
                FastIoCPrefabInstantiator = ReflectionUtility.ResolveType(FastIoCConstants.FAST_IOC_PREFAB_INSTANTIATOR_TYPE),
                ContextRuntime = ReflectionUtility.ResolveType(FastIoCConstants.CONTEXT_RUNTIME_TYPE),
            };

            return types;
        }

        public static void RegisterCoreFrameworkBindings(
            HashSet<Type> reachableTypes,
            Dictionary<Type, List<Type>> installerBindings,
            CoreTypes coreTypes)
        {
            if (!coreTypes.IsValid)
            {
                List<string> unresolvedTypes = GetUnresolvedCoreTypeNames(coreTypes);
                Debug.LogWarning($"[FastIoC] Core framework types not all resolved. Skipping core binding registration. Unresolved types: {string.Join(", ", unresolvedTypes)}");
                return;
            }

            if (coreTypes.InstantiationFactory != null)
            {
                reachableTypes.Add(coreTypes.InstantiationFactory);

                if (!installerBindings.ContainsKey(coreTypes.IInstantiator))
                    installerBindings[coreTypes.IInstantiator] = new List<Type> { coreTypes.InstantiationFactory };
            }

            if (coreTypes.FastIoCPrefabInstantiator != null)
            {
                reachableTypes.Add(coreTypes.FastIoCPrefabInstantiator);

                if (!installerBindings.ContainsKey(coreTypes.IPrefabInstantiator))
                    installerBindings[coreTypes.IPrefabInstantiator] = new List<Type> { coreTypes.FastIoCPrefabInstantiator };
            }
        }

        private static List<string> GetUnresolvedCoreTypeNames(CoreTypes coreTypes)
        {
            List<string> unresolved = new();

            if (coreTypes.IInstantiator == null)
                unresolved.Add(FastIoCConstants.IINSTANTIATOR_TYPE);

            if (coreTypes.InstantiationFactory == null)
                unresolved.Add(FastIoCConstants.INSTANTIATION_FACTORY_TYPE);

            if (coreTypes.IPrefabInstantiator == null)
                unresolved.Add(FastIoCConstants.IPREFAB_INSTANTIATOR_TYPE);

            if (coreTypes.FastIoCPrefabInstantiator == null)
                unresolved.Add(FastIoCConstants.FAST_IOC_PREFAB_INSTANTIATOR_TYPE);

            if (coreTypes.ContextRuntime == null)
                unresolved.Add(FastIoCConstants.CONTEXT_RUNTIME_TYPE);

            return unresolved;
        }

        public static void MarkCoreTypesAsSingletons(
            Dictionary<Type, Lifetime> typeLifetimes,
            CoreTypes coreTypes)
        {
            if (coreTypes.IInstantiator != null && typeLifetimes.ContainsKey(coreTypes.IInstantiator))
                typeLifetimes[coreTypes.IInstantiator] = Lifetime.Singleton;

            if (coreTypes.InstantiationFactory != null && typeLifetimes.ContainsKey(coreTypes.InstantiationFactory))
                typeLifetimes[coreTypes.InstantiationFactory] = Lifetime.Singleton;

            if (coreTypes.IPrefabInstantiator != null && typeLifetimes.ContainsKey(coreTypes.IPrefabInstantiator))
                typeLifetimes[coreTypes.IPrefabInstantiator] = Lifetime.Singleton;

            if (coreTypes.FastIoCPrefabInstantiator != null && typeLifetimes.ContainsKey(coreTypes.FastIoCPrefabInstantiator))
                typeLifetimes[coreTypes.FastIoCPrefabInstantiator] = Lifetime.Singleton;
        }
    }
}
#endif

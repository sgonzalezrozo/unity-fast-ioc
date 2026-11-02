#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using FastIoC.Editor.Graph;
using FastIoC.API;
using FastIoC.Core;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    public static class TypeLifetimeBuilder
    {
        public static Dictionary<Type, Lifetime> BuildTypeLifetimes(
            Dictionary<Type, List<Type>> installerBindings,
            HashSet<Type> reachableTypes,
            CoreFrameworkTypesRegistry.CoreTypes coreTypes)
        {
            Dictionary<Type, Lifetime> result = new();

            foreach (Type type in reachableTypes)
                result[type] = Lifetime.Transient;

            CoreFrameworkTypesRegistry.MarkCoreTypesAsSingletons(result, coreTypes);

            Dictionary<string, List<Type>> autoDiscoveredInstallers = InstallerGraphCollector.CollectInstallersByScene();
            ExtractBindingLifetimes(result, autoDiscoveredInstallers);

            return result;
        }

        private static void ExtractBindingLifetimes(
            Dictionary<Type, Lifetime> typeLifetimes,
            Dictionary<string, List<Type>> autoDiscoveredInstallers)
        {
            List<Type> allAutoDiscoveredTypes = new();
            foreach (KeyValuePair<string, List<Type>> kvp in autoDiscoveredInstallers)
                allAutoDiscoveredTypes.AddRange(kvp.Value);

            foreach (Type installerType in allAutoDiscoveredTypes)
            {
                try
                {
                    object tempInstaller = null;
                    GameObject tempGO = null;

                    if (typeof(MonoBehaviour).IsAssignableFrom(installerType))
                    {
                        tempGO = new GameObject("TempAnalysis");
                        tempInstaller = tempGO.AddComponent(installerType);
                    }
                    else if (typeof(BindingRecorder).IsAssignableFrom(installerType))
                    {
                        tempInstaller = Activator.CreateInstance(installerType);
                    }

                    try
                    {
                        if (tempInstaller != null)
                        {
                            MethodInfo installMethod = installerType.GetMethod(
                                FastIoCConstants.METHOD_INSTALL_BINDINGS,
                                BindingFlags.Public | BindingFlags.Instance);

                            if (installMethod != null)
                                installMethod.Invoke(tempInstaller, null);

                            MethodInfo getBindingsMethod = installerType.GetMethod(
                                FastIoCConstants.METHOD_GET_BINDINGS_FOR_GENERATION,
                                BindingFlags.Public | BindingFlags.Instance);

                            if (getBindingsMethod != null)
                            {
                                Dictionary<Type, BindingInfo> bindings =
                                    getBindingsMethod.Invoke(tempInstaller, null) as Dictionary<Type, BindingInfo>;

                                if (bindings != null)
                                {
                                    foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
                                    {
                                        Type implementationType = binding.Value.ImplementationType;
                                        string lifetimeName = binding.Value.LifetimeName;

                                        if (!Enum.TryParse(lifetimeName, out Lifetime lifetime))
                                            lifetime = Lifetime.Transient;

                                        if (typeLifetimes.ContainsKey(implementationType))
                                            typeLifetimes[implementationType] = lifetime;
                                        if (typeLifetimes.ContainsKey(binding.Key))
                                            typeLifetimes[binding.Key] = lifetime;
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (tempGO != null)
                            UnityEngine.Object.DestroyImmediate(tempGO);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FastIoC] Could not extract bindings from {installerType.Name}: {ex.Message}");
                }
            }
        }
    }
}
#endif


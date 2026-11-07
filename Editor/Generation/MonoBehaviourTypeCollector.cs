#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using FastIoC.Core;
using FastIoC.Core.Abstraction;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    public static class MonoBehaviourTypeCollector
    {
        private const BindingFlags NONPUBLIC_INSTANCE = BindingFlags.NonPublic | BindingFlags.Instance;

        public static HashSet<Type> CollectAllMonoBehaviourTypes(
            HashSet<Type> reachableTypes,
            Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, bool> instanceBindings)
        {
            HashSet<Type> allMonoBehaviours = new();

            foreach (Type type in reachableTypes)
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(type) && !type.Name.EndsWith(FastIoCConstants.INSTALLER_SUFFIX))
                    allMonoBehaviours.Add(type);
            }

            ScanForSceneMonoBehaviours(allMonoBehaviours, reachableTypes, interfaceImplementations, instanceBindings);

            return allMonoBehaviours;
        }

        private static void ScanForSceneMonoBehaviours(
            HashSet<Type> allMonoBehaviours,
            HashSet<Type> reachableTypes,
            Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, bool> instanceBindings)
        {
            try
            {
                ITypeMetadataProvider typeProvider = GetTypeProvider();

                IEnumerable<Type> monoBehaviourTypes;
                if (typeProvider == null)
                {
                    Debug.Log("[FastIoC] Using fallback direct reflection to scan for MonoBehaviour types...");
                    monoBehaviourTypes = ScanMonoBehavioursDirectly();
                }
                else
                {
                    monoBehaviourTypes = typeProvider.GetTypes(t =>
                        typeof(MonoBehaviour).IsAssignableFrom(t)
                        && !t.IsGenericTypeDefinition
                        && !t.Name.EndsWith(FastIoCConstants.INSTALLER_SUFFIX));
                }

                foreach (Type type in monoBehaviourTypes)
                {
                    if (HasInjectableFields(type, reachableTypes, interfaceImplementations, instanceBindings))
                        allMonoBehaviours.Add(type);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Error scanning for scene MonoBehaviour types: {ex.Message}");
            }
        }

        private static bool HasInjectableFields(
            Type type,
            HashSet<Type> reachableTypes,
            Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, bool> instanceBindings)
        {
            FieldInfo[] fields = type.GetFields(NONPUBLIC_INSTANCE);

            foreach (FieldInfo field in fields)
            {
                DeferredDependency.Unwrap(field.FieldType, out Type fieldType);
                bool isDirectlyReachable = reachableTypes.Contains(fieldType);
                bool isInterfaceWithImpl = interfaceImplementations.ContainsKey(fieldType);
                bool isInstanceBinding = instanceBindings.ContainsKey(fieldType);

                if (isDirectlyReachable || isInterfaceWithImpl || isInstanceBinding)
                    return true;
            }

            return false;
        }

        private static ITypeMetadataProvider GetTypeProvider()
        {
            try
            {
                // Try to create UnityAppDomainMetadataProvider directly (corrected namespace)
                Type providerType = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    providerType = assembly.GetType("FastIoC.UnityIntegration.Runtime.Providers.UnityAppDomainMetadataProvider", throwOnError: false);
                    if (providerType != null)
                        break;
                }

                if (providerType != null)
                {
                    object providerInstance = Activator.CreateInstance(providerType);
                    return (ITypeMetadataProvider)providerInstance;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[FastIoC] Failed to create UnityAppDomainMetadataProvider: {ex.Message}");
            }

            // Provider unavailable - will use fallback
            return null;
        }

        private static IEnumerable<Type> ScanMonoBehavioursDirectly()
        {
            List<Type> result = new();

            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.IsDynamic)
                        continue;

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FastIoC] Failed to get types from assembly {assembly.GetName().Name}: {ex.Message}");
                        continue;
                    }

                    foreach (Type type in types)
                    {
                        try
                        {
                            if (typeof(MonoBehaviour).IsAssignableFrom(type)
                                && !type.IsGenericTypeDefinition
                                && !type.Name.EndsWith(FastIoCConstants.INSTALLER_SUFFIX))
                            {
                                result.Add(type);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[FastIoC] Failed to inspect type {type.FullName}: {ex.Message}");
                        }
                    }
                }

                Debug.Log($"[FastIoC] Direct reflection found {result.Count} MonoBehaviour types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error in MonoBehaviour direct reflection scan: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }
    }
}
#endif

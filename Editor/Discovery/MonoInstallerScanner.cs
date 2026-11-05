#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FastIoC.API;
using FastIoC.Core.Abstraction;
using UnityEngine;

namespace FastIoC.Editor.Discovery
{
    public static class MonoInstallerScanner
    {
        public static Dictionary<string, List<Type>> ScanAllByScene()
        {
            Dictionary<string, List<Type>> result = new();

            try
            {
                ITypeMetadataProvider typeProvider = GetTypeProvider();

                // If provider unavailable, use direct reflection fallback
                List<(Type type, MonoInstallerAttribute attr)> installerTypes;
                if (typeProvider == null)
                {
                    Debug.Log("[FastIoC] Using fallback direct reflection to scan for MonoInstallers...");
                    installerTypes = ScanDirectly().ToList();
                }
                else
                {
                    installerTypes = typeProvider.GetTypesWithAttribute<MonoInstallerAttribute>().ToList();
                }

                foreach ((Type type, MonoInstallerAttribute attr) in installerTypes)
                {
                    if (attr.Disabled)
                        continue;

                    if (!typeof(BindingRecorder).IsAssignableFrom(type))
                        continue;

                    if (type.IsAbstract || type.IsInterface)
                        continue;

                    foreach (string scene in attr.Scenes)
                    {
                        if (!result.ContainsKey(scene))
                            result[scene] = new List<Type>();

                        result[scene].Add(type);
                    }
                }

                foreach (string scene in result.Keys.ToList())
                {
                    result[scene].Sort((typeA, typeB) =>
                    {
                        MonoInstallerAttribute attrA = typeA.GetCustomAttribute<MonoInstallerAttribute>();
                        MonoInstallerAttribute attrB = typeB.GetCustomAttribute<MonoInstallerAttribute>();

                        int priorityA = attrA?.Priority ?? 0;
                        int priorityB = attrB?.Priority ?? 0;

                        return priorityB.CompareTo(priorityA);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error scanning for MonoInstallers: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }

        public static List<Type> ScanByScene(string sceneName)
        {
            Dictionary<string, List<Type>> allByScene = ScanAllByScene();
            List<Type> result = new();

            if (allByScene.ContainsKey("*"))
                result.AddRange(allByScene["*"]);

            if (allByScene.ContainsKey(sceneName))
                result.AddRange(allByScene[sceneName]);

            return result;
        }

        public static List<Type> ScanGlobal()
        {
            Dictionary<string, List<Type>> allByScene = ScanAllByScene();
            return allByScene.ContainsKey("*") ? allByScene["*"] : new List<Type>();
        }

        private static ITypeMetadataProvider GetTypeProvider()
        {
            try
            {
                // EDITOR-ONLY: Create UnityAppDomainMetadataProvider directly for code generation
                // This doesn't depend on the runtime registry or runtime initialization
                Type providerType = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    providerType = assembly.GetType("FastIoC.UnityIntegration.Runtime.Providers.UnityAppDomainMetadataProvider", throwOnError: false);
                    if (providerType != null)
                        break;
                }

                if (providerType == null)
                {
                    Debug.Log("[FastIoC] UnityAppDomainMetadataProvider not found in loaded assemblies. Will use direct reflection fallback.");
                    return null;
                }

                // Create instance directly - this works at editor time without runtime initialization
                object providerInstance = Activator.CreateInstance(providerType);
                return (ITypeMetadataProvider)providerInstance;
            }
            catch (Exception ex)
            {
                Debug.Log($"[FastIoC] Failed to create UnityAppDomainMetadataProvider: {ex.Message}. Using direct reflection fallback.");
                return null;
            }
        }

        private static IEnumerable<(Type type, MonoInstallerAttribute attr)> ScanDirectly()
        {
            List<(Type type, MonoInstallerAttribute attr)> result = new();

            try
            {
                // Direct reflection scan across all loaded assemblies
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
                            MonoInstallerAttribute attr = type.GetCustomAttribute<MonoInstallerAttribute>();
                            if (attr != null)
                                result.Add((type, attr));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[FastIoC] Failed to inspect type {type.FullName}: {ex.Message}");
                        }
                    }
                }

                Debug.Log($"[FastIoC] Direct reflection found {result.Count} types with [MonoInstaller] attribute");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Error in direct reflection scan: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }
    }
}
#endif

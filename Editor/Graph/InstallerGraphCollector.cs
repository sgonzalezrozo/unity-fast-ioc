using System;
using System.Collections.Generic;
using System.Reflection;
using FastIoC.Editor.Discovery;
using FastIoC.API;
using UnityEngine;
using BindingInfo = FastIoC.API.BindingInfo;
using Object = UnityEngine.Object;

namespace FastIoC.Editor.Graph
{
    public static class InstallerGraphCollector
    {
        private static Dictionary<Type, BindingInfo> cachedBindingInfo = new();

        public static Dictionary<Type, BindingInfo> GetCachedBindingInfo()
        {
            return cachedBindingInfo;
        }

        public static void ClearBindingInfoCache()
        {
            cachedBindingInfo.Clear();
        }

        public static List<Type> CollectInstallers()
        {
            List<Type> allInstallers = new();

            Dictionary<string, List<Type>> installersByScene = MonoInstallerScanner.ScanAllByScene();

            foreach (KeyValuePair<string, List<Type>> kvp in installersByScene)
            {
                foreach (Type type in kvp.Value)
                {
                    if (!allInstallers.Contains(type))
                        allInstallers.Add(type);
                }
            }

            Debug.Log($"[FastIoC] Found {allInstallers.Count} installers");
            return allInstallers;
        }

        public static Dictionary<string, List<Type>> CollectInstallersByScene()
        {
            return MonoInstallerScanner.ScanAllByScene();
        }

        public static List<Type> CollectInstallersForScene(string sceneName)
        {
            return MonoInstallerScanner.ScanByScene(sceneName);
        }

        public static List<Type> CollectGlobalInstallers()
        {
            return MonoInstallerScanner.ScanGlobal();
        }

        public static Dictionary<Type, List<Type>> AnalyzeInstallerBindings(List<Type> installerTypes)
        {
            Dictionary<Type, List<Type>> results = new();

            foreach (Type installerType in installerTypes)
            {
                List<Type> bindings = new();

                try
                {
                    object installerInstance = null;
                    GameObject tempGO = null;

                    if (typeof(MonoBehaviour).IsAssignableFrom(installerType))
                    {
                        tempGO = new GameObject("TempAnalysis");
                        installerInstance = tempGO.AddComponent(installerType);
                    }
                    else if (typeof(BindingRecorder).IsAssignableFrom(installerType))
                    {
                        try
                        {
                            installerInstance = Activator.CreateInstance(installerType);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[FastIoC] Could not create {installerType.Name} via Activator: {ex.Message}");
                        }
                    }

                    try
                    {
                        if (installerInstance != null)
                        {
                            MethodInfo installMethod = installerType.GetMethod("InstallBindings", BindingFlags.Public | BindingFlags.Instance);
                            if (installMethod != null)
                            {
                                installMethod.Invoke(installerInstance, null);

                                IBindingRecorder bindingRecorder = installerInstance as IBindingRecorder;
                                if (bindingRecorder != null)
                                {
                                    Dictionary<Type, BindingInfo> bindingDict = bindingRecorder.GetBindingsForGeneration();

                                    if (bindingDict != null && bindingDict.Count > 0)
                                    {
                                        foreach (KeyValuePair<Type, BindingInfo> kvp in bindingDict)
                                        {
                                            Type interfaceType = kvp.Key;
                                            BindingInfo bindingInfo = kvp.Value;
                                            Type implType = bindingInfo.ImplementationType;

                                            bindings.Add(implType);
                                            cachedBindingInfo[interfaceType] = bindingInfo;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (tempGO != null)
                            Object.DestroyImmediate(tempGO);
                    }

                    results[installerType] = bindings;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FastIoC] Error analyzing installer {installerType.Name}: {ex.Message}\n{ex.StackTrace}");
                    results[installerType] = new List<Type>();
                }
            }

            return results;
        }
    }
}
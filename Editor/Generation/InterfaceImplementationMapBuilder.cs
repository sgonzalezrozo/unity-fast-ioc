#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Tools.FastIoC.Editor.Graph;
using Tools.FastIoC.Runtime.API;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;

namespace Tools.FastIoC.Editor.Generation
{
    public static class InterfaceImplementationMapBuilder
    {
        public static Dictionary<Type, Type> BuildInterfaceImplementationMap(CoreFrameworkTypesRegistry.CoreTypes coreTypes)
        {
            Dictionary<Type, Type> map = new();

            AddCoreFrameworkBindings(map, coreTypes);
            AddAutoDiscoveredBindings(map);

            return map;
        }

        private static void AddCoreFrameworkBindings(Dictionary<Type, Type> map, CoreFrameworkTypesRegistry.CoreTypes coreTypes)
        {
            if (coreTypes.IInstantiator != null && coreTypes.InstantiationFactory != null)
                map[coreTypes.IInstantiator] = coreTypes.InstantiationFactory;

            if (coreTypes.IPrefabInstantiator != null && coreTypes.FastIoCPrefabInstantiator != null)
                map[coreTypes.IPrefabInstantiator] = coreTypes.FastIoCPrefabInstantiator;
        }

        private static void AddAutoDiscoveredBindings(Dictionary<Type, Type> map)
        {
            Dictionary<string, List<Type>> autoDiscoveredInstallers = InstallerGraphCollector.CollectInstallersByScene();
            List<Type> allAutoDiscoveredTypes = new();

            foreach (KeyValuePair<string, List<Type>> kvp in autoDiscoveredInstallers)
                allAutoDiscoveredTypes.AddRange(kvp.Value);

            foreach (Type installerType in allAutoDiscoveredTypes)
                ProcessAutoDiscoveredInstaller(map, installerType);
        }

        private static void ProcessAutoDiscoveredInstaller(Dictionary<Type, Type> map, Type installerType)
        {
            try
            {
                object tempInstaller = null;
                GameObject tempGO = null;

                try
                {
                    if (typeof(MonoBehaviour).IsAssignableFrom(installerType))
                    {
                        tempGO = new GameObject("TempAnalysis");
                        tempInstaller = tempGO.AddComponent(installerType);
                    }
                    else if (typeof(BindingRecorder).IsAssignableFrom(installerType))
                    {
                        ConstructorInfo ctor = installerType.GetConstructor(Type.EmptyTypes);
                        if (ctor != null)
                            tempInstaller = Activator.CreateInstance(installerType);
                    }

                    if (tempInstaller != null)
                    {
                        InvokeInstallBindings(tempInstaller, installerType);
                        ExtractBindingsFromInstaller(map, tempInstaller, installerType);
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
                Debug.LogWarning($"[FastIoC] Could not extract bindings from auto-discovered {installerType.Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void InvokeInstallBindings(object tempInstaller, Type installerType)
        {
            MethodInfo installMethod = installerType.GetMethod(
                FastIoCConstants.METHOD_INSTALL_BINDINGS,
                BindingFlags.Public | BindingFlags.Instance);

            if (installMethod != null)
                installMethod.Invoke(tempInstaller, null);
        }

        private static void ExtractBindingsFromInstaller(Dictionary<Type, Type> map, object tempInstaller, Type installerType)
        {
            MethodInfo getBindingsMethod = installerType.GetMethod(
                FastIoCConstants.METHOD_GET_BINDINGS_FOR_GENERATION,
                BindingFlags.Public | BindingFlags.Instance);

            if (getBindingsMethod == null)
                return;

            Dictionary<Type, BindingInfo> bindings =
                getBindingsMethod.Invoke(tempInstaller, null) as Dictionary<Type, BindingInfo>;

            if (bindings != null)
            {
                foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
                {
                    Type interfaceType = binding.Key;
                    Type implementationType = binding.Value.ImplementationType;

                    map[interfaceType] = implementationType;
                }
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tools.FastIoC.Editor.Graph;
using Tools.FastIoC.Runtime.API;
using Tools.FastIoC.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace Tools.FastIoC.Editor.Generation
{
    public static class GenerationCoordinator
    {
        private static List<string> lastValidationErrors;

        public static void GenerateAll()
        {
            lastValidationErrors = null;
            try
            {
                Debug.Log("[FastIoC] Starting code generation...");

                List<Type> installers = InstallerGraphCollector.CollectInstallers();
                if (installers.Count == 0)
                {
                    Debug.LogWarning("[FastIoC] No installers found. Skipping generation.");
                    return;
                }

                InstallerGraphCollector.ClearBindingInfoCache();
                Dictionary<Type, List<Type>> installerBindings = InstallerGraphCollector.AnalyzeInstallerBindings(installers);
                HashSet<Type> reachableTypes = CollectReachableTypes(installerBindings);

                // Filter out test types to prevent build errors
                reachableTypes = FilterOutTestTypes(reachableTypes);

                CoreFrameworkTypesRegistry.CoreTypes coreTypes = CoreFrameworkTypesRegistry.GetCoreFrameworkTypes();
                CoreFrameworkTypesRegistry.RegisterCoreFrameworkBindings(reachableTypes, installerBindings, coreTypes);

                ValidateDependencyGraph(reachableTypes, installerBindings);

                Dictionary<Type, Lifetime> typeLifetimes = TypeLifetimeBuilder.BuildTypeLifetimes(installerBindings, reachableTypes, coreTypes);
                Dictionary<Type, bool> instanceBindings = InstanceBindingsBuilder.BuildInstanceBindingsMap(installerBindings);
                Dictionary<Type, Type> interfaceImplementations = InterfaceImplementationMapBuilder.BuildInterfaceImplementationMap(coreTypes);

                HashSet<Type> typesForGeneration = BuildTypesForGeneration(reachableTypes, instanceBindings);
                string typeIdsCode = TypeIdEmitter.GenerateTypeIds(typesForGeneration, out Dictionary<Type, int> typeIdMap);
                string activatorsCode = ActivatorEmitter.GenerateActivators(reachableTypes, typeIdMap, typeLifetimes, instanceBindings, interfaceImplementations);

                HashSet<Type> allMonoBehaviourTypes = MonoBehaviourTypeCollector.CollectAllMonoBehaviourTypes(reachableTypes, interfaceImplementations, instanceBindings);
                string injectorCode = MonoInjectorEmitter.GenerateMonoInjectors(allMonoBehaviourTypes, typeIdMap, interfaceImplementations, instanceBindings);
                string injectableMonoBehavioursCode = InjectableMonoBehavioursEmitter.GenerateRegistry(allMonoBehaviourTypes);

                string registryCode = RegistryEmitter.GenerateRegistry(
                    reachableTypes,
                    typeIdMap,
                    typeLifetimes,
                    InstallerGraphCollector.CollectInstallersByScene(),
                    installerBindings,
                    installers);

                string instanceRegistryCode = InstanceRegistryEmitter.GenerateInstanceRegistry(typeIdMap);
                Dictionary<Type, BindingInfo> allInstanceBindings = InstallerGraphCollector.GetCachedBindingInfo();
                string instanceInitCode = InstanceRegistryPopulator.GenerateInstancePopulationCode(allInstanceBindings, typeIdMap);

                WriteGeneratedFiles(typeIdsCode, activatorsCode, injectorCode, injectableMonoBehavioursCode, registryCode, instanceRegistryCode, instanceInitCode);

                Debug.Log("[FastIoC] Code generation completed successfully");
                GenerationStatusWriter.WriteSuccessStatus();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Generation failed: {ex}");
                GenerationStatusWriter.WriteFailureStatus(
                    ex.Message,
                    lastValidationErrors);
            }
        }

        private static HashSet<Type> CollectReachableTypes(Dictionary<Type, List<Type>> installerBindings)
        {
            HashSet<Type> allScannedTypes = new();
            foreach (KeyValuePair<Type, List<Type>> kvp in installerBindings)
            {
                allScannedTypes.Add(kvp.Key);
                foreach (Type t in kvp.Value)
                    allScannedTypes.Add(t);
            }

            return ReachabilityPruner.PruneUnreachableTypes(installerBindings, allScannedTypes.ToList());
        }

        private static void ValidateDependencyGraph(
            HashSet<Type> reachableTypes,
            Dictionary<Type, List<Type>> installerBindings)
        {
            Dictionary<Type, DependencyDagValidator.DagNode> dag = DependencyDagValidator.BuildDag(reachableTypes);

            if (DependencyDagValidator.HasCycles(dag, out List<string> cycleErrors))
            {
                lastValidationErrors = cycleErrors;
                foreach (string error in cycleErrors)
                    Debug.LogError($"[FastIoC] {error}");

                throw new InvalidOperationException("Dependency graph contains cycles");
            }

            if (!DependencyDagValidator.ValidateMissingBindings(dag, installerBindings, out List<string> missingErrors))
            {
                lastValidationErrors = missingErrors;
                foreach (string error in missingErrors)
                    Debug.LogError($"[FastIoC] {error}");

                throw new InvalidOperationException("Dependency graph has missing bindings");
            }

            Debug.Log("[FastIoC] DAG validation passed");
        }

        private static HashSet<Type> BuildTypesForGeneration(
            HashSet<Type> reachableTypes,
            Dictionary<Type, bool> instanceBindings)
        {
            HashSet<Type> typesForGeneration = new(reachableTypes);

            foreach (Type instanceType in instanceBindings.Keys)
                typesForGeneration.Add(instanceType);

            return typesForGeneration;
        }

        private static HashSet<Type> FilterOutTestTypes(HashSet<Type> types)
        {
            HashSet<Type> filtered = new();

            foreach (Type type in types)
            {
                Assembly assembly = type.Assembly;
                string assemblyName = assembly.GetName().Name;

                // Exclude test assemblies by name pattern
                if (assemblyName.Contains("Test") || assemblyName.Contains("test"))
                {
                    Debug.Log($"[FastIoC] Filtering out test type: {type.FullName} from assembly {assemblyName}");
                    continue;
                }

                filtered.Add(type);
            }

            return filtered;
        }

        private static void WriteGeneratedFiles(
            string typeIdsCode,
            string activatorsCode,
            string injectorCode,
            string injectableMonoBehavioursCode,
            string registryCode,
            string instanceRegistryCode,
            string instanceInitCode)
        {
            string generatedPath = FastIoCConstants.GENERATED_PATH;
            EnsureDirectory(generatedPath);

            WriteFile(Path.Combine(generatedPath, "GeneratedTypeIds.cs"), typeIdsCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedActivators.cs"), activatorsCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedMonoInjectors.cs"), injectorCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedInjectableMonoBehaviours.cs"), injectableMonoBehavioursCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedRegistry.cs"), registryCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedInstanceRegistry.cs"), instanceRegistryCode);
            WriteFile(Path.Combine(generatedPath, "InstanceRegistryInitializer.cs"), instanceInitCode);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static void WriteFile(string path, string content)
        {
            File.WriteAllText(path, content);
            Debug.Log($"[FastIoC] Generated: {path}");
        }
    }
}
#endif

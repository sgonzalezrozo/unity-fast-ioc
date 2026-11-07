#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FastIoC.Editor.Graph;
using FastIoC.API;
using FastIoC.Core;
using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor.Generation
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
                    Debug.Log("[FastIoC] No installers yet. Add one to generate code.");
                    return;
                }

                InstallerGraphCollector.ClearBindingInfoCache();
                Dictionary<Type, List<Type>> installerBindings = InstallerGraphCollector.AnalyzeInstallerBindings(installers);
                HashSet<Type> reachableTypes = CollectReachableTypes(installerBindings);

                // Filter out test types to prevent build errors
                reachableTypes = FilterOutTestTypes(reachableTypes);

                CoreFrameworkTypesRegistry.CoreTypes coreTypes = CoreFrameworkTypesRegistry.GetCoreFrameworkTypes();
                CoreFrameworkTypesRegistry.RegisterCoreFrameworkBindings(reachableTypes, installerBindings, coreTypes);

                ValidateDependencyGraph(reachableTypes, coreTypes);

                Dictionary<Type, Lifetime> typeLifetimes = TypeLifetimeBuilder.BuildTypeLifetimes(installerBindings, reachableTypes, coreTypes);
                Dictionary<Type, bool> instanceBindings = InstanceBindingsBuilder.BuildInstanceBindingsMap(installerBindings);
                Dictionary<Type, Type> interfaceImplementations = InterfaceImplementationMapBuilder.BuildInterfaceImplementationMap(coreTypes);

                HashSet<Type> typesForGeneration = BuildTypesForGeneration(reachableTypes, instanceBindings);
                string typeIdsCode = TypeIdEmitter.GenerateTypeIds(typesForGeneration, out Dictionary<Type, int> typeIdMap);
                string activatorsCode = ActivatorEmitter.GenerateActivators(reachableTypes, typeIdMap, typeLifetimes, instanceBindings, interfaceImplementations);

                HashSet<Type> allMonoBehaviourTypes = MonoBehaviourTypeCollector.CollectAllMonoBehaviourTypes(reachableTypes, interfaceImplementations, instanceBindings);
                InternalsAccessPlan internalsAccess = InternalsAccessPlan.Create();
                MonoInjectorEmitter.Result injectorResult = MonoInjectorEmitter.GenerateMonoInjectors(allMonoBehaviourTypes, typeIdMap, interfaceImplementations, instanceBindings, internalsAccess);
                string injectorCode = injectorResult.Code;
                string moduleCode = GeneratedModuleEmitter.GenerateModule();

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

                WriteGeneratedFiles(typeIdsCode, activatorsCode, injectorCode, moduleCode, registryCode, instanceRegistryCode, instanceInitCode);
                internalsAccess.WriteFiles();
                ReportInjectorResult(injectorResult);

                Debug.Log("[FastIoC] Code generation completed successfully");
                GenerationStatusWriter.WriteSuccessStatus(UnboundFieldWarnings(injectorResult));
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
            CoreFrameworkTypesRegistry.CoreTypes coreTypes)
        {
            Dictionary<Type, BindingInfo> bindings = InstallerGraphCollector.GetCachedBindingInfo();
            Dictionary<Type, DependencyDagValidator.DagNode> dag = DependencyDagValidator.BuildDag(reachableTypes, bindings);

            if (DependencyDagValidator.HasCycles(dag, out List<string> cycleErrors))
            {
                lastValidationErrors = cycleErrors;
                foreach (string error in cycleErrors)
                    Debug.LogError($"[FastIoC] {error}");

                throw new InvalidOperationException("Dependency graph contains cycles");
            }

            HashSet<Type> frameworkTypes = new()
            {
                coreTypes.IInstantiator,
                coreTypes.InstantiationFactory,
                coreTypes.IPrefabInstantiator,
                coreTypes.FastIoCPrefabInstantiator,
                coreTypes.ContextRuntime
            };
            frameworkTypes.Remove(null);

            if (!DependencyDagValidator.ValidateMissingBindings(reachableTypes, bindings, frameworkTypes, out List<string> missingErrors))
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
            string moduleCode,
            string registryCode,
            string instanceRegistryCode,
            string instanceInitCode)
        {
            string generatedPath = FastIoCConstants.GENERATED_PATH;
            EnsureDirectory(generatedPath);
            DeleteObsoleteGeneratedFiles(generatedPath);

            WriteFile(Path.Combine(generatedPath, "GeneratedTypeIds.cs"), typeIdsCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedActivators.cs"), activatorsCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedMonoInjectors.cs"), injectorCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedModule.cs"), moduleCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedRegistry.cs"), registryCode);
            WriteFile(Path.Combine(generatedPath, "GeneratedInstanceRegistry.cs"), instanceRegistryCode);
            WriteFile(Path.Combine(generatedPath, "InstanceRegistryInitializer.cs"), instanceInitCode);
        }

        private static readonly string[] ObsoleteGeneratedFiles =
        {
            // Earlier versions: the runtime found this list by reflection. Scene manifests and GeneratedModule replace it.
            "GeneratedInjectableMonoBehaviours.cs"
        };

        private static void DeleteObsoleteGeneratedFiles(string generatedPath)
        {
            foreach (string fileName in ObsoleteGeneratedFiles)
            {
                string path = Path.Combine(generatedPath, fileName).Replace('\\', '/');
                if (!File.Exists(path))
                    continue;

                AssetDatabase.DeleteAsset(path);
                if (File.Exists(path))
                    File.Delete(path);

                Debug.Log($"[FastIoC] Deleted obsolete generated file: {path}");
            }
        }

        private static void ReportInjectorResult(MonoInjectorEmitter.Result result)
        {
            Debug.Log($"[FastIoC] MonoBehaviour injectors: {result.InjectorCount} container, {result.LocalInjectorCount} local, {result.DataInjectorCount} data.");

            if (result.SkippedTypes.Count > 0)
            {
                Debug.LogWarning("[FastIoC] These MonoBehaviours have injectable fields but generated code can't reference their type " +
                                 "(make the type public, or add [assembly: InternalsVisibleTo(\"Assembly-CSharp\")] to its assembly):\n  • " +
                                 string.Join("\n  • ", result.SkippedTypes));
            }

            if (result.UnboundFields.Count > 0)
            {
                Debug.LogWarning($"[FastIoC] {result.UnboundFields.Count} `internal` field(s) have a service type that isn't bound in any installer, " +
                                 "so they stay null. Bind the type, or ignore this if you assign the field yourself:\n  • " +
                                 string.Join("\n  • ", result.UnboundFields));
            }

            if (result.IgnoredPrivateFields.Count > 0)
            {
                Debug.LogWarning("[FastIoC] These private fields have a bound type but aren't injected, because only `internal` fields are. " +
                                 "Make a field `internal` if it's a dependency; ignore this if it isn't:\n  • " +
                                 string.Join("\n  • ", result.IgnoredPrivateFields));
            }

            if (result.ReflectionFallbackFields.Count > 0)
            {
                Debug.LogWarning($"[FastIoC] {result.ReflectionFallbackFields.Count} injected field(s) still use reflection. " +
                                 "Add [assembly: InternalsVisibleTo(\"Assembly-CSharp\")] to their assemblies to remove it:\n  • " +
                                 string.Join("\n  • ", result.ReflectionFallbackFields));
            }
            else
            {
                Debug.Log("[FastIoC] MonoBehaviour injection uses no reflection.");
            }
        }

        private static List<string> UnboundFieldWarnings(MonoInjectorEmitter.Result result)
        {
            List<string> warnings = new(result.UnboundFields.Count);
            foreach (string field in result.UnboundFields)
                warnings.Add($"{field} isn't bound in any installer, so it stays null.");
            return warnings;
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

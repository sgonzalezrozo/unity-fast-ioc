#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FastIoC.Editor.Graph;
using FastIoC.Editor.Validation;
using FastIoC.API;
using FastIoC.Core;
using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    public static class GenerationCoordinator
    {
        private static List<string> lastValidationErrors;

        /// <summary>
        /// Generates again even if the result is code that failed to compile before. Manual regeneration uses it:
        /// the cause may be fixed without changing the generated code, for example by making a type accessible.
        /// If it still doesn't compile, <see cref="GeneratedCodeRecovery"/> removes it again; nothing loops,
        /// because only a manual run takes this path.
        /// </summary>
        public static void GenerateAllRetryingFailedCode()
        {
            GeneratedCodeRecovery.Clear();
            GenerateAll();
        }

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

                ValidateConstructors(reachableTypes, instanceBindings);

                HashSet<Type> typesForGeneration = BuildTypesForGeneration(reachableTypes, instanceBindings);
                string typeIdsCode = TypeIdEmitter.GenerateTypeIds(typesForGeneration, out Dictionary<Type, int> typeIdMap);
                string activatorsCode = ActivatorEmitter.GenerateActivators(reachableTypes, typeIdMap, typeLifetimes, instanceBindings, interfaceImplementations);

                HashSet<Type> allMonoBehaviourTypes = MonoBehaviourTypeCollector.CollectAllMonoBehaviourTypes(reachableTypes, interfaceImplementations, instanceBindings);
                InternalsAccessPlan internalsAccess = InternalsAccessPlan.Create();
                MonoInjectorEmitter.Result injectorResult = MonoInjectorEmitter.GenerateMonoInjectors(allMonoBehaviourTypes, typeIdMap, interfaceImplementations, instanceBindings, internalsAccess);
                string injectorCode = injectorResult.Code;

                string registryCode = RegistryEmitter.GenerateRegistry(
                    reachableTypes,
                    typeIdMap,
                    typeLifetimes,
                    InstallerGraphCollector.CollectInstallersByScene(),
                    installerBindings,
                    installers);

                string instanceRegistryCode = InstanceRegistryEmitter.GenerateInstanceRegistry(typeIdMap);
                Dictionary<Type, BindingInfo> allInstanceBindings = InstallerGraphCollector.GetCachedBindingInfo();

                // Build the global-interface set for path-binding scope checks.
                HashSet<Type> globalInterfaceTypes = BuildGlobalInterfaceTypes(
                    allInstanceBindings, InstallerGraphCollector.CollectInstallersByScene());

                // Validate path bindings (assigns manifest indices) and write the manifest asset.
                Dictionary<Type, AssetPathValidator.AssetInfo> validatedAssets =
                    ValidateAndWriteManifest(allInstanceBindings, globalInterfaceTypes);

                string instanceInitCode = InstanceRegistryPopulator.GenerateInstancePopulationCode(
                    allInstanceBindings, typeIdMap, validatedAssets);

                Dictionary<string, List<Type>> installersByScene = InstallerGraphCollector.CollectInstallersByScene();

                List<string> nonLazyTypeIdNames = CollectNonLazyTypeIds(
                    allInstanceBindings,
                    interfaceImplementations,
                    typeIdMap,
                    installersByScene);

                List<(string contract, string typeIdMember)> globalExports = CollectGlobalBindingExports(
                    allInstanceBindings,
                    interfaceImplementations,
                    typeIdMap,
                    installersByScene);

                string moduleCode = GeneratedModuleEmitter.GenerateModule(nonLazyTypeIdNames, globalExports);

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

        /// <summary>Returns the set of interface types whose installer is in the global scope.</summary>
        private static HashSet<Type> BuildGlobalInterfaceTypes(
            Dictionary<Type, BindingInfo> allBindings,
            Dictionary<string, List<Type>> installersByScene)
        {
            HashSet<Type> globalInstallers = new();
            if (installersByScene.TryGetValue(FastIoCConstants.GLOBAL_SCOPE, out List<Type> globals))
                foreach (Type t in globals) globalInstallers.Add(t);

            HashSet<Type> result = new();
            foreach (KeyValuePair<Type, BindingInfo> kvp in allBindings)
            {
                Type owner = InstallerGraphCollector.GetBindingOwner(kvp.Key);
                if (owner != null && globalInstallers.Contains(owner))
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>
        /// Validates all path bindings and writes/updates the AssetManifest.
        /// Returns the validated assets (with manifest indices) for use by the code emitters.
        /// If there are no path bindings, returns an empty dictionary without touching the manifest.
        /// </summary>
        private static Dictionary<Type, AssetPathValidator.AssetInfo> ValidateAndWriteManifest(
            Dictionary<Type, BindingInfo> allBindings,
            HashSet<Type> globalInterfaceTypes)
        {
            bool hasPathBindings = allBindings.Values.Any(b => b.IsPathBinding);
            if (!hasPathBindings)
            {
                // No path bindings — ensure any stale manifest is removed.
                AssetManifestWriter.WriteManifest(new Dictionary<Type, AssetPathValidator.AssetInfo>());
                return new Dictionary<Type, AssetPathValidator.AssetInfo>();
            }

            Dictionary<Type, AssetPathValidator.AssetInfo> validated =
                AssetPathValidator.ValidatePathBindings(allBindings, globalInterfaceTypes);
            AssetManifestWriter.WriteManifest(validated);
            return validated;
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

            // Validate FromProjectContext() bindings before writing any files.
            FastIoC.Editor.Validation.HostedComponentValidator.Validate(
                InstallerGraphCollector.GetCachedBindingInfo(),
                InstallerGraphCollector.CollectInstallersByScene());
        }

        private static void ValidateConstructors(HashSet<Type> reachableTypes, Dictionary<Type, bool> instanceBindings)
        {
            List<string> errors = DependencyDagValidator.FindTypesWithoutPublicConstructor(reachableTypes, instanceBindings);
            if (errors.Count == 0)
                return;

            lastValidationErrors = errors;
            foreach (string error in errors)
                Debug.LogError($"[FastIoC] {error}");

            throw new InvalidOperationException("Some bound types have no public constructor");
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

        /// <summary>
        /// Returns the ordered list of GeneratedTypeIds member names for NonLazy bindings.
        /// Validates that no NonLazy binding lives in a scene-specific installer.
        /// Order: installer Priority desc, then DeclarationOrder within the same priority.
        /// </summary>
        internal static List<string> CollectNonLazyTypeIds(
            Dictionary<Type, BindingInfo> allBindings,
            Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, int> typeIdMap,
            Dictionary<string, List<Type>> installersByScene)
        {
            ValidateNonLazyBindings(allBindings, installersByScene);

            // Gather (installerPriority, declarationOrder, concreteTypeIdName) for each NonLazy global binding.
            List<(int priority, int order, string name)> entries = new();

            foreach (KeyValuePair<Type, BindingInfo> kvp in allBindings)
            {
                BindingInfo info = kvp.Value;
                if (!info.IsNonLazy)
                    continue;

                // Resolve to the concrete TypeId: use the impl type, or fall back through interfaceImplementations.
                Type concreteType = info.ImplementationType;
                if (!typeIdMap.ContainsKey(concreteType) &&
                    interfaceImplementations != null &&
                    interfaceImplementations.TryGetValue(concreteType, out Type mapped))
                {
                    concreteType = mapped;
                }

                if (!typeIdMap.ContainsKey(concreteType))
                    continue; // type was filtered out (test type, etc.)

                string typeIdName = CodeGenTypeUtility.GetTypeIdMemberName(concreteType);

                int installerPriority = GetInstallerPriority(kvp.Key, installersByScene);
                entries.Add((installerPriority, info.DeclarationOrder, typeIdName));
            }

            // Sort: Priority desc, then DeclarationOrder asc.
            entries.Sort((a, b) =>
            {
                int cmp = b.priority.CompareTo(a.priority);
                return cmp != 0 ? cmp : a.order.CompareTo(b.order);
            });

            // De-duplicate concrete type names while preserving order (a concrete type bound to several interfaces
            // should only be created once).
            HashSet<string> seen = new();
            List<string> result = new();
            foreach ((_, _, string name) in entries)
            {
                if (seen.Add(name))
                    result.Add(name);
            }

            return result;
        }

        /// <summary>
        /// Gathers (contract C# name, concrete TypeId member) for every global binding so the bridge can hand
        /// each contract to another container. Scene-scoped bindings and framework/auto bindings (no user
        /// installer) are excluded. Each contract resolves through its concrete TypeId, so an interface and
        /// its self-binding map to the same instance.
        /// </summary>
        internal static List<(string contract, string typeIdMember)> CollectGlobalBindingExports(
            Dictionary<Type, BindingInfo> allBindings,
            Dictionary<Type, Type> interfaceImplementations,
            Dictionary<Type, int> typeIdMap,
            Dictionary<string, List<Type>> installersByScene)
        {
            installersByScene.TryGetValue(FastIoCConstants.GLOBAL_SCOPE, out List<Type> globalInstallers);

            List<(string contract, string typeIdMember)> exports = new();

            foreach (KeyValuePair<Type, BindingInfo> kvp in allBindings)
            {
                Type owner = InstallerGraphCollector.GetBindingOwner(kvp.Key);
                if (owner == null)
                    continue; // framework / auto binding, not a user global binding

                if (globalInstallers == null || !globalInstallers.Contains(owner))
                    continue; // scene-scoped binding

                Type concreteType = kvp.Value.ImplementationType;
                if (!typeIdMap.ContainsKey(concreteType) &&
                    interfaceImplementations != null &&
                    interfaceImplementations.TryGetValue(concreteType, out Type mapped))
                {
                    concreteType = mapped;
                }

                if (!typeIdMap.ContainsKey(concreteType))
                    continue; // type was filtered out (test type, etc.)

                exports.Add((CodeGenTypeUtility.GetCSharpName(kvp.Key), CodeGenTypeUtility.GetTypeIdMemberName(concreteType)));
            }

            return exports;
        }

        /// <summary>Throws if any NonLazy binding belongs to a scene-specific installer.</summary>
        internal static void ValidateNonLazyBindings(
            Dictionary<Type, BindingInfo> allBindings,
            Dictionary<string, List<Type>> installersByScene)
        {
            foreach (KeyValuePair<Type, BindingInfo> kvp in allBindings)
            {
                if (!kvp.Value.IsNonLazy)
                    continue;

                Type owner = InstallerGraphCollector.GetBindingOwner(kvp.Key);
                if (owner == null)
                    continue;

                // Check whether the installer is ONLY in scene scopes (not global).
                bool isGlobal = false;
                if (installersByScene.TryGetValue(FastIoCConstants.GLOBAL_SCOPE, out List<Type> globalInstallers))
                    isGlobal = globalInstallers.Contains(owner);

                if (!isGlobal)
                {
                    throw new InvalidOperationException(
                        $"[FastIoC] NonLazy() is only supported on global installers. " +
                        $"Type '{kvp.Key.FullName}' is NonLazy but its installer '{owner.Name}' is scene-specific. " +
                        $"Remove .NonLazy() or move the binding to a global installer.");
                }
            }
        }

        private static int GetInstallerPriority(Type boundInterfaceType, Dictionary<string, List<Type>> installersByScene)
        {
            Type owner = InstallerGraphCollector.GetBindingOwner(boundInterfaceType);
            if (owner == null)
                return 0;

            MonoInstallerAttribute attr = owner.GetCustomAttribute<MonoInstallerAttribute>();
            return attr?.Priority ?? 0;
        }

        /// <summary>The generated sources, in write order. <see cref="GeneratedCodeRecovery"/> hashes them in this order.</summary>
        internal static readonly string[] GeneratedSourceFiles =
        {
            "GeneratedTypeIds.cs",
            "GeneratedActivators.cs",
            "GeneratedMonoInjectors.cs",
            "GeneratedModule.cs",
            "GeneratedRegistry.cs",
            "GeneratedInstanceRegistry.cs",
            "InstanceRegistryInitializer.cs",
        };

        private static void WriteGeneratedFiles(
            string typeIdsCode,
            string activatorsCode,
            string injectorCode,
            string moduleCode,
            string registryCode,
            string instanceRegistryCode,
            string instanceInitCode)
        {
            // Same order as GeneratedSourceFiles.
            string[] sources = { typeIdsCode, activatorsCode, injectorCode, moduleCode, registryCode, instanceRegistryCode, instanceInitCode };

            // Writing code that already failed to compile would lock the project again: see GeneratedCodeRecovery.
            if (GeneratedCodeRecovery.IsKnownToFail(GeneratedCodeRecovery.ComputeCodeHash(sources)))
            {
                lastValidationErrors = GeneratedCodeRecovery.FailedCodeErrors;
                throw new InvalidOperationException(
                    "This is the same generated code that failed to compile, so it wasn't written again. " +
                    "Fix the cause listed in errorDetails; the next change regenerates.");
            }

            string generatedPath = FastIoCConstants.GENERATED_PATH;
            EnsureDirectory(generatedPath);
            DeleteObsoleteGeneratedFiles(generatedPath);

            for (int i = 0; i < GeneratedSourceFiles.Length; i++)
                WriteFile(Path.Combine(generatedPath, GeneratedSourceFiles[i]), sources[i]);

            GeneratedCodeRecovery.Clear();
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
                const int shown = 20;
                string list = string.Join("\n  • ", result.UnboundFields.Take(shown));
                if (result.UnboundFields.Count > shown)
                    list += $"\n  … and {result.UnboundFields.Count - shown} more, listed in GenerationStatus.json";

                Debug.LogWarning($"[FastIoC] {result.UnboundFields.Count} `internal` field(s) have a service type that isn't bound in any installer, " +
                                 "so they stay null. Bind the type, or ignore this if you assign the field yourself:\n  • " + list);
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
            // Unchanged files are left alone, so regenerating the same code doesn't trigger a recompile.
            if (File.Exists(path) && File.ReadAllText(path) == content)
                return;

            File.WriteAllText(path, content);
            GeneratedCodeBuildGuard.MarkGeneratedCodeChanged();
            Debug.Log($"[FastIoC] Generated: {path}");
        }
    }
}
#endif

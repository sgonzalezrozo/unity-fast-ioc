#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastIoC.API;
using FastIoC.Editor.Graph;
using FastIoC.Editor.Validation;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    public static class InstanceRegistryPopulator
    {
        public static string GenerateInstancePopulationCode(
            Dictionary<Type, BindingInfo> bindings,
            Dictionary<Type, int> typeIdMap,
            Dictionary<Type, FastIoC.Editor.Validation.AssetPathValidator.AssetInfo> validatedAssets = null)
        {
            StringBuilder sb = new();
            
            sb.AppendLine("using System;");
            sb.AppendLine("using FastIoC.Core;");
            sb.AppendLine("using FastIoC.API;");
            sb.AppendLine("using UnityEngine;");

            // Collect namespaces for instance binding types
            HashSet<string> namespaces = new();
            foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
            {
                if (binding.Value.IsInstance && !string.IsNullOrEmpty(binding.Key.Namespace))
                {
                    namespaces.Add(binding.Key.Namespace);
                }
            }

            foreach (string ns in namespaces)
                sb.AppendLine($"using {ns};");

            sb.AppendLine();
            sb.AppendLine("namespace FastIoCGenerated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Populates the InstanceRegistry with pre-created instances during runtime initialization.");
            sb.AppendLine("    /// Supports path-based asset bindings - FromInstance(\"path/to/asset.prefab\")");
            sb.AppendLine("    /// All bindings are AOT (Ahead-of-Time) compiled at edit time via BindingRecorder.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class InstanceRegistryInitializer");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Called from ContextRuntime.Initialize() to populate instance bindings.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static void InitializeInstances()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                // Get ProjectContext instance");
            sb.AppendLine("                ProjectContext projectContext = ProjectContext.Instance;");
            sb.AppendLine("                if (projectContext == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    Debug.LogWarning(\"[FastIoC] ProjectContext not found during instance initialization\");");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                // Instance bindings are loaded via path-based assets only (AOT - no reflection)");
            sb.AppendLine("                // Components are injected later by SceneInjector");
              // Add path-based asset initialization code
              string pathBasedInitCode = PathBasedAssetInitializer.GeneratePathBasedAssetInitCode(bindings, typeIdMap, validatedAssets);
              if (!string.IsNullOrEmpty(pathBasedInitCode))
              {
                  sb.Append(pathBasedInitCode);
                  sb.AppendLine();
              }

              AppendValueBindings(sb, bindings, typeIdMap);
              AppendHostedComponentBindings(sb, bindings, typeIdMap);

              sb.AppendLine("            }");
              sb.AppendLine("            catch (Exception ex)");
              sb.AppendLine("            {");
              sb.AppendLine("                Debug.LogError($\"[FastIoC] Failed to initialize instances: {ex.Message}\\n{ex.StackTrace}\");");
              sb.AppendLine("            }");
              sb.AppendLine("        }");
              sb.AppendLine("    }");
              sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Generates the code that restores FromInstance(value) bindings. Values can't be saved
        /// at edit time, so each installer that declares one runs once more at startup.
        /// </summary>
        private static void AppendValueBindings(StringBuilder sb, Dictionary<Type, BindingInfo> bindings, Dictionary<Type, int> typeIdMap)
        {
            Dictionary<Type, List<BindingInfo>> bindingsByInstaller = new();

            foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
            {
                if (!binding.Value.IsInstance || binding.Value.IsPathBinding || binding.Value.IsProjectContextBinding)
                    continue;

                Type installerType = InstallerGraphCollector.GetBindingOwner(binding.Key);
                if (installerType == null || !typeIdMap.ContainsKey(binding.Value.ImplementationType))
                    continue;

                if (!bindingsByInstaller.TryGetValue(installerType, out List<BindingInfo> installerBindings))
                {
                    installerBindings = new List<BindingInfo>();
                    bindingsByInstaller[installerType] = installerBindings;
                }

                installerBindings.Add(binding.Value);
            }

            foreach (KeyValuePair<Type, List<BindingInfo>> installer in bindingsByInstaller.OrderBy(kvp => kvp.Key.FullName))
            {
                sb.AppendLine($"                // Instance bindings from {installer.Key.FullName}");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {CodeName(installer.Key)} installer = new {CodeName(installer.Key)}();");
                sb.AppendLine("                    installer.InstallBindings();");
                sb.AppendLine("                    var recordedBindings = installer.GetBindingsForGeneration();");

                foreach (BindingInfo binding in installer.Value.OrderBy(info => info.InterfaceType.FullName))
                {
                    string typeIdName = binding.ImplementationType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                    sb.AppendLine($"                    GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, recordedBindings[typeof({CodeName(binding.InterfaceType)})].InstanceValue);");
                }

                sb.AppendLine("                }");
                sb.AppendLine();
            }
        }

        private static string CodeName(Type type)
        {
            return "global::" + type.FullName?.Replace("+", ".");
        }

        /// <summary>
        /// Emits one <c>GetComponentInChildren</c> call per <c>FromProjectContext()</c> binding.
        /// Uses the concrete impl type so no reflection is needed at runtime.
        /// </summary>
        private static void AppendHostedComponentBindings(
            StringBuilder sb,
            Dictionary<Type, BindingInfo> bindings,
            Dictionary<Type, int> typeIdMap)
        {
            bool hasAny = false;
            foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
            {
                if (!binding.Value.IsProjectContextBinding)
                    continue;

                Type implType = binding.Value.ImplementationType;
                if (!typeIdMap.ContainsKey(implType))
                    continue;

                if (!hasAny)
                {
                    sb.AppendLine("                // Hosted component bindings (FromProjectContext)");
                    sb.AppendLine("                global::UnityEngine.GameObject hostedRoot = projectContext.HostedRoot;");
                    sb.AppendLine("                if (hostedRoot != null)");
                    sb.AppendLine("                {");
                    hasAny = true;
                }

                string typeIdName = implType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";
                string implName = CodeName(implType);
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        {implName} component = hostedRoot.GetComponentInChildren<{implName}>(true);");
                sb.AppendLine($"                        if (component != null)");
                sb.AppendLine($"                            GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, component);");
                sb.AppendLine($"                        else");
                sb.AppendLine($"                            global::UnityEngine.Debug.LogError(\"[FastIoC] No {implType.Name} found under hostedRoot\");");
                sb.AppendLine($"                    }}");
            }

            if (hasAny)
                sb.AppendLine("                }");
        }

        /// <summary>
        /// Collects all instance bindings from all installers (BindingRecorder only).
        /// This includes path-based bindings from BindingRecorder instances.
        /// </summary>
        public static Dictionary<Type, BindingInfo> CollectInstanceBindings(List<Type> allInstallerTypes = null)
        {
            Dictionary<Type, BindingInfo> allBindings = new();

            if (allInstallerTypes == null)
            {
                allInstallerTypes = new();
            }

            // Collect from ALL discovered installers (BindingRecorder instances with path-based bindings)
            foreach (Type installerType in allInstallerTypes)
            {
                Debug.Log($"[FastIoC] CollectInstanceBindings: Processing {installerType.Name}");

                // Skip MonoBehaviours (they should use [MonoInstaller] attribute discovery, not reflection)
                if (typeof(MonoBehaviour).IsAssignableFrom(installerType))
                {
                    Debug.Log($"[FastIoC]   SKIP: {installerType.Name} is a MonoBehaviour");
                    continue;
                }

                try
                {
                    // Try to instantiate the installer to call GetBindingsForGeneration
                    object installerInstance = null;

                    if (typeof(BindingRecorder).IsAssignableFrom(installerType) && !typeof(MonoBehaviour).IsAssignableFrom(installerType))
                    {
                        Debug.Log($"[FastIoC]   Creating instance of BindingRecorder: {installerType.Name}");
                        // BindingRecorder - can instantiate via reflection
                        installerInstance = Activator.CreateInstance(installerType);
                    }

                    if (installerInstance != null)
                    {
                        Debug.Log($"[FastIoC]   Getting bindings from {installerType.Name}");
                        System.Reflection.MethodInfo getBindingsMethod = installerType.GetMethod("GetBindingsForGeneration",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                        if (getBindingsMethod != null)
                        {
                            Dictionary<Type, BindingInfo> bindings =
                                getBindingsMethod.Invoke(installerInstance, null) as Dictionary<Type, BindingInfo>;

                            if (bindings != null)
                            {
                                Debug.Log($"[FastIoC]   Found {bindings.Count} bindings in {installerType.Name}");
                                foreach (KeyValuePair<Type, BindingInfo> binding in bindings)
                                {
                                    Debug.Log($"[FastIoC]     - {binding.Key.Name}: IsPathBinding={binding.Value.IsPathBinding}, AssetPath={binding.Value.AssetPath}");
                                    // Don't override if already found
                                    if (!allBindings.ContainsKey(binding.Key))
                                    {
                                        allBindings[binding.Key] = binding.Value;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FastIoC] Could not collect instance bindings from installer {installerType.Name}: {ex.Message}");
                }
            }

            return allBindings;
        }
    }
}
#endif



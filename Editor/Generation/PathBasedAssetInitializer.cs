#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Tools.FastIoC.Editor.Validation;
using Tools.FastIoC.Runtime.API;

namespace Tools.FastIoC.Editor.Generation
{
    public static class PathBasedAssetInitializer
    {
        private const string RESOURCES_PREFIX = "Assets/Resources/";
        private const string PREFAB_EXTENSION = ".prefab";
        private const string ASSET_EXTENSION = ".asset";

        public static string GeneratePathBasedAssetInitCode(
            Dictionary<Type, BindingInfo> allBindings,
            Dictionary<Type, int> typeIdMap)
        {
            StringBuilder sb = new();

            List<BindingInfo> pathBindings = new();
            foreach (KeyValuePair<Type, BindingInfo> binding in allBindings)
            {
                if (binding.Value.IsPathBinding)
                    pathBindings.Add(binding.Value);
            }

            if (pathBindings.Count == 0)
                return "";

            sb.AppendLine("            // Initialize path-based asset bindings");
            sb.AppendLine();

            Dictionary<Type, AssetPathValidator.AssetInfo> validatedAssets;
            try
            {
                validatedAssets = AssetPathValidator.ValidatePathBindings(allBindings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[FastIoC] Path-based binding validation failed: {ex.Message}", ex);
            }

            foreach (BindingInfo binding in pathBindings)
            {
                Type interfaceType = binding.InterfaceType;

                if (!validatedAssets.ContainsKey(interfaceType))
                    continue;

                AssetPathValidator.AssetInfo assetInfo = validatedAssets[interfaceType];
                string typeIdName = interfaceType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";

                sb.AppendLine($"            // Load asset binding: {interfaceType.FullName} ({assetInfo.LoadingStrategy})");
                sb.AppendLine($"            {{");

                if (assetInfo.LoadingStrategy == AssetPathValidator.LoadingStrategy.Resources)
                    GenerateResourcesLoadCode(sb, assetInfo, typeIdName);
                else if (assetInfo.LoadingStrategy == AssetPathValidator.LoadingStrategy.Addressables)
                    GenerateAddressablesLoadCode(sb, assetInfo, typeIdName);

                sb.AppendLine($"            }}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates code to load from Resources folder.
        /// </summary>
        private static void GenerateResourcesLoadCode(
            StringBuilder sb,
            AssetPathValidator.AssetInfo assetInfo,
            string typeIdName)
        {
            string resourcesPath = assetInfo.AssetPath;
            if (resourcesPath.StartsWith(RESOURCES_PREFIX))
                resourcesPath = resourcesPath.Substring(RESOURCES_PREFIX.Length);
            if (resourcesPath.EndsWith(PREFAB_EXTENSION))
                resourcesPath = resourcesPath.Substring(0, resourcesPath.Length - PREFAB_EXTENSION.Length);
            if (resourcesPath.EndsWith(ASSET_EXTENSION))
                resourcesPath = resourcesPath.Substring(0, resourcesPath.Length - ASSET_EXTENSION.Length);

            if (assetInfo.IsScriptableObject)
            {
                sb.AppendLine($"                UnityEngine.Object asset = Resources.Load(\"{resourcesPath}\");");
                sb.AppendLine($"                if (asset != null)");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, asset);");
                sb.AppendLine($"                    Debug.Log(\"[FastIoC] Loaded ScriptableObject: {assetInfo.InterfaceType.Name} from Resources/{resourcesPath}\");");
                sb.AppendLine($"                }}");
                sb.AppendLine($"                else");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    Debug.LogError(\"[FastIoC] Failed to load ScriptableObject at path: Resources/{resourcesPath}\");");
                sb.AppendLine($"                }}");
            }
            else if (assetInfo.IsPrefab)
            {
                sb.AppendLine($"                GameObject prefab = Resources.Load<GameObject>(\"{resourcesPath}\");");
                sb.AppendLine($"                if (prefab != null)");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    bool prefabWasActive = prefab.activeSelf;");
                sb.AppendLine($"                    prefab.SetActive(false);");
                sb.AppendLine($"                    try");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        GameObject instance = UnityEngine.Object.Instantiate(prefab);");
                sb.AppendLine($"                        instance.name = prefab.name;");
                sb.AppendLine($"                        {assetInfo.ComponentType.FullName} component = instance.GetComponent<{assetInfo.ComponentType.Name}>();");
                sb.AppendLine($"                        if (component != null)");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, component);");
                sb.AppendLine($"                            try");
                sb.AppendLine($"                            {{");
                sb.AppendLine($"                                SceneInjector.InjectIfNeeded(component);");
                sb.AppendLine($"                            }}");
                sb.AppendLine($"                            catch");
                sb.AppendLine($"                            {{");
                sb.AppendLine($"                            }}");
                sb.AppendLine($"                            instance.SetActive(true);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                        else");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            Debug.LogError(\"[FastIoC] Instantiated prefab missing component {assetInfo.ComponentType.Name}\");");
                sb.AppendLine($"                            UnityEngine.Object.Destroy(instance);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                    finally");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        prefab.SetActive(prefabWasActive);");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                }}");
                sb.AppendLine($"                else");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    Debug.LogError(\"[FastIoC] Failed to load prefab from Resources/{resourcesPath}\");");
                sb.AppendLine($"                }}");
            }
        }

        private static void GenerateAddressablesLoadCode(
            StringBuilder sb,
            AssetPathValidator.AssetInfo assetInfo,
            string typeIdName)
        {
            string addressKey = string.IsNullOrEmpty(assetInfo.Address) ? assetInfo.AssetPath : assetInfo.Address;

            if (assetInfo.IsScriptableObject)
            {
                sb.AppendLine($"                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<UnityEngine.Object>(\"{addressKey}\");");
                sb.AppendLine($"                handle.Completed += (result) => {{");
                sb.AppendLine($"                    if (result.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, result.Result);");
                sb.AppendLine($"                        Debug.Log(\"[FastIoC] Loaded Addressable ScriptableObject: {assetInfo.InterfaceType.Name} ({addressKey})\");");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                    else");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        Debug.LogError($\"[FastIoC] Failed to load Addressable: {addressKey} - {{result.OperationException}}\");");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                }};");
            }
            else if (assetInfo.IsPrefab)
            {
                sb.AppendLine($"                var handle = UnityEngine.AddressableAssets.Addressables.InstantiateAsync(\"{addressKey}\");");
                sb.AppendLine($"                handle.Completed += (result) => {{");
                sb.AppendLine($"                    if (result.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        GameObject instance = result.Result;");
                sb.AppendLine($"                        {assetInfo.ComponentType.FullName} component = instance.GetComponent<{assetInfo.ComponentType.Name}>();");
                sb.AppendLine($"                        if (component != null)");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, component);");
                sb.AppendLine($"                            try");
                sb.AppendLine($"                            {{");
                sb.AppendLine($"                                SceneInjector.InjectIfNeeded(component);");
                sb.AppendLine($"                            }}");
                sb.AppendLine($"                            catch");
                sb.AppendLine($"                            {{");
                sb.AppendLine($"                            }}");
                sb.AppendLine($"                            instance.SetActive(true);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                        else");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            Debug.LogError(\"[FastIoC] Addressable prefab missing component {assetInfo.ComponentType.Name}\");");
                sb.AppendLine($"                            UnityEngine.Object.Destroy(instance);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                    else");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        Debug.LogError($\"[FastIoC] Failed to instantiate Addressable: {addressKey} - {{result.OperationException}}\");");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                }};");
            }
        }
    }
}
#endif


#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastIoC.Editor.Validation;
using FastIoC.API;

namespace FastIoC.Editor.Generation
{
    public static class PathBasedAssetInitializer
    {
        private const string PREFAB_EXTENSION = ".prefab";
        private const string ASSET_EXTENSION = ".asset";

        /// <param name="validatedAssets">Pre-validated asset infos from <see cref="AssetPathValidator.ValidatePathBindings"/>.</param>
        public static string GeneratePathBasedAssetInitCode(
            Dictionary<Type, BindingInfo> allBindings,
            Dictionary<Type, int> typeIdMap,
            Dictionary<Type, AssetPathValidator.AssetInfo> validatedAssets)
        {
            if (validatedAssets == null || validatedAssets.Count == 0)
                return "";

            StringBuilder sb = new();

            bool hasManifest = validatedAssets.Values.Any(a => a.LoadingStrategy == AssetPathValidator.LoadingStrategy.Manifest);
            if (hasManifest)
            {
                sb.AppendLine("            // Load asset manifest for bindings outside Resources and Addressables");
                sb.AppendLine("            var fastIoCManifest = global::UnityEngine.Resources.Load<global::FastIoC.API.FastIoCAssetManifest>(\"Tools/FastIoC/AssetManifest\");");
                sb.AppendLine("            if (fastIoCManifest == null) { global::UnityEngine.Debug.LogError(\"[FastIoC] AssetManifest not found. Run Tools > FastIoC > Regenerate Code to rebuild it.\"); return; }");
                sb.AppendLine();
            }

            sb.AppendLine("            // Initialize path-based asset bindings");
            sb.AppendLine();

            foreach (KeyValuePair<Type, BindingInfo> binding in allBindings)
            {
                if (!binding.Value.IsPathBinding)
                    continue;

                Type interfaceType = binding.Key;
                if (!validatedAssets.TryGetValue(interfaceType, out AssetPathValidator.AssetInfo assetInfo))
                    continue;

                string typeIdName = interfaceType.FullName?.Replace(".", "_").Replace("+", "_") ?? "Unknown";

                sb.AppendLine($"            // {interfaceType.FullName} ({assetInfo.LoadingStrategy})");
                sb.AppendLine("            {");

                switch (assetInfo.LoadingStrategy)
                {
                    case AssetPathValidator.LoadingStrategy.Resources:
                        GenerateResourcesLoadCode(sb, assetInfo, typeIdName);
                        break;
                    case AssetPathValidator.LoadingStrategy.Addressables:
                        GenerateAddressablesLoadCode(sb, assetInfo, typeIdName);
                        break;
                    case AssetPathValidator.LoadingStrategy.Manifest:
                        GenerateManifestLoadCode(sb, assetInfo, typeIdName);
                        break;
                }

                sb.AppendLine("            }");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void GenerateResourcesLoadCode(
            StringBuilder sb,
            AssetPathValidator.AssetInfo assetInfo,
            string typeIdName)
        {
            AssetPathValidator.TryGetResourcesLoadPath(assetInfo.AssetPath, out string resourcesPath);

            if (assetInfo.IsScriptableObject)
            {
                sb.AppendLine($"                global::UnityEngine.Object asset = global::UnityEngine.Resources.Load(\"{resourcesPath}\");");
                sb.AppendLine($"                if (asset != null)");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, asset);");
                sb.AppendLine($"                }}");
                sb.AppendLine($"                else");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    global::UnityEngine.Debug.LogError(\"[FastIoC] Failed to load ScriptableObject at Resources/{resourcesPath}\");");
                sb.AppendLine($"                }}");
            }
            else if (assetInfo.IsPrefab)
            {
                AppendPrefabInstantiationCode(sb, assetInfo, typeIdName,
                    loadLine: $"global::UnityEngine.GameObject prefab = global::UnityEngine.Resources.Load<global::UnityEngine.GameObject>(\"{resourcesPath}\");",
                    nullErrorMsg: $"[FastIoC] Failed to load prefab from Resources/{resourcesPath}");
            }
        }

        private static void GenerateManifestLoadCode(
            StringBuilder sb,
            AssetPathValidator.AssetInfo assetInfo,
            string typeIdName)
        {
            if (assetInfo.IsScriptableObject)
            {
                sb.AppendLine($"                global::UnityEngine.Object asset = fastIoCManifest.Get({assetInfo.ManifestIndex});");
                sb.AppendLine($"                if (asset != null)");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, asset);");
                sb.AppendLine($"                }}");
                sb.AppendLine($"                else");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    global::UnityEngine.Debug.LogError(\"[FastIoC] AssetManifest entry {assetInfo.ManifestIndex} is null\");");
                sb.AppendLine($"                }}");
            }
            else if (assetInfo.IsPrefab)
            {
                AppendPrefabInstantiationCode(sb, assetInfo, typeIdName,
                    loadLine: $"global::UnityEngine.GameObject prefab = fastIoCManifest.Get({assetInfo.ManifestIndex}) as global::UnityEngine.GameObject;",
                    nullErrorMsg: $"[FastIoC] AssetManifest entry {assetInfo.ManifestIndex} is null or not a GameObject");
            }
        }

        /// <summary>
        /// Shared prefab instantiation block used by both Resources and Manifest strategies.
        /// Adds DontDestroyOnLoad for global bindings.
        /// </summary>
        private static void AppendPrefabInstantiationCode(
            StringBuilder sb,
            AssetPathValidator.AssetInfo assetInfo,
            string typeIdName,
            string loadLine,
            string nullErrorMsg)
        {
            sb.AppendLine($"                {loadLine}");
            sb.AppendLine($"                if (prefab != null)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    bool prefabWasActive = prefab.activeSelf;");
            sb.AppendLine($"                    prefab.SetActive(false);");
            sb.AppendLine($"                    try");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        global::UnityEngine.GameObject instance = global::UnityEngine.Object.Instantiate(prefab);");
            sb.AppendLine($"                        instance.name = prefab.name;");
            sb.AppendLine($"                        {assetInfo.ComponentType.FullName} component = instance.GetComponent<{assetInfo.ComponentType.Name}>();");
            sb.AppendLine($"                        if (component != null)");
            sb.AppendLine($"                        {{");
            sb.AppendLine($"                            GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, component);");
            if (assetInfo.IsGlobal)
                sb.AppendLine($"                            global::UnityEngine.Object.DontDestroyOnLoad(instance);");
            sb.AppendLine($"                            try {{ global::FastIoC.API.SceneInjector.InjectIfNeeded(component); }} catch {{ }}");
            sb.AppendLine($"                            instance.SetActive(true);");
            sb.AppendLine($"                        }}");
            sb.AppendLine($"                        else");
            sb.AppendLine($"                        {{");
            sb.AppendLine($"                            global::UnityEngine.Debug.LogError(\"[FastIoC] Instantiated prefab missing component {assetInfo.ComponentType.Name}\");");
            sb.AppendLine($"                            global::UnityEngine.Object.Destroy(instance);");
            sb.AppendLine($"                        }}");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    finally");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        prefab.SetActive(prefabWasActive);");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                }}");
            sb.AppendLine($"                else");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    global::UnityEngine.Debug.LogError(\"{nullErrorMsg}\");");
            sb.AppendLine($"                }}");
        }

        private static void GenerateAddressablesLoadCode(
            StringBuilder sb,
            AssetPathValidator.AssetInfo assetInfo,
            string typeIdName)
        {
            string addressKey = string.IsNullOrEmpty(assetInfo.Address) ? assetInfo.AssetPath : assetInfo.Address;

            if (assetInfo.IsScriptableObject)
            {
                sb.AppendLine($"                var handle = global::UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<global::UnityEngine.Object>(\"{addressKey}\");");
                sb.AppendLine($"                handle.Completed += (result) => {{");
                sb.AppendLine($"                    if (result.Status == global::UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)");
                sb.AppendLine($"                        GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, result.Result);");
                sb.AppendLine($"                    else");
                sb.AppendLine($"                        global::UnityEngine.Debug.LogError($\"[FastIoC] Failed to load Addressable: {addressKey} - {{result.OperationException}}\");");
                sb.AppendLine($"                }};");
            }
            else if (assetInfo.IsPrefab)
            {
                sb.AppendLine($"                var handle = global::UnityEngine.AddressableAssets.Addressables.InstantiateAsync(\"{addressKey}\");");
                sb.AppendLine($"                handle.Completed += (result) => {{");
                sb.AppendLine($"                    if (result.Status == global::UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        global::UnityEngine.GameObject instance = result.Result;");
                sb.AppendLine($"                        {assetInfo.ComponentType.FullName} component = instance.GetComponent<{assetInfo.ComponentType.Name}>();");
                sb.AppendLine($"                        if (component != null)");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            GeneratedInstanceRegistry.RegisterInstance(GeneratedTypeIds.{typeIdName}.Id, component);");
                if (assetInfo.IsGlobal)
                    sb.AppendLine($"                            global::UnityEngine.Object.DontDestroyOnLoad(instance);");
                sb.AppendLine($"                            try {{ global::FastIoC.API.SceneInjector.InjectIfNeeded(component); }} catch {{ }}");
                sb.AppendLine($"                            instance.SetActive(true);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                        else");
                sb.AppendLine($"                        {{");
                sb.AppendLine($"                            global::UnityEngine.Debug.LogError(\"[FastIoC] Addressable prefab missing component {assetInfo.ComponentType.Name}\");");
                sb.AppendLine($"                            global::UnityEngine.Object.Destroy(instance);");
                sb.AppendLine($"                        }}");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                    else");
                sb.AppendLine($"                        global::UnityEngine.Debug.LogError($\"[FastIoC] Failed to instantiate Addressable: {addressKey} - {{result.OperationException}}\");");
                sb.AppendLine($"                }};");
            }
        }
    }
}
#endif

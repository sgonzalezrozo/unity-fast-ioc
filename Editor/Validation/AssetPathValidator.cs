#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FastIoC.API;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
#if UNITY_ADDRESSABLES_ENABLED
using UnityEngine.AddressableAssets;
#endif

namespace FastIoC.Editor.Validation
{
    public static class AssetPathValidator
    {
        public enum LoadingStrategy
        {
            Resources,
            Addressables,
            /// <summary>Asset is outside Resources/Addressables; loaded from the generated FastIoCAssetManifest. Global bindings only.</summary>
            Manifest
        }

        public class AssetInfo
        {
            public Type InterfaceType { get; set; }
            public string AssetPath { get; set; }
            public string Address { get; set; }
            public Type AssetType { get; set; }
            public Type ComponentType { get; set; }
            public bool IsScriptableObject { get; set; }
            public bool IsPrefab { get; set; }
            public LoadingStrategy LoadingStrategy { get; set; } = LoadingStrategy.Resources;
            /// <summary>Index into the generated FastIoCAssetManifest. -1 for non-manifest bindings.</summary>
            public int ManifestIndex { get; set; } = -1;
            /// <summary>True when the binding comes from a global installer.</summary>
            public bool IsGlobal { get; set; }
        }

        /// <summary>
        /// Extracts the <c>Resources.Load</c> path from an asset path that contains a <c>/Resources/</c> segment.
        /// Uses the LAST occurrence so nested paths like <c>Assets/Foo/Resources/Bar/A.asset</c> work correctly.
        /// Strips the file extension. Returns false if the path contains no <c>/Resources/</c> segment
        /// (e.g. <c>Assets/MyResources/A.asset</c>).
        /// </summary>
        public static bool TryGetResourcesLoadPath(string assetPath, out string loadPath)
        {
            const string marker = "/Resources/";
            int idx = assetPath.LastIndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                loadPath = null;
                return false;
            }

            loadPath = assetPath.Substring(idx + marker.Length);
            int dotIdx = loadPath.LastIndexOf('.');
            if (dotIdx > 0)
                loadPath = loadPath.Substring(0, dotIdx);
            return true;
        }

        public static Dictionary<Type, AssetInfo> ValidatePathBindings(
            Dictionary<Type, BindingInfo> allBindings,
            HashSet<Type> globalInterfaceTypes)
        {
            Dictionary<Type, AssetInfo> validatedAssets = new();

            foreach (KeyValuePair<Type, BindingInfo> binding in allBindings)
            {
                if (!binding.Value.IsPathBinding)
                    continue;

                Type interfaceType = binding.Key;
                string assetPath = binding.Value.AssetPath;
                bool isGlobal = globalInterfaceTypes != null && globalInterfaceTypes.Contains(interfaceType);

                AssetInfo info = ValidateSingleBinding(interfaceType, assetPath, isGlobal);
                if (info != null)
                    validatedAssets[interfaceType] = info;
                else
                    throw new InvalidOperationException(
                        $"[FastIoC] Code generation FAILED: Path-based binding validation failed for {interfaceType.Name}. " +
                        $"See previous error messages for details.");
            }

            // Assign manifest indices in deterministic order (InterfaceType.FullName).
            int idx2 = 0;
            foreach (KeyValuePair<Type, AssetInfo> kvp in validatedAssets
                .Where(k => k.Value.LoadingStrategy == LoadingStrategy.Manifest)
                .OrderBy(k => k.Key.FullName))
            {
                kvp.Value.ManifestIndex = idx2++;
            }

            return validatedAssets;
        }

        private static AssetInfo ValidateSingleBinding(Type interfaceType, string assetPath, bool isGlobal)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new InvalidOperationException($"[FastIoC] Binding {interfaceType.FullName} has empty asset path");

            assetPath = assetPath.Trim();

            string resolvedPath = ResolveAssetPath(assetPath);
            if (string.IsNullOrEmpty(resolvedPath))
                throw new InvalidOperationException(
                    $"[FastIoC] Code generation FAILED: Could not resolve asset path '{assetPath}'. " +
                    $"Provide either a full path (Assets/...) or ensure the asset exists in the project.");

            assetPath = resolvedPath;

            LoadingStrategy strategy = DetermineLoadingStrategy(assetPath, isGlobal, out string addressablesAddress);

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
                throw new InvalidOperationException(
                    $"[FastIoC] Code generation FAILED: Asset not found at path '{assetPath}' (binding: {interfaceType.FullName})");

            AssetInfo info = new AssetInfo
            {
                InterfaceType = interfaceType,
                AssetPath = assetPath,
                Address = addressablesAddress,
                LoadingStrategy = strategy,
                IsGlobal = isGlobal
            };

            if (asset is GameObject prefab)
            {
                info.IsPrefab = true;
                info.AssetType = typeof(GameObject);

                Component component = prefab.GetComponent(interfaceType);
                if (component == null)
                    throw new InvalidOperationException(
                        $"[FastIoC] Code generation FAILED: Prefab '{assetPath}' missing component '{interfaceType.Name}' " +
                        $"on root GameObject. Add component {interfaceType.FullName} to the prefab root.");

                info.ComponentType = component.GetType();
            }
            else if (asset is ScriptableObject scriptableObj)
            {
                info.IsScriptableObject = true;
                info.AssetType = scriptableObj.GetType();

                if (!interfaceType.IsAssignableFrom(info.AssetType))
                    throw new InvalidOperationException(
                        $"[FastIoC] Code generation FAILED: ScriptableObject '{assetPath}' type {info.AssetType.FullName} " +
                        $"is not assignable to {interfaceType.FullName}");

                info.ComponentType = info.AssetType;
            }
            else
            {
                throw new InvalidOperationException(
                    $"[FastIoC] Code generation FAILED: Asset '{assetPath}' is not a Prefab or ScriptableObject. " +
                    $"Path-based bindings only support prefabs and scriptable objects.");
            }

            Debug.Log($"[FastIoC] Validated binding: {interfaceType.Name} -> {assetPath} ({strategy})");
            return info;
        }

        private static string ResolveAssetPath(string assetPathOrName)
        {
            if (assetPathOrName.Contains("/"))
            {
                UnityEngine.Object testAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPathOrName);
                if (testAsset != null)
                    return assetPathOrName;

                return null;
            }

            string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPathOrName);
            List<string> candidates = new();
            foreach (string guid in AssetDatabase.FindAssets(assetName))
                candidates.Add(AssetDatabase.GUIDToAssetPath(guid));

            return SelectAssetByName(assetName, candidates);
        }

        /// <summary>
        /// Picks the asset a name refers to. <c>AssetDatabase.FindAssets</c> matches parts of names and also returns
        /// scripts, so a class and its asset with the same name (<c>ShopConfig.cs</c>, <c>ShopConfig.asset</c>) both
        /// match. Only an exact file name counts, scripts and folders are skipped, and more than one match fails
        /// instead of guessing. Returns null when nothing matches.
        /// </summary>
        internal static string SelectAssetByName(string assetName, IEnumerable<string> candidatePaths)
        {
            List<string> matches = new();
            foreach (string path in candidatePaths)
            {
                if (string.IsNullOrEmpty(path) || System.IO.Path.GetFileNameWithoutExtension(path) != assetName)
                    continue;

                string extension = System.IO.Path.GetExtension(path);
                if (string.IsNullOrEmpty(extension) || IsCodeFile(extension))
                    continue;

                matches.Add(path);
            }

            if (matches.Count <= 1)
                return matches.Count == 1 ? matches[0] : null;

            throw new InvalidOperationException(
                $"[FastIoC] Code generation FAILED: {matches.Count} assets are named '{assetName}': " +
                string.Join(", ", matches) + ". Pass the full Assets/... path instead of the name.");
        }

        private static bool IsCodeFile(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".cs":
                case ".asmdef":
                case ".asmref":
                case ".dll":
                case ".js":
                    return true;
                default:
                    return false;
            }
        }

        internal static LoadingStrategy DetermineLoadingStrategy(string assetPath, bool isGlobal, out string addressablesAddress)
        {
            addressablesAddress = null;

            if (TryGetResourcesLoadPath(assetPath, out _))
                return LoadingStrategy.Resources;

            if (TryDetectAddressableAtEditorTime(assetPath, out addressablesAddress))
                return LoadingStrategy.Addressables;

            if (isGlobal)
                return LoadingStrategy.Manifest;

            throw new InvalidOperationException(
                $"[FastIoC] Code generation FAILED: Asset '{assetPath}' is neither in a Resources/ folder " +
                $"nor marked for Addressables, and its binding is scene-specific.\n" +
                $"Choose one of:\n" +
                $"  1. Move the asset to a Resources/ subfolder\n" +
                $"  2. Mark the asset for Addressables\n" +
                $"  3. Move the binding to a global installer (Manifest strategy applies to global bindings only)");
        }

        private static bool TryDetectAddressableAtEditorTime(string assetPath, out string address)
        {
            address = null;

            try
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogWarning($"[FastIoC] Could not get GUID for asset: {assetPath}");
                    return false;
                }

                Debug.Log($"[FastIoC] Checking Addressables for GUID: {guid}");

                AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                    return false; // Addressables not configured — not an error, fall through to Manifest

                List<AddressableAssetEntry> allEntries = new();
                settings.GetAllAssets(allEntries, true);

                for (int i = 0; i < allEntries.Count; i++)
                {
                    AddressableAssetEntry entry = allEntries[i];
                    if (entry.guid != guid)
                        continue;

                    address = entry.address;
                    Debug.Log($"[FastIoC] Found Addressable: {entry.address} (GUID: {guid})");
                    return true;
                }

                Debug.Log($"[FastIoC] Asset not found in Addressables (GUID: {guid})");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FastIoC] Exception during Addressables detection: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
#endif

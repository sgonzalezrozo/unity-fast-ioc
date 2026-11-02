#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Tools.FastIoC.Runtime.API;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
#if UNITY_ADDRESSABLES_ENABLED
using UnityEngine.AddressableAssets;
#endif

namespace Tools.FastIoC.Editor.Validation
{
    public static class AssetPathValidator
    {
        public enum LoadingStrategy
        {
            Resources,
            Addressables
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
        }

        public static Dictionary<Type, AssetInfo> ValidatePathBindings(Dictionary<Type, BindingInfo> allBindings)
        {
            Dictionary<Type, AssetInfo> validatedAssets = new();

            foreach (KeyValuePair<Type, BindingInfo> binding in allBindings)
            {
                if (binding.Value.IsPathBinding)
                {
                    Type interfaceType = binding.Key;
                    string assetPath = binding.Value.AssetPath;

                    AssetInfo info = ValidateSingleBinding(interfaceType, assetPath);
                    if (info != null)
                        validatedAssets[interfaceType] = info;
                    else
                        throw new InvalidOperationException(
                            $"[FastIoC] Code generation FAILED: Path-based binding validation failed for {interfaceType.Name}. " +
                            $"See previous error messages for details.");
                }
            }

            return validatedAssets;
        }

        private static AssetInfo ValidateSingleBinding(Type interfaceType, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new InvalidOperationException(
                    $"[FastIoC] Binding {interfaceType.FullName} has empty asset path");
            }

            assetPath = assetPath.Trim();

            string resolvedPath = ResolveAssetPath(assetPath);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                throw new InvalidOperationException(
                    $"[FastIoC] Code generation FAILED: Could not resolve asset path '{assetPath}'. " +
                    $"Provide either a full path (Assets/...) or ensure the asset exists in the project.");
            }

            assetPath = resolvedPath;
            string addressablesAddress = null;

            LoadingStrategy strategy = DetermineLoadingStrategy(assetPath, out addressablesAddress);

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"[FastIoC] Code generation FAILED: Asset not found at path '{assetPath}' (binding: {interfaceType.FullName})");
            }

            AssetInfo info = new AssetInfo
            {
                InterfaceType = interfaceType,
                AssetPath = assetPath,
                Address = addressablesAddress,
                LoadingStrategy = strategy
            };

            if (asset is GameObject prefab)
            {
                info.IsPrefab = true;
                info.AssetType = typeof(GameObject);

                Component component = prefab.GetComponent(interfaceType);
                if (component == null)
                {
                    throw new InvalidOperationException(
                        $"[FastIoC] Code generation FAILED: Prefab '{assetPath}' missing component '{interfaceType.Name}' " +
                        $"on root GameObject. Add component {interfaceType.FullName} to the prefab root.");
                }

                info.ComponentType = component.GetType();
            }
            else if (asset is ScriptableObject scriptableObj)
            {
                info.IsScriptableObject = true;
                info.AssetType = scriptableObj.GetType();

                if (!interfaceType.IsAssignableFrom(info.AssetType))
                {
                    throw new InvalidOperationException(
                        $"[FastIoC] Code generation FAILED: ScriptableObject '{assetPath}' type {info.AssetType.FullName} " +
                        $"is not assignable to {interfaceType.FullName}");
                }

                info.ComponentType = info.AssetType;
            }
            else
            {
                throw new InvalidOperationException(
                    $"[FastIoC] Code generation FAILED: Asset '{assetPath}' is not a Prefab or ScriptableObject. " +
                    $"Path-based bindings only support prefabs and scriptable objects.");
            }

            Debug.Log($"[FastIoC] Validated binding: {interfaceType.Name} -> {assetPath} ({strategy}) ✓");
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

            string[] guids = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(assetPathOrName));

            if (guids.Length == 0)
                return null;

            if (guids.Length > 1)
            {
                string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPathOrName);
                Debug.LogWarning($"[FastIoC] Multiple assets found with name '{assetName}'. " +
                    $"Using first match. Consider using full path for clarity.");
            }

            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        private static LoadingStrategy DetermineLoadingStrategy(string assetPath, out string addressablesAddress)
        {
            addressablesAddress = null;

            if (assetPath.Contains("Resources/"))
                return LoadingStrategy.Resources;

            if (TryDetectAddressableAtEditorTime(assetPath, out addressablesAddress))
                return LoadingStrategy.Addressables;

            throw new InvalidOperationException(
                $"[FastIoC] Code generation FAILED: Asset '{assetPath}' is neither in Resources/ folder nor marked for Addressables.\n" +
                $"Choose one of:\n" +
                $"  1. Move to: Assets/Resources/{assetPath}\n" +
                $"  2. OR mark for Addressables in AddressableAssets settings");
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
                {
                    Debug.LogError("[FastIoC] Addressable Asset Settings could not be found!");
                    return false;
                }

                List<AddressableAssetEntry> allEntries = new();
                settings.GetAllAssets(allEntries, true);

                for (int i = 0; i < allEntries.Count; i++)
                {
                    AddressableAssetEntry entry = allEntries[i];
                    if (entry.guid != guid)
                        continue;

                    address = entry.address;
                    Debug.Log($"[FastIoC] ✓ Found Addressable: {entry.address} (GUID: {guid})");
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


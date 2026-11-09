#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastIoC.API;
using FastIoC.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor.Generation
{
    /// <summary>
    /// Writes or updates <c>Assets/Resources/Tools/FastIoC/AssetManifest.asset</c> so that
    /// path-based bindings whose assets are outside <c>Resources/</c> and Addressables can be
    /// loaded at runtime without moving the assets.
    /// <para>
    /// The asset order is deterministic (sorted by <c>InterfaceType.FullName</c>) and matches the
    /// <c>ManifestIndex</c> values that <see cref="AssetPathValidator.ValidatePathBindings"/> assigns,
    /// so generated <c>manifest.Get(index)</c> calls resolve the correct asset.
    /// </para>
    /// </summary>
    public static class AssetManifestWriter
    {
        private const string ManifestAssetPath = "Assets/Resources/Tools/FastIoC/AssetManifest.asset";
        private const string ManifestResourcePath = "Tools/FastIoC/AssetManifest";

        /// <summary>
        /// Writes or updates the manifest from the already-validated asset infos.
        /// If there are no Manifest-strategy entries the file is deleted (or left absent).
        /// Call this before <c>WriteGeneratedFiles</c> so the asset exists when the domain reloads.
        /// </summary>
        public static void WriteManifest(Dictionary<Type, AssetPathValidator.AssetInfo> validatedAssets)
        {
            // Collect manifest entries in the same deterministic order used by ValidatePathBindings.
            List<(Type interfaceType, AssetPathValidator.AssetInfo info)> entries = validatedAssets
                .Where(kvp => kvp.Value.LoadingStrategy == AssetPathValidator.LoadingStrategy.Manifest)
                .OrderBy(kvp => kvp.Key.FullName)
                .Select(kvp => (kvp.Key, kvp.Value))
                .ToList();

            if (entries.Count == 0)
            {
                if (File.Exists(ManifestAssetPath))
                {
                    AssetDatabase.DeleteAsset(ManifestAssetPath);
                    Debug.Log("[FastIoC] Deleted empty AssetManifest (no manifest-strategy bindings).");
                }
                return;
            }

            UnityEngine.Object[] newAssets = entries
                .Select(e => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(e.info.AssetPath))
                .ToArray();

            FastIoCAssetManifest manifest = AssetDatabase.LoadAssetAtPath<FastIoCAssetManifest>(ManifestAssetPath);

            if (manifest != null)
            {
                SerializedObject so = new SerializedObject(manifest);
                SerializedProperty prop = so.FindProperty("assets");

                bool changed = prop.arraySize != newAssets.Length;
                if (!changed)
                {
                    for (int i = 0; i < newAssets.Length; i++)
                    {
                        if (prop.GetArrayElementAtIndex(i).objectReferenceValue != newAssets[i])
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (!changed)
                    return; // identical — skip write

                prop.arraySize = newAssets.Length;
                for (int i = 0; i < newAssets.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = newAssets[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(manifest);
            }
            else
            {
                string dir = Path.GetDirectoryName(ManifestAssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                manifest = ScriptableObject.CreateInstance<FastIoCAssetManifest>();
                AssetDatabase.CreateAsset(manifest, ManifestAssetPath);

                SerializedObject so = new SerializedObject(manifest);
                SerializedProperty prop = so.FindProperty("assets");
                prop.arraySize = newAssets.Length;
                for (int i = 0; i < newAssets.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = newAssets[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(manifest);
            }

            Debug.Log($"[FastIoC] AssetManifest updated with {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")} at {ManifestAssetPath}.");
        }
    }
}
#endif

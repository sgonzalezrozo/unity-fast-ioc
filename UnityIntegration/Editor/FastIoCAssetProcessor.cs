#if UNITY_EDITOR
using FastIoC.Editor.Generation;
using UnityEditor;
using UnityEngine;

namespace FastIoC.UnityIntegration.Editor
{
    public class FastIoCAssetProcessor : AssetPostprocessor
    {
        private const bool ENABLE_AUTO_REGENERATE = true;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            #pragma warning disable CS0162
            if (!ENABLE_AUTO_REGENERATE)
                return;
            #pragma warning restore CS0162

            bool hasDeletedScripts = false;
            foreach (string deletedAsset in deletedAssets)
            {
                if (deletedAsset.Contains("Assets/FastIoC/Generated"))
                    continue;

                if (!deletedAsset.EndsWith(".cs"))
                    continue;

                hasDeletedScripts = true;
                Debug.Log($"[FastIoC] Detected deleted script: {deletedAsset}");
                break;
            }

            if (!hasDeletedScripts)
                return;

            Debug.Log("[FastIoC] Script deletion detected - regenerating code to remove stale type references...");
            EditorApplication.delayCall += () =>
            {
                GenerationCoordinator.GenerateAll();
                FastIoCEditorAutoRegen.ClearLastHash();
            };
        }
    }
}
#endif


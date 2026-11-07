#if UNITY_EDITOR
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

            // Regenerating now would read the types still loaded in memory, deleted ones included,
            // and write references that no longer compile. Remove the generated code instead, so the
            // project compiles; FastIoCEditorAutoRegen regenerates it after the domain reload.
            Debug.Log("[FastIoC] Script deletion detected - removing generated code; it regenerates after the next compile.");
            EditorApplication.delayCall += () =>
            {
                RemoveGeneratedScripts();
                FastIoCEditorAutoRegen.ClearLastHash();
            };
        }

        private static void RemoveGeneratedScripts()
        {
            string folder = FastIoC.Core.FastIoCConstants.GENERATED_PATH;
            if (!System.IO.Directory.Exists(folder))
                return;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string file in System.IO.Directory.GetFiles(folder, "*.cs"))
                    AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}
#endif


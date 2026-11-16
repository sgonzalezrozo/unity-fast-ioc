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

            // Only a deleted script the generated code uses makes it stop compiling. Deleting any other
            // script, such as one from another package, leaves the generated code valid.
            System.Collections.Generic.List<string> inUse = FastIoC.Editor.Generation.GeneratedCodeReferences.FindDeletedScriptsInUse(
                deletedAssets, FastIoC.Editor.Generation.GeneratedCodeReferences.ReadGeneratedSources());
            if (inUse.Count == 0)
                return;

            // Batch mode never regenerates by itself, so removing the code there would leave builds without
            // injection and without an error. Keep it: the compile error names what to fix, and the build
            // regenerates before it starts.
            if (Application.isBatchMode)
            {
                Debug.LogWarning($"[FastIoC] Deleted script(s) used by the generated code: {string.Join(", ", inUse)}. " +
                                 "In batch mode the generated code is kept; run Tools > FastIoC > Regenerate Code once the project compiles.");
                return;
            }

            // Regenerating now would read the types still loaded in memory, deleted ones included,
            // and write references that no longer compile. Remove the generated code instead, so the
            // project compiles; FastIoCEditorAutoRegen regenerates it after the domain reload.
            Debug.Log($"[FastIoC] Deleted script(s) used by the generated code: {string.Join(", ", inUse)} - removing generated code; it regenerates after the next compile.");
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


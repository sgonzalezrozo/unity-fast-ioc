using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor
{
    /// <summary>
    /// Links to the Fast IoC documentation. After the package is imported, shows a welcome dialog once per project.
    /// </summary>
    [InitializeOnLoad]
    internal static class FastIoCDocumentation
    {
        internal const string Url = "https://github.com/sgonzalezrozo/unity-fast-ioc#readme";

        private const string ShownKeyPrefix = "com.sgonzalezrozo.fast-ioc.WelcomeShown:";

        static FastIoCDocumentation()
        {
            if (Application.isBatchMode || EditorPrefs.GetBool(ShownKey, false))
                return;

            EditorApplication.update += ShowWelcomeWhenIdle;
        }

        // One key per project, so the dialog shows again in another project.
        private static string ShownKey => ShownKeyPrefix + Application.dataPath;

        private static void ShowWelcomeWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= ShowWelcomeWhenIdle;
            if (EditorPrefs.GetBool(ShownKey, false))
                return;

            EditorPrefs.SetBool(ShownKey, true);
            bool open = EditorUtility.DisplayDialog(
                "Fast IoC is installed",
                "Next, run Tools > FastIoC > Setup. It creates the ProjectContext prefab and generates the injection code.\n\nThe documentation shows how to write installers, bind services and inject them.",
                "Open Documentation",
                "Close");

            if (open)
                OpenDocumentation();
        }

        [MenuItem("Tools/FastIoC/Documentation", priority = 100)]
        private static void OpenDocumentationFromMenu() => OpenDocumentation();

        internal static void OpenDocumentation() => Application.OpenURL(Url);
    }
}

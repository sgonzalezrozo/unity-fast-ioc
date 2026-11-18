using UnityEditor;
using UnityEngine;

namespace FastIoC.Editor
{
    /// <summary>
    /// Links to the Fast IoC documentation.
    /// </summary>
    internal static class FastIoCDocumentation
    {
        internal const string Url = "https://github.com/sgonzalezrozo/unity-fast-ioc#readme";

        [MenuItem("Tools/FastIoC/Documentation", priority = 100)]
        private static void OpenDocumentationFromMenu() => OpenDocumentation();

        internal static void OpenDocumentation() => Application.OpenURL(Url);
    }
}

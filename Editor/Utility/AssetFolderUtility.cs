using UnityEditor;

namespace FastIoC.Editor
{
    public static class AssetFolderUtility
    {
        /// <summary>
        /// Creates every missing folder of <paramref name="folderPath"/> (for example
        /// <c>Assets/Resources/Tools/FastIoC</c>) through the AssetDatabase, so assets can be created
        /// there right away, even in a project that has no <c>Resources</c> folder yet.
        /// </summary>
        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

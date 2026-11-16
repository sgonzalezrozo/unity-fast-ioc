#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FastIoC.Core;

namespace FastIoC.Editor.Generation
{
    /// <summary>
    /// Tells whether deleted scripts are used by the generated code. Only those make the generated code stop
    /// compiling; deleting any other script, such as one from another package, leaves it valid.
    /// </summary>
    public static class GeneratedCodeReferences
    {
        /// <summary>
        /// The deleted scripts (asset paths) whose type, named after the file, appears in the generated sources.
        /// Generated files themselves and non-scripts are ignored.
        /// </summary>
        public static List<string> FindDeletedScriptsInUse(IEnumerable<string> deletedAssets, IReadOnlyList<string> generatedSources)
        {
            List<string> inUse = new();
            if (deletedAssets == null || generatedSources == null || generatedSources.Count == 0)
                return inUse;

            foreach (string asset in deletedAssets)
            {
                if (string.IsNullOrEmpty(asset) || !asset.EndsWith(".cs") || asset.Replace('\\', '/').Contains(FastIoCConstants.GENERATED_PATH))
                    continue;

                string typeName = Path.GetFileNameWithoutExtension(asset);
                Regex reference = new($@"\b{Regex.Escape(typeName)}\b");
                foreach (string source in generatedSources)
                {
                    if (source != null && reference.IsMatch(source))
                    {
                        inUse.Add(asset);
                        break;
                    }
                }
            }

            return inUse;
        }

        /// <summary>The generated sources on disk, or an empty list.</summary>
        public static List<string> ReadGeneratedSources()
        {
            List<string> sources = new();
            string folder = FastIoCConstants.GENERATED_PATH;
            if (!Directory.Exists(folder))
                return sources;

            foreach (string file in Directory.GetFiles(folder, "*.cs"))
                sources.Add(File.ReadAllText(file));

            return sources;
        }
    }
}
#endif

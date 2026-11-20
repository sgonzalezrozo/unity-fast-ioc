using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FastIoC.Editor
{
    /// <summary>
    /// Adds assembly references to the assembly definition that compiles a folder, so the scripts a wizard writes there
    /// compile. Unity doesn't add a package's assemblies to a project's .asmdef on its own, and a script under an .asmdef
    /// that misses them fails with CS0246. A folder with no .asmdef compiles in Assembly-CSharp, which already sees
    /// auto-referenced packages, so nothing changes there.
    /// </summary>
    internal static class AssemblyReferences
    {
        private const string GUID_PREFIX = "GUID:";
        private const string LOG_PREFIX = "FastIoC";

        private static readonly Regex ReferencesRx = new Regex("\"references\"\\s*:\\s*\\[(?<items>[^\\]]*)\\]");
        private static readonly Regex NameRx = new Regex("\"name\"\\s*:\\s*\"(?<value>[^\"]*)\"\\s*(?<comma>,?)");
        private static readonly Regex AsmrefRx = new Regex("\"reference\"\\s*:\\s*\"(?<value>[^\"]*)\"");
        private static readonly Regex StringRx = new Regex("\"(?<value>[^\"]*)\"");

        /// <summary>
        /// Adds each named assembly that exists in the project to the .asmdef that compiles <paramref name="folder"/>.
        /// Returns the .asmdef path when it changed, or null when nothing changed.
        /// </summary>
        public static string Ensure(string folder, params string[] assemblyNames)
        {
            string asmdefPath = FindOwningAsmdef(folder);
            if (asmdefPath == null || !asmdefPath.StartsWith("Assets/"))
                return null;

            string json = File.ReadAllText(asmdefPath);
            List<string> references = ReadReferences(json, out bool hasArray);
            bool useGuids = references.Count > 0 && AllGuids(references);
            List<string> added = new List<string>();

            foreach (string assemblyName in assemblyNames)
            {
                if (string.IsNullOrEmpty(assemblyName) || assemblyName == ReadAssemblyName(json))
                    continue;

                string referencePath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
                if (string.IsNullOrEmpty(referencePath))
                    continue;

                string guidEntry = GUID_PREFIX + AssetDatabase.AssetPathToGUID(referencePath);
                if (references.Contains(assemblyName) || references.Contains(guidEntry))
                    continue;

                references.Add(useGuids ? guidEntry : assemblyName);
                added.Add(assemblyName);
            }

            if (added.Count == 0)
                return null;

            File.WriteAllText(asmdefPath, WriteReferences(json, references, hasArray));
            AssetDatabase.ImportAsset(asmdefPath);
            Debug.Log($"[{LOG_PREFIX}] Added {string.Join(", ", added)} to the Assembly Definition References of {asmdefPath}, so the new scripts compile.");
            return asmdefPath;
        }

        /// <summary>
        /// The .asmdef that compiles scripts in <paramref name="folder"/>: the nearest one up the folder tree, or the one
        /// an .asmref there points to. Null when the folder compiles in Assembly-CSharp.
        /// </summary>
        public static string FindOwningAsmdef(string folder)
        {
            string directory = (folder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            while (directory.StartsWith("Assets") || directory.StartsWith("Packages/"))
            {
                if (Directory.Exists(directory))
                {
                    string[] asmdefs = Directory.GetFiles(directory, "*.asmdef");
                    if (asmdefs.Length > 0)
                        return asmdefs[0].Replace('\\', '/');

                    string[] asmrefs = Directory.GetFiles(directory, "*.asmref");
                    if (asmrefs.Length > 0)
                        return ResolveAsmref(asmrefs[0]);
                }

                int slash = directory.LastIndexOf('/');
                if (slash < 0)
                    return null;
                directory = directory.Substring(0, slash);
            }
            return null;
        }

        /// <summary>The "name" of an .asmdef file.</summary>
        public static string ReadAssemblyName(string asmdefJson)
        {
            Match name = NameRx.Match(asmdefJson ?? string.Empty);
            return name.Success ? name.Groups["value"].Value : null;
        }

        private static string ResolveAsmref(string asmrefPath)
        {
            Match reference = AsmrefRx.Match(File.ReadAllText(asmrefPath));
            if (!reference.Success)
                return null;

            string value = reference.Groups["value"].Value;
            string path = value.StartsWith(GUID_PREFIX)
                ? AssetDatabase.GUIDToAssetPath(value.Substring(GUID_PREFIX.Length))
                : CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(value);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        internal static List<string> ReadReferences(string json, out bool hasArray)
        {
            List<string> references = new List<string>();
            Match array = ReferencesRx.Match(json);
            hasArray = array.Success;
            if (!array.Success)
                return references;

            foreach (Match item in StringRx.Matches(array.Groups["items"].Value))
                references.Add(item.Groups["value"].Value);
            return references;
        }

        internal static string WriteReferences(string json, List<string> references, bool hasArray)
        {
            StringBuilder array = new StringBuilder("\"references\": [");
            for (int i = 0; i < references.Count; i++)
                array.Append(i == 0 ? "\n" : ",\n").Append("        \"").Append(references[i]).Append('"');
            array.Append("\n    ]");

            if (hasArray)
                return ReferencesRx.Replace(json, array.ToString().Replace("$", "$$"), 1);

            Match name = NameRx.Match(json);
            if (!name.Success)
                return json;

            string replacement = name.Value.TrimEnd().TrimEnd(',') + ",\n    " + array + (name.Groups["comma"].Length > 0 ? "," : string.Empty);
            return json.Substring(0, name.Index) + replacement + json.Substring(name.Index + name.Length);
        }

        private static bool AllGuids(List<string> references)
        {
            for (int i = 0; i < references.Count; i++)
            {
                if (!references[i].StartsWith(GUID_PREFIX))
                    return false;
            }
            return true;
        }
    }
}

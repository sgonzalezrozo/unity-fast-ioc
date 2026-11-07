using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FastIoCBench.Editor
{
    /// <summary>
    /// Runs Zenject's reflection baking on the optimized copy explicitly. Zenject only bakes when Unity compiles
    /// an assembly, and Unity skips compiling when the code didn't change, so relying on that leaves the copy unbaked.
    /// </summary>
    public static class BenchZenjectBaking
    {
        public const string EDITOR_DLL = "Library/ScriptAssemblies/" + BenchCodeEmitter.OPT_ASSEMBLY + ".dll";
        private const string BAKED_MARKER = "__zenCreateInjectTypeInfo";

        /// <summary>Bakes the assembly at <paramref name="path"/> (relative to the project) and returns whether it is baked afterwards.</summary>
        public static bool Bake(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[FastIoCBench] Can't bake {path}: file not found.");
                return false;
            }

            if (IsBaked(path))
                return true;

            Type observer = BenchSceneBuilder.FindType("Zenject.ReflectionBaking.ReflectionBakingBuildObserver");
            MethodInfo weave = observer?.GetMethod("TryWeaveAssembly", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (weave == null)
            {
                Debug.LogError("[FastIoCBench] Zenject reflection baking not found (ReflectionBakingBuildObserver.TryWeaveAssembly).");
                return false;
            }

            try
            {
                weave.Invoke(null, new object[] { path.Replace('\\', '/') });
            }
            catch (TargetInvocationException ex)
            {
                Debug.LogError($"[FastIoCBench] Zenject reflection baking failed on {path}: {ex.InnerException}");
                return false;
            }

            bool baked = IsBaked(path);
            if (!baked)
                Debug.LogError($"[FastIoCBench] Zenject reflection baking ran on {path} but added nothing. Check {BenchPaths.BAKING_SETTINGS}.");
            return baked;
        }

        /// <summary>Baked methods are named in the assembly's string heap, so the file contains their name.</summary>
        public static bool IsBaked(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] marker = Encoding.ASCII.GetBytes(BAKED_MARKER);
            for (int i = 0; i <= bytes.Length - marker.Length; i++)
            {
                int j = 0;
                while (j < marker.Length && bytes[i + j] == marker[j])
                    j++;
                if (j == marker.Length)
                    return true;
            }

            return false;
        }

        /// <summary>Not used by the pipeline: a baked editor copy can stop loading after a later domain reload. Kept for manual checks.</summary>
        public static void BakeEditorCopy()
        {
            if (Bake(EDITOR_DLL))
            {
                Debug.Log($"[FastIoCBench] Zenject reflection baking applied to {EDITOR_DLL}.");
                EditorUtility.RequestScriptReload();
            }
        }
    }

    /// <summary>Bakes the player copy after the player scripts compile, before IL2CPP, and fails the build if it isn't baked.</summary>
    internal sealed class BenchZenjectBakingBuildStep : IPostBuildPlayerScriptDLLs
    {
        public int callbackOrder => 1000;

        public void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            if (string.IsNullOrEmpty(BenchPipeline.CurrentProfile))
                return;

            string name = BenchCodeEmitter.OPT_ASSEMBLY + ".dll";
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string path = report.GetFiles().Select(f => f.path).FirstOrDefault(p => Path.GetFileName(p) == name);
            if (path == null)
            {
                string bee = Path.Combine(projectRoot, "Library", "Bee");
                path = Directory.Exists(bee)
                    ? Directory.GetFiles(bee, name, SearchOption.AllDirectories).Where(p => p.Replace('\\', '/').Contains("PlayerScriptAssemblies")).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                    : null;
            }

            if (path == null)
                throw new BuildFailedException($"[FastIoCBench] Player copy of {name} not found, so Zenject reflection baking can't be applied.");

            if (Path.IsPathRooted(path) && path.Replace('\\', '/').StartsWith(projectRoot.Replace('\\', '/')))
                path = path.Substring(projectRoot.Length + 1);

            if (!BenchZenjectBaking.Bake(path))
                throw new BuildFailedException($"[FastIoCBench] Zenject reflection baking could not be applied to {path}. The zenject-baked results would be wrong, so the build stops.");

            Debug.Log($"[FastIoCBench] Zenject reflection baking applied to the player copy ({path}).");
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FastIoCBench.Editor
{
    public static class BenchPaths
    {
        public const string ROOT = "Assets/Benchmark/Generated";
        public const string CODE = ROOT + "/Code";
        public const string PREFABS = ROOT + "/Prefabs";
        public const string SCENES = ROOT + "/Scenes";
        public const string RESOURCES = ROOT + "/Resources";
        public const string PROFILE_FILE = ROOT + "/profile.txt";
        public const string TIMINGS_FILE = ROOT + "/editor-timings.json";
    }

    [Serializable]
    public sealed class BenchEditorTimings
    {
        public string profile;
        public double fixtureCodeSeconds;
        public double fastIocGenerationSeconds;
        public double assetsSeconds;
        public int generatedFiles;
    }

    /// <summary>
    /// Generate runs in two halves around a script compile:
    /// 1. write the fixture code (and remove Fast IoC's old generated code, which names the old types);
    /// 2. after the compile: prefabs, scenes, then Fast IoC code generation, timed.
    /// </summary>
    [InitializeOnLoad]
    public static class BenchPipeline
    {
        private const string PENDING_KEY = "FastIoCBench.PendingAssets";
        public const string ROUNDS_KEY = "FastIoCBench.Rounds";

        public static string Status { get; private set; } = "";

        public static int Rounds
        {
            get => Math.Max(1, EditorPrefs.GetInt(ROUNDS_KEY, 5));
            set => EditorPrefs.SetInt(ROUNDS_KEY, Math.Max(1, value));
        }

        public static string CurrentProfile => File.Exists(BenchPaths.PROFILE_FILE) ? File.ReadAllText(BenchPaths.PROFILE_FILE).Trim() : null;

        static BenchPipeline()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(PENDING_KEY, "")))
                EditorApplication.delayCall += ContinueAfterCompile;
        }

        public static void Generate(BenchProfile profile)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Stopwatch sw = Stopwatch.StartNew();
            BenchGraph graph = BenchGraph.Build(profile);

            AssetDatabase.DeleteAsset(BenchPaths.ROOT);
            DeleteFastIoCGeneratedCode();

            Directory.CreateDirectory(BenchPaths.ROOT);
            var files = BenchCodeEmitter.Emit(graph, BenchPaths.CODE);
            File.WriteAllText(BenchPaths.PROFILE_FILE, profile.profileName);
            File.WriteAllText(BenchPaths.ROOT + "/link.xml",
                "<linker>\n" +
                "  <!-- Zenject and VContainer create these types by reflection. -->\n" +
                "  <assembly fullname=\"Assembly-CSharp\">\n" +
                "    <namespace fullname=\"" + BenchCodeEmitter.NAMESPACE + "\" preserve=\"all\"/>\n" +
                "  </assembly>\n" +
                "</linker>\n");

            BenchEditorTimings timings = new()
            {
                profile = profile.profileName,
                fixtureCodeSeconds = sw.Elapsed.TotalSeconds,
                generatedFiles = files.Count,
            };
            File.WriteAllText(BenchPaths.TIMINGS_FILE, JsonUtility.ToJson(timings, true));

            Status = $"{profile.profileName}: wrote {files.Count} files ({graph.InterfaceCount} bindings, {graph.Impls.Count} classes, {graph.MonoInstanceTypes.Count} scene objects). Compiling...";
            Debug.Log("[FastIoCBench] " + Status);

            SessionState.SetString(PENDING_KEY, profile.profileName);
            AssetDatabase.Refresh();
        }

        private static void ContinueAfterCompile()
        {
            string profileName = SessionState.GetString(PENDING_KEY, "");
            if (string.IsNullOrEmpty(profileName) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (!string.IsNullOrEmpty(profileName))
                    EditorApplication.delayCall += ContinueAfterCompile;
                return;
            }

            SessionState.EraseString(PENDING_KEY);

            if (BenchSceneBuilder.FindType(BenchCodeEmitter.NAMESPACE + ".GeneratedBenchFixture") == null)
            {
                Status = "The generated code didn't compile. Fix the errors in the Console, then Generate again.";
                Debug.LogError("[FastIoCBench] " + Status);
                return;
            }

            BenchProfile profile = BenchProfile.LoadAll().FirstOrDefault(p => p.profileName == profileName);
            if (profile == null)
            {
                Status = $"Profile {profileName} not found.";
                Debug.LogError("[FastIoCBench] " + Status);
                return;
            }

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                BenchGraph graph = BenchGraph.Build(profile);
                BenchSceneBuilder.Build(graph);
                double assetsSeconds = sw.Elapsed.TotalSeconds;

                sw.Restart();
                FastIoC.Editor.Generation.GenerationCoordinator.GenerateAll();
                double fastIocSeconds = sw.Elapsed.TotalSeconds;

                BenchEditorTimings timings = ReadTimings() ?? new BenchEditorTimings { profile = profileName };
                timings.assetsSeconds = assetsSeconds;
                timings.fastIocGenerationSeconds = fastIocSeconds;
                File.WriteAllText(BenchPaths.TIMINGS_FILE, JsonUtility.ToJson(timings, true));

                EditorSceneManager.OpenScene($"{BenchPaths.SCENES}/Boot.unity");
                Status = $"{profileName} ready. Fast IoC generation took {fastIocSeconds:0.0} s. Expected graph hash {graph.ExpectedHash}, {graph.ExpectedDistinctInstances} instances, {graph.ExpectedInjectedFields} injected fields.";
                Debug.Log("[FastIoCBench] " + Status);
            }
            catch (Exception ex)
            {
                Status = "Building scenes failed: " + ex.Message;
                Debug.LogException(ex);
            }
        }

        public static BenchEditorTimings ReadTimings()
        {
            return File.Exists(BenchPaths.TIMINGS_FILE) ? JsonUtility.FromJson<BenchEditorTimings>(File.ReadAllText(BenchPaths.TIMINGS_FILE)) : null;
        }

        private static void DeleteFastIoCGeneratedCode()
        {
            string folder = FastIoC.Core.FastIoCConstants.GENERATED_PATH;
            if (!Directory.Exists(folder))
                return;

            foreach (string file in Directory.GetFiles(folder, "*.cs"))
                AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
        }
    }
}

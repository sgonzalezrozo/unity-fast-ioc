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
        public const string OPTIMIZED = ROOT + "/Optimized";

        /// <summary>Download VContainer.SourceGenerator.dll from the VContainer release that matches the package and put it here. The "~" keeps Unity from importing it as a plugin.</summary>
        public const string SOURCE_GENERATOR_SOURCE = "Assets/Benchmark/SourceGenerator~/VContainer.SourceGenerator.dll";
        public const string SOURCE_GENERATOR_COPY = OPTIMIZED + "/VContainer.SourceGenerator.dll";
        public const string BAKING_SETTINGS = "Assets/Benchmark/ZenjectReflectionBakingSettings.asset";

        public static bool HasSourceGenerator => System.IO.File.Exists(SOURCE_GENERATOR_SOURCE);
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
            bool withSourceGenerator = BenchPaths.HasSourceGenerator;
            var files = BenchCodeEmitter.Emit(graph, BenchPaths.CODE, withSourceGenerator);
            files.AddRange(BenchCodeEmitter.EmitOptimized(graph, BenchPaths.OPTIMIZED));
            File.WriteAllText(BenchPaths.PROFILE_FILE, profile.profileName);
            File.WriteAllText(BenchPaths.ROOT + "/link.xml",
                "<linker>\n" +
                "  <!-- Zenject and VContainer create these types by reflection. -->\n" +
                "  <assembly fullname=\"Assembly-CSharp\">\n" +
                "    <namespace fullname=\"" + BenchCodeEmitter.NAMESPACE + "\" preserve=\"all\"/>\n" +
                "  </assembly>\n" +
                "  <assembly fullname=\"" + BenchCodeEmitter.OPT_ASSEMBLY + "\" preserve=\"all\"/>\n" +
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

            SetUpOptimizations(withSourceGenerator);

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

            if (BenchSceneBuilder.FindType(BenchCodeEmitter.NAMESPACE + ".GeneratedBenchFixture") == null
                || BenchSceneBuilder.FindType(BenchCodeEmitter.OPT_NAMESPACE + ".OptimizedFixture") == null)
            {
                Status = "The generated assemblies are not loaded. See the [FastIoCBench] diagnostics in the Console.";
                Debug.LogError("[FastIoCBench] " + Status + "\n" + DiagnoseAssemblies());
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
                BenchSceneBuilder.Build(graph, BenchPaths.HasSourceGenerator);
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

        /// <summary>
        /// VContainer source generator: copied next to the optimized copy's asmdef, so it only runs on that assembly
        /// (nothing references it). Zenject reflection baking: one settings asset, limited to the same assembly and namespace.
        /// </summary>
        private static void SetUpOptimizations(bool withSourceGenerator)
        {
            if (withSourceGenerator)
            {
                File.Copy(BenchPaths.SOURCE_GENERATOR_SOURCE, BenchPaths.SOURCE_GENERATOR_COPY, true);
                AssetDatabase.ImportAsset(BenchPaths.SOURCE_GENERATOR_COPY, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(BenchPaths.SOURCE_GENERATOR_COPY) is PluginImporter plugin)
                {
                    // An analyzer, not a plugin: no platform at all, and the label Unity looks for.
                    plugin.SetCompatibleWithAnyPlatform(false);
                    plugin.SetCompatibleWithEditor(false);
                    foreach (BuildTarget target in new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.StandaloneOSX, BuildTarget.StandaloneWindows64 })
                        plugin.SetCompatibleWithPlatform(target, false);
                    AssetDatabase.SetLabels(AssetDatabase.LoadMainAssetAtPath(BenchPaths.SOURCE_GENERATOR_COPY), new[] { "RoslynAnalyzer" });
                    plugin.SaveAndReimport();
                }
            }
            else
            {
                Debug.LogWarning($"[FastIoCBench] {BenchPaths.SOURCE_GENERATOR_SOURCE} not found: the vcontainer-sg variant is left out. Download VContainer.SourceGenerator.dll from the VContainer release that matches the package version.");
            }

            System.Type settingsType = BenchSceneBuilder.FindType("Zenject.ReflectionBaking.ZenjectReflectionBakingSettings");
            if (settingsType == null)
            {
                Debug.LogError("[FastIoCBench] Zenject reflection baking not found. The zenject-baked variant will run without baking.");
                return;
            }

            ScriptableObject settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BenchPaths.BAKING_SETTINGS);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance(settingsType);
                AssetDatabase.CreateAsset(settings, BenchPaths.BAKING_SETTINGS);
            }

            SerializedObject so = new(settings);
            so.FindProperty("_isEnabledInBuilds").boolValue = true;
            // Builds only: a baked copy in Library/ScriptAssemblies can stop loading after a later domain reload.
            so.FindProperty("_isEnabledInEditor").boolValue = false;
            so.FindProperty("_allGeneratedAssemblies").boolValue = false;
            // Zenject compares entries with Path.GetFileNameWithoutExtension, which would cut ".GenOpt": keep the ".dll".
            SetStrings(so.FindProperty("_includeAssemblies"), BenchCodeEmitter.OPT_ASSEMBLY + ".dll");
            SetStrings(so.FindProperty("_excludeAssemblies"));
            SetStrings(so.FindProperty("_namespacePatterns"), "^" + BenchCodeEmitter.OPT_NAMESPACE.Replace(".", "\\."));
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void SetStrings(SerializedProperty list, params string[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        [MenuItem("Tools/FastIoC Benchmark Diagnostics")]
        private static void LogDiagnostics() => Debug.Log("[FastIoCBench] " + DiagnoseAssemblies());

        /// <summary>What is loaded versus what was compiled, to tell a compile error from an assembly that didn't load.</summary>
        public static string DiagnoseAssemblies()
        {
            System.Text.StringBuilder sb = new();
            sb.AppendLine("Loaded assemblies of interest:");
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = a.GetName().Name;
                if (name == "Assembly-CSharp" || name.StartsWith("FastIoCBench"))
                {
                    string location;
                    try { location = a.IsDynamic ? "dynamic" : a.Location; } catch (Exception) { location = "?"; }
                    sb.AppendLine($"  {name}  location='{location}'");
                }
            }

            foreach (string name in new[] { "Assembly-CSharp", BenchCodeEmitter.OPT_ASSEMBLY })
            {
                string path = $"Library/ScriptAssemblies/{name}.dll";
                sb.AppendLine(File.Exists(path) ? $"Compiled: {path} ({new FileInfo(path).Length} bytes, {File.GetLastWriteTime(path):HH:mm:ss})" : $"Missing: {path}");
            }

            sb.AppendLine($"GeneratedBenchFixture found: {BenchSceneBuilder.FindType(BenchCodeEmitter.NAMESPACE + ".GeneratedBenchFixture") != null}");
            sb.AppendLine($"OptimizedFixture found: {BenchSceneBuilder.FindType(BenchCodeEmitter.OPT_NAMESPACE + ".OptimizedFixture") != null}");
            foreach (var assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor))
            {
                if (assembly.name == BenchCodeEmitter.OPT_ASSEMBLY)
                    sb.AppendLine($"CompilationPipeline knows {assembly.name}: output {assembly.outputPath}, flags {assembly.flags}");
            }

            return sb.ToString();
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

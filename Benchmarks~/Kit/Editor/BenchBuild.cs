using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FastIoCBench.Editor
{
    /// <summary>Player settings, build info (commit, versions) and the player build.</summary>
    public static class BenchBuild
    {
        public const string BUNDLE_ID = "com.sgrozo.fastiocbenchmark";
        private const string FAST_IOC_PACKAGE = "com.sgonzalezrozo.fast-ioc";

        public static void ApplyPlayerSettings()
        {
            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                // iOS takes its C++ configuration from the Xcode scheme (Release when archiving or with the Release scheme).
                if (target == NamedBuildTarget.Android)
                    PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetApplicationIdentifier(target, BUNDLE_ID);
            }

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.productName = "FastIoC Benchmark";
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[FastIoCBench] Player settings applied: IL2CPP Release, ARMv7 + ARM64, " + BUNDLE_ID + ", Frame Timing Stats on, Development Build off.");
        }

        public static string StrippingLevel(BuildTarget target)
        {
            return PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target))).ToString();
        }

        /// <summary>Writes Resources/FastIoCBenchBuildInfo.json. Throws if the Fast IoC commit can't be read.</summary>
        public static BenchBuildInfo WriteBuildInfo(BuildTarget target, bool forPlayer)
        {
            string profile = BenchPipeline.CurrentProfile;
            if (string.IsNullOrEmpty(profile))
                throw new BuildFailedException("[FastIoCBench] No generated fixture. Generate a profile first.");

            var fastIoc = UnityEditor.PackageManager.PackageInfo.FindForPackageName(FAST_IOC_PACKAGE);
            if (fastIoc == null)
                throw new BuildFailedException("[FastIoCBench] Fast IoC package not found.");

            string commit = Git(fastIoc.resolvedPath, "rev-parse HEAD").Trim();
            if (!Regex.IsMatch(commit, "^(?!0+$)[0-9a-fA-F]{7,40}$"))
                throw new BuildFailedException($"[FastIoCBench] Can't read the Fast IoC commit (got '{commit}'). Is {fastIoc.resolvedPath} a git repository?");

            bool dirty = !string.IsNullOrWhiteSpace(Git(fastIoc.resolvedPath, "status --porcelain"));
            BenchEditorTimings timings = BenchPipeline.ReadTimings();

            BenchBuildInfo info = new()
            {
                // Play Mode keeps one id per profile, so the plan advances across Play sessions instead of restarting.
                buildId = forPlayer ? Guid.NewGuid().ToString("N") : "editor-" + profile,
                profile = profile,
                gitCommit = commit,
                gitDirty = dirty,
                fastIocVersion = fastIoc.version,
                zenjectVersion = LockedVersion("Extenject.git") ?? (Directory.Exists("Assets/Plugins/Zenject") ? "Assets/Plugins/Zenject" : "missing"),
                vcontainerVersion = LockedVersion("VContainer.git") ?? "missing",
                strippingLevel = StrippingLevel(target),
                builtAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                rounds = BenchPipeline.Rounds,
                fastIocGenerationSeconds = timings?.fastIocGenerationSeconds ?? 0,
            };

            if (dirty)
                Debug.LogWarning($"[FastIoCBench] {fastIoc.resolvedPath} has uncommitted changes. Results will record commit {commit.Substring(0, 7)} with fastiocDirty=true. Commit before the runs you publish.");

            Directory.CreateDirectory(BenchPaths.RESOURCES);
            string path = $"{BenchPaths.RESOURCES}/{BenchBuildInfo.RESOURCE_NAME}.json";
            File.WriteAllText(path, JsonUtility.ToJson(info, true));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return info;
        }

        public static void BuildPlayer(BuildTarget target)
        {
            string profile = BenchPipeline.CurrentProfile;
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (string.IsNullOrEmpty(profile) || scenes.Length == 0)
            {
                Debug.LogError("[FastIoCBench] Generate a profile before building.");
                return;
            }

            string location = target == BuildTarget.Android
                ? $"Builds/Android/FastIoCBench-{profile}.apk"
                : $"Builds/iOS/FastIoCBench-{profile}";
            Directory.CreateDirectory(Path.GetDirectoryName(location));

            BuildPlayerOptions options = new()
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
            };

            Stopwatch sw = Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            string message = $"[FastIoCBench] {target} build {summary.result} in {sw.Elapsed.TotalSeconds:0} s: {location}, {summary.totalSize / (1024.0 * 1024.0):0.0} MB";
            if (summary.result == BuildResult.Succeeded)
                Debug.Log(message);
            else
                Debug.LogError(message);
        }

        /// <summary>Version of a git package from packages-lock.json: the tag in its URL (or HEAD) plus the locked commit.</summary>
        private static string LockedVersion(string urlFragment)
        {
            const string lockPath = "Packages/packages-lock.json";
            if (!File.Exists(lockPath))
                return null;

            string text = File.ReadAllText(lockPath);
            Match url = Regex.Match(text, "\"version\":\\s*\"([^\"]*" + Regex.Escape(urlFragment) + "[^\"]*)\"");
            if (!url.Success)
                return null;

            string block = text.Substring(url.Index, Math.Min(800, text.Length - url.Index));
            Match hash = Regex.Match(block, "\"hash\":\\s*\"([0-9a-fA-F]+)\"");
            string value = url.Groups[1].Value;
            int tag = value.LastIndexOf('#');
            string result = tag >= 0 ? value.Substring(tag + 1) : "HEAD";
            if (hash.Success)
                result += "@" + hash.Groups[1].Value.Substring(0, Math.Min(10, hash.Groups[1].Value.Length));
            return result;
        }

        private static string Git(string workingDirectory, string arguments)
        {
            try
            {
                ProcessStartInfo psi = new("git", arguments)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                // Unity launched from the Dock doesn't inherit the shell PATH.
                psi.EnvironmentVariables["PATH"] = "/usr/bin:/usr/local/bin:/opt/homebrew/bin:" + Environment.GetEnvironmentVariable("PATH");
                using Process process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);
                return output;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoCBench] git {arguments} failed: {ex.Message}");
                return "";
            }
        }
    }

    /// <summary>Writes fresh build info (new build id, current commit) into every player build.</summary>
    internal sealed class BenchBuildInfoPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (string.IsNullOrEmpty(BenchPipeline.CurrentProfile))
                return;

            if (EditorUserBuildSettings.development)
                Debug.LogWarning("[FastIoCBench] Development Build is on. Use its results only for allocations, never for timings.");

            BenchBuildInfo info = BenchBuild.WriteBuildInfo(report.summary.platform, forPlayer: true);
            Debug.Log($"[FastIoCBench] Build info: profile {info.profile}, commit {info.gitCommit.Substring(0, 7)}{(info.gitDirty ? " (dirty)" : "")}, Zenject {info.zenjectVersion}, VContainer {info.vcontainerVersion}, stripping {info.strippingLevel}, {info.rounds} rounds.");
        }
    }

    /// <summary>Play Mode needs build info too, so the runner can be checked in the Editor.</summary>
    [InitializeOnLoad]
    internal static class BenchPlayModeInfo
    {
        static BenchPlayModeInfo()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode || string.IsNullOrEmpty(BenchPipeline.CurrentProfile))
                    return;

                try
                {
                    BenchBuild.WriteBuildInfo(EditorUserBuildSettings.activeBuildTarget, forPlayer: false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(ex.Message);
                }
            };
        }
    }
}

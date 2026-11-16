#if UNITY_EDITOR
using System.IO;
using FastIoC.Core;
using FastIoC.Editor.Graph;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace FastIoC.Editor
{
    /// <summary>
    /// Stops a player build when the Editor hasn't loaded the latest generated code yet, or when the project has
    /// installers but no generated code.
    /// The scene baker reads the generated injectors loaded in the Editor; building right after a regeneration,
    /// before Unity recompiles and reloads, would bake scenes from the old code (or without a manifest).
    /// </summary>
    [InitializeOnLoad]
    internal sealed class GeneratedCodeBuildGuard : IPreprocessBuildWithReport
    {
        private const string PENDING_KEY = "FastIoC.GeneratedCodeNotLoaded";
        private const string MODULE_FILE = FastIoCConstants.GENERATED_PATH + "/GeneratedModule.cs";

        static GeneratedCodeBuildGuard()
        {
            // A script reload just happened, so the Editor now runs the generated code on disk.
            SessionState.EraseBool(PENDING_KEY);
        }

        /// <summary>Called when generation writes a changed file.</summary>
        internal static void MarkGeneratedCodeChanged() => SessionState.SetBool(PENDING_KEY, true);

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            const string waitAndRetry = "Wait for Unity to finish compiling (click into the Editor if nothing happens), then build again.";

            if (Generation.GeneratedCodeRecovery.HasFailedCode)
                throw new BuildFailedException("[FastIoC] Generated code didn't compile and was removed, so this build would have no dependency injection. " +
                                               "Fix the cause listed in " + FastIoCConstants.GENERATED_PATH + "/GenerationStatus.json, then regenerate.");

            if (SessionState.GetBool(PENDING_KEY, false))
                throw new BuildFailedException("[FastIoC] Fast IoC generated new code that the Editor hasn't loaded yet. " + waitAndRetry);

            if (File.Exists(MODULE_FILE) && !GeneratedModuleRegistry.IsRegistered)
                throw new BuildFailedException("[FastIoC] The generated code isn't loaded in the Editor yet. " + waitAndRetry);

            // Installers without generated code would build a player with no injection and no error.
            if (!File.Exists(MODULE_FILE) && InstallerGraphCollector.CollectInstallers().Count > 0)
                throw new BuildFailedException("[FastIoC] The project has installers but no generated code, so this build would have no dependency injection. " +
                                               "Run Tools > FastIoC > Regenerate Code, wait for Unity to compile, then build again.");
        }
    }
}
#endif

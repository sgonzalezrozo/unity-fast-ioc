#if UNITY_EDITOR
using FastIoC.API;
using FastIoC.Editor.Generation;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace FastIoC.Editor
{
    /// <summary>
    /// Regenerates the code before every player build, so a build never uses code that's missing or out of date.
    /// Unchanged files aren't rewritten, so an up-to-date project builds as usual. If anything changed, the Editor
    /// hasn't compiled it yet, and <see cref="GeneratedCodeBuildGuard"/> stops the build and asks to build again.
    /// Define <c>FASTIOC_SKIP_BUILD_GENERATION</c> to turn this off.
    /// </summary>
    internal sealed class GeneratedCodeBuildRegenerator : IPreprocessBuildWithReport
    {
        // Before the guard (-1000), so the guard sees what generation changed.
        public int callbackOrder => -2000;

        public void OnPreprocessBuild(BuildReport report)
        {
#if !FASTIOC_SKIP_BUILD_GENERATION
            GenerationCoordinator.GenerateAll();

            if (GenerationStatusReader.WasLastGenerationFailed(out _))
                throw new BuildFailedException("[FastIoC] Code generation failed before the build. Fix the errors in Assets/FastIoC/Generated/GenerationStatus.json, then build again.");
#endif
        }
    }
}
#endif

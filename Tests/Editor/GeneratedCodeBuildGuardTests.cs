using FastIoC.Editor;
using NUnit.Framework;

namespace FastIoC.Tests.Editor
{
    // A build stopped by out-of-date generated code told batch-mode builds to click into the Editor, which they don't
    // have. In batch mode nothing compiles the new code within the same run: generation goes first, as its own run.
    public class GeneratedCodeBuildGuardTests
    {
        [Test]
        public void InBatchMode_TheAdviceIsToGenerateFirst_OrBuildAgain()
        {
            string advice = GeneratedCodeBuildGuard.RetryAdvice(true);

            StringAssert.Contains("FastIoC.Editor.Generation.GenerationCoordinator.GenerateAll", advice);
            StringAssert.Contains("run the build again", advice);
            StringAssert.DoesNotContain("click into the Editor", advice);
        }

        [Test]
        public void InTheEditor_TheAdviceIsToWaitForTheCompile()
        {
            StringAssert.Contains("click into the Editor", GeneratedCodeBuildGuard.RetryAdvice(false));
        }
    }
}

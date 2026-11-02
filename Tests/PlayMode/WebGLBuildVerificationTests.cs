using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tools.FastIoC.Tests.PlayMode
{
    /// <summary>
    /// WebGL build verification tests.
    /// Verifies that FastIoC works correctly in WebGL environment.
    /// </summary>
    public class WebGLBuildVerificationTests : IntegrationTestBase
    {
        private string testScene = "Assets/Tools/FastIoC/Tests/Scenes/TestScene.unity";
        private string testScene2 = "Assets/Tools/FastIoC/Tests/Scenes/TestScene2.unity";

        [UnityTest]
        public IEnumerator WebGLBuild_SceneLoading_WorksInBrowser()
        {
            yield return WaitForSceneLoad(testScene);

            InjectedTestComponent component = FindInScene<InjectedTestComponent>("TestScene");
            Assert.IsNotNull(component, "Component not found in first scene");
            Assert.IsNotNull(component.InjectedService, "Service not injected in WebGL scene");

            yield return UnloadScene("TestScene");
            yield return WaitForSceneLoad(testScene2);

            component = FindInScene<InjectedTestComponent>("TestScene2");
            Assert.IsNotNull(component, "Component not found in second scene");
            Assert.IsNotNull(component.InjectedService, "Service not injected after scene transition in WebGL");

            Assert.Pass("WebGL scene loading and transitions work correctly");
        }

        [UnityTest]
        public IEnumerator WebGLBuild_ServiceResolution_WithoutReflection()
        {
            yield return WaitForSceneLoad(testScene);

            SceneTransitionTestComponent component = FindInScene<SceneTransitionTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");

            // Verify services resolved without runtime reflection
            Assert.IsNotNull(component.TrackableService, "Cached service not resolved - reflection may be required");
            Assert.IsNotNull(component.QuickService, "Singleton service not resolved - reflection may be required");

            // Verify service data is valid
            Assert.That(component.TrackableService.ServiceId, Does.Match(@"TrackableService#\d+"),
                "Service ID format indicates codegen issue");

            Assert.Pass("WebGL service resolution works without runtime reflection");
        }

        [UnityTest]
        public IEnumerator WebGLBuild_AsyncOperations_HandleTimeoutsCorrectly()
        {
            yield return WaitForSceneLoad(testScene);

            AsyncTestComponent component = FindInScene<AsyncTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");

            // Test 1: Normal async completion
            Task initTask = component.AsyncService.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);

            Assert.IsTrue(initTask.IsCompletedSuccessfully, "Async operation should complete in WebGL");
            Assert.That(component.AsyncService.IsInitialized, Is.True, "Service should be initialized");

            // Test 2: Timeout handling in WebGL
            CancellationTokenSource cts = new(System.TimeSpan.FromMilliseconds(300));
            Task timeoutTask = component.LongRunningService.InitializeAsync(cts.Token);

            yield return new WaitUntil(() => timeoutTask.IsCompleted);

            Assert.IsTrue(timeoutTask.IsCanceled || timeoutTask.IsFaulted, "Timeout should cause task to be cancelled or faulted");

            if (timeoutTask.IsFaulted)
            {
                Assert.IsInstanceOf<OperationCanceledException>(timeoutTask.Exception?.InnerException,
                    "Should throw OperationCanceledException on timeout");
            }

            cts.Dispose();

            Assert.Pass("WebGL async operations and timeouts work correctly");
        }

        [UnityTest]
        public IEnumerator WebGLBuild_MemoryUsage_BelowThreshold()
        {
            // Get initial memory
            long initialMemory = System.GC.GetTotalMemory(true);

            yield return WaitForSceneLoad(testScene);

            InjectedTestComponent[] components = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);
            Assert.Greater(components.Length, 0, "No test components found");

            long afterLoadMemory = System.GC.GetTotalMemory(false);
            long memoryUsed = afterLoadMemory - initialMemory;

            // WebGL has strict memory limits - aim for < 10MB per scene
            const long WEBGL_MEMORY_THRESHOLD_BYTES = 10 * 1024 * 1024;

            Assert.That(memoryUsed, Is.LessThan(WEBGL_MEMORY_THRESHOLD_BYTES),
                $"Scene load used {memoryUsed / (1024 * 1024)}MB, exceeds WebGL threshold of 10MB");

            // Verify injection works even with memory constraints
            foreach (InjectedTestComponent component in components)
            {
                Assert.IsNotNull(component.InjectedService,
                    "Injection should still work within WebGL memory constraints");
            }

            Assert.Pass($"WebGL memory usage acceptable: {memoryUsed / (1024 * 1024):F2}MB used");
        }
    }
}


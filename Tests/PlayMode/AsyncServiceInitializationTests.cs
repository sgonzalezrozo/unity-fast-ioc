using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tools.FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Async service initialization and timeout handling tests.
    /// Verifies that async services initialize properly and timeouts work correctly.
    /// </summary>
    public class AsyncServiceInitializationTests : IntegrationTestBase
    {
        private string testScene = "Assets/Tools/FastIoC/Tests/Scenes/TestScene.unity";

        [UnityTest]
        public IEnumerator AsyncInitialization_WithoutCancellation_CompletesSuccessfully()
        {
            yield return WaitForSceneLoad(testScene);

            AsyncTestComponent component = FindInScene<AsyncTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");
            Assert.IsNotNull(component.AsyncService, "Async service not injected");
            Assert.That(component.AsyncService.IsInitialized, Is.False, "Service should not be initialized yet");

            Task initTask = component.AsyncService.InitializeAsync(CancellationToken.None);
            yield return new WaitUntil(() => initTask.IsCompleted);

            Assert.IsTrue(initTask.IsCompletedSuccessfully, "Initialization task did not complete successfully");
            Assert.That(component.AsyncService.IsInitialized, Is.True, "Service should be initialized");
            Assert.That(component.AsyncService.InitializationData, Is.Not.Empty, "Initialization data should be set");
        }

        [UnityTest]
        public IEnumerator AsyncInitialization_WithTimeout_ThrowsCancellationException()
        {
            yield return WaitForSceneLoad(testScene);

            AsyncTestComponent component = FindInScene<AsyncTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");
            Assert.IsNotNull(component.LongRunningService, "Long-running service not injected");
            Assert.That(component.LongRunningService.IsInitialized, Is.False, "Service should not be initialized");

            CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(500));
            Task initTask = component.LongRunningService.InitializeAsync(cts.Token);

            yield return new WaitUntil(() => initTask.IsCompleted);

            Assert.IsTrue(initTask.IsCanceled || initTask.IsFaulted, "Task should be cancelled or faulted");
            Assert.That(component.LongRunningService.IsInitialized, Is.False, "Service should not be marked initialized after timeout");

            if (initTask.IsFaulted)
            {
                Assert.IsInstanceOf<OperationCanceledException>(initTask.Exception?.InnerException,
                    "Exception should be OperationCanceledException");
            }

            cts.Dispose();
        }

        [UnityTest]
        public IEnumerator AsyncInitialization_MultipleServices_AllInitialize()
        {
            yield return WaitForSceneLoad(testScene);

            AsyncTestComponent component = FindInScene<AsyncTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");
            Assert.IsNotNull(component.AsyncService, "Async service not injected");

            Task initTask = component.AsyncService.InitializeAsync();

            yield return new WaitUntil(() => initTask.IsCompleted);

            Assert.IsTrue(initTask.IsCompletedSuccessfully, "Async service initialization failed");
            Assert.That(component.AsyncService.IsInitialized, Is.True, "Async service should be initialized");
        }

        [UnityTest]
        public IEnumerator AsyncInitialization_OnSceneLoad_InitializesBeforeResolve()
        {
            yield return WaitForSceneLoad(testScene);

            AsyncTestComponent component = FindInScene<AsyncTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component should be found");
            Assert.IsNotNull(component.AsyncService, "Async service should be injected");

            CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
            Task initTask = component.AsyncService.InitializeAsync(cts.Token);

            yield return new WaitUntil(() => initTask.IsCompleted);

            Assert.IsTrue(initTask.IsCompletedSuccessfully, "Service initialization should complete within timeout");
            Assert.That(component.AsyncService.IsInitialized, Is.True, "Service should be initialized");

            cts.Dispose();
        }
    }
}


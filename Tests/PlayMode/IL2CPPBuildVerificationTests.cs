using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FastIoC.Tests.PlayMode
{
    /// <summary>
    /// IL2CPP build verification tests.
    /// Verifies that FastIoC works correctly when compiled with IL2CPP.
    /// </summary>
    public class IL2CPPBuildVerificationTests : IntegrationTestBase
    {
        private string testScene = "Assets/Tools/FastIoC/Tests/Scenes/TestScene.unity";

        [UnityTest]
        public IEnumerator IL2CPPBuild_ReflectionMetadata_PreservedCorrectly()
        {
            yield return WaitForSceneLoad(testScene);

            InjectedTestComponent component = FindInScene<InjectedTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");

            Assert.IsNotNull(component.InjectedService, "Service was not injected - reflection metadata may be missing");
            Assert.That(component.InjectedService.GetTestData(), Is.Not.Empty,
                "Service didn't initialize - codegen metadata lost");

            Assert.Pass("Reflection metadata preserved correctly for IL2CPP");
        }

        [UnityTest]
        public IEnumerator IL2CPPBuild_ServiceResolution_WorksOnDevice()
        {
            yield return WaitForSceneLoad(testScene);

            SceneTransitionTestComponent component = FindInScene<SceneTransitionTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");

            Assert.IsNotNull(component.TrackableService, "Cached service not resolved");
            Assert.IsNotNull(component.QuickService, "Singleton service not resolved");

            string trackableId = component.TrackableService.ServiceId;
            string quickData = component.QuickService.GetData();

            Assert.That(trackableId, Is.Not.Empty, "Cached service ID is empty");
            Assert.That(quickData, Is.Not.Empty, "Singleton service data is empty");

            Assert.Pass("IL2CPP service resolution works correctly");
        }

        [UnityTest]
        public IEnumerator IL2CPPBuild_NoReflectionExceptions_OnLoad()
        {
            yield return WaitForSceneLoad(testScene);

            InjectedTestComponent[] components = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);
            Assert.Greater(components.Length, 0, "No test components found");

            foreach (InjectedTestComponent component in components)
            {
                Assert.IsNotNull(component.InjectedService,
                    $"Component {component.name} injection failed - possible reflection exception during load");
            }

            Assert.Pass("No reflection exceptions encountered during IL2CPP scene load");
        }

        [UnityTest]
        public IEnumerator IL2CPPBuild_Performance_WithinThreshold()
        {
            float startTime = Time.realtimeSinceStartup;

            yield return WaitForSceneLoad(testScene);

            float loadTime = Time.realtimeSinceStartup - startTime;

            AsyncTestComponent component = FindInScene<AsyncTestComponent>("TestScene");
            Assert.IsNotNull(component, "Test component not found");

            startTime = Time.realtimeSinceStartup;
            var asyncTask = component.AsyncService.InitializeAsync();
            yield return new WaitUntil(() => asyncTask.IsCompleted);
            float asyncTime = Time.realtimeSinceStartup - startTime;

            const float SCENE_LOAD_THRESHOLD_MS = 2000f;
            const float ASYNC_INIT_THRESHOLD_MS = 500f;

            Assert.That(loadTime * 1000f, Is.LessThan(SCENE_LOAD_THRESHOLD_MS),
                $"Scene load took {loadTime * 1000f}ms, exceeds threshold of {SCENE_LOAD_THRESHOLD_MS}ms");

            Assert.That(asyncTime * 1000f, Is.LessThan(ASYNC_INIT_THRESHOLD_MS),
                $"Async init took {asyncTime * 1000f}ms, exceeds threshold of {ASYNC_INIT_THRESHOLD_MS}ms");

            Assert.Pass($"IL2CPP performance acceptable: SceneLoad={loadTime * 1000f:F1}ms, AsyncInit={asyncTime * 1000f:F1}ms");
        }
    }
}


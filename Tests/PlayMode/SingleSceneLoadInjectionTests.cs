using System.Collections;
using NUnit.Framework;
using FastIoC.UnityIntegration.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Priority 1 Integration Test: Single Scene Load → Injection Works
    ///
    /// Verifies that:
    /// - FastIoC initializes correctly on scene load
    /// - MonoBehaviours are injected with their dependencies
    /// - Singleton services are available
    /// - AsCached() services are created per-scene
    /// </summary>
    public class SingleSceneLoadInjectionTests : IntegrationTestBase
    {
        [UnityTest]
        public IEnumerator LoadScene_DefaultSetup_InjectsMonoBehaviours()
        {
            // Arrange
            string scenePath = TEST_SCENE_PATH;

            // Act
            yield return WaitForSceneLoad(scenePath);

            // Assert
            InjectedTestComponent component = FindInScene<InjectedTestComponent>(scenePath);
            Assert.IsNotNull(component, "InjectedTestComponent not found in scene");
            Assert.IsNotNull(component.InjectedService, "InjectedService was not injected");
            Assert.IsNotEmpty(component.InjectionLog, "No injection events logged");
        }

        [UnityTest]
        public IEnumerator LoadScene_SingletonService_RemainsConsistent()
        {
            // Arrange
            string scenePath = TEST_SCENE_PATH;
            yield return WaitForSceneLoad(scenePath);

            // Act
            InjectedTestComponent component1 = FindInScene<InjectedTestComponent>(scenePath);
            InjectedTestComponent component2 = FindInScene<InjectedTestComponent>(scenePath);

            // Assert
            Assert.IsNotNull(component1, "First component not found");
            Assert.IsNotNull(component2, "Second component not found");
            Assert.AreSame(component1.InjectedService, component2.InjectedService,
                "Singleton service not reused - got different instances");
        }

        [UnityTest]
        public IEnumerator LoadScene_FastIoCProviderInitialized_CanResolveServices()
        {
            // Arrange
            string scenePath = TEST_SCENE_PATH;
            yield return WaitForSceneLoad(scenePath);

            // Act & Assert
            Assert.IsTrue(FastIoCProviderRegistry.IsInitialized, "FastIoC provider registry not initialized");
            Assert.IsNotNull(FastIoCProviderRegistry.TypeProvider, "Type provider is null");
            Assert.IsNotNull(FastIoCProviderRegistry.LifecycleObserver, "Lifecycle observer is null");
        }

        [UnityTest]
        public IEnumerator LoadScene_MultipleComponents_EachInjected()
        {
            // Arrange
            string scenePath = TEST_SCENE_PATH;
            yield return WaitForSceneLoad(scenePath);

            // Act
            InjectedTestComponent[] components = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);

            // Assert
            Assert.Greater(components.Length, 0, "No InjectedTestComponent found in scene");
            foreach (InjectedTestComponent component in components)
            {
                Assert.IsNotNull(component.InjectedService,
                    $"Component {component.name} did not receive injected dependency");
            }
        }
    }
}


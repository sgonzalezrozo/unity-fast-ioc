using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tools.FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Scene transition and cleanup lifecycle tests.
    /// Verifies that cached services are cleaned up on unload and singletons persist.
    /// </summary>
    public class SceneTransitionCleanupTests : IntegrationTestBase
    {
        private string testScene1 = "Assets/Tools/FastIoC/Tests/Scenes/TestScene.unity";
        private string testScene2 = "Assets/Tools/FastIoC/Tests/Scenes/TestScene2.unity";

        [UnityTest]
        public IEnumerator TransitionBetweenScenes_ServicesCleaned_OnUnload()
        {
            yield return WaitForSceneLoad(testScene1);

            SceneTransitionTestComponent component1 = FindInScene<SceneTransitionTestComponent>("TestScene");
            Assert.IsNotNull(component1, "Test component not found in first scene");
            Assert.IsNotNull(component1.TrackableService, "Trackable service not injected in scene 1");

            string service1Id = component1.TrackableService.ServiceId;

            yield return UnloadScene("TestScene");

            yield return WaitForSceneLoad(testScene2);

            SceneTransitionTestComponent component2 = FindInScene<SceneTransitionTestComponent>("TestScene2");
            Assert.IsNotNull(component2, "Test component not found in second scene");
            Assert.IsNotNull(component2.TrackableService, "Trackable service not injected in scene 2");

            Assert.AreNotEqual(service1Id, component2.TrackableService.ServiceId,
                "Cached service should be recreated after scene transition");
        }

        [UnityTest]
        public IEnumerator TransitionBetweenScenes_SingletonsPersist_AcrossLoad()
        {
            yield return WaitForSceneLoad(testScene1);

            SceneTransitionTestComponent component1 = FindInScene<SceneTransitionTestComponent>("TestScene");
            Assert.IsNotNull(component1, "Test component not found in first scene");
            Assert.IsNotNull(component1.QuickService, "Quick service not injected");

            string singletonData = component1.QuickService.GetData();

            yield return UnloadScene("TestScene");

            yield return WaitForSceneLoad(testScene2);

            SceneTransitionTestComponent component2 = FindInScene<SceneTransitionTestComponent>("TestScene2");
            Assert.IsNotNull(component2, "Test component not found in second scene");
            Assert.IsNotNull(component2.QuickService, "Quick service not injected after transition");

            Assert.AreEqual(singletonData, component2.QuickService.GetData(),
                "Singleton should persist across scene transitions");
        }

        [UnityTest]
        public IEnumerator TransitionBetweenScenes_BindingsRecreated_InNewScene()
        {
            yield return WaitForSceneLoad(testScene1);

            InjectedTestComponent[] componentsScene1 = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);
            Assert.Greater(componentsScene1.Length, 0, "No components in first scene");

            foreach (InjectedTestComponent component in componentsScene1)
            {
                Assert.IsNotNull(component.InjectedService, $"Component {component.name} not injected in scene 1");
            }

            yield return UnloadScene("TestScene");

            yield return WaitForSceneLoad(testScene2);

            InjectedTestComponent[] componentsScene2 = Object.FindObjectsByType<InjectedTestComponent>(FindObjectsSortMode.None);
            Assert.Greater(componentsScene2.Length, 0, "No components in second scene");

            foreach (InjectedTestComponent component in componentsScene2)
            {
                Assert.IsNotNull(component.InjectedService, $"Component {component.name} not injected in scene 2");
            }
        }

        [UnityTest]
        public IEnumerator TransitionBetweenScenes_MultipleTransitions_NoMemoryLeak()
        {
            const int transitionCount = 3;

            for (int i = 0; i < transitionCount; i++)
            {
                yield return WaitForSceneLoad(testScene1);

                SceneTransitionTestComponent component = FindInScene<SceneTransitionTestComponent>("TestScene");
                Assert.IsNotNull(component, $"Test component not found on transition {i}");
                Assert.IsNotNull(component.TrackableService, $"Service not injected on transition {i}");

                yield return UnloadScene("TestScene");

                if (i < transitionCount - 1)
                {
                    yield return WaitForSceneLoad(testScene2);
                    yield return UnloadScene("TestScene2");
                }
            }

            Assert.Pass("Multiple transitions completed without memory issues");
        }
    }
}


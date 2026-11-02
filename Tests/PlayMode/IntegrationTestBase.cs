using System.Collections;
using NUnit.Framework;
using FastIoC.UnityIntegration.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Base class for FastIoC integration tests (PlayMode).
    /// Provides utilities for testing scene loading, injection, and lifecycle.
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected const float TEST_TIMEOUT = 10f;
        protected const string TEST_SCENE_PATH = "Assets/Tools/FastIoC/Tests/Scenes/TestScene.unity";

        /// <summary>
        /// Ensures FastIoC is initialized before each test.
        /// </summary>
        [SetUp]
        public virtual void Setup()
        {
            // FastIoC should auto-initialize via RuntimeInitializeOnLoadMethod
            Assert.IsTrue(FastIoCProviderRegistry.IsInitialized, "FastIoC provider registry not initialized");
        }

        /// <summary>
        /// Cleans up after each test (unload scenes, reset state).
        /// </summary>
        [TearDown]
        public virtual void TearDown()
        {
            // Unload all loaded scenes except active scene
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene != SceneManager.GetActiveScene())
                {
                    SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        /// <summary>
        /// Waits for a scene to load asynchronously.
        /// </summary>
        protected IEnumerator WaitForSceneLoad(string scenePath)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(scenePath);
            while (!op.isDone)
            {
                yield return null;
            }
            yield return null; // One frame for injection
        }

        /// <summary>
        /// Waits for a scene to unload asynchronously.
        /// If the scene is the only one loaded, suppresses the unload attempt (not supported).
        /// If the scene is the active scene but not the only one, makes another scene active first.
        /// </summary>
        protected IEnumerator WaitForSceneUnload(string sceneName)
        {
            Scene sceneToUnload = SceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(sceneName));

            if (!sceneToUnload.isLoaded)
                yield break;

            Scene activeScene = SceneManager.GetActiveScene();

            // If this is the only loaded scene, we cannot unload it (Unity limitation)
            // This is expected during tests - the test framework may keep a base scene loaded
            if (SceneManager.sceneCount <= 1)
                yield break;

            // If it's the active scene, make another loaded scene active first
            if (sceneToUnload == activeScene)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && scene != sceneToUnload)
                    {
                        SceneManager.SetActiveScene(scene);
                        yield return null; // One frame for active scene change to settle
                        break;
                    }
                }
            }

            // Now unload the scene
            AsyncOperation op = SceneManager.UnloadSceneAsync(sceneName);
            while (!op.isDone)
                yield return null;
        }

        /// <summary>
        /// Finds a component of type T in the active scene.
        /// </summary>
        protected T FindInScene<T>(string sceneName) where T : Component
        {
            Scene scene = SceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(sceneName));
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                T component = root.GetComponentInChildren<T>();
                if (component != null)
                {
                    return component;
                }
            }
            return null;
        }

        /// <summary>
        /// Waits for a specific condition to be true, with timeout.
        /// </summary>
        protected IEnumerator WaitForCondition(System.Func<bool> condition, float timeoutSeconds = 5f)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), $"Condition not met after {timeoutSeconds}s");
        }

        /// <summary>
        /// Helper method for tests to unload a scene safely.
        /// </summary>
        protected IEnumerator UnloadScene(string sceneName)
        {
            yield return WaitForSceneUnload(sceneName);
        }
    }
}


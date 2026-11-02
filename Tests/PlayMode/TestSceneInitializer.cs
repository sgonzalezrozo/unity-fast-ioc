using UnityEngine;

namespace FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Initializes test scenes by adding required components.
    /// Called automatically when test scenes load.
    /// </summary>
    public class TestSceneInitializer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeTestScene()
        {
            // This will be called before each scene loads during tests
            // We'll set up the required components
        }

        private void Start()
        {
            // Create test components if they don't exist
            CreateTestComponentsIfNeeded();
        }

        private static void CreateTestComponentsIfNeeded()
        {
            // Check for InjectedTestComponent
            if (Object.FindFirstObjectByType<InjectedTestComponent>() == null)
            {
                GameObject testObj = new GameObject("TestComponent");
                testObj.AddComponent<InjectedTestComponent>();
                Debug.Log("[TestSceneInitializer] Created InjectedTestComponent");
            }

            // Check for SceneTransitionTestComponent
            if (Object.FindFirstObjectByType<SceneTransitionTestComponent>() == null)
            {
                GameObject transitionObj = new GameObject("SceneTransitionComponent");
                transitionObj.AddComponent<SceneTransitionTestComponent>();
                Debug.Log("[TestSceneInitializer] Created SceneTransitionTestComponent");
            }

            // Check for AsyncTestComponent
            if (Object.FindFirstObjectByType<AsyncTestComponent>() == null)
            {
                GameObject asyncObj = new GameObject("AsyncTestComponent");
                asyncObj.AddComponent<AsyncTestComponent>();
                Debug.Log("[TestSceneInitializer] Created AsyncTestComponent");
            }

            // Note: TestSceneInstaller is a BindingRecorder (FastIoC framework class)
            // It's NOT a MonoBehaviour, so we can't AddComponent it
            // FastIoC will auto-discover and use it via [MonoInstaller] attribute
        }
    }
}


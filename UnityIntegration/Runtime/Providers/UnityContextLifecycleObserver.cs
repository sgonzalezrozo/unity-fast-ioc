using System;
using Tools.FastIoC.Runtime.Core.Abstraction;
using UnityEngine.SceneManagement;

namespace Tools.FastIoC.UnityIntegration.Runtime.Providers
{
    /// <summary>
    /// Observes context lifecycle by listening to Unity's SceneManager events.
    /// When scenes load/unload, notifies observers so FastIoC can create/destroy context-scoped bindings.
    /// </summary>
    public class UnityContextLifecycleObserver : IContextLifecycleObserver
    {
        private bool isObserving;

        public event Action<string> OnContextCreated;
        public event Action<string> OnContextDestroyed;

        public UnityContextLifecycleObserver()
        {
            isObserving = false;
        }

        public void StartObserving()
        {
            if (isObserving)
                return;

            isObserving = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void StopObserving()
        {
            if (!isObserving)
                return;

            isObserving = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            OnContextCreated?.Invoke(scene.name);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            OnContextDestroyed?.Invoke(scene.name);
        }
    }
}


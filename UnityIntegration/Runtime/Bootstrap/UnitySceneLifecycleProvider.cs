using System;
using Tools.FastIoC.Runtime.API;
using UnityEngine.SceneManagement;

namespace Tools.FastIoC.UnityIntegration.Runtime.Bootstrap
{
    public class UnitySceneLifecycleProvider : ISceneLifecycleProvider
    {
        public event Action<string> OnSceneLoaded;
        public event Action<string> OnSceneUnloaded;

        public UnitySceneLifecycleProvider()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        public string GetActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            OnSceneLoaded?.Invoke(scene.name);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            OnSceneUnloaded?.Invoke(scene.name);
        }

        public void Cleanup()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }
    }
}


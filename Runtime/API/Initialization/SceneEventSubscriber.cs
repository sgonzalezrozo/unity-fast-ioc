using System;
using Tools.FastIoC.Runtime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tools.FastIoC.Runtime.API.Initialization
{
    public class SceneEventSubscriber
    {
        private readonly ContextRuntime runtime;

        public SceneEventSubscriber(ContextRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public void Subscribe()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public void Unsubscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (runtime == null)
                return;

            string sceneName = scene.name;

            try
            {
                if (FastIoCReflectionCache.CreateSceneContextRuntime == null)
                    return;

                ContextRuntime tempSceneRuntime = FastIoCReflectionCache.CreateSceneContextRuntime(sceneName) as ContextRuntime;

                if (tempSceneRuntime == null)
                    return;

                RecipeActivator[] sceneActivators = tempSceneRuntime.GetGlobalActivators();
                Lifetime[] sceneLifetimes = tempSceneRuntime.GetGlobalLifetimes();

                if (sceneActivators != null && sceneLifetimes != null)
                    runtime.PushSceneContainer(sceneName, sceneActivators, sceneLifetimes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Failed to load scene container for '{sceneName}': {ex.Message}");
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (runtime == null)
                return;

            try
            {
                runtime.PopSceneContainer(scene.name);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Failed to clear scene container: {ex.Message}");
            }
        }
    }
}

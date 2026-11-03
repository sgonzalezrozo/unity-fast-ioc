using System;
using FastIoC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoC.API.Initialization
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
                IGeneratedModule module = GeneratedModuleRegistry.Module;
                if (module == null)
                    return;

                ContextRuntime tempSceneRuntime = module.CreateSceneContextRuntime(sceneName);
                if (tempSceneRuntime != null)
                {
                    RecipeActivator[] sceneActivators = tempSceneRuntime.GetGlobalActivators();
                    Lifetime[] sceneLifetimes = tempSceneRuntime.GetGlobalLifetimes();

                    if (sceneActivators != null && sceneLifetimes != null)
                        runtime.PushSceneContainer(sceneName, sceneActivators, sceneLifetimes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Failed to load scene container for '{sceneName}': {ex.Message}");
            }

            // Inject the MonoBehaviours of scenes loaded after startup.
            // Scenes already injected (the first scene, injected by ProjectContext) are skipped.
            SceneInjection.InjectScene(scene, runtime);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            SceneInjection.MarkUnloaded(scene);

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

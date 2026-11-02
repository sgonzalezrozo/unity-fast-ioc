using FastIoC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoC.API.Initialization
{
    public class RuntimeBootstrapper
    {
        public ContextRuntime Bootstrap(string sceneName)
        {
            if (!FastIoCReflectionCache.IsInitialized)
            {
                Debug.LogError("[FastIoC] Reflection cache not initialized. Did FrameworkInitializer.Initialize() run?");
                return null;
            }

            if (FastIoCReflectionCache.GeneratedRegistryType == null)
            {
                Debug.LogWarning("[FastIoC] GeneratedRegistry not found. Expected namespace: FastIoCGenerated");
                Debug.LogWarning("[FastIoC] Did you run 'Tools > FastIoC > Setup Project'?");
                Debug.LogWarning("[FastIoC] Check that Assets/FastIoC/Generated/ contains GeneratedRegistry.cs");
                return null;
            }

            FastIoCReflectionCache.EnsureInitialized?.Invoke();

            if (FastIoCReflectionCache.CreateSceneContextRuntime != null)
            {
                ContextRuntime runtime = FastIoCReflectionCache.CreateSceneContextRuntime(sceneName) as ContextRuntime;
                if (runtime != null)
                    return runtime;
            }

            if (FastIoCReflectionCache.CreateProjectContextRuntime != null)
            {
                ContextRuntime runtime = FastIoCReflectionCache.CreateProjectContextRuntime() as ContextRuntime;
                if (runtime == null)
                    Debug.LogError("[FastIoC] Failed to create ProjectContext runtime.");
                return runtime;
            }

            Debug.LogError("[FastIoC] CreateProjectContextRuntime not available in reflection cache.");
            return null;
        }

        public ContextRuntime BootstrapCurrentScene()
            => Bootstrap(SceneManager.GetActiveScene().name);
    }
}


using FastIoC.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FastIoC.API.Initialization
{
    public class RuntimeBootstrapper
    {
        public ContextRuntime Bootstrap(string sceneName)
        {
            IGeneratedModule module = GeneratedModuleRegistry.Module;
            if (module == null)
            {
                Debug.LogWarning("[FastIoC] Generated code is not registered (FastIoCGenerated.GeneratedModule).");
                Debug.LogWarning("[FastIoC] Did you run 'Tools > FastIoC > Setup Project' or 'Tools > FastIoC > Regenerate Code'?");
                Debug.LogWarning("[FastIoC] Check that Assets/FastIoC/Generated/ contains GeneratedModule.cs");
                return null;
            }

            module.EnsureInitialized();

            ContextRuntime runtime = module.CreateSceneContextRuntime(sceneName) ?? module.CreateProjectContextRuntime();
            if (runtime == null)
                Debug.LogError("[FastIoC] Failed to create ProjectContext runtime.");

            return runtime;
        }

        public ContextRuntime BootstrapCurrentScene()
            => Bootstrap(SceneManager.GetActiveScene().name);
    }
}

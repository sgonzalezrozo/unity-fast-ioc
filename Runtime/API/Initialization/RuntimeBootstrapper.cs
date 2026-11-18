using System;
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
                // No installers yet means nothing to inject, which is fine. Installers without the generated code is the problem.
                if (ProjectHasInstallers())
                {
                    Debug.LogWarning("[FastIoC] Generated code is not registered (FastIoCGenerated.GeneratedModule).");
                    Debug.LogWarning("[FastIoC] Did you run 'Tools > FastIoC > Setup Project' or 'Tools > FastIoC > Regenerate Code'?");
                    Debug.LogWarning("[FastIoC] Check that Assets/FastIoC/Generated/ contains GeneratedModule.cs");
                }

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

        // The same installers generation looks for (MonoInstallerScanner). A player build stops when installers have no
        // generated code (GeneratedCodeBuildGuard), so a player without generated code has no installers.
        private static bool ProjectHasInstallers()
        {
#if UNITY_EDITOR
            foreach (Type type in UnityEditor.TypeCache.GetTypesWithAttribute<MonoInstallerAttribute>())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(BindingRecorder).IsAssignableFrom(type))
                    continue;

                MonoInstallerAttribute attribute = (MonoInstallerAttribute)Attribute.GetCustomAttribute(type, typeof(MonoInstallerAttribute));
                if (attribute != null && !attribute.Disabled)
                    return true;
            }
#endif
            return false;
        }
    }
}

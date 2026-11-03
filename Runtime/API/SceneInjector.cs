using FastIoC.Core;
using UnityEngine;

namespace FastIoC.API
{
    public class SceneInjector : MonoBehaviour
    {
        private ContextRuntime context;
        private bool hasRun;

        public void SetContext(ContextRuntime ctx)
        {
            if (ctx == null)
            {
                Debug.LogError("[FastIoC] SceneInjector: Context is null. Did ProjectContext initialize correctly?");
                return;
            }

            context = ctx;

            if (!hasRun)
                InjectInjectableMonoBehavioursInScene();
        }

        private void InjectInjectableMonoBehavioursInScene()
        {
            hasRun = true;

            if (context == null)
            {
                Debug.LogWarning("[FastIoC] SceneInjector: Context not yet set. Skipping injection.");
                return;
            }

            SceneInjection.InjectLoadedScenes(context);
        }

        /// <summary>
        /// Injects a MonoBehaviour created at runtime outside of IInstantiator.
        /// Does nothing if its type has no generated injector.
        /// </summary>
        public static void InjectIfNeeded(MonoBehaviour instance)
        {
            if (instance == null)
            {
                Debug.LogError("[FastIoC] InjectIfNeeded: Instance is null.");
                return;
            }

            ProjectContext projectContext = ProjectContext.Instance;
            if (projectContext == null)
            {
                Debug.LogError("[FastIoC] ProjectContext not initialized. Cannot inject dynamically instantiated MonoBehaviour.");
                return;
            }

            ContextRuntime contextRuntime = projectContext.Runtime;
            if (contextRuntime == null)
            {
                Debug.LogError("[FastIoC] ProjectContext.Runtime is not initialized.");
                return;
            }

            InjectionRunner.TryInject(instance, contextRuntime);
        }
    }
}

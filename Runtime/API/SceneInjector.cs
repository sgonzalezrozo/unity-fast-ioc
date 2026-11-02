using FastIoC.API.Injection;
using FastIoC.Core;
using UnityEngine;

namespace FastIoC.API
{
    public class SceneInjector : MonoBehaviour
    {
        private static CachedTypeResolver s_cachedTypeResolver;
        private static InjectionCoordinator s_cachedCoordinator;

        private ContextRuntime context;
        private bool hasRun;
        private InjectionCoordinator injectionCoordinator;

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

            InitializeCachedCoordinator();
            injectionCoordinator ??= s_cachedCoordinator;
            injectionCoordinator.InjectScene(context);
        }

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

            try
            {
                if (FastIoCReflectionCache.IsInitialized && FastIoCReflectionCache.InjectMonoBehaviour != null)
                    FastIoCReflectionCache.InjectMonoBehaviour(instance, contextRuntime);
                else
                {
                    InitializeCachedCoordinator();
                    s_cachedCoordinator.InjectInstance(instance, contextRuntime);
                }
            }
            catch (System.Exception ex)
            {
                if (ex.Message.Contains("not registered") || ex.InnerException?.Message.Contains("not registered") == true)
                    return;

                Debug.LogError($"[FastIoC] Unexpected error during injection of {instance.GetType().Name}: {ex.Message}");
            }
        }

        private static void InitializeCachedCoordinator()
        {
            if (s_cachedCoordinator != null)
                return;

            s_cachedTypeResolver ??= new CachedTypeResolver();
            s_cachedCoordinator = new InjectionCoordinator(s_cachedTypeResolver);
        }
    }
}

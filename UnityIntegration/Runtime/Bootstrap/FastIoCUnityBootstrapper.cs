using System;
using ToolsCore;
using FastIoC.Adapters;
using FastIoC.Core.Abstraction;
using FastIoC.UnityIntegration.Runtime.Providers;
using UnityEngine;

namespace FastIoC.UnityIntegration.Runtime.Bootstrap
{
    public static class FastIoCUnityBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            try
            {
                ITypeMetadataProvider typeProvider = new UnityAppDomainMetadataProvider();
                IContextLifecycleObserver lifecycleObserver = new UnityContextLifecycleObserver();

                FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver);
                ToolInjectionServices.MonoBehaviourInjector = new FastIoCMonoBehaviourInjector();
                lifecycleObserver.StartObserving();
                ProjectContextHelper.LoadOrCreateProjectContext();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Bootstrap failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

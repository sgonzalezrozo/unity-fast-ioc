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
        /// <summary>
        /// When true (the default), Fast IoC starts before the first scene loads.
        /// Set it to false from a <c>SubsystemRegistration</c> or <c>AfterAssembliesLoaded</c> method
        /// to start Fast IoC later with <see cref="Start"/>.
        /// </summary>
        public static bool AutoStart { get; set; } = true;

        /// <summary>True once <see cref="Start"/> has run in this session.</summary>
        public static bool IsStarted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // AutoStart is not reset here: another SubsystemRegistration method may have set it already.
            IsStarted = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (AutoStart)
                Start();
        }

        /// <summary>
        /// Starts Fast IoC: creates the ProjectContext and injects the scenes that are loaded.
        /// Does nothing if Fast IoC has already started.
        /// </summary>
        public static void Start()
        {
            if (IsStarted)
                return;

            IsStarted = true;
            long start = FastIoC.Core.BootTiming.Now();
            FastIoC.Core.BootTiming.Point("Bootstrap start");

            try
            {
                ITypeMetadataProvider typeProvider = new UnityAppDomainMetadataProvider();
                IContextLifecycleObserver lifecycleObserver = new UnityContextLifecycleObserver();

                FastIoCProviderRegistry.Initialize(typeProvider, lifecycleObserver);
                ToolInjectionServices.MonoBehaviourInjector = new FastIoCMonoBehaviourInjector();
                lifecycleObserver.StartObserving();
                ProjectContextHelper.LoadOrCreateProjectContext();
                FastIoC.Core.BootTiming.Span("Bootstrap total", start);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Bootstrap failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastIoC.API.Initialization;
using FastIoC.Core;
using UnityEngine;

namespace FastIoC.API
{
    public class ProjectContext : MonoBehaviour, IConstrainedToRoot
    {
        private static ProjectContext instance;
        private ContextRuntime runtime;
        // Filled by the generated injector. Must be internal so generated code can assign it directly.
        internal IInstantiator instantiator;
        private CancellationTokenSource bootstrapCts;

        [SerializeField] private GameObject hostedRoot;

        private SceneEventSubscriber sceneSubscriber;

        public static ProjectContext Instance => instance;
        /// <summary>The inactive child GameObject whose components are managed by FastIoC.</summary>
        public GameObject HostedRoot => hostedRoot;

        [ServiceLocatorGuard(
            "Warning: Direct access to Runtime indicates Service Locator anti-pattern. " +
            "This property is only for FastIoC framework use. " +
            "Prefer constructor injection or field injection for dependencies.")]

        public ContextRuntime Runtime => runtime;

        private void Awake()
        {
            if (!SingletonEnforcer<ProjectContext>.EnforceSingleton(this))
                return;

            instance = this;

            ValidateGenerationStatus();

            FrameworkInitializer frameworkInit = new();
            frameworkInit.Initialize();

            ValidateRootConstraint();

            RuntimeBootstrapper bootstrapper = new();
            runtime = bootstrapper.BootstrapCurrentScene();
            if (runtime == null)
                return;

            InitializeInstanceRegistry();
            InjectCurrentSceneMonoBehaviours();
            InjectHostedMonoBehaviours();
            CreateNonLazyInstances();
            ActivateHostedRoot();

            sceneSubscriber = new(runtime);
            sceneSubscriber.Subscribe();

            // Nothing created so far needs initializing: skip the task, token and semaphore.
            if (runtime.InitializableCount > 0)
            {
                bootstrapCts = new CancellationTokenSource();
                _ = InitializeAsyncServicesAsync();
            }
        }

        private void ValidateRootConstraint()
        {
            if (transform.parent != null)
            {
                Debug.LogError(
                    $"[FastIoC] CONSTRAINT VIOLATION: ProjectContext '{gameObject.name}' must be at the root of its prefab. " +
                    $"Parent GameObjects are not allowed. Current parent: {transform.parent.gameObject.name}",
                    gameObject);
            }
        }

        private void ValidateGenerationStatus()
        {
#if UNITY_EDITOR
            if (!GenerationStatusReader.WasLastGenerationFailed(out GenerationStatus status))
                return;

            string errorMessage = $"[FastIoC] CODE GENERATION FAILED\n" +
                                  $"Last attempt: {status.lastUpdateTime}\n" +
                                  $"Error: {status.errorReason}";

            List<string> details = status.errorDetails.ToList();
            if (details.Count > 0)
            {
                errorMessage += "\n\nDetails:";
                foreach (string detail in details)
                    errorMessage += $"\n  • {detail}";
            }

            errorMessage += "\n\nFix the issues and regenerate code:\n" +
                            "Tools > FastIoC > Regenerate Code";

            Debug.LogError(errorMessage, gameObject);
            Debug.Break();
#endif
        }

        private void InitializeInstanceRegistry()
        {
            try
            {
                GeneratedModuleRegistry.Module?.InitializeInstances();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Could not initialize instance registry: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CreateNonLazyInstances()
        {
            if (runtime == null)
                return;

            try
            {
                GeneratedModuleRegistry.Module?.CreateNonLazyInstances(runtime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FastIoC] Could not create NonLazy instances: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void InjectHostedMonoBehaviours()
        {
            if (hostedRoot == null || runtime == null)
                return;

            foreach (MonoBehaviour mb in hostedRoot.GetComponentsInChildren<MonoBehaviour>(true))
                InjectionRunner.TryInject(mb, runtime);
        }

        private void ActivateHostedRoot()
        {
            if (hostedRoot != null)
                hostedRoot.SetActive(true);
        }

        private void InjectCurrentSceneMonoBehaviours()
        {
            if (runtime == null)
            {
                Debug.LogError("[FastIoC] Runtime not initialized, cannot inject");
                return;
            }

            SceneInjector sceneInjector = gameObject.GetComponent<SceneInjector>();
            if (sceneInjector == null)
            {
                sceneInjector = gameObject.AddComponent<SceneInjector>();
            }

            sceneInjector.SetContext(runtime);
        }

        private async Task InitializeAsyncServicesAsync()
        {
            if (runtime == null)
            {
                Debug.LogWarning("[FastIoC] Runtime is null - cannot initialize async services");
                return;
            }

            try
            {
                await runtime.InitializeAllAsync(maxConcurrency: 4, bootstrapCts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[FastIoC] Async initialization was cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FastIoC] Async initialization failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnDestroy()
        {
            sceneSubscriber?.Unsubscribe();
            bootstrapCts?.Cancel();
            bootstrapCts?.Dispose();

            runtime?.Dispose();
            instance = null;
            SingletonEnforcer<ProjectContext>.Reset();
        }
    }
}

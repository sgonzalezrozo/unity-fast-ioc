using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastIoC.Core
{
    public class ContextRuntime
    {
        private readonly RecipeActivator[] globalActivators;
        private readonly Lifetime[] globalLifetimes;
        private readonly InstanceCacheArray cacheArray;
        private readonly int maxTypeId;
        private readonly SceneContainerStack sceneStack;
        private readonly IResolutionStrategy resolutionStrategy;
        private readonly InitializationCoordinator initCoordinator;
        private readonly DisposalCoordinator disposeCoordinator;
        private readonly DiagnosticsReporter diagnostics;

        private bool isDisposed;

        public ContextRuntime(int maxTypeId, RecipeActivator[] activators, Lifetime[] lifetimes)
        {
            this.maxTypeId = maxTypeId;
            this.globalActivators = activators ?? throw new ArgumentNullException(nameof(activators));
            this.globalLifetimes = lifetimes ?? throw new ArgumentNullException(nameof(lifetimes));
            this.cacheArray = new InstanceCacheArray(maxTypeId);

            sceneStack = new(cacheArray);
            resolutionStrategy = new DualContainerResolutionStrategy(sceneStack);
            initCoordinator = new();
            disposeCoordinator = new(cacheArray);
            diagnostics = new();
        }

        public void PushSceneContainer(string sceneName, RecipeActivator[] activators, Lifetime[] lifetimes)
            => sceneStack.Push(sceneName, activators, lifetimes);

        public void PopSceneContainer(string sceneName)
            => sceneStack.Pop(sceneName);

        public RecipeActivator[] GetTopSceneActivators()
            => sceneStack.GetTopActivators();

        public Lifetime[] GetTopSceneLifetimes()
            => sceneStack.GetTopLifetimes();

        public RecipeActivator[] GetGlobalActivators()
            => globalActivators;

        public Lifetime[] GetGlobalLifetimes()
            => globalLifetimes;

        public string PrintInjectionsInBinding()
        {
            StringBuilder sb = new();
            sb.AppendLine("[FastIoC] Registered Bindings:");

            sb.AppendLine("  GLOBAL CONTAINER:");
            int globalCount = 0;
            for (int i = 0; i <= maxTypeId && i < globalActivators.Length; i++)
            {
                if (globalActivators[i] == null)
                    continue;

                Lifetime lifetime = globalLifetimes[i];
                sb.AppendLine($"    [{i}] Lifetime: {lifetime}");
                globalCount++;
            }

            if (globalCount == 0)
                sb.AppendLine("    (no global bindings)");

            if (sceneStack.Count > 0)
            {
                sb.AppendLine(sceneStack.PrintDiagnostics());
            }

            return sb.ToString();
        }

        public object Resolve(TypeId typeId)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (ServiceLocatorEnforcer.EnforcementLevel != ServiceLocatorEnforcementLevel.Off)
                ServiceLocatorEnforcer.CheckResolution(skipFrames: 2);
            #endif

            if (TryResolveCore(typeId, out object instance))
                return instance;

            throw new InvalidOperationException($"TypeId {typeId.Id} is not registered in scene or global container.");
        }

        /// <summary>
        /// Resolves <paramref name="typeId"/>, or returns false if it isn't bound in the active scopes.
        /// Generated injectors use it, so a binding missing from a scene doesn't cost an exception.
        /// </summary>
        public bool TryResolve(TypeId typeId, out object instance)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (ServiceLocatorEnforcer.EnforcementLevel != ServiceLocatorEnforcementLevel.Off)
                ServiceLocatorEnforcer.CheckResolution(skipFrames: 2);
            #endif

            return TryResolveCore(typeId, out instance);
        }

        private bool TryResolveCore(TypeId typeId, out object instance)
        {
            if (isDisposed)
                throw new InvalidOperationException("Context has been disposed.");

            return resolutionStrategy.TryResolve(typeId, typeId.Id, globalActivators, globalLifetimes, cacheArray, this, out instance);
        }

        /// <summary>
        /// Registers a newly activated instance for its lifecycle hooks.
        /// Called only on creation, so a cached instance costs no type checks on later resolves.
        /// </summary>
        internal void TrackCreatedInstance(object instance)
        {
            if (instance is IInitializable or IAsyncInitializable)
                initCoordinator.TrackInstance(instance);
            if (instance is IDisposableHook)
                disposeCoordinator.TrackInstance(instance);
        }

        /// <summary>Instances created so far that implement IInitializable or IAsyncInitializable.</summary>
        public int InitializableCount => initCoordinator.Count;

        public void ReportCacheStatistics()
            => diagnostics.ReportCacheStatistics();

        public void InitializeAll()
            => initCoordinator.InitializeAll();

        public async Task InitializeAllAsync(CancellationToken ct = default)
            => await initCoordinator.InitializeAllAsync(ct);

        public async Task InitializeAllAsync(int maxConcurrency, CancellationToken ct = default)
            => await initCoordinator.InitializeAllAsync(maxConcurrency, ct);

        public void Dispose()
        {
            if (isDisposed)
                return;

            disposeCoordinator.Dispose();
            sceneStack.Clear();
            initCoordinator.Clear();
            isDisposed = true;
        }

        public void ClearSceneSingletons()
        {
            if (isDisposed)
                return;

            disposeCoordinator.ClearSceneSingletons();
        }
    }
}

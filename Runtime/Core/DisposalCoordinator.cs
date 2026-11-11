using System;
using System.Collections.Generic;

namespace FastIoC.Core
{
    /// <summary>
    /// Disposes the <see cref="IDisposable"/> instances the container created, in reverse creation order:
    /// scene singletons and cached instances when a scene unloads, global singletons when the context is disposed.
    /// Instances the container doesn't own are never disposed: transient instances (their consumer owns them),
    /// instance bindings (the project registered them) and <see cref="UnityEngine.Object"/>s (Unity owns them).
    /// An exception from one <c>Dispose</c> is logged, and the remaining instances are still disposed.
    /// </summary>
    public class DisposalCoordinator
    {
        private readonly struct SceneEntry
        {
            public readonly IDisposable Instance;
            public readonly Lifetime Lifetime;

            public SceneEntry(IDisposable instance, Lifetime lifetime)
            {
                Instance = instance;
                Lifetime = lifetime;
            }
        }

        private readonly List<IDisposable> singletons = new();

        // Scene singletons and cached instances share a list, so their disposal follows creation order.
        private readonly List<SceneEntry> sceneInstances = new();

        private readonly HashSet<object> trackedInstances = new(InstanceReferenceComparer.Instance);
        private readonly InstanceCacheArray cacheArray;
        private readonly Func<TypeId, object, bool> isInstanceBinding;
        private bool isDisposed;

        /// <param name="cacheArray">The caches cleared after their instances are disposed.</param>
        /// <param name="isInstanceBinding">
        /// Returns true when an instance came from an instance binding, which the container doesn't own.
        /// Null means no instance bindings.
        /// </param>
        public DisposalCoordinator(InstanceCacheArray cacheArray, Func<TypeId, object, bool> isInstanceBinding = null)
        {
            this.cacheArray = cacheArray ?? throw new ArgumentNullException(nameof(cacheArray));
            this.isInstanceBinding = isInstanceBinding;
        }

        /// <summary>Called once per created instance. Keeps it for disposal if the container owns it.</summary>
        public void TrackInstance(TypeId typeId, Lifetime lifetime, object instance)
        {
            if (isDisposed || instance is not IDisposable disposable || instance is UnityEngine.Object)
                return;

            if (lifetime == Lifetime.Transient)
                return;

            if (isInstanceBinding != null && isInstanceBinding(typeId, instance))
                return;

            if (!trackedInstances.Add(instance))
                return;

            if (lifetime == Lifetime.Singleton)
                singletons.Add(disposable);
            else
                sceneInstances.Add(new SceneEntry(disposable, lifetime));
        }

        /// <summary>
        /// Disposes scene singletons and cached instances, then clears both caches.
        /// Called when a scene unloads.
        /// </summary>
        public void DisposeSceneInstances()
        {
            if (isDisposed)
                return;

            DisposeSceneEntries(disposeCached: true);
            cacheArray.ClearSceneSingletons();
            cacheArray.ClearAllCached();
        }

        /// <summary>Disposes scene singletons only, then clears their cache. Cached instances stay.</summary>
        public void ClearSceneSingletons()
        {
            if (isDisposed)
                return;

            DisposeSceneEntries(disposeCached: false);
            cacheArray.ClearSceneSingletons();
        }

        /// <summary>Disposes everything the container owns, scene instances first, then clears every cache.</summary>
        public void Dispose()
        {
            if (isDisposed)
                return;

            DisposeSceneEntries(disposeCached: true);
            for (int i = singletons.Count - 1; i >= 0; i--)
                DisposeSafely(singletons[i]);

            singletons.Clear();
            trackedInstances.Clear();
            cacheArray.ClearAllCached();
            cacheArray.ClearSceneSingletons();
            cacheArray.ClearAllSingletons();
            isDisposed = true;
        }

        /// <summary>Forgets every tracked instance without disposing it, and allows tracking again.</summary>
        public void Clear()
        {
            singletons.Clear();
            sceneInstances.Clear();
            trackedInstances.Clear();
            isDisposed = false;
        }

        /// <summary>Number of tracked instances waiting for disposal.</summary>
        public int Count => singletons.Count + sceneInstances.Count;

        private void DisposeSceneEntries(bool disposeCached)
        {
            for (int i = sceneInstances.Count - 1; i >= 0; i--)
            {
                SceneEntry entry = sceneInstances[i];
                if (!disposeCached && entry.Lifetime == Lifetime.Cached)
                    continue;

                sceneInstances.RemoveAt(i);
                DisposeSafely(entry.Instance);
            }
        }

        private void DisposeSafely(IDisposable instance)
        {
            trackedInstances.Remove(instance);
            try
            {
                instance.Dispose();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace FastIoC.Core
{
    public class DisposalCoordinator
    {
        private readonly List<object> disposableInstances = new();
        private readonly HashSet<object> trackedInstances = new(InstanceReferenceComparer.Instance);
        private readonly InstanceCacheArray cacheArray;
        private bool isDisposed;

        public DisposalCoordinator(InstanceCacheArray cacheArray)
        {
            this.cacheArray = cacheArray ?? throw new ArgumentNullException(nameof(cacheArray));
        }

        public void TrackInstance(object instance)
        {
            if (instance == null)
                return;

            if (instance is IDisposableHook && trackedInstances.Add(instance))
                disposableInstances.Add(instance);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            for (int i = disposableInstances.Count - 1; i >= 0; i--)
            {
                if (disposableInstances[i] is IDisposableHook hook)
                {
                    try
                    {
                        hook.OnDispose();
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogException(exception);
                    }
                }
            }

            cacheArray.ClearAllCached();
            cacheArray.ClearAllSingletons();
            disposableInstances.Clear();
            trackedInstances.Clear();
            isDisposed = true;
        }

        public void ClearSceneSingletons()
        {
            if (isDisposed)
                return;

            cacheArray.ClearSceneSingletons();
        }

        public void Clear()
        {
            disposableInstances.Clear();
            trackedInstances.Clear();
            isDisposed = false;
        }

        public int Count => disposableInstances.Count;
    }
}

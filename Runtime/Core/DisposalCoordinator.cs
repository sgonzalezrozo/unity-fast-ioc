using System;
using System.Collections.Generic;

namespace Tools.FastIoC.Runtime.Core
{
    public class DisposalCoordinator
    {
        private readonly List<object> disposableInstances = new();
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

            if (instance is IDisposableHook)
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
                    catch (Exception)
                    {
                    }
                }
            }

            cacheArray.ClearAllCached();
            cacheArray.ClearAllSingletons();
            disposableInstances.Clear();
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
            isDisposed = false;
        }

        public int Count => disposableInstances.Count;
    }
}


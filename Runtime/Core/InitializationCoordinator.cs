using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FastIoC.Core
{
    /// <summary>
    /// Coordinates initialization of IInitializable and IAsyncInitializable instances.
    /// Tracks instances during resolution and initializes them in proper order.
    /// Supports both synchronous and asynchronous initialization with concurrency control.
    /// </summary>
    public class InitializationCoordinator
    {
        private readonly List<object> initializableInstances = new();
        private readonly HashSet<object> trackedInstances = new(InstanceReferenceComparer.Instance);
        private bool isInitialized;
        private bool isAsyncInitialized;

        /// <summary>
        /// Track an instance for later initialization if it implements IInitializable or IAsyncInitializable.
        /// </summary>
        public void TrackInstance(object instance)
        {
            if (instance == null)
                return;

            if (instance is not (IInitializable or IAsyncInitializable))
                return;

            if (trackedInstances.Add(instance))
                initializableInstances.Add(instance);
        }

        /// <summary>
        /// Initialize all tracked IInitializable instances synchronously.
        /// </summary>
        public void InitializeAll()
        {
            if (isInitialized)
                return;

            foreach (object instance in initializableInstances)
            {
                if (instance is IInitializable init)
                {
                    try
                    {
                        init.Initialize();
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogException(exception);
                    }
                }
            }

            isInitialized = true;
        }

        /// <summary>
        /// Initialize all tracked instances asynchronously.
        /// Execution order:
        /// 1. IInitializable.Initialize() (synchronous, all in sequence)
        /// 2. IAsyncInitializable.InitializeAsync() (concurrent, all at once)
        /// </summary>
        public async Task InitializeAllAsync(CancellationToken ct = default)
        {
            if (isInitialized && isAsyncInitialized)
                return;

            try
            {
                foreach (object instance in initializableInstances)
                {
                    ct.ThrowIfCancellationRequested();
                    if (instance is IInitializable init)
                    {
                        try
                        {
                            init.Initialize();
                        }
                        catch (Exception exception)
                        {
                            UnityEngine.Debug.LogException(exception);
                        }
                    }
                }

                isInitialized = true;

                // 3. Async initialization (IAsyncInitializable) - concurrent
                List<Task> asyncTasks = new();
                foreach (object instance in initializableInstances)
                {
                    ct.ThrowIfCancellationRequested();
                    if (instance is IAsyncInitializable asyncInit)
                    {
                        asyncTasks.Add(asyncInit.InitializeAsync(ct));
                    }
                }

                if (asyncTasks.Count > 0)
                    await Task.WhenAll(asyncTasks);

                isAsyncInitialized = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Initialize all tracked instances asynchronously with concurrency control.
        /// Limits number of simultaneous async tasks to avoid resource exhaustion.
        /// </summary>
        public async Task InitializeAllAsync(int maxConcurrency, CancellationToken ct = default)
        {
            if (maxConcurrency < 1)
                throw new ArgumentException("maxConcurrency must be >= 1", nameof(maxConcurrency));

            if (isInitialized && isAsyncInitialized)
                return;

            try
            {
                foreach (object instance in initializableInstances)
                {
                    ct.ThrowIfCancellationRequested();
                    if (instance is IInitializable init)
                    {
                        try
                        {
                            init.Initialize();
                        }
                        catch (Exception exception)
                        {
                            UnityEngine.Debug.LogException(exception);
                        }
                    }
                }

                isInitialized = true;

                // 3. Async initialization with concurrency control
                using (SemaphoreSlim semaphore = new(maxConcurrency))
                {
                    List<Task> asyncTasks = new();

                    foreach (object instance in initializableInstances)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (instance is IAsyncInitializable asyncInit)
                        {
                            asyncTasks.Add(InitializeWithLimit(asyncInit, ct, semaphore));
                        }
                    }

                    if (asyncTasks.Count > 0)
                        await Task.WhenAll(asyncTasks);
                }

                isAsyncInitialized = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task InitializeWithLimit(IAsyncInitializable asyncInit, CancellationToken ct, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await asyncInit.InitializeAsync(ct);
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Clear all tracked instances.
        /// </summary>
        public void Clear()
        {
            initializableInstances.Clear();
            trackedInstances.Clear();
            isInitialized = false;
            isAsyncInitialized = false;
        }

        /// <summary>
        /// Get count of tracked instances.
        /// </summary>
        public int Count => initializableInstances.Count;
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tools.FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Test services for async initialization verification
    /// </summary>

    public interface IAsyncService
    {
        Task InitializeAsync(CancellationToken ct = default);
        bool IsInitialized { get; }
        string InitializationData { get; }
    }

    public class AsyncService : IAsyncService
    {
        private int delayMilliseconds;
        private bool isInitialized;
        private string initializationData;

        public bool IsInitialized => isInitialized;
        public string InitializationData => initializationData;

        public AsyncService() : this(100)
        {
        }

        public AsyncService(int delayMs)
        {
            delayMilliseconds = delayMs;
            isInitialized = false;
            initializationData = string.Empty;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                await Task.Delay(delayMilliseconds, ct);
                initializationData = $"AsyncService initialized at {DateTime.Now:O}";
                isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                isInitialized = false;
                initializationData = "Initialization cancelled";
                throw;
            }
        }
    }

    public interface ILongRunningService
    {
        Task InitializeAsync(CancellationToken ct = default);
        bool IsInitialized { get; }
        int ExecutionTimeMs { get; }
    }

    public class LongRunningService : ILongRunningService
    {
        private int executionTimeMs;
        private bool isInitialized;

        public bool IsInitialized => isInitialized;
        public int ExecutionTimeMs => executionTimeMs;

        public LongRunningService() : this(5000)
        {
        }

        public LongRunningService(int executionMs)
        {
            executionTimeMs = executionMs;
            isInitialized = false;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            try
            {
                await Task.Delay(executionTimeMs, ct);
                isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                isInitialized = false;
                throw;
            }
        }
    }

    public interface IQuickService
    {
        string GetData();
    }

    public class QuickService : IQuickService
    {
        private string data;

        public QuickService()
        {
            data = $"QuickService#{UnityEngine.Random.Range(1000, 9999)}";
        }

        public string GetData() => data;
    }

    public interface ITrackableService
    {
        string ServiceId { get; }
        static int InstanceCount { get; }
    }

    public class TrackableService : ITrackableService
    {
        private static int instanceCount = 0;
        private string serviceId;

        public string ServiceId => serviceId;

        public TrackableService()
        {
            instanceCount++;
            serviceId = $"TrackableService#{instanceCount}";
        }
    }
}


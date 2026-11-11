using System;
using System.Threading;
using System.Threading.Tasks;
using FastIoC.Core;

namespace FastIoC.Samples.PlatformCheck
{
    public interface IPlatformClock
    {
        int Ticks { get; }
    }

    public class PlatformClock : IPlatformClock
    {
        public int Ticks => 7;
    }

    public class PlatformCounter
    {
        private static int created;
        public int Number { get; } = Interlocked.Increment(ref created);
    }

    /// <summary>Constructor injection with a plain, a Lazy and a Func dependency.</summary>
    public class PlatformCheckService
    {
        public IPlatformClock Clock { get; }
        public Lazy<IPlatformClock> LazyClock { get; }
        public Func<PlatformCounter> CreateCounter { get; }

        public PlatformCheckService(IPlatformClock clock, Lazy<IPlatformClock> lazyClock, Func<PlatformCounter> createCounter)
        {
            Clock = clock;
            LazyClock = lazyClock;
            CreateCounter = createCounter;
        }
    }

    /// <summary>Bound NonLazy: created at startup although nothing asks for it.</summary>
    public class PlatformNonLazyProbe
    {
        public static bool Created { get; private set; }

        public PlatformNonLazyProbe() => Created = true;
    }

    /// <summary>A plain value registered with FromInstance(value) when the game starts.</summary>
    public class PlatformValue
    {
        public int Number { get; set; }
    }

    /// <summary>Stands in for another container: counts the exported contracts and keeps one to resolve.</summary>
    public class PlatformCountingSink : IGlobalBindingSink
    {
        public int Count { get; private set; }
        public ExternalBinding? Clock { get; private set; }

        public void Add(Type contract, ExternalBinding binding)
        {
            Count++;
            if (contract == typeof(IPlatformClock))
                Clock = binding;
        }
    }

    /// <summary>Sync and async initialization. Task.Yield keeps it on the main thread, which WebGL needs.</summary>
    public class PlatformInitService : IInitializable, IAsyncInitializable
    {
        public bool Initialized { get; private set; }
        public bool AsyncInitialized { get; private set; }

        public void Initialize() => Initialized = true;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await Task.Yield();
            AsyncInitialized = true;
        }
    }
}

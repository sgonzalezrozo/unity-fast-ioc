using System;

namespace FastIoC.Abstraction
{
    public interface IContextLifecycleObserver
    {
        event Action<string> OnContextCreated;

        event Action<string> OnContextDestroyed;

        void StartObserving();

        void StopObserving();
    }
}


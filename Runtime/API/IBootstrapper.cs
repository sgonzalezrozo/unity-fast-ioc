using FastIoC.Core;

namespace FastIoC.API
{
    public interface IBootstrapper
    {
        void Initialize();
        void Shutdown();
        ContextRuntime GetRuntime();
    }
}


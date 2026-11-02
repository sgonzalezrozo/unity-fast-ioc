using Tools.FastIoC.Runtime.Core;

namespace Tools.FastIoC.Runtime.API
{
    public interface IBootstrapper
    {
        void Initialize();
        void Shutdown();
        ContextRuntime GetRuntime();
    }
}


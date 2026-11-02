using Tools.FastIoC.Runtime.Core;

namespace Tools.FastIoC.Runtime.API.Initialization
{
    public class FrameworkInitializer
    {
        public void Initialize()
        {
            ServiceLocatorEnforcer.Initialize(ServiceLocatorEnforcementLevel.Strict);
            FastIoCReflectionCache.Initialize();
        }
    }
}


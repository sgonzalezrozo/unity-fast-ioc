using FastIoC.Core;

namespace FastIoC.API.Initialization
{
    public class FrameworkInitializer
    {
        public void Initialize()
        {
            ServiceLocatorEnforcer.Initialize(ServiceLocatorEnforcementLevel.Strict);
        }
    }
}

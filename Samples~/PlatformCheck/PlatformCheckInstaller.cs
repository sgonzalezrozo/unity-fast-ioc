using FastIoC.API;

namespace FastIoC.Samples.PlatformCheck
{
    [MonoInstaller]
    public class PlatformCheckInstaller : BindingRecorder
    {
        public override void InstallBindings()
        {
            Bind<IPlatformClock>().To<PlatformClock>().AsSingle();
            Bind<PlatformCounter>().AsTransient();
            Bind<PlatformCheckService>().AsSingle();
            Bind<PlatformInitService>().AsSingle();

            // Features from 0.5 on: NonLazy, a runtime value, and an asset loaded through the generated manifest.
            Bind<PlatformNonLazyProbe>().AsSingle().NonLazy();
            Bind<PlatformValue>().FromInstance(new PlatformValue { Number = 42 });
            Bind<PlatformCheckConfig>().FromInstance("PlatformCheckConfig").AsSingle();
        }
    }
}

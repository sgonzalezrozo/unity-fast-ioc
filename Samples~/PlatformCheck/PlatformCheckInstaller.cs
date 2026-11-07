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
        }
    }
}

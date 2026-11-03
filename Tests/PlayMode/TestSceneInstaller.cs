using FastIoC.API;

namespace FastIoC.Tests.PlayMode
{
    /// <summary>
    /// Installer for integration test scenes.
    /// Sets up the ITestService binding for injection testing.
    /// </summary>
    [MonoInstaller]
    public class TestSceneInstaller : BindingRecorder
    {
        public override void InstallBindings()
        {
            // Basic injection testing
            Bind<ITestService>()
                .To<TestService>()
                .AsSingle();

            // Async initialization testing
            Bind<IAsyncService>()
                .To<AsyncService>()
                .AsTransient(); // New instance each resolve

            Bind<ILongRunningService>()
                .To<LongRunningService>()
                .AsTransient(); // New instance each resolve

            // Scene transition testing
            Bind<IQuickService>()
                .To<QuickService>()
                .AsSingle(); // Persist across scene transitions

            Bind<ITrackableService>()
                .To<TrackableService>()
                .AsCached(); // Per-scene lifecycle
        }
    }
}


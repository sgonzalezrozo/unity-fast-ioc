using FastIoC.API;

[MonoInstaller]
public class QuickStartInstaller : BindingRecorder
{
    public override void InstallBindings()
    {
        Bind<IInventoryService>().To<InventoryService>().AsSingle();
    }
}

# Fast IoC rules for AI coding agents

Copy this file into your project's agent rules (for example `AGENTS.md`, `CLAUDE.md` or `.cursor/rules/fast-ioc.md`).
It tells an agent how this project wires dependencies with Fast IoC.

## How dependencies work here

- This project uses **Fast IoC** for dependency injection. Don't add singletons, static instances, `FindObjectOfType`, `FindAnyObjectByType` or service locators to share services.
- Bindings live in installers: classes that extend `BindingRecorder` and have `[MonoInstaller]` (namespace `FastIoC.API`). Each feature has its own installer next to its code. Don't edit a shared, central installer to add a feature's bindings.
- Fast IoC generates code from the installers after every script compile. The game runs the generated code in `Assets/FastIoC/Generated/`. **Never edit files in that folder by hand.**

## Add a service

```csharp
using FastIoC.API;

[MonoInstaller]                       // global; add scene names to limit it: [MonoInstaller("Shop")]
public class ShopInstaller : BindingRecorder
{
    public override void InstallBindings()
    {
        Bind<IShopService>().To<ShopService>().AsSingle();
    }
}
```

- Lifetimes: `AsSingle()` (one shared instance), `AsCached()` (one instance, cleared when a scene unloads), `AsTransient()` (new instance each time, the default).
- A ScriptableObject or prefab used as a service: `Bind<ShopConfig>().FromInstance("ShopConfig").AsSingle();` In a global installer the asset can be in any folder. In a scene-specific installer it must be in a `Resources` folder or marked as Addressable.
- Bindings are by type only. There are no named bindings, factories with runtime parameters, decorators or open generic bindings. Put settings such as numbers or URLs in a config class and bind that.

## Inject dependencies

Plain C# classes get them through **one public constructor**:

```csharp
public class ShopService : IShopService
{
    private readonly IInventoryService inventory;

    public ShopService(IInventoryService inventory) => this.inventory = inventory;
}
```

MonoBehaviours get them through **`internal` fields**. Not public, not private, no `[Inject]` attribute:

```csharp
public class ShopView : MonoBehaviour
{
    internal IShopService shop;        // correct
    // [Inject] private IShopService shop;   wrong: not injected
    // public IShopService shop;             wrong: not injected

    private void Start() => shop.Open();
}
```

Use injected fields from `Start()` or later.

## Create objects at runtime

- Prefabs that need injection: inject `FastIoC.Core.IInstantiator` and call `instantiator.Instantiate<T>(prefab, parent)`. `T` must implement `IInstanciable` or `IInjectableForChildren<TData>`.
- Pass data: implement `IInjectableForChildren<TData>` and call `Instantiate<T>(prefab, parent, data: value)`. `Data` is set before `Awake()`.
- An object created with `Object.Instantiate` or `AddComponent`: call `SceneInjector.InjectIfNeeded(component)` (namespace `FastIoC.API`).
- Don't resolve services from `ContextRuntime` in game code. Fast IoC treats it as a service locator and throws in the Editor and development builds.

## Create dependencies later

Use `Lazy<T>` (resolved the first time `.Value` is read) or `Func<T>` (resolved on every call) as a constructor parameter or `internal` field when `T` shouldn't be created with the class. `T` must still be bound. Don't read `.Value` inside a constructor. `Func<T>` takes no arguments.

## Lifecycle

Services can implement `IInitializable`, `IAsyncInitializable` or `IDisposableHook` (namespace `FastIoC.Core`). Async initialization doesn't block the first scene, so check that a service is ready before using it.

## Check your work

After changing scripts, wait for Unity to compile, then read `Assets/FastIoC/Generated/GenerationStatus.json`:

- `"status": 0` means generation succeeded.
- `"status": 1` means it failed. The file lists the errors, such as a missing binding (`X requires Y, which isn't bound in any installer`) or a dependency cycle. Fix them in the installers or constructors, not in the generated code.
- `warnings` lists `internal` MonoBehaviour fields whose interface or abstract type has no binding. They stay `null` at runtime, so bind the type unless the code assigns the field itself.

To regenerate without a compile, run **Tools > FastIoC > Regenerate Code**, or in batch mode:

```bash
unity -batchmode -quit -projectPath . \
  -executeMethod FastIoC.Editor.Generation.GenerationCoordinator.GenerateAll
```

Unbound fields of concrete classes aren't reported, so bind every type you inject.

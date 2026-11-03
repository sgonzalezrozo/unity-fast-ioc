# Fast IoC

**Code-generated dependency injection for Unity, with installer-based bindings.**

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Fast IoC generates registries, activators and injectors at edit time. FastIoC avoids runtime reflection for dependency graph discovery and resolution, using generated IDs and activators, but still uses limited runtime reflection for generated injector binding and prefab injection fallback paths.

> **TODO:** Add a short GIF or screenshot of Setup, regeneration and a running scene.

## Why I built it

I've spent 17 years building free-to-play Unity games, most of them mobile online multiplayer. On those projects, startup time is something players notice.

I wanted the structure of a dependency injection container, with its dependency graph validated before the game runs and less reflection at startup.

> **TODO:** Replace this with your own story: which project or problem made you start Fast IoC, and what existing containers did not solve.

## Is it right for your project?

**Use Fast IoC if you want:**

- Feature-owned installers instead of one long project installer
- Bindings validated in the Editor, before you press Play
- Generated IDs and activators for dependency resolution
- Global and scene-specific installers
- Project bootstrap that runs before the first scene loads
- Warnings or errors when code uses the container as a service locator

**Consider something else if you:**

- Don't want generated code in your project
- Need a container with a long public track record and a large community. Zenject/Extenject, VContainer and Reflex are established alternatives.

## Features

- Installer-based binding with `BindingRecorder` and `Bind<T>()`
- Auto-discovered installers, with no central list to maintain
- Registries, activators and injectors generated at edit time
- Automatic regeneration when installers change
- Cycle and missing-binding validation during generation
- Constructor injection for plain classes
- `internal` field injection for MonoBehaviours
- `AsSingle`, `AsCached`, `AsTransient` and `FromInstance` bindings
- Global and scene-specific installers with priorities
- Sync and async initialization, plus disposal hooks
- Prefab instantiation with injection and data through `IInstantiator`
- Service-locator detection in the Editor and development builds

## Compatibility

| Requirement | Supported |
|---|---|
| Unity | 2022.3 or newer. **TODO:** add tested versions. |
| Scripting backend | **TODO:** confirm Mono and IL2CPP |
| Platforms | **TODO:** list tested platforms |
| Managed code stripping | **TODO:** add tested levels and any required `link.xml` |
| Enter Play Mode with domain reload disabled | **TODO:** confirm support |
| Dependencies | [Tools Core](https://github.com/sgonzalezrozo/unity-tools-core), Addressables |

## Installation

Git must be installed on your machine.

### 1. Install Tools Core

Unity doesn't install Git dependencies automatically, so add [Tools Core](https://github.com/sgonzalezrozo/unity-tools-core) first.

1. In Unity, open **Window > Package Manager**.
2. Select **+ > Add package from git URL**.
3. Enter:

```text
https://github.com/sgonzalezrozo/unity-tools-core.git
```

### 2. Install this package

1. In Unity, open **Window > Package Manager**.
2. Select **+ > Add package from git URL**.
3. Enter:

```text
https://github.com/sgonzalezrozo/unity-fast-ioc.git
```

To pin a release, add its tag:

```text
https://github.com/sgonzalezrozo/unity-fast-ioc.git#v0.2.0
```

Fast IoC adds the `FAST_IoC` scripting define for all build targets when it loads. This changes your project's Player Settings, so commit that change.

## Setup

1. Run **Tools > FastIoC > Setup**.
2. Confirm that `Assets/Resources/Tools/FastIoC/ProjectContext.prefab` was created.
3. Add your first installer, as shown in [Quick start](#quick-start). Fast IoC then creates `Assets/FastIoC/Generated/`.
4. Commit the prefab and generated folder. The generated code is needed in player builds.

## Quick start

### 1. Define a service

```csharp
public interface IInventoryService
{
    int ItemCount { get; }
}

public class InventoryService : IInventoryService
{
    public int ItemCount => 3;
}
```

### 2. Bind it

```csharp
using FastIoC.API;

[MonoInstaller]
public class GameplayInstaller : BindingRecorder
{
    public override void InstallBindings()
    {
        Bind<IInventoryService>().To<InventoryService>().AsSingle();
    }
}
```

### 3. Inject it

Plain classes receive dependencies through their public constructor:

```csharp
public class ShopService
{
    private readonly IInventoryService inventory;

    public ShopService(IInventoryService inventory)
    {
        this.inventory = inventory;
    }
}
```

MonoBehaviours receive them through `internal` fields:

```csharp
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    internal IInventoryService inventory;

    private void Start()
    {
        Debug.Log($"Items: {inventory.ItemCount}");
    }
}
```

The field must be `internal`. Public and private fields aren't injected. Give each service one public constructor.

### 4. Press Play

Fast IoC regenerates automatically when installers change. If you've just changed bindings, you can also run **Tools > FastIoC > Regenerate Code**. Add `InventoryView` to a scene and press Play to see `Items: 3` in the Console.

## Organize bindings by feature

Project installers tend to grow into one long file that every feature has to edit. With Fast IoC, each feature can own its bindings in its own installer:

```csharp
using FastIoC.API;

[MonoInstaller]
public class InventoryInstaller : BindingRecorder
{
    public override void InstallBindings()
    {
        Bind<IInventoryService>().To<InventoryService>().AsSingle();
    }
}

[MonoInstaller]
public class ShopInstaller : BindingRecorder
{
    public override void InstallBindings()
    {
        Bind<IShopService>().To<ShopService>().AsSingle();
    }
}
```

Fast IoC discovers classes with `[MonoInstaller]` automatically, so there's no central list or scene reference to update.

- **Adding a feature:** add its installer, without editing a shared file.
- **Removing a feature:** delete its installer along with its code.
- **Working in a team:** features change separate files, which reduces merge conflicts.
- **Finding bindings:** each feature's bindings live next to its code.

## Core concepts

| Concept | Meaning |
|---|---|
| Installer | A `BindingRecorder` class with `[MonoInstaller]` that declares bindings in `InstallBindings()` |
| Binding | A rule that tells the container what to provide for a type |
| Project context | The prefab that starts the container before the first scene loads |
| Generated code | The registries, activators and injectors in `Assets/FastIoC/Generated/` |

## Binding reference

```csharp
public override void InstallBindings()
{
    Bind<IMusicService>().To<MusicService>().AsSingle();
    Bind<IEnemyFactory>().To<EnemyFactory>().AsCached();
    Bind<IDamageCalculator>().To<DamageCalculator>().AsTransient();
    Bind<PlayerSessionModel>().AsSingle();
    Bind<GameConfig>().FromInstance("Config/GameConfig");
}
```

| Binding | Behavior |
|---|---|
| `AsSingle()` | One shared instance, created on first resolve |
| `AsCached()` | One cached instance, cleared when a scene unloads. |
| `AsTransient()` | A new instance for each resolve. This is the default lifetime. |
| `FromInstance(value)` | A singleton using the given instance |
| `FromInstance(assetPath)` | A singleton loaded from an asset path |

> **TODO:** Confirm when `FromInstance(value)` should be used, given that bindings are read at edit time.

## Scene-specific installers

```csharp
using FastIoC.API;

[MonoInstaller("MainMenu", "Shop", Priority = 200)]
public class MenuInstaller : BindingRecorder
{
    public override void InstallBindings()
    {
        Bind<UiSessionModel>().AsSingle();
    }
}
```

- The strings are scene names.
- `[MonoInstaller]` with no scene names is global.
- Installers with a higher `Priority` run first. The default is `0`.
- Set `Disabled = true` on the attribute to exclude an installer.

## Instantiating prefabs

Inject `IInstantiator` to create prefabs with dependencies:

```csharp
using FastIoC.Core;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;

    internal IInstantiator instantiator;

    public EnemyView Spawn()
    {
        return instantiator.Instantiate<EnemyView>(enemyPrefab, transform);
    }
}
```

The component type must implement `IInstanciable`, or a child interface such as `IInjectableForChildren`.

## Lifecycle

```csharp
using FastIoC.Core;

public class SaveSystem : IInitializable, IDisposableHook
{
    public void Initialize() { }
    public void OnDispose() { }
}
```

Services can also implement `IAsyncInitializable`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using FastIoC.Core;

public class RemoteConfigService : IAsyncInitializable
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await Task.Delay(100, ct);
    }
}
```

- Async initialization doesn't block the first scene, so check that a service is ready before using it.

## Editor commands

| Command | Use it when |
|---|---|
| `Tools/FastIoC/Setup` | Setting up a project for the first time |
| `Tools/FastIoC/Regenerate Code` | Regenerating manually |

## Runtime reflection

FastIoC avoids runtime reflection for dependency graph discovery and resolution, using generated IDs and activators. It still uses limited runtime reflection in these places:

- Finding the generated code at startup
- Setting `internal` MonoBehaviour fields from generated injectors, with `FieldInfo` lookups cached
- Instantiating `IInstantiable` prefabs through `IInstantiator`
- Prefab injection fallback paths

Test IL2CPP builds with code stripping enabled before shipping.

## Performance

| Test | Fast IoC | Zenject, default reflection mode |
|---|---:|---:|
| Startup with 1,000 bindings and 500+ objects | ~200 ms | ~1.2 s |

> **TODO:** Add the device, Unity version, scripting backend, build type, Zenject version and a link to the benchmark project.

## Tests

Add this to your project's `Packages/manifest.json` to run the package's Play Mode tests:

```json
"testables": ["com.sgonzalezrozo.fast-ioc"]
```

## Troubleshooting

| Problem | Try this |
|---|---|
| Play Mode stops with `CODE GENERATION FAILED` | Fix the listed errors, then run **Regenerate Code** |
| `TypeId ... is not registered` | Add a binding, and check that its scene installer applies to the active scene |
| A MonoBehaviour field stays `null` | Make the field `internal`, bind its type, and use it from `Start()` |
| `Service Locator Anti-Pattern Detected` | Inject the dependency instead of resolving it from `ContextRuntime`. The default level is strict, so this throws in the Editor and development builds. |

## Limitations

- MonoBehaviour fields must be `internal`.
- Installers run in the Editor during generation, so don't rely on runtime-only state in `InstallBindings()`.
- See [Runtime reflection](#runtime-reflection) for where reflection is still used.

## Project status

This is a pre-release package at version `0.2.0`. Its API and behavior may change before `1.0.0`.

> **TODO:** Add a roadmap link and breaking-change policy.

## Contributing

Bug reports and pull requests are welcome. Please open an issue before starting a large change.

## Related packages

| Package | Purpose |
|---|---|
| [Screen System](https://github.com/sgonzalezrozo/unity-screen-system) | Declarative UI navigation |
| [UI Tweens](https://github.com/sgonzalezrozo/unity-ui-tweens) | ECS-based UI animation |
| [Recyclable Scroller](https://github.com/sgonzalezrozo/unity-recyclable-scroller) | Virtualized lists |
| [Enhanced Console](https://github.com/sgonzalezrozo/unity-enhanced-console) | Unity Editor log triage |
| [Tools Core](https://github.com/sgonzalezrozo/unity-tools-core) | Shared injection interfaces |

## License

Released under the [MIT License](LICENSE).

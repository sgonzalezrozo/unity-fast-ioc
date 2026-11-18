# Injection & Lifecycle

[← Back to README](../README.md)

- [Constructors and fields](#constructors-and-fields)
- [Instantiating prefabs](#instantiating-prefabs)
- [Create dependencies later](#create-dependencies-later)
- [Lifecycle](#lifecycle)

---

## Constructors and fields

- **Plain classes** receive dependencies through their one public constructor.
- **MonoBehaviours** receive them through `internal` fields. There's no `[Inject]` attribute; public and private fields aren't injected. Use them from `Start()`.

```csharp
public class InventoryView : MonoBehaviour
{
    internal IInventoryService inventory;
}
```

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

### Pass data to a prefab

Implement `IInjectableForChildren<T>` and pass the data to `Instantiate`. Fast IoC sets `Data` before it fills the container fields, so it's ready in `Awake()` once the instance activates:

```csharp
using FastIoC.Core;
using UnityEngine;

public class EnemyView : MonoBehaviour, IInjectableForChildren<EnemyData>
{
    public EnemyData Data { get; set; }
}

// In the spawner:
EnemyView boss = instantiator.Instantiate<EnemyView>(enemyPrefab, transform, data: bossData);
```

### Reference other components of the prefab

An `internal` field whose type is a MonoBehaviour is filled with a component of that type from the same instantiated prefab, if the field is still empty:

```csharp
public class EnemyController : MonoBehaviour
{
    internal EnemyModel model; // the EnemyModel on this prefab
}
```

Fields you assign in the Inspector are left as they are.

### Objects created outside Fast IoC

Scenes loaded at any time are injected when they load. For an object you create with `Object.Instantiate` or `AddComponent`, call `SceneInjector.InjectIfNeeded(component)`.

## Create dependencies later

Ask for `Lazy<T>` or `Func<T>` instead of `T` when a dependency shouldn't be created with the class, or when you need a new instance on demand:

```csharp
using System;

public class SaveService
{
    private readonly Lazy<ICloudSync> cloud;
    private readonly Func<EnemyAI> createEnemyAI;

    public SaveService(Lazy<ICloudSync> cloud, Func<EnemyAI> createEnemyAI)
    {
        this.cloud = cloud;
        this.createEnemyAI = createEnemyAI;
    }

    public void Upload() => cloud.Value.Push();      // ICloudSync is created here, the first time
    public EnemyAI Spawn() => createEnemyAI();       // resolves EnemyAI on every call
}
```

- **`Lazy<T>`** resolves `T` the first time you read `.Value`, then keeps it.
- **`Func<T>`** resolves `T` on every call, so `T`'s lifetime decides what you get: a new instance for `AsTransient()`, the same one for `AsSingle()`.
- **Both work in constructors and in `internal` MonoBehaviour fields**, such as `internal Lazy<IShopService> shop;`.
- **`T` must be bound.** Generation fails for a constructor parameter and warns for a field, as it does for `T` itself.
- **They don't count as cycles.** Nothing is created in a loop at startup, so `A(Lazy<B>)` with `B(A)` is allowed. Don't read `.Value` in the constructor, or the loop comes back.
- **They resolve when used.** A field whose `T` is bound only in another scene throws when it's used, instead of staying `null`.
- `Func<T>` takes no arguments. Factories with runtime parameters aren't supported yet.

## Lifecycle

```csharp
using System;
using FastIoC.Core;

public class SaveSystem : IInitializable, IDisposable
{
    public void Initialize() { }
    public void Dispose() { }
}
```

Fast IoC disposes the `IDisposable` instances it creates, in reverse creation order:

| Lifetime | Disposed when |
|---|---|
| `AsSingle()` in a global installer | The `ProjectContext` is destroyed: the app quits, or Play Mode ends |
| `AsSingle()` in a scene installer, and `AsCached()` | A scene unloads |
| `AsTransient()` | Never: the class that received it owns it |

Instances Fast IoC didn't create are never disposed: `FromInstance(...)`, `FromProjectContext()` and any Unity object. If one `Dispose` throws, the exception is logged and the other instances are still disposed.

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
- Async initialization runs on the main thread. On WebGL, which has no background threads, don't use `Task.Run` or wait on a task in `InitializeAsync`.

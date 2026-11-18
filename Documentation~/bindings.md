# Bindings

[← Back to README](../README.md)

- [Binding reference](#binding-reference)
- [Scene-specific installers](#scene-specific-installers)
- [Bind assets](#bind-assets)
- [Bind existing objects](#bind-existing-objects)
- [Create services at startup](#create-services-at-startup)
- [Host components on the ProjectContext prefab](#host-components-on-the-projectcontext-prefab)

---

## Binding reference

```csharp
public override void InstallBindings()
{
    Bind<IMusicService>().To<MusicService>().AsSingle();
    Bind<IEnemyFactory>().To<EnemyFactory>().AsCached();
    Bind<IDamageCalculator>().To<DamageCalculator>().AsTransient();
    Bind<PlayerSessionModel>().AsSingle();
    Bind<GameConfig>().FromInstance("GameConfig").AsSingle();
    Bind<AnalyticsService>().AsSingle().NonLazy();
    Bind<AudioPlayer>().FromProjectContext();
}
```

| Binding | Behavior |
|---|---|
| `AsSingle()` | One shared instance, created on first resolve |
| `AsCached()` | One cached instance, cleared when a scene unloads. Use this to tie an instance to a scene. |
| `AsTransient()` | A new instance for each resolve. This is the default lifetime. |
| `FromInstance(value)` | A singleton using an object you create. See [Bind existing objects](#bind-existing-objects). |
| `FromInstance(assetPath).AsSingle()` | A singleton loaded from a prefab or ScriptableObject. See [Bind assets](#bind-assets). |
| `AsSingle().NonLazy()` | A singleton created at startup instead of on first resolve. See [Create services at startup](#create-services-at-startup). |
| `FromProjectContext()` | A component that already lives on the `ProjectContext` prefab. See [Host components on the ProjectContext prefab](#host-components-on-the-projectcontext-prefab). |

A scene-specific `AsSingle()` instance stays cached after its scene unloads, so use `AsCached()` when the instance shouldn't outlive the scene.

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

## Bind assets

Use `FromInstance` for a prefab or ScriptableObject that the game needs as a single shared service:

```csharp
using FastIoC.API;

namespace MyGame.Shop
{
    [MonoInstaller("MainMenu", "Shop", Priority = 200)]
    public class ShopInstaller : BindingRecorder
    {
        public override void InstallBindings()
        {
            Bind<ShopModel>().AsSingle();
            Bind<ShopConfig>().FromInstance("ShopConfig").AsSingle();
        }
    }
}
```

- **Finish with `AsSingle()`.** `FromInstance(assetPath)` doesn't record the binding by itself.
- **Pass the asset's name or its full path.** `"ShopConfig"` searches the project for an asset with exactly that file name, skipping scripts such as `ShopConfig.cs`. If two assets have that name, generation fails and lists them: pass the full path. A value containing `/` must be the full path, such as `Assets/Resources/Config/ShopConfig.asset`.
- **Global installers accept assets in any folder.** An asset in a `Resources` folder is loaded from there, and one marked as Addressable through Addressables. Any other asset is referenced from a generated manifest, `Assets/Resources/Tools/FastIoC/AssetManifest.asset`, so you don't have to move it. Commit the manifest and its `.meta`.
- **Scene-specific installers need `Resources` or Addressables.** Generation fails for an asset that's in neither.
- **Prefabs must have the bound type on their root.** Fast IoC instantiates the prefab at startup.
- **ScriptableObjects must be assignable to the bound type.**

Other prefabs don't belong here. Keep them in Addressables and load them when needed. Screens, for example, are better loaded when they open.

## Bind existing objects

Use `FromInstance(value)` for a plain object that needs specific values, or a type the container can't construct:

```csharp
Bind<IGameClock>().FromInstance(new FixedClock(startHour: 8));
```

A value can't be saved at edit time, so Fast IoC runs this installer once more when the game starts, and registers the object it gets:

- **Build the value from code that works in the game and in the Editor.** The installer runs in both.
- **Don't create scene objects in the installer.** It also runs during every generation.
- **Installers with this binding need a public parameterless constructor.**
- **The value is shared across all scenes.** It's registered at startup, even if the installer lists scene names.

## Create services at startup

Services are created on first resolve. Add `.NonLazy()` to a global `AsSingle()` binding to create it when the game starts, for example a service that only listens to events and is never injected:

```csharp
Bind<AnalyticsService>().AsSingle().NonLazy();
Bind<IPushNotifications>().To<PushNotifications>().AsSingle().NonLazy();
```

- **They're created after the first scene is injected and before `Initialize` runs.**
- **The order is fixed.** Installers with a higher `Priority` go first, then bindings in the order they're declared.
- **One failure doesn't stop the rest.** If a service throws while it's created, the error is logged with its type and the next service is created.
- **Global installers only.** `NonLazy()` in a scene-specific installer fails generation.
- **A class bound to several contracts is created once.**

## Host components on the ProjectContext prefab

Some services are MonoBehaviours that need values set in the Inspector, such as a sound manager or a coroutine runner. Put them on the `ProjectContext` prefab and bind them with `FromProjectContext()`:

```csharp
Bind<ICoroutineRunner>().To<CoroutineRunner>().FromProjectContext();
Bind<SoundManager>().FromProjectContext().NonLazy();
```

**Setup** gives the `ProjectContext` prefab an inactive child named `Hosted` and assigns it to the **Hosted Root** field. Add each component under it, in `Assets/Resources/Tools/FastIoC/ProjectContext.prefab`. Keep `Hosted` inactive in the prefab.

In a project set up with an older version, run **Setup** again and choose **Regenerate**, or add an inactive child by hand and assign it to **Hosted Root**. Setup leaves a Hosted Root you assigned alone.

At startup Fast IoC injects the `internal` fields of every MonoBehaviour under the hosted root, creates the `NonLazy()` services, and then activates the hosted root. Every hosted component has its dependencies by the time its `Awake` runs.

Generation fails if:

- the binding is in a scene-specific installer;
- the prefab is missing, or **Hosted Root** is empty or active;
- there isn't exactly one component of the bound type under **Hosted Root**.

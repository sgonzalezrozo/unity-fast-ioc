# FAQ

[← Back to README](../README.md)

**About the project**
- [Is it right for my project?](#is-it-right-for-my-project)
- [Is it production-ready? Does anyone use it?](#is-it-production-ready-does-anyone-use-it)
- [Why was it built?](#why-was-it-built)
- [Is it free for commercial use?](#is-it-free-for-commercial-use)

**Choosing a container**
- [How do I do dependency injection in Unity?](#how-do-i-do-dependency-injection-in-unity)
- [Fast IoC vs Zenject (Extenject) vs VContainer: which should I use?](#fast-ioc-vs-zenject-extenject-vs-vcontainer-which-should-i-use)
- [What is the best dependency injection framework for Unity?](#what-is-the-best-dependency-injection-framework-for-unity)
- [Can it run alongside another container?](#can-it-run-alongside-another-container)

**Compatibility**
- [Does it work with IL2CPP and mobile?](#does-it-work-with-il2cpp-and-mobile)
- [Does it work with WebGL?](#does-it-work-with-webgl)
- [Does it work with Unity 6?](#does-it-work-with-unity-6)
- [Does it use reflection?](#does-it-use-reflection)
- [Does it work with AI coding agents?](#does-it-work-with-ai-coding-agents)

**Usage**
- [How do I inject into a MonoBehaviour?](#how-do-i-inject-into-a-monobehaviour)
- [Can I inject ScriptableObjects?](#can-i-inject-scriptableobjects)
- [How do I spawn prefabs with dependencies?](#how-do-i-spawn-prefabs-with-dependencies)
- [How do I make Unity code testable?](#how-do-i-make-unity-code-testable)
- [How much does it cost at startup?](#how-much-does-it-cost-at-startup)

---

## About the project

### Is it right for my project?

**Use Fast IoC if you want:**

- Feature-owned installers instead of one long project installer
- Dependency cycles, missing bindings and invalid assets caught in the Editor, before you press Play
- Generated IDs and activators for dependency resolution
- Global and scene-specific installers
- Project bootstrap that runs before the first scene loads
- Warnings or errors when code uses the container as a service locator

**Consider something else if you:**

- Don't want generated code in your project
- Need a container with a long public track record and a large community. Zenject/Extenject, VContainer and Reflex are established alternatives.

### Is it production-ready? Does anyone use it?

Not yet. It's a pre-release package (`0.x`) by a single author. It isn't used in a shipped game, and it has no other users yet, so expect API changes before `1.0.0`. Bug reports are very welcome. See [Project Status](project-status.md).

### Why was it built?

I decided to build my own IoC framework to understand how injection really works, to control how it happens and, most of all, to improve performance.

Game projects usually start out smooth. As features pile up, performance degrades fast, but the whole team is busy building those features, so optimization tends to wait until the end. By then, optimizing injection, at least in Zenject, is painful.

Something else came up along the way. Since every declaration in Fast IoC happens at edit time, why should a binding be declared in more than one place? With Zenject, if I wanted to control a binding's lifetime without keeping the reference global, I had to repeat it in each scene context that needed it. In Fast IoC, one installer declares the binding and lists its scenes.

I built and refined it on my own time in personal projects. In November 2026 I extracted it into this standalone package and documented it. Earlier history lives in a private repository.

### Is it free for commercial use?

Yes. It's MIT licensed.

## Choosing a container

### How do I do dependency injection in Unity?

Bind each service in an installer, then receive it through a constructor (plain classes) or an `internal` field (MonoBehaviours). Fast IoC generates the code that connects them. See [Getting Started](getting-started.md#quick-start).

### Fast IoC vs Zenject (Extenject) vs VContainer: which should I use?

All three are dependency injection containers for Unity. Fast IoC generates its injection code in the Editor, so it uses no reflection at runtime, catches missing bindings and cycles before Play Mode, and finds feature installers on its own. Zenject and VContainer have more binding features and a long track record, and they don't add generated files to your project. See [How it compares](core-concepts.md#how-it-compares) and [Limitations](troubleshooting.md#limitations).

### What is the best dependency injection framework for Unity?

It depends on what you value. Zenject (Extenject) and VContainer are the established choices, with large communities. Fast IoC is a new option for teams that want no runtime reflection, edit-time errors and installers owned by each feature. See [How it compares](core-concepts.md#how-it-compares).

### Can it run alongside another container?

Yes. Set `FastIoCUnityBootstrapper.AutoStart` to `false` from a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method, then call `FastIoCUnityBootstrapper.Start()` when you want it. The benchmark project uses this to run several containers in one build.

To let the other container use Fast IoC's services, implement `IGlobalBindingSink` and pass it to `ProjectContext.ExportGlobalBindings`. The generated code calls `Add(contract, binding)` once for each global contract, and `binding.Resolve()` returns the instance from Fast IoC. Scene-specific bindings aren't exported. The bridge is one way: Fast IoC can't resolve from the other container, so every dependency of a Fast IoC service must be bound in Fast IoC.

```csharp
using System;
using FastIoC.Core;

public class ExternalContainerBridge : IGlobalBindingSink
{
    public void Add(Type contract, ExternalBinding binding)
    {
        // Register `contract` in the other container so it calls binding.Resolve().
    }
}
```

Call it once, after the other container has registered its own bindings, so the sink can reject a contract that both containers bind.

## Compatibility

### Does it work with IL2CPP and mobile?

Yes. It's verified in IL2CPP Release builds with `Minimal` code stripping on Android (a mid-range and a low-end phone) and iOS (an iPhone 13 mini and an iPhone Air). See [Performance](performance.md).

### Does it work with WebGL?

Yes. It's verified in a WebGL release build with `High` code stripping, using the **Platform Check** sample: constructor and field injection, singletons, `Lazy<T>` and `Func<T>`, prefab instantiation, and sync and async initialization all pass. WebGL runs everything on one thread, and so does Fast IoC, but keep your own `IAsyncInitializable` code off background threads: don't use `Task.Run` or block on a task. Desktop and consoles aren't verified yet. The same sample checks them in one build.

### Does it work with Unity 6?

Yes. It's tested on Unity `6000.3`. The package declares `2022.3` as its minimum, but older versions aren't tested yet.

### Does it use reflection?

Not at runtime. Registries, activators and injectors are generated at edit time, and scenes are injected from a baked manifest. See [Runtime reflection](core-concepts.md#runtime-reflection).

### Does it work with AI coding agents?

Yes, through a rules file you copy into your project. See [AI Coding Agents](ai-agents.md).

## Usage

### How do I inject into a MonoBehaviour?

Declare the dependency as an `internal` field. It's assigned before `Awake()`. There's no `[Inject]` attribute:

```csharp
public class InventoryView : MonoBehaviour
{
    internal IInventoryService inventory;
}
```

### Can I inject ScriptableObjects?

Yes. Bind the asset with `FromInstance` and finish with `AsSingle()`. See [Bind assets](bindings.md#bind-assets).

```csharp
Bind<GameConfig>().FromInstance("GameConfig").AsSingle();
```

### How do I spawn prefabs with dependencies?

Inject `IInstantiator` and call `Instantiate<T>`. You can also pass data that's ready in `Awake()`. See [Instantiating prefabs](injection.md#instantiating-prefabs).

### How do I make Unity code testable?

Move logic out of MonoBehaviours into plain classes that receive their dependencies through the constructor. A unit test creates the class with `new` and passes fakes, with no container or scene. Fast IoC doesn't include test helpers yet.

```csharp
var shop = new ShopService(new FakeInventory(itemCount: 0));
Assert.IsFalse(shop.CanBuy());
```

### How much does it cost at startup?

With 3,000 bindings and 1,500 injected MonoBehaviours, building the container and injecting the first scene takes about 11 ms on an iPhone Air, 15 ms on an iPhone 13 mini, 39 ms on a Pixel 5 and 124 ms on a Galaxy A02s. See [Performance](performance.md).

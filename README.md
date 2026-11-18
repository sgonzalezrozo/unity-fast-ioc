<div align="center">

# Unity Fast IoC

<sub>DESIGNED & BUILT BY</sub><br>
**✦ [`SANTIAGO GONZÁLEZ ROZO (@sgonzalezrozo)`](https://github.com/sgonzalezrozo) ✦**

> Dependency injection for Unity with generated code and no runtime reflection. Missing bindings and dependency cycles are reported before you press Play, and each feature owns its installer. With 3,000 bindings and 1,500 injected objects: 11–39 ms to build the container and inject the first scene on three of four phones (IL2CPP).

[![Unity](https://img.shields.io/badge/Unity-6000.3%20tested-black?style=for-the-badge&logo=unity)](Documentation~/getting-started.md#requirements)
[![Version](https://img.shields.io/badge/version-0.9.4-blue?style=for-the-badge)](CHANGELOG.md)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

[**Installation**](#installation) • [**Key Features**](#key-features) • [**Quickstart**](#quickstart) • [**Bindings**](Documentation~/bindings.md) • [**Core Concepts**](Documentation~/core-concepts.md) • [**Code Generation**](Documentation~/code-generation.md)

<sub>[Getting Started](Documentation~/getting-started.md) • [Injection & Lifecycle](Documentation~/injection.md) • [Performance](Documentation~/performance.md) • [AI Agents](Documentation~/ai-agents.md) • [Troubleshooting](Documentation~/troubleshooting.md) • [FAQ](Documentation~/faq.md) • [Project Status](Documentation~/project-status.md)</sub>

</div>

---

## Installation

Fast IoC depends on [Tools Core](https://github.com/sgonzalezrozo/unity-tools-core). Unity doesn't install Git dependencies automatically, so add both, in this order:

1. Open **Window > Package Manager** in Unity.
2. Click **`+`** in the top-left corner and select **Add package from git URL...**
3. Paste each URL below and click **Add**:

```text
https://github.com/sgonzalezrozo/unity-tools-core.git
```

```text
https://github.com/sgonzalezrozo/unity-fast-ioc.git
```

Fast IoC adds the `FAST_IoC` scripting define for all build targets when it loads; commit that Player Settings change. Requirements and tested platforms: [Getting Started](Documentation~/getting-started.md#requirements).

---

## Key Features

* **[Feature-owned installers](Documentation~/getting-started.md#organize-bindings-by-feature)**: add an installer next to a feature's code and Fast IoC finds it.
  There's no central list to edit, so features stay self-contained and teams hit fewer merge conflicts.
* **[Errors before Play Mode](Documentation~/code-generation.md#when-generation-fails)**: dependency cycles, missing bindings and invalid assets fail generation in the Editor.
  Unbound MonoBehaviour fields are reported as warnings.
* **[No runtime reflection](Documentation~/core-concepts.md#runtime-reflection)**: registries, activators and injectors are generated at edit time.
  Scenes are injected from a baked manifest, and code stripping works with no `link.xml`.
* **[Measured on real devices](Documentation~/performance.md)**: verified in IL2CPP Release builds on Android, iOS and WebGL.
  Spawning 50 injected prefabs stays well inside a 30 fps frame, even on a low-end phone.
* **[Everything a game needs](Documentation~/bindings.md)**: singletons, scene-scoped and transient lifetimes, assets, `Lazy<T>` and `Func<T>`, startup services, prefab instantiation with data, and async initialization.

---

## Quickstart

1. Run **Tools > FastIoC > Setup**.
2. Bind a service in an installer anywhere in your project:

```csharp
[MonoInstaller]
public class GameplayInstaller : BindingRecorder
{
    public override void InstallBindings() => Bind<IInventoryService>().To<InventoryService>().AsSingle();
}
```

3. Receive it through an `internal` field (MonoBehaviours) or the constructor (plain classes), save, and press Play.

<details>
<summary><strong>View full sample script</strong></summary>

```csharp
using FastIoC.API;
using UnityEngine;

// 1. A service
public interface IInventoryService
{
    int ItemCount { get; }
}

public class InventoryService : IInventoryService
{
    public int ItemCount => 3;
}

// 2. Its binding. Fast IoC finds [MonoInstaller] classes on its own.
[MonoInstaller]
public class GameplayInstaller : BindingRecorder
{
    public override void InstallBindings()
    {
        Bind<IInventoryService>().To<InventoryService>().AsSingle();
    }
}

// 3a. A plain class receives it through its one public constructor.
public class ShopService
{
    private readonly IInventoryService inventory;

    public ShopService(IInventoryService inventory)
    {
        this.inventory = inventory;
    }
}

// 3b. A MonoBehaviour receives it through an internal field. Add it to a scene and press Play.
public class InventoryView : MonoBehaviour
{
    internal IInventoryService inventory;

    private void Start()
    {
        Debug.Log($"Items: {inventory.ItemCount}"); // Items: 3
    }
}
```

</details>

The full walkthrough is in [Getting Started](Documentation~/getting-started.md).

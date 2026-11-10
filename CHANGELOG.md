# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Documentation

- The README says CI isn't set up yet, and the Tests workflow runs only by hand.

## [0.5.0] - 2026-11-10

### Added

- `AsSingle().NonLazy()`: marks a global Singleton binding as eager. `ProjectContext` resolves all NonLazy bindings after scene injection and before `IInitializable.Initialize` runs. Creation order follows installer Priority (descending) then declaration order; if a type fails to create the error is logged and the remaining types are still created. NonLazy on a scene-specific installer is a generation-time error with a clear message.
- `IGeneratedModule.CreateNonLazyInstances(ContextRuntime)`: new method implemented by the generated `GeneratedModule`.
- `BindingInfo.IsNonLazy` and `BindingInfo.DeclarationOrder`: carry NonLazy state through the generation pipeline.
- `FromProjectContext()`: binds a MonoBehaviour component that already lives on `ProjectContext.HostedRoot`. FastIoC finds it with `GetComponentInChildren<TImpl>(true)`, injects its `internal` fields, then calls `hostedRoot.SetActive(true)` after all bindings are registered and NonLazy instances are created. Chain `.NonLazy()` to ensure it is resolved at startup.
- `ProjectContext.HostedRoot`: new `[SerializeField]` field. Assign an inactive child GameObject in the prefab; FastIoC activates it after injection.
- `HostedComponentValidator`: generation fails with a clear message when a hosted binding is in a scene installer, `hostedRoot` is missing or active, or zero/multiple components of the bound type exist under it.
- `FastIoCAssetManifest`: a generated ScriptableObject at `Assets/Resources/Tools/FastIoC/AssetManifest.asset` that holds direct asset references for `FromInstance` path bindings whose assets live outside any `Resources/` folder and are not Addressables. No asset moves required; memory cost is the same as a serialized reference.
- `LoadingStrategy.Manifest`: new strategy selected automatically for global path bindings outside Resources and Addressables. Scene-specific bindings without a valid strategy fail generation with a clear message.
- `AssetPathValidator.TryGetResourcesLoadPath`: pure, testable method that finds the **last** `/Resources/` segment in a path and strips the extension. Fixes a bug where nested Resources paths (e.g. `Assets/MyGame/Resources/Config/Foo.asset`) generated an incorrect `Resources.Load` path.
- Global path bindings that instantiate prefabs now call `DontDestroyOnLoad` on the instance so the prefab survives scene changes.
- `IGlobalBindingSink` and `ExternalBinding` (`Runtime/Core`): a one-way bridge so a second DI container can expose FastIoC's global contracts. `ExternalBinding.Resolve()` goes straight to the runtime, bypassing the `ServiceLocatorEnforcer`.
- `ProjectContext.ExportGlobalBindings(IGlobalBindingSink)`: hands every global contract to the sink. The generated module emits one `sink.Add(typeof(Contract), new ExternalBinding(runtime, typeId))` per global contract; scene-scoped and framework bindings are excluded. Interfaces and their self-binding map to the same instance (shared concrete TypeId). Costs nothing unless called.
- `FastIoCUnityBootstrapper.AutoStart` and `FastIoCUnityBootstrapper.Start()`. Set `AutoStart` to false from a `SubsystemRegistration` method to start Fast IoC later, for example when another container runs in the same build.
- Added `Benchmarks~/`: a benchmark kit, profiles and run scripts that measure IL2CPP Release builds on Android and iOS phones, plus a summary script that stops on invalid results. The README's Performance section now has results from two Android phones and two iPhones.
- Added benchmark result schema documentation under `Documentation~/benchmarks/`.
- Added an Edit Mode test suite in `Tests/`: container lifetimes and scenes, cycle and missing-binding validation, generated activators and MonoBehaviour injectors (including `Lazy<T>`/`Func<T>`), installer analysis and `IPrefabInstantiator` active state. The README explains how to run it.
- The README lists planned features, starting with injecting every implementation of an interface.
- A **Platform Check** sample: add one component to a scene, build for any platform, and read PASS/FAIL results on screen for injection, `Lazy<T>`/`Func<T>`, prefab instantiation and initialization. WebGL with `High` stripping is verified with it.
- `Lazy<T>` and `Func<T>` dependencies, in constructors and `internal` MonoBehaviour fields. The generated code passes a wrapper that resolves `T` when it's used. Generation checks that `T` is bound, and doesn't report a cycle that goes through one of them. Run **Regenerate Code** after updating.
- Generation warns about `internal` MonoBehaviour fields whose type is an interface or abstract class from your code with no binding, since nothing fills them. They're also listed under the new `warnings` in `GenerationStatus.json`. Fields with another container's `[Inject]` attribute aren't reported, and the Console shows the first 20.
- Added `Documentation~/agent-rules.md`, rules to copy into a project's `AGENTS.md`, `CLAUDE.md` or Cursor rules so AI coding agents use Fast IoC correctly, and an `llms.txt` summary at the package root.
- The README has a FAQ and a section on using Fast IoC with AI coding agents. The package description and keywords now use the terms people search for.

### Changed

- **Breaking:** `IGeneratedModule` has a new method, `CreateNonLazyInstances`. Code generated by an earlier version no longer compiles, and **Regenerate Code** can't run while the project doesn't compile. Delete `Assets/FastIoC/Generated/`, let Unity compile, then run **Regenerate Code**.
- `AsSingle()` returns `BindingOptions` instead of `void`, so `.NonLazy()` can follow it. Existing installers compile unchanged.
- `IInstantiator` no longer adds an `InstantiationScope` component and a dictionary to every instance, and no longer allocates a component array: local references resolve from a reused list of the instance's components. Spawning allocates less and is faster. `InstantiationScope` is kept for compatibility but is no longer added automatically.
- Loading a scene no longer creates a temporary `ContextRuntime` (three arrays the size of all bindings) to read its recipe, and scenes without bindings of their own no longer push the global recipe as a scene container. Scene singletons and cached instances are still cleared on every scene unload.
- Injecting a newly loaded scene finds its manifest among the scene's root objects instead of searching every object in memory, and allocates nothing.
- `ProjectContext` skips the async initialization task, cancellation token and semaphore when nothing needs initializing yet.
- The scene-singleton and cached instance arrays are allocated only when a binding with that lifetime is first cached.
- Generated modules implement the new `IGeneratedModuleFeatures`, so the runtime can skip work the project doesn't need. Run **Regenerate Code** after updating; code generated by earlier versions still works through the slower paths.
- Console and Editor messages use plain text instead of emojis.
- `ContextRuntime.Resolve` reads lifecycle interfaces only when it creates an instance, not on every cached resolve. Lifecycle tracking uses a hash set instead of searching a list.
- Scene recipes are built the first time a scene needs them, and they share the global delegates.
- Generated injectors use the new `ContextRuntime.TryResolve`, so a binding missing from the current scene no longer throws an exception. Run **Regenerate Code** after updating.
- Benchmark README and performance docs now point raw evidence to `Benchmarks~/results/raw/` and keep schema validation in `Documentation~/benchmarks/`.

### Removed

- Removed the unused `Analyzers` folder. It was never compiled or called. Service-locator detection at runtime is unchanged.
- Removed the out-of-date Play Mode tests and benchmark suite. A new test suite will replace them.

### Fixed

- Constructor parameters with a compile-time default value that are not bound in any installer now keep that default in the generated activator (e.g. `int limit = 12` stays `12`). Previously the activator emitted `default(T)`, collapsing optional primitives to `0`/`false`/`null` even though the dependency validator already treats them as optional. Reference types and nullables without a default still fall back to `default(T)`. The generated parameter is now declared with its fully-qualified type, fixing invalid code for generic and nullable parameter types (e.g. `TimeSpan?`).
- The first scene of a player build could stay uninjected when the scene had no baked manifest (for example a build made before the Editor loaded new generated code). Fast IoC injected the scene from ProjectContext while the scene was still loading and marked it done, so the scene's load event skipped it. Scenes are now marked injected only once they've finished loading; objects injected earlier aren't injected twice.
- A player build now stops with a clear message if Fast IoC generated new code that the Editor hasn't loaded yet, instead of baking scenes from old code or without a manifest. Unchanged generated files are no longer rewritten, so regenerating the same code doesn't trigger a recompile.
- `IPrefabInstantiator` (used by tools such as the recyclable scroller and the screen system) now returns the copy active when the prefab is active, like `Object.Instantiate`. It used to return it inactive, so callers that didn't activate it themselves showed nothing. Awake and OnEnable still run after injection, the prefab is restored before injection runs, `Instantiate<T>` injects the whole copy even when `T` isn't a MonoBehaviour, and spawning no longer allocates a component array. `InstantiateAndInject` now leaves the copy inactive, as documented, instead of running Awake before injection.
- Deleting a script no longer leaves generated code that doesn't compile. Generation used to run before the recompile and read the deleted types still in memory; the generated code is now removed and regenerated after the compile.
- Player builds no longer fail with CS0234 in `ConstrainToRootOnly.cs`: its `UnityEditor` usings are now Editor-only.
- A project with no installers yet no longer shows warnings.
- A binding missing from the current scene no longer leaves the other fields of that MonoBehaviour null.
- Generated code no longer logs each loaded ScriptableObject in player builds.

### Documentation

- More package keywords.
- README: `NonLazy()`, `FromProjectContext()`, assets outside `Resources` in global installers, exporting global bindings to another container, default parameter values, and what to do when stale generated code doesn't compile.
- Removed the unpublished startup comparison until a reproducible benchmark exists.
- Said that the package isn't used in production or in a shipped game yet, and described Tools Core as a required dependency.
- Listed unsupported binding features under Limitations: named, value, factory, decorator and open generic bindings.
- Removed links to packages that aren't published yet.
- The Tests workflow no longer needs a token to clone Tools Core.

## [0.4.0] - 2026-11-03

### Changed

- The runtime no longer uses reflection. The generated `GeneratedModule` registers itself with `[RuntimeInitializeOnLoadMethod]`, instead of the runtime searching assemblies for generated types.
- Generated injectors assign `internal` MonoBehaviour fields directly instead of using `FieldInfo.SetValue`. An `internal` field in a package without `InternalsVisibleTo("Assembly-CSharp")` still uses reflection, and generation lists it in the Console.
- Generation writes `FastIoC.InternalsVisibleTo.g.cs` next to each `.asmdef` under `Assets/` that has injected `internal` fields.
- Scenes are injected from a `SceneInjectionManifest` baked at build time and in Play Mode, instead of searching every MonoBehaviour. Scenes without a manifest fall back to `FindObjectsByType`.
- **Breaking:** Prefab data (`IInjectableForChildren<T>.Data`) and local references are set by generated code. Local references are now filled only for `internal` fields.
- `IPrefabInstantiator` instantiation no longer calls `IInstantiator` through `MakeGenericMethod`.
- Scenes loaded after startup now have their MonoBehaviours injected.
- Existing projects regenerate automatically once after updating, because `GeneratedModule.cs` is missing.

### Removed

- **Breaking:** `FastIoCReflectionCache`, `GeneratedInjectorsCache`, `CachedTypeResolver`, `ITypeResolver`, `IInjectionStrategy`, `BatchScanInjectionStrategy`, `ReflectionInjectionStrategy`, `InjectionCoordinator`, `InjectableDataHandler`, `LocalComponentResolver`. `ReflectionUtility` moved to the Editor assembly.
- The generated `GeneratedInjectableMonoBehaviours.cs`. Generation deletes it.

### Fixed

- `IInjectableForChildren<T>.Data` is now set. It was never set before, because the runtime looked for an `Inject` method the interface doesn't have.
- **Breaking:** Injected fields were selected with a bit mask that also matched `private` fields, contrary to the documentation. Only `internal` fields are injected now. Generation warns about `private` fields with a bound type, so you can make the real dependencies `internal`.
- Inactive scene objects are injected in Unity 2023.1 and later.

## [0.3.0] - 2026-11-03

### Fixed

- Automatic regeneration now runs after each script compile when installers, bindings, constructors or `internal` MonoBehaviour fields change. It no longer polls every two seconds.
- `FromInstance(value)` now reaches the game. The installer runs once more at startup to create the value.
- A constructor parameter with no binding now fails generation, instead of passing `null`.
- Cycle detection now follows interface bindings.
- Binding an interface with `FromInstance(assetPath)` no longer generates code that fails to compile.
- The saved generation hash is now per project, so projects no longer share it.

### Changed

- Updated to Tools Core `0.3.0`, which removes `IMaskSource`.
- `BindingRecorder` no longer uses reflection to read binding builders, so installers are safe to run in IL2CPP builds.

### Documentation

- Corrected the `FromInstance(assetPath)` example, which needs `AsSingle()` and the asset's name or full path.
- Corrected the description of automatic regeneration and missing-binding validation.
- Added sections on binding assets, generated code, CI, failure examples and alternatives.
- Added contributing, security and issue template files, and a Quick Start sample.
- Removed internal development notes from code comments, and added XML documentation to the main binding APIs.

## [0.2.0] - Unreleased

### Changed

- **Breaking:** Renamed the namespaces from `Tools.FastIoC.*` to `FastIoC.*`, and removed `Runtime` from runtime namespaces.
- **Breaking:** Renamed the assemblies to `FastIoC`, `FastIoC.Core`, `FastIoC.Editor`, `FastIoC.Integration`, `FastIoC.Integration.Editor` and `FastIoC.Tests.PlayMode`.
- **Breaking:** Changed the generated code namespace to `FastIoCGenerated`. Delete `Assets/FastIoC/Generated/` before updating, then regenerate.

## [0.1.2] - Unreleased

- Updated to Tools Core `0.2.0`.

## [0.1.1] - Unreleased

- Removed the `.meta` file for an empty folder.

## [0.1.0] - Unreleased

First pre-release for private testing.

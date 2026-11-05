# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Changed

- Console and Editor messages use plain text instead of emojis.
- `ContextRuntime.Resolve` reads lifecycle interfaces only when it creates an instance, not on every cached resolve. Lifecycle tracking uses a hash set instead of searching a list.
- Scene recipes are built the first time a scene needs them, and they share the global delegates.
- Generated injectors use the new `ContextRuntime.TryResolve`, so a binding missing from the current scene no longer throws an exception. Run **Regenerate Code** after updating.

### Fixed

- A binding missing from the current scene no longer leaves the other fields of that MonoBehaviour null.
- Generated code no longer logs each loaded ScriptableObject in player builds.

### Removed

- Removed the unused `Analyzers` folder. It was never compiled or called. Service-locator detection at runtime is unchanged.
- Removed the out-of-date Play Mode tests and benchmark suite. A new test suite will replace them.

### Documentation

- Said that the package isn't used in production or in a shipped game yet, and described Tools Core as a required dependency.
- Listed unsupported binding features under Limitations: named, value, factory, decorator and open generic bindings.

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

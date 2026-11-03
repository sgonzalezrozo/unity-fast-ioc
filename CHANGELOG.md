# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Fixed

- Automatic regeneration now runs after each script compile when installers, bindings, constructors or `internal` MonoBehaviour fields change. It no longer polls every two seconds.
- `FromInstance(value)` now reaches the game. The installer runs once more at startup to create the value.
- A constructor parameter with no binding now fails generation, instead of passing `null`.
- Cycle detection now follows interface bindings.
- Binding an interface with `FromInstance(assetPath)` no longer generates code that fails to compile.
- The saved generation hash is now per project, so projects no longer share it.

### Changed

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

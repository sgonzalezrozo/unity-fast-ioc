# Project Status

[← Back to README](../README.md)

- [Status](#status)
- [Versioning](#versioning)
- [Contributing](#contributing)
- [Run the tests](#run-the-tests)
- [Related packages](#related-packages)
- [License](#license)

---

## Status

Pre-release (`0.9.4`), by a single author, not yet used in a shipped game. Its API and behavior may change before `1.0.0`.

An Edit Mode test suite of 81 tests covers the container, disposal, graph validation, the code generators, generated-code recovery, deleted-script detection, assembly linking and prefab instantiation. The last recorded run, in Unity `6000.3.6f1`, reported 80 passing; see the table below for what each area checks. The Platform Check sample passed in IL2CPP builds on Android, iOS and WebGL with 0.8.x. See [Run the tests](#run-the-tests).

Known gaps are listed under [Limitations](troubleshooting.md#limitations), and the next features under [Planned features](troubleshooting.md#planned-features).

## Versioning

Until `1.0.0`, minor versions (`0.x.0`) can include breaking changes, and patch versions (`0.x.y`) don't. Breaking changes are marked **Breaking** in the [changelog](../CHANGELOG.md).

## Contributing

Bug reports and pull requests are welcome; please open an issue before a large change. See the [contributing guide](../.github/CONTRIBUTING.md) and [security policy](../.github/SECURITY.md). Planned work is tracked in [issues](https://github.com/sgonzalezrozo/unity-fast-ioc/issues).

## Run the tests

The tests live in `Tests/` and run in Edit Mode. Unity compiles a package's tests only in a project that lists it as testable:

1. In a project that uses Fast IoC, add the [Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest) package.
2. Add this to `Packages/manifest.json`, next to `dependencies`:

   ```json
   "testables": ["com.sgonzalezrozo.fast-ioc"]
   ```

3. Open **Window > General > Test Runner**, select **EditMode** and **Run All**.

The repository has a Tests workflow in `.github/workflows/tests.yml`, but CI isn't set up yet: it runs only by hand, and needs a Unity license in the repository secrets.

| Area | Tests | What they check |
|---|---|---|
| Container | 8 | Singleton, transient and cached lifetimes, scene bindings, resolving an unbound type, and one instance for a class bound to several contracts |
| Disposal | 9 | Scene and app-exit disposal, reverse creation order, transients and instances Fast IoC didn't create left alone, a failing `Dispose` that doesn't stop the rest |
| Graph validation | 6 | Constructor cycles, cycles through `Lazy<T>`, missing bindings and their messages, parameters with default values |
| Code generation | 46 | Generated activators and MonoBehaviour injectors for plain, `Lazy<T>` and `Func<T>` dependencies, default parameter values, private fields, installer analysis, `NonLazy()` order and validation, asset loading strategies, hosted components, global binding export, generated-code recovery |
| Deleted scripts | 4 | A deleted script the generated code uses is reported; unrelated scripts, generated files and non-scripts are ignored |
| Build guard | 2 | The message when a build needs generated code the Editor hasn't compiled yet, in batch mode and in the Editor |
| Assembly linking | 1 | Runtime assemblies with startup methods are always linked |
| Prefab instantiation | 5 | Active state of copies, the prefab restored after copying, the parent, `InstantiateAndInject` |

The test types live in their own `FastIoC.Fixtures` assembly, because generation skips assemblies whose name contains `Test`.

## Related packages

| Package | Purpose |
|---|---|
| [Tools Core](https://github.com/sgonzalezrozo/unity-tools-core) | Shared injection code, required by this package |
| [Screen System](https://github.com/sgonzalezrozo/unity-screen-system) | Layer-based UI manager, injected by Fast IoC with no setup |
| [UI Tweens](https://github.com/sgonzalezrozo/unity-ui-tweens) | ECS-based UI animation |
| [UI Foundations](https://github.com/sgonzalezrozo/unity-ui-foundations) | uGUI click Controllers and view states |
| [Unity Architecture Agents](https://github.com/sgonzalezrozo/unity-architecture-agents) | Architecture rules for coding agents |

## License

Released under the [MIT License](../LICENSE).

# Troubleshooting & Limitations

[← Back to README](../README.md)

- [Troubleshooting](#troubleshooting)
- [Limitations](#limitations)
- [Planned features](#planned-features)

---

## Troubleshooting

| Problem | Try this |
|---|---|
| Play Mode stops with `CODE GENERATION FAILED` | Fix the listed errors, then run **Regenerate Code** |
| `requires ..., which isn't bound in any installer` | Bind the type, or give the constructor parameter a default value |
| `Could not resolve asset path` | Use the asset's name or its full `Assets/...` path |
| `neither in Resources/ folder nor marked for Addressables` | Move the asset to a `Resources` folder or mark it as Addressable |
| `TypeId ... is not registered` | Add a binding, and check that its scene installer applies to the active scene |
| A MonoBehaviour field stays `null` | Make the field `internal`, bind its type, and use it from `Start()`. Check `warnings` in `GenerationStatus.json`. |
| `These private fields have a bound type but aren't injected` | Make the field `internal` if it's a dependency. If it's private state, ignore the warning. |
| `injected field(s) still use reflection` | The listed fields are `internal` in an assembly that doesn't expose its internals. Add `[assembly: InternalsVisibleTo("Assembly-CSharp")]` to it, then regenerate. |
| `Generated code is not registered` | The project has installers but no generated code. Run **Regenerate Code**, and check that `Assets/FastIoC/Generated/GeneratedModule.cs` exists. A project with no installers yet has nothing to inject and logs nothing. |
| `has no public constructor, so the generated code can't create it` | Make the constructor public. Generated code can't call a private, protected or internal one. |
| `Generated code didn't compile, so Fast IoC removed it` | Fix the errors listed in `GenerationStatus.json`. The next change regenerates; **Regenerate Code** retries right away. |
| `This is the same generated code that failed to compile` | Generation refused to write code that already broke the build. Fix the listed cause, or run **Regenerate Code** if the fix doesn't change the generated code (for example, an added `InternalsVisibleTo`). |
| Compile errors in `Assets/FastIoC/Generated/` that stay after an update from 0.5.0 or earlier | Those versions can't remove generated code that doesn't compile. Delete the `Assets/FastIoC/Generated/` folder once, let Unity compile, then run **Regenerate Code**. |
| `Service Locator Anti-Pattern Detected` | Inject the dependency instead of resolving it from `ContextRuntime`. The default level is strict, so this throws in the Editor and development builds. |

More detail on failed generations is in [Code Generation › When generation fails](code-generation.md#when-generation-fails).

## Limitations

- MonoBehaviour fields must be `internal`. Local references inside an instantiated prefab are also filled only for `internal` fields.
- Generation only warns about unbound MonoBehaviour fields typed as an interface or abstract class. Unbound fields of concrete classes aren't reported.
- Installers with a `FromInstance(value)` binding run once more at startup.
- Installers run in the Editor during generation, so don't rely on runtime-only state in `InstallBindings()`. Bindings are fixed when the code is generated and can't change at runtime.
- Bindings are by type only. There are no named or ID bindings, and no value bindings: `Bind<T>()` takes a class, so put configuration such as an `int` or a URL in a typed config class and bind that.
- `Func<T>` takes no arguments: there are no factories with runtime parameters yet. There are no decorators or open generic bindings either. Bind each closed type, such as `IRepository<Player>`, on its own.

## Planned features

These are missing today and planned for upcoming versions, roughly in this order:

1. **Inject every implementation of an interface.** A constructor or field asking for `IEnumerable<IFeature>` or `IFeature[]` gets every bound `IFeature`, for example to start all features in one place.
2. **Factories with runtime parameters.** `Func<T>` takes no arguments today, so there's no way to pass a value, such as an enemy's level, when creating `T`.
3. **Optional dependencies.** A way to mark a constructor parameter or field as optional, so a missing binding leaves it `null` without an error or warning.
4. **Test helpers.** A small API to build a container in a unit test and swap a binding for a fake.
5. **Named bindings.** More than one binding of the same type, told apart by an ID.
6. **Decorators and open generic bindings.** Wrapping a service in another one, and binding `IRepository<>` once for every `T`.

Ideas and votes are welcome in [issues](https://github.com/sgonzalezrozo/unity-fast-ioc/issues).

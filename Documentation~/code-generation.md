# Code Generation

[← Back to README](../README.md)

- [Generated files](#generated-files)
- [Scene manifests](#scene-manifests)
- [What to commit](#what-to-commit)
- [When regeneration runs](#when-regeneration-runs)
- [Run it in CI](#run-it-in-ci)
- [When generation fails](#when-generation-fails)
- [Examples of generation errors](#examples-of-generation-errors)

---

## Generated files

Fast IoC writes these files to `Assets/FastIoC/Generated/`:

| File | Contents |
|---|---|
| `GeneratedTypeIds.cs` | An ID for each type |
| `GeneratedActivators.cs` | The code that creates each service |
| `GeneratedMonoInjectors.cs` | The code that fills `internal` MonoBehaviour fields, prefab data and local references |
| `GeneratedModule.cs` | Registers the generated code with the runtime at startup |
| `GeneratedRegistry.cs` | Global and scene bindings |
| `GeneratedInstanceRegistry.cs`, `InstanceRegistryInitializer.cs` | Assets bound with `FromInstance` |
| `GenerationStatus.json` | The result of the last generation |

If a MonoBehaviour with `internal` injected fields lives in an assembly definition under `Assets/`, Fast IoC also writes `FastIoC.InternalsVisibleTo.g.cs` next to that `.asmdef`, so the generated code in `Assembly-CSharp` can assign those fields. Commit it too. Assemblies in packages are never modified: add `[assembly: InternalsVisibleTo("Assembly-CSharp")]` to them yourself.

## Scene manifests

When Unity builds a scene, and when a scene loads in Play Mode in the Editor, Fast IoC adds a hidden `SceneInjectionManifest` to it. It holds references to the scene's injectable MonoBehaviours and the index of each one's injector, so scene injection doesn't search the scene. The scene asset itself is never changed. If a scene has no manifest, for example when Play Mode starts without reloading the scene, Fast IoC finds the objects with `FindObjectsByType` instead.

## What to commit

Commit the `.cs` files and their `.meta` files, because player builds need them. Also commit `Assets/FastIoC/Generated/.gitignore`: Fast IoC writes it so Git ignores `GenerationStatus.json`, which changes on every generation and is only read in the Editor. Unity doesn't import files that start with a dot, so it has no `.meta`.

If the status file was committed before, stop tracking it once:

```bash
git rm --cached Assets/FastIoC/Generated/GenerationStatus.json Assets/FastIoC/Generated/GenerationStatus.json.meta
```

## When regeneration runs

Fast IoC checks for changes after each script compile, and regenerates when you've changed:

- An installer, including adding or removing one
- A binding, or the scenes or priority of its installer
- The constructor of a bound type
- An `internal` field on a MonoBehaviour

It also regenerates when you delete a script the generated code uses; deleting any other script, such as one from another package, leaves the generated code alone. After a failed generation, it tries again on the next compile. To regenerate at any time, run **Tools > FastIoC > Regenerate Code**.

Every player build regenerates first. When nothing changed, the build goes on as usual. When something changed, the Editor hasn't compiled it yet, so the build stops and asks you to build again once Unity finishes compiling. A build never uses generated code that's missing or out of date, and a failed generation stops it. Define `FASTIOC_SKIP_BUILD_GENERATION` to skip the regeneration, for example when CI generates in its own run; a failed generation recorded in `GenerationStatus.json` still stops the build.

## Run it in CI

Batch mode doesn't regenerate by itself after a compile. Run generation as its own step, then build in a second Unity run, so the build compiles the new code:

```bash
unity -batchmode -quit -projectPath . \
  -executeMethod FastIoC.Editor.Generation.GenerationCoordinator.GenerateAll
```

If the build regenerates code that the previous step didn't write, it stops instead of building with old code, and its message says so: run the build again, which compiles the code the stopped run wrote. A build also stops when the project has installers but no generated code.

Deleting a script the generated code uses doesn't remove the generated code in batch mode, because nothing would regenerate it there: the compile error names what to fix.

Generation catches its own errors, so Unity can still exit with code `0` after a failed run. Check the status file afterward. A value of `1` means it failed:

```bash
if grep -q '"status": *1' Assets/FastIoC/Generated/GenerationStatus.json; then exit 1; fi
```

## When generation fails

- The errors are logged in the Console, and `GenerationStatus.json` records them.
- The previous generated files stay in place.
- Entering Play Mode logs `CODE GENERATION FAILED` and pauses the Editor.
- Player builds stop, because every build regenerates first and stops when generation fails.

If the generated code itself doesn't compile, generation could never run again, because it runs after a successful compile. Fast IoC breaks that loop. When every compile error is in the generated code, it removes the generated sources (keeping `GenerationStatus.json`, which lists the compile errors) so the project compiles, and it won't write that same code again. Fix the cause and the next change regenerates; **Regenerate Code** always retries. Player builds stop until new generated code is written. If your own code also has errors, nothing is removed: fix those first. In batch mode nothing is removed, so CI fails on the compile error.

A player build does stop if Fast IoC generated new code that the Editor hasn't loaded yet, for example when you build right after **Regenerate Code**. Wait for Unity to finish compiling, then build again. Scenes are baked from the generated code loaded in the Editor, so building earlier would bake them from the old code.

## Examples of generation errors

### A dependency cycle

```csharp
public class OrderService
{
    public OrderService(PaymentService payment) { }
}

public class PaymentService
{
    public PaymentService(OrderService order) { }
}
```

If both are bound, generation fails, and Play Mode shows:

```text
[FastIoC] CODE GENERATION FAILED
Last attempt: <time>
Error: Dependency graph contains cycles

Details:
  • Cycle detected: PaymentService -> OrderService

Fix the issues and regenerate code:
Tools > FastIoC > Regenerate Code
```

### A missing binding

```csharp
public class ShopService
{
    // IPaymentGateway has no binding in any installer.
    public ShopService(IPaymentGateway gateway) { }
}
```

If `ShopService` is bound, generation fails, and Play Mode shows:

```text
[FastIoC] CODE GENERATION FAILED
Last attempt: <time>
Error: Dependency graph has missing bindings

Details:
  • ShopService requires IPaymentGateway, which isn't bound in any installer.

Fix the issues and regenerate code:
Tools > FastIoC > Regenerate Code
```

Constructor parameters with a default value are skipped. If their type isn't bound, they keep that default value, so `int retries = 3` gets `3`.

### An unbound MonoBehaviour field

```csharp
public class ShopView : MonoBehaviour
{
    // IShopService has no binding in any installer.
    internal IShopService shop;
}
```

Generation still succeeds, because an `internal` field isn't always a dependency, but nothing fills this one. Fast IoC warns about `internal` fields whose type is an interface or abstract class from your code with no binding, and lists them under `warnings` in `GenerationStatus.json`:

```text
[FastIoC] 1 `internal` field(s) have a service type that isn't bound in any installer, so they stay null.
  • ShopView.shop (IShopService)
```

Fields of concrete classes aren't checked, because they're often plain state. Fields with another container's `[Inject]` attribute are skipped too, so a project moving from Zenject or VContainer doesn't get a warning for every field the old container still fills.

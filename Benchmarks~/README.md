# Fast IoC Benchmarks~

Compares Fast IoC with Zenject (Extenject) and VContainer on real phones, in IL2CPP Release builds. The `~` keeps this folder out of package imports.

## Layout

```text
Benchmarks~/
├── Kit/          local UPM package: fixture generator, runner, result writer (Editor + Runtime)
├── profiles/     project sizes, each with a fixed seed
├── tools/        device run scripts, result check, summary
└── results/      raw JSON per run, and summaries
```

The Unity project itself is disposable and lives outside the repo (`../FastIoCBenchmarkProject`), so Zenject, VContainer and generated code never enter this repository. Its manifest references this repo with `file:` paths, including `Kit/`.

## What one build measures

One build contains the three containers plus a `none` baseline (same scene and prefabs, no container, nothing injected) that measures what Unity costs on its own. Each launch runs **one** variant from a cold process, following a plan stored on the device (`persistentDataPath/FastIoCBenchmarks/plan.json`). The order rotates every round. Fast IoC is kept dormant with `FastIoCUnityBootstrapper.AutoStart = false`; Zenject and VContainer only start when their scene or root scope loads.

Per launch, in this order:

| Metric | What it is |
|---|---|
| `container-build` | Building the global container with every binding (Fast IoC `Start()`, Zenject `ProjectContext.Instance`, VContainer root `LifetimeScope`). Singletons are not created yet. |
| `scene-first-inject` | First load of the scene that resolves the whole graph: from its first `Awake` to its first `Start`. Includes creating every singleton and injecting every scene MonoBehaviour. IL2CPP first-use costs land here. |
| `scene-first-load` | Same, from the `LoadScene` call (adds file loading and the rest of that frame). |
| `scene-first-preawake` | From the `LoadScene` call to the scene's first `Awake`: Unity loading, before any container runs. |
| `startup-total` | From the first managed code in the process to the scene being injected. |
| `scene-reload-inject` | 5 unload/load cycles of the same scene, singletons already built. median/p95/min/max. |
| `prefab-batch` | 1,000 injected prefab instances in batches of 50 per frame, through each container's own API. |
| `heap-after-ready`, `*-heap-growth`, `*-gc-collections` | Managed memory, Release-safe (`GC.GetTotalMemory`, `GC.CollectionCount`). |
| `graph-valid` | 1 if the resolved graph has the hash, instance count and field count the generator expects. A run with 0 must not be used. |

Every service exposes an id and its dependencies, so the runner hashes what each container actually built. Lifetimes are singleton and transient only; transients depend only on singletons.

## Run it

Requirements: Unity 6000.3, Android SDK/`adb`, Xcode 15+ for iOS.

1. Open `../FastIoCBenchmarkProject` in Unity Hub (**Add > Add project from disk**).
2. Add Extenject: **Window > Package Manager > + > Install package from git URL**:
   `https://github.com/Mathijs-Bakker/Extenject.git?path=UnityProject/Assets/Plugins/Zenject/Source`
3. **Tools > FastIoC > Setup**.
4. **Tools > FastIoC Benchmark**: Apply player settings, pick a profile, Generate. Wait until the window says the profile is ready (two compiles).
5. Enter Play Mode once on the Boot scene: the Console must say `Graph OK` for the variant it ran.
6. Build Android or iOS from the same window. Builds go to `Builds/`.
7. Install and run the plan (4 variants x runs per variant launches, 20 by default):

   ```bash
   adb -s <SERIAL> install -r Builds/Android/FastIoCBench-<profile>.apk
   ../unity-fast-ioc/Benchmarks~/tools/run_android.sh <SERIAL> <device> 20
   # iOS: build and install the Xcode project, then
   ../unity-fast-ioc/Benchmarks~/tools/run_ios.sh <DEVICE_ID> <device> 20
   ```

8. Check, copy and summarize:

   ```bash
   python3 tools/check_variants.py ~/Library/Logs/Unity/fastioc-compare/<device>
   cp ~/Library/Logs/Unity/fastioc-compare/<device>/*.json results/raw/   # skip plan.json
   python3 tools/summarize_results.py
   ```

Each result records device, OS, Unity version, Fast IoC commit (and whether the tree was dirty), Zenject and VContainer versions from `packages-lock.json`, stripping level, profile, seed and run index. Publish only runs from a clean commit, and publish unfavorable results too.

## Known limits

- Fast IoC bakes an injection manifest into every scene in the build, including the Zenject and VContainer scenes, where it is never read. Its load cost there is a few serialized references.
- VContainer registers its PlayerLoop systems at startup in every launch, whichever variant runs.
- Zenject reflection baking and the VContainer source generator are not included yet.
- `startup-200` is an optimization target and should not be mixed into scaling charts.
- `game-3000` is the size of a typical mobile multiplayer game (about 3x the README profile) and is the headline profile for the brief.

The previous `Assets/` and `Packages/` scaffold folders here are superseded by `Kit/` and the external project.

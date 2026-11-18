# Performance

[← Back to README](../README.md)

- [Device results](#device-results)
- [Measure startup time](#measure-startup-time)

---

## Device results

Measured in IL2CPP Release builds on four phones, with Unity `6000.3.6f1`, `Minimal` stripping and incremental GC. The test project has 3,000 bindings, 1,500 injected MonoBehaviours in 100 features and dependency chains up to 12 deep, roughly the size of a mobile multiplayer game. Each value is the median of 5 cold launches.

| Metric | Pixel 5 (mid-range Android) | Galaxy A02s (low-end Android) | iPhone 13 mini (2021) | iPhone Air (2025) |
|---|---|---|---|---|
| Build the global container | 9.7 ms | 21.2 ms | 6.4 ms | 4.2 ms |
| Inject the first scene (1,500 objects) | 28.9 ms | 102.5 ms | 8.1 ms | 6.4 ms |
| Re-inject the scene after a reload, median / p95 | 11.5 / 15.3 ms | 23.0 / 25.9 ms | 2.0 / 3.2 ms | 4.1 / 4.8 ms |
| Spawn 50 injected prefabs, median / p95 | 3.3 / 6.3 ms | 7.2 / 10.4 ms | 3.0 / 5.0 ms | 2.3 / 3.4 ms |
| Managed heap after startup (same scene without DI) | 1.81 MB (0.89 MB) | 1.19 MB (0.69 MB) | 1.80 MB (0.89 MB) | 1.81 MB (0.89 MB) |
| Garbage for 1,000 prefab spawns (same scene without DI) | 116 KB (108 KB) | 56 KB (48 KB) | 124 KB (116 KB) | 120 KB (120 KB) |

- **Startup runs once, while the first scene loads.** On the same phones, Unity itself takes about 430 ms (Pixel 5) and 770 ms (A02s) to load that scene. On both iPhones, the whole startup costs Fast IoC less than one 30 fps frame.
- **Spawning fits in a frame.** A batch of 50 prefabs stays well under the 33 ms of a 30 fps frame, even on the A02s.
- **Garbage stays close to plain Unity.** Spawning 1,000 prefabs allocates at most 8 KB more than the same prefabs without DI.

The raw results for every run are in [`Benchmarks~/results/raw/`](../Benchmarks~/results/raw/), and [`Benchmarks~/README.md`](../Benchmarks~/README.md) explains how to run the same tests on your own devices. Numbers from your own project will differ, so measure it.

## Measure startup time

Add the scripting define symbol `FASTIOC_BOOT_TIMING` (**Project Settings > Player > Scripting Define Symbols**, for each platform you build) to log how long each startup step takes. Without the symbol the timing calls are compiled out, so they cost nothing.

```text
[BootTiming] FastIoC Bootstrap: load ProjectContext prefab                       12 ms  @    1.023 s
[BootTiming] FastIoC ProjectContext: instance bindings (assets, prefabs, hosted) 140 ms  @    1.180 s
[BootTiming] FastIoC NonLazy: MyGame_Services_Analytics                           35 ms  @    1.230 s
[BootTiming] FastIoC Scene loaded: container + injection (Menu)                    4 ms  @    2.410 s
```

Each line gives a step and its duration. The time after `@` is seconds since the engine started (`Time.realtimeSinceStartup`), so you can log your own steps with the same clock and read everything as one timeline. Lines cover: loading and instantiating the `ProjectContext` prefab, each step of its startup (runtime, instance bindings, scene and hosted injection, every NonLazy binding, activation of hosted components), initialization, and the work done when each scene loads and unloads.

On Android, read them with:

```bash
adb logcat -s Unity | grep --line-buffered "\[BootTiming\]"
```

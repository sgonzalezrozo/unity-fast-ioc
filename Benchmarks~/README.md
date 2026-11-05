# Fast IoC Benchmarks~

Benchmark scaffold for Fast IoC and comparison variants.

This folder is a staged Unity benchmark project layout stored inside the package repository on purpose.  
The `~` suffix keeps it out of package imports while preserving benchmark tooling in-source.

## Current scope

- Project structure and profiles are scaffolded.
- Result JSON contract is shared with:
  - `../Documentation~/benchmarks/benchmark-result.schema.json`
- Basic result summarization tooling is available in `tools/`; build/run automation is still in progress.

## Unity version

The project targets Unity `6000.3.16f1`, the version both READMEs list as tested. Add `ProjectSettings/ProjectVersion.txt` and `Packages/packages-lock.json` when the project is first opened, and commit both so runs can be repeated.

## Expected flow

1. Complete missing Unity project files (for example `ProjectSettings/`) and open `Benchmarks~/`.
2. Select profile + variant from benchmark tooling (Editor window or CLI wrapper).
3. Generate fixture graph with fixed seed.
4. Build IL2CPP player.
5. Run repeated launches on device and collect raw JSON in `results/raw/`.
6. Run `tools/summarize_results.py` to produce medians and p95 per commit, metric aggregation and device/build context. It stops on any invalid file.

## Profile notes

- `startup-200` is an optimization target profile for startup-heavy bindings and should not be mixed into linear scaling charts.
- Scaling curves should use `small-100`, `growth-500`, `readme-scale-1000`, `mature-2500`, and `large-team-5000`.

## Folder layout

```text
Benchmarks~/
├── Assets/Benchmark/
│   ├── Editor/
│   ├── Runtime/
│   └── Generated/
├── Packages/manifest.json
├── profiles/
├── results/
│   ├── raw/
│   └── summary/
└── tools/
```

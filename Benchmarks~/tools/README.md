# Tools

- `summarize_results.py`: reads schema-compatible files in `results/raw/*.json` and writes median/p95 summaries to `results/summary/summary.json`, grouped by scenario, variant, metric, source aggregation, commit, build mode, backend, device model and Unity version. It stops on any invalid file and warns when a group has fewer runs than `totalRuns`. Use `--raw` and `--out` to change the folders.

Planned additions:

- build automation entrypoint (Unity batch mode)
- install/launch automation per platform
- repeated-run orchestrator with process restart between runs

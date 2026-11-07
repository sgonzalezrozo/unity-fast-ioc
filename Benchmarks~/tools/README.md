# Tools

- `run_android.sh <serial> <device> [launches]`: launches the app once per run on an Android phone, waits for it to quit, then pulls the results to `~/Library/Logs/Unity/fastioc-compare/<device>/`.
- `run_ios.sh <device-id> <device> [launches] [seconds]`: same for iOS with `xcrun devicectl`.
- `check_variants.py <folder>`: checks that every run resolved the expected graph and that no run is missing, and prints medians per device, profile and variant. Exits with an error otherwise.
- `summarize_results.py`: reads schema-compatible files in `results/raw/*.json` and writes median/p95 summaries to `results/summary/summary.json`, grouped by scenario, variant, metric, source aggregation, commit, build mode, backend, device model and Unity version. It stops on any invalid file and warns when a group has fewer runs than `totalRuns`. Use `--raw` and `--out` to change the folders.

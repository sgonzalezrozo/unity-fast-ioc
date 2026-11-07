#!/usr/bin/env python3
"""Checks a folder of raw results before summarizing: every variant resolved the expected graph,
and every (device, profile, variant) has the expected number of runs. Prints a short table of medians."""
import json
import re
import sys
from collections import defaultdict
from pathlib import Path
from statistics import median

KEY_METRICS = ["container-build", "scene-first-inject", "scene-first-preawake", "scene-reload-inject", "prefab-batch", "heap-after-ready"]


def metric(result, name, aggregation=None):
    for m in result["metrics"]:
        if m["name"] == name and (aggregation is None or m.get("aggregation") == aggregation):
            return m["value"]
    return None


def main():
    folder = Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    results = [json.loads(p.read_text()) for p in sorted(folder.glob("*.json")) if p.name != "plan.json"]
    if not results:
        sys.exit(f"No results in {folder}")

    groups = defaultdict(list)
    problems = []
    for r in results:
        key = (r["session"]["deviceModel"], r["workload"]["profileOrScenario"], r["workload"]["variant"])
        groups[key].append(r)
        notes = r["session"].get("notes", "")
        if r["workload"]["variant"] == "none":
            continue  # baseline: nothing injected, nothing to check
        if metric(r, "graph-valid") != 1 or metric(r, "graph-valid-after-reload") != 1:
            problems.append(f"{key} run {r['workload']['runIndex']}: wrong graph ({notes})")
        coverage = metric(r, "optimization-coverage")
        if r["workload"]["variant"] in ("zenject-baked", "vcontainer-sg") and (coverage is None or coverage < 100):
            problems.append(f"{key} run {r['workload']['runIndex']}: optimization covers {coverage}% of the classes, not 100%")
        if (metric(r, "prefab-null-fields") or 0) != 0:
            problems.append(f"{key} run {r['workload']['runIndex']}: prefab fields not injected")
        if "fastiocDirty=true" in notes:
            problems.append(f"{key} run {r['workload']['runIndex']}: built from uncommitted Fast IoC changes")

    print(f"{'device':<18}{'profile':<20}{'variant':<12}{'runs':>5}  " + "  ".join(f"{m:>20}" for m in KEY_METRICS))
    for key, rs in sorted(groups.items()):
        expected = max(r["workload"].get("totalRuns", 0) for r in rs)
        if len(rs) < expected:
            problems.append(f"{key}: {len(rs)} of {expected} runs")
        cells = []
        for name in KEY_METRICS:
            agg = "median" if name in ("scene-reload-inject", "prefab-batch") else None
            values = [v for v in (metric(r, name, agg) for r in rs) if v is not None]
            cells.append(f"{median(values):>20.2f}" if values else f"{'-':>20}")
        print(f"{key[0][:17]:<18}{key[1]:<20}{key[2]:<12}{len(rs):>5}  " + "  ".join(cells))

    if problems:
        print("\nProblems:")
        for p in problems:
            print("  " + p)
        sys.exit(1)
    print("\nAll runs resolved the expected graph (the none baseline injects nothing and is not checked).")


if __name__ == "__main__":
    main()

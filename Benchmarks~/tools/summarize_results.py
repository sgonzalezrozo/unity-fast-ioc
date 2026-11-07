#!/usr/bin/env python3
"""Summarizes raw benchmark JSON files. Any invalid file stops the run, so no result is lost silently."""
import argparse
import json
import math
import re
import sys
from pathlib import Path
from statistics import median
from typing import Dict, List, Tuple

COMMIT_PATTERN = re.compile(r"^(?!0+$)[0-9a-fA-F]{7,40}$")
SESSION_FIELDS = (
    "deviceName",
    "deviceModel",
    "osVersion",
    "unityVersion",
    "gitCommit",
    "buildMode",
    "scriptingBackend",
)
WORKLOAD_FIELDS = ("profileOrScenario", "variant", "runIndex")
GROUP_FIELDS = (
    "scenario",
    "variant",
    "metric",
    "unit",
    "sourceAggregation",
    "gitCommit",
    "buildMode",
    "scriptingBackend",
    "deviceModel",
    "unityVersion",
)


def p95(values: List[float]) -> float:
    ordered = sorted(values)
    return ordered[max(0, int(round(0.95 * (len(ordered) - 1))))]


def validate(result: object) -> List[str]:
    if not isinstance(result, dict):
        return ["the top level must be an object"]

    errors: List[str] = []
    session = result.get("session")
    workload = result.get("workload")
    metrics = result.get("metrics")

    if not isinstance(session, dict):
        errors.append("missing session")
    else:
        errors.extend(f"missing session.{name}" for name in SESSION_FIELDS if not session.get(name))
        if not COMMIT_PATTERN.match(str(session.get("gitCommit", ""))):
            errors.append(f"gitCommit isn't a real commit: {session.get('gitCommit')!r}")

    if not isinstance(workload, dict):
        errors.append("missing workload")
    else:
        errors.extend(f"missing workload.{name}" for name in WORKLOAD_FIELDS if not workload.get(name))

    if not isinstance(metrics, list) or not metrics:
        errors.append("missing metrics")
        return errors

    for index, metric in enumerate(metrics):
        if not isinstance(metric, dict):
            errors.append(f"metrics[{index}] must be an object")
            continue

        value = metric.get("value")
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
            errors.append(f"metrics[{index}].value isn't a number: {value!r}")

        if not metric.get("name") or not metric.get("unit"):
            errors.append(f"metrics[{index}] needs a name and a unit")

    return errors


def load_results(raw_folder: Path) -> List[Dict[str, object]]:
    results: List[Dict[str, object]] = []
    problems: List[str] = []

    for file_path in sorted(raw_folder.rglob("*.json")):
        try:
            result = json.loads(file_path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as error:
            problems.append(f"{file_path.name}: {error}")
            continue

        errors = validate(result)
        if errors:
            problems.extend(f"{file_path.name}: {error}" for error in errors)
        else:
            results.append(result)

    if problems:
        raise SystemExit("Invalid benchmark results:\n" + "\n".join(f"  {problem}" for problem in problems))

    if not results:
        raise SystemExit(f"No benchmark results found in {raw_folder}.")

    return results


def summarize(results: List[Dict[str, object]]) -> List[Dict[str, object]]:
    grouped: Dict[Tuple[str, ...], List[float]] = {}
    expected_runs: Dict[Tuple[str, ...], int] = {}

    for result in results:
        session = result["session"]
        workload = result["workload"]

        for metric in result["metrics"]:
            key = (
                str(workload["profileOrScenario"]),
                str(workload["variant"]),
                str(metric["name"]),
                str(metric["unit"]),
                str(metric.get("aggregation") or "single"),
                str(session["gitCommit"]).lower(),
                str(session["buildMode"]),
                str(session["scriptingBackend"]),
                str(session["deviceModel"]),
                str(session["unityVersion"]),
            )
            grouped.setdefault(key, []).append(float(metric["value"]))
            expected_runs[key] = max(expected_runs.get(key, 0), int(workload.get("totalRuns") or 0))

    entries: List[Dict[str, object]] = []
    for key, values in sorted(grouped.items()):
        entry: Dict[str, object] = dict(zip(GROUP_FIELDS, key))
        entry["sampleCount"] = len(values)
        entry["expectedRuns"] = expected_runs[key]
        entry["median"] = median(values)
        entry["p95"] = p95(values)
        entries.append(entry)

        if len(values) < expected_runs[key]:
            print(
                f"Warning: {key[0]} / {key[1]} / {key[2]} has {len(values)} of {expected_runs[key]} runs.",
                file=sys.stderr,
            )

    return entries


def main() -> None:
    results_root = Path(__file__).resolve().parents[1] / "results"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw", type=Path, default=results_root / "raw", help="folder with raw JSON files")
    parser.add_argument("--out", type=Path, default=results_root / "summary" / "summary.json", help="summary file to write")
    args = parser.parse_args()

    entries = summarize(load_results(args.raw))

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps({"entries": entries}, indent=2), encoding="utf-8")
    print(f"Wrote {args.out}")


if __name__ == "__main__":
    main()

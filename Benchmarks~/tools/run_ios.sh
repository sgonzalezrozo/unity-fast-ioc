#!/usr/bin/env bash
# Runs the whole plan on one iPhone (Xcode 15+): one cold launch per run, then copies the results.
# Usage: tools/run_ios.sh <device-id> <device-folder-name> [launches=30]
# Device id: xcrun devicectl list devices. Keep the phone unlocked, charging, with Auto-Lock off.
# No time limit: each launch waits until the run has written its result file.
# Stops early once the phone has a full plan of results (TOTAL, 30 by default), so extra launches are harmless.
set -euo pipefail
ID="${1:?device id (xcrun devicectl list devices)}"; DEVICE="${2:?folder name, e.g. iphone13mini}"
LAUNCHES="${3:-30}"
TOTAL="${TOTAL:-30}" # results in a full plan (variants x runs per variant); the app stops writing after that
BUNDLE=com.sgrozo.fastiocbenchmark
OUT="$HOME/Library/Logs/Unity/fastioc-compare/$DEVICE"
mkdir -p "$OUT"

count_results() {
  xcrun devicectl device info files --device "$ID" --domain-type appDataContainer \
    --domain-identifier "$BUNDLE" --subdirectory Documents/FastIoCBenchmarks 2>/dev/null \
    | grep -o '[A-Za-z0-9_.-]*\.json' | grep -vc '^plan\.json$' || true
}

for i in $(seq 1 "$LAUNCHES"); do
  before=$(count_results); before=${before:-0}
  if [ "$before" -ge "$TOTAL" ]; then echo "The phone already has all $TOTAL results."; break; fi
  echo "Launch $i/$LAUNCHES (results on phone: $before)"
  xcrun devicectl device process launch --terminate-existing --device "$ID" "$BUNDLE" >/dev/null
  sleep 10
  waited=10
  while [ "$(count_results)" -le "$before" ]; do
    sleep 5; waited=$((waited + 5))
    if [ $((waited % 120)) -eq 0 ]; then echo "  still running (${waited}s)"; fi
  done
  sleep 5 # let the app quit on its own before the next cold launch
done

xcrun devicectl device copy from --device "$ID" --domain-type appDataContainer --domain-identifier "$BUNDLE" \
  --source Documents/FastIoCBenchmarks --destination "$OUT"
echo "Results: $OUT ($(ls "$OUT"/*.json 2>/dev/null | grep -vc plan.json) result files)"

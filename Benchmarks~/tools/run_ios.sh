#!/usr/bin/env bash
# Runs the whole plan on one iPhone (Xcode 15+): one cold launch per run, then copies the results.
# Usage: tools/run_ios.sh <device-id> <device-folder-name> [launches=30] [seconds-per-launch=45]
# Device id: xcrun devicectl list devices. Keep the phone unlocked with Auto-Lock off.
set -euo pipefail
ID="${1:?device id (xcrun devicectl list devices)}"; DEVICE="${2:?folder name, e.g. iphone13mini}"
LAUNCHES="${3:-30}"; WAIT="${4:-45}"
BUNDLE=com.sgrozo.fastiocbenchmark
OUT="$HOME/Library/Logs/Unity/fastioc-compare/$DEVICE"
mkdir -p "$OUT"
for i in $(seq 1 "$LAUNCHES"); do
  echo "Launch $i/$LAUNCHES"
  xcrun devicectl device process launch --terminate-existing --device "$ID" "$BUNDLE" >/dev/null
  sleep "$WAIT"
done
xcrun devicectl device copy from --device "$ID" --domain-type appDataContainer --domain-identifier "$BUNDLE" \
  --source Documents/FastIoCBenchmarks --destination "$OUT"
echo "Results: $OUT"

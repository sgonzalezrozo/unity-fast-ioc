#!/usr/bin/env bash
# Runs the whole plan on one Android phone: one cold launch per run, then pulls the results.
# Usage: tools/run_android.sh <adb-serial> <device-folder-name> [launches=20]
set -euo pipefail
S="${1:?adb serial (adb devices)}"; DEVICE="${2:?folder name, e.g. pixel5}"; LAUNCHES="${3:-20}"
PKG=com.sgrozo.fastiocbenchmark
OUT="$HOME/Library/Logs/Unity/fastioc-compare/$DEVICE"
mkdir -p "$OUT"
adb -s "$S" shell svc power stayon true || true
for i in $(seq 1 "$LAUNCHES"); do
  echo "Launch $i/$LAUNCHES"
  adb -s "$S" shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
  sleep 3
  # No time limit: each launch runs until the app quits by itself.
  while adb -s "$S" shell pidof "$PKG" >/dev/null 2>&1; do
    sleep 2
  done
  sleep 3
done
adb -s "$S" shell svc power stayon false || true
adb -s "$S" pull "/sdcard/Android/data/$PKG/files/FastIoCBenchmarks/." "$OUT/"
echo "Results: $OUT ($(ls "$OUT"/*.json 2>/dev/null | grep -vc plan.json) result files)"

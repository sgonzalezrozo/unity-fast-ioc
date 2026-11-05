# Benchmark evidence format

All benchmark numbers published for this package must be backed by raw JSON files that follow `benchmark-result.schema.json`.

## Required fields

- `tool`: package identifier (`"fast-ioc"` for this package)
- `schemaVersion`: schema version (currently `"1.0.0"`)
- `timestampUtc`: ISO-8601 UTC timestamp
- `session`: environment metadata (device, OS, Unity, build mode, backend, commit)
- `workload`: benchmark target metadata (profile/scenario, variant, run index, warmup frames, measured frames)
- `metrics`: numeric measurements by name and unit

## Summary rules

- Publish medians and p95 from repeated runs per profile/variant.
- Keep raw run JSON files in-repo and reference them from any README claim.
- Include unfavorable results; do not drop outliers silently.

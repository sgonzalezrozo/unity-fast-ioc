# AI Coding Agents

[← Back to README](../README.md)

Coding agents follow the patterns they find in a project. Without instructions, they tend to share services in Unity through singletons, `FindObjectOfType` and Inspector references. Give them the Fast IoC rules and they wire dependencies the same way you do:

1. Copy [`agent-rules.md`](agent-rules.md) into your project's agent rules, for example `AGENTS.md`, `CLAUDE.md` or `.cursor/rules/fast-ioc.md`.
2. Keep the generated folder committed, so the agent can read which bindings exist.

Fast IoC suits agents for a few reasons:

- **Dependencies are visible in code.** A constructor or an `internal` field shows what a class needs, without opening scenes or prefabs.
- **The agent can check its own work.** After a compile, `Assets/FastIoC/Generated/GenerationStatus.json` reports missing bindings and dependency cycles, and generation can run in batch mode. See [Run it in CI](code-generation.md#run-it-in-ci).
- **Each feature owns its installer.** An agent adds a feature without editing a shared file.
- **Plain classes are easy to test.** Pass fakes to the constructor in a unit test.

There are no MCP tools for Fast IoC yet. The rules file works with any agent that reads project files.

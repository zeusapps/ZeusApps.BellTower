# Implementation 069 (DEV-069): ANALYSIS-5900

Awaiting independent verification.

- `HookDeliveryReporter.LiveOwnedCommands(provider)` exposes the owned commands present in the provider settings (manifest only filters ownership). `Provider()` and the ingress image reporter now share one bounded parse (`ConfiguredHooks`).
- `HookIngressImageReporter` evaluates those live commands for Claude Code and Codex alike; unreadable, oversized or malformed settings, or no owned command present, give `unknown`.
- `docs/installation.md`: comparison described as the owned hook commands present in the provider settings; states `hookStart.ingressImage` is authoritative for the image hash and `confirmationCoverage.*.ingressImage` reports the latest capture's producer shape. `docs/compatibility.md` does not describe the comparison (unchanged).
- Tests (`HookIngressImageReporterTests`): manifest [Older, Selected] with settings holding only Selected is current and available, with Older is stale, for both providers and each Codex shell form; missing settings, no owned command, malformed and oversized settings are unknown.
- Not done: reuse of installation resolution (optional).
- Build note: the repository `bin` output is locked by a running bell-tower process, so build and tests ran on a scratchpad copy of the tree (Release, 0 warnings; focused filter 71 passed). The working-tree Release build could not be run for that reason. Format verify on changed .cs passed; `openspec validate --strict` valid.

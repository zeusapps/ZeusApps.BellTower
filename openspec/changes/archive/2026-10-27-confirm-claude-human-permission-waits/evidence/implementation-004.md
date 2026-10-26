# Implementation 004: confirm-claude-human-permission-waits (targeted fix)

Developer handoff against baseline `754aed5`. Nothing is committed. This fixes `ANALYSIS-002`; it does not close the finding, which awaits independent QA and code review. Tests use the fake Codex runtime and isolated `TestWorkspace` data. No live provider session, installed app, audio, user settings or default data directory was touched.

## Fix

The Codex pump captures the opaque identity set and `ApprovalRevision` for permission waits at query start. `SessionState.ApprovalRevision` advances only when the permission wait identity set changes, and causal replay copies the value. Core accepts a runtime confirmation only when both the captured set and revision match. The pump applies the same revision guard to transient positive and negative status, so A→A+B→A cannot revive the original cached A result. Unrelated events do not advance the revision.

Each query receives a new observation ID. A persisted result retains that ID for inbox replay and retry deduplication; a later query at the same wait scope gets a distinct ID, so a stale result consumed while A+B cannot poison a valid fresh A result after B resolves. Missing scope or revision on older queued runtime observations is treated as unverified and cannot confirm. The two metadata fields are additive in the internal Observation JSON; capture protocol remains version 1. Wait identities remain opaque and contain no raw tool metadata.

The real pump/StateStore regression delays a positive read for A, adds B before the result arrives, consumes and rejects that result against A+B, resolves B, verifies the cached result stays stale against A, then confirms A through a fresh query. It runs on both causal and unordered paths. Reducer tests cover exact scope and legacy metadata; capture tests cover additive roundtrip and older observations. Existing tests cover unchanged-set confirmation, multiple waits, already-confirmed wait protection, question coexistence, stop/end/turn/attachment behavior, and silent long-journal restore.

## Changed paths owned by this fix

- `src/BellTower/Core/Observation.cs`
- `src/BellTower/Core/SessionState.cs`
- `src/BellTower/Core/SessionReducer.cs`
- `src/BellTower/Core/CausalSessionReducer.cs`
- `src/BellTower/Core/WaitState.cs`
- `src/BellTower/Sessions/CodexPermissionPump.cs`
- `tests/BellTower.Tests/Core/HumanWaitCorrectionTests.cs`
- `tests/BellTower.Tests/Providers/CodexPermissionTests.cs`
- `tests/BellTower.Tests/Providers/CaptureTests.cs`
- `docs/architecture.md`
- `docs/hook-ingress.md`

## Checks

- `dotnet build -c Release --no-restore` — passed, zero warnings and errors.
- Targeted `CodexPermissionTests`, `HumanWaitCorrectionTests`, `CaptureTests` — passed, 95/95.
- Scoped `dotnet format ZeusApps.BellTower.slnx --no-restore --include ...` — passed for the changed C# files.
- Full suite — not run by developer; QA owns the full-suite check.
- Native/live approval flow and packaged installation — not run.

The scoped test host stalled in the sandbox; the same targeted command completed successfully outside it. One initial run exposed and fixed an `else-if` placement regression and an over-aged fixture timestamp; the final build and targeted run above are after those corrections.

## Review identity

`./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs README.md`

`head=754aed5 baseline=754aed5 files=62 diff=1ed64cece66a untracked=15/a39eeb1f80e7`

The manifest includes concurrent work outside this fix. Review only after the orchestrator records its frozen target; rerun the identity command at final handoff if the shared target changes.

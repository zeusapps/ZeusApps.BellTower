# DEV-067 evidence (task 4.12, ANALYSIS-5600..5602)

Scope: Codex managed daemon (`app-server` with `--managed-daemon` before `--`) classifies `shared-backend` regardless of its parent.

- Adapter: `AgentModeClassifier.LauncherIndependentBackend(provider, arguments)`; true only for Codex managed-daemon shape. False for every Claude line, the owned runtime (`--listen ws://... --ws-auth`), bare/stdio `app-server`, `app-server daemon pid-update-loop`, `app-server -- --managed-daemon`, `exec --managed-daemon`.
- Core: `NativeAdoption.Needs/CrossCheck` take `launcherIndependent` (default false); such a backend needs no relationship and the relations delegate is not called. All other shapes keep R1-R3.
- Capture: `AttachmentCapture` passes the fact from the arguments it already read (no extra process or file read).
- Transport: `CodexBackendCheck` keeps its `Classify == shared-backend` gate (managed lines are a subset), so it is not stricter; tests pin the 0.172.0 and 0.166.0 lines in both capture and transport.
- Store: existing `AttachmentEvidence.Merge`; tests show stored unknown adopts shared-backend and a known mode is never downgraded.
- Text: CapabilityMatrix row, docs/compatibility.md rows (shared conversations not yet reached, gaps list). DiagnosticsReporter carries no such statement.
- Tests: ModeCrossCheckTests (predicate rows, Core rows, parity, store merge, transport agreement), NativeAgentRelationshipProcessTests (9 nested rows under both entries for representatives: non-Codex launcher, updater launcher, backend/headless parent, owned shape, flag after `--`, Claude; real orphan rows; mixed simultaneous with orphan + `--no-daemon` + Claude + nested rows; consumers NavigationReasons/IsPermanentLimit/CodexRuntimeOpener.Select asserted for every shared outcome), NativeOwnedRuntimeProcessTests (owned ws shape stays record-bound), CodexSharedAccessTests, CapabilityMatrixTests.
- Test hardening: the orphan helper kills only its own image folder so concurrent stand-ins of the mixed test are untouched.
- Checks: Release build 0 warnings; focused areas passed (585); NativeAgentRelationshipProcess 46 passed repeatedly; dotnet format verify clean on changed files; openspec validate --strict valid. A full-suite run showed load-related failures in unrelated tests (audio, strip rendering, bounded state, footer setup, access-denied starts of fresh stand-ins) that pass in isolation; not caused by this change.
- Not performed: live acceptance on a real orphaned backend (task 4.11/6.1, needs the next dev build).

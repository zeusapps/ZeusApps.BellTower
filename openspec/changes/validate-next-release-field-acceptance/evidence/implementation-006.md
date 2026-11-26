# Implementation 006: tasks 2.2 and 2.3 (DEV-006)

Harness, tests, script and docs only; no product code, planning file, report, ledger or version declaration changed. Carries ANALYSIS-142. Nothing here is closed or approved by the developer.

- Baseline `02d4f56`. Client / model: Claude Code, `claude-sonnet-5-5`.
- Committable run summaries: `harness-2.2.md` (63 of 63 checks passed) and `harness-2.3.md` (28 of 29 passed; the Codex median gate failed, reported as it is).

## What changed

- `tools/Shared/HookTiming.cs` (new): the one hook timing implementation (sample records, nearest-rank percentile, `Summarize`, the batch plan and the alternating-leg rule), compiled into both `tools/BellTower.Ingress.Bench` (which lost its local copy and delegates; its JSON fields, console lines and CI behavior are unchanged) and the harness.
- Thread safety on the load path: `Instance.CommandLog` is a `LockedList`; `ProcessResult` carries the agent's own in-agent milliseconds; `AgentSession.Hook` and `Instance.Run` document the parallel contract. `Instance.WithProcessEnvironment` stays single-threaded by contract (documented).
- ANALYSIS-142: the load scenario runs hooks through `AgentSession.Hook` only, so the existing before/after `HookTrayStart.LastAttempt` observation applies. Not moved into `Instance.Run`; the contract is documented in `Instance.Run`, `AgentSession.Hook`, `docs/distribution-validation.md` and `docs/development.md`.
- New harness pieces: `SandboxGit` (real Git through the already allowed Git Bash), `TranscriptModel` (labeled modeled transcripts, pinned to the test fixture builders by a test), `ConversationSurfaces` (reads badge, tab, chip inputs and speech), `CaptureObserver` (inbox watcher plus poll), `MachineLoad`, `ProviderModel` additions (`source` for resume, `ShellToolPayload` as a labeled model, `TranscriptPath`), `CandidateInstall`, `RunReport.Measurements` (raw data to `result.json` only), and an invariant culture for the harness.
- Scenarios `five-conversations` (2.2) and `packaged-hook-benchmark` (2.3), registered in `ScenarioCatalog`. The 2.1 scenario is untouched (its 61 checks still pass in `--scenario all`).
- Tests: `tests/BellTower.Tests/FieldAcceptance/LoadScenarioTests.cs` (11 new; 78 FieldAcceptance tests in all).

## Findings worth the orchestrator's attention

1. Codex median gate fails on this machine. Standalone runs: median 359.9 ms (first try) and 354.3 ms (committed `harness-2.3.md`); inside `--scenario all` 283.9 ms. Claude 89 to 138 ms; mixed 178 to 240 ms. The `echo 1` control through `pwsh.exe` runs slower (median about 512 ms) than the hook command (about 354 ms), so the number is dominated by PowerShell 7 startup, not by the ingress. Not tuned. The gate is near the limit and run-dependent; it needs the field machine or a repeat before 2.3 can be called passed.
2. Codex explicit native names are not observable and the product does not promote them: `NativeNamePolicy.Pause` (no `NativeTitle` write for Codex anywhere in `src/`). The Codex row verifies the shared tiers only; recorded as unfinished parity (ledger F7), not as a restriction, per the parity rule.
3. After a branch change the invalidated conversation shows the launch checkout's name (`main`), because with no work the checkout falls back to the launch directory. Several conversations in one launch directory then share that name. This is the documented last tier, so I did not call it a defect; the scenario requires distinctness of every other tier and states the fallback in the check.
4. A bug in my own fixture, found and fixed: OpenSpec change names must be lowercase kebab-case; uppercase ticket names were silently ignored by history recovery.

No product defect found.

## Not performed (stated in the run notes)

Real terminal tab write; rendering a chip (needs a pending approval; tasks 2.4 and 2.7); audio (silent sink records nothing); Codex native names (above); live authenticated provider runs. Transcripts and the `PostToolUse` inspection payloads are labeled models.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build -c Release --no-restore` | passed, 0 warnings, 0 errors |
| `dotnet test ... --filter FullyQualifiedName~FieldAcceptance` | passed, 78 of 78 |
| Full `dotnet test -c Release --no-build --no-restore` | see the return block |
| `dotnet format --verify-no-changes --include <changed .cs files>` | passed, exit 0 (after the mutating run on the same files) |
| `five-conversations` full run | 63 of 63 (including the 11 guard self-test checks and the isolation check) |
| `packaged-hook-benchmark` full run | 28 of 29; `codex/median-below-300ms` failed |
| `--scenario all` | 129 of 129 passed (the Codex median happened to pass at 283.9 ms) |

Machine load during the committed 2.3 run: 6 to 10% of 24 logical processors busy, 365 to 413 processes, classified idle.

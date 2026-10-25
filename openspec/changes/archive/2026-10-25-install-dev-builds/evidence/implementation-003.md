# Implementation 003: install-dev-builds

Developer handoff for finding fixes QA-001, ANALYSIS-001, QA-005, ANALYSIS-002 documentation, and task 4.3. The baseline is `b74a12b`; no commit was made.

## Target identity

`head=b74a12b baseline=b74a12b files=27 diff=5b2efc8567ad untracked=10/3e9c4782a768` (`scripts/review-identity.ps1 -Baseline b74a12b` over `src`, `tests`, `scripts`, the three scoped docs, and both skill folders).

## Changes

- `UpdateTransaction.Cleanup` and `Retire` serialize with update operations but no longer hold the launcher gate through payload scans, hashes or deletion. Removal rechecks selection and previous under the launcher gate, acquires an exclusive payload lease there, then verifies and removes the payload after releasing the launcher gate. The lease protects in-progress removal from launched processes.
- `install-dev.ps1` uses the update gate and shared `EnsureNoCoordinator` policy for Switch/Rollback. It refuses active and interrupted update plans before tray stop or selection change. Candidate copy and verification happen outside the data and launcher gates. Pointer verification and replacement use a short launcher-gate section; tray waits remain outside it. The update gate is released before the tray restart.
- Disposable-only `-TestBarrierDirectory` provides deterministic slow phases after Switch payload verification and after Rollback snapshot restoration, before pointer replacement. It is rejected unless `-Disposable` is also supplied and every path is under `artifacts/`.
- `PayloadRemovalTestCheckpoint` is an internal test-only callback, accessible to `BellTower.Tests` through `InternalsVisibleTo`; production users cannot configure it through environment variables or script arguments.
- The new `HookMaintenanceTests` creates a disposable installation with the built stable launcher and payload, blocks Cleanup while it verifies a removable payload, then runs generated launcher-based legacy hook commands for Claude, Codex and mixed sessions. The hooks finish within their three-second limit while Cleanup remains blocked, and durable events are present. The shared C# Retire path uses the same checkpoint and removal predicate.
- Installer tests cover active and interrupted pending plans for both Switch and Rollback, assert recovery metadata and selection remain unchanged, and assert no tray exit request was written. The earlier owner-drift test fixture now serializes its changed JSON correctly and verifies the detailed refusal.
- Docs and the two byte-identical skills now require a dev marker for Retire, explain automatic cleanup of unmarked legacy versions, and state that rollback restores one selection level.

No change was made to the stable launcher or hook deadlines. The historical 0.1.8 startup delay and the user's work-machine timeout were not reproduced; generated-shell trials establish behavior for the disposable installations only.

## Checks

| Check | Result |
|---|---|
| `dotnet format ZeusApps.BellTower.slnx --no-restore --include src/BellTower/BellTower.csproj src/BellTower/Updates/UpdateTransaction.cs tests/BellTower.Tests/Updates/PayloadRetentionTests.cs tests/BellTower.Tests/Updates/HookMaintenanceTests.cs tests/BellTower.Tests/Scripts/InstallDevScriptTests.cs` | passed |
| `dotnet build -c Release --no-restore` | passed, 0 warnings and 0 errors |
| `dotnet test -c Release --no-build --no-restore --filter "FullyQualifiedName~PayloadRetentionTests|FullyQualifiedName~InstallDevScriptTests|FullyQualifiedName~HookMaintenanceTests"` | passed, 26/26 |
| Hook maintenance trial | passed in the focused suite: four generated PowerShell launcher hook invocations (Claude, Codex, and two mixed-session events) completed before releasing the Cleanup verification barrier; all four were durable and the leased/verified target remained present during the barrier |

## QA execution entry points

For deterministic disposable Switch/Rollback trials, pass `-TestBarrierDirectory <directory-under-artifacts>` along with the script's existing disposable path arguments. The script writes `switch-before-selection.entered` after copying/verifying the candidate and `rollback-before-selection.entered` after restoring the monitoring snapshot. While each process is waiting, run the generated launcher commands from `HookCommandBuilder.Build("event", "claude")` and `Build("event", "codex")`, including a mixed concurrent pair. Release the phase by creating the matching `.release` file in that barrier directory. For example, create `switch-before-selection.release` to resume Switch. The marker is test-only; do not use it against the maintainer installation.

The automated Cleanup/Retire checkpoint trial is `HookMaintenanceTests.CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks`; it uses the actual stable launcher and verifies durable events. Run it with:

```text
dotnet test -c Release --no-build --no-restore --filter FullyQualifiedName~HookMaintenanceTests
```

## Remaining acceptance

- QA owns the full test suite, final native package, read-only maintainer Preflight, complete disposable Switch/Rollback/Retire cycle, and final packaged NativeAOT ingress benchmark.
- QA should exercise the script Switch and Rollback barriers with actual generated provider commands, plus Retire while its shared removal checkpoint is blocked. The field report and 0.1.8 startup measurements remain distinct from these generated-shell trials.
- JavaScript tests and whole-target review remain for acceptance. No maintainer installation mutation, live Switch, settings edit, commit or publication was performed.

```
status: done
role / client / model: developer / Codex CLI 0.164.0 / gpt-6-luna
task / finding IDs: 4.3; QA-001; ANALYSIS-001; QA-005; ANALYSIS-002 documentation
changed or report paths: source, test, installer script, docs, both skills; openspec/changes/install-dev-builds/evidence/implementation-003.md
checks: scoped dotnet format -> passed; dotnet build -c Release --no-restore -> passed; focused test filter -> passed 26/26
target identity: head=b74a12b baseline=b74a12b files=27 diff=5b2efc8567ad untracked=10/3e9c4782a768
limitations / decisions needed: QA owns native package/Preflight/cycle/benchmark and generated hooks during Switch/Rollback/Retire barriers; user-field failure cause remains unverified
next: QA and code-analyzer review the stable implementation; QA records remaining acceptance evidence
```

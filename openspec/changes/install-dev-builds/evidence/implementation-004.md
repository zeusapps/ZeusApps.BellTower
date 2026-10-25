# Implementation handoff: install-dev-builds / 004

## Scope and identity

- Developer round: canonical ANALYSIS-150 and QA-150, plus the approved delayed-start fence and matching regressions.
- Baseline: `b74a12ba20eb9d3595204b41295b9dab73b7895d`.
- Final scoped identity: `head=b74a12b baseline=b74a12b files=27 diff=41ba2dbe9660 untracked=10/88d345b46144`.
- Identity command: `./scripts/review-identity.ps1 -Baseline b74a12b -Paths src tests scripts docs/development.md docs/releases.md docs/installation.md .claude/skills/install-dev-build .agents/skills/install-dev-build`.
- Claude and Codex skill copies have matching SHA-256 `c9ada458ca27b48897054ed517f9e9a756429b080c5cb9835a691e4583474190`.

## Changes

- `scripts/install-dev.ps1` now creates exclusive operation-owned pending and restart records and retains the named `tray-instance` object without owning its mutex. This reservation remains across slow Switch/Rollback work. The updater releases its update gate and the reservation before starting the authorized tray, allowing startup cleanup to take the update gate.
- The existing recorded tray is still the only process asked to exit. Exit verification uses its PID and exact `ProcessProbe` start-time identity, not `TrayController.IsRunning`, which reports the reservation as an existing object.
- A bounded drain checks local `Win32_Process` records (five-second CIM operation timeout; 30-second total drain deadline) for tray invocations whose live image is under this installation's versioned payloads. It compares the live PID image with CIM, parses the command and data directory in memory, checks PID/start-time identity, and revalidates the exact `ProcessProbe` tuple. Raw command lines are never logged. Unknown identity, data root or a child that does not exit fails closed before selection mutation; no process is killed.
- A live CIM comparison found its `CreationDate` timestamp differed from `Process.StartTime` by 9 ticks (0.9 microseconds). CIM time is used only as a one-microsecond correlation check; the exact `ProcessProbe` PID/start-time tuple remains authoritative for subsequent checks.
- Markers are renewed and matching tray starts drained after slow preparation and immediately before reservation release. Rollback recovery accepts only a manifest-matching ID/token, installation root, data root, original/target versions and an ended prior owner; both pending and restart records must match. Marker drift preserves the other record's bytes. `UpdateTransaction.EnsureNoCoordinator` keeps its original behavior for ordinary updater callers.
- Candidate restart carries the owned activation ID through the stable launcher. Only after the tray is verified are matching operation records removed. Post-selection failures retain recovery records.
- Documentation and both byte-identical skills describe the fence, safe refusal and manifest-bound recovery.
- Regression tests cover foreign restart-marker byte preservation and matching dead-owner Rollback recovery. Existing installer cases continue covering successful Switch/Rollback, selection drift and pending update refusal.

## Checks run

- `dotnet build -c Release --no-restore` -> passed before the final recovery overload was added; the later focused `dotnet test` builds recompiled the final C# changes successfully.
- `dotnet test -c Release --no-restore --filter FullyQualifiedName~InstallDevScriptTests` -> passed, 23/23, including the new marker refusal and recovery tests.
- Final affected regressions after the process-image and CIM precision checks: Switch/Rollback success, foreign-marker refusal, and matching recovery -> passed, 3/3.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include src/BellTower/Updates/UpdateTransaction.cs tests/BellTower.Tests/Scripts/InstallDevScriptTests.cs` -> passed.
- PowerShell parser check for `scripts/install-dev.ps1` -> passed.
- `git diff --check` -> passed. Existing line-ending warnings referenced unrelated `docs/releases.md` and `src/BellTower/BellTower.csproj`; neither was formatted by this assignment.

## QA handoff still required

Source is frozen at the identity above. QA should now execute its prepared disposable delayed-child fixture against the unchanged barrier names `switch-before-selection` and `rollback-before-selection`, including old-order `CanStartTray` success followed by pause before mutex creation. Verify it exits under the reservation before pointer/state mutation, foreign/expired/unknown marker refusal and preservation, matching recovery, and successful authorized restart. Use the QA hook scripts to prove legacy Claude, Codex and mixed hooks plus native captures complete while slow phases remain blocked. Full coverage still needs Switch, Rollback, Cleanup and Retire slow-phase trials, full test suite/formatter verification, final native package freshness, read-only maintainer Preflight, disposable Switch/Rollback/Retire cycle, and the ingress benchmark. The source-level and fixture results do not establish the user's work-machine timing cause or a live provider-settings migration; installing/packaging does not itself migrate hooks to NativeAOT.

No maintainer installation, user tray, provider process or personal provider settings were used or changed. No branch or commit was created.

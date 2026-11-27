# Implementation 003 (DEV-003): task 2.1, installed field acceptance harness

Developer handoff, 2026-11-25, baseline `02d4f56`. Delivers the foundation of the installed field acceptance harness and the upgrade-without-setup scenario (task 2.1, ledger row U1). No checkbox, planning artifact, handoff, spec, version declaration or product (`src/`) file was changed. Nothing was committed. Awaiting independent QA and code-analyzer review.

## What was built

- **Entry point.** `scripts/field-acceptance.ps1` (default scenario `upgrade-without-setup`). It downloads and describes the published baseline, builds the fixture-version candidate on demand, builds the harness and runs a scenario. Run results go to the gitignored `artifacts/field-acceptance/runs/<timestamp>/` (`result.json`, `summary.md`); the baseline lives in `artifacts/field-acceptance/baseline/` with `provenance.json`; the candidate stays under `artifacts/update-acceptance/packages/`. `-EvidenceOut` writes the sanitized committable summary (`evidence/harness-2.1.md`).
- **Tool choice.** A new sibling project `tools/BellTower.FieldAcceptance` (added to `ZeusApps.BellTower.slnx`, referenced by the test project) instead of extending `tools/BellTower.Smoke`. Smoke is one linear, fixed-argument scenario that CI runs; the new tool is scenario-based (`IScenario` in `ScenarioCatalog`, built on `Instance`: install, data, fake home, fake providers, guarded process runner), so tasks 2.2 to 2.7 add scenarios without touching Smoke or the CI behavior of `scripts/update-acceptance.ps1`. Neither `scripts/update-acceptance.ps1` nor `scripts/package.ps1` was changed.
- **Fake providers.** `claude.exe` and `codex.exe` are copies of the harness executable. A fake session stays running and runs each installed hook command as its child (Git Bash for Claude, PowerShell or the command shell for Codex), so hooks see real ancestry; `codex exec` answers the setup hook probe through PowerShell. No real provider binary or credential is used. The existing Smoke fixture agents and `tests/BellTower.Tests.FakeLauncher` were not reused: Smoke's agent is a `cmd.exe` copy without a long-lived session, and the fake launcher stands in for the tray, which this scenario needs to be real.
- **Tests.** `tests/BellTower.Tests/FieldAcceptance` (28 tests): the production-path guard (layout, environment, command and the self-test), the before/after production snapshot and diff, the evidence sanitizer and the report. They use invented production locations.
- **Docs.** `docs/distribution-validation.md` ("Installed field acceptance harness") and a pointer in `docs/development.md`.

## Acceptance mapping

1. **Real baseline.** `gh release download v0.1.9` into `artifacts/field-acceptance/baseline/`. The harness recomputes the SHA-256 and requires it to equal the published `.sha256`, the release descriptor `archiveHash` and the GitHub asset digest (all three matched: `2739659e...f062`). Provenance is in the result and the evidence. Never rebuilt.
2. **Isolation.** Per-instance install, data, home, temp and fake-bin; allow-listed child environment; `ProductionGuard` on the layout, environment and every command; a guard self-test against the real production locations at the start of every run; before/after fingerprint of setup-owned production files, the whole production installation tree, the Bell Tower scheduled tasks and the running production Bell Tower processes (any gating difference fails the run). A run directory inside production is refused before anything is created (checked with `--run-dir` under the real data directory: exit 2, nothing created).
3. **Baseline seeded by 0.1.9.** 0.1.9 `setup --clients both --apply`, 0.1.9 tray, hooks through the 0.1.9 ingress for two sessions per provider in one launch directory, 0.1.9 `rename` for a name, change, checkout and color pin on one session per provider; four identity stores with four distinct palette accents.
4. **Upgrade without setup, then with setup.** The real update transaction (`UpdateTransaction.Stage`/`Start`) from the published 0.1.9 to the 0.1.11 fixture-version candidate with the tray stopped; settings byte-identical; the 0.1.9 image file retained. Then `setup --host-bridge --apply` inside the sandbox home. Both states are checked per provider and mixed (see the table).
5. **Outputs.** JSON and Markdown under the gitignored run directory; sanitized `evidence/harness-2.1.md` (all names, home and repository paths replaced by placeholders; the harness refuses to write it if a private value remains).
6. **Docs.** Above.

## Results (final run, 56 of 56 checks)

| Check (stale = upgraded, setup not rerun; current = after `setup --host-bridge --apply`) | Claude Code | Codex | Mixed |
| --- | --- | --- | --- |
| baseline setup, seed, identity stores by 0.1.9 | pass | pass | pass |
| stale: doctor `hookStart.ingressImage` `stale` + advice | pass | pass | availability "needs a setup refresh": pass |
| stale: 0.1.9-image captures accepted | 7 of 7 | 7 of 7 (command shell) | 14 of 14, 6 sessions listed |
| stale: baseline data preserved | pass | pass | pass |
| stale: native adoption inactive | mode `unknown` | `shared-runtime`, no process | both listed |
| stale: hook-started tray inactive | pass | pass | |
| stale: repaired Codex command inactive | | 0.1.9 command fails under PowerShell | |
| current: doctor `current`, no advice | pass | pass | availability "available": pass |
| current: captures accepted | 7 of 7 | 7 of 7 (PowerShell) | 14 of 14 |
| current: baseline data preserved | pass | pass | pass |
| current: native adoption active | `interactive-client` | `embedded-runtime`, verified-live | both listed |
| current: hook-started tray active | pass | pass | |
| current: PowerShell-safe Codex command | | pass | |
| production unchanged | | | pass (87 files, tasks, processes) |

Inactive hook-side features in the stale state, each measured against the same probe in the current state: native adoption of plain `claude` and `codex --no-daemon` (attachment mode and ownership), the hook-started tray (stopped tray stays stopped, no start attempt), and the repaired Codex hook command (the 0.1.9 form only parses in the command shell).

## Findings to carry (not fixed)

- The 0.1.9 Codex hook command fails under PowerShell (parser error). That is the known defect repaired after 0.1.9, now reproduced on an upgraded install: until the user reruns setup, Codex hooks still fail where Codex runs hooks through PowerShell, which the archived native evidence says is the Windows default. Release notes for task 4.4 should say the setup rerun is required for Codex to deliver hooks at all, not only for new features. The harness's choice of the command shell for the stale-state Codex captures models a Codex that falls back to cmd; it does not claim Codex does so.
- The harness models Codex's hook shell from archived native evidence (PowerShell default, cmd fallback). It does not exercise Codex's `/hooks` trust prompt.
- `setup --host-bridge` could not exercise the VS Code focus-bridge extension install: the sandbox profile has no VS Code (the child `USERPROFILE` redirect also moves the product's known-folder lookups, which is useful isolation but means the bridge is not tested here). Tasks 2.6 and 4.1 own it.
- `setup` registers an on-demand tray-start task per data directory in the user's Task Scheduler (outside any sandbox directory). The harness deletes it at cleanup and fails the run if another Bell Tower task changed or the sandbox task remains; it is the one effect outside the sandbox directories.
- Trays started by the harness are silent: `--silent`, a muted configuration and update checks disabled; the hook-started tray (which does not take `--silent`) runs under the same muted configuration. The hook-start probe clears the user-stop record the `exit` command writes, to model a tray that is simply not running; this is a labeled fixture manipulation inside the sandbox.

## Checks

- `dotnet build -c Release --no-restore` -> passed, 0 warnings, 0 errors.
- `dotnet test -c Release --no-build --no-restore` -> second full run passed (2134 of 2134). The first full run reported 2 failures in `CodexSharedTransport` WebSocket connection (stack in `CodexConnection.ConnectAsync`) that I could not name or reproduce: the Codex-filtered subset (229) and the whole suite passed on rerun with no change. Not attributed to this work; reported, not investigated further.
- `dotnet test ... --filter FullyQualifiedName~FieldAcceptance` -> 28 passed.
- `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <changed .cs files>` -> passed (the formatter was applied first).
- `scripts/field-acceptance.ps1 -EvidenceOut ...` -> 56 of 56 checks passed; no production change observed.
- Node tests and OpenSpec validation were not run (no `vscode/` or OpenSpec file changed).

## Notes for reviewers

- New untracked files: `tools/BellTower.FieldAcceptance/**` (with `packages.lock.json`), `scripts/field-acceptance.ps1`, `tests/BellTower.Tests/FieldAcceptance/**`, `evidence/harness-2.1.md`, this file. Modified: `ZeusApps.BellTower.slnx`, `tests/BellTower.Tests/BellTower.Tests.csproj` and its `packages.lock.json` (project reference), `docs/development.md`, `docs/distribution-validation.md`.
- Heavy runtime: a run takes about 70 seconds after the packages exist and uses about 110 MB of disk per extracted install before cleanup.

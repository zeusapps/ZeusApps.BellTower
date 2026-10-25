# Shutdown checkpoint — install-dev-builds

Saved 2026-10-24 22:16:30 UTC (2026-10-25 in Europe/Warsaw). User requested laptop shutdown and then: "i still see timeouts. please wrap it up". Implementation and QA preparation are stopped. No automatic continuation is authorized until the user resumes.

## Current result

- **Incomplete; not ready to install.** Tasks remain 9/13 checked. Open: 4.1, 4.3, 6.1, 6.3.
- The live installation/provider settings were not changed. Existing live timeouts are not claimed fixed. Maintainer Preflight was read-only and preserved selection, launcher, all 24 payload files and tray PID/start identity.
- NativeAOT benchmark on diagnostic package 0.1.9003 passed: 242 native captures and 50 legacy events durable; replay passed; native Claude median 265.6 ms/max 307.9 ms, Codex median 186.3/max 218.7 ms. Native ingress under a held launcher gate passed 4/4. Legacy eight-way controls exceeded three seconds in 47/50 samples. These are isolated generated-shell trials, not a field reproduction or live migration.
- Latest full suite before partial round 005: 754 passed/3 failed of 757. Font and existing legacy CLI tests passed alone; the new maintenance-hook test reproduced a lease teardown failure alone, masking the primary hook outcome.

## Exact saved source

Baseline/HEAD `b74a12ba20eb9d3595204b41295b9dab73b7895d`, branch `add-claude-repository-subagents`; no commits, stash, reverts or branch changes.

`head=b74a12b baseline=b74a12b files=30 diff=a3393fb5519a untracked=13/5382df1f9140`

Identity command: `./scripts/review-identity.ps1 -Baseline b74a12b -Paths src tests scripts docs/development.md docs/releases.md docs/installation.md .claude/skills/install-dev-build .agents/skills/install-dev-build`.

The unrelated staged file `openspec/changes/restore-claude-badge-identity/reports/findings.md` remains at index blob `ef25d4af1debef6c35d1c2d626ddd15952a9276d`.

## Partial Implementation 005 — unbuilt/unverified

User explicitly approved **one additional focused fix/review round** after the two-cycle workflow limit. That approval persists for resumption; it is not a completed round.

Developer stopped after partial edits to `scripts/install-dev.ps1` and `src/BellTower/Updates/UpdateTransaction.cs`, plus new:

- `src/BellTower/IO/WindowsCommandLine.cs`
- `src/BellTower/IO/TrayInvocationCommand.cs`
- `tests/BellTower.Tests/IO/TrayInvocationCommandTests.cs`

Parser/classifier and tests are added but not built or connected end-to-end. Script contains initial owner/process-scan and marker/bootstrap edits. **Known likely partial-code bug:** `$activationId` is assigned only in the new-operation branch but a later common recovery path still references it. Do not treat this tree as a stable handoff.

Still required: correct pre-mutation boundary placement, full both-record binding and phase/idempotent recovery, stale-presence matrix, both-direction activation/lineage verification, HookMaintenance exception-safe release/join/reap, focused tests and a real disposable native restart smoke. No round-005 tests/build/native smoke ran.

## Resume instructions

1. Read `reports/findings.md`, immutable `reports/qa-004.md` and `reports/analysis-004.md`, and **`evidence/analysis-004-fix-plan.md`** (complete corrective matrix).
2. Resume delegated developer Implementation 005 against this partial identity; preserve all unrelated work. Fix canonical ANALYSIS-200/201/202 and QA-200. QA-201/202/203 duplicate the canonical source findings. QA-204 eight-way legacy stress remains a measured follow-up; controlled legacy maintenance cases retain their three-second deadline and native benchmark gates remain mandatory.
3. Freeze a tested handoff with identity before independent QA 005 and Analysis 005. Reserved reports: `reports/qa-005.md`, `reports/analysis-005.md`; new IDs QA-250–299 and ANALYSIS-250–299; evidence prefixes `qa-005-*`, `analysis-005-*`. Do not reuse historical report/ID allocations.
4. Complete actual delayed-child, both recovery phases, semantic/byte marker drift, expired/unknown-start refusal, stopped-tray presence, all four maintenance phases with individual and mixed-provider hooks, full native lifecycle, clean full suite, final package/benchmark and read-only maintainer Preflight. Distinguish actual old 0.1.8 legacy/no-fast compatibility from capture-1 source-version fixtures for native mixed rollback. No live Switch or integration apply is authorized.
5. Only after completion criteria hold: reconcile tasks/findings, commit exact scoped work excluding unrelated staged edits. Archive/push/live installation are separate actions. A post-commit package must match new HEAD because Preflight rejects old source commits.

## QA 005 preparation saved

Prepared, not accepted: `evidence/qa-005-delayed-child/`, `qa-005-fixture.ps1`, `qa-005-prepare-hooks.ps1`, `qa-005-hook-trial.ps1`, `qa-005-delayed-child.ps1`. Compiled simulator: `artifacts/qa-005-fixtures/delayed-child-publish4/bell-tower.exe`; it uses real tray argv forms and env/file pause control. It is not the released 0.1.8 binary or a consumer-compatibility substitute.

All specialists stopped. No QA 005 acceptance or fixture child was started; no owned native trial process needs shutdown. Existing user tray/sessions remain untouched. Sandbox process setup often fails; approved `require_escalated` execution worked. Do not repeat failing default-shell attempts or infer an approval from elapsed time.

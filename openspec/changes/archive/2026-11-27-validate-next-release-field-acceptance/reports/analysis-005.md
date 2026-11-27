> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125). The reviewer did not attempt the write: its session instructions forbid report files, and the ANALYSIS-004 write had been refused with "Subagents should return findings as text". Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).
> Reviewer: ANALYSIS-005 code-analyzer subagent; client Claude Code (Agent SDK subagent, auto mode; version not re-probed, ANALYSIS-004 recorded 2.1.332); model claude-opus-5-5.

# Analysis report: validate-next-release-field-acceptance / 005

## Review target

- Reviewer / role: ANALYSIS-005 subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code, Agent SDK subagent, auto mode. The version was not re-probed this round; ANALYSIS-004 recorded 2.1.332. Model `claude-opus-5-5`.
- Date: 2026-11-25T09:01Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-005. This is a light recheck of ANALYSIS-060 and -061 after the orchestrator's planning fix (option (i)), plus a coherence regression check. It covers correctness, coherence and safety only.
- Task and requirement references: tasks 2.1, 2.4, 3.3, 3.4, 4.4 and 4.5 in [tasks.md](../tasks.md); [design.md](../design.md) section 1; [proposal.md](../proposal.md) execution dependencies; the 2026-11-25 entries in [findings.md](findings.md)
- Scope and relevant neighboring paths: working-tree diff of `tasks.md`, `design.md`, `proposal.md` and `docs/handoffs/next-release-plan.md` (rows 6 and 7). Read-only neighbors at `02d4f56`: `scripts/package.ps1`, `scripts/update-acceptance.ps1`, `tools/BellTower.Smoke/Program.cs`, `src/BellTower/Updates/UpdateTransaction.cs`, `.github/workflows/validate.yml`, `.github/workflows/release.yml`, `evidence/implementation-001.md`
- Baseline commit: `02d4f56`
- Implementation identity: `scripts/review-identity.ps1 -Baseline 02d4f56 -Paths Directory.Build.props README.md vscode src scripts docs tests tools openspec/changes/validate-next-release-field-acceptance/evidence`
  - HEAD 02d4f569cab3…
  - Manifest: `?` evidence/coverage-ledger.md, `?` evidence/implementation-001.md, `?` evidence/implementation-002.md, `M` docs/handoffs/next-release-plan.md
  - Tracked diff SHA-256 b729fdd3aff22807…
  - Untracked: 5e8a77fd… coverage-ledger.md, df84fde4… implementation-001.md, b497e9bb… implementation-002.md
  - Summary: `head=02d4f56 baseline=02d4f56 files=4 diff=b729fdd3aff2 untracked=3/acf09ae68802`, matching the assignment.
- Before/after identity: after the review the summary was the same (`head=02d4f56 baseline=02d4f56 files=4 diff=b729fdd3aff2 untracked=3/acf09ae68802`). There was no unexpected change. The planning files (tasks, design, proposal, findings) are outside the identity paths. I reviewed them as the working-tree diff; `git diff --stat` shows design 4, proposal 4, findings 42 and tasks 10 changed lines.
- Prior reports / rechecked findings: [analysis-004.md](analysis-004.md), ANALYSIS-060 and ANALYSIS-061

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 060: is option (i) recorded? | Read tasks 2.1, 3.4 and 4.4, design section 1 and findings line 79 | 2.1 tells the harness to build with `package.ps1 -FixtureVersion 0.1.11` under `artifacts/update-acceptance/`, label the evidence as fixture-version and never treat it as the release candidate. 4.4 reruns `update-acceptance.ps1 -BaselineArchive <real 0.1.9>` and the harness upgrade, rollback and upgraded-without-setup scenarios on the synchronized package. Design section 1 says the same | Recorded consistently |
| 060: can 2.1 run from the scripts? | `package.ps1:69-72` (fixture output must be under `artifacts/update-acceptance/`), `:109` (fixture builds skip the extension version check), the VSIX and contract are stamped with `$version`, the launcher `--version` check passes. Smoke `Program.cs:51-52` reads `<zip>.release.json` and needs distinct versions. `UpdateTransaction.cs:46-47` accepts 0.1.11 > 0.1.9 | A 0.1.11 fixture candidate can be built and passed to Smoke or the harness against the real 0.1.9 zip. Without `-Dev`, `package.ps1` does not read the user's installation | Workable |
| 060: can 3.4 run from the scripts? | `update-acceptance.ps1:1,6,13-14` | The script has no candidate-version parameter. It always runs `package.ps1` with the declarations version (0.1.9) into `artifacts/releases` and passes that zip to Smoke | Not workable as written: ANALYSIS-080 |
| 060: can 4.4 run, and what does it cover? | `update-acceptance.ps1` after synchronization; `validate.yml:44-68`; `release.yml:17-40` | After synchronization the script builds 0.1.11 and Smoke runs 0.1.9 -> 0.1.11, so it works. The published bytes are built by CI at the tag, and that CI job also runs `update-acceptance.ps1` with the real latest release (0.1.9 < 0.1.11) on those same bytes. The local harness rerun covers a local build of the release commit, not the published bytes | Workable; the coverage claim is overstated: ANALYSIS-081 |
| Safety of the fixture-version candidate | `evidence/implementation-001.md:11` (the 0.1.10 rationale); `UpdateTransaction.cs:46-47,60-61` | The 0.1.11 fixture zip is an unmarked stable-looking 0.1.11 payload, the same hazard that caused 0.1.10 to be skipped | ANALYSIS-082 |
| Does 2.4 name both providers and mixed? | Read task 2.4 | Says "for both providers and a mixed run" and includes the rollback to 0.1.9 while hooks still point at the candidate's ingress image | Yes (covers both clients and mixed) |
| 061 | Compared proposal line 40, design line 11 and the handoff "Current state" | All three list `adopt-native-sessions-from-hooks` as new after 0.1.9 and landed. "still to land" is gone from both predecessor lists | Verified |
| 042 residual (1) | Handoff lines 84-85 | Rows 6 and 7 now read "New after 0.1.9; archived" | Closed. Residual (2), the ledger Part 2 labels, is deferred to 4.3 per findings |
| New contradictions | Cross-read design section 1, tasks 1.3, 2.1, 3.3, 3.4 and 4.4, the proposal and the handoff | No conflict on version, sequencing or provider scope besides ANALYSIS-080 and -081. The dated "only adopt must still land" in design line 9 and task 4.5 describes the 2026-11-22 decisions and is not a regression | None besides the findings below |
| Build, tests, formatting | Not run | Planning-only target, light review | Not performed |

## Findings

### ANALYSIS-080: Task 3.4 needs a feature `update-acceptance.ps1` does not have

- Severity: warning
- Related task / requirement: 3.4, 2.1, 3.3; release-readiness "baseline 0.1.9 upgrade and rollback"; regression from the ANALYSIS-060 fix
- Locations:
  - `scripts/update-acceptance.ps1:1` (only `-BaselineArchive`), `:6` (version from `Directory.Build.props`), `:13` (plain `./scripts/package.ps1`, default `artifacts/releases`), `:14` (passes `artifacts/releases/bell-tower-$version-win-x64.zip` to Smoke)
  - `tools/BellTower.Smoke/Program.cs:52` ("Acceptance requires distinct compiled versions.")
  - [tasks.md](../tasks.md) 3.4: "scripts/update-acceptance.ps1 (including `-BaselineArchive` with the real 0.1.9 archive against the fixture-version candidate)"
  - [findings.md](findings.md) line 79: "3.4 runs update-acceptance with the real 0.1.9 archive"
- Observation: before 4.4 the declarations read 0.1.9. Running `update-acceptance.ps1 -BaselineArchive <real 0.1.9>` therefore does two things:
  - It writes a stable-named `artifacts/releases/bell-tower-0.1.9-win-x64.zip` built from unreleased source.
  - It then fails at the Smoke version assert, because both zips are 0.1.9.
  The script cannot use the fixture-version candidate from 2.1. No task assigns the script change 3.4 depends on. Design section 1 ("Extend the packaged acceptance project/scripts") makes such a change in scope but does not name it. The harness path in 2.1 is not affected.
- Proposed improvement and benefit: record one of these in 3.4 (and in the 2.1 harness work):
  - (a) Add an optional candidate fixture-version parameter to `update-acceptance.ps1`. It packages with `-FixtureVersion` into `artifacts/update-acceptance/packages` and passes that zip to Smoke. Runs without the parameter, including CI, behave exactly as today.
  - (b) Replace the 3.4 script run with the direct commands: `package.ps1 -FixtureVersion 0.1.11 -OutputDirectory artifacts/update-acceptance/packages`, then `dotnet run --project tools/BellTower.Smoke -c Release --no-build -- <real 0.1.9 zip> artifacts/update-acceptance/packages/bell-tower-0.1.11-win-x64.zip`. Keep the default-mode `update-acceptance.ps1` run (0.1.7 fixture baseline) as the regression check.
  Either way 3.4 can be run as written, and no 0.1.9-named rebuild lands in `artifacts/releases`.
- Behavior-preservation risks: CI calls `update-acceptance.ps1 -BaselineArchive $env:BASELINE_ARCHIVE` (`validate.yml:68`) and uploads `artifacts/releases/*` as the release bytes (`validate.yml:71`, `release.yml:19`). Option (a) must leave the default output path and that artifact unchanged.
- Evidence type: source observation
- Verification / measurement plan: text check that 3.4 names (a) or (b). With (a), check that a script test or a `-PlanOnly`-style dry run shows the fixture zip under `artifacts/update-acceptance/`, and that the CI command line is unchanged. The run log should show Smoke "PACKAGED UPDATE PASSED: old 0.1.9 -> new 0.1.11".
- Relationship: residual of ANALYSIS-060

### ANALYSIS-081: Task 4.4 says the local rerun "covers the published bytes", but CI builds the published bytes

- Severity: suggestion
- Related task / requirement: 4.4
- Locations: [tasks.md](../tasks.md) 4.4 ("so the evidence covers the published bytes"); `.github/workflows/release.yml:17-40` (publishes the CI `windows-release` artifact); `.github/workflows/validate.yml:44-68` (at the tag, downloads the latest release and runs `update-acceptance.ps1` on the bytes it then uploads)
- Observation: the published archive is built by CI at the tag. On those bytes, only the CI `update-acceptance.ps1` run uses the real 0.1.9 baseline. The local harness rerun in 4.4 covers a local build of the same commit. The packaging does not claim reproducible builds. My ANALYSIS-060 wording ("would not cover the published bytes") was carried into the task, but the claim is literally true only for the CI run.
- Proposed improvement and benefit: in 4.4, say that the harness rerun covers the release-commit build, and that the tag CI run (`validate.yml` "Packaged session-preserving update acceptance") is the update check on the published bytes, with its log kept. Alternatively, run the harness against the downloaded CI `windows-release` artifact. Each piece of evidence then names the build it covers.
- Behavior-preservation risks: none (planning text)
- Evidence type: source observation
- Verification / measurement plan: text check of 4.4. At the release, check that the tag CI log shows "old 0.1.9 -> new 0.1.11".
- Relationship: residual of ANALYSIS-060

### ANALYSIS-082: The 0.1.11 fixture-version candidate is an unmarked stable payload; keep it out of the maintainer installation

- Severity: suggestion
- Related task / requirement: 2.1, 1.3 (the 0.1.10 skip rationale)
- Locations: [tasks.md](../tasks.md) 2.1; `scripts/package.ps1:69-72,78` (a fixture build has `informationalVersion` = the plain version and no `build` block); `src/BellTower/Updates/UpdateTransaction.cs:46-47,60-61`; [implementation-001.md](../evidence/implementation-001.md) line 11
- Observation: `package.ps1 -FixtureVersion 0.1.11` produces `bell-tower-0.1.11-win-x64.zip` with a stable 0.1.11 contract and no dev marker. If that payload were ever installed or selected in the maintainer's real installation, it would make the published 0.1.11 look already installed, or block staging with a folder collision. That is the same hazard that caused 0.1.10 to be skipped. 2.1 limits harness installs to disposable roots and says the fixture is never the release candidate. It does not forbid installing it for live checks, which use dev builds at base 0.1.9.
- Proposed improvement and benefit: add one clause to 2.1: the fixture-version candidate is installed only under disposable harness roots and never into a real installation; live checks use `-Dev` builds. This keeps the 0.1.11 number unused.
- Behavior-preservation risks: none (planning text)
- Evidence type: source observation
- Verification / measurement plan: text check of 2.1
- Relationship: introduced by the ANALYSIS-060 fix (option (i))

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-060 | `head=02d4f56 baseline=02d4f56 files=4 diff=b729fdd3aff2 untracked=3/acf09ae68802` plus the working-tree planning edits | 2.1, 4.4, design section 1 and findings line 79 name the fixture 0.1.11 candidate before 4.4, its label and its exclusion from `artifacts/releases`, and the rerun on the synchronized package. The 2.1 harness path and the 4.4 `update-acceptance.ps1` rerun work against `package.ps1`, Smoke and `UpdateTransaction.Stage`. 2.4 names both providers and a mixed run | Verified for 2.1, 4.4 and design. 3.4 is not workable as written (ANALYSIS-080); coverage wording in ANALYSIS-081 and a safety guard in ANALYSIS-082 |
| ANALYSIS-061 | same | Proposal line 40 now matches design line 11 (adopt is new after 0.1.9; no "still to land"). The handoff "Current state" agrees | Verified |

## Assessment and limitations

Option (i) is recorded the same way across tasks, design and findings. The harness path is workable, 2.4 covers both providers and a mixed run, and ANALYSIS-061 is fixed. Handoff rows 6 and 7 now close ANALYSIS-042 residual (1). One part of option (i) cannot run as written: task 3.4's `update-acceptance.ps1` run against the fixture-version candidate, because the script has no way to receive it (ANALYSIS-080, warning). It needs either a named script extension or a direct Smoke call. The two suggestions make clearer which build each piece of evidence covers (081) and keep the fixture 0.1.11 out of real installations (082).

Coverage: Claude Code and Codex parity only at the planning level (task text for both providers and mixed). No product code changed. Not performed: build, tests, formatting and any script execution. This is a planning-only light review, and none of these would change the outcome. Nothing read the user's installation. `package.ps1` reads `-TargetInstallation` only with `-Dev`, and I ran no package commands.

There were no unexpected writes, and the identity was the same before and after. I did not attempt the report write because my session instructions forbid report files; the text is inline for the orchestrator to save. The orchestrator owns triage and ledger status. Findings.md records fix cycle 2 as the last permitted cycle.

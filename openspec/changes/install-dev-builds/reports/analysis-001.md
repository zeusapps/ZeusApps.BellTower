<!--
Saved verbatim by the orchestrator (Claude Code main session, claude-opus-5-5) under the client-refusal fallback.
Reviewer: code-analyzer subagent; client Claude Code 2.1.310, subagent, auto permission mode; model claude-opus-5-5.
The client refused the reviewer's writes of reports/analysis-001.md and evidence/analysis-001-retention-map.md (tried once each, not retried).
The retention-map evidence is saved separately at ../evidence/analysis-001-retention-map.md. Report text below is the reviewer's, unmodified.
-->

# Analysis report: install-dev-builds / 001

## Review target

- Reviewer / role: code-analyzer subagent (Analysis 001) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.310 (`claude --version`), subagent spawned by the orchestrator, auto mode, `claude-opus-5-5`
- Date: 2026-10-24T20:21Z
- Change root / assignment: `openspec/changes/install-dev-builds` / Analysis 001
- Task and requirement references: tasks 1.1, 1.2, 2.1, 3.1, 3.2, 4.1, 4.2, 5.1, 5.2 ([tasks.md](../tasks.md)). Requirements in [specs/development-builds/spec.md](../specs/development-builds/spec.md): "Dev versions never collide with stable releases", "Supported dev installation with rollback", "Retiring dev payloads", "Packaging locates its native toolchain", "Dev-build skill parity for Claude Code and Codex"
- Scope and relevant neighboring paths: the 24 target files listed below. Neighbors read: `src/BellTower/Updates/UpdateTransaction.cs` (`Cleanup`, `Apply`), `src/BellTower.Distribution/{InstallationGate,DistributionJson,CaptureCompatibility,AttachmentHealthCompatibility}.cs`, `src/BellTower.Launcher/Program.cs`, `src/BellTower/Tray/TrayApplication.cs:55-66`, `src/BellTower/Setup/HookCommandBuilder.cs`, and the archived `openspec/changes/archive/2026-10-23-reconcile-session-liveness-and-title-refresh/evidence/development-trial-operations.ps1`
- Baseline commit: `b74a12b`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline b74a12b -Paths src,tests,scripts,docs/development.md,docs/releases.md,docs/installation.md,.claude/skills/install-dev-build,.agents/skills/install-dev-build`
  - HEAD `b74a12ba20eb9d3595204b41295b9dab73b7895d`
  - Tracked diff SHA-256 `d7215fdec6d919d3b08d5c0eebe89e5996a72b053bc698f1a3374a1bbc8e7c7c`
  - 16 modified files, plus 8 untracked: both SKILL.md copies `02d598c7…7bd`, `scripts/install-dev.ps1` `e72a994d…08d`, `VersionDisplay.cs` `8af4bfa3…d08d`, and four test files
  - Summary: `head=b74a12b baseline=b74a12b files=24 diff=d7215fdec6d9 untracked=8/78c150b1a77e` (matches the expected value)
- Before/after identity: the same command after the analysis printed the identical summary line. There were no unexpected mutations. My two attempted writes were refused, so I wrote nothing.
- Prior reports / rechecked findings: none

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared dev-range policy | Inspection, `grep` for `9000`, `DevPatchStart`, `IsDevVersion` and the strict regex | C#: one `ReleaseContract.IsDevVersion` and `DevPatchStart`. `install-dev.ps1:59` calls the C# helper. `package.ps1:15-17` holds its own copy | One canonical C# policy. The PowerShell copy is justified but nothing guards it against drift (ANALYSIS-004) |
| Shared effective version | Inspection of `ReleaseContract.Effective`, `Installation.EffectiveVersion`, and their callers | Used by `UpdateDiscovery.Hint`, `update --check` (`UpdateCommand.cs:40`), `UpdateTransaction.Stage` and the `install-dev.ps1:211` Legacy guard | One policy, no parallel copies. Simplification noted in ANALYSIS-003. The base-version trust gap is ANALYSIS-008 |
| Shared display | Inspection of `VersionDisplay`, `ApplicationInfo`, `Installation.DisplayVersion` and the five surfaces | Every surface formats through `VersionDisplay.Format` or `FromInformational`. Dev status is re-derived from the formatted text | Single formatter. Text sniffing in ANALYSIS-003 |
| install-dev.ps1 vs archived trial script | Side-by-side read of both scripts | Private ACL workspace, verified owner (`Read-Owner`, `Assert-SameOwner`, `Assert-OwnedCandidate`), launch gate plus setup/state/presentation data locks, snapshot hashes, `Assert-OriginalBytes` before and after, launcher-parent check on the candidate tray: all kept line for line. Hard-coded versions and level 0 replaced by selection, descriptor and contract values (`Assert-Compatible`) | Invariants kept. The long launch-gate hold is inherited (ANALYSIS-006). An extra PowerShell capture rule is ANALYSIS-009 |
| Launcher and bootstrap compatibility | Read `BellTower.Launcher/Program.cs` (not in the diff), `DistributionJson.Options` (default unmapped-member skip), `Installation.Payload` | `0.1.9001` matches `^(0\|[1-9]\d*)…$`. The `build` member is omitted when null and ignored by older readers. Old `Validate`, `Cleanup` and `Stage` loops accept dev contracts. AssemblyVersion `0.1.9001.0` stays under the 65534 part limit | Compatible by source inspection. I did not run 0.1.8 or 0.1.9 binaries; the developer's disposable cycle claims a real 0.1.8 launcher (QA's area) |
| Update path | Inspection of `ReleaseOffer.EnsureStable` and its three call sites, plus `Stage` | Dev offers are refused in `ReleaseClient`, the `Hint` cache and `Stage`. `Stage` compares against the effective version, so a stable release still passes over a dev selection. The folder-exists check is unchanged | Never offers or stages dev from GitHub. Stable updates from a dev selection are allowed. Old payload code (the legacy 0.1.10 currently selected) cannot use the base-version rule, as documented |
| Interaction with `UpdateTransaction.Cleanup` | Rule-by-rule mapping (evidence below) | Retire's removable set equals the set the tray's Cleanup removes, which runs in every packaged tray since `ed16b9f`/`v0.1.8` | ANALYSIS-001 and ANALYSIS-002 |
| Scope additions | Inspection plus test usage (`grep` in `tests/BellTower.Tests/Scripts`) | See Assessment | Mostly justified. ANALYSIS-001 (`-Legacy` guard), ANALYSIS-005 (`-AssemblyDirectory`), ANALYSIS-007 (dirty rule) |
| Skill parity | `cmp` of both SKILL.md copies, frontmatter read, location compared with the existing `.agents/skills/openspec-*` | Byte-identical (hash above). Only `name` and `description` frontmatter. Same layout as the existing Codex skills. Test enforces identity | Parity holds with no provider-specific policy. Live Codex discovery is not verified (task 6.2, not my check) |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <the 17 target .cs files>` | exit 0 | passed |
| Performance | Inspection | `Installation.EffectiveVersion()` reads the selected `release.json` three times per `Hint` (`Selection`→`Payload`, `Contract`→`Payload`, `Contract`) | Negligible (small file, user-triggered). No finding, not measured |
| Build / tests | not run | QA owns functional checks; format verify was the proportional check | not performed |

## Findings

### ANALYSIS-001: Retire re-implements Cleanup's retention policy in PowerShell, so `-Legacy` and its base-version guard protect nothing that Cleanup does not already remove

- Severity: warning
- Related task / requirement: 4.2; "Retiring dev payloads"
- Locations: `scripts/install-dev.ps1:194-243` (key lines 203-225, 229-234, the Legacy guard at 207-211) against `src/BellTower/Updates/UpdateTransaction.cs:187-235` (`Cleanup`). Cleanup runs from `src/BellTower/Updates/UpdateDiscovery.cs:60-68` and `src/BellTower/Tray/TrayApplication.cs:62-63`.
- Observation: Retire repeats Cleanup step for step: the same gates; selected, `previous`, and recovery-plan retention with the same outcome states (`failed`, `rolled-back`, `staged` or missing); a validated contract; an exclusive lease; refusal of unowned files; `VerifyFiles`; then delete. Only the dev filter is new. Every payload Retire may remove is therefore removed anyway by any packaged tray (0.1.8 or later) at its next startup or 15-minute pass. Cleanup removes stable payloads under the same conditions.
  - The `-Legacy` "newer than the selected build's stable base" heuristic only stops Retire. It does not stop the tray. The spec's "never retires stable" holds only for this procedure.
  - The handoff says Retire "stays useful for dev payloads … still retained by recovery references" (decision 2). That is contradicted by `install-dev.ps1:221`, which refuses exactly those payloads.
  - The design's Migration Plan step 3, and the skill's §6 claim that legacy payloads "become removable", mostly describe something the tray does on its own.
- Proposed improvement and benefit: extract one C# retention and removal predicate from `Cleanup`, for example `UpdateTransaction.RetainedVersions(installation)` and `TryRemovePayload(installation, version)`, used by both `Cleanup` and Retire. Retire then adds only the dev filter. The retention rule then lives in one place, and a later Cleanup change cannot leave Retire behind. Orchestrator decision: keep `-Legacy` and the heuristic (now documented as advisory, because the tray removes those payloads anyway), or drop them and document that the tray reclaims legacy 0.1.9 and 0.1.10 once they are neither selected nor `previous`.
- Behavior-preservation risks: Cleanup currently swallows errors and skips modified payloads, while Retire throws with a reason. The shared predicate must return the reason and leave the caller to choose between skipping and failing. Keep the gate order: update, then launch.
- Evidence type: source observation (rule map below)
- Verification / measurement plan:
  - Unit test the shared predicate against the existing `DevBuildTests.PreviousMayNameADevVersionAndCleanupRetainsIt` fixtures.
  - Rerun the `InstallDevScriptTests` Retire cases.
  - Add one case: a dev payload that a `failed` plan retains is refused by Retire and kept by Cleanup.
- Relationship: none

### ANALYSIS-002: Preflight does not report what the tray's Cleanup will remove after a Switch or Rollback, so rollback depth is silently one level

- Severity: suggestion
- Related task / requirement: 4.1, 5.2; "Supported dev installation with rollback"
- Locations: `scripts/install-dev.ps1:337` (the selection becomes `{candidate, previous: original}`), `:378` (the original `current.json` bytes are restored). `UpdateTransaction.cs:192-194`. `Installation.cs:24-25` only parses `previous`.
- Observation:
  - After a Switch, the dev tray's first Cleanup deletes every payload other than the candidate, the original and recovery-retained versions. That includes the original selection's own `previous`, whether stable or legacy.
  - After a Rollback, the restored `current.json` can name a `previous` whose payload is gone. This is harmless, because only Cleanup reads it. The original tray's first Cleanup also deletes the dev payload.
  - Neither Preflight nor the skill tells the maintainer about this. The docs mention only "the installed tray's own cleanup also removes verified unretained payloads".
- Proposed improvement and benefit: with the ANALYSIS-001 predicate, Preflight prints `removedByTrayAfterSwitch: [...]`. The skill and docs say that Rollback restores one level and that a rolled-back dev payload is reclaimed automatically. The maintainer gets an honest, decision-ready preflight at no extra policy cost.
- Behavior-preservation risks: Preflight must stay read-only. The computation reads only `current.json` and the `updates/` plans.
- Evidence type: source observation
- Verification / measurement plan: run a disposable cycle with an installation holding `{0.1.10 selected, previous 0.1.9}` plus `0.1.8`. Assert that Preflight lists `0.1.9` and `0.1.8`, and that after the Switch and one tray start, exactly those two are gone.
- Relationship: depends on ANALYSIS-001

### ANALYSIS-003: dev status is re-derived from display text, and the strict version regex is repeated

- Severity: suggestion
- Related task / requirement: 1.1, 2.1
- Locations:
  - `src/BellTower.Distribution/VersionDisplay.cs:35` (`IsDev(display) => display.Contains(" dev")`), used at `src/BellTower/Cli/ApplicationInfo.cs:15`, `src/BellTower/Cli/UpdateCommand.cs:41` and `src/BellTower/Tray/OverviewForm.cs:49`
  - The regex at `ReleaseContract.cs:26` and `:35`, and `VersionDisplay.cs:25`
  - `ReleaseContract.Effective` (`:30-36`)
- Observation:
  - There are three ways to answer "is this dev": `IsDevVersion`, contract `Build`, and a substring match on formatted text.
  - The regex is copied twice in C#.
  - `Effective`'s regex fallback (a malformed `baseVersion` silently becomes `MAJOR.MINOR.0`) cannot be reached for contracts read through `Installation.Contract`, because `Payload()` has already validated them.
- Proposed improvement and benefit:
  - Add `ApplicationInfo.IsDev => ReleaseContract.IsDevVersion(Version)` (or pass the bool), and use `IsDevVersion` on the selected version in `UpdateCommand`.
  - Add a `ReleaseContract.TryParse` and reuse it in `Effective` and `FromInformational`.
  - Simplify `Effective` to `build is null ? new(M, m, 0) : Parse(build.BaseVersion)`.
  - The result is one dev predicate and no display-format coupling.
- Behavior-preservation risks: `FromInformational` must still classify unmarked or malformed informational versions as "dev (unmarked)". Keep `DevBuildTests.InformationalVersionFallbackFormatsTheSameLabels` passing.
- Evidence type: source observation
- Verification / measurement plan: run the existing `DevBuildTests`, plus a case where `Effective` is given an invalid base and throws instead of falling back.
- Relationship: none

### ANALYSIS-004: package.ps1's copy of the dev range has no drift guard

- Severity: suggestion
- Related task / requirement: 3.2
- Locations: `scripts/package.ps1:15-17`, `:47`. `tests/BellTower.Tests/Scripts/PackageScriptTests.cs:62-63` hard-codes 8999 and 9000.
- Observation: the script cannot load the assemblies before building, so the duplicate is justified. But no test ties `$devPatchStart` to `ReleaseContract.DevPatchStart`. The script's `(\d+)` PATCH also accepts leading zeros (`0.1.09001`), which `ReleaseContract.Parse` rejects.
- Proposed improvement and benefit: drive the `-PlanOnly` tests with `ReleaseContract.DevPatchStart - 1` and `DevPatchStart` instead of literals, and use `(0|[1-9]\d*)` for PATCH. A constant change then breaks a test instead of splitting the policy.
- Behavior-preservation risks: none for valid versions.
- Evidence type: source observation
- Verification / measurement plan: the modified `PackageScriptTests` pass, and fail when the C# constant is changed locally.
- Relationship: none

### ANALYSIS-005: `-AssemblyDirectory` is not confined like the other test switches, and it disables the freshness guard

- Severity: suggestion
- Related task / requirement: 4.1 (stale-build guard)
- Locations: `scripts/install-dev.ps1:33`, `:42-47`, `:179-182`, `:263`. Skill rule 4 in both SKILL.md copies.
- Observation: `-NoTray` and `-SkipProbe` require `-Disposable` and `artifacts/` paths. `-AssemblyDirectory` does not. Against the real installation it loads assemblies from any folder, and line 263 then skips the "sources newer than Release assemblies" check without saying so. Only the tests use it, always with `-Disposable` (`InstallDevScriptTests.cs:190`, `:295`, `:310`). The skill does not list it among the switches to avoid.
- Proposed improvement and benefit: require `-Disposable` for `-AssemblyDirectory`, or run the freshness check against whichever directory is used. Either closes a quiet bypass of a design-required guard (design "Risks", stale build).
- Behavior-preservation risks: the tests already pass `-Disposable`. The developer's maintainer Preflight used the defaults.
- Evidence type: source observation
- Verification / measurement plan: extend `TestOnlyOptionsAndPathsAreConfined` with `-AssemblyDirectory` without `-Disposable`, which should be refused.
- Relationship: none

### ANALYSIS-006: Switch and Rollback hold the launch gate across the tray-stop wait, the payload copy and the hashing, and skip the update gate (inherited)

- Severity: suggestion
- Related task / requirement: 4.1; "The procedure SHALL NOT stop agent sessions"
- Locations:
  - `scripts/install-dev.ps1:313-386`: the launch gate covers `Stop-VerifiedTray` (up to 30 s, `:318`), `Copy-Item` of the prepared payload (`:335`, about 100 MB per the handoff), and two `VerifyFiles` hash passes (`:334`, `:336`)
  - `src/BellTower.Launcher/Program.cs:15` waits 10 s on the same gate (`InstallationGate.cs:9`), then fails
  - The archived script has the same structure
  - Product `Apply` takes the launch gate only around the selection compare-and-write
- Observation:
  - Any launcher invocation during that window fails with "Another installation operation is busy". That covers every hook verb that is not `event`, and every `event` hook when fast capture is off (`HookCommandBuilder.cs:12-16`). Agents keep running, but their notifications can be lost.
  - Switch also takes no `update` gate and does no pending-update (`EnsureNoCoordinator`) check, while Retire takes both gates.
- Proposed improvement and benefit:
  - Copy and verify the payload under the `update` gate first.
  - Take the launch gate only for the final selection-hash check and write.
  - Refuse when `updates/pending.json` exists.
  - This shrinks the hook-failure window to milliseconds and lines up with Stage and Apply.
- Behavior-preservation risks: the folder-exists and selection-hash checks must stay inside the launch gate. Rollback's restore of `current.json` must stay inside it too.
- Evidence type: source observation and a performance hypothesis (window length not measured)
- Verification / measurement plan: time the Switch phases in a disposable cycle (stop wait, copy, hashing) and record how long the launch gate is held. A launcher `--version` probe started in parallel during the copy should not fail after the change.
- Relationship: inherited from the archived trial script, not introduced here

### ANALYSIS-007: the build-input dirty rule's allow-list misses build inputs

- Severity: suggestion
- Related task / requirement: 3.2; "Dirty working tree" scenario
- Locations: `scripts/package.ps1:96`
- Observation: the rule itself is justified, because untracked OpenSpec notes should not mark a build dirty. But the allow-list leaves out `global.json` (SDK selection) and `.editorconfig` (analyzer severities). A change to either produces a package marked clean even though its output differs. `docs/` is rightly included, because the payload ships all of `docs/` (handoff item 8).
- Proposed improvement and benefit: invert to an exclusion pathspec, for example `git status --porcelain -- . ':!openspec' ':!artifacts'`. That avoids false "clean" markers without listing inputs by hand.
- Behavior-preservation risks: excluded areas must not be packaged. Check against `package.ps1`'s `$ownedNames`.
- Evidence type: source observation
- Verification / measurement plan: a `PackageScriptTests` `-PlanOnly` case with a modified `global.json` copy in a temporary worktree reports `dirty: true`.
- Relationship: none

### ANALYSIS-008: the effective version trusts `baseVersion` without relating it to the version or to the original selection

- Severity: suggestion
- Related task / requirement: 1.2, 4.1
- Locations: `src/BellTower.Distribution/ReleaseContract.cs:9-17` (`ReleaseBuild.Validate`) and `:30-36`. `scripts/install-dev.ps1:269-276` (Preflight). The Legacy guard at `:211`.
- Observation:
  - `Validate` checks only that the base is a stable, parseable version.
  - Preflight does not compare the candidate's base with the original's effective version.
  - Suppose a dev build comes from an older branch, for example base 0.1.8, over a selected or `previous` stable 0.1.10. Discovery then keeps offering 0.1.10, `Stage` fails with "Candidate version already exists", and the Legacy guard widens to stable 0.1.9 and 0.1.10.
- Proposed improvement and benefit: require that the base has the same MAJOR.MINOR as the version, in `ReleaseBuild.Validate`. Have Preflight warn or refuse when `candidate.EffectiveVersion < original.EffectiveVersion`. This avoids a persistent false "update available".
- Behavior-preservation risks: `package.ps1` always derives the dev MAJOR.MINOR from the base, so valid packages are unaffected.
- Evidence type: source observation
- Verification / measurement plan: a `DevBuildTests` case with a mismatched MAJOR.MINOR base, rejected. An `InstallDevScriptTests` Preflight case with a candidate base older than the original's stable version.
- Relationship: related to ANALYSIS-001 (Legacy guard)

### ANALYSIS-009: `Test-PendingCaptures` is a capture rule that exists only in PowerShell

- Severity: suggestion
- Related task / requirement: 4.1 ("compatibility derived from the contracts")
- Locations: `scripts/install-dev.ps1:163-171` against `src/BellTower.Distribution/CaptureCompatibility.cs:7-21`
- Observation:
  - `Assert-Compatible` calls `CaptureCompatibility.Ensure`, which already refuses a `capture-protocol.json` marker when the consumer is not 1.
  - It then adds a second rule: refuse when `Capture == 0` and `captures-v1/*.json` exist without the marker. The marker branch duplicates the C# check.
  - Product update recovery (`Apply` rollback) uses only `CaptureCompatibility`, so dev rollback and update rollback apply different rules.
- Proposed improvement and benefit: if pending captures without the marker really matter for capture-0 readers, move that rule into `CaptureCompatibility.Ensure` so updates get it too. Otherwise drop it. That leaves one capture policy.
- Behavior-preservation risks: moving it into C# tightens update Stage and Apply for capture-0 targets, which today means only legacy 0.1.8. Review that against the update acceptance fixtures.
- Evidence type: source observation
- Verification / measurement plan: keep `PreflightRefusesDataThatTheOriginalCannotReadAfterRollback` passing. If moved, add a `CaptureCompatibility` unit case.
- Relationship: none

## Re-verification

None (first round).

## Assessment and limitations

- **Shared policy.** There is one C# dev range (`ReleaseContract.IsDevVersion` and `DevPatchStart`), one effective version (`ReleaseContract.Effective` through `Installation.EffectiveVersion`), and one display formatter (`VersionDisplay`). `install-dev.ps1` uses these through the loaded assemblies. The only parallel copies are `package.ps1`'s range check (justified, but needs a drift guard: ANALYSIS-004) and the PowerShell Retire and capture rules (ANALYSIS-001, ANALYSIS-009).
- **install-dev.ps1.** Every safety invariant of the archived trial script is kept. Compatibility comes from the contracts (`CaptureCompatibility` with each contract's `Capture` and `AttachmentHealth`). Versions come from the selection and the descriptor.
- **Launcher and bootstrap.** Not changed. The 0.1.8 and 0.1.9 launchers and payloads stay compatible by source inspection (strict version format, unknown JSON members ignored).
- **Update path.** It never offers or stages a dev build from GitHub (`EnsureStable` in `ReleaseClient`, the `Hint` cache and `Stage`). It still allows a stable update over a dev selection, because the effective version is used in `Stage`, `Hint` and `update --check`.
- **Cleanup.** See ANALYSIS-001 and ANALYSIS-002.
- **Scope additions:**
  - `EnsureStable`: justified. The spec requires it, and staging is the essential call site. A hypothetical dev-range GitHub release would show up as a check error rather than "no update", which is acceptable because stable packaging refuses the range.
  - `-Disposable`, `-NoTray`, `-SkipProbe`: justified and confined (`TestOnlyOptionsAndPathsAreConfined`).
  - `-AssemblyDirectory`: justified for tests but under-confined (ANALYSIS-005).
  - `-TargetInstallation`, `-VsWherePath`, `-PlanOnly`, `-CheckToolchain`: justified. They are read-only or build nothing, and they make the version and toolchain policy testable.
  - `-Legacy` guard: a heuristic that adds no protection given Cleanup. Decision needed (ANALYSIS-001).
  - Build-input-only dirty rule: justified in principle, but the list is incomplete (ANALYSIS-007).
  - `ApplicationInfo.Version` stripping `-dev`: needed for the presence and selection comparisons.
  - Restart-only-if-running: consistent with the product's `HadTray`.
- **Skill parity.** The two copies are byte-identical and a test enforces it. There is no per-provider policy. Live Codex discovery is still unverified (task 6.2).
- **Not performed:** build, the test suite, and running the old 0.1.8 or 0.1.9 binaries (QA's area). No timing measurement for ANALYSIS-006. I created no disposable data, so nothing needed cleaning up. I made no writes: both attempted writes were refused.
- **Decisions needed:**
  1. Keep or drop `-Legacy` and its heuristic, given Cleanup (ANALYSIS-001).
  2. Whether to extract the shared retention predicate now or later.

```
status: partial
role / client / model: code-analyzer / Claude Code 2.1.310 / claude-opus-5-5
task / finding IDs: tasks 1.1-5.2 analyzed; new findings ANALYSIS-001 (warning), ANALYSIS-002 to ANALYSIS-009 (suggestions)
changed or report paths: none written (client refused reports/analysis-001.md and evidence/analysis-001-retention-map.md); full report and evidence text inline above
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <17 target .cs files> -> passed; build/tests -> not run (QA scope)
target identity: before head=b74a12b baseline=b74a12b files=24 diff=d7215fdec6d9 untracked=8/78c150b1a77e; after identical
limitations / decisions needed: report write refused, report text is inline for the orchestrator to save; no runtime or timing measurement; Codex live discovery not checked; decide on -Legacy given Cleanup (ANALYSIS-001)
next: orchestrator saves the report and evidence, triages ANALYSIS-001 to ANALYSIS-009 into the ledger, and routes any accepted fixes to the developer
```

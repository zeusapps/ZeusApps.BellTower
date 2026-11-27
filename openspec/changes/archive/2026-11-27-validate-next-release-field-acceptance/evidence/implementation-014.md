# Implementation 014 (DEV-012): tasks 4.3, 4.4 and 5.1 to 5.3

Scope: user decision 2026-11-26 (release essentials). One run of each check, no new harness checks, no field-harness reruns. Nothing committed, no task ticked. Raw logs: `artifacts/dev014/` (ignored). No `~/.claude`, `~/.codex`, real installation or user tray (8060, 28388) was touched.

## 4.4 versions

Version declarations changed from 0.1.9 to the candidate 0.1.11:

| Declaration | Value |
| --- | --- |
| `Directory.Build.props` `<Version>` (app, launcher, ingress, distribution, descriptor and probe versions all derive from it through `scripts/package.ps1`) | 0.1.11 |
| `vscode/package.json` `version` (extension and VSIX) | 0.1.11 |
| Built package `artifacts/releases/bell-tower-0.1.11-win-x64` | `bell-tower.exe --version` 0.1.11; `--distribution-info` effectiveVersion 0.1.11, isDev false, packageName `bell-tower-0.1.11-win-x64.zip`; `release.json` contract version 0.1.11; `current.json` 0.1.11; VSIX `bell-tower-focus-0.1.11.vsix` with `extension/package.json` 0.1.11 |

Remaining `0.1.9` text in the tree is intentional: the baseline tag `v0.1.9` in `scripts/field-acceptance.ps1`, fixtures and tests that name the published baseline, and comments. A grep of `*.props`, `*.csproj`, `package.json`, `scripts/package.ps1` and `update-acceptance.ps1` found no other version declaration.

Built zip (default-mode run, last build) `bell-tower-0.1.11-win-x64.zip` sha256 `de400a829041d026972daf2269dbbe982b36ba421ba263c1eaffbab53070c76a`; matches its `.sha256` sidecar. It is a local check build, not the release artifact (the tag CI run builds and verifies the published bytes, ANALYSIS-060, -081).

## 4.4 update-acceptance reruns (one each)

| Run | Result |
| --- | --- |
| Baseline archive `artifacts/field-acceptance/baseline/bell-tower-0.1.9-win-x64.zip`, sha256 `2739659eb95e4d15ae0d4c8d9c35a69e4947a9b6191edaa5a71291560c29f062`, equals its `.sha256` sidecar (checked with `sha256sum` before the run) | `scripts/update-acceptance.ps1 -BaselineArchive <real 0.1.9 zip>` exit 0: PACKAGED ROLLBACK PASSED, PACKAGED UPDATE PASSED (old 0.1.9 to new 0.1.11, baseline fast ingress). Log `artifacts/dev014/ua-baseline.log` |
| Default mode (0.1.7 fixture baseline to the candidate) | exit 0: PACKAGED ROLLBACK PASSED, PACKAGED UPDATE PASSED (old 0.1.7 to new 0.1.11). Log `artifacts/dev014/ua-default.log` |

The smoke teardown race seen once in DEV-011's default-mode run (`tools/BellTower.Smoke/Program.cs:210`) did **not** recur in this run, so it was not investigated. One observation: after the default run, two hook-started trays of that run (`stopped-installation` and its 0.1.7 version, PIDs 45028 and 37696 under `artifacts/update-acceptance/run-388e0c283776455484af9cf0c9869db0`) were still alive although the smoke reported success; they were verified by path as orphans under `artifacts/` and stopped. This looks like the same hook-started-tray teardown area as the DEV-011 failure; a candidate for the follow-up list, not a gate by itself.

## 4.3 ledger

`evidence/coverage-ledger.md` Part 6 adds the final status per required report item (passed, failed or documented limitation, unobserved), links implementation and evidence files, marks tasks 2.5 to 2.7, 3.1 and 3.2 as descoped by the 2026-11-26 decision (not passed evidence) and tasks 4.1, 4.2, 6.1 to 6.4 as user-owned and pending.

## 5.1 shared policy (one product change)

The only product change in this release is the retained checkout (task 2.8): `CheckoutPolicy.Choose` takes a `retained` argument and `IdentityResolver` passes it, with no provider parameter or branch anywhere in the policy. Tests in `tests/BellTower.Tests/Sessions/CheckoutResolutionTests.cs`, parameterized with `claude` and `codex`: `ABranchChangeInTheVerifiedCheckoutKeepsItAsTheWorkingCheckoutAndRefreshesItsContext`, `ALaunchFallbackAndAmbiguityAreNeverRetained`, `ClearCheckoutAndResetReleaseARetainedCheckoutForGood`, `AClearThatLandsMidPassIsNotUndoneByThePassThatStartedBeforeIt`, `AFailedListingIsCachedAsAFailureForABoundedLife`, `UnrelatedWorkObservedAfterTheSwitchIsNotClearedByTheVerifiedCheckoutsBranchChange`, `AFailedWorktreeListingKeepsTheRetainedCheckoutUntilThereIsPositiveEvidence`, `AnyNewWorkReleasesTheRetainedCheckout`, `ABranchSwitchThatRemovesTheVerifyingChangeFolderStillRetainsTheCheckout`; the mixed-session case `MixedSessionsInOneLaunchDirectoryResolveIndependentlyWithoutCrossReuse for checkout isolation, and the installed mixed run `mixed/branch-change-keeps-working-checkout` in harness-2.2 for retention`; provider-neutral cases `APinAndProviderEvidenceKeepWinningOverARetainedCheckout`, `CheckoutPolicyRetainsOnlyInPlaceOfTheLaunchFallbackAndOnlyForAListedWorktree`, `TheRetainedSourceIsAStablePersistedTokenAndOnlyThePresentationIsWorded`. Installed evidence: H2.2 five-conversation fixtures (Claude, Codex, mixed) and H2.4 retained-checkout checks. The presentation (`OverviewForm`, `list`) reads the same `CheckoutPolicy.Describe` for both.

## 5.2 equal availability audit (read-only, no new runs)

- Setup: `SetupCommand` `--clients` accepts `both`, `claude`, `codex`; the status-line, desktop and host-bridge options apply to both (read of `src/BellTower/Cli/SetupCommand.cs`).
- Defaults and controls: attach, clear, color, reset and `rename --use-bell-tower-name` are shared commands naming both providers (`RenameCommand`, `ListCommand`, `CapabilityMatrix` rows "Checkout resolution and controls", "Color, reset and legacy badge import", "Setup, configuration, diagnostics and tray controls").
- Diagnostics: doctor reports the matrix and the `hookStart.ingressImage` state for both providers; new rows state the 0.1.11 limitations per provider.
- Unequal items found and recorded, none hidden: Codex explicit native names (unfinished parity gap), ANALYSIS-240 and ANALYSIS-265 (Codex only), ANALYSIS-242 (plain Claude, pre-existing). All are labeled unfinished or unverified, with no claimed provider restriction and no proof asserted.

## 5.3 capability matrix

`src/BellTower/Diagnostics/CapabilityMatrix.cs` and `docs/compatibility.md` gained seven rows: branch-change checkout retention (supported for both); explicit native name as a title tier (Claude supported, Codex unverified/unfinished); hook image after an upgrade or manual rollback (Codex unverified, ANALYSIS-240 and the setup rerun); first hook that cannot identify the agent process (Codex known regression ANALYSIS-265, Claude plain-launch ANALYSIS-242); descoped installed-harness scope (unverified for both); user-owned live checks (unverified for both); publisher-signed updates (unfinished for both). No row is marked restricted. The existing `CapabilityMatrixTests` (statuses, no restriction without proof, document and source agreement) cover the new rows.

## Release notes

`docs/release-notes-0.1.11.md` (draft; linked from `docs/releases.md` step 5). The repository keeps no notes file; the release workflow only passes `--generate-notes`, so the maintainer pastes or replaces the body. It states: the branch-change fix for both providers; 0.1.10 skipped; rerun `setup --host-bridge --apply` (no automatic re-provision); resume or restart plain `codex --no-daemon` conversations after the rerun; manual rollback guidance (ANALYSIS-240); known Codex regression (ANALYSIS-265) with workaround; Codex native-name gap; rollback effects (ANALYSIS-186); publisher signing deferred, SHA-256 verification only; hash verification instructions and the possible Codex trust prompt.

## Checks run once after the edits (files converted to CRLF last)

See the end of this file for results.

| Check | Result |
| --- | --- |
| `dotnet format ZeusApps.BellTower.slnx --verify-no-changes` | exit 0 |
| `dotnet build ZeusApps.BellTower.slnx -c Release` | succeeded, 0 warnings, 0 errors |
| `dotnet test -c Release --no-build` (complete .NET suite) | 2246 of 2247 passed. One failure: `CliTests.GeneratedPowerShellHookCommandPreservesUnicodeAndFinishesWithinItsTimeout` (PowerShell CLIXML progress text on stderr under load, `CliTests.cs:343`); it passed alone (1/1). Load-sensitive, unrelated to the edited files |
| `openspec validate validate-next-release-field-acceptance --strict` | valid |
| Node suite | not rerun: no JavaScript file changed (only the `package.json` version) |

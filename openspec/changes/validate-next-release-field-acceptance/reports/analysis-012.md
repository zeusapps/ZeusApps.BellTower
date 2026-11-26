> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125); the reviewer did not attempt the write because the client refuses subagent report writes.
> Reviewer: ANALYSIS-012 code-analyzer subagent; client Claude Code (delegated subagent, auto mode); model claude-opus-5-5.
> Refusal message as reported for earlier reports: "Subagents should return findings as text". Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

# Analysis report: validate-next-release-field-acceptance / ANALYSIS-012

## Review target

- Reviewer / role: code-analyzer subagent for orchestrator session `claude-release-gate-20261125` / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (delegated subagent, auto mode; the subagent cannot see the client version) / `claude-opus-5-5`
- Date: 2026-11-25T23:38Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-012. This is the final recheck (fix cycle 2) of ANALYSIS-200 to -206 against DEV-009, plus a review of the whole retention lifecycle.
- Task and requirement references:
  - [tasks.md](../tasks.md) 2.1, 2.2 and 2.8.
  - [session-identity delta](../specs/session-identity/spec.md), "Git branch and worktree context" (line 4) and the scenario "Verified checkout changes branch after its work is cleared" (lines 33-36).
- Scope:
  - Product: `src/BellTower/Tray/OverviewForm.cs`, `Sessions/IdentityResolver.cs`, `Core/SessionIdentity.cs`, `IO/GitReader.cs`, plus the DEV-008 neighbors `Core/CheckoutPolicy.cs`, `Core/IdentityControls.cs` and `Sessions/SessionCatalog.cs`.
  - Harness: `tools/BellTower.FieldAcceptance/Evidence/RunReport.cs`, `Program.cs:195-212` and `scripts/field-acceptance.ps1`.
  - Tests: `tests/BellTower.Tests/Sessions/CheckoutResolutionTests.cs`, `Tray/OverviewControlTests.cs` and `FieldAcceptance/SurfaceAndGateTests.cs`.
  - Documentation: `docs/session-identity.md:59` and `docs/distribution-validation.md:28`.
  - Evidence: [implementation-009](../evidence/implementation-009.md).
- Neighboring paths read: `src/BellTower/Tray/TrayApplication.cs:287-366` (how often the resolver runs), `src/BellTower/Core/WorkPolicy.cs` (how work binds to a branch) and `SessionIdentity.cs:70-77` (`WorkSubject`, `Checkout`).
- Baseline commit: `02d4f56`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 02d4f56 -Paths Directory.Build.props README.md vscode src scripts docs tests tools ZeusApps.BellTower.slnx openspec/changes/validate-next-release-field-acceptance/evidence openspec/changes/validate-next-release-field-acceptance/specs` printed `Summary: head=02d4f56 baseline=02d4f56 files=83 diff=9ab7066ad5de untracked=65/fb48073de8b3`. This matches the assignment.
- Before/after identity: the same command printed the same summary line after the analysis. Nothing changed unexpectedly.
- Prior reports and rechecked findings: [analysis-011](analysis-011.md), ANALYSIS-200 to -206.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Retention lifecycle as a whole | Traced `RefreshAsync` (`IdentityResolver.cs:20-74`), `RetainedAsync` (`:79-91`), `CheckoutPolicy.Choose`, `RetainedCheckout` and `Release` (`CheckoutPolicy.cs:28-54, 96-99`), and every writer of `CheckoutSource` and `CheckoutWork` (searched all `src/**/*.cs`). Only the resolver lambda, `Release` (via `SessionCatalog.cs:92` and `IdentityControls.cs:24`) and the default value write them. | **How a checkout becomes retained.** `branchCleared` (`:57`), or the vanished-folder case (`:86-90`, which now requires `ActiveWork == CheckoutWork`).<br>**How it stays retained.** Through `:83-84`.<br>**How it is released.** A pin, provider evidence, any work subject, ambiguity, a successful listing without the checkout, a missing directory, Clear and Reset.<br>**Concurrency.** The final decision is made under the lock (`:62-64`). The tray runs one pass at a time (`TrayApplication.cs:287, 333-345`), so the only concurrent writers are the user controls. | Correct. No defect at warning level. One narrow race is noted in the assessment. |
| Linearizability of the locked decision | Checked each control that can land mid-pass against the outcome of a sequential order | Clear or Reset on a retained snapshot gives `launch`, and the next pass stays at `launch`, because `RetainedAsync` needs `retained` or `unique-work`. A pin landing mid-pass gives `launch` with the pin shown, and the next pass picks the pin. A Clear landing during a pass that turns `unique-work` into `retained` matches the order "clear, then pass", because the clear changed nothing the pass had read. | Consistent. |
| Transient after a lost race | `:59, 64` when `retain && !kept` | This pass still writes `WorkingCheckout` and the branch or subject of the retained checkout while `CheckoutSource = launch`. The next pass rebinds through `ApplyBranch`, because the previous and selected checkouts differ, so no work is cleared. | Self-corrects within one 2 s pass. Not a defect. |
| What `CheckoutWork` does | `SessionIdentity.cs:28-30`, `IdentityResolver.cs:65-68, 86-87` | `CheckoutWork` holds the snapshot `WorkSubject` whenever the source is `unique-work`. It is kept while the checkout is retained and cleared for every other source. A record that predates the field (null) disables only the vanished-folder case, and only until the next `unique-work` pass writes it. A value that 0.1.9 keeps after a rollback can only cause a launch fallback (a false negative), never a wrong retention. | Correct. It is additive. The test round-trips the field through `AdditionalData`. |
| Delta spec, compared sentence by sentence | Spec line 4 and scenario lines 33-36 against the code | Each sentence is mapped in the assessment. "Clears the work that verified it" is what `CheckoutWork` enforces for the vanished-folder case; in the `branchCleared` case the same pass derives the verifying work. The spec does not name the field, which is right for a requirement; `docs/session-identity.md:59` names it accurately. | Matches. The single exception is a race inside one pass (see the assessment). |
| Listing cache | `IdentityResolver.cs:9, 12-16, 118-127` and `GitReader.cs:43-49` | A failure is stored as `Paths = null` for 10 s (`FailureLife`), and a success for 30 s. A cached failure is read as "no evidence", and `RetainedCheckout` then checks that the directory exists. A cached success that still lists a removed checkout is caught by `Choose`'s `exists(retained)`. Production code passes `listing = null`, so the resolver uses `git.TryWorktrees`. | Correct. |
| Tray Clear | `OverviewForm.cs:134, 259-267` | `clear ? "" : …` always sends `""`. `originalCheckout` resets to `""`. A pinned row behaves as before. | Correct. |
| Harness exit codes | `RunReport.cs:39-42, 57-59`, `Program.cs:204-212`, `field-acceptance.ps1:4-7, 35-41, 84-93`, `docs/distribution-validation.md:28` | JSON now has `verdict` and `exitCode`. The script exits 2 when another run is active, passes 3 through, and returns any other non-zero code with `exit`. The header and the docs match. | Correct. One note under ANALYSIS-204. |
| Dead code | Searched all `.cs` files, tracked and untracked, for `.Worktrees(` and `TryWorktrees` | The only listing API is `TryWorktrees`, used at `IdentityResolver.cs:122`. | Correct. |
| Parity | Changed product code | No provider branches. The new resolver and tray tests run as Claude and Codex theories. The additive-field test is provider-neutral. Mixed sessions are covered by `MixedSessionsInOneLaunchDirectoryResolveIndependentlyWithoutCrossReuse`. | Holds for both clients. |
| Unit tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~CheckoutResolutionTests\|FullyQualifiedName~FieldAcceptance\|FullyQualifiedName~IdentityRecovery\|FullyQualifiedName~IdentityControl\|FullyQualifiedName~OverviewControlTests"`. The binary is from 23:59:23 and the newest source file from 23:59:05. | 197 passed, 0 failed | passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include` with the 11 changed C# files in scope | exit 0 | passed |
| Harness | Not run | Excluded by the assignment | not performed |

## Findings

**No findings.** ANALYSIS-220 to -239 are unused.

## Re-verification

All rows were checked at `head=02d4f56 … diff=9ab7066ad5de untracked=65/fb48073de8b3`.

| Finding ID | Independent evidence | Outcome |
| --- | --- | --- |
| ANALYSIS-200 | `OverviewForm.cs:134` calls `Configure(false, false, true)`, and `:265` sends `""` when `clear` is set. `SessionCatalog.cs:92` then runs `Release`. Test `ClearCheckoutOnAnUnpinnedRetainedRowStillSendsAnEmptyCheckout` (Claude, Codex) uses `CheckoutPin = null`, source `retained`, and expects `(key, "", null, false)`. It passed. | verified |
| ANALYSIS-201 | `IdentityResolver.cs:62-64` writes `retained` only when the fresh `CheckoutPin is null` and the fresh `CheckoutSource` equals the snapshot's; otherwise it writes `launch`. Test `AClearThatLandsMidPassIsNotUndoneByThePassThatStartedBeforeIt` (Claude, Codex) clears inside the `CheckoutCache` read at `:45`. That read is the first cache read for a retained snapshot, because `RetainedAsync` reads no branch for that source. The test asserts `launch` after the pass and after one more, and the working checkout is back at main. It passed. The `unique-work` to `retained` transition still holds, because the snapshot and the fresh source are both `unique-work`, as the existing tests confirm. | verified |
| ANALYSIS-202 | `:120` gives a failure a 10 s life and a success 30 s. `:122-125` caches the null result. Test `AFailedListingIsCachedAsAFailureForABoundedLife` (Claude, Codex) counts 1 listing at +2 s and 2 listings at +11 s. The ANALYSIS-181 tests at `CheckoutResolutionTests.cs:669-716` still pass, so a cached failure keeps the checkout while its directory exists and releases it when the directory is gone. | verified |
| ANALYSIS-203 | `:86-87` requires `ActiveWork == CheckoutWork`, and `:65-66` records the verifying work. Test `UnrelatedWorkObservedAfterTheSwitchIsNotClearedByTheVerifiedCheckoutsBranchChange` (Claude, Codex) keeps work B with source `launch` at main. Without the check, B's `WorkBranch` would be the stale `ObservedBranch` and the test would retain, so the test can fail. The QA-181 test `ABranchSwitchThatRemovesTheVerifyingChangeFolderStillRetainsTheCheckout` still passes. The additive-field test passed. | verified between passes; an in-pass residual is noted in the assessment |
| ANALYSIS-204 | `RunReport.cs:58-59` adds `verdict` and `exitCode`, and `SurfaceAndGateTests` asserts `"PASSED WITH UNFINISHED ITEMS"` and `3`. Note: for an interrupted run the JSON says `verdict: FAILED, exitCode: 1` while the process exits 130 (`Program.cs:206-209`, where `ExitCode` is documented only for a completed run). That is a failure either way, and the `run/interrupted` check in the JSON identifies the case, so no false pass is possible. It is below the warning threshold and not raised. | verified |
| ANALYSIS-205 | `field-acceptance.ps1:39-40` sets `$global:LASTEXITCODE = 2` and runs `exit 2`. `:84-93` uses `exit 3` and `exit $code`. Setup failures still `throw`, as the header (`:6-7`) and `docs/distribution-validation.md:28` both say. DEV-009 measured `pwsh -File` returning exit 2 with the mutex held. Not repeated here, because the harness was excluded. | verified by source plus the DEV-009 measurement |
| ANALYSIS-206 | `GitReader.Worktrees` is gone, and `TryWorktrees` (`GitReader.cs:44`) is the only listing API. No other callers were found in tracked or untracked `.cs` files. | verified |

## Assessment and limitations

**Scope covered.** Every DEV-009 change named in the assignment, the retention lifecycle end to end, and the delta spec compared sentence by sentence. Clients: Claude Code and Codex. The path is shared with no provider branches, and the tests are Claude and Codex theories plus the mixed-session test.

**Delta spec against the implementation** (spec line 4):
- "When a branch change in a uniquely verified working checkout clears the work that verified it, that checkout SHALL remain …, labeled as retained": implemented by `branchCleared` (`IdentityResolver.cs:57`) and the vanished-folder case (`:86-90`). `CheckoutWork` is the record of "the work that verified it" for the vanished-folder case. For `branchCleared`, the verifying work is the one this pass mapped.
- "until a user pin, provider evidence or new work supersedes it": stronger tiers win in `Choose`, and `:84` releases the checkout on any `WorkSubject`.
- "SHALL NOT fall back to the launch checkout for that reason alone": matches.
- "A checkout still verified by a pinned change keeps its unique-work basis": `branchCleared` needs `WorkSubject is null`, and `:86` excludes `PinnedChange`.
- "A failed Git worktree listing SHALL NOT release …; a successful listing without it, a missing directory, clearing the checkout or a reset releases it": covered by `RetainedCheckout` (including a cached failure) and `exists` in `Choose`. The tray and CLI Clear both send `""` and so reach `Release`. Reset reaches it through `IdentityControls.cs:24`. A release that lands mid-pass now wins.
- The scenario at lines 33-36 matches.

**Complexity.** The lifecycle is now:
- four small `CheckoutPolicy` members;
- two cases in `RetainedAsync`;
- one locked decision of three lines.

Each part is used, the redundant second `Choose` keeps the tier order in `Core`, and no simplification is needed.

**Residual noted, not raised.** It is below the warning threshold, and a recheck raises no new suggestions. The ANALYSIS-203 check reads `ActiveWork` from the pass's snapshot. Suppose new unmapped work C is recorded after the snapshot (`:23`) but before the locked write (`:51`), in the same pass that first reads the switched branch. Then:
- `ApplyBranch` on the fresh record clears C, because C's `WorkBranch` is the stale `ObservedBranch` and the checkout is the same.
- The source becomes `retained`.

The window is one pass's read-to-write span, so it is narrower than the gap between passes that ANALYSIS-203 covered. In the `branchCleared` path, clearing C happened before this change too; only the `retained` label is new. The state corrects itself on C's next live hook evidence. If the orchestrator wants it closed, a bounded fix is to compute `retain` only while the fresh `WorkSubject` (before `ApplyBranch`) equals the snapshot's `work`.

**Measurements and runs.**
- No harness or Bench run (excluded by the assignment).
- The unit-test filter and the format verification ran against the current build, and both passed.

**Writes and identity.**
- The report write was not attempted: this client tells subagents to return reports as text and not to write report files, as with analysis-011. The full text is returned here for the orchestrator to save as `reports/analysis-012.md`.
- No repository file was written, and no real installation, data directory, `~/.claude`, `~/.codex` or tray was touched.
- The review identity was the same before and after the analysis.
- The orchestrator owns triage and ledger status.

> Saved verbatim by the orchestrator (Claude Code, session claude-release-gate-20261125); the reviewer did not attempt the write because the client refuses subagent report writes.
> Reviewer: ANALYSIS-011 code-analyzer subagent; client Claude Code (delegated subagent, auto mode); model claude-opus-5-5.
> Refusal message as reported for earlier reports: "Subagents should return findings as text". Known restriction: [Known client restrictions](../../../../docs/agent-workflow.md#known-client-restrictions).

# Analysis report: validate-next-release-field-acceptance / ANALYSIS-011

## Review target

- Reviewer / role: code-analyzer subagent for orchestrator session `claude-release-gate-20261125` / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (delegated subagent, auto mode; the version is not shown to the subagent) / `claude-opus-5-5`
- Date: 2026-11-25T22:39Z
- Change root / assignment: `openspec/changes/validate-next-release-field-acceptance` / ANALYSIS-011 (recheck of DEV-008, plus a lifecycle review)
- Task and requirement references:
  - [tasks.md](../tasks.md) 2.2, 2.3 and 2.8.
  - [session-identity delta](../specs/session-identity/spec.md), "Git branch and worktree context", including the two sentences the orchestrator added, and the scenario "Verified checkout changes branch after its work is cleared".
  - `openspec/specs/standalone-setup/spec.md:89`.
- Scope:
  - Product: `src/BellTower/Core/CheckoutPolicy.cs`, `Core/IdentityControls.cs`, `IO/GitReader.cs`, `Sessions/IdentityResolver.cs`, `Sessions/SessionCatalog.cs`, `Tray/OverviewForm.cs`.
  - Tests: `tests/BellTower.Tests/Sessions/CheckoutResolutionTests.cs`, `FieldAcceptance/SurfaceAndGateTests.cs`.
  - Harness: `tools/BellTower.FieldAcceptance/Evidence/RunReport.cs`, `Program.cs`, `Scenarios/PackagedHookBenchmarkScenario.cs` (`ContractGate`), `Scenarios/FiveConversationsScenario.cs`, `Harness/TowerReader.cs`, `scripts/field-acceptance.ps1`.
  - Evidence: [implementation-008](../evidence/implementation-008.md).
- Neighboring paths read:
  - `src/BellTower/Core/SessionIdentity.cs`, `Core/WorkPolicy.cs`, `Core/HistoryState.cs`, `Core/IdentityInheritance.cs`.
  - `src/BellTower/IO/CheckoutCache.cs`, `IO/IdentityStore.cs:81-99`.
  - `src/BellTower/Tray/TrayApplication.cs:196, 287-294, 335-369`, `src/BellTower/Cli/RenameCommand.cs:35`.
  - `tests/BellTower.Tests/Tray/OverviewControlTests.cs:52-109`, `docs/distribution-validation.md:28`, `docs/session-identity.md:59`.
- Baseline commit: `02d4f56`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 02d4f56 -Paths Directory.Build.props README.md vscode src scripts docs tests tools ZeusApps.BellTower.slnx openspec/changes/validate-next-release-field-acceptance/evidence openspec/changes/validate-next-release-field-acceptance/specs` printed `head=02d4f56 baseline=02d4f56 files=77 diff=37a336d277c8 untracked=61/273837cbd2d0`. This matches the assignment.
- Before/after identity: after the analysis the same command printed the same summary line. Nothing changed unexpectedly.
- Prior reports and rechecked findings: [analysis-010](analysis-010.md) ANALYSIS-180 to -185 and -187, and [qa-010](qa-010.md) QA-180 and QA-181. ANALYSIS-186 stays deferred and was not rechecked.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| How a checkout becomes retained, stays retained and is released | Traced `IdentityResolver.RefreshAsync` pass by pass. Listed every place that reads or writes `CheckoutSource`, `CheckoutPin`, `WorkingCheckout` and the work fields: the resolver, `Configure`, `Reset`, `WorkPolicy.Apply`/`Clear`, `HistoryState.Reset` and fork inheritance | A checkout becomes retained in two ways: `branchCleared` (`IdentityResolver.cs:54`) and the QA-181 case (`:74-77`). It stays retained through `:72-73`. It is released by a pin, provider evidence, any work subject, ambiguity, a listing without it, a missing directory, `Release` in `Configure` and `Reset`, or a successful listing without it. A transcript restart and fork inheritance do not touch it, which is correct | ANALYSIS-200, -201, -203 |
| Tray and CLI controls | Read `OverviewForm.cs:131-138, 217-221, 258-272` and the tray control test | The tray "Clear checkout" sends `null` (unchanged) for a conversation without a pin | ANALYSIS-200 |
| Concurrent writes | Compared the snapshot read at `IdentityResolver.cs:20` with the locked write at `:48-61` and `IdentityStore.Update:81-90` | The resolver writes `CheckoutSource` from its stale snapshot | ANALYSIS-201 |
| Regressions for identities that are not retained | Read `Worktrees` and its caching (`IdentityResolver.cs:105-116`), `GitReader.Run`, and the 2 s tray tick. Measured: `git worktree list --porcelain` in a directory outside Git exits 128 (git 2.51.0) | A failed listing is no longer cached, so every session launched outside Git now runs `git worktree list` on every pass | ANALYSIS-202 |
| 0.1.9 data | `SessionIdentity` schema and the conditions in `RetainedAsync` | No 0.1.9 source value equals `retained`. The QA-181 case needs `unique-work` plus `ActiveWork` plus `WorkBranch`, all of which 0.1.9 data can carry, and its result is the behavior the spec asks for. The source value used in the development tree only drops to launch (the DEV-008 note). No schema change | No new finding. ANALYSIS-186 is deferred |
| Delta spec, compared text by text | The requirement's new sentences against the code | The pinned-change sentence matches (`:54`, `:74`). The failed-listing sentence matches (`CheckoutPolicy.cs:96-99`; a failed listing is never cached). "Clearing the checkout or a reset releases it" does not hold for the tray's Clear (ANALYSIS-200) or under a race (ANALYSIS-201). The QA-181 case can retain for work that never verified the checkout, which goes beyond "clears the work that verified it" (ANALYSIS-203) | ANALYSIS-200, -201, -203 |
| Complexity | Read the double `Choose` call (`:35-37`) and the decision split between the snapshot and the locked update | The second `Choose` call always returns `(retained, Retained)`, because `RetainedAsync` already ensures the directory exists. It is redundant, but it keeps the tier order in `Core`. That is acceptable, so it is not a finding. Splitting the decision between snapshot and lock is the root cause of ANALYSIS-201 | The recommendation is folded into ANALYSIS-201 |
| Dead code | Searched for callers of `GitReader.Worktrees` | No callers remain | ANALYSIS-206 |
| Harness verdict and exit codes | `RunReport.cs:38-42, 50-67`, `Program.cs:42, 209-212, 320`, `field-acceptance.ps1:4-6, 35-38, 77-86`. Probe: a script that throws after a native exit 130, run with `pwsh -File`, exits 1. Run in-session, `$LASTEXITCODE` stays 130 | The console, summary and evidence verdicts are correct. `result.json` still says `passed: true`. The documented codes 2 and 130 do not reach `pwsh -File` callers | ANALYSIS-204, -205 |
| Contract gate | `PackagedHookBenchmarkScenario.cs:242-244, 263-275` and the test | Missing or rejected hooks, invalid controls and NaN estimates all fail the gate | ANALYSIS-183 verified |
| Parity | Searched the changed product code for provider branches | There are none; the path is shared. The new resolver tests run for both Claude and Codex, except three facts on the shared path. The harness check covers the Claude, Codex and mixed instances | Parity holds (both clients) |
| Unit tests | `dotnet test tests/BellTower.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~CheckoutResolutionTests\|FullyQualifiedName~FieldAcceptance\|FullyQualifiedName~IdentityRecovery\|FullyQualifiedName~IdentityControl"` (the binaries at 23:27 are newer than every source file, the newest at 22:28) | 187 passed, 0 failed | passed |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <13 DEV-008 C# files>` | exit 0 | passed |
| Harness | Not run | By assignment | not performed |

## Findings

### ANALYSIS-200: The tray's "Clear checkout" still does not release a retained checkout

- Severity: warning
- Related task / requirement: 2.8; delta spec "clearing the checkout or a reset releases it"; ANALYSIS-180
- Locations:
  - `src/BellTower/Tray/OverviewForm.cs:217-220`: the checkout box is seeded from `CheckoutPin ?? ""`.
  - `OverviewForm.cs:134`: the Clear handler.
  - `OverviewForm.cs:264`: sends `checkout.Text == originalCheckout ? null : checkout.Text`.
  - `src/BellTower/Sessions/SessionCatalog.cs:90-93`: `Release` runs only when `checkout is not null`.
- Observation:
  - A retained conversation has no pin, so the box and `originalCheckout` are both `""`. Clear leaves the box `""` and invokes `Configure(key, null, null, false)`, so `Release` never runs.
  - `tests/BellTower.Tests/Tray/OverviewControlTests.cs:93-104` only clears after "Apply" has set a pin.
  - The new resolver test calls `catalog.Configure(key, "", …)` directly (`CheckoutResolutionTests.cs:563`). It covers the CLI `rename --checkout ""` path, not the tray.
- Failure scenario (both providers):
  1. A conversation is retained on `D360-worktrees/DEP-7070`.
  2. The user selects it in the overview, which shows "checkout (retained: branch changed in the verified checkout)", and clicks "Clear checkout".
  3. The status says "Saved", but the next pass keeps the retained checkout. This is the first failure path in ANALYSIS-180.
  4. Only "Reset pins" (which also clears the name, change pin and color) or the CLI releases it.
- Proposed improvement and benefit: have the Clear button always send `""`, for example by invoking `ConfigureIdentity(editing, "", null, false)` from its handler instead of going through the unchanged-text comparison. The policy stays shared and the control is a thin adapter.
- Behavior-preservation risks: Clear on a pinned row must still send `""`, as it does today. Clear on a `unique-work` row is harmless, because `Release` touches only `retained`.
- Evidence type: source observation
- Verification / measurement plan: an `OverviewControlTests` case with a row that has `CheckoutPin = null` and `CheckoutSource = "retained"`: select it, click "Clear checkout", and expect `(key, "", null, false)`. Run it for a Claude row and a Codex row.
- Relationship: residual of ANALYSIS-180

### ANALYSIS-201: A resolver pass that overlaps Clear or Reset writes `retained` back, and it then sticks

- Severity: warning
- Related task / requirement: 2.8; delta spec "clearing the checkout or a reset releases it"
- Locations:
  - `src/BellTower/Sessions/IdentityResolver.cs:20`: snapshot read outside the lock.
  - `IdentityResolver.cs:36-37, 42`: retention decided from the snapshot, with awaited Git reads in between.
  - `IdentityResolver.cs:57`: `value.CheckoutSource = … source` written over fresh state.
  - `IdentityResolver.cs:72-73`: a persisted `retained` is read back as input.
  - `src/BellTower/Sessions/SessionCatalog.cs:92`, `src/BellTower/Core/IdentityControls.cs:24`, `src/BellTower/IO/IdentityStore.cs:81-90`.
- Observation:
  - Each update holds the identity lock, but the resolver writes a `source` it computed from a snapshot taken before awaits on `Worktrees`, `CheckoutCache.ReadAsync` and others. `CheckoutCache` is serialized with the title and native-name pumps, and a cache miss spawns four Git processes.
  - Before the retention work, a write from a stale snapshot was harmless, because every value was derived again on the next pass.
  - `retained` is now persisted state that the resolver reads back. If the stale snapshot said `retained`, the overwrite brings it back for good: the next pass sees `retained` with no work and keeps it.
- Failure scenario (both providers; the tray UI thread or the CLI process against the tray's thread-pool continuation):
  1. A resolver pass for a retained conversation reads the identity.
  2. While it awaits Git, the user clicks Reset pins or runs `rename --checkout ""`, and the source is set to `launch`.
  3. The pass then writes `retained` and the worktree.
  4. The user's release is silently lost until they repeat it.
  The window is each 2 s pass's read-to-write span for that session. How often it is hit is a hypothesis.
- Proposed improvement and benefit: decide retention against fresh state. In the update lambda, write `Retained` (from `source` or `branchCleared`) only while `value.CheckoutSource` still equals the snapshot's `identity.CheckoutSource` and `value.CheckoutPin is null`. Otherwise write `source == Retained ? Launch : source` and let the next pass derive it again. This keeps the whole lifecycle decision in one place against locked state.
- Behavior-preservation risks: the normal transition from unique-work to retained (snapshot `unique-work`, fresh `unique-work`) must still write `retained`. A concurrent pin must still win on the next pass.
- Evidence type: source observation; the frequency is a hypothesis
- Verification / measurement plan: a deterministic resolver test, run for both Claude and Codex. Build `new CheckoutCache(read: dir => { catalog.Configure(key, "", null, false); return new GitReader().ReadUncached(dir); })`, so the clear lands mid-pass on a retained conversation. Expect `launch` after this pass and after one more.
- Relationship: new; caused by retention being persisted and read back (ANALYSIS-180 fix scope)

### ANALYSIS-202: Failed worktree listings are no longer cached, so sessions launched outside Git run `git worktree list` on every 2 s pass

- Severity: warning
- Related task / requirement: 2.8 (ANALYSIS-181 fix); regression for identities that are not retained
- Locations:
  - `src/BellTower/Sessions/IdentityResolver.cs:105-116` (`:110-111`: "a failed listing is never cached") and `:27-29` (runs for every session without a pin).
  - `src/BellTower/IO/GitReader.cs:46-51, 79-82, 87`: returns null on a non-zero exit, empty output or a 2 s timeout.
  - `src/BellTower/Tray/TrayApplication.cs:287-294, 341-344`: a pass every 2 s, over sessions one at a time.
- Observation:
  - `git worktree list --porcelain` outside a repository exits 128 (measured, git 2.51.0), so `TryWorktrees` returns null on every call.
  - Before DEV-008 the empty result was cached for 30 s. Now each such session spawns a Git child on every overview pass, about 15 times more often.
  - The same applies to a listing that keeps timing out under load: every pass now waits up to 2 s for each affected session, one after another, where it used to wait once per 30 s. That lengthens the whole overview and presentation refresh. Machine load is exactly the condition ANALYSIS-181 was about.
- Failure scenario: Claude or Codex sessions started in a home or scratch folder, which is common, keep spawning `git` every 2 s for as long as the tray runs. Five sessions in a repository whose listing times out make each refresh take about 10 s.
- Proposed improvement and benefit: cache the failure as well, for example `(At, Paths: null)` with the same 30 s life or a shorter backoff such as 10 s. Retention already treats null as "no evidence", so caching it keeps the ANALYSIS-181 meaning. Alternatively, skip the listing when the cached `Toplevel(launch)` is null.
- Behavior-preservation risks: a cached failure must still keep a retained checkout while its directory exists, and must not be cached as an empty success.
- Evidence type: measured (Git exit code) plus source observation; the refresh-latency effect under load is a hypothesis
- Verification / measurement plan: a resolver test for a session launched outside Git that refreshes twice within 30 s and asserts one listing. This needs a `GitReader` seam, or a count of `ChildLaunchCount` through an injectable reader.
- Relationship: residual of ANALYSIS-181

### ANALYSIS-203: The QA-181 case can clear new work that never verified the checkout, and labels the checkout retained

- Severity: suggestion
- Related task / requirement: 2.8; delta spec "clears the work that verified it"; ANALYSIS-182
- Locations:
  - `src/BellTower/Sessions/IdentityResolver.cs:74-77`: checks only the persisted `unique-work` source, `ActiveWork` and `WorkBranch` against the checkout's branch.
  - `src/BellTower/Core/WorkPolicy.cs:58`: new work binds `WorkBranch = ObservedBranch`, which can be stale.
  - `src/BellTower/Core/CheckoutPolicy.cs:84-89`: the same-checkout branch change clears the work.
- Observation:
  - The persisted `unique-work` source does not record which work verified the checkout.
  - Suppose work B, which maps to no worktree (for example a change in an external planning store), is observed after a `git switch` in W but before the next pass. B's `WorkBranch` is then W's old branch.
  - The QA-181 case picks W, `ApplyBranch` clears B (same checkout, branch differs) and the source becomes `retained`.
  - Before DEV-008, the same pass fell back to launch and `ApplyBranch` rebound B to the launch branch, so B survived.
- Failure scenario (both providers):
  1. An agent runs `git switch -c next` in its ticket worktree and starts work on an external-store change B within about one 2 s pass.
  2. B is cleared and a branch floor is recorded, so B's history records from before the switch are never work again.
  3. The name changes to the new branch's subject, and W is shown as "retained".
  4. The state recovers on B's next live hook evidence.
- Proposed improvement and benefit:
  - Option 1: record the work that verified a `unique-work` checkout (one additive, optional identity field that 0.1.9 tolerates through `JsonExtensionData`), and require `ActiveWork` to equal it in `:74`.
  - Option 2: accept the edge case and say in the spec and in `docs/session-identity.md` that the QA-181 case is decided per pass.
  The orchestrator decides.
- Behavior-preservation risks: the QA-181 tests (`CheckoutResolutionTests.cs:652-692`) and the field layout must keep retaining.
- Evidence type: source observation; the timing window is a hypothesis
- Verification / measurement plan: a resolver test for Claude and Codex. Reach `unique-work` on worktree `alpha`, switch its branch, apply work on an unmapped change before refreshing, and refresh. The expected result follows the decision: B kept and the source `launch`, or the documented behavior.
- Relationship: new; side effect of the QA-181 fix

### ANALYSIS-204: `result.json` still reports `passed: true` for a run with unfinished items

- Severity: suggestion
- Related task / requirement: 2.2; QA-180
- Locations: `tools/BellTower.FieldAcceptance/Evidence/RunReport.cs:38, 50-67` (`passed = Passed`; no verdict or exit code in the JSON)
- Observation: the console, `summary.md` and the evidence file use `Verdict`, and the process exits 3. The machine-readable result gives `passed: true` plus an `unfinishedItems` count, which a reader has to combine on its own.
- Failure scenario: a 4.x gate or a later tool that reads `result.json` and gates on `passed` takes the Codex native-name parity gap as acceptance, which is the misreading QA-180 was meant to remove.
- Proposed improvement and benefit: add `verdict = Verdict` and `exitCode = ExitCode` to `ToJson`, and optionally document `passed` as "no failures".
- Behavior-preservation risks: none. The change only adds fields.
- Evidence type: source observation
- Verification / measurement plan: extend `AnUnfinishedItemIsCountedAsItsOwnOutcomeAndIsNeverAPass` to assert that the JSON contains `"verdict": "PASSED WITH UNFINISHED ITEMS"` and `"exitCode": 3`.
- Relationship: residual of QA-180

### ANALYSIS-205: The script's documented exit codes 2 and 130 do not reach `pwsh -File` callers

- Severity: suggestion
- Related task / requirement: 2.1 and 2.2 (QA-180 documentation)
- Locations:
  - `scripts/field-acceptance.ps1:4-6`: header.
  - `field-acceptance.ps1:35-38`: "another run active" is a `throw`, not exit 2.
  - `field-acceptance.ps1:86`: `throw` for any other non-zero code.
  - `docs/distribution-validation.md:28`: says the script stops "with exit code 2".
- Observation:
  - Probe: a script that throws after a native exit 130 exits 1 under `pwsh -File`. In-session, `$LASTEXITCODE` stays 130 alongside the exception.
  - The script's own mutex check throws before any native call, so even in-session `$LASTEXITCODE` is not 2.
  - Only 0 and 3 are reliable through the script.
- Failure scenario: a release-gate wrapper that tells "another run is active" (retry) apart from a failure (stop) by exit code never sees 2 or 130 and treats both as a failure. No false pass is possible.
- Proposed improvement and benefit: either `exit $code` for 1, 2 and 130 after printing the message (and `exit 2` at line 37), or document that the script reports 0 and 3 and that other outcomes are terminating errors.
- Behavior-preservation risks: in-session callers that rely on the exception would see an exit code instead.
- Evidence type: measured (throw-to-exit-code probe) plus source observation
- Verification / measurement plan: run `pwsh -File scripts/field-acceptance.ps1` while the mutex is held (QA, in disposable roots) and check that the exit code matches the documentation.
- Relationship: new; related to QA-180

### ANALYSIS-206: `GitReader.Worktrees` has no callers left

- Severity: suggestion
- Related task / requirement: 2.8 (ANALYSIS-181 fix)
- Locations: `src/BellTower/IO/GitReader.cs:43`
- Observation:
  - The only listing caller now uses `TryWorktrees` (`IdentityResolver.cs:109`). A search of all `.cs` files finds no other use.
  - The empty-on-failure wrapper is the shape that caused ANALYSIS-181.
- Failure scenario: a later caller picks the convenient wrapper, and a failed listing again reads as "no worktrees".
- Proposed improvement and benefit: remove `Worktrees`, leaving one listing API with explicit failure.
- Behavior-preservation risks: none; nothing references it.
- Evidence type: source observation
- Verification / measurement plan: Release build.
- Relationship: new

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-180 | `head=02d4f56 … diff=37a336d277c8 untracked=61/273837cbd2d0` | `CheckoutPolicy.Release` (`CheckoutPolicy.cs:28-32`) runs from `SessionCatalog.cs:92` and `IdentityControls.cs:24`. CLI `rename --checkout ""`, CLI `--reset` and tray "Reset pins" (`OverviewForm.cs:138`, reset=true) reach it. Test `ClearCheckoutAndResetReleaseARetainedCheckoutForGood` passed (Claude and Codex, clear and reset). The tray's "Clear checkout" sends `null` for a conversation without a pin and never reaches it, and a concurrent pass can undo a release | Still failing for the tray Clear (ANALYSIS-200). Race residual: ANALYSIS-201 |
| ANALYSIS-181 | same | `TryWorktrees` returns null on failure (`GitReader.cs:46-51`); the null is not cached (`IdentityResolver.cs:110-111`). `RetainedCheckout` keeps the checkout on null while its directory exists and releases it on a listing without it (`CheckoutPolicy.cs:96-99`). The pin, provider and unique-work tiers treat null as empty, as before. Both tests passed | Verified. Performance residual: ANALYSIS-202 |
| ANALYSIS-182 | same | `IdentityResolver.cs:72-73`: a retained checkout is offered only while `WorkSubject is null`. New unmapped work gives `launch`, and `ApplyBranch` rebinds it to the launch branch (test `AnyNewWorkReleasesTheRetainedCheckout`, asserts `WorkBranch == "main"`) | Verified. A related edge in the QA-181 case: ANALYSIS-203 |
| ANALYSIS-183 | same | `ContractGate` (`PackagedHookBenchmarkScenario.cs:243-244`) requires controls valid, planned = run = accepted, and both estimates below 300 ms. NaN fails. Used at `:273`, and the detail text says so (`:275`). The test covers missing, rejected, invalid controls, the limit and NaN | Verified |
| ANALYSIS-184 | same | `branchCleared` uses `WorkSubject` after `ApplyBranch` (`IdentityResolver.cs:54`). The QA-181 case excludes `PinnedChange` (`:74`). Test `APinnedChangeThatStillVerifiesTheCheckoutNeverFlipsTheLabelToRetained` passed for both providers | Verified |
| ANALYSIS-185 | same | `Retained = "retained"`, with the data-contract comment (`CheckoutPolicy.cs:15-19`). `Describe` is used by the only display site, `OverviewForm.cs:233`. `list` prints the raw value (`SessionCatalog.cs:35`). No VS Code or protocol consumer exists (searched). The test pins the value and its JSON | Verified |
| ANALYSIS-187 | same | `FiveConversationsScenario.cs:67-68, 226-230` requires `list` `checkoutSource` to equal `retained`, or `unique-work` for the pinned conversation, and prints it. `TowerReader.cs:7, 24` reads it. The installed run was not repeated here; DEV-008 reports that the first strict run failed, which shows the check is not vacuous | Verified by source |
| QA-180 | same | `RunReport.Verdict`/`ExitCode` (`RunReport.cs:39-42`) feed the console (`Program.cs:320`), the summary (`:75`) and the exit code (`Program.cs:212`). The script passes 3 through with a warning (`field-acceptance.ps1:81-85`). Tests pin 0, 1 and 3 | Verified. Residuals: ANALYSIS-204 (JSON), ANALYSIS-205 (other exit codes) |
| QA-181 | same | `RetainedAsync` second case (`IdentityResolver.cs:74-77`) plus `ApplyBranch` keeps the checkout when the switch removes the change folder, with three stable passes for both providers. A folder that vanishes without a branch change still falls back to launch (test) | Verified for the reported scenario. Edge: ANALYSIS-203 |

## Assessment and limitations

Scope covered:
- every DEV-008 product, test and harness change named in the assignment;
- how the retained checkout is created, kept and released, traced across all writers of the involved fields;
- the delta spec compared text by text, including the orchestrator's two sentences;
- regressions for identities that are not retained, and 0.1.9 data.

Clients: Claude Code and Codex. The product path is shared with no provider branches. Resolver tests run as Claude/Codex theories, and the mixed instances were checked by source.

Delta spec match: the two added sentences match the resolver exactly. Three exceptions:
- "clearing the checkout … releases it" fails from the tray's Clear button (ANALYSIS-200);
- the same release can be undone by a concurrent pass (ANALYSIS-201);
- the QA-181 case may retain for work that never verified the checkout (ANALYSIS-203).

Regressions:
- Sessions launched outside Git, and listings that time out, lost the 30 s listing cache (ANALYSIS-202).
- No functional regression was found for identities that are not retained, or for 0.1.9 data.
- ANALYSIS-186 stays deferred.

Complexity: the lifecycle now spans `CheckoutPolicy` (Choose, RetainedCheckout, Release, Describe), two cases in `RetainedAsync` and `branchCleared` in the locked update. The redundant second `Choose` call is acceptable. The real issue is that part of the decision is made on a snapshot outside the lock; ANALYSIS-201's fix moves the final decision onto locked state.

Decisions needed:
1. ANALYSIS-203: record the work that verified the checkout, or document the edge case.
2. ANALYSIS-205: pass exit codes through the script, or narrow the documentation.

Measurements and runs:
- No harness or Bench run was performed (by assignment).
- Measured: the Git exit code outside a repository, and how PowerShell maps a thrown error to the exit code. Both probes used scratch folders outside the repository.
- The refresh-latency effects in ANALYSIS-201 and -202 are hypotheses.

Writes and identity:
- No repository file was written. The report write was not attempted, because this client tells subagents not to write report files and refused earlier analysis reports. The full text is returned inline for the orchestrator to save verbatim as `reports/analysis-011.md`.
- The review identity was the same before and after the analysis.
- No real installation, data directory, `~/.claude`, `~/.codex` or tray was touched.
- The orchestrator owns triage and ledger status.

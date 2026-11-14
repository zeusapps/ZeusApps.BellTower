<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-036 / code-analyzer; Client: Claude Code delegated subagent, auto mode (exact version not exposed); Model: claude-opus-5-5; Refusal: the client refused the report write ("Subagents should return findings as text"), as reported by the reviewer. -->

ANALYSIS-036 (code-analyzer, light recheck) is done. The ANALYSIS-3301 partial-failure residual is closed. The fix is the smallest one possible, adds no second restore policy or shell matching, and creates no new wrong-shell write risk. The new test covers both providers, matched and mixed.

The fix cycle introduced one regression, ANALYSIS-3600: `tests/BellTower.Tests/Hosts/NativePresentationTests.cs` now has LF line endings and fails the format verify check. The fix is one formatter run, and it will change the review identity.

The client refused my write of `reports/analysis-036.md` ("Subagents should return findings as text"). I did not retry, so the full report is below for you to save.

```
status: done
role / client / model: code-analyzer / Claude Code (delegated subagent, auto mode; exact version not exposed) / claude-opus-5-5
task / finding IDs: recheck ANALYSIS-3301 (partial-failure residual), tasks 4.3/4.4; new ANALYSIS-3600 (regression)
changed or report paths: reserved openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-036.md (write refused; full report inline below)
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include src/BellTower/Hosts/TerminalTitlePump.cs -> passed; same check with --include tests/BellTower.Tests/Hosts/NativePresentationTests.cs -> failed (638 ENDOFLINE diagnostics, nothing else; ANALYSIS-3600); review-identity before/after -> passed (identical); build/test -> not run (QA runs concurrently; assignment forbids); developer mutant -> not rerun
target identity: before "head=1c80a7d baseline=1c80a7d files=35 diff=938883541ee6 untracked=10/042b0b79cd94"; after identical
limitations / decisions needed: the client refused the report write, so the full text is inline. Triage ANALYSIS-3600: the developer re-saves the file with CRLF via `dotnet format ... --include` on that one file, which changes the untracked hash, so QA's concurrent report may cite a superseded identity. The cost of the extra store listing per restore call is a hypothesis, not a measurement. No real host run.
next: orchestrator saves the report, pairs it with the QA recheck, routes ANALYSIS-3600 to the developer and decides whether ANALYSIS-3301 closes
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-036

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (delegated subagent, auto permission mode; this subagent cannot see the exact client version), `claude-opus-5-5`
- Date: 2026-11-14T09:33Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-036. This is a light recheck of the ANALYSIS-3301 partial-failure residual recorded in [analysis-035](analysis-035.md), plus regression in the touched code.
- Task and requirement references: tasks 4.3 and 4.4 ([tasks.md](../tasks.md)), design decision 5 ([design.md](../design.md)), developer evidence [implementation-049](../evidence/implementation-049.md)
- Scope and relevant neighboring paths: the 35 assigned paths. I read these in detail:
  - `src/BellTower/Hosts/TerminalTitlePump.cs:151-195` (`RestoreUnpresentedAsync`)
  - `tests/BellTower.Tests/Hosts/NativePresentationTests.cs:520-581`
  - neighbors `src/BellTower/IO/TitleRestoreStore.cs`, `src/BellTower/Hosts/TerminalTitleService.cs:10-84`, `src/BellTower/Hosts/AttachedConsoleTitleWriter.cs:25-39`, `src/BellTower/Hosts/IsolatedTerminalPresentationWriter.cs`, `src/BellTower/Core/TitleRestore.cs` and `src/BellTower/Core/ProcessIdentity.cs`
- Baseline commit: 1c80a7d (dirty tree)
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 1c80a7d -Paths <35 paths>` -> `Summary: head=1c80a7d baseline=1c80a7d files=35 diff=938883541ee6 untracked=10/042b0b79cd94`. This matches the assignment. Untracked hashes: `NativePresentationTests.cs` 895f06d71ea1..., `implementation-049.md` 24a9c4f4b403...
- Before/after identity: I ran the same command after the analysis and got `Summary: head=1c80a7d baseline=1c80a7d files=35 diff=938883541ee6 untracked=10/042b0b79cd94`. The two are identical, so nothing changed unexpectedly.
- Prior reports / rechecked findings: [analysis-035](analysis-035.md) (ANALYSIS-3301 partial-failure residual), [analysis-034](analysis-034.md), [analysis-033](analysis-033.md)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 3301 residual: partial failure in a two-shell group | Traced `RestoreUnpresentedAsync` before and after the `RestoreAsync` call, against `TerminalTitleService.Restore` | `TerminalTitlePump.cs:175-180`. The pump re-lists the store after every call, including the exception path (`done = false`). `restored` is the owner's records from the first listing whose shell has no record now. Receipts are forgotten for destinations whose `Desired.Shell` equals one of those shells. `done` now only selects the retry delay (`:181-189`). `Restore` removes a record only when it succeeds or when the shell is not live (`TerminalTitleService.cs:68-82`) | **Closed for the reported case.** When shell X is restored and shell Y fails, X's survivors lose their receipt and write again on the next refresh. Y's survivors keep theirs, and Y's record stays for the 30 s retry. |
| Smallest change / no second policy | `git diff 1c80a7d -- TerminalTitlePump.cs` and inspection | The only behavioral change is how `restored` is computed (was `done ? owner : []`, per the evidence mutant). The receipt-forgetting loop, the retry key, the log and the store are unchanged. `ITerminalPresentationWriter` and the helper protocol are unchanged | This is the smallest change. The alternative, having `RestoreAsync` report which shells it restored, would change the writer interface and the `sync-title --restore` process contract. There is no second restore policy, because restore decisions stay in `TerminalTitleService.Restore`. |
| Duplicated shell matching | Inspection | Both comparisons use `ProcessIdentity` value equality (`sealed record`, pid plus start ticks). This is the same identity the store keys on (`TitleRestoreStore.cs:13,15`) and the service uses | No duplicated matching logic. The `kept` check matches any record in the shell, not only this owner's. The store holds one record per shell, so the two are the same unless another writer took the shell in the meantime. In that case receipts are kept, which is the conservative choice and does not compete with the new last writer. |
| Wrong-shell / reused-shell write risk | Traced the effect of a forgotten receipt | Forgetting removes only a pump `Destination` and never writes. The rewrite goes through the existing `WriteAsync` -> `TerminalTitleService.Apply` path, which checks the stored attachment, agent and shell identity and that both processes are live (`TerminalTitleService.cs:27-35`). A dead or reused shell's record is discarded by `Restore`. Its identity cannot equal a live shell's identity, because the start ticks differ. If a destination still targets that dead identity, `Apply` refuses the write | **No new wrong-shell or reused-shell write.** The worst case is one extra write attempt, through the checked path, for a survivor in a shell whose record went away. |
| Neighbor: title restored but `ResetColors` failed (one record) | Read `AttachedConsoleTitleWriter.cs:31-39` and `Restore` | The record is kept, so the shell counts as not restored and its survivors keep their receipts. On the 30 s retry, `RestoreTitle` returns true (the console no longer shows the written title), the colors are reset and the record is removed. The re-list then forgets those receipts | This behavior predates the fix and matches the old `done`-gated behavior, so it is not a regression. The survivor's title is missing for at most one retry delay. I raised no new suggestion because this is a recheck. |
| Error path of the second listing | Read `TitleRestoreStore.List` | Unreadable files are skipped (`:44-49`). Only directory enumeration can throw, and the listing at the top of the method already had that exposure | No regression in practice. A transient enumeration failure exactly between the two listings would skip that pass's retry scheduling, and the next refresh would retry with no 30 s delay. This needs a failure inside a few milliseconds and is not worth a finding. |
| Performance | Inspection only, not measured | The fix adds one directory enumeration and N small JSON reads per `RestoreAsync` call. A call happens only for an unpresented owner outside its 30 s delay, which means a session end, a switch-off or an identity change, plus at most one call per 30 s for each failing owner. Each call already starts a `sync-title` helper process with a 3 s timeout (`IsolatedTerminalPresentationWriter.cs:16-17`). There is one record per shell that shows a Bell Tower title | **Hypothesis:** the cost is negligible next to the helper process launch it follows. A measurement plan, if one is wanted: time `List()` with about 50 record files in a disposable `--data-dir` and compare it with one `RestoreAsync` helper round trip. |
| Test structure | Read `NativePresentationTests.cs:520-581` | The test uses two real shells (the test process and a cmd helper) with one survivor in each. A synthetic ended owner `"ended"` owns both records, and restore fails only on the second shell. The cases are claude/codex, codex/claude, claude/claude and codex/codex, with modes InteractiveClient for Claude and EmbeddedRuntime for Codex. Assertions: exactly one restore. The failed shell's record is kept and still owned by `"ended"`. Exactly one extra write, and it lands in the restored shell, so the failed shell's survivor was not rewritten. The survivor records the restored title as its prior title. Nothing happens within 30 s. After 31 s the failed shell is restored and its survivor writes again. The fake clock means the test never actually waits | **Sound.** Both providers are covered, matched and mixed. The owner is provider-neutral, which matches the code: records carry no provider. Line 572 is an indirect assertion that passes whether the record is null or owned by someone other than `"ended"`. Line 574 then pins the survivor as the new owner, so the test still checks the outcome. I did not rerun the developer's mutant. |
| Regression: formatting | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <each changed file>` | `TerminalTitlePump.cs` returned exit 0. `NativePresentationTests.cs` returned exit 2 with 638 `ENDOFLINE` diagnostics and no other kind. A byte count shows 638 lines and 0 CR characters, against `.editorconfig:5` `end_of_line = crlf`. The same file passed this check in ANALYSIS-035 | **Regression**, see ANALYSIS-3600 |
| Build / tests | not run | QA runs them on the same tree at the same time, and the assignment forbids it | not performed |

## Findings

### ANALYSIS-3600: NativePresentationTests.cs was rewritten with LF line endings and fails the format verify check

- Severity: warning (fails a repository gate; no behavior impact)
- Related task / requirement: tasks 4.3 and 4.4, fix cycle 3 ([implementation-049](../evidence/implementation-049.md))
- Locations: `tests/BellTower.Tests/Hosts/NativePresentationTests.cs` (whole file, untracked, hash 895f06d71ea1...), `.editorconfig:5`
- Observation: every one of the file's 638 lines ends in LF only. `dotnet format --verify-no-changes --include` on this file exits 2 with one `ENDOFLINE` diagnostic per line and no others. ANALYSIS-035 ran the same check on this file and it passed, so the fix-cycle-3 edit converted the whole file. The local gate `./scripts/format.ps1 -Verify` and the `Formatting` step in `.github/workflows/validate.yml:33-35` run the same check. The local `core.autocrlf=true` and the lack of a `.gitattributes` file mean the committed blob would not differ. The working-tree verify still fails, and so does any checkout that does not convert line endings.
- Proposed improvement and benefit: the developer runs `dotnet format ZeusApps.BellTower.slnx --no-restore --include tests/BellTower.Tests/Hosts/NativePresentationTests.cs`, which only rewrites line endings, and then re-verifies. This restores a clean format gate for the review target.
- Behavior-preservation risks: none. Only line endings change. The untracked hash in the review identity will change, so QA and analysis evidence must cite the new identity.
- Evidence type: measured result (format verify exit code and diagnostic count, CR byte count)
- Verification / measurement plan: rerun `dotnet format ... --verify-no-changes --include tests/BellTower.Tests/Hosts/NativePresentationTests.cs` and expect exit 0.
- Relationship: new, a regression in this fix cycle. Not a duplicate.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-3301 (partial-failure residual: after a two-shell group partly fails, a survivor in the restored shell is not rewritten) | `head=1c80a7d baseline=1c80a7d files=35 diff=938883541ee6 untracked=10/042b0b79cd94` | Source trace of `TerminalTitlePump.cs:175-180`. The re-list after each call forgets receipts only for shells whose record is gone, whatever `done` is and on the exception path too. Matched shells keep their receipts. Retry and logging are unchanged. The pump-level test `APartialRestoreFailureRewritesTheSurvivorInTheRestoredShellOnlyAndRetriesTheFailedShell` (4 provider pairings) covers the group path that ANALYSIS-035 said had no coverage. I found no new wrong-shell or reused-shell write path, and no duplicated policy or matching. I did not build, run tests or rerun the mutant | **Verified.** The orchestrator decides closure. |

## Assessment and limitations

The ANALYSIS-3301 residual is resolved by a minimal change. The pump now checks the store itself, not the helper's overall result, to decide which shells were given back. It forgets receipts only for those shells, so restore policy and shell matching each stay in one place. Claude Code and Codex share this path, and the new test covers matched and mixed pairings with both providers' modes.

The fix cycle introduced one regression: the test file has LF line endings and fails the format verify check (ANALYSIS-3600). The fix is a single formatter run. It changes the review identity, so QA's concurrent report may cite a target the fix replaces.

The cost of the extra store listing is recorded as a hypothesis, not a measurement. Not performed: builds and tests (QA runs them concurrently), rerunning the developer's mutant, a real host run (trial 6.1), and performance measurement. The client refused the report write, so the report is returned inline. I wrote nothing else.

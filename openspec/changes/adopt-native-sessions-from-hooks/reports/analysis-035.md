<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-035 / code-analyzer; Client: Claude Code delegated subagent, auto mode (exact version not exposed); Model: claude-opus-5-5; Refusal: the client refused the report write ("Subagents should return findings as text"), as reported by the reviewer. -->

ANALYSIS-035 (code-analyzer light recheck, fix cycle 2) is done. The client refused the write of reports/analysis-035.md ("Subagents should return findings as text"). I did not retry, so the full report is below for you to save.

All three assigned residuals are verified:
- **3300, test comment:** verified. `MixedLaunchAndNativeTests.cs:92` now matches its assertions.
- **3300, task 4.3 text:** verified. `tasks.md:29` now states the decision 5 rule.
- **3301, two-shell resume:** verified for the reported defect. `Restore` now attempts every record the attachment owns, each on its own, and `FirstOrDefault` is gone.

One narrower gap remains under 3301. I don't think it blocks closure. It is not a regression, but it needs your triage. After a partial failure (the record in shell X is restored, the one in shell Y fails), a live session in shell X is not rewritten. The pump forgets same-shell receipts only when the whole group succeeds (`done == true`), and the retry group then holds only shell Y's record. No new finding IDs were raised.

```
status: done
role / client / model: code-analyzer / Claude Code (delegated subagent, auto mode; exact version not exposed) / claude-opus-5-5
task / finding IDs: recheck of ANALYSIS-3300 (2 residuals) and ANALYSIS-3301 (two-shell residual), tasks 4.3 and 4.4; no new IDs raised
changed or report paths: reserved openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-035.md (write refused; full report inline below)
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <TerminalTitlePump.cs, TerminalTitleService.cs, TitleRestoreStore.cs, NativePresentationTests.cs, MixedLaunchAndNativeTests.cs> -> passed; review-identity before/after -> passed (identical); build/test -> not run (QA runs concurrently; assignment forbids); developer mutants -> not rerun
target identity: before "head=1c80a7d baseline=1c80a7d files=34 diff=ea9a3f4298e1 untracked=9/b6c04d726961"; after identical
limitations / decisions needed: the client refused the report write, so the full text is inline. Triage: the ANALYSIS-3301 partial-failure gap (TerminalTitlePump.cs:173-181 forgets same-shell receipts only on a full group success). Narrow and not a regression; the bounded fix is to forget receipts for each shell whose record is gone after the call, whatever done returns. No pump-level test covers the two-shell group. No real host run, no measurements.
next: orchestrator saves the report, pairs it with the QA recheck, and decides whether the partial-failure gap blocks closure or is deferred
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-035

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (delegated subagent, auto permission mode; exact client version not exposed to this subagent), `claude-opus-5-5`
- Date: 2026-11-13T19:50Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-035 (light recheck, fix cycle 2: the residuals of ANALYSIS-3300 and ANALYSIS-3301 recorded in ANALYSIS-034, plus regression and new duplication in the touched code)
- Task and requirement references: tasks 4.3 and 4.4 ([tasks.md](../tasks.md)); design decision 5 ([design.md](../design.md)); developer evidence [implementation-048](../evidence/implementation-048.md)
- Scope and relevant neighboring paths: the 34 assigned paths. Read in detail: `src/BellTower/Hosts/TerminalTitlePump.cs`, `src/BellTower/Hosts/TerminalTitleService.cs`, `src/BellTower/IO/TitleRestoreStore.cs`, `tests/BellTower.Tests/Hosts/NativePresentationTests.cs:390-518`, `tests/BellTower.Tests/Hosts/MixedLaunchAndNativeTests.cs:92-97`, `tasks.md:29`. Callers checked by grep: `src/BellTower/Cli/SyncTitleCommand.cs:23`, `src/BellTower/Hosts/IsolatedTerminalPresentationWriter.cs:16`
- Baseline commit: 1c80a7d (dirty tree)
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 1c80a7d -Paths <34 paths>` -> `Summary: head=1c80a7d baseline=1c80a7d files=34 diff=ea9a3f4298e1 untracked=9/b6c04d726961` (matches the assignment)
- Before/after identity: the same command after the analysis -> `Summary: head=1c80a7d baseline=1c80a7d files=34 diff=ea9a3f4298e1 untracked=9/b6c04d726961`. Identical; no unexpected mutation.
- Prior reports / rechecked findings: [analysis-034](analysis-034.md) (residuals of ANALYSIS-3300 and ANALYSIS-3301), [analysis-033](analysis-033.md)

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 3300 residual 1: stale test comment | Read the comment and the assertions it describes | `MixedLaunchAndNativeTests.cs:92` now reads "native sessions get a title and their own palette background"; line 97 asserts `NotNull(write.Background)` | Fixed. The comment matches the assertion. |
| 3300 residual 2: task 4.3 acceptance text | `git diff 1c80a7d -- tasks.md` and read line 29 | "exit restores only what Bell Tower changed" is replaced by "on exit or switch-off the prior title is restored and a background Bell Tower wrote is reset to the terminal default (design decision 5)" | Fixed. It agrees with decision 5 and the spec. The "only while the console still shows Bell Tower's title" condition is covered by the reference to decision 5. |
| 3301 residual: one attachment owning records in two shells | Traced `OwnedBy`, `Restore`, the pump's `RestoreUnpresentedAsync`, and the `sync-title --restore` caller. Grepped for `ForAttachment` | `TitleRestoreStore.cs:18`, `TerminalTitleService.cs:61-84`, `TerminalTitlePump.cs:151-192`; no `ForAttachment` hits remain | Fixed for the reported defect. Every owned record is attempted under one lock hold. A failure keeps only its own record and returns false. A dead or reused shell discards only its own record. The pump makes one request per owning attachment and retries with the existing 30 s per-owner delay; the retry only sees the records still left. One narrower gap remains in the partial-failure path (see Re-verification). |
| QA-4100 coverage (context only, owned by QA) | Read the new tests | `NativePresentationTests.cs:415-480`; both provider orders and both modes | The surviving-session rewrite and the failure-then-retry path are now driven through the pump with the real service behind the writer boundary. The two-shell case is tested at service level only (`NativePresentationTests.cs:485-518`); no pump test covers the per-group receipt forgetting across two shells. |
| Regression | Read the touched paths | `Restore` with no owned record returns true (empty loop), as before. `SyncTitleCommand.cs:23` exit-code contract unchanged. The `ResetColors` failure path is unchanged | No regression found. Claude Code and Codex share one path; the new tests are matched for both. |
| Duplication / complexity | Inspection | `OwnedBy` is a filter over the existing `List()`. The pump groups the list it already reads. No second record format or policy copy was added | No new duplication. Cost: each unpresented owner causes one extra directory read under the lock. This is bounded by the record count, and records exist only for shells that currently show a Bell Tower title. Not measured. |
| Deferred 3302 to 3305 | Checked whether the fix made one worse | The per-attachment `restoreRetry` key (3304) no longer blocks other records, because the service now attempts all of them | None got worse. The 3304 interaction noted in ANALYSIS-034 is removed. |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <the 3 touched source files and 2 touched test files>` | exit 0 | passed |
| Build / tests | not run | QA runs them on the same tree at the same time, and the assignment forbids it | not performed |

## Findings

**No findings.** This is a recheck, so no new IDs are raised. The remaining item is recorded under the rechecked ID below.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-3300 (residual 1, test comment) | `head=1c80a7d ... diff=ea9a3f4298e1 untracked=9/b6c04d726961` | `MixedLaunchAndNativeTests.cs:92` now matches the assertions at lines 93 and 97 | **Verified.** |
| ANALYSIS-3300 (residual 2, task 4.3 text) | same identity | `tasks.md:29` now states the decision 5 rule: the title is restored and a background Bell Tower wrote is reset, never restored. Task 4.3 is still unchecked, which keeps the "assumed until trial 6.1" label in the matrix and docs consistent | **Verified.** |
| ANALYSIS-3301 (residual: resumed attachment with records in two shells, only the first restored) | same identity | `TerminalTitleService.Restore` (`:61-84`) loops over `OwnedBy(attachmentId)` and handles each record independently. `FirstOrDefault` is gone. The service test `AResumedAttachmentThatOwnsRecordsInTwoShellsRestoresEachIndependently` (claude, codex) covers this with a second live shell. The developer reports that a first-record-only mutant failed it. I did not rerun that mutant. The pump forgets receipts for sessions in every shell of the group after a full success (`TerminalTitlePump.cs:173-181`). **Remaining narrow gap** (source observation, not reproduced, not blocking in my judgment). The receipts are forgotten only when the whole group restored (`done == true`). Take a partial failure: the record in shell X is restored, the record in shell Y fails, and `Restore` returns false. A session still presented in shell X then keeps its receipt, so it is not rewritten. On the retry the group holds only the record in Y, so the session in X is still not rewritten, and shell X shows the prior title and default colors until that session's desired title changes. This needs all of: a resume into another shell, a second live session in the old shell that was not the last writer, and an unverifiable console in the new shell. A bounded fix: forget the receipts of same-shell sessions for each record that is gone after the call (compare the owner's shells before and after), whatever `done` returns. Neither the pump test nor any other test covers the two-shell group path through the pump | **Verified** for the reported defect (only the first record was restored; the retry key blocked the others). One narrower partial-failure gap remains for the orchestrator to triage. It is not a regression: before this fix the second shell was never restored at all. |

## Assessment and limitations

Fix cycle 2 resolves all three assigned residuals. Both ANALYSIS-3300 texts now agree with design decision 5. ANALYSIS-3301's two-shell edge case is fixed in the service: every owned record is restored independently, and the pump issues one request per owner. The change adds no duplication or new policy copy and causes no regression. Claude Code and Codex share the path, and the new tests are matched for both providers.

For orchestrator triage, recorded under ANALYSIS-3301 rather than raised as a new finding: after a partial restore failure in a two-shell group, a surviving session in the restored shell is not rewritten. It is narrow and not a regression; a fix is sketched in Re-verification. Deferred ANALYSIS-3302 to 3305 were not reopened, and none got worse.

Not performed: builds and tests (QA runs them concurrently), rerunning the developer's mutants, any real host run (trial 6.1), and performance measurements. The client refused the report write, so the report is returned inline. I wrote nothing else.

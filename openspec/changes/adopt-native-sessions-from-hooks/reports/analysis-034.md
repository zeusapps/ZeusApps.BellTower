<!-- Saved by the orchestrator verbatim from the reviewer's inline return. Reviewer: ANALYSIS-034 / code-analyzer; Client: Claude Code 2.1.323 delegated subagent, auto mode; Model: claude-opus-5-5; Refusal: the client refused the report write ("Subagents should return findings as text"), as reported by the reviewer. -->

ANALYSIS-034 (code-analyzer recheck) is done. The client refused the write of reports/analysis-034.md ("Subagents should return findings as text"). I did not retry, so the full report is below for you to save.

ANALYSIS-3300 is verified for the design, spec, code, matrix, doctor and docs: all of them now state one rule. Two small texts are still stale: a test comment and the task 4.3 acceptance line.

ANALYSIS-3301 is verified: the order-dependent restore and both scans are gone. The developer's added rewrite after a restore is correct and adds no duplication. Two follow-ups are open: no pump-level test covers that rewrite, and a resume into another shell can leave one attachment owning two records.

No new finding IDs were raised.

```
status: done
role / client / model: code-analyzer / Claude Code 2.1.323 (subagent, auto mode) / claude-opus-5-5
task / finding IDs: recheck ANALYSIS-3300, ANALYSIS-3301 (tasks 4.3, 4.4, 5.2, 5.3); no new IDs raised
changed or report paths: reserved openspec/changes/adopt-native-sessions-from-hooks/reports/analysis-034.md (write refused; full report inline below)
checks: dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <27 target C# files> -> passed; review-identity before/after -> passed (identical); build/test -> not run (QA runs concurrently; assignment forbids)
target identity: before "head=1c80a7d baseline=1c80a7d files=33 diff=225f5a8ef6f1 untracked=8/1705f41fb8c1"; after identical
limitations / decisions needed: the client refused the report write, full text inline. Triage needed: (1) 3300 residuals: stale comment at tests/BellTower.Tests/Hosts/MixedLaunchAndNativeTests.cs:92 and the task 4.3 acceptance text at tasks.md:29; (2) 3301 follow-ups: a pump-level test of the surviving-session rewrite, and one attachment owning records in two shells after a resume (FirstOrDefault plus the per-attachment retry key). No real host run, no measurements.
next: orchestrator saves the report, pairs it with the QA recheck and triages the residuals
```

---

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-034

## Review target

- Reviewer / role: code-analyzer subagent / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.323, delegated subagent in auto permission mode, `claude-opus-5-5`
- Date: 2026-11-13T19:40Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-034 (recheck of ANALYSIS-3300 and ANALYSIS-3301 plus regression in the touched code)
- Task and requirement references: tasks 4.3, 4.4, 5.2, 5.3 ([tasks.md](../tasks.md)); design decisions 5 and 8 ([design.md](../design.md)); delta spec [session-identity](../specs/session-identity/spec.md) "Presentation ownership for adopted terminals"; developer evidence [implementation-047](../evidence/implementation-047.md)
- Scope and relevant neighboring paths: the 33 assigned paths, plus the orchestrator-revised `design.md` and `specs/session-identity/spec.md`. Neighbors read for context only: `src/BellTower/IO/AttachmentStore.cs` (shell replacement on resume), `openspec/changes/adopt-native-sessions-from-hooks/tasks.md`, the other delta specs (grep for color and restoration rules)
- Baseline commit: 1c80a7d (dirty tree)
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 1c80a7d -Paths <33 paths>` -> `Summary: head=1c80a7d baseline=1c80a7d files=33 diff=225f5a8ef6f1 untracked=8/1705f41fb8c1` (matches the assignment)
- Before/after identity: the same command after the analysis -> `Summary: head=1c80a7d baseline=1c80a7d files=33 diff=225f5a8ef6f1 untracked=8/1705f41fb8c1`. Identical; no unexpected mutation.
- Prior reports / rechecked findings: [analysis-033](analysis-033.md), ANALYSIS-3300 and ANALYSIS-3301

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| ANALYSIS-3300: one rule across artifacts | Read the diffs of `design.md` and `specs/session-identity/spec.md` against 1c80a7d. Compared them with `NativePresentation.cs`, `TerminalTitleService.cs`, `AttachedConsoleTitleWriter.cs`, the matrix row (`CapabilityMatrix.cs:104-106`), the doctor text (`DiagnosticsReporter.cs:89`) and the docs (`attachment-health.md:26-28`, `compatibility.md:80,94`, `installation.md:80`) | Design decision 5 (`design.md:96`) and decision 8 (`design.md:120`), spec requirement (`spec.md:21`) and scenarios (`spec.md:29,39-45`) | All of them now state the same rule. The title is restored on the recorded shell only, and only while the console still shows Bell Tower's title. A background Bell Tower wrote is reset with OSC 110/111. The prior color is never read. A gone or reused shell is discarded without a write. Two residual texts remain (see Re-verification). |
| ANALYSIS-3300: stale title-only wording | `Grep` over the repository for `GrantsTitle`, "restores only what", "leave ... surface unchanged", "prior color", "never a background" | `GrantsTitle` has no remaining hits. `tests/BellTower.Tests/Hosts/MixedLaunchAndNativeTests.cs:92` and `tasks.md:29` still have hits | Rename complete. One stale test comment and one task acceptance criterion remain. |
| ANALYSIS-3301: per-shell record | Traced `WriteNative`, `Restore`, `TitleRestoreStore`, the pump's `RestoreUnpresentedAsync`, and `sync-title --restore` | `TerminalTitleService.cs:42-80`, `TitleRestoreStore.cs:12-18`, `TerminalTitlePump.cs:151-191`, `SyncTitleCommand.cs:21-24` | The order dependence is gone and both scans are removed. Records are bounded by the shells that currently show a Bell Tower title. One residual edge case: an attachment that owns two records (see Re-verification). |
| ANALYSIS-3301: added pump behavior | Walked through sessions S1 and S2 in shell X: owner ends, non-owner ends, switch-off with both live, switch back on, a managed session sharing the shell | `TerminalTitlePump.cs:172-180` | Correct and needed. Without it a surviving session's receipt would stop it rewriting after the restore, so the shell would keep the prior title and default colors. The behavior also fixes switch-off followed by switch-on for a session that does not own the record. No new duplication: it reuses the existing `destinations` map. No test drives it through the pump. |
| Regression | Read the touched code paths and tests at the reviewed identity | `NativePresentationTests.cs:323-401`, `MixedLaunchAndNativeTests.cs:78-97` | No behavioral regression found. Claude Code and Codex share one path. The matched and mixed tests cover both providers (`interactive-client` and `embedded-runtime`, both write orders). |
| Deferred 3302 to 3305 | Checked whether the fix made any of them worse | `restoreRetry` is still keyed by attachment (`TerminalTitlePump.cs:157,183`) | 3302 is partly addressed (rename). 3303 and 3305 are unchanged. 3304 is unchanged, except that its per-attachment retry key now interacts with the two-record edge case below. |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include <27 C# files in the target>` | exit 0 | passed |
| Build / tests | not run | QA runs them on the same tree at the same time, and the assignment forbids it | not performed |

## Findings

**No findings.** This is a recheck, so no new IDs are raised. The residual items are recorded under the rechecked IDs below.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-3300 | `head=1c80a7d ... diff=225f5a8ef6f1 untracked=8/1705f41fb8c1` | Decision 5 (`design.md:96`) now states the rule. The title is restored only on the recorded shell and only while Bell Tower's title is shown. The background is reset with OSC 110/111 only when a color was written. The accepted trade-off is recorded: a hand-set background becomes the profile default. Decision 8 (`design.md:120`) and the spec requirement (`spec.md:21`) say the same. The scenario "Native presentation is on" (`spec.md:29`) replaces "restores only what it changed". "No safe restoration mechanism" is replaced by "Simultaneous native sessions are told apart" and "Restoration target is gone or reused" (`spec.md:39-45`). The code matches: `TerminalTitleService.Restore` and `AttachedConsoleTitleWriter.RestoreTitle` skip the title when it is no longer shown, `ResetColors` runs only when `Colored` is set, and `TitleRestore.cs:10` documents it. The matrix (`CapabilityMatrix.cs:105-106`), the doctor (`DiagnosticsReporter.cs:89`), `attachment-health.md:28`, `compatibility.md:80,94` and `installation.md:80` state the same rule and label the host effect of the reset as assumed until trial 6.1. `GrantsPresentation` replaces `GrantsTitle` everywhere. The comments listed in 3300 are corrected. Residual 1: `tests/BellTower.Tests/Hosts/MixedLaunchAndNativeTests.cs:92` still says "native sessions get a title and never a background", and line 97 asserts the opposite (`Assert.NotNull(write.Background)`). This is the same kind of stale comment that 3300 named at `NativePresentationTests.cs:101`. Residual 2: the acceptance text of task 4.3 (`tasks.md:29`, "exit restores only what Bell Tower changed") was not revised. It still reads as restoring the background, while decision 5 says the background is reset and not restored. Note, not a contradiction: the spec and decision 5 also say a surface becomes writable only when its restoration is "verified", while the matrix honestly labels the host reset as assumed. That holds together only while task 4.3 stays open until trial 6.1 confirms the reset. | Verified for design, spec, code, matrix, doctor and docs. Two residual texts remain: one test comment and the task 4.3 criterion. The orchestrator decides whether they block closure. |
| ANALYSIS-3301 | same identity | **Correctness.** The store has one file per shell (`ContentHash(pid:startTicks)`, `TitleRestoreStore.cs:13`), and `Get` rechecks `value.Shell == shell`. `WriteNative` reuses `Previous` from the existing record or reads the title once. It makes the writer the owner and keeps `Colored` sticky (`TerminalTitleService.cs:42-53`). So `Written` is always the last native write to that shell, and the switch-off case restores once in either write order. `SwitchingOffTwoLiveSessionsInOneShellRestoresThePriorTitleOnceWhicheverWroteLast` covers this for Claude Code and Codex. `Restore` resolves the record through `ForAttachment` under the presentation lock. When the owner changed after the pump listed the records, `Restore` finds nothing and writes nothing, which is safe. A gone or reused shell is discarded, and a failure keeps the record. **Simplicity.** The inheritance scan, the scan over every attachment and the "another live session" rule are gone. `ForAttachment` lists only the records that exist now, which is bounded. The old `title-restore` folder is never read. That is acceptable for unreleased dev data, but those dev-build titles are never restored and the folder stays. **Added pump behavior** (`TerminalTitlePump.cs:172-180`). After a successful restore, every destination whose desired shell equals the record's shell loses its receipt and writes again. `WriteNative` then reads the restored title as the new prior title. This is correct: the restore reset that shell's title and colors, including a managed session that shared it. It is the simplest fix and adds no duplication. Its cost is a brief title and color flap in the shell, and a session in backoff in that shell loses its backoff. Coverage gap: no test drives this path through the pump. The pump tests use a single native session, and the service-level tests do not involve the pump. A mutant that deletes the loop would very likely survive. **Residual edge case** (source observation, not reproduced). A resume can move an attachment to a new shell (`AttachmentStore.cs:69,82-85`). The attachment can then own records in two shells. While it stays live, the old shell keeps Bell Tower's title and color, because its owner is still presented. When it ends, `ForAttachment` returns only `FirstOrDefault`. The pump's `restoreRetry` is keyed by attachment ID. If the first record keeps failing on a live shell whose console cannot be verified, the other record is never attempted. The bounded fix is to have `Restore(attachmentId)` handle every record that attachment owns, or to have the pump address records by shell. | Verified for the reported order dependence and the scans. The orchestrator should consider two follow-ups: a pump-level test of the surviving-session rewrite, and the two-record edge case after a resume into another shell. |

## Assessment and limitations

The fix cycle resolves the substance of both findings. The design, spec, code, matrix, doctor and docs now state one rule for adopted direct sessions. The title is restored on the recorded shell only, and only while Bell Tower's title is shown. The background Bell Tower wrote is reset to the terminal default and never restored. The shell-keyed record removes the order-dependent restore and both scans, without new duplication. The added rewrite of a surviving session in the same shell is correct and minimal. Claude Code and Codex share one path. Matched tests cover both providers and both write orders.

Items for orchestrator triage, recorded under the existing IDs and not raised as new findings:
- ANALYSIS-3300 residuals: the stale comment at `MixedLaunchAndNativeTests.cs:92`, and the task 4.3 acceptance text "exit restores only what Bell Tower changed".
- ANALYSIS-3301 follow-ups: a pump-level test for the rewrite after a restore in a shared shell, and the edge case where one attachment owns records in two shells after a resume.

Deferred ANALYSIS-3302 to 3305 were not reopened. None got worse, except that the per-attachment retry key from 3304 now interacts with the two-record edge case.

Not performed: builds and tests (QA runs them concurrently), any real host run (trial 6.1), and performance measurements. The client refused the report write, so the report is returned inline. I wrote nothing else.

<!-- Saved verbatim by the orchestrator (session claude-bound-state-20261121) on 2026-11-21: the client refused the code-analyzer's native report write and the subagent returned the full text inline. -->

# Analysis report: bound-ended-session-state / ANALYSIS-004 (recheck of DEV-004, fix cycle 3)

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-004) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (this subagent cannot see the client version; earlier reports record 2.1.330), subagent mode (auto), model `claude-opus-5-5`
- Date: 2026-11-21
- Change root / assignment: `openspec/changes/bound-ended-session-state` / ANALYSIS-004 (recheck)
- Task and requirement references:
  - task 2.1 in [tasks.md](../tasks.md)
  - design decision 4 (revised 2026-11-21) and decision 5 in [design.md](../design.md)
  - requirement "Bounded monitoring state" in [specs/session-tracking/spec.md](../specs/session-tracking/spec.md): "a bounded, sanitized reason without one entry per tick", "Doctor SHALL report ... any persistent refresh failure", and the scenarios "Live sessions alone exceed the budget" and "Doctor reports state health"
  - developer evidence [implementation-004.md](../evidence/implementation-004.md)
- Scope and relevant neighboring paths:
  - `src/BellTower/IO/RefreshHealth.cs` (all of it)
  - `tests/BellTower.Tests/IO/RefreshHealthTests.cs` (all of it)
  - Neighbors read: `src/BellTower/Tray/TrayApplication.cs` (`Tick`, lines 242-304, and the 250 ms WinForms timer at line 19), `src/BellTower/Diagnostics/StateReporter.cs` (the persistent rule at lines 8 and 38), `src/BellTower/IO/AtomicFile.cs`, `src/BellTower/IO/Json.cs`, and the refresh paragraph of `docs/architecture.md` (line 31)
- Baseline commit: 5e95554
- Implementation identity (before): `head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/e2351c042def`
  - Tracked diff SHA-256: `87172e79b1189eadf5bce8fcb79b2762db7b54e4e886fe9cf83b3e4af61b6a7f`
  - Untracked: `RefreshHealth.cs` 4e6a501a…, `RefreshHealthTests.cs` 2a4f2bb1…. The other five untracked hashes are unchanged from analysis-003.
- Before/after identity: after the analysis the same command printed `head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/e2351c042def`. It matches the before identity and the assigned target, so there was no unexpected mutation.
- Prior reports / rechecked findings: [analysis-003.md](analysis-003.md), ANALYSIS-200 and ANALYSIS-201

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Probe setup | `RefreshHealth.cs` and `Json.cs` were copied byte-identical (`cmp`, SHA-256 4e6a501a…) to a scratch console outside the repository. Small stubs stood in for `DataPaths`, `LocalLog` (it records lines in memory) and `AtomicFile` (it counts writes). A fake `TimeProvider` drove the clock. The probe ran with a scratch data directory and `BELLTOWER_DATA_DIR` pointed at scratch. | The real `Describe`, `Failed`, `Succeeded`, `Read` and generated regexes ran. Reflection called the private `Quoted()` and `PathPattern()` to attribute cost. | No real data directory, installation or tray was used. Nothing in the probe is committed. |
| Sanitizer correctness (ANALYSIS-200) | `Describe` on 21 synthetic messages | All the ANALYSIS-200 cases are masked: `Bob's app` quoted and unquoted, `Smith's Project`, `E:\codex-home\Bob's\sessions`, two quoted paths on one line, a UNC path and a Unix path with `Bob's`, `O'Neil repo` inside a sharing-violation message, `'90s` when quoted, `Bob's2`, a close followed by `:` or `!`, and a quoted path with spaces. Paths that still leak are listed under ANALYSIS-300. | The fix is correct for an apostrophe followed by a letter, and inside quotes for one followed by a digit. An apostrophe followed by a space still leaks the rest of the path, and so does an unquoted one followed by a digit (ANALYSIS-300). |
| Regex backtracking cost | Describe timed on 13 adversarial 2,000-character inputs and a 20,000-character input, which `Describe` truncates to 2,000. Each regex was timed alone. Scaling was measured at 250, 500, 1,000 and 2,000 characters. For comparison only, the probe also timed an approximation of the pre-DEV-004 quoted rule, whose inner segments stop at any apostrophe. | Worst case: `\'a` repeated, about 150 ms per `Describe`. `Quoted()` takes 50-125 ms of that. `PathPattern()` stays at 0.1 ms or less, and the approximated old rule at about 0.02 ms. Benign inputs (2,000 `x`, a long path with spaces, 1,999 backslashes) take under 1 ms. A 260-character path with dozens of apostrophes takes 0.006 ms. Cost grows with length because `{0,300}?` caps each candidate opening. | This is new complexity from DEV-004: inner segments may now cross apostrophes, and every apostrophe not after a letter or digit can open a match. The cost is bounded and polynomial. It needs a crafted message over about 1,000 characters with hundreds of `'`, letter and separator triples. `Quoted` has no match timeout and runs on every failed tick on the UI thread, before the limiter. The cost is measured but not a blocking finding: no realistic IO message reaches it, and MAX_PATH-length text costs about 2 ms. |
| Limiter bounds (ANALYSIS-201) | Fake clock with 250 ms ticks for 30 minutes. Plans: strict alternation and 1 failure in 4 with one reason; alternation with 8 and 12 rotating reasons and with unique reasons; 2 failures and 1 success with 3 reasons; and seeded random mixes (p = 0.5 with 5 reasons, and p = 0.9 with about 1,000 reasons, seeds 1-3). The probe reported the most lines and record writes in any sliding 10-minute window. | One reason, alternation or 1 in 4: 2 lines and 2 writes per 10 minutes, against 2,400 lines before DEV-004. 8 rotating reasons: 16 lines and 16 writes. 12 or unique reasons: 17 lines and 16 writes. Random mixes: 9-12 lines and 3-10 writes. A continuous failure alone: 1 line and 1 write. | Bounded for every mix. Failure lines stay at 9 or fewer per 10 minutes, and each recovery line pairs with a logged failure, so the total is at most 2 × 8 + 1 = 17. That is within the spec's "without one entry per tick". It is about twice the "same bound as a continuous failure" in decision 4 and the "about nine lines" in `docs/architecture.md`, because the docs count failure lines only and recovery lines come on top. This is not a blocking finding. |
| Record correctness for doctor | The same runs, checking after every tick (1) that the record never shows a failure after a success, (2) that a recorded `failingSince` always equals the start of the current failure window, and (3) how long the current window has run while the record shows no failure. Added runs: a logged failure, one success, then a continuous failure of the same reason, of a new reason, at 250 ms and at 2 s ticks. Restart runs. | (1) 0 stale failures in every run. (2) 0 wrong `failingSince` values. (3) A continuous failure of the same reason after a logged, recovered one stays unrecorded for 598 s (594 s at 2 s ticks). After a 5-minute logged failure the gap is 300 s. A new reason is recorded at once (0 s). Random mixes leave failures unrecorded for up to 22 s. Restarts: a new run's first failure rewrites the record with the new window, and a new run's first success recovers an earlier run's failure with one recovery line and no extra write on a healthy record. | Doctor never shows an ended failure, and every recorded window is correct. But a failure that persists after a flap stays invisible to doctor for up to 10 minutes, against the 1-minute persistence rule (ANALYSIS-301). |
| Regression in DEV-004 code | Reading the code. Lines 76-82: `&&` binds tighter than `\|\|`, which is the intended grouping. Lines 89-96: the first-success read of an earlier run's record. Lines 104-116: invariants. The 3 new tests were also read. | `recordShowsFailure` implies `windowLogged` at every point. A failed `Persist` is still swallowed, as before. Line 76 never sees a null `lastPersisted` while `recordShowsFailure` is true. `FailuresInterleavedWithSuccesses...` exercises only one reason, so it does not catch the 17-line total. `AFailureStillFailing...` (lines 377-417) asserts the unrecorded continuing failure at `Start+4s` that ANALYSIS-301 describes. | Apart from ANALYSIS-301, no other regression. |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include src/BellTower/IO/RefreshHealth.cs tests/BellTower.Tests/IO/RefreshHealthTests.cs` | exit 0 | passed |
| Build / suite | Not run | QA owns the build and test run. A concurrent build in the repository would interfere. | not run |
| Parity | Code reading | No provider branch was added. The policy is shared by the Claude Code and Codex refresh paths, which are the shared drain. | Parity holds. Both findings affect both providers equally. |

## Findings

### ANALYSIS-300: An apostrophe followed by a space, or an unquoted one followed by a digit, still ends the path early and leaks the rest

- Severity: warning (content safety; low likelihood). It blocks only under design decision 4's "including a path that contains an apostrophe".
- Related task / requirement: 2.1; design decision 4 ("every path replaced, including a path that contains an apostrophe"); spec "a bounded, sanitized reason"
- Locations: `src/BellTower/IO/RefreshHealth.cs` at the reviewed identity:
  - `:203`: `Quoted` accepts any apostrophe followed by whitespace as the closing quote
  - `:207`: `PathPattern` allows an apostrophe in a segment only when a letter follows (`'(?=\p{L})`)
  - `:183`: `PathTail` follows the same letter-only rule
- Observation: probe output, with synthetic names:
  - `Could not find file 'D:\Clients\Jones' Project\notes\plan.md'.` becomes `... '<path>' Project\notes\plan.md'.`
  - `Could not open D:\Clients\Jones' Project\notes\plan.md now` becomes `... <path>' Project\notes\plan.md now`
  - `Access to the path 'D:\Shared\Students' Work\grades.xlsx' is denied.` becomes `... '<path>' Work\grades.xlsx' is denied.`
  - `Access to the path 'D:\Music\Rock 'n' Roll\x.mp3' is denied.` becomes `... '<path>' Roll\x.mp3' is denied.`
  - `Could not open D:\Music\'90s\mix.mp3 now` becomes `... <path>'90s\mix.mp3 now`. The quoted form is masked, because `Quoted` accepts a digit and `PathPattern` and `PathTail` do not.
  - Why it leaks: a closing quote that is a trailing (possessive) apostrophe ends the match, and the remaining relative fragment has no drive, UNC or Unix root for `PathPattern` to catch.
  - The fix for ANALYSIS-200 followed analysis-003's proposal exactly. That proposal covered only an apostrophe followed by a letter, so this residual also belongs to the earlier finding.
- Proposed improvement and benefit:
  - In `Quoted`, accept a closing apostrophe only when the text after it, up to the next apostrophe or line end, holds no `\` or `/`. For example, add `(?![^'\r\n]*[\\/])` after the close lookahead.
  - In `PathPattern` and `PathTail`, let a non-final segment hold any apostrophe, because a separator still has to follow. Accept a digit after the apostrophe as `Quoted` does.
  - Add the five cases above to `AnApostropheInAPathOutsideTheProfileLeaksNoFragmentOfThePath`.
- Behavior-preservation risks:
  - Two quoted paths on one line (`'D:\a\x' and 'E:\b\y' today`) must still be masked separately, which the proposed lookahead allows.
  - The added scan must not worsen the measured `Quoted` worst case (about 125 ms at 2,000 characters). Re-time the `\'a` and `'a/` inputs, or cap the lookahead.
  - The 13 existing sanitizer cases, the 4 profile apostrophe cases and the 5 DEV-004 apostrophe cases must keep their outputs.
- Evidence type: measured result (scratch probe of the reviewed `Describe`)
- Verification / measurement plan: unit cases asserting that no backslash and none of `Project`, `Work`, `Roll` or `90s` survives, plus a timing check on the adversarial inputs
- Relationship: residual of ANALYSIS-200 (and of ANALYSIS-001)

### ANALYSIS-301: A failure that continues after a flap stays unrecorded for up to 10 minutes, so doctor reports refresh as healthy

- Severity: warning (doctor correctness; introduced by DEV-004)
- Related task / requirement: 2.1; spec "Doctor SHALL report ... any persistent refresh failure", scenarios "Live sessions alone exceed the budget" and "Doctor reports state health"; design decision 5 (persistent after 1 minute), which conflicts with decision 4's revised "A failure is recorded when it is logged"
- Locations:
  - `src/BellTower/IO/RefreshHealth.cs:58-61` and `:62-71`: a rate-limited or suppressed failure returns before `Persist`
  - `src/BellTower/IO/RefreshHealth.cs:76-82`: the record is written only for a logged failure
  - `src/BellTower/Diagnostics/StateReporter.cs:8,38`: doctor's 1-minute persistent rule reads only the record
  - `tests/BellTower.Tests/IO/RefreshHealthTests.cs:393-398`: the test asserts the unrecorded state
- Observation:
  - DEV-004 records a failure only when its log line is written. A recovery now keeps the per-reason limiter.
  - So when reason A was logged at t0 and recovered, a new failure of A that starts before t0 + 10 minutes is neither logged nor recorded until t0 + 10 minutes. This holds even when the failure is continuous.
  - Meanwhile the record shows `failingSince = null` and the recovery's `lastSuccess`, so doctor reports refresh as not failing while captures stay queued.
  - Measured on the fake clock: 598 s unrecorded at 250 ms ticks, 594 s at 2 s ticks, and 300 s after a 5-minute logged failure. Doctor's threshold is 60 s.
  - The same applies when the eight remembered reasons are all fresh and a new reason arrives. Only the suppression notice is logged, and `windowLogged` and the record stay unset.
  - This can happen in the spec's own scenario: a live-only budget overflow near the boundary fails, then succeeds once a session ends and is compacted, then fails again for good.
  - Before DEV-004, every window start was recorded (analysis-003).
- Proposed improvement and benefit:
  - Separate recording from logging for a window that lasts.
  - Also write the failure record, with `failingSince` set to the window start, when the record shows no failure and the current window has lasted a fixed time below doctor's threshold, for example 30 seconds. Do this even when the log line is suppressed.
  - Strict alternation never reaches that age, so the flap bound is unchanged: it costs no extra writes, at most 2 writes per 30 s of real failure, and the line bound stays the same.
  - Doctor then reports every failure that persists past 1 minute.
- Behavior-preservation risks:
  - "Doctor never reports an ended failure" must hold, so a recovery must still persist whenever the record shows a failure.
  - Log lines must stay rate-limited, because only the record write changes.
  - The design's sentence "A failure is recorded when it is logged" needs the orchestrator's reconciliation with decision 5. The test at lines 393-398 would change.
- Evidence type: measured result (fake-clock probe)
- Verification / measurement plan: a fake-clock test that logs failure A, recovers, then fails A continuously for 15 minutes. Assert that the record shows `failingSince` equal to the window start within 60 s, and that the strict-alternation and 1-in-4 bounds are unchanged.
- Relationship: regression introduced by the ANALYSIS-201 fix (the design revision to decision 4)

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-200 | `diff=87172e79b118 untracked=7/e2351c042def` | All four cases from the finding are masked with no fragment left: `Bob's app` quoted and unquoted, `Smith's Project`, `codex-home\Bob's`. So are two quoted paths on one line, UNC and Unix paths with `Bob's`, and a sharing-violation message with `O'Neil repo`. An apostrophe followed by a space, or an unquoted one followed by a digit, still leaks. | verified for the reported cases (ANALYSIS-300 is the residual class) |
| ANALYSIS-201 | same | Strict alternation and 1 failure in 4: 2 lines and 2 writes per 10 minutes, against 2,400 before. Any mix of reasons and successes: at most 17 lines (9 or fewer of them failure lines) and 16 writes per 10 minutes. No stale failure is ever shown, and `failingSince` is always correct when recorded. | verified (bounded for any mix). The fix causes the doctor regression ANALYSIS-301. |
| ANALYSIS-001 (final) | same | As ANALYSIS-200 | still failing (narrow; ANALYSIS-300) |
| ANALYSIS-002 (final) | same | As ANALYSIS-201 | verified |

## Assessment and limitations

- **Verdicts:**
  - ANALYSIS-200: verified for its cases. The residual class is ANALYSIS-300, a trailing or possessive apostrophe, plus the unquoted digit case.
  - ANALYSIS-201: verified. The limiter is bounded for every failure and success mix. Its fix introduces ANALYSIS-301, where doctor misses a continuing failure for up to 10 minutes.
- **Complexity introduced by DEV-004:**
  - The limiter now has two more flags. Their invariants hold (`recordShowsFailure` implies `windowLogged`).
  - The quoted rule's worst case rose from about 0.02 ms to about 125 ms for a crafted 2,000-character message. It is bounded, runs on the UI thread before the limiter, and has no match timeout. No realistic IO message reaches it, so it is not raised as a finding. It matters for any fix to ANALYSIS-300.
- **Docs and design wording:** total log output is about twice decision 4's "same bound as a continuous failure" and the "about nine lines" in `docs/architecture.md`, because recovery lines come on top. Both texts read correctly if they mean failure lines. No defect.
- **Decision for the orchestrator:**
  - The fix-cycle budget is exhausted, so decide whether ANALYSIS-300 and ANALYSIS-301 block or go to the user as follow-ups.
  - ANALYSIS-301 needs decision 4's "recorded when it is logged" reconciled with decision 5 and the spec's "any persistent refresh failure".
- **Not performed:**
  - No repository build or test run (QA owns them).
  - No live tray, no real data directory and no installation.
  - The probes used synthetic names and paths, and stubbed `LocalLog` and `AtomicFile`. The limiter logic does not depend on their internals.
- **Writes:** the client refused the native report write, so this text is returned inline for the orchestrator to save. Probe sources and data stay in the session scratch directory, outside the repository. Target identity was unchanged before and after.

---

```
status: done
role / client / model: code-analyzer / Claude Code (client version not visible to the subagent), subagent auto mode / claude-opus-5-5
task / finding IDs: ANALYSIS-004 recheck of ANALYSIS-200 (verified for its cases), ANALYSIS-201 (verified); new ANALYSIS-300 (apostrophe-before-space and unquoted-digit path leak), ANALYSIS-301 (doctor misses a continuing failure for up to 10 min, regression from DEV-004)
changed or report paths: none written. The client refused the write to openspec/changes/bound-ended-session-state/reports/analysis-004.md; the full report is inline above.
checks: dotnet format --verify-no-changes --include RefreshHealth.cs RefreshHealthTests.cs -> passed; scratch probe (sanitizer, regex cost, limiter, record and restart) -> run, results in the report; dotnet build/test -> not run (QA owns them)
target identity: before head=5e95554 baseline=5e95554 files=22 diff=87172e79b118 untracked=7/e2351c042def; after identical
limitations / decisions needed: the report write was refused, so the text is inline. Decide whether ANALYSIS-300 and ANALYSIS-301 block now that the fix-cycle budget is exhausted. ANALYSIS-301 needs decision 4's "recorded when it is logged" reconciled with decision 5 and the spec. The quoted-regex worst case (about 125 ms per crafted 2,000-character message, on the UI thread, no timeout) is recorded as measured, not as a finding.
next: orchestrator saves the report, triages ANALYSIS-300 and ANALYSIS-301, and reconciles the design text if needed
```

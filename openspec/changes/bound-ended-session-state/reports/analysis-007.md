<!-- Saved verbatim by the orchestrator (session claude-bound-state-20261121) on 2026-11-21: the client refused the code-analyzer's native report write and the subagent returned the full text inline. -->

# Analysis report: bound-ended-session-state / ANALYSIS-007 (recheck of DEV-007, fix cycle 6)

## Review target

- Reviewer / role: code-analyzer subagent (assignment ANALYSIS-007) / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (this subagent cannot see the client version; earlier reports record 2.1.330), subagent mode (auto), model `claude-opus-5-5`
- Date: 2026-11-21 (UTC)
- Change root / assignment: `openspec/changes/bound-ended-session-state` / ANALYSIS-007 (recheck)
- Task and requirement references:
  - task 2.1 in [tasks.md](../tasks.md)
  - design decision 4 (structural quoted-path rule) in [design.md](../design.md)
  - requirement "Bounded monitoring state" in [specs/session-tracking/spec.md](../specs/session-tracking/spec.md)
  - developer evidence [implementation-007.md](../evidence/implementation-007.md)
- Scope and relevant neighboring paths:
  - `src/BellTower/IO/RefreshHealth.cs` (DEV-007 changes only the `Quoted` closing lookahead and its comment, lines 225-228)
  - `tests/BellTower.Tests/IO/RefreshHealthTests.cs`: the new theory `AClosingQuoteBeforeAnyPunctuationLeavesNoNameFragmentAndKeepsTheTrailingText` (lines 440-461) and the changed assertion in `ACraftedMessageCompletesQuicklyAndLeaksNoPath` (line 483)
  - `docs/architecture.md` line 31
- Baseline commit: 5e95554
- Implementation identity (before): `head=5e95554 baseline=5e95554 files=22 diff=9f3ab1aba1d2 untracked=7/06c081ffc9e0`
  - Tracked diff SHA-256: `9f3ab1aba1d212e7f940eb5f150d48d97efc24a77d396a0fad701a53cc5dcfd9`
  - Untracked: `RefreshHealth.cs` 11e7ac58…, `RefreshHealthTests.cs` f0dd66cd…. The other five untracked hashes match analysis-006.
  - This matches the assigned target.
- Before/after identity: after the analysis the same command printed `head=5e95554 baseline=5e95554 files=22 diff=9f3ab1aba1d2 untracked=7/06c081ffc9e0`. It matches, so there was no unexpected mutation.
- Prior reports / rechecked findings: residual 1 of [analysis-006.md](analysis-006.md#assessment-and-limitations) (a close followed by a character outside `\s . , ; : ) !`), filed as QA-500 in [qa-006.md](qa-006.md#qa-500-a-quoted-rooted-path-whose-closing-quote-is-followed-by----or--is-not-matched-and-the-last-word-of-a-name-with-a-space-leaks).

## Analysis performed

All probes reuse the analysis-006 console harness in the session scratchpad, outside the repository. Two builds ran side by side, each from a byte-identical copy of its `RefreshHealth.cs`:

- DEV-006 (d929c94d…)
- DEV-007 (11e7ac58…, the reviewed target)

Both use the same stubs for `DataPaths`, `LocalLog` and `AtomicFile`. All probe data is synthetic. Both providers are covered: the sanitizer and limiter are shared, with no provider branch, and the refresh is the shared drain for Claude Code and Codex sessions.

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Change footprint | `diff` of the DEV-006 against the DEV-007 `RefreshHealth.cs` | Only the `Quoted` lookahead `(?=[\s.,;:)!]\|\z)` changed, to `(?![\p{L}\p{N}])`, plus its comment. No other line differs. | Matches implementation-007 |
| Realistic closes (analysis-006 exact matrix) | Exact-output check on 23,100 messages: 11 roots x 7 mids x 15 finals x 20 closes | DEV-007 gives 0 inexact outputs for every realistic close (`\s . , ; : ) !`, end, CRLF, tab, no-break space). The only mismatch is the close `'` (1,155 cases): a doubled final quote is absorbed into the mask (`'<path>'` instead of `'<path>''`). That is over-masking, identical in DEV-006. | Verified, no regression |
| Unusual-close matrix (residual 1) | Same matrix, closes `? ] " - > }` and an ellipsis | DEV-007: 0 inexact. DEV-006: 952 inexact per close, for example `'D:\Secret One'?` became `'<path> One'?`. The leak-heuristic run (15,444 messages) drops from 1,902 leaks to 0, including the 1,200 combinations that analysis-006 found worse than DEV-005 (`'D:\Rock 'n' Secret'?` became `'<path>' Secret'?`). | Verified, residual 1 resolved |
| Apostrophe directly before a separator (shape the wider close newly treats as a close candidate) | 7 roots x 5 mids (`Bob'\`, `Students'\…`, `'90s'\`, `Rock 'n'\…`, `a'\b'\c d\`) x 5 finals x 12 closes (2,100 messages), all with a closing quote | DEV-007: 0 inexact. DEV-006: 1,015 inexact. Greedy matching to the last close covers inner `'\` and `'/`. | Verified |
| Unusual characters | Analysis-006 chars matrix (7,938 messages) plus 6 unquoted cases | 0 inexact; output byte-identical to DEV-006 | Verified, no regression |
| Multiple paths on a line | 648 combinations plus the 4 targeted lines | 0 leaks, outputs identical to DEV-006 | Verified, no regression |
| Markers, profile and user name | The 17 analysis-006 cases | Identical to DEV-006, no fragment | Verified, no regression |
| Truncation (analysis-006 cases) | Cut inside a quoted path, a path straddling the cut, many short paths before a cut path | No fragment. One output changed form: `'<path>''<path>`, where DEV-006 gave `'<path>' '<path>`. Neither leaks. | Verified |
| Truncation or no closing quote, with an apostrophe before a separator | A quoted path with no valid close later on its line, either cut by the 2,000-character cap or ending without a closing quote. Tails tried: `Bob'\Secret x\y z`, `Secret'\Secret x y`, `Students'\…`, `Secret' x\…`. | DEV-007 masks up to the inner `'\` and keeps the rest, for example `'<path>'\Secret x\y z`. DEV-006 leaked only the last word (`'<path> z`). Both versions leak `'<path>' x\Secret y z` for an apostrophe before a space, which is pre-existing; analysis-006's truncation row did not exercise it. | Residual R1 (see Assessment), no producer |
| Tail after the last close that starts with a separator | `'C:\a\x'\Secret Sub\Name now`, `'/srv/x'/Secret y z now`, the theory input `'C:\Users\Jo Doe\x y'/z q` | DEV-007 keeps the tail (`'<path>'\Secret Sub\Name now`, `'<path>'/z q`). DEV-006 masked more of it (`'<path> now`, `'<path> q`). implementation-007 lists this shape as residual by design, and the new theory asserts it at line 446. | Same residual R1, no producer |
| Close followed by a letter or digit | `'C:\Secret x\y z'x`, `'…'1`, `'…'s later` | Unchanged from DEV-006: `'<path> z'x`. The `_` case improves (`'<path>'_later`). | Unchanged, no producer |
| Trailing-text over-masking | `Check 'config'? now`, `(see 'help'] later)`, `users' files?`, `Don't retry` | DEV-007 extends the mask to the later apostrophe before punctuation (`'<path>'? now`, `'<path>'] later)`). `Don't` is not a close in either version. | Over-masking only, as accepted in decision 4 |
| Bracket and JSON forms | `$['…']`, `$['…']['b c']`, `{"p":"'…'"}`, `<'…'>`, `['…', '…']` | DEV-007 masks all of them completely. DEV-006 left `z` in each. | Improved |
| Cost sweep | 33 crafted units (the analysis-006 26 plus `'C:\a'] `, `'/a'? `, `'C:\a'1 `, ` 'C:\a'x`, `'C:\'\ `, `'\\'/ `, `'/'/ `) x 10 lengths up to 2,000 x 2 prefixes x 5 runs (660 inputs); the per-rule runs; benign 2,000-character messages | DEV-007: worst complete `Describe` 3.39 ms (`\'a`, first-call JIT), 0 timeouts, benign under 0.14 ms. DEV-006 in the same session: worst 11.03 ms, 0 timeouts. The new units run in at most 1.3 ms. | Bounded, no regression |
| Timeout and fallback | Oversized inputs through each generated rule; the 900-part profile case | `Quoted` still throws `RegexMatchTimeoutException` at its 20 ms setting (28 ms wall clock). The `DataTail` and `PathPattern` behavior is unchanged. The `Describe` catch is unchanged. | Verified |
| Limiter and record | The analysis-006 27-scenario limiter and restart simulation | Output byte-identical to DEV-006 | Verified unchanged |
| Complexity | Inspection | One enumerated character class becomes a shorter negative lookahead. No new rule, branch or allocation. | No new complexity |
| Tests | Read the new theory and the changed assertion | The 9 inputs cover the residual-1 closes with exact outputs. The changed assertion (`text.TrimEnd('\\')`) only tolerates trailing backslashes from the crafted unit, and `Secret` is still asserted absent. | Adequate for the recheck |
| Docs | `docs/architecture.md:31` | "Masked through the last closing quote on its line" still describes the behavior. The doc does not define the closing quote, so no wording became stale. | Consistent |
| Format | `dotnet format ZeusApps.BellTower.slnx --no-restore --verify-no-changes --include src/BellTower/IO/RefreshHealth.cs tests/BellTower.Tests/IO/RefreshHealthTests.cs` | exit 0 | Passed |
| Build / tests | Not run | QA owns the full build and test run | Not performed |

## Findings

**No findings.** No blocking issue was found. One non-blocking residual (R1) is recorded under Assessment for triage.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| analysis-006 residual 1 / QA-500 | `diff=9f3ab1aba1d2 untracked=7/06c081ffc9e0` | A rooted quoted path closed before `? ] " } > -` or an ellipsis now gives the exact expected output: 0 inexact of 23,100 (DEV-006 had 952 per close), and 0 leaks of 15,444 (DEV-006 had 1,902). The worse-than-DEV-005 subset (an inner apostrophe before whitespace plus an unusual close) is gone. Inner `'\` and `'/` with a later close: 0 of 2,100 inexact. | verified (QA-500 closure is QA's to confirm) |
| Regression vs DEV-006 | same | Realistic-close, chars, multi-path, marker, analysis-006 truncation, limiter and timeout runs are equal or better. The only shapes that are worse are the residual R1 shapes below. | no blocking regression |

## Assessment and limitations

- Verdict: residual 1 of analysis-006 (QA-500) is fixed. There is no new complexity and cost is lower (worst 3.4 ms, no timeouts). The limiter and record behavior is unchanged. No blocking findings.
- **Residual R1, for orchestrator triage (non-blocking: no producer).** DEV-007 treats an apostrophe followed by `\` or `/` as a closing quote. When no valid close follows later on the same line, the mask ends there and the rest is kept. DEV-006 kept the last word in these shapes, and DEV-007 keeps more:
  - Examples:
    - a quoted path cut by the 2,000-character cap after a segment ending in `'`: `'<path>'\Secret x\y z`, where DEV-006 gave `'<path> z`
    - a quoted path with no closing quote
    - a close followed directly by a separator: `'<path>'/z q` in the new theory, where DEV-006 gave `'<path> q`
  - Pre-existing in both versions: the truncated shape with an apostrophe before a space (`'<path>' x\Secret y z`). Analysis-006's truncation row did not exercise it.
  - Producer search: .NET IO messages always close the quote and end it with a space, `.` or the end of the text. A complete message with an inner `'\` is fully masked by greedy matching to the last close. Reaching the cap needs a quoted path of about 1,950 characters. The tick's own paths start at the data directory, which is replaced wholesale, and Bell Tower's file names contain no apostrophes. The profile is replaced by a marker before `Quoted` runs.
  - implementation-007 records the after-close tail as residual by design. Whether to accept it is a decision for the orchestrator. It is not raised here.
- Unchanged, no producer: a close followed by a letter or digit (`'…z'x`) keeps the last word, as in DEV-006. Unquoted final names with a space and rootless quoted segments are unchanged from analysis-006.
- Not performed: the build and test suite (owned by QA), and anything against a real data directory, installation or live tray.
- Unexpected writes: none in the repository. Probe projects and outputs are in the session scratchpad only.

---

```
status: done
role / client / model: code-analyzer / Claude Code (version not visible to subagent; earlier reports record 2.1.330) / claude-opus-5-5
task / finding IDs: ANALYSIS-007 recheck of analysis-006 residual 1 (QA-500): verified; no new findings (ANALYSIS-600..699 unused)
changed or report paths: none written. The write to openspec/changes/bound-ended-session-state/reports/analysis-007.md was refused and the file is absent, so the full report is inline above.
checks: dotnet format --verify-no-changes --include RefreshHealth.cs RefreshHealthTests.cs -> passed; sanitizer matrices DEV-006 vs DEV-007 (23,100 exact + 15,444 leak + 2,100 apostrophe-before-separator + 7,938 chars + 648 multi-path + 17 marker + truncation + 27 regression cases) -> passed; cost sweep (660 inputs) -> passed, worst 3.39 ms, no timeouts; timeout/fallback -> passed; limiter/restart simulation -> passed, byte-identical to DEV-006; build/test -> not run (QA owns)
target identity: before and after: head=5e95554 baseline=5e95554 files=22 diff=9f3ab1aba1d2 untracked=7/06c081ffc9e0 (match, equals assigned target)
limitations / decisions needed: the report write was refused, so the text is inline. Triage non-blocking residual R1: with no valid close later on the line (a cut at 2,000 characters, no closing quote, or a close followed by \ or /), text after an inner '\ or '/ is kept. DEV-006 kept only the last word there. No producer found, and implementation-007 accepts the after-close tail as by design.
next: orchestrator saves the report, updates the ledger for QA-500 and analysis-006 residual 1, and decides whether to accept R1
```

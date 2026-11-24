> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-061); client: Claude Code (version not exposed); mode: light review (docs-only); model: claude-sonnet-5-5 (cost-efficient override recorded in the ledger).
> Refusal (as reported by the reviewer): the client refused the write of this report; the reviewer did not retry.

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-061 (light review of DEV-070)

## Review target

- Reviewer / role: ANALYSIS-061 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code (version not exposed to the subagent), code-analyzer subagent, light review (docs-only), claude-sonnet-5-5 (cost-efficient override recorded in the ledger)
- Date: 2026-11-24 (UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-061
- Task and requirement references: DEV-070; [analysis 060](analysis-060.md) (ANALYSIS-6000); [QA-065](qa-065.md) (QA-6501); [findings ledger](findings.md)
- Scope and relevant neighboring paths: the last bullet of "Native sessions adopted from hooks" in `docs/follow-ups.md` (line 63); neighbors `reports/qa-065.md`, `evidence/implementation-069.md`
- Baseline commit: 9550ced
- Implementation identity (before): `head=9550ced baseline=9550ced files=22 diff=74460069c2b1 untracked=2/1c86223ba079`
  - Tracked diff SHA-256: 74460069c2b1e158b0ed11cb9eb403f34752c2c0abfef21be9ad70d165a450f3.
  - Untracked: `HookIngressImageReporter.cs` e3a110092f3dd3ebb3ff9bcbf8115a3f84f166ece78eddb80311b18f4f5ba5ec, `HookIngressImageReporterTests.cs` 3e55b5c971155b14b33a8f77c3e3648b571c6a22957ce4a38052a3be9886a437.
- Before/after identity: the after run gives the same summary line, `head=9550ced baseline=9550ced files=22 diff=74460069c2b1 untracked=2/1c86223ba079`. No unexpected mutation.
- Prior reports / rechecked findings: none rechecked. The target restates ANALYSIS-6000 from [analysis 060](analysis-060.md).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Accuracy of the restatement | Compared line 63 with ANALYSIS-6000 in analysis 060 | The four named tests match the report's candidate list. The launch command, the single check or expired deadline in the `finally` blocks, `TestWorkspace.Dispose`, the indefinite lifetime, the no-user-data-risk statement and "applies to both providers' launch paths" all match. | Accurate, with one causal nuance (ANALYSIS-6100) |
| Cross-reference | Read `reports/qa-065.md` 47-57 | QA-6501 is the suite leaving a tray (a second occurrence, stopped by data-dir match) and names ANALYSIS-6000 as deferred. "duplicate QA-6501" is correct. | Accurate |
| Safety of the proposal | Read the bullet against project facts "Safety" | The helper matches "only that workspace's exact `--data-dir`" and kills "only the matching processes as a last resort, never by process name". The bullet has no instruction to stop the user's tray or sessions, and none to stop the current orphan. | Safe |
| Decisions marked | Compared with analysis 060 "Proposed improvement" and "Assessment" | The test-only no-tray path (a product change), the mutex pre-hold and the product rule that a tray exits when its data root disappears are each marked "needs a decision". The core `StopTray` helper is stated as "Proposed". | Correct and conservative. Analysis 060 treats the mutex pre-hold as test-only, so marking it is over-cautious but harmless. |
| Behavior-preservation caveat | Same | `DetachedTraySurvives...` must still prove the tray outlives the starting process. The bullet carries this over. | Present. The "pre-hold must not hide a started-tray assertion" caveat is not carried over. I did not raise it, because it belongs to the later design. |
| Placement and form | `grep -n "^#"`, `sed -n 55,63p` | The bullet is the last line of the "Native sessions adopted from hooks" section. It uses the same bullet style, and the section intro ("applies to both providers unless stated"; cites `analysis-058`, `analysis-051` onward and `findings.md`) covers it. | Fits |
| Hygiene | Grep of `docs/follow-ups.md` for `C:\Users` and `@`; read the bullet | 0 hits. The bullet uses only repository-relative paths and placeholders (`<root>`). | Clean |
| Build, tests, format | Not run | Docs-only target. A light review limits checks to correctness and safety. | Not performed (scope) |

## Findings

### ANALYSIS-6100: The DEV-069 build block is stated as caused by the leaked tray

- Severity: suggestion
- Related task / requirement: DEV-070, ANALYSIS-6000
- Locations: `docs/follow-ups.md` line 63 ("locks `src/BellTower/bin/Release` (it blocked the in-tree build in DEV-069)"); [implementation 069](../evidence/implementation-069.md) line 10
- Observation: Implementation 069 records only that the output was "locked by a running bell-tower process". Analysis 060 says the orphan test tray "explains" that lock, which is an inference. The tray ran from that output directory, but nobody confirmed it as the process holding the lock in DEV-069. The bullet states the cause as fact. The lock itself is supported separately by QA-6501 ("a leaked tray locks `src/BellTower/bin`").
- Proposed improvement and benefit: Optionally soften to "it locks `src/BellTower/bin/Release` (a running `bell-tower` process locked the in-tree build in DEV-069)". The entry then does not assert an unconfirmed attribution.
- Behavior-preservation risks: none (docs wording).
- Evidence type: source observation (document comparison).
- Verification / measurement plan: re-read line 63 against implementation 069 line 10.
- Relationship: new

## Re-verification

Not applicable (not a recheck).

## Assessment and limitations

- The bullet states ANALYSIS-6000 accurately and cites QA-6501 correctly. It is safe: it matches the exact data-dir, never kills by process name, and gives no instruction about the user's tray, sessions or the current orphan. It marks the product-affecting options as needing a decision. There is one optional suggestion (ANALYSIS-6100) and no blocking issue.
- Not performed: build, tests and format (docs-only, light review).
- Unexpected writes: none. The identity is unchanged before and after.
- The orchestrator owns triage and ledger status.

# Analysis report: confirm-claude-human-permission-waits / 005

## Review target

- Reviewer / role: delegated Codex analyzer / code-analyzer.
- Client / version / mode / resolved model: Codex, local CLI previously observed as 0.164.0 (active host build unavailable), Default/workspace-write, `gpt-6.1-sol` high (role default).
- Date: 2026-10-26 UTC.
- Change root / assignment: `openspec/changes/confirm-claude-human-permission-waits`; user-authorized additional targeted ANALYSIS-002 cycle, Analysis 005.
- Task / requirement references: 2.3–2.5 and 4.1; exact approval correlation and freshness, partial parallel resolution, independent episodes and silent restore in [attention-notifications](../specs/attention-notifications/spec.md) and [session-tracking](../specs/session-tracking/spec.md).
- Scope: targeted changes in Core Observation/WaitState/SessionState/SessionReducer/CausalSessionReducer and Sessions/CodexPermissionPump; neighboring tests, protocol round trips and documentation. Report/evidence only; no implementation edits or new suggestions.
- Baseline and HEAD: `754aed525415d0cb3f47e8b6064e91fe1abdc618`.
- Before identity: `head=754aed5 baseline=754aed5 files=62 diff=1ed64cece66a untracked=15/a39eeb1f80e7`, from `./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs README.md`. Full diff SHA-256: `1ed64cece66a7a2610f9825e1481f943cc668db4e1dab2870ba09f7a07a6fa96`.
- After identity: same summary and full tracked hash, with no unexpected target mutation. [Retained manifest/untracked hashes](../evidence/analysis-005-target-after.txt) match the before manifest. Reports and other change tracking edits are outside this implementation identity.
- Prior report / handoff: [Analysis 004](analysis-004.md), [Implementation 004](../evidence/implementation-004.md). Recheck canonical ANALYSIS-002; reserved ANALYSIS-250–299 not used.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Original in-flight old-request result | Real pump + StateStore + fake delayed client, `dotnet run --project artifacts/analysis-005-pump-probe/Probe.csproj --no-restore` | [Source](../evidence/analysis-005-pump-source.cs.txt), [results](../evidence/analysis-005-pump-results.md) | Passed on both causal and unordered paths: P1 resolves, P2 starts, old result is rejected by transient and stored paths; P2 stays provisional with no announcement. |
| A → A+B → A and poisoned dedupe receipt | Same independent pump probe, including consuming the old result against A+B before B resolves | Same evidence | Passed on both paths: cached A result remains stale, revision survives snapshot read, and a fresh A query confirms with a new observation ID. Replaying that same new observation remains idempotent. |
| Mutable caller state | Same probe mutates the original session object through the reducer while query is pending | Same evidence | Passed on both paths: captured array and scope do not follow mutation to a different request. |
| Unchanged set and unrelated activity | Same probe adds unrelated ToolStart while the query is pending | Same evidence | Passed on both paths: unrelated activity does not advance the revision, and the query confirms its still-current request. |
| Partial narrowing of an ambiguous permission's candidate set | Same probe creates Candidates A+B with identical fingerprints, resumes A before the delayed result, then consumes it | Same evidence, measured candidate diagnostics | Failed on both paths: remaining candidate B is confirmed by old evidence. Canonical ANALYSIS-002 remains acceptance-breaking. |
| Persisted generation / deterministic replay | Source inspection of pre/post permission-scope comparison and CausalSessionReducer.Copy; executed generation persistence and both-path tests above | SessionReducer lines 76, 181–184 and 385–389; CausalSessionReducer copies ApprovalRevision | Named-wait set changes are deterministic and survive snapshots/checkpoints. Candidate membership is missing from that comparison when Episode exists, causing the failure below. |
| Scope identity and upgrade behavior | Inspect WaitState.ConfirmationScopeId and reducer runtime guard; protocol/legacy test changes read | WaitState lines 23–27; reducer lines 503–517 | Missing runtime scope/revision is conservatively rejected. Legacy no-Episode waits use an opaque hash fallback. Once Episode exists the identity omits changing Candidates; thus the default common path does not enforce the complete inspected set. No native upgrade trial performed. |
| Replacement, stop/end/turn, question/mixed isolation | Source inspection of existing attachment/turn/stop guards, permission-set removal and query capture; read relevant regression changes | Reducer rejects mismatched attachment/turn and stopped foreground before accepting runtime confirmation; question waits excluded from permission scope; pump captures attachment, turn and question-presence separately | Passed by source inspection for the bounded named-wait cases; not independently executed as new native tests. The candidate-membership issue affects shared policy regardless of such guards. |
| Silent restore and prior continuing-positive guard | Source inspection against Analysis 004 independent evidence | Runtime guard still rejects while a confirmed permission is outstanding; RestoreSince still controls back-dated confirmation while observation At stays current | No change to those algorithms found beyond requiring captured scope/revision. Current long-journal tests were read; not rerun independently by this analyzer. QA owns full-suite execution. |
| Performance impact | Source inspection only | PermissionScope enumerates and sorts waits before/after each reducer event; common Episode path does not hash | Added work scales with outstanding permissions and repeats during bounded causal replay. No measurement was made; neither cause nor absence of impact on QA's latency-test failures is established. New CPU work was paused for QA's quiet reruns. |
| Async-tool evidence 005/006 | Read retained probe summaries | `implementation-005*` and `implementation-006*` evidence | Bounded fake-endpoint/local-catalog app-server checks; not proof of a provider-wide/TUI restriction, not authenticated/native-user acceptance. Their stated limits are appropriate. |
| Full build / suite / format | Not run by analyzer | QA reported stable Release build complete before the pump probe referenced application/protocol assemblies | Not performed here to avoid shared outputs and duplicated load. No QA result is presented as this analyzer's execution. |

The independent probe ran 28 asserted checks successfully, including the two candidate-group setup checks. Its candidate branches then print measured unsafe results rather than asserting safe rejection; program exit success is not acceptance success. Source and result notes preserve this distinction. No provider socket, physical audio, real approval or production configuration was used.

## Findings

No new finding IDs. Existing ANALYSIS-002 remains failing for candidate-set narrowing.

### ANALYSIS-002: The captured permission scope does not change when an ambiguous wait narrows from candidates A+B to B

- Severity: warning; breaks the agreed exact inspected-set/freshness acceptance.
- Related task / requirement: 2.3, 2.4, 4.1; partial parallel resolution, shared human-wait confirmation and automatic-review silence. An old status result must not be reassigned by elimination after its potential source resumes.
- Locations at this target: `src/BellTower/Core/WaitState.cs:25–27` returns Episode as ConfirmationScopeId whenever present, ignoring Candidates. `src/BellTower/Core/SessionReducer.cs:232–241` narrows Candidates while preserving Episode. PermissionScope at lines 385–389 and revision comparison at lines 181–184 therefore see no change. The runtime acceptance guard at lines 503–517 and transient pump scope comparison consequently accept the old result for the narrowed wait.
- Measured reproduction: start operations A/B with identical fingerprints, then one no-ID PermissionRequest. The reducer creates one provisional permission with Candidates A+B. Begin a real pump query using the fake delayed client. Resume A via ToolStart before the old positive response arrives. The reducer retains only candidate B but ApprovalRevision remains unchanged. Release the positive result that may describe A and drain the store. On both causal paths, the measured output is `candidates=B, revisionUnchanged=True`, then `confirmation=Confirmed, attention=True`.
- Expected: narrowing the unresolved candidate set invalidates the old result and its transient cache. B requires evidence obtained for the current inspected set; resumption of A cannot establish that B needs a person.
- Why the new fix misses it: it protects changes to the list of wait Episodes, not all changes to the underlying unresolved operation candidates. A single ambiguous WaitState can represent multiple candidates, and the supported partial-resumption path mutates that set without changing Episode. The existing regression `AmbiguousParallelPermissionRequiresEveryCandidateToResumeOrComplete` confirms that this narrowing behavior is intentional product behavior.
- Proposed correction and benefit: include current unresolved candidate membership in the evidence scope and generation comparison, alongside a stable opaque wait identity. Candidate removal must invalidate cached positive/negative status and stored observations, including A+B → B and any return to an earlier candidate set. Preserve idempotent observation replay and fresh-query recovery after stale rejection. Do not broaden this into unrelated approval policy changes.
- Behavior-preservation risks: preserve existing missing-ID ambiguity handling, same-fingerprint partial resolution, named-wait ABA protection, question independence, unchanged-set query success and silent restore. Keep private/raw tool metadata out of serialized scope IDs.
- Verification plan: retain this exact delayed-client candidate-narrowing case on both reducer paths. Assert transient and persisted rejection, then issue a fresh query for the narrowed set and verify confirmation succeeds without a poisoned dedupe receipt. Keep the now-passing original race and named-wait ABA regressions.
- Evidence type: measured simulated execution of real pump, reducer and StateStore with a fake client. Native frequency and mode-specific provider occurrence are not claimed.
- Relationship: canonical ANALYSIS-002; no duplicate ID allocated. The previously reported named-wait race is fixed, but the same inspected-set acceptance remains incomplete.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-002 | `diff=1ed64cece66a untracked=15/a39eeb1f80e7` | Independent old-P1/new-P2, ABA, mutable-session, unchanged-set probes pass; candidate A+B → B probe fails on both reducer paths | Still failing for the bounded candidate-set case above. |

## Assessment and limitations

The targeted recheck is complete and current code identity is unchanged. The main named-request fix is effective, including the requested stale-consumption/fresh-query dedupe scenario. It does not cover candidate-set narrowing, so this report does not approve implementation completion or commit. The orchestrator owns triage, cycle-limit decisions and any new assignment; no fix was made here.

No new suggestion was introduced. Native/live approval acceptance, installed/packaged trials, broad async-tool availability and performance measurements were not performed. Earlier reports remain unchanged. Only this report, assigned Analysis 005 evidence and ignored disposable probe outputs were written.

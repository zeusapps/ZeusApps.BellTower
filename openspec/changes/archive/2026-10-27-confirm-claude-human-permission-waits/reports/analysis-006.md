# Analysis report: confirm-claude-human-permission-waits / 006

## Review target

- Reviewer / role: delegated Codex analyzer / code-analyzer.
- Client / version / mode / resolved model: Codex, local CLI previously observed as 0.164.0 (active host build unavailable), Default/workspace-write, `gpt-6.1-sol` high (role default).
- Date: 2026-10-26 UTC.
- Change root / assignment: `openspec/changes/confirm-claude-human-permission-waits`, Analysis 006 recheck of canonical ANALYSIS-002 against [Implementation 007](../evidence/implementation-007.md).
- Task / requirements: 2.3–2.5 and 4.1; exact inspected approval scope/freshness, partial parallel resolution, independent episodes and silent restore in [attention-notifications](../specs/attention-notifications/spec.md) and [session-tracking](../specs/session-tracking/spec.md).
- Scope: WaitState candidate-inclusive opaque identity and pump capture, with reducer revision/replay/persistence and named-wait regressions. No new suggestions, implementation edits, planning edits or ledger edits.
- Baseline and HEAD: `754aed525415d0cb3f47e8b6064e91fe1abdc618`.
- Before identity: `head=754aed5 baseline=754aed5 files=62 diff=518bc5fdeb23 untracked=15/c0940d0a9277`, from `./scripts/review-identity.ps1 -Baseline 754aed5 -Paths src tests docs README.md`. Full diff SHA-256: `518bc5fdeb23904a21470c7e627ef5c1352978e40d45d8974c56cd8012dc9f1b`.
- After identity: identical summary, full tracked hash, manifest and untracked hashes; no unexpected target mutation. [Retained identity output](../evidence/analysis-006-target-after.txt). Reports and change-tracking files are outside this implementation identity.
- Prior reports: [Analysis 004](analysis-004.md), [Analysis 005](analysis-005.md). Existing finding ANALYSIS-002 rechecked; reserved ANALYSIS-300–349 unused.

## Analysis performed

Static inspection completed while QA ran its quiet full-suite window. Independent probe compilation/execution began only after the orchestrator released that window. All commands and file writes used default workspace permissions; no escalation request was made.

Commands:

```text
dotnet build artifacts/analysis-005-pump-probe/Probe.csproj --no-restore
dotnet run --project artifacts/analysis-005-pump-probe/Probe.csproj --no-build --no-restore
```

The existing ignored standalone project was reused with its own outputs, referencing the current Release application/protocol assemblies after QA's current build. Build passed with zero warnings/errors. Probe passed all 62 assertions. [Exact source](../evidence/analysis-006-probe-source.cs.txt) and [captured output](../evidence/analysis-006-probe-output.txt) are retained. The output text is transcribed from the successful command result without executing an unnecessary second run.

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Candidate A+B → B race | Independent real pump + StateStore + fake delayed client, both causal and unordered paths | Passed: A's resumption narrows the ambiguous wait to B and advances generation. Old in-flight marker, transient result and persisted result are rejected. A fresh query for B confirms successfully. This directly rechecks Analysis 005's failure. |
| Original P1-resolves/P2-starts race | Same independent probe, both paths | Passed: old result does not confirm the newer automatic review, and no announcement is produced. |
| Named-wait A → A+B → A | Consume old A result while A+B exists, resolve B, inspect transient cache, snapshot read, then query A again | Passed on both paths: old result stays invalid after return to A; revision persists; fresh query has a new ID and confirms. Replaying that same fresh observation is idempotent. |
| Captured mutable session | Mutate caller state through reducer while fake read is withheld | Passed on both paths: returned evidence retains the inspected scope instead of following later state. |
| Unchanged set / unrelated events | Add unrelated ToolStart while query is withheld | Passed on both paths: unrelated activity leaves revision unchanged and valid current-set evidence still confirms. |
| Candidate ordering and privacy | Independent direct WaitState checks, with and without Episode | Passed: order reversal produces identical scope, membership narrowing produces different scope, and raw candidate/fingerprint metadata is absent. Legacy fallback remains opaque and order-canonical. |
| Legacy queued runtime evidence | Independent reducer check: missing scope/revision followed by a properly scoped fresh result | Passed on both paths: unscoped evidence stays provisional; fresh scoped evidence recovers. |
| Stop/end/new turn/replacement | Independent reducer boundary probes | Passed on both paths: old captured result confirms no permission after the boundary. Completion attention is not confused with permission confirmation. |
| Long-journal silent restore | More than the causal replay window of unrelated events, JSON snapshot round trip, then scoped restore | Passed on both paths: checkpoint revision survives, both pre-run requests confirm at their stored starts plus 1 ms, attention restores and AlertPolicy emits no sound request. |
| Mixed providers | Independent Claude question + Codex permission probe | Passed on both paths: question kind/episode remain unchanged while Codex confirms independently. |
| Architecture / deterministic replay | Inspect WaitState, reducer pre/post PermissionScope, checkpoint copy and pump Check capture | Passed: sorted candidate hash feeds the existing shared comparison; scope/revision apply to transient and persisted paths. Captured string IDs are materialized before awaits and emitted unchanged. Shared policy remains in Core; the adapter supplies evidence. |
| Existing already-confirmed-wait guard | Source inspection and read current regression | Passed by inspection: pump skips publication for a captured confirmed permission, and reducer retains its corresponding guard. The new membership hash does not remove these protections. This row is not a separately executed native test. |
| Full product suite / format / package | Not executed by this analyzer | QA owns full-suite and formatting evidence; packaging is orchestrator-owned. This report does not count other agents' commands as this analyzer's execution. |
| Performance / native acceptance | Not measured / not performed | No throughput or latency conclusion. No live provider sessions, installed tray, approvals, production settings or physical audio were used. |

The scope's common Episode form now combines an opaque wait identity and the hash of sorted current candidate membership. The legacy identity fallback hashes its known wait identity fields separately. The pump captures these strings before its asynchronous read; later caller changes cannot broaden emitted evidence. The existing persisted ApprovalRevision comparison therefore detects narrowing as well as named-wait additions/removals. Unrelated events do not churn the revision, as measured above.

## Findings

**No new findings.** The assigned ANALYSIS-002 correction passes the bounded independent recheck. No new finding ID allocated.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-002 | `diff=518bc5fdeb23 untracked=15/c0940d0a9277` | All 62 independent assertions above, including exact previous candidate-narrowing failure, transient/persisted rejection and fresh narrowed-set recovery, both reducer paths | Verified in simulated functional execution. No native/live acceptance claim. |

## Assessment and limitations

The code-analysis recheck is complete on the unchanged frozen target. The original named-request race, revision ABA/fresh-query receipt case and candidate-narrowing race now reject old evidence while accepting fresh evidence for the current inspected set. There is no remaining blocking finding from this assigned recheck. The orchestrator owns ledger closure and the overall completion decision.

Evidence is real application pump/reducer/persistence exercised with fake delayed runtime clients and disposable data, plus direct shared-policy probes. It proves those scenarios and does not replace native auto-versus-human trials, installed/package migration acceptance, or broad async-tool/provider-mode availability. No performance result is claimed. Earlier reports and evidence remain unchanged. Only this report, assigned Analysis 006 evidence and ignored standalone probe outputs were written.

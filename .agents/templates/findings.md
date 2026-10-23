# Findings: <change>

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Current orchestrator

Advisory marker; resume and hand-off rules are in the [workflow](../workflows/openspec-delegation.md#mixed-client-operation).

- Client / version: <client and version>
- Session identity: <session ID or name>
- Since (UTC): <timestamp>
- Previous owner: <client / session identity, or none>

## Assignments and reservations

| Assignment | Role / owner | Client / model | Model override rationale | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- | --- | --- |
| <assignment> | <role/owner> | <client and resolved model ID> | <reason, or none: role default> | <IDs/identity> | <qa-001.md or analysis-001.md; optional screenshot names> | <unused QA or ANALYSIS range> | <reserved / running / returned> |

Allocation, reservation, and ownership rules are in the [orchestrator workflow](../workflows/openspec-delegation.md); this table records them.

## Summary

| ID | Source report(s) | Task / requirement | Severity | Blocking decision | Status | Owner | Latest verification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| <ID> | <relative report links> | <IDs> | <critical/warning/suggestion> | <yes/no/pending and rationale reference> | <state> | <owner or unassigned> | <relative report anchor or pending> |

If there are no findings, say so after reviewing both reports. Do not use the placeholder row as a real finding.

## Finding details and history

### <ID>: <title>

- Sources and evidence: <relative links to every source report/evidence>
- Required behavior: <task/spec IDs and expected result>
- Triage: <blocking decision and rationale>
- Action / assignment: <developer and bounded correction, or investigation>
- Duplicate of: <canonical ID or none; retain this entry>
- Deferral: <rationale and follow-up destination/reconsideration condition, or none>
- User decision: <exact agreed choice/date when scope or behavior changes, or pending/not needed>
- Verification: <independent reviewer, implementation identity, report link, result>

| Date (UTC) | From | To | Actor | Evidence / decision |
| --- | --- | --- | --- | --- |
| <timestamp> | <previous state> | <new state> | <owner> | <report, developer handoff, rationale, or user decision> |

Statuses, severity and blocking rules, and verification flow are defined in the [orchestrator workflow](../workflows/openspec-delegation.md#finding-triage-and-fix-routing); record them here without restating them. Keep unsuccessful history.

## Completion assessment

- Current QA / analysis reports: <relative links and covered target>
- Required acceptance: <satisfied / remaining criteria>
- Findings: <counts by blocking state; all findings accounted for>
- Nonblocking follow-ups: <IDs, rationale, destination>
- Evidence gaps / provider restrictions: <exact version/mode, operation, proof/fallback where established; unverified otherwise>
- Commit: <SHA and branch of the orchestrator commit with the scoped paths, or not yet committed; pushes need separate authorization>
- OpenSpec task reconciliation: <verified tasks and remaining work; report existence alone is insufficient>

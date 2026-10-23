# Findings: <change>

Orchestrator-owned ledger. Reviewers write their assigned reports; developers write implementation. Preserve entries and dated history across rechecks, resumption, and archive.

## Assignments and reservations

| Assignment | Role / owner | Task IDs / target | Report / evidence paths | Reserved new finding IDs | State |
| --- | --- | --- | --- | --- | --- |
| <assignment> | <role/owner> | <IDs/identity> | <qa-001.md or analysis-001.md; optional screenshot names> | <unused QA or ANALYSIS range> | <reserved / running / returned> |

Allocate the next unused report number per role and change. Rechecks keep existing finding IDs and use a new report. Do not reuse an allocation after interruption. Reserve disjoint outputs before concurrent work. Record the one ledger owner when multiple sessions share a change.

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

States: `reported`, `assigned`, `awaiting-verification`, `verified`, `awaiting-user-decision`, `dismissed`, `duplicate`, `deferred`. Severity does not decide blocking status automatically. Developer fixes enter awaiting-verification; independent success permits verified; failed checks reopen as reported. Keep unsuccessful history. Required acceptance cannot be deferred without explicit user agreement.

## Completion assessment

- Current QA / analysis reports: <relative links and covered target>
- Required acceptance: <satisfied / remaining criteria>
- Findings: <counts by blocking state; all findings accounted for>
- Nonblocking follow-ups: <IDs, rationale, destination>
- Evidence gaps / provider restrictions: <exact version/mode, operation, proof/fallback where established; unverified otherwise>
- OpenSpec task reconciliation: <verified tasks and remaining work; report existence alone is insufficient>

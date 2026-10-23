# Analysis report: <change> / <round>

Template only: replace placeholders with actual analysis evidence. Write to the orchestrator-reserved path; retain previous reports.

## Review target

- Reviewer / role: <identity> / code-analyzer
- Client / version / mode / model (resolved ID): <observed values, for example client name, version, mode, and the resolved model ID rather than an alias; or explicitly unavailable>
- Date: <UTC timestamp>
- Change root / assignment: <resolved root and assignment ID>
- Task and requirement references: <IDs and change-relative links>
- Scope and relevant neighboring paths: <explicit paths>
- Baseline commit: <commit>
- Implementation identity: <`scripts/review-identity.ps1` output before the analysis: HEAD, manifest, diff SHA-256, untracked hashes, summary line>
- Before/after identity: <the same command after the analysis and the comparison, or unexpected differences>
- Prior reports / rechecked findings: <relative links and IDs, or none>

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| <duplication / complexity / simplification / architecture / performance> | <inspection or exact check> | <specific observation or relative link> | <result or not applicable/not performed with reason> |

## Findings

State **No findings** explicitly when applicable. Otherwise repeat:

### <ANALYSIS-ID>: <title>

- Severity: <critical / warning / suggestion>
- Related task / requirement: <IDs>
- Locations: <repository-relative paths and lines at reviewed identity>
- Observation: <concrete duplication, complexity, drift, or performance evidence>
- Proposed improvement and benefit: <bounded recommendation>
- Behavior-preservation risks: <affected contracts and edge cases>
- Evidence type: <measured result / source observation / performance hypothesis>
- Verification / measurement plan: <proportional check; do not imply a hypothesis was measured>
- Relationship: <existing/canonical finding ID if repeated or duplicate>

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| <existing ID> | <identity> | <inspection/check and result> | <verified / still failing / not performed> |

## Assessment and limitations

<Scope covered, recommendations, measurements not performed, unexpected writes, and decisions needed. The orchestrator owns triage and ledger status.>

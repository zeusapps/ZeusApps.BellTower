# QA report: <change> / <round>

Template only: replace placeholders with observed evidence. Do not describe this template as an executed check. Write to the orchestrator-reserved path; never overwrite an earlier round.

## Review target

- Reviewer / role: <identity> / QA
- Client / version / mode / model (resolved ID): <observed values, for example client name, version, mode, and the resolved model ID rather than an alias; or explicitly unavailable>
- Date: <UTC timestamp>
- Change root / assignment: <resolved root and assignment ID>
- Task and requirement references: <IDs and change-relative links>
- Scope: <explicit implementation paths>
- Baseline commit: <commit>
- Implementation identity: <`scripts/review-identity.ps1` output before the review: HEAD, manifest, diff SHA-256, untracked hashes, summary line>
- Before/after identity: <the same command after the review and the comparison, or unexpected differences reported below>
- Prior reports / rechecked findings: <relative links and IDs, or none>

## Checks and coverage

| Scenario / task | Command or observation | Evidence type | Result | Evidence / limitation |
| --- | --- | --- | --- | --- |
| <scenario> | <exact command or manual steps> | <fixture / installed native / live> | <passed / failed / not performed / not applicable> | <relative evidence link or reason> |

Cover the assignment's applicable provider, concurrent-session, and error cases, and state which client (Claude Code, Codex, or both) each check covers. Explain unavailable/non-applicable checks. A source inspection is not an executed functional test. Screenshot links use `../screenshots/<reserved-name>.png`; omit them when no useful visual scenario applies.

## Findings

State **No findings** explicitly when applicable. Otherwise repeat:

### <QA-ID>: <title>

- Severity: <critical / warning / suggestion>
- Related task / requirement: <IDs>
- Location: <repository-relative path and line, at reviewed identity>
- Expected / actual: <specific behavior>
- Reproduction: <steps or exact command>
- Evidence: <result and relative supporting links>
- Recommended correction: <actionable advice; no implementation edits>
- Relationship: <existing/canonical finding ID if repeated or duplicate>

## Re-verification

| Finding ID | Corrected target | Check and evidence | Outcome |
| --- | --- | --- | --- |
| <existing ID> | <identity> | <command/result or relative link> | <verified / still failing / not performed> |

## Assessment and limitations

<Acceptance covered, remaining gaps, unexpected writes, and decisions needed. Return findings to the orchestrator; do not edit the ledger or declare developer-reported fixes closed.>

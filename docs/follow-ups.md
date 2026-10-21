# Follow-ups after the initial change

The user requested archive and commit after reviewing live acceptance. These items remain explicit follow-ups rather than failed or implicitly passed release checks.

## Repeated permission notifications

Nonblocking by user decision. The original report was a missing second notification after approval of the first request. Version 0.1.7 normalizes shell/patch command correlation across hook stages and gives distinct new blockers fresh notification episodes. Automated regressions pass; the user has not repeated the actual add/commit approval flow and intends to report any remaining problem separately.

## Display and RDP resizing

Nonblocking by user decision. Bounds-clamping and layout checks are automated; live monitor/RDP resize behavior has not been accepted. The user confirmed ordinary three-dot dragging and does not require live resize testing for this change.

## Automatic change naming and worktree association

Deferred scope. Current names use an explicit manual name, explicit change association or directory fallback. Git context reflects the recorded session directory. A future integration should update the correct session when an agent chooses a change or works in a different worktree, preserve manual overrides and remain independent of personal mods and WorkLog. See the archived change's handoff.md for candidate standalone integration approaches.

## Retained hook fixtures and rare scenarios

The archive retains five unchecked tasks (1.2, 1.3, 4.1, 4.2 and 8.2) concerning comprehensive authenticated hook captures and dependent acceptance. Preserve the distinction between documented-contract fixtures, automated host/process probes and user-confirmed live results. Approval denial/cancellation, some question/failure/background combinations, linked-worktree switching and host edge cases are not all observed; normal working checks in live-acceptance.md do not need repeating.

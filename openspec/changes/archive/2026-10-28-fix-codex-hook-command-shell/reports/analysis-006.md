# Analysis report: fix-codex-hook-command-shell / 006

## Review target

- Reviewer / role: analyze_hook_fix / code-analyzer.
- Client / version / mode / model: host version unavailable; installed Codex CLI 0.164.0; delegated Default-mode analysis; role-configured `gpt-6.1-sol`, high effort.
- Date: 2026-10-27 UTC.
- Assignment: stable shell-selection continuation for task 2.1 / QA-003 and related regressions. Reserved IDs ANALYSIS-250 through ANALYSIS-299.
- Requirements: [tasks](../tasks.md), [design](../design.md), [setup delta](../specs/standalone-setup/spec.md), [ordering delta](../specs/session-tracking/spec.md), [findings ledger](findings.md). Read [shell continuation evidence](../evidence/shell-continuation-001.md) and [native investigation 003](../evidence/native-investigation-003.md).
- Scope: HookCommandBuilder, ProviderHookProbe, IntegrationSetup, IngressProvision, IntegrationOwnership, HookDeliveryReporter and relevant setup/doctor/project-profile tests; the four assigned documentation files. Core and benchmark results are carried forward only as described below.
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Prior reports: [analysis-004](analysis-004.md) and [analysis-005](analysis-005.md), retained unchanged.

Before/after command:

```text
./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths src tests docs/compatibility.md docs/follow-ups.md docs/hook-ingress.md docs/installation.md tools/BellTower.Ingress.Bench
```

Manifest:

```text
? src/BellTower/Diagnostics/HookDeliveryReporter.cs
? src/BellTower/Setup/ProviderHookProbe.cs
? tests/BellTower.Tests/Diagnostics/HookDeliveryReporterTests.cs
M docs/compatibility.md
M docs/follow-ups.md
M docs/hook-ingress.md
M docs/installation.md
M src/BellTower/Core/SessionReducer.cs
M src/BellTower/Diagnostics/CapabilityMatrix.cs
M src/BellTower/Diagnostics/DiagnosticsReporter.cs
M src/BellTower/Setup/HookCommandBuilder.cs
M src/BellTower/Setup/IngressProvision.cs
M src/BellTower/Setup/IntegrationOwnership.cs
M src/BellTower/Setup/IntegrationSetup.cs
M tests/BellTower.Tests/Core/PermissionNotificationTests.cs
M tests/BellTower.Tests/Core/ReducerTests.cs
M tests/BellTower.Tests/Providers/CaptureTests.cs
M tests/BellTower.Tests/Providers/CodexPermissionTests.cs
M tests/BellTower.Tests/Setup/FastHookSetupTests.cs
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `ff9951a31d79ff801d4bc9805cd4648b6681c00a8ceae9c52bf1dcf72e38a766`.
- Untracked hashes: reporter `b637c3ea13bc6aa75857a7cb0eaf09b0b191a349df9a78007dd1d2b2f6f92611`; probe `3b0a6c25275b1ca0e55818acb14be00bcb408458bcb0a9e5cf47a51eedd76982`; reporter tests `fd6def19a0c117acd90bb173695f210ea520db165210fb348d6e19d6080cc7eb`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=20 diff=ff9951a31d79 untracked=3/d9d7e40c7d6d`; matched. This source-only scope excludes the developer's additional evidence artifact and these review files.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Selected-shell setup | Inspect Plan, builder and Apply ordering | Planning probes the validated source; preview includes selected form; retained target hash and exact command are checked before SetupPlan writes settings/ownership | Fail-closed structure supported; false-negative completion race remains ANALYSIS-250 |
| Probe isolation and bounds | Inspect synthetic config, loopback endpoint, temp roots, process ownership and cleanup | Isolated home/project/data; fake provider and synthetic credential; disabled plugins/apps/update/analytics; selection has nominal 30-second bound and individual verification timeout; only created process trees are killed | No unsafe configuration write or real-model dispatch identified; process cleanup can add time beyond the nominal polling budget |
| Shell adapters | Inspect quoting and direct controls recorded in continuation evidence | Explicit PowerShell/cmd/Bash forms; cmd percent segmentation; Bash slash normalization; exact cmd `/C` and Bash argv controls are labeled direct-shell evidence | Source/evidence review passed; analyzer did not independently rerun cmd/Bash controls or native alternate-shell dispatch |
| Probe completion | Ignored synthetic executable writes a protocol-validated capture and immediately exits 0; invoke current probe through reflection | Two completed outside-sandbox runs each rejected 7 of 8 valid deliveries | Failed; ANALYSIS-250; [reproduction evidence](../evidence/analysis-006-reproductions.md) |
| Metadata preservation | Isolated project-removal preview with existing Cmd selection | Selection becomes null while planned provider hook changes remain zero | Failed; ANALYSIS-251 |
| Doctor | Inspect metadata parsing, ownership matching, receipt validation and selected-shell reporting; execute focused tests | Recorded Cmd avoids old-command false classification; packaged resolver can report matches/drift/unverified; malformed receipts remain safe and private contents omitted | Existing checks passed; metadata loss and false-negative probe affect its reliability as described in findings |
| Focused regressions | Release no-build/no-restore filter for non-opt-in FastHookSetupTests, HookDeliveryReporterTests and ProjectProfileTests | 18 passed, zero failed/skipped outside sandbox | Passed; does not cover either new reproduction; opt-in native checks deliberately excluded |
| Package integrity | Independently hash 0.1.9016 ZIP and retained payload ingress | ZIP `7c8d5e7a07e5dc39a8de12652c2aac5b3d94959d7ff31eb148ab4688b4372480`; helper `ade83e42b4327117ecc5d8339bcff79dd7360f7dc7272ec7a3d8747f5462f5a9` | Matches handoff; source/package binding is developer-recorded, no installation |
| Scope and verification hygiene | Before/after identity, `git diff --check`, assigned-output scan | Matching source; no whitespace errors or targeted personal/secret-pattern hits | Passed |
| Product build / full suite / native probes / new benchmark | Not repeated by analyzer | Independent QA runs broader functional/native checks; scratch project references existing Release assemblies without rebuilding product | Not performed here |

## Findings

### ANALYSIS-250: A successful short-lived native runner can be rejected after its durable capture

- Severity: warning.
- Related requirements: tasks 2.1/2.2, delivery-based selection and reliable setup/doctor verification.
- Locations: `src/BellTower/Setup/ProviderHookProbe.cs:199`, `:200`, `:222`; callers `IngressProvision.cs:35` and `HookDeliveryReporter.cs:79`.
- Observation: `observed` is set only inside a loop requiring the child still to be running. If the child writes a valid capture and exits between polls, the final condition rejects because `observed` is false even though `HasCapture` would succeed. A completed process is not evidence of missing delivery. The synthetic runner reproduction produced eight valid captures per run and exited 0 for every call, but seven calls per run were rejected. This can make valid shell selection/setup fail or doctor report unverified based on timing.
- Proposed bounded correction and benefit: after the process exit/polling boundary and cleanup, decide delivery from the actual validated capture state rather than requiring the earlier live-process flag. Keep capture validation and timeout/owned-process cleanup. This accepts successful completed probes without relaxing the requirement that delivery be durable.
- Behavior-preservation risks: missing, malformed and wrong-provider captures must remain rejected; failure before settings writes, bounded timeout and cleanup must remain intact. Do not infer success merely from process exit 0.
- Evidence type: source observation and executed synthetic boundary reproduction, not evidence that a real provider mode has failed in the field.
- Verification: add a deterministic fast-success runner fixture plus missing/invalid capture controls; verify both selection and exact-target Apply retain fail-closed settings snapshots.
- Relationship: new regression finding, distinct from prior ANALYSIS-002 encoding correction and QA-003 native acceptance gaps.

### ANALYSIS-251: Removing a project profile erases selection metadata for unchanged global Codex hooks

- Severity: warning.
- Related requirements: task 2.1 selected-shell ownership, preservation of unrelated global configuration, honest doctor state.
- Locations: `src/BellTower/Setup/IntegrationSetup.cs:21`, `:47`, `:49`, `:64`; existing project-removal contract in `tests/BellTower.Tests/Setup/ProjectProfileTests.cs:35`.
- Observation: the new clear-selection branch checks `remove` and selected Codex but does not exclude `removeProject`. The project-only path skips provider settings edits, yet writes null into the global `SelectedCodexHookShell`. An isolated preview changed Cmd to null with zero provider hook changes. On apply, this discards valid selection evidence for still-installed hooks. Doctor then loses its expected shell, and its legacy-command classification no longer recognizes the previously selected Cmd form as such.
- Proposed bounded correction and benefit: clear global shell selection only when the Codex integration itself is removed; retain it during project-profile removal, consistently with preservation of global hooks.
- Behavior-preservation risks: full Codex integration removal should still clear its metadata; Claude-only changes and unknown manifest fields must remain preserved.
- Evidence type: source observation and executed isolated planning reproduction; no provider settings were applied.
- Verification: extend project-profile preservation coverage with a recorded shell value, inspect/apply project removal and verify global settings and selected-shell evidence survive; separately assert full Codex removal clears the field.
- Relationship: new metadata-preservation regression introduced by the selected-shell continuation.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-003 / task 2.1 | Current source target | Adapter generation, planning selection and exact-target recheck exist; native PowerShell and direct cmd/Bash controls have distinct evidence labels | Progress supported; current revision still needs ANALYSIS-250/251 correction and independent native QA |
| ANALYSIS-002 | Current probe | Full Tomlyn construction remains and special-character root is expanded; no manual TOML table-key interpolation reintroduced | Specific encoding correction retained by source inspection; native parser not rerun here |
| ANALYSIS-003 | Current reporter/tests | Current focused receipt regressions retain malformed-shape safety, unchanged bytes and sanitized output | Verified relevant regressions |
| ANALYSIS-001 / ANALYSIS-050 | Prior Core target from analysis 004 | No new Core implementation was assigned in this continuation; prior explicit correlation review retained | Carried forward, not re-executed or newly closed here |
| Task 2.3 metric | Prior benchmark target from analysis 005 | This continuation changes setup/doctor selection, not the retained mean arithmetic or direct helper source. Tested native PowerShell command form remains the same | Carry forward the recorded 0.1.9015 synthetic trial only; no new 0.1.9016 performance measurement claimed |

## Assessment and limitations

The shared provisioning/ownership boundary remains appropriate, and the thin provider shell adapters have a concrete purpose. No architectural/style churn is proposed. The current native PowerShell path, source-backed Windows fallback rule and direct real-shell adapter controls support the implementation approach, but native cmd/Bash dispatch has not been observed. Missing supported forcing controls are not proof of a provider restriction; no universal shell-selection/support claim is made.

The source snapshot is not ready for an unconditional acceptance decision because of the two reproduced correctness defects. Neither reproduction changed implementation, user configuration, provider settings, installation or active user processes. The isolated fake runner and disposable project metadata are fixture evidence only. Full native verification remains independently owned by QA, and task 5.2 user-operated post-migration/mixed/trust acceptance remains pending.

The analyzer wrote only this report/evidence and ignored scratch outputs. The initial sandbox scratch execution failed with invalid-handle errors and wrote no valid captures; it is not acceptance evidence. Completed outside-sandbox reproductions establish the reported race. The implementation identity matched before/after. The orchestrator owns finding triage and further routing; no implementation edit was made during this review.

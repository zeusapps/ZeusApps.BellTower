# Analysis report: fix-codex-hook-command-shell / 007

## Review target

- Reviewer / role: analyze_hook_fix / code-analyzer.
- Client / version / mode / model: host version unavailable; installed Codex CLI 0.164.0; delegated Default-mode recheck; role-configured `gpt-6.1-sol`, high effort.
- Date: 2026-10-27 UTC.
- Assignment: bounded recheck of ANALYSIS-250 and ANALYSIS-251 plus directly related regression. Reserved new IDs ANALYSIS-300 through ANALYSIS-349; none assigned. No new suggestions.
- Requirements: tasks 2.1/2.2 and ownership-preserving setup/diagnostics; [tasks](../tasks.md), [design](../design.md), [setup delta](../specs/standalone-setup/spec.md).
- Scope: corrected ProviderHookProbe completion logic, IntegrationSetup removal condition, related setup/project-profile fixtures and neighboring fail-closed/cleanup paths. No product build or full-suite/native duplication while independent QA runs.
- Prior immutable report: [analysis-006](analysis-006.md). Developer evidence read: [shell-continuation-002](../evidence/shell-continuation-002.md).
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.

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
M tests/BellTower.Tests/Setup/ProjectProfileTests.cs
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `959d86f7d758195d9f52f47110ae24ea8d4f5a0fde610a8ee7ce89d6da550da3`.
- Untracked hashes: reporter `b637c3ea13bc6aa75857a7cb0eaf09b0b191a349df9a78007dd1d2b2f6f92611`; probe `3e6a758b76fe766d46b142ea9d9adb0c4a81ce6d4beecff907c24a0919657a6e`; reporter tests `fd6def19a0c117acd90bb173695f210ea520db165210fb348d6e19d6080cc7eb`.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=21 diff=959d86f7d758 untracked=3/cce5a39616ce`; matched. Review files and ignored scratch are excluded.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Original completion reproduction | Rebuild only existing ignored harness against current Release DLLs; rerun unchanged eight-case sequence | Eight valid captures, eight accepted fast exits, zero false rejections | Passed; [evidence](../evidence/analysis-007-reproductions.md) |
| Completion guards | Actual synthetic missing/invalid/wrong-provider/timeout runner controls; inspect deadline and completion predicate | All four reject; exit 0 alone is insufficient; timeout reports timeout; 12 child roots logged and zero remaining | Passed; synthetic controls are not native provider evidence |
| Deadline boundary | Inspect `ProviderHookProbe.cs:211`, `:220`, `:231`; execute existing predicate regression | Timeout requires no in-loop capture, child still running and elapsed deadline. Normal completed runner may confirm through final validated capture; timeout does not become success from a post-termination capture | Passed source/predicate review; no precisely synchronized wall-clock boundary experiment claimed |
| Original metadata reproduction | Run unchanged isolated project-removal preview | Cmd remains Cmd; zero provider hook changes | Passed |
| Applied metadata contract | Inspect and execute `ProjectOnlyCodexRemovalPreservesGlobalHookShellMetadata` | Project-only Apply preserves hook bytes, recorded shell and unknown field; Claude-only Apply preserves them; full Codex removal clears shell and keeps unknown field | Passed; fixture behavior supports the requirement rather than merely mirroring the changed condition |
| Focused regressions | Release no-build/no-restore non-opt-in setup/doctor/project-profile filter | 20 passed, zero failed/skipped | Passed; native opt-in checks excluded |
| Package integrity | Hash candidate ZIP and payload ingress | ZIP `fe5e164d189c6e423d6d79c2115a291bbd81f64c6c7cd8716cf540910f327a79`; helper `82cc115629b60f2d34ace16f91d72009e6eab1e8b28d80eb0ce796d150ad5707` | Matches handoff; source/package binding separately recorded by developer; no install |
| Identity / whitespace / own output hygiene | Before/after identity; `git diff --check`; targeted assigned-output scan | Matching source, no whitespace errors or targeted personal/secret-pattern hits | Passed |
| Product build / full suite / native probes / benchmark | Not repeated by analyzer | QA independently runs full/native acceptance; only scratch harness rebuilt | Not performed here |

## Findings

**No new findings.** No IDs in ANALYSIS-300 through ANALYSIS-349 are assigned.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-250 | Current probe hash/identity above | Original eight fast-exit reproductions now all pass; added process controls reject missing/invalid/wrong-provider/timeout and clean up; no exit-code-only acceptance | Verified correction; orchestrator owns status transition |
| ANALYSIS-251 | Current IntegrationSetup and ProjectProfileTests | Original preview retains Cmd with no hook edits; current executed fixture verifies applied project/Claude-only preservation and full-removal clearing including unknown metadata | Verified correction; orchestrator owns status transition |
| ANALYSIS-003 | Unchanged reporter/test hashes from analysis 006 | Current focused tests retain malformed-receipt safety, unchanged bytes and sanitized diagnostics | Relevant regression verified |
| ANALYSIS-002 | Current probe | Tomlyn config generation and special-character path remain outside the bounded completion edit | Carry forward analysis 006 source review and earlier parser proof; no new native parser run |
| ANALYSIS-001 / ANALYSIS-050 | Earlier reviewed Core correction | No Core edit in this bounded fix; analysis 004/006 mapping retained | Carried forward, not re-executed here |
| Task 2.3 / shell adapters | Prior analysis 005/006 targets | This fix changes completion acceptance and project metadata only; helper source, command forms and benchmark arithmetic are unaffected | Carry forward prior evidence with its original scope; no fresh 0.1.9018 latency or native cmd/Bash claim |

## Assessment and limitations

The two assigned correctness defects are corrected. Completion now checks final durable provider-matching capture state after runner termination, while a live runner without observed delivery remains subject to its deadline. Missing data, malformed data and another provider's valid capture are still rejected. Exact-target provisioning and its ordering before settings/ownership writes remain unchanged. Project-only removal now leaves global selection evidence aligned with the global hooks it preserves.

The bounded correction is ready from code analysis, subject to independent QA and orchestrator reconciliation. Native PowerShell selection and direct cmd/Bash adapter controls retain their previous distinct evidence scopes. Native Codex cmd/Bash turn dispatch remains unobserved; no hard restriction or universal native support is inferred. The analyzer did not perform a user-operated post-migration/mixed-session/trust trial, and task 5.2 remains outside this recheck. No new concurrent native-worker evidence is treated as reviewed here.

Only assigned report/evidence and ignored scratch outputs were written. The harness restores its process-local environment and uses disposable data; logged probe directories were all removed. No product build contention, user configuration change, installation, migration or active-user-process operation occurred. The source identity matched before and after review. Finding ledger status and the whole-change completion decision remain with the orchestrator.

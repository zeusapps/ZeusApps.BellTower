# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-042

## Review target

- Reviewer / role: independent recheck subagent / code-analyzer
- Client / version / mode / model (resolved ID): Codex; installed CLI reports `0.169.0`; delegated non-interactive Default mode; assigned resolved model `gpt-6.1-sol`, high reasoning. The host application version is unavailable; the installed CLI version is not evidence that a native Codex hook ran.
- Date: 2026-11-15T15:44:58Z
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-042, DEV-055 task 4.5.
- Task and requirement references: [proposal](../proposal.md), [design decision 9](../design.md), [task 4.5](../tasks.md), [session-tracking scenarios](../specs/session-tracking/spec.md), [DEV-055 evidence](../evidence/implementation-055.md).
- Scope and relevant neighboring paths: recheck ANALYSIS-4100 and ANALYSIS-4101 plus regression only. Principal paths: `src/BellTower.Protocol/{HookTrayStart,TrayProcess}.cs`, `src/BellTower.Ingress/Program.cs`, `src/BellTower/{Cli/ExitCommand,Tray/TrayApplication,Tray/TrayController,Tray/TrayHost}.cs`, `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs`, `tests/BellTower.Tests.FakeLauncher/Program.cs`, and the three documentation paths below. Architectural reference: `docs/development.md`. No new suggestions.
- Baseline commit: `5e955542e430397bbc82291395d00c48e5d7cba8`.
- Implementation identity: command before and after: `./scripts/review-identity.ps1 -Baseline 5e95554 -Paths src tests docs/compatibility.md docs/hook-ingress.md docs/installation.md ZeusApps.BellTower.slnx`.
  - HEAD and baseline: `5e955542e430397bbc82291395d00c48e5d7cba8`.
  - Manifest: 23 paths. Modified: `ZeusApps.BellTower.slnx`; `docs/{compatibility,hook-ingress,installation}.md`; `src/BellTower.Ingress/Program.cs`; `src/BellTower.Protocol/BellTower.Protocol.csproj`; `src/BellTower/{Cli/ExitCommand,Diagnostics/DiagnosticsReporter,IO/DataPaths,Setup/IngressProvision,Setup/IntegrationRefresh,Tray/TrayApplication,Tray/TrayController,Tray/TrayHost}.cs`; `tests/BellTower.Tests/{BellTower.Tests.csproj,Testing/BuiltApplication.cs}`. Untracked: the seven paths and hashes below.
  - Tracked diff SHA-256: `f0eb110b328d8b2612b7cd8c5e2a0f921c7c6736c9b6c1380dfa1c687f5677c4`.

| Untracked path | SHA-256, before and after |
| --- | --- |
| `src/BellTower.Protocol/HookTrayStart.cs` | `2b1d532f965d73f4216dfa4570bfdc6a7a5776e4a8658075b44af1b953bda71e` |
| `src/BellTower.Protocol/TrayProcess.cs` | `e510ae47d63dcf0c6808bf5d8216c967d19c1c2ac7f117ca9f420dd847cf3068` |
| `src/BellTower/Diagnostics/HookStartReporter.cs` | `3e8cd554eaa70da39620366877b9bc561452290bbf77544ba8d9cc482429d835` |
| `tests/BellTower.Tests.FakeLauncher/BellTower.Tests.FakeLauncher.csproj` | `9db7cf599338080094630fdfdb3629bab0faa9dc13f8b9ee0f0e9aba56c43469` |
| `tests/BellTower.Tests.FakeLauncher/Program.cs` | `71e175eba712b8dc5e548a1b208ed58a636be84ee893623db05ebcbbbde6adc5` |
| `tests/BellTower.Tests.FakeLauncher/packages.lock.json` | `3e34d637a4170eb57a361f0c0229c99a5a4f3ab4619b5a60db7c00dc7984f1b8` |
| `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs` | `be49a7c24950c10539761852d8037d34eaac739b4bb769420f35c5404ce2b408` |

- Before/after identity: both commands passed with `head=5e95554 baseline=5e95554 files=23 diff=f0eb110b328d untracked=7/fa1813bdefad`; full manifests and hashes match. No unexpected target mutation.
- Prior reports / rechecked findings: [ANALYSIS-041](analysis-041.md), ANALYSIS-4100/4101; [QA-048](qa-048.md), QA-4800 regression and duplicate QA-4801/4802 context. Earlier reports are unchanged.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Sign-in identity / elevation | Read token acquisition, selection, process creation and cleanup; checked current primary Windows documentation | Full selects the linked token identity; Limited and Default keep their own identity. Full duplicates the linked primary token with the documented query/duplicate/assign-primary rights. | Canonical Full/Limited identity selection is correct by source. Actual native token comparison and elevated launch remain unperformed. Default elevation handling remains ANALYSIS-4100. |
| Native API ownership | Inspected both creation paths and every handle/buffer owner against [CreateProcessWithTokenW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw) | Writable command allocation, explicit application path, Unicode environment, no standard-handle inheritance flag; ordinary path sets `inheritHandles=false`. Returned process/thread handles close on success; token, duplicate, linked token, environment and allocations release in cleanup. | No additional ownership defect identified. Successful creation does not prove initialization or independence from all jobs. |
| Provider-job lifetime | Independent disposable native probe, PowerShell `Add-Type` calling Win32 APIs in an owned child shell | Two-job procedure and exact output below. | Successful partial breakaway reproduced; ANALYSIS-4101 remains. No production process or real tray started. |
| Capture / shared policy / parity | Read ingress ordering and common `HookTrayStart` path; inspected matched provider and mixed-provider tests | Capture persists before neutral output and start check. No separate Claude/Codex launch policy. Job refusal tests assert queued capture, `{}`, exit 0 and no stand-in. Mixed hooks retain one shared gate. | Source regression check passed for the exercised ordinary paths; native Codex hook startup is unproven. |
| User-stop producers / management | Read production menu factory, CLI action, controller and tests at lines 297–338 | Tests call actual `CliApplication.Run(["exit", ...])` and the production menu item's `PerformClick`. Plain management `RequestExit` records no stop; successful tray registration clears it. | QA-4800's missing-producer-coverage issue is corrected by source inspection. Execution credited only to the orchestrator's reported focused run. |
| Functional / performance checks | No build, formatter or suite invocation, per assignment while root tests use shared outputs | Orchestrator supplied focused `HookTrayStartTests` result: 33/33 passed, 28 s. Not independently rerun. | No fresh performance measurements. No inference that ordinary fixture success covers Full/Default-elevated or nested-job creation. |

Independent native job probe: an owned helper created outer job A with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` (`0x2000`) and no breakaway, assigned itself to A, created inner job B with `JOB_OBJECT_LIMIT_BREAKAWAY_OK` (`0x800`), and assigned itself to B. It called `CreateProcessW` for a benign owned child with `CREATE_BREAKAWAY_FROM_JOB | CREATE_NO_WINDOW | CREATE_SUSPENDED`, `inheritHandles=false`. Before executing any child instruction it queried the returned child handle using `IsProcessInJob` against A, B and null. Output:

```text
created=True; error=0
child-in-outer-kill-job=True; child-in-inner-breakaway-job=False; child-in-any-job=True
```

The suspended child was terminated and its thread/process handles closed. The helper exited and released its own jobs. This is an executed native API probe, not a production ingress run, provider probe, tray startup, latency measurement or build. It used no provider settings, default Bell Tower data, inherited production sessions or shared build outputs.

## Findings

### ANALYSIS-4100: Default elevation type still permits an implicitly elevated tray

- Severity: warning
- Related task / requirement: task 4.5; design decision 9 safe startup; the correction's documented guarantee that elevated hooks never launch an elevated tray.
- Locations: `src/BellTower.Protocol/TrayProcess.cs:209`–`241`, `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs:360`–`365`, `docs/hook-ingress.md:21`, `docs/installation.md:83`, at the reviewed identity above.
- Observation: `elevated = elevation == 2` treats Default (`1`) as ordinary and launches with the current process token through `CreateProcessW`. Windows defines [Default as having no linked token](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ne-winnt-token_elevation_type); it is not a test that administrator rights are absent. With UAC disabled an administrator has an elevated unsplit token, as explained by Microsoft's [Windows engineering article](https://devblogs.microsoft.com/oldnewthing/20150618-00/?p=45351). Thus the new Full-token branch fixes split-token handling but does not establish the advertised no-elevated-start invariant. The Default unit row tests only identity selection, not actual elevation or launch routing.
- Proposed improvement and benefit: query actual [TOKEN_ELEVATION.TokenIsElevated](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-token_elevation) before selecting a launch path. Keep normal Default and Limited tokens usable; permit only a verified non-elevated selected token. If an elevated Default token has no safe limited counterpart, record failure and retain the capture under decision 9. Keep the canonical limited-token identity rule for Full/Limited.
- Behavior-preservation risks: refusing every Default token would break normal standard users. A failure must retain neutral hook behavior and queued capture for both providers. No user policy or UAC configuration should be changed to test this.
- Evidence type: source observation with primary Windows documentation; no live UAC-disabled or native elevated provider run. The documentation inference is explicit; it is not a measured host failure.
- Verification / measurement plan: cover Default/non-elevated, Default/elevated, Full with a verified limited linked token, Limited, and failed elevation lookup; add the safe-refusal branch to provider-neutral hook checks. Native Full/Limited identity equality and actual selected child elevation remain separate acceptance evidence.
- Relationship: recheck of canonical ANALYSIS-4100; not a new finding. Split-token identity part corrected by source; elevated-start part still failing at the Default edge. QA-4802 remains duplicate context.

### ANALYSIS-4101: Successful partial breakaway can retain an outer killing job

- Severity: warning
- Related task / requirement: task 4.5 and design decision 9, tray lifetime independent of a provider job that ends with the hook.
- Locations: `src/BellTower.Protocol/TrayProcess.cs:238`–`253`, `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs:342`–`357`, `tests/BellTower.Tests.FakeLauncher/Program.cs:97`–`144`, `docs/hook-ingress.md:21`, at the reviewed identity above.
- Observation: the unsafe retry after access denied is removed. However, the success branch immediately releases both child handles and reports `Started`; it never checks the child's remaining job membership. The native two-job probe above succeeds while retaining the child in the outer kill-on-close job. This matches Microsoft's [Nested Jobs](https://learn.microsoft.com/en-us/windows/win32/procthread/nested-jobs) rule that breakaway moves upward only until an ancestor refuses it. Therefore avoiding the failure fallback alone does not establish independence from nested killing jobs. Existing tests model an immediate refusing kill job, which is a distinct case.
- Proposed improvement and benefit: verify the created child is safely outside the inherited job chain before it can run or consume captures. A bounded option is suspended creation, query the child for any job, terminate/refuse when membership or inspection is unsafe, and resume only a verified safe child. Preserve handle cleanup and diagnostic failure on every branch for both creation APIs. No requirement to infer safety from only the immediate job's flags.
- Behavior-preservation risks: inspecting after normal creation allows the tray to run and drain captures before refusal. New suspended paths must terminate only their own newly created child on refusal and must release process/thread handles on query, resume and termination errors. A safe refusal may conservatively leave captures queued; decision 9 permits this.
- Evidence type: independent executed native API result plus source observation and primary documentation. Actual Claude/Codex nested job configurations were not probed.
- Verification / measurement plan: add matched Claude/Codex fixtures with an outer non-breakaway killing job and an inner breakaway-allowed job, asserting no child executes/consumes captures, failed diagnostic, durable capture and neutral hook response. Retain immediate-refusal and ordinary successful-start coverage; include the selected-token API path in native acceptance.
- Relationship: recheck of canonical ANALYSIS-4101, still failing for nested jobs despite correcting the immediate-refusal fallback. QA-4801 is duplicate context. No new ID used.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-4100 | DEV-055, `f0eb110b328d` / untracked `fa1813bdefad` | Full/Limited canonical identity and linked-token launch inspected; Default can select the elevated current token. | Still failing: elevated Default startup. Native split-token equality/launch not performed. |
| ANALYSIS-4101 | Same | Fallback removed; immediate refusing-job fixture added. Independent nested-job probe confirms successful creation can retain outer killing job. | Still failing: unsafe success branch for nested jobs. |
| QA-4800 (regression context) | Same | Actual CLI parser/action and production menu click handler are now tested. Orchestrator reports 33 focused tests passed. | Source coverage gap corrected; no independent suite execution here. |

## Assessment and limitations

DEV-055 preserves one shared hook policy and fixes the original immediate refusal retry, canonical split-token selection and the two producer coverage gaps. The two required safety findings still have concrete remaining cases above; they should return to the developer before declaring this correction accepted. The orchestrator owns finding status and triage.

Native elevated Claude/Codex hook startup, real Full/Limited comparison, native Codex job behavior, installed host trial and a current NativeAOT publish were not performed. These are evidence gaps, not proven provider restrictions. The focused test result is supplied by the orchestrator; this reviewer ran only read-only identity/source checks and the isolated native job probe. No performance result is invented or carried forward as a new measurement. Only this assigned report was written; no implementation, tests, earlier reports, settings, process ownership or shared build outputs were changed.

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-043

## Review target

- Reviewer / role: independent final recheck subagent / code-analyzer.
- Client / version / mode / model (resolved ID): Codex; installed CLI `0.169.0` observed in ANALYSIS-042; delegated non-interactive Default mode; assigned resolved model `gpt-6.1-sol`, high reasoning. Host application version unavailable. No native Codex provider hook was run by this reviewer.
- Date: 2026-11-15T16:12:44Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-043, DEV-056 task 4.5, fix cycle 2.
- Task and requirement references: [proposal](../proposal.md), [design decision 9](../design.md), [task 4.5 and acceptance task 6.1](../tasks.md), [session-tracking scenarios](../specs/session-tracking/spec.md), [DEV-056 evidence](../evidence/implementation-056.md).
- Scope and relevant neighboring paths: canonical ANALYSIS-4100/4101 plus regression only. Principal paths: `src/BellTower.Protocol/{TrayProcess,HookTrayStart}.cs`, `src/BellTower.Ingress/Program.cs`, `src/BellTower/{Cli/ExitCommand,Tray/TrayApplication,Tray/TrayController,Tray/TrayHost}.cs`, `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs`, `tests/BellTower.Tests.FakeLauncher/Program.cs`, and `docs/{compatibility,hook-ingress,installation}.md`. Architectural reference: `docs/development.md`. No suggestions or implementation changes.
- Baseline commit: `5e955542e430397bbc82291395d00c48e5d7cba8`.
- Implementation identity: before and after command: `./scripts/review-identity.ps1 -Baseline 5e95554 -Paths src tests docs/compatibility.md docs/hook-ingress.md docs/installation.md ZeusApps.BellTower.slnx`.
  - HEAD and baseline: `5e955542e430397bbc82291395d00c48e5d7cba8`.
  - Manifest: 23 paths. Modified: `ZeusApps.BellTower.slnx`; `docs/{compatibility,hook-ingress,installation}.md`; `src/BellTower.Ingress/Program.cs`; `src/BellTower.Protocol/BellTower.Protocol.csproj`; `src/BellTower/{Cli/ExitCommand,Diagnostics/DiagnosticsReporter,IO/DataPaths,Setup/IngressProvision,Setup/IntegrationRefresh,Tray/TrayApplication,Tray/TrayController,Tray/TrayHost}.cs`; `tests/BellTower.Tests/{BellTower.Tests.csproj,Testing/BuiltApplication.cs}`. Seven untracked paths appear below.
  - Tracked diff SHA-256: `bceaf5143001bf7084799450cdc254f1a519cf6f6568d340ab25bb88d784d39f`.

| Untracked path | SHA-256, before and after |
| --- | --- |
| `src/BellTower.Protocol/HookTrayStart.cs` | `2b1d532f965d73f4216dfa4570bfdc6a7a5776e4a8658075b44af1b953bda71e` |
| `src/BellTower.Protocol/TrayProcess.cs` | `b574599d0a66ed92d15f9199a263cea738d21a723c3c3fe4f1790382c3e19b1e` |
| `src/BellTower/Diagnostics/HookStartReporter.cs` | `3e8cd554eaa70da39620366877b9bc561452290bbf77544ba8d9cc482429d835` |
| `tests/BellTower.Tests.FakeLauncher/BellTower.Tests.FakeLauncher.csproj` | `9db7cf599338080094630fdfdb3629bab0faa9dc13f8b9ee0f0e9aba56c43469` |
| `tests/BellTower.Tests.FakeLauncher/Program.cs` | `d8d0dca8a173f79aa6e7dd0edce6f78a58469746542f3589c142f1194d6a108e` |
| `tests/BellTower.Tests.FakeLauncher/packages.lock.json` | `3e34d637a4170eb57a361f0c0229c99a5a4f3ab4619b5a60db7c00dc7984f1b8` |
| `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs` | `969107ec5f25ab13466652891044b72596fda277f60e67d59de36fe17d33ea9f` |

- Before/after identity: both passed with `head=5e95554 baseline=5e95554 files=23 diff=bceaf5143001 untracked=7/e6fa752a1eed`. Full manifests and hashes match; no unexpected mutation.
- Prior reports / rechecked findings: [ANALYSIS-042](analysis-042.md), canonical ANALYSIS-4100/4101; [ANALYSIS-041](analysis-041.md) and [QA-048](qa-048.md) for history and QA-4800 regression context. Prior reports remain unchanged.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Actual elevation / canonical identity | Source at `TrayProcess.cs:124–172,229–260,344–370`; six independent in-memory selector checks | Reads both type and actual elevated bit. Default/non-elevated and Limited/non-elevated select current; elevated Default refuses launch. Full/elevated selects a linked token only after checking Limited and non-elevated. Identity uses that verified linked Limited token; elevated unsplit Default keeps its own identity. | Required selection correction verified by source and bounded executed checks. Actual UAC token-pair equality and linked-token process creation remain unperformed. |
| Suspended creation / nested jobs | Source at `TrayProcess.cs:269–304`; direct-source native probe below | Both creation APIs receive `CREATE_SUSPENDED`; child any-job query precedes resume. The previously unsafe nested topology now returns `Failed/provider-job-breakaway-incomplete`. | Required nested-job correction verified by source and executed direct-source refusal. No native provider job claim. |
| API ownership / failure cleanup | Inspected created-child `try/finally`, `StopSuspended` and outer cleanup | Unsafe membership, failed inspection, failed resume and unexpected suspend count call termination on only the newly created child; process/thread handles close in `finally`. Termination failure includes a bounded diagnostic. Token, duplicate, linked handle, environment and allocations retain their cleanup. No wait for tray readiness or child exit. | No remaining blocking defect identified in inspected normal API and failure branches. Failed-query/resume/termination branches were inspected, not fault-injected. |
| Capture / parity / user stop | Read unchanged shared ingress, common start policy and production stop producers; inspected current fixtures | Capture precedes start; hook output stays neutral. Claude and Codex share one implementation and have separate immediate-refusal and nested-partial fixtures. Mixed hooks retain shared burst/mutex gates. Actual CLI and production menu handler coverage remain present; management stops remain plain `RequestExit`. | Source regression check passed. Production ingress fixtures were not executed by this reviewer on DEV-056. |
| Documentation / architecture / performance | Read current three documentation paths against design decision 9 and implementation | Docs describe actual elevation, suspended membership gate, safe refusal and unverified native Codex jobs. No new provider policy duplication. | No findings. No performance measurements or new performance hypothesis. |
| Build / tests / format | Not run against project outputs per assignment | At review time DEV-056 evidence records Protocol compilation, then blocked shared assembly copy; solution format access denied; folder whitespace verify passed. Developer preparing isolated build separately. | Current full project build, focused/full suite and AOT status are pending separate evidence. DEV-055's prior test results are not credited to DEV-056. |

Primary API contracts used: [TOKEN_ELEVATION](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-token_elevation) supplies the actual elevated bit; [IsProcessInJob](https://learn.microsoft.com/en-us/windows/win32/api/jobapi/nf-jobapi-isprocessinjob) with a null job checks any membership; [ResumeThread](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-resumethread) returns the previous count, so one is the expected successful release and `uint.MaxValue` is failure. [CreateProcessWithTokenW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw) supplies the selected-token creation path; [Nested Jobs](https://learn.microsoft.com/en-us/windows/win32/procthread/nested-jobs) explains why successful breakaway alone was insufficient. These are the primary references already supplied/fetched for this review sequence; no further Context7 request was made.

Independent direct-source probe: PowerShell `Add-Type` compiled the unchanged current `TrayProcess.cs` text in memory, adding only `System`/`System.IO` global imports, a two-value `HookStartOutcome` stub and a wrapper. It did not load or rewrite project outputs. Six selector checks covered Default elevated refusal, Default ordinary selection, Full linked selection, Limited selection and canonical Full/Limited identity. Each passed.

The wrapper invoked the production `TrayProcess.Start` method using a benign system executable in place of a launcher and an unused disposable data path. In the nested case, its owned helper first assigned itself to an outer kill-on-close/no-breakaway job and then an inner breakaway-allowed job, reproducing ANALYSIS-042's topology. The nested child was refused before resume. The ordinary helper inherited the agent environment's existing job constraint and safely refused creation; it does not establish successful ordinary startup. Exact output:

```text
six-selector-checks=True
mode=ordinary; outcome=Failed; detail=provider-job-breakaway-refused
six-selector-checks=True
mode=nested; outcome=Failed; detail=provider-job-breakaway-incomplete
```

The initial uncompressed helper command exceeded Windows' command-line length and launched nothing. The compressed in-memory retry above succeeded. No real tray, provider, default Bell Tower data directory or user setting was used. This proves the current source's selector behavior and native job refusal path under the wrapper; it is not a project test, NativeAOT publish, installed-launcher test or provider acceptance run.

## Findings

**No findings.** No remaining blocking defect was identified in the assigned corrections and inspected regressions. No new IDs or suggestions are raised. This conclusion is limited to source review and the direct-source probes above; it does not replace the outstanding build, fixture and native acceptance evidence.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-4100 | DEV-056; tracked `bceaf5143001`, untracked `e6fa752a1eed` | Actual elevated bit now gates selection; elevated Default refuses. Linked identity/launch requires Limited and non-elevated. Six executed selector checks include the required cases and canonical mapping. | Verified within source/probe scope. Native UAC pair and actual selected-token startup not performed. Orchestrator owns status. |
| ANALYSIS-4101 | Same | Both APIs create suspended; query child membership before resume; unsafe/query/resume paths terminate the owned child. Exact-source nested native probe now refuses partial breakaway. | Verified within source/probe scope. Matched ingress fixture execution pending separate evidence. |
| QA-4800 (regression context) | Same | Actual CLI parser/action and production menu click test remain at `HookTrayStartTests.cs:320–342`; management request remains separate. | Source regression check passed; no execution credited from the prior target. |

## Assessment and limitations

The two required corrections meet the reviewed implementation contract: an elevated Default token cannot implicitly launch the tray, and successful partial breakaway cannot resume a child still bound to a job. Existing shared capture, stop and management policy remains coherent for Claude, Codex and mixed hooks. No third implementation cycle is requested by this analysis.

Current project build and focused/full fixture results must come from the separate DEV-056 verification, not earlier DEV-055 results. Actual UAC token-pair equality, elevated linked-token startup, native Codex hook job behavior, installed host/restart trials and current NativeAOT publication remain unperformed here and are not waived. In particular, conservative safe failure retaining a queued capture is not proof that an actual Codex hook starts a usable tray. These remain evidence gaps rather than provider restrictions.

No shared output, implementation, test, previous report, user settings or production process was changed. Only this report was written. The orchestrator owns final acceptance, ledger disposition and any decision about unmet native evidence.

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-044

## Review target

- Reviewer / role: independent light final-target recheck subagent / code-analyzer.
- Client / version / mode / model (resolved ID): Codex; installed CLI `0.169.0` observed in ANALYSIS-042; delegated non-interactive Default mode; assigned resolved model `gpt-6.1-sol`, high reasoning. Host application version unavailable.
- Date: 2026-11-15T16:21:08Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-044, DEV-056 task 4.5.
- Task and requirement references: [task 4.5 and acceptance task 6.1](../tasks.md), [design decision 9](../design.md), [session-tracking scenarios](../specs/session-tracking/spec.md), [DEV-056 evidence](../evidence/implementation-056.md).
- Scope and relevant neighboring paths: only the post-ANALYSIS-043 compile repair in `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs:396–407`: public theory parameter changed from internal enum to `int`, equivalent numeric expected data, assertion cast. Check correctness, safety and preservation of cases; carry forward unchanged production analysis by hash. No suggestions or implementation changes.
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
| `tests/BellTower.Tests/Hosts/HookTrayStartTests.cs` | `5259cc3844b0437f19db6a39d4f23c82d8e9a288a1566559430c24f243349c0e` |

- Before/after identity: both passed with `head=5e95554 baseline=5e95554 files=23 diff=bceaf5143001 untracked=7/a5924b210bfa`. Full manifests and hashes match. No unexpected mutation.
- Prior reports / rechecked findings: [ANALYSIS-043](analysis-043.md), canonical ANALYSIS-4100/4101 and unchanged production evidence; [ANALYSIS-042](analysis-042.md) for the preceding defects. Prior reports are unchanged.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Exact repair boundary | Read current test; reverse only eight expected-value replacements, the public parameter type and assertion cast in memory; hash reconstructed text with `SHA256.HashData` | Reconstruction SHA-256 is `969107ec5f25ab13466652891044b72596fda277f60e67d59de36fe17d33ea9f`, exactly the ANALYSIS-043 test hash. No files written by reconstruction. | Confirms no other test edits or removed cases. |
| Expected-value correctness | Compare eight rows with `TrayProcess.cs:12`, `LaunchTokenChoice { Refuse, Current, LinkedLimited }` | Enum values are Refuse=0, Current=1, LinkedLimited=2; assertion converts the expected integer to the same enum used previously. | All eight row meanings preserved; mapping below. |
| Public signature / isolation | Inspect `HookTrayStartTests.cs:405–407` | Method now exposes only accessible primitive/nullable types; its body retains the same selector call and enum equality assertion. No process, fixture, provider or data-directory logic changed. | Removes the reported inconsistent-accessibility cause without changing test behavior. |
| Production carry-forward | Compare full target identity with ANALYSIS-043 | Tracked diff is identical and all six other untracked hashes are identical; only the test hash changes. | ANALYSIS-043 production source review and exact-source probe evidence remain applicable by byte identity. No new native probe needed for this signature edit. |
| Build / functional checks | Read DEV-056 evidence and orchestrator handoff; do not run competing builds/tests | Final isolated snapshot Release build is reported passed with SDK 10.0.115 after the signature repair. QA focused and root full tests are being handled separately. | Build result is supplied evidence, not independently rerun. No test result claimed here. |
| Performance / architecture | Scope check | Only test parameter/data representation changed. | No production performance or architecture effect identified; no measurement performed. |

All selector cases remain:

| Elevation type | Actual elevated | Numeric expected | Preserved enum meaning |
| --- | --- | --- | --- |
| Default (1) | false | 1 | Current |
| Default (1) | true | 0 | Refuse |
| Full (2) | true | 2 | LinkedLimited |
| Full (2) | false | 0 | Refuse |
| Limited (3) | false | 1 | Current |
| Limited (3) | true | 0 | Refuse |
| unknown (null) | false | 0 | Refuse |
| Default (1) | unknown (null) | 0 | Refuse |

## Findings

**No findings.** The narrow compile repair preserves every test case and assertion meaning. No reserved finding ID was used and no suggestion is raised.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-4100 | Final DEV-056: tracked `bceaf5143001`, untracked `a5924b210bfa` | Production `TrayProcess.cs` hash remains `b574599d0a66ed92d15f9199a263cea738d21a723c3c3fe4f1790382c3e19b1e`. Carry forward ANALYSIS-043 source review and six exact-source selector checks. Eight production selector theory rows are preserved by the repair. | Verified within the same source/probe scope; native UAC comparison and selected-token startup remain unperformed. |
| ANALYSIS-4101 | Same | Identical production and fake-launcher source hashes carry forward ANALYSIS-043 suspended-child review and executed nested-job refusal probe. No job fixture or ingress case changed. | Verified within the same source/probe scope; current QA fixture results belong to separate reports. |
| Test CS0051 repair | Final test hash `5259cc3844b0437f19db6a39d4f23c82d8e9a288a1566559430c24f243349c0e` | Exact in-memory reconstruction matches the prior hash; primitive public parameter and equivalent cast inspected; successful isolated build supplied by developer/orchestrator. | Repair verified by source and supplied build evidence. |

## Assessment and limitations

The final target retains ANALYSIS-043's production conclusions while repairing the test compilation issue without coverage loss. This light review does not repeat or relabel the earlier probes as new executions. No remaining blocking issue was identified in the assigned scope.

Focused/full test execution is delegated separately and is not claimed here. Native UAC token-pair equality, actual elevated linked-token tray startup, native Codex hook job behavior, current NativeAOT publication and installed host/restart acceptance remain unperformed in this analysis sequence and are not waived. Safe queued-capture refusal still must not be described as proof that an actual Codex hook starts a usable tray. The orchestrator owns finding disposition and final acceptance.

Only this assigned report was written. No source, tests, earlier reports, shared build outputs, user settings or production processes were changed.

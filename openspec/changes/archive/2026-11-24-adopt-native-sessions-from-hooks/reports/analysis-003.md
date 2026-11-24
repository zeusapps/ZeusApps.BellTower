# Analysis report: adopt-native-sessions-from-hooks / 003

## Review target

- Reviewer / role: setup_analysis / code-analyzer.
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / delegated Default mode / gpt-6.1-sol (high).
- Date: 2026-10-29T19:57:34Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-003 final setup recheck.
- Task and requirement references: partial task 5.1; ANALYSIS-100 and duplicate QA-100; regression for existing managed presentation and native-only setup isolation.
- Scope: exact sixteen-file final setup target below. Read-only reference reports: [analysis 002](analysis-002.md) and [QA 003](qa-003.md).
- Baseline commit: `a69392e9ae6734b7f229d9266ebe4a3144ef49a4`.
- Prior reports / rechecked findings: ANALYSIS-100 from analysis 002; original shared bridge corrections checked only for regression.
- Implementation identity before review:

```text
HEAD: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Baseline: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Paths: docs/installation.md openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-003.md openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-005.md src/BellTower/Cli/SetupCommand.cs src/BellTower/Setup/DesktopSetup.cs src/BellTower/Setup/IntegrationOwnership.cs src/BellTower/Setup/IntegrationSetup.cs src/BellTower/Setup/JsonSettingsFile.cs src/BellTower/Setup/OwnedSettings.cs src/BellTower/Setup/TerminalProfilesEditor.cs src/BellTower/Setup/VsCodeBridgeSettings.cs tests/BellTower.Tests/Cli/NativeSetupCliTests.cs tests/BellTower.Tests/Setup/DesktopSetupTests.cs tests/BellTower.Tests/Setup/NativeHostSetupTests.cs tests/BellTower.Tests/Setup/ProjectProfileTests.cs
Manifest (A added, M modified, D deleted, ? untracked):
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-003.md
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-005.md
  ? src/BellTower/Setup/OwnedSettings.cs
  ? src/BellTower/Setup/VsCodeBridgeSettings.cs
  ? tests/BellTower.Tests/Cli/NativeSetupCliTests.cs
  ? tests/BellTower.Tests/Setup/NativeHostSetupTests.cs
  M docs/installation.md
  M src/BellTower/Cli/SetupCommand.cs
  M src/BellTower/Setup/DesktopSetup.cs
  M src/BellTower/Setup/IntegrationOwnership.cs
  M src/BellTower/Setup/IntegrationSetup.cs
  M src/BellTower/Setup/JsonSettingsFile.cs
  M src/BellTower/Setup/TerminalProfilesEditor.cs
  M tests/BellTower.Tests/Setup/DesktopSetupTests.cs
  M tests/BellTower.Tests/Setup/ProjectProfileTests.cs
Tracked diff SHA-256: 1becc3a551efd333e8e037d07595b8e1ff0fdba806a44a5adfe3ce22f65835b2
Untracked file SHA-256:
  8006e8bc263cf34a8df88647113f6c70ae6b0905f2ddca3e1a7569ef4729c68a  openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md
  2fe355e8c629aa1da5ee9079de65b92ff4c0ab33e90be97d2f84efcc7ed545c8  openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-003.md
  39e5bd6f7ea0aa498dc71b98fb97e32a8cb58afc6e9e929e2d058bc127054194  openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-005.md
  cb60593b535118dffc1c2920f314d0f25851f8ec4ecc441ff8fa7e6293fd9487  src/BellTower/Setup/OwnedSettings.cs
  299e9d1888f43be1a68acc88ed1f320628c9a9344b449bf09524d693647fa4c1  src/BellTower/Setup/VsCodeBridgeSettings.cs
  6b28f51683703d148db7259a2d2d91ea0218e00fd978ba8d17d9402f216244b5  tests/BellTower.Tests/Cli/NativeSetupCliTests.cs
  465935163c36f698b7f9c60a21e4ca390a7764fba8d58d916a69f60706765c16  tests/BellTower.Tests/Setup/NativeHostSetupTests.cs
Summary: head=a69392e baseline=a69392e files=16 diff=1becc3a551ef untracked=7/061edc8d545b
```

- The exact same command and sixteen paths produced identical full output after review. Every tracked and untracked hash matched.
- Before/after summary: `Summary: head=a69392e baseline=a69392e files=16 diff=1becc3a551ef untracked=7/061edc8d545b`.
- The report itself, ledger and concurrent native-probe outputs are outside the frozen implementation identity.

## Analysis performed

| Area | Independent method / evidence | Outcome / limitation |
| --- | --- | --- |
| ANALYSIS-100: retain shared tab presentation | Inspected `TerminalProfilesEditor.VsCode` and the call path through `DesktopSetup.Plan` | Passed source recheck: the removal decision now considers remaining desktop providers, project consumers and retained VS Code profile ownership before restoring the tab-title value. |
| Paired provider removal | Inspected both provider-order cases in `DesktopSetupTests.PartialProviderRemovalRetainsTabTitleUntilTheLastDesktopProviderLeaves` and source consumer calculation | Passed source/coverage check for Claude-first and Codex-first removal, with prior personal value restored after the last consumer. Fixtures were not executed by this analyzer. |
| Project-only removal | Inspected project ownership updates and `ProjectRemovalRetainsSharedTabTitleUntilAllDesktopProfilesLeave` | Passed source/coverage check: project removal retains presentation while generic or other project consumers remain. |
| Retained edited profiles and manual title values | Inspected remaining-profile predicate, `OwnedSettings` conditional restoration and added desktop assertions | Passed source recheck: retained edited profiles keep the presentation request; later user-edited tab titles are preserved during partial and final removal. |
| Native-only presentation isolation | Traced host-bridge-only setup, which skips DesktopSetup; inspected native setup assertion for absent tab-title setting | Passed source recheck: native-only setup acquires bridge connection settings without title/profile/import ownership. |
| Earlier shared bridge corrections | Inspected retained per-provider native consumers, shared settings helper and one-file planned-edit merge | Passed regression source check: the final title guard does not alter shared bridge settings/removal policy. The helper hashes match the prior corrected-target review. |
| Documentation and handoff limits | Read `docs/installation.md` scoped changes and `evidence/implementation-005.md` | Passed: setup option/removal descriptions retain trust/live-host caveats and do not assert complete native terminal support. |
| Functional tests and formatting | Read independent QA 003 and developer handoff | Not run by this analyzer. QA 003 records 59 passing Release fixture tests on this same sixteen-file target; the developer records the narrower checks. These are attributed evidence, not analyzer execution. |
| Native execution, extension actions and provider setup | No such operation performed | Not performed. No personal settings, runtime, database, terminal, security control or process was touched. |
| Target integrity | Exact review-identity command before/after | Passed, full output matched. |
| Performance | No performance change or hypothesis in this bounded correction | Not applicable. |

## Findings

**No new findings.** ANALYSIS-100 is corrected at the reviewed target. No suggestion or additional finding ID was allocated.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-100 / duplicate QA-100 | Sixteen-file identity above | Source inspection of the remaining-consumer guard, owned-value restoration and paired provider/project/edited-profile regression cases; independent QA 003 reports matching-target fixture success | Verified by source recheck. Functional fixture evidence is separately attributable to QA 003; native acceptance remains unperformed. |

## Assessment and limitations

The tab-title retention regression is corrected. Source review found no additional acceptance defect in this bounded recheck, and the independent QA report covers functional fixtures on the same target. The orchestrator owns the ledger decision; this report does not close findings itself.

This approves only the reviewed setup correction. It does not complete task 5.1's native trust/live bridge acceptance or the broader native-adoption change. Full terminal-support scope remains intact. No source, test, planning, ledger or earlier report was edited; only this reserved report was written, and target identity stayed unchanged.

The separate [binder architecture report](analysis-004.md) is already saved. It describes a conditional metadata-only local-log probe, not a proven binder. Its ordinary TUI event availability and exact current conversation/process semantics still require isolated native evidence. No provider database read or sandbox/setup change was authorized or performed by this analyzer.


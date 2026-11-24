# Analysis report: adopt-native-sessions-from-hooks / 002

## Review target

- Reviewer / role: setup_analysis / code-analyzer.
- Client / version / mode / model (resolved ID): Codex CLI 0.165.0 / delegated Default mode / gpt-6.1-sol (high).
- Date: 2026-10-29T16:54:41Z.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-002 recheck.
- Task and requirement references: task 5.1; [native-command setup](../specs/standalone-setup/spec.md); existing managed setup and presentation preservation; ANALYSIS-001 and ANALYSIS-002 from [analysis 001](analysis-001.md), canonical duplicates QA-001 and QA-002.
- Scope: the exact thirteen-file corrected setup target below. Read-only neighbors include `SetupPlan.cs`, `ProviderSettingsEditor.cs`, `DesktopOwnership.cs` and `VsCodeBridgeSetup.cs`.
- Baseline commit: `a69392e9ae6734b7f229d9266ebe4a3144ef49a4`.
- Prior reports / rechecked findings: [analysis 001](analysis-001.md), ANALYSIS-001/ANALYSIS-002 only plus correction regression.
- Implementation identity before review:

```text
HEAD: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Baseline: a69392e9ae6734b7f229d9266ebe4a3144ef49a4
Paths: docs/installation.md openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-003.md src/BellTower/Cli/SetupCommand.cs src/BellTower/Setup/DesktopSetup.cs src/BellTower/Setup/IntegrationOwnership.cs src/BellTower/Setup/IntegrationSetup.cs src/BellTower/Setup/JsonSettingsFile.cs src/BellTower/Setup/OwnedSettings.cs src/BellTower/Setup/TerminalProfilesEditor.cs src/BellTower/Setup/VsCodeBridgeSettings.cs tests/BellTower.Tests/Cli/NativeSetupCliTests.cs tests/BellTower.Tests/Setup/NativeHostSetupTests.cs
Manifest (A added, M modified, D deleted, ? untracked):
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md
  ? openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-003.md
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
Tracked diff SHA-256: 54893ef6671a508932171f9629ab3a745b7249989ea055be7e4f0b64c976f7d9
Untracked file SHA-256:
  8006e8bc263cf34a8df88647113f6c70ae6b0905f2ddca3e1a7569ef4729c68a  openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-002.md
  2fe355e8c629aa1da5ee9079de65b92ff4c0ab33e90be97d2f84efcc7ed545c8  openspec/changes/adopt-native-sessions-from-hooks/evidence/implementation-003.md
  cb60593b535118dffc1c2920f314d0f25851f8ec4ecc441ff8fa7e6293fd9487  src/BellTower/Setup/OwnedSettings.cs
  299e9d1888f43be1a68acc88ed1f320628c9a9344b449bf09524d693647fa4c1  src/BellTower/Setup/VsCodeBridgeSettings.cs
  6b28f51683703d148db7259a2d2d91ea0218e00fd978ba8d17d9402f216244b5  tests/BellTower.Tests/Cli/NativeSetupCliTests.cs
  465935163c36f698b7f9c60a21e4ca390a7764fba8d58d916a69f60706765c16  tests/BellTower.Tests/Setup/NativeHostSetupTests.cs
Summary: head=a69392e baseline=a69392e files=13 diff=54893ef6671a untracked=6/57b674b020cb
```

- The same command with exactly these paths produced identical full output after review. Every tracked/untracked hash matched.
- Before/after summary: `Summary: head=a69392e baseline=a69392e files=13 diff=54893ef6671a untracked=6/57b674b020cb`.
- Concurrent native-probe scratch and evidence paths were outside this frozen target.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| ANALYSIS-001: shared bridge settings | Source trace through `IntegrationSetup`, `VsCodeBridgeSettings`, `OwnedSettings` and profile editing; inspected new fixture assertions | Passed source recheck: native-only setup now writes the two bridge connection settings independently; retention follows remaining consumers; unchanged prior values restore and later user edits are preserved. |
| ANALYSIS-002: provider and generic removal | Inspected per-provider set copy, union/subtraction, remaining-consumer calculation and action request | Passed source recheck for Claude, Codex and mixed native/desktop ownership: selected removal releases only selected native consumers and generic removal follows the same rule. |
| Same-file merge and mutable snapshots | Inspected `JsonSettingsFile.Set`, `VsCodeBridgeSettings.Plan` and ownership copies | Passed source check: planned profile edits feed the second editor, one combined FileChange retains the original Before snapshot, and Root gets cloned values. Desktop ownership is deep-copied before shared setting updates. |
| Restoration and action failure | Inspected `OwnedSettings`, existing `SetupPlan.Apply` and new retry fixture | Passed source check: restored-before values drop stale metadata on retry, changed user values remain, and failed removal actions do not commit the removal manifest. Actual extension failure was not executed. |
| Presentation regression from extraction | Compared corrected `TerminalProfilesEditor.VsCode` with baseline | Failed: removal now restores the shared tab-title setting unconditionally, even while another managed desktop provider remains. ANALYSIS-100 below. |
| Native-only profile isolation | Inspected host-bridge branch and new fixture assertions | Passed source check: native-only setup skips DesktopSetup and sets only bridge connection settings; no tab title, terminal profile, module or import is generated by that branch. |
| Installation documentation | Inspected scoped diff in `docs/installation.md` | Passed: new option/removal examples match the implemented CLI and retain native trust/live binding limitations. Full terminal-support scope is not waived or claimed complete. |
| Functional/build/formatter checks | Not rerun by analyzer; separate QA recheck assigned | Not performed by analyzer. Developer reports 52 fixture tests and formatting passed; this is not independent executed evidence. |
| Native/host trials | No provider/extension/session operations performed | Not performed; this review cannot establish actual hook trust or host acceptance. |
| Target integrity | Exact review-identity command before and after | Passed: full output matched. |
| Performance | No performance hypothesis or optimization introduced by this review | Not applicable. |

## Findings

### ANALYSIS-100: Partial desktop removal now restores the other provider's tab presentation

- Severity: warning.
- Related task / requirement: task 5.1 preservation of existing managed setup and other-provider settings; regression check for the shared-settings extraction made under ANALYSIS-001.
- Location at the corrected reviewed identity: `src/BellTower/Setup/TerminalProfilesEditor.cs:70`; call path `src/BellTower/Setup/DesktopSetup.cs:63`.
- Observation: the baseline guarded all shared settings restoration with `if (!remove || ownership.Providers.Count == 0)`. The correction moved bridge settings into their new shared helper but made the remaining `terminal.integrated.tabs.title` restoration unconditional. A desktop installation for both providers sets that value to `${sequence}`. Removing only Claude now restores/removes it while Codex profiles, module/imports and desktop ownership remain installed. Project-profile removal can likewise restore the shared setting while generic/other project profiles remain. The new native-only ownership logic does not prevent this because this setting is edited earlier by TerminalProfilesEditor.
- Proposed correction and benefit: retain the desktop tab-title setting while any applicable managed desktop profile/provider still needs it; restore its unchanged owned value only after the final desktop consumer is removed. Keep native-only setup from acquiring this presentation setting.
- Behavior-preservation risks: partial provider removal, project-only removal, retained edited profiles, prior personal tab-title settings and later user edits. Existing conditional restoration semantics must remain intact.
- Evidence type: source observation and baseline diff; no live VS Code operation.
- Reproduction: source-trace sequence, not executed by analyzer: install desktop setup for `both`; remove only `claude`; `DesktopSetup` retains Codex but still calls `TerminalProfilesEditor.VsCode(... remove: true)`, which now calls OwnedSettings removal at line 70. Its unchanged owned tab-title value is restored even though Codex remains.
- Verification plan: an isolated both-provider desktop fixture must retain the Bell Tower tab-title setting after either provider is removed first, restore the original only after the final provider is removed, and preserve it during project-only removal while other desktop consumers remain. Verify native-only mode still never acquires the setting.
- Relationship: new warning regression introduced by the ANALYSIS-001 correction. No new suggestions are raised.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 / canonical QA-001 | Thirteen-file identity above | Shared bridge settings helper is called for native or desktop consumers; owned settings handling and same-file merge inspected | Verified by source recheck for the original defect; functional/native acceptance is separate. |
| ANALYSIS-002 / canonical QA-002 | Thirteen-file identity above | NativeBridgeProviders copy/union/subtraction and remaining-consumer policy inspected | Verified by source recheck for the original defect; functional/native acceptance is separate. |

## Assessment and limitations

The two original findings are corrected at the reviewed target. One warning regression remains in managed desktop presentation retention and should be routed through the allowed fix gate before accepting this setup correction. This report does not close ledger entries; the orchestrator owns triage.

No live extension or provider operation was performed, and no native terminal-support gap was accepted as a provider restriction. Only this reserved report was written. Earlier reports remain unchanged, and the full corrected target identity matched before and after review.


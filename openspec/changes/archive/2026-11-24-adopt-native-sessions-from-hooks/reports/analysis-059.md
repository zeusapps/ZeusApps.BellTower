> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-059); client: Claude Code 2.1.331, code-analyzer subagent; mode: read-only analysis (resumed once after a rate limit); model: claude-opus-5-5.
> Refusal (as reported by the reviewer): the client refused the write of this report; the reviewer did not retry.

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-059 (recheck of ANALYSIS-5800(a) and ANALYSIS-5801 against DEV-068)

## Review target

- Reviewer / role: ANALYSIS-059 / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code 2.1.331, code-analyzer subagent (read-only analysis; the only write would be this report, which the client refused), claude-opus-5-5
- Date: 2026-11-24T21:07Z to 2026-11-24T21:32Z. The run was interrupted by a rate limit and resumed without redoing completed analysis. Identity was measured again on resume.
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks`, ANALYSIS-059 (recheck)
- Task and requirement references: tasks 5.2 and 5.3 ([tasks](../tasks.md)); attention-notifications "Viewed wait timer"; [implementation 068](../evidence/implementation-068.md)
- Scope and relevant neighboring paths:
  - `src/BellTower/Diagnostics/{HookIngressImageReporter,HookStartReporter,HookDeliveryReporter,CapabilityMatrix,DiagnosticsReporter,ConfirmationCoverageReporter}.cs`
  - `tests/BellTower.Tests/Diagnostics/{HookIngressImageReporterTests,CapabilityMatrixTests}.cs`
  - `docs/{compatibility,installation,follow-ups}.md`
  - Neighbors read for the recorded-command contract: `src/BellTower/Setup/{HookCommandBuilder,IngressProvision,ProviderSettingsEditor,IntegrationSetup,IntegrationOwnership}.cs`
- Baseline commit: 9550ced
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 9550ced -Paths src tests docs vscode scripts`, HEAD 9550ced12d19d6a2d4e0f857e64879ff1d8007c6.
  - The manifest has 22 entries: 2 untracked and 20 modified, as assigned.
  - Tracked diff SHA-256 4e615f3d02c37422f1006888afa4814fb022354248bb2b8e39e611133a54e2dd.
  - Untracked hashes: `src/BellTower/Diagnostics/HookIngressImageReporter.cs` 8ea1da71..., `tests/BellTower.Tests/Diagnostics/HookIngressImageReporterTests.cs` 28b03abb....
  - Summary `head=9550ced baseline=9550ced files=22 diff=4e615f3d02c3 untracked=2/a2e19607ee87`
- Before/after identity: the summary was the same on resume and after the analysis: `head=9550ced baseline=9550ced files=22 diff=4e615f3d02c3 untracked=2/a2e19607ee87`. No unexpected mutation.
- Prior reports / rechecked findings: [analysis 058](analysis-058.md), ANALYSIS-5800(a) and ANALYSIS-5801. ANALYSIS-5800(b), 5802 and 5803 are deferred and were not reopened. They were checked only for how `docs/follow-ups.md` records them.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Shared logic for both providers | Inspection of `HookIngressImageReporter.cs` 21-47 and `HookStartReporter.cs` 19-28 | One `Evaluate`/`RecordedHash` rule serves both providers. Only the settings path differs, and it comes from `HookDeliveryReporter.RecordedCommands` | No per-provider policy duplication |
| Shell-form parsing | `HookCommandBuilder.cs` 17-46 compared with the `CommandPattern` regex (`HookIngressImageReporter.cs` 68) | Claude bash literal (`/`), Codex PowerShell `& '...\'` (`\`), Cmd `"..."` (`\`; `%` escaping does not touch the fixed hash segment), Windows bash (`/`) and the non-Windows bash literal (native separator) all match. A data-dir quote only changes earlier segments. The legacy `-EncodedCommand` form, a hashless path and an older `provider-ingress/<other revision>` path give null, which is not counted | Parsing is safe. An unparseable command never becomes stale |
| Duplicated hash/path parsing | `grep provider-ingress` and `"bell-tower-ingress.exe"` across `src` | The command-path regex is the only parser of the hook path. `SelectedHash` (`HookIngressImageReporter.cs` 49-66) repeats the installation resolution of `HookDeliveryReporter.CodexShellStatus` (61-71): process path, then `installation.json` or `ForPayload`, then `Contract(...).Files["bell-tower-ingress.exe"]` | Small, bounded repetition (about 7 lines). Recorded under ANALYSIS-5800(a) and not raised separately (recheck rule) |
| Source of the compared commands (manifest or live settings) | `ProviderSettingsEditor.cs` 25 and 44; `IntegrationSetup.cs` 42 and 187; `grep Commands.Remove` | Setup keeps the owned-command list for each settings path as an append-only history: `previousCommands.Append(command).Distinct()`. Only `--remove` deletes it. After setup is rerun with a new image, the manifest holds both the old-hash and the new-hash commands | **ANALYSIS-5900** |
| Doctor cost | Inspection of `HookStartReporter` and `HookIngressImageReporter.Provider` | Reads `integration.json` twice and resolves the installation and `release.json` twice (once per provider). It reads no provider settings file. All files are small and bounded | Acceptable. Using the live settings (the ANALYSIS-5900 fix) would add at most one bounded read of each settings file that `hookDelivery` already parses, unless the result is shared |
| Matrix, doctor and docs wording | Diff of `CapabilityMatrix.cs`, `DiagnosticsReporter.cs` 101, `CapabilityMatrixTests`, `docs/compatibility.md`, `docs/installation.md`; `grep` for "assumed until the 6.1", "reset is assumed" and "Failed navigation leaves the timer" in `docs` (no match) | The exit-reset wording is one constant (`ExitResetEvidence`) shared by the matrix and doctor, with matching text in docs. It is pinned by `CapabilityMatrixTests` together with the absence of "assumed". The first-hook timing and the about 4 s latency are stated the same way in the matrix, doctor and docs for both providers | Honest. One caveat on the `ingressImage` vocabulary is in the assessment |
| Follow-ups record | Read the new section of `docs/follow-ups.md`; compared with analysis-058, analysis-054 line 34 and analysis-051 | The section records 5800(b) with the Codex trust reason, 5802 (probe first), 5803 with 2405 (cache and log-once), the about 4 s latency (both providers, cause not investigated), 5100 (typed reason, not yet a defect) and 5401 (about 0.5 s; analysis-054 measured a bare PowerShell start at 486 to 578 ms). The 5803 entry leaves out that 4.12 widened the path's reach; that is not an inaccuracy | Accurate |
| Build, tests, format verify | Not run | Owned by QA-064 | Not performed (assignment) |

Client coverage: every check covers Claude Code and Codex. ANALYSIS-5900 affects both providers equally, because both use the same manifest path and rule.

## Findings

### ANALYSIS-5900: The stale verdict cannot clear after the advised setup refresh, because setup keeps old owned commands

- Severity: warning (doctor permanently gives false advice for exactly the update case it targets; read-only, no data risk; blocks verification of ANALYSIS-5800(a))
- Related task / requirement: 5.2; ANALYSIS-5800(a)
- Locations (reviewed identity above):
  - `src/BellTower/Diagnostics/HookIngressImageReporter.cs` 25-39: `Evaluate` over `RecordedCommands`; stale if any recorded hash differs
  - `src/BellTower/Diagnostics/HookDeliveryReporter.cs` 17 (`RecordedCommands` returns the manifest list) and 99-119
  - `src/BellTower/Setup/ProviderSettingsEditor.cs` 25 and 44: `ownership.Commands[path] = previousCommands.Append(command).Distinct(StringComparer.Ordinal).ToList();`
  - `src/BellTower/Setup/IntegrationSetup.cs` 42: copies the previous commands forward
  - `tests/BellTower.Tests/Diagnostics/HookIngressImageReporterTests.cs` 22-34: each fixture records a single command per path
  - `docs/installation.md`, the hook-start paragraph: describes the comparison as "the owned hook commands setup recorded"
- Observation:
  - `integration.json` `Commands` is an ownership history, kept so that setup can recognize and remove every command form it ever wrote. It is not the current hook set.
  - The scenario: an update moves the selected image from hash A to hash B, doctor reports `stale`, and the user runs `setup --host-bridge --apply` as advised. The settings file now calls B, but the manifest lists `[A-command, B-command]`. `Evaluate` finds A different from the selected B and keeps reporting `stale`. `availability` keeps saying "needs a setup refresh" for both providers indefinitely, and only `--remove` clears it.
  - A rollback (B, then A again) is also misreported, because the list keeps B.
  - The manifest also says nothing about whether a command is still in the settings file. A user who deleted the hooks by hand still gets a `current` or `stale` verdict, while `hookDelivery.configured` is false.
  - The tests do not catch this, because no fixture records more than one command for a path.
- Proposed improvement and benefit:
  - Evaluate the owned commands that are actually present in the provider settings file: the intersection that `HookDeliveryReporter.ConfiguredHooks` / `OwnedCommandsIn` already computes, with the manifest as the ownership filter. This is the right source for the question "which image do the hooks call".
  - It also gives the correct answer for an edited handler that setup kept (old hash still live: stale) and for removed hooks (none present: unknown).
  - Expose the live owned command list from `HookDeliveryReporter` once (for example, return it from the same parse), rather than parsing a second time. An unreadable or oversized settings file gives `unknown`.
  - Do not use "the last recorded command" instead. `Distinct` keeps the first position, so after a rollback the last entry is not the current command.
  - Adjust the `installation.md` sentence to say "the owned hook commands present in the provider settings".
  - Benefit: the stale advice clears after the refresh it asks for, and the verdict agrees with `hookDelivery.configured`.
- Behavior-preservation risks:
  - Doctor reads the provider settings files, which it already does for `hookDelivery`. No writes.
  - Keep the parse bounded (1 MB, `MaxDepth 32`, as now).
  - Keep `unknown` for encoded legacy commands and for unreadable settings.
- Evidence type: source observation. The scenario follows from the setup code. It was not executed: setup apply is prohibited and tests are owned by QA.
- Verification / measurement plan: fixtures for both providers and each Codex shell form:
  - (1) The manifest records `[Older, Selected]` and the settings file contains only the Selected command: the verdict is `current` and `availability` is "available".
  - (2) The same manifest with a settings file that contains the Older command (a retained edited handler): `stale`.
  - (3) A manifest entry whose settings file has no owned command, or does not exist: `unknown`.
  - (4) An oversized or malformed settings file: `unknown`.
- Relationship: a defect in the DEV-068 correction of ANALYSIS-5800(a), which remains open until it is fixed. The 5800(a) behavior-preservation note required that "unknown, never stale" hold for each shell form, and that part holds. This finding concerns the source of the commands.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-5800(a) | `head=9550ced ... files=22 diff=4e615f3d02c3 untracked=2/a2e19607ee87` | The reporter exists for both providers, is read-only and writes nothing. Every `HookCommandBuilder` form parses, and unparseable or unselected cases are `unknown`. `hookStart.available` is unchanged and `availability` carries the advice. However, it compares the append-only manifest history rather than the live owned hooks, so `stale` never clears after the advised refresh (ANALYSIS-5900). The selected-hash resolution repeats `CodexShellStatus` (minor) | still failing (ANALYSIS-5900) |
| ANALYSIS-5801 | same | `docs/installation.md` now states that a chip click on a shared-backend session hides the timer once the reason is shown, and that every other failed click leaves it visible, for both providers. The old sentence is gone (`grep`: no match). The Overview and CLI sentence is unchanged. The `docs/compatibility.md` "Mode cross-check" bullet adds the managed-daemon recognition. This matches `NavigationOutcome` and the spec scenario | verified |

## Assessment and limitations

- Scope covered: the DEV-068 diagnostics, test and docs changes for ANALYSIS-5800(a) and ANALYSIS-5801, plus a regression check of the matrix, doctor and docs wording and of `docs/follow-ups.md`. All of it covers Claude Code and Codex.
- Result: ANALYSIS-5801 is verified. ANALYSIS-5800(a) is still failing because of ANALYSIS-5900 (warning).
- On the developer's choice to compare the recorded manifest commands rather than the live settings: this is not acceptable as built.
  - Avoiding the settings read saves nothing material, because `hookDelivery` already parses both files in the same doctor run.
  - The manifest is an ownership history, so the verdict is wrong after the very refresh it advises, and it can disagree with `hookDelivery.configured`.
- Note for triage, not a new finding: doctor now has two fields named `ingressImage` with different evidence and vocabularies.
  - `confirmationCoverage.*.ingressImage` gives "current", "outdated: rerun setup" or "no capture seen yet", based on the producer shape of the latest capture.
  - `hookStart.ingressImage.*` gives `current`, `stale` or `unknown`, based on the hash.
  - For the same provider they can read "current" and "stale" at once without either being wrong. The orchestrator may want the docs to say which one is authoritative for the image hash.
- Deferred items ANALYSIS-5800(b), 5802 and 5803 were not reopened. Their `docs/follow-ups.md` entries are accurate.
- Not performed:
  - Build, tests and format verify (QA-064).
  - Any live, installed or setup check (prohibited).
  - Execution of the ANALYSIS-5900 scenario, which was derived from source.
- Unexpected writes: none. The client refused the write of this report, so the text is returned inline. Identity was unchanged on resume and after the analysis.

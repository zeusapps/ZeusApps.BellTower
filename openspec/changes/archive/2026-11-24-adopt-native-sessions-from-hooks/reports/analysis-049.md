> Saved verbatim by the orchestrator after a refused write.
> Reviewer: code-analyzer subagent (ANALYSIS-049); client: Claude Code, Agent SDK subagent (client version not observed); mode: non-interactive; model: claude-opus-5-5.
> Refusal (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

# Analysis report: adopt-native-sessions-from-hooks / ANALYSIS-049 (final recheck of the DEV-061 corrections)

> Write refused (exact): "Subagents should return findings as text, not write report files. Include this content in your final response instead."

## Review target

- Reviewer / role: independent code analyzer / code-analyzer
- Client / version / mode / model (resolved ID): Claude Code, Agent SDK subagent (client version not observed in this session) / non-interactive / `claude-opus-5-5`
- Date: 2026-11-23T21:18Z (UTC)
- Change root / assignment: `openspec/changes/adopt-native-sessions-from-hooks` / ANALYSIS-049 (final recheck, fix cycle 2)
- Task and requirement references:
  - [tasks](../tasks.md) 4.6 to 4.10; [design](../design.md) decisions 9 to 13
  - [session-identity](../specs/session-identity/spec.md) "Provider-owned terminal titles"; [session-tracking](../specs/session-tracking/spec.md)
  - developer evidence [implementation-061](../evidence/implementation-061.md); orchestrator decision for 4800 at the end of [findings](findings.md)
- Scope (the DEV-061 diff) and neighbors read:
  - `src/BellTower/IO/ProviderTitleState.cs`, `src/BellTower/Tray/TrayApplication.cs`, `src/BellTower/Setup/HookStartTaskSetup.cs`, `src/BellTower/Setup/ProviderTitleSettings.cs`, `src/BellTower/Diagnostics/DiagnosticsReporter.cs`
  - `docs/installation.md`, `docs/compatibility.md`
  - `tests/BellTower.Tests/Hosts/ProviderTitleStateTests.cs`, `tests/BellTower.Tests/Setup/ProviderTitleStepTests.cs`
  - neighbors: `src/BellTower/IO/AtomicFile.cs`, `src/BellTower/IO/Json.cs`, `src/BellTower/IO/DataPaths.cs`, `src/BellTower/Hosts/TerminalTitlePump.cs`, `src/BellTower/Hosts/IsolatedTerminalPresentationWriter.cs`, `src/BellTower/Hosts/WindowsTerminalNavigator.cs`
- Baseline commit: `2e7f318`
- Implementation identity (before): `./scripts/review-identity.ps1 -Baseline 2e7f318 -Paths src tests docs vscode`
  - HEAD `2e7f3182b6b61d9e5e570a20289b915889df4da1`; 40 tracked modified and 21 untracked paths (manifest as printed)
  - tracked diff SHA-256 `6c6beec81cd81d5f35e56290f485f4d4c93b85e3ffd23257a597167f488dcfb8`
  - untracked hashes include `2496bf99…` ProviderTitleState.cs, `5ac36eb0…` HookStartTaskSetup.cs, `aba77330…` ProviderTitleStateTests.cs, `4e67ea5e…` ProviderTitleStepTests.cs
  - summary `head=2e7f318 baseline=2e7f318 files=61 diff=6c6beec81cd8 untracked=21/a701204182dd` (matches the assignment)
- Before/after identity: after the analysis the same command printed `head=2e7f318 baseline=2e7f318 files=61 diff=6c6beec81cd8 untracked=21/a701204182dd` with the same tracked diff SHA-256. Identical; no unexpected mutation.
- Prior reports / rechecked findings: [analysis-048](analysis-048.md) (ANALYSIS-4800, ANALYSIS-4801); [qa-055](qa-055.md).

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| 4800: record key and shape | Source, `ProviderTitleState.Observe:78-164`, `ReadRecords:167-178` | Key is `provider|<GetFullPath(settings file), upper-cased>`; value `Entry(Since, Stamp)`. A process resolving another home reads and writes another key | Matches the decision |
| 4800: doctor read-only | Source, `Report:70-74`, `DiagnosticsReporter.cs:91-93` | `Report` calls `Observe(record: false)`; every write or remove in `Observe` is guarded by `record`. With no record it answers from the file without storing | Matches the decision |
| 4800: unknown state | Source, `Observe:101-130`, `Read:193-214` | Over 1 MiB, stat or read I/O failure, JSON/TOML parse failure give `Unknown`; it is not cached (`TryRemove`), and the switch returns `entry?.Since` without writing or removing | Matches the decision for the settings file. See ANALYSIS-4900 for the record file |
| 4800: tray tick | Source, `TrayApplication.cs:287-295` | `ObserveBoth()` runs once per two-second overview block, before the title, name and overview refresh | Matches the decision. Placement causes ANALYSIS-4900 |
| 4800: which processes decide | Source, `IsolatedTerminalPresentationWriter.Start`, `WindowsTerminalNavigator:25-37`, `:79-89` | `sync-title` and `focus-terminal` are started by the tray with no environment override, so they inherit the tray's `CLAUDE_CONFIG_DIR`/`CODEX_HOME` and use the same key the tick observes. The evidence note that helpers use "the profile the provider in that terminal uses" is inaccurate, in the safe direction: the tick covers every production decider | Tick coverage is complete in production |
| 4800: older shape | Source and test `AnOlderProviderKeyedRecordIsReadAsUnknownAndTheSettingIsSightedAgain` | A provider-keyed string value fails to bind to `Entry` (JsonException), so the file reads as empty and the setting is re-sighted at the file's write time. The old keys also cannot collide with the new composite keys | Matches the decision |
| 4800: limitation | `docs/installation.md:80`, `docs/compatibility.md:114`, test `ARemovalAndReAddNobodySawIsTheDocumentedLimitation` | Both docs state the tray-downtime gap; compatibility lists it as open evidence. The test asserts the surviving old time by name | Documented and tested. Spec wording note under Assessment |
| 4800: concurrency | Source | Read-modify-write of `provider-titles.json` is still unlocked across tray, helpers and doctor (doctor no longer writes). A lost update can drop or revive an entry only within a millisecond window that would also need an unobserved removal and re-add; dropping moves the effective time later (conservative) | No new risk |
| 4801: script | Source, `HookStartTaskSetup.UnregisterScript:40-58` | Lookup with `-ErrorAction Stop` in `try`; `catch` re-throws unless the category is `ObjectNotFound`. Under `$ErrorActionPreference = 'Stop'` the uncaught `throw` ends `powershell.exe -EncodedCommand` with a non-zero exit; `Run`/`SetupAction` then fail and `OnSuccess` (record delete) does not run. The folder step keeps `SilentlyContinue` and a swallowed COM `DeleteFolder`, which only removes an empty folder | Matches the expected fix |
| 4801: tests | Source, `ProviderTitleStepTests:512-583` | Real script text with `Get-ScheduledTask` shadowed by an advanced function: `ObjectNotFound` exits 0; `PermissionDenied` and a plain `throw` exit non-zero. Through `IntegrationSetup` and `SetupPlan.Apply` (`-EncodedCommand`, as in production) the failure throws and `task.txt` stays; the not-found retry clears it. The real not-found category is covered by `RemovingATaskThatIsNotThereCountsAsRemoved` (real scheduler, reported passing by DEV-061; QA owns execution) | Verified by source; not executed here |
| Duplication / complexity | Source | `Observe` grew to one method with three readings and two guarded write sites; it is still proportionate. `ObserveBoth` hard-codes the two provider names, as the rest of the file does. No copied policy | No finding |
| Architecture | Source against `docs/development.md` | `ProviderTitleState` stays in IO; Tray calls IO; Diagnostics only reads. No new Setup or Hosts dependency | Conforms |
| Claude Code and Codex parity | Source and test list | One `Observe` rule for both; only `Read` parses per provider. All new tests are provider-parameterized or mixed (`ReportNeverCreatesARecordAndAMixedPairKeepsEachProvidersOwnRecord`). The 4801 script is provider-independent | No parity gap |
| Performance | Measured, disposable probe: Release `bell-tower.dll` copied to the scratchpad, `CLAUDE_CONFIG_DIR`/`CODEX_HOME` and the data directory set to disposable folders, 500 timed `ObserveBoth()` calls | Unchanged files: p50 0.235 ms, p95 0.439 ms, max 10.8 ms. Settings file stamp changed on every call (n=50): p50 11.1 ms, p95 15.1 ms, max 19.2 ms; this path re-parses and writes the record with `WriteThrough` on the UI thread | Steady cost negligible on a local disk (measured). The changed-stamp cost occurs only when a provider rewrites its settings file. A slow or network provider home blocking the UI thread is a hypothesis, not measured |
| Malformed record file | Same disposable probe, record file set to `null` and to an empty file | `null`: `ObserveBoth`, `DisabledFor` and `Report` throw `System.IO.InvalidDataException` ("Empty JSON document."). Empty file: no exception | ANALYSIS-4900 |
| Build / tests / format | Not run | QA-056 owns them; the DEV-061 test classes include real Task Scheduler cases, which this assignment prohibits touching | Not performed |

## Findings

### ANALYSIS-4900: A malformed `provider-titles.json` now stops the tray's two-second refresh

- Severity: suggestion (regression from the DEV-061 diff; non-blocking because Bell Tower never writes the triggering content and deleting the file recovers)
- Related task / requirement: 4.7; ANALYSIS-4800 expected fix (tray-tick observation); `docs/development.md` (state files are read defensively)
- Locations:
  - `src/BellTower/IO/ProviderTitleState.cs:167-178`: `ReadRecords` catches `IOException`, `UnauthorizedAccessException`, `JsonException` and `NotSupportedException`, but not `InvalidDataException`
  - `src/BellTower/IO/AtomicFile.cs:48` (file over 16 MiB) and `src/BellTower/IO/Json.cs:19` (document `null`) throw `InvalidDataException`
  - `src/BellTower/Tray/TrayApplication.cs:291`: `ObserveBoth()` runs on the UI tick before `titles.Update`, `nativeNames.Update` and `RefreshOverview`; the tick's catch at `:298` swallows the exception
- Observation: when the record file contains `null` or exceeds 16 MiB, `ReadRecords` lets `InvalidDataException` escape. Measured in the disposable probe: `ObserveBoth`, `DisabledFor` and `Report` all throw. Before DEV-061 the title decision ran inside the title pump's background task, so only titles failed. Now the throw happens on the UI tick before the rest of the two-second block. While the file stays malformed, native name sync and the Sessions overview refresh stop as well, every two seconds, and `refreshHealth` records a failure. Doctor's `nativeSessions` report throws too.
- Proposed improvement and benefit: catch `InvalidDataException` in `ReadRecords` and treat it as empty, like the older shape. The next present sighting then rewrites a valid file, so the state heals itself. Keeping `ObserveBoth` at the start of the block is then harmless.
- Behavior-preservation risks: none for valid files. An unreadable record file moves the effective time to the file's write time, which can only withhold a title (the same conservative direction as the older shape).
- Evidence type: measured result (disposable probe on the built Release assembly) plus source observation.
- Verification / measurement plan: a provider-parameterized `ProviderTitleStateTests` case that writes `null` to `provider-titles.json`, then asserts that `ObserveBoth` and `Report` do not throw and that a later present sighting writes a valid record.
- Relationship: new; regression of the ANALYSIS-4800 correction.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-4800 | `head=2e7f318 … diff=6c6beec81cd8 untracked=21/a701204182dd` | Records are keyed by provider and resolved file and store `{Since, Stamp}`. `Report` never writes or forgets. Unknown readings are not cached and leave the record and any granted answer unchanged. The tray tick observes both files, and every production decider (`sync-title` and `focus-terminal` inherit the tray's environment) uses the keys the tick observes. The older shape reads as empty and is re-sighted. The tray-downtime gap is documented in both docs and covered by a named test. All new tests cover Claude Code, Codex and a mixed pair. A residual: an unreadable record file (as opposed to the settings file) is still treated as empty, which can withdraw a granted title in the conservative direction. For a malformed file it also blocks the refresh (ANALYSIS-4900) | verified (source). Residual regression ANALYSIS-4900 is non-blocking |
| ANALYSIS-4801 | same | Only `ObjectNotFound` counts as removed; any other lookup failure re-throws, the script exits non-zero, the action fails, `OnSuccess` does not delete the record, and the manifest commit after actions keeps ownership (ordering verified in ANALYSIS-048). Normal and not-found paths are unchanged; the folder step stays best-effort. Tests cover not-found, permission-denied and generic failure, plus the record kept through `SetupPlan.Apply` and cleared on retry | verified (source); test execution owned by QA |

## Assessment and limitations

Scope covered: the ANALYSIS-4800 and ANALYSIS-4801 corrections and the regression surface of the DEV-061 diff (`ProviderTitleState`, the tray tick, the unregister script, doctor wiring, docs and tests) for Claude Code and Codex, by source inspection and one disposable measurement probe.

- ANALYSIS-4800 and ANALYSIS-4801 are verified by source against the orchestrator's expected fixes.
- One new regression, ANALYSIS-4900 (suggestion, non-blocking): a malformed record file throws on the UI tick and stops the two-second refresh.
- No duplication, complexity, architecture or parity regression was found in the DEV-061 diff.
- Performance (measured on a local disk): the tick adds about 0.24 ms p50. A settings-file change costs about 11 ms on the UI thread for one tick. Slow provider homes are a hypothesis only.

For the orchestrator:

- The session-identity scenario "Setting reset or applied while a session runs" states the rule without the tray-downtime exception that the docs now record as a limitation. Decide whether the delta spec should name that exception, so the spec and the documented behavior agree.
- The DEV-061 note about which home the helpers resolve is inaccurate: they inherit the tray's environment. This is harmless and affects only the evidence wording.
- The 4801 tests that run the not-found case call the real COM `DeleteFolder` for the test folder `\BellTowerTest-DEV061` (swallowed if absent). QA may want to note this as part of its Task Scheduler hygiene check.

Not performed:

- build, tests and format verification (owned by QA-056; the test classes touch the real Task Scheduler, which this assignment prohibits)
- live tray, title or click probes
- an elevated or NativeAOT check

The probe used only scratchpad folders outside the repository; its copied assemblies stay locked by the probe's shell host until that host exits. The write of this report to the reserved path was refused, as quoted at the top, and no repository file was written.

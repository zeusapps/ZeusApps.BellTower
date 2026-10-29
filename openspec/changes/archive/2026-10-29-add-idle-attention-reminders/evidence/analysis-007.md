# Analysis 007 evidence: final target-006 recheck

Date: 2026-10-29. Baseline/head `47dfca9fd46b309017d4d785a1f91fd249d42041`; frozen [target-006](review-target-006.txt).

## Execution and target

Read the ledger, [implementation-006](implementation-006.md), prior analysis/QA evidence and current source. Source inspection occurred while QA ran alone. The analyzer executed scratch only after the orchestrator confirmed the clean QA full run had finished. No analyzer full suite, product build, formatter or redundant documentation lookup was run.

Before and after, the complete identity output exactly matched target-006:

`head=47dfca9 baseline=47dfca9 files=31 diff=830d1d13ae60 untracked=18/53ddcdc3b756`.

Tracked SHA-256: `830d1d13ae60eebfe3fc8e8a018fd954502f784dc61225f48d9242217d6d2f51`.

## Bounded actual-helper/manual-clock probes

Command: `dotnet run --project artifacts/idle-analysis-007/Probe.csproj -c Release`, outside the sandbox. The ignored project links the actual current `ReminderFixtureDriver.cs`, references existing application/protocol Release assemblies, and does not build product projects. Source/tray/queue checks use injected observations, uninitialized tray fixtures and silent sinks. No native/RDP query, UI constructor, physical input/audio, microphone, real provider, production data or network send is used.

Observed output:

```text
PASS work and cleanup failures both propagate
PASS all consumer errors propagate
PASS controlled process-local worker limit established
PASS actual helper reaches public80ms and eight-consumer assertions
PASS owned release precedes probe exit and completion without self-timeout
PASS shared-clock healthy Claude and Codex reminders eligible
PASS 501ms manual age remains suppressed
PASS future manual observation remains suppressed
PASS recent input remains suppressed
PASS healthy mixed-provider queue barrier ready
PASS shared-clock mixed-provider queue preserves healthy tone and speech
PASS mixed ordinary/reminder barrier ready
PASS current input cancels mixed speech and preserves ordinary tone
PASS ordinary alert bypasses injected reminder clock
```

### ANALYSIS-101

Repeated the actual-helper controlled scenario from analysis-006: in one isolated scratch process, temporarily limit its own pool to one worker, seed the actual source, start/hold its probe, and run public production-default fresh-read rejection plus eight dedicated concurrent consumers. The dedicated driver now explicitly releases held work before completing. The poll does not need its three-second self-timeout, the release callback runs before probe exit, work and release use the same owned driver thread, and that driver is joined before the caller returns. Original scratch-process pool settings are restored in `finally`; no runner/application global tuning is proposed or applied.

The actual helper also preserves both a recognizable work exception and a recognizable cleanup exception, and reports all eight dedicated-consumer exceptions. Success is not substituted for failed assertions/cleanup.

### ANALYSIS-004

The actual source and actual tray reminder predicate share one thread-safe manual clock. Healthy Claude and Codex waits remain eligible. An observation that advances the clock by 501 ms is rejected; a future observation is rejected; recent input is rejected. These checks exercise the existing source/tray 500 ms guards with controlled time rather than widening them.

The actual queue receives a synchronized aggregate of one Claude and two Codex reminders. Tone and speech remain permitted, with phrase `3 sessions still waiting`. In a separate ordinary/reminder mixed batch, input changes during the tone: stale aggregate speech is canceled and the valid ordinary tone remains.

Set the reminder clock delegate to throw, then validate an ordinary alert. The ordinary branch succeeds without consulting it. Source inspection confirms the property is internal, defaults to `static () => DateTimeOffset.UtcNow`, and is read only for reminder validity. `WindowsUserIdleSource` remains unchanged: nominal production refresh budget 80 ms, monotonic remaining-budget wait, 500 ms age guard, no pre-request/cached-positive fallback. The healthy fixtures' five-second allowance remains test-only.

## Current QA result and history

[QA-006](../reports/qa-006.md) independently records one normal default Release run: **1,089 passed, zero failed, zero skipped**, in three minutes. Its current target, formatting verification, full reminder/driver/native-boundary fixture coverage and hygiene scan are recorded there. The analyzer did not rerun that suite.

This clean current result satisfies the requested regression evidence; it does not establish causes for earlier full-suite failures. Those failed runs and isolated passes remain preserved in earlier reports. The current corrections address demonstrated fixture ownership/time assumptions without claiming to explain every historical failure.

## Exact carry-forward mapping

From target-005 to target-006, only `TrayApplication.cs` plus these test paths changed:

- `ReminderFixtureDriver.cs`: `461bd8190cfdb5dcd37c984593c6173717d8cd7787dcfa8d4e5a0f8b418691dc`.
- New `ReminderFixtureDriverTests.cs`: `ed5274be1b98e15599d90b9f30d8dd2cab975f29a31f8baccbcbc1d5012d1c89`.
- `ReminderPlaybackTests.cs`: `5a32c2942279f8de665200e3fcf77edb1351b14a45b7421fff07853c4b7a14a1`.

`WindowsUserIdleSourceTests.cs` is unchanged (`017dd5bc2fb2adf62ffd1083188b6014e6f72cf0595b9bec3317a0d1e7edb54b`). The sole product change is the internal UTC-default reminder clock seam, with unchanged ordinary-alert UTC behavior independently checked above.

Previously verified coverage remains applicable:

- ANALYSIS-001 / QA-007 / QA-008: StateStore, receipt/restart/pruning logic unchanged; `ReminderPolicy.cs` hash remains `fc9a85bcc27b16b14ace8e4817c108b1e48ae682b822b585dd041fa32bd14698`.
- ANALYSIS-002: escalation/config/store/schema paths unchanged; current-projection/config/confirmation/Mute probes retain their verified scope.
- ANALYSIS-003 / QA-006: native mapping and source unchanged (`WindowsUserIdleSource.cs` `d5dd2cf728eec69acb309769331e0000ab533682d9710cd5222245af720c5beb`; `UserIdleSnapshot.cs` `83ede6e05ce9f118fb9653437f07f3aff2b06ff9c52afaaa624ac8a6d344183d`). Affected current/stale/future time behavior was independently rerun with the shared clock.
- ANALYSIS-005: ConfigStore and settings mapping unchanged; form hash remains `838af4c6017047a9d903c686ba6e459caf7b5528f55d62dd9599aad5971c6c03`.
- Matched Claude/Codex and mixed-session policy/audio defaults remain unchanged; current probes and the clean full suite additionally cover the affected real-wrapper/queue path.

Current documentation and API/provider restrictions are unchanged. No hard provider restriction is claimed. Live native desktop/RDP, installed tray, authenticated provider and physical audio/input acceptance remain outside these synthetic checks, as previously documented.

Only reserved report/evidence and ignored scratch files were written by the analyzer. No implementation/test/config/task/ledger/historical-report change, commit, installation or publication occurred. Own outputs were inspected for sensitive content; comprehensive build/format/hygiene/full-regression evidence belongs to the current QA/developer reports.

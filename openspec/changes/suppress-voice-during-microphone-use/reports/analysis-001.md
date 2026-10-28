# Analysis report: suppress-voice-during-microphone-use / 001

## Review target

- Reviewer / role: microphone_analysis / code-analyzer
- Client / version / mode / model: Codex CLI 0.164.0 / delegated default-mode review / gpt-6.1-sol, high reasoning (assigned role default)
- Date: 2026-10-28T15:46:00Z
- Change root / assignment: `openspec/changes/suppress-voice-during-microphone-use` / ANALYSIS-REVIEW-001
- Task and requirement references: all 11 tasks in [tasks](../tasks.md); [design](../design.md); both [microphone](../specs/microphone-aware-audio/spec.md) and [attention](../specs/attention-notifications/spec.md) requirements.
- Scope: `src`, `tests`, `README.md`, `docs/compatibility.md`; neighboring responsibilities checked against `docs/development.md`.
- Baseline and HEAD: `7ddef11527676676b3808fb23adf28a68c9930b4`.
- Prior reports / rechecked findings: none.
- Before and after command: `./scripts/review-identity.ps1 -Baseline 7ddef11 -Paths src tests README.md docs/compatibility.md`.
- Before/after comparison: identical; no implementation mutation observed.

```text
Summary: head=7ddef11 baseline=7ddef11 files=15 diff=1a90ff0cc8f5 untracked=4/8eab2ff9c917
Tracked diff SHA-256: 1a90ff0cc8f5d1e704fc1231e0d962c75df9f1ee5188a6e91ddd5a0b0e25cc26
Untracked SHA-256:
b97658132d08e0a9b598435706d4d49b387e79106826f29cea0f1cd7ca43e892 src/BellTower/Core/MicrophoneActivity.cs
930d067bb712bfc8957f65551a662866e5208e48c3f14489fec1cf80e93dc6b6 src/BellTower/Tray/IMicrophoneActivitySource.cs
2041a6cb75864bc8703b6d78430659eaa38d601639f4fd92ee369e9b7a44c4e4 src/BellTower/Tray/WindowsMicrophoneActivitySource.cs
6a6ac9a3b0606c2af347d50519f5cfada4f00c35e86d0b7673a5b4e00e94defe tests/BellTower.Tests/Diagnostics/MicrophoneDiagnosticsTests.cs
Manifest:
? src/BellTower/Core/MicrophoneActivity.cs
? src/BellTower/Tray/IMicrophoneActivitySource.cs
? src/BellTower/Tray/WindowsMicrophoneActivitySource.cs
? tests/BellTower.Tests/Diagnostics/MicrophoneDiagnosticsTests.cs
M README.md
M docs/compatibility.md
M src/BellTower/Core/AppConfig.cs
M src/BellTower/Diagnostics/DiagnosticsReporter.cs
M src/BellTower/Tray/AudioQueue.cs
M src/BellTower/Tray/IAudioSink.cs
M src/BellTower/Tray/SilentAudioSink.cs
M src/BellTower/Tray/TrayApplication.cs
M src/BellTower/Tray/WindowsAudioSink.cs
M tests/BellTower.Tests/IO/StorageTests.cs
M tests/BellTower.Tests/Tray/AudioQueueTests.cs
```

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Requirements and architecture | Read proposal, design, both specs, tasks, changed sources/tests/docs and development boundaries | Shared queue policy, persisted setting, tray control and common diagnostics serve Claude Code and Codex | Passed for shared placement; native correctness blockers below affect both clients |
| Native contracts | Required Context7 library lookup for Microsoft Windows, then docs lookup against `/microsoft/windows-classic-samples` | Resolution succeeded; docs query returned no matching material. Consulted current primary Microsoft Learn and WinSDK headers below | Failed: three ABI/enum defects confirmed; no fallback to remembered constants |
| Compiled ABI metadata | Reflection over the already-built `src/BellTower/bin/Release/net10.0-windows/bell-tower.dll`; inspect nested enum, IID and methods sorted by MetadataToken | Actual enum `Active=0; Inactive=1; Expired=2`; actual collection IID ends `C0A5F6A9A4A0`; actual manager order has register/unregister before enumerator | Failed, ANALYSIS-001 through ANALYSIS-003; no native audio calls made |
| Freshness | Reflection fixture sets an Inactive snapshot observed 1.2 seconds earlier and a fresh cache retrieval time, then calls GetSnapshot | Returned `Inactive`, observed age 1212 ms, despite the one-second contract | Failed, ANALYSIS-004. This tests the real cached-return branch, not a microphone device |
| Callback bounds | Reflection fixture instantiates `CallbackSessionBuffer<int>`, calls Add 1024 times, reads retained queue Count, then disposes | Retained 1024 items; no admission cap | Failed, ANALYSIS-006. Initial harness attempt had PowerShell boxing errors and was corrected with explicitly boxed Int32 values; only the successful run supports this result |
| Cancellation | Inspect queue discard logic, independent tone/speech callbacks, and WindowsAudioSink pre-speech setup | Queue discards suppressed phrases and retains tone guard; native sink has an unguarded gap immediately before Speak | Failed, ANALYSIS-005. No physical speech or timing measurement performed |
| Resource ownership | Inspect MTA worker, callback synchronization, unregister/finally cleanup and single in-flight task | Caller wait is capped and one task is retained per source; callback memory is not capped | Partial: no native lifetime execution performed because the ABI is unsafe |
| Additional COM release hypothesis | Disposable in-memory Scripting.Dictionary alias-release probe, without audio/device access | First ReleaseComObject returned 0; IsComObject remained true; second returned -1 in this environment | Inconclusive for a product defect; no finding based on this probe |
| Provider parity and reminder composition | Inspect matched active/unknown Claude/Codex fixtures, mixed aggregate fixture and shared AudioItem boundary | Neither provider has a separate audio policy; later reminder-like items enter the same queue | Passed source review; broader fixture coverage and full regressions belong to independent QA |
| Formatting / regression execution | `git diff --check` | No whitespace errors | Passed. Full suite/build/format execution not repeated to avoid competing with QA's assigned run |
| Safety and native acceptance | No microphone stream, physical sink, user configuration, installation or active-session operations invoked | Analysis used reflection and an unrelated harmless COM object only | Native microphone/RDP/installed acceptance not performed; not claimed passed |

## Findings

### ANALYSIS-001: Native collection interface ID does not match Windows

- Severity: critical.
- Related task / requirement: 1.2, 1.3, 4.1; current-session microphone state.
- Location: `src/BellTower/Tray/WindowsMicrophoneActivitySource.cs:289`, reviewed identity above.
- Observation: the declared IMMDeviceCollection IID is `0BD7A1BE-7A1A-44DB-8397-C0A5F6A9A4A0`. Microsoft's [WinSDK mmdeviceapi.h](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/mmdeviceapi.h) declares `0BD7A1BE-7A1A-44DB-8397-CC5392387B5E`. Compiled metadata confirms the incorrect value. Binding the returned collection through an unsupported IID cannot establish the intended native contract; the fail-safe path can leave production permanently Unknown.
- Proposed improvement and benefit: correct the IID and audit every handwritten native identifier against the same primary contract.
- Behavior-preservation risks: correcting only this IID exposes the unsafe manager slots in ANALYSIS-002; fix all ABI findings together before native execution.
- Evidence type: primary-contract comparison and executed compiled-metadata inspection; native failure was not executed.
- Verification plan: executable SDK-contract assertion for this IID, plus an isolated read-only metadata smoke that enumerates capture endpoints after all ABI corrections. Open no capture stream and play no sound. Report unavailable endpoints honestly.
- Relationship: none; distinct from ANALYSIS-002/003.

### ANALYSIS-002: Session-manager methods occupy incorrect COM vtable slots

- Severity: critical.
- Related task / requirement: 1.2, 1.3, 4.1; bounded complete native observation.
- Location: `src/BellTower/Tray/WindowsMicrophoneActivitySource.cs:311`, especially declarations at 315-317 and calls at 156-158.
- Observation: after the two inherited methods, Windows declares GetSessionEnumerator, RegisterSessionNotification, UnregisterSessionNotification, then the ducking methods. The implementation puts GetSessionEnumerator after registration/unregistration. The [WinSDK audiopolicy.h vtable](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/audiopolicy.h) establishes the ABI order; reflection confirms the built assembly differs. RegisterSessionNotification would dispatch through the enumerator slot with incompatible parameter meaning. This is an unmanaged call-safety defect, not merely a missing observation.
- Proposed improvement and benefit: restore exact native method ordering/signatures and audit all declarations; this makes calls target the intended native operations.
- Behavior-preservation risks: do not execute the existing default probe to reproduce this mismatch. Native signatures can corrupt memory when dispatched incorrectly.
- Evidence type: primary-contract comparison and executed compiled metadata; unsafe native dispatch intentionally not attempted.
- Verification plan: executable contract assertions for inherited and derived slot order/signatures against the official header. After all ABI corrections, run isolated read-only endpoint/session enumeration and notification registration/unregistration smoke. Positive/negative raw session-state fixtures must cross the interop conversion boundary.
- Relationship: none; fix together with ANALYSIS-001/003.

### ANALYSIS-003: Native active and inactive values are reversed

- Severity: critical.
- Related task / requirement: 1.2, 1.3, 2.2, 4.1; active-stream speech suppression.
- Location: `src/BellTower/Tray/WindowsMicrophoneActivitySource.cs:274`, used by IsActive at 250-253.
- Observation: the local enum gives Active=0 and Inactive=1. The documented [AudioSessionState enum](https://learn.microsoft.com/en-us/windows/win32/api/audiosessiontypes/ne-audiosessiontypes-audiosessionstate) defines Inactive first (0), then Active (1), then Expired (2). Once native binding works, an actual running stream is treated as inactive and an idle stream as active. Existing fixtures use the product enum after conversion, so they do not detect this reversal.
- Proposed improvement and benefit: use explicit SDK numeric constants and test conversion of raw native values; running capture must suppress speech for either provider.
- Behavior-preservation risks: ensure expired and invalid raw values do not accidentally imply active or fresh idle; retain Unknown on invalid/incomplete results.
- Evidence type: primary documentation and compiled enum inspection; actual microphone use not exercised.
- Verification plan: raw integer 0/1/2 contract checks plus fake native control states flowing through the production IsActive/conversion path and speech policy. Follow with the safe metadata smoke described above.
- Relationship: none; independent of binding failures masking this defect.

### ANALYSIS-004: Cached snapshot can report stale Inactive as current state

- Severity: warning.
- Related task / requirement: 1.3, 4.2; stale evidence yields Unknown and honest diagnostics.
- Locations: `src/BellTower/Tray/WindowsMicrophoneActivitySource.cs:43`, cache timestamps at 59/90; `src/BellTower/Diagnostics/DiagnosticsReporter.cs:57`.
- Observation: Evaluate validates scan-start age, but caching lasts one second from result collection instead of observation. GetSnapshot's cached-return branch never rechecks ObservedAt. A scan that took part of the freshness interval can therefore remain cached past that interval. A reflection fixture returned Inactive at 1212 ms observation age while its collection timestamp was fresh. AudioQueue independently rejects it, so speech remains fail-safe, but doctor reports stale Inactive instead of Unknown and the source contract is violated.
- Proposed improvement and benefit: expire cached results by observation age, or normalize an over-age snapshot to Unknown before returning; retain explicit reasons and recovery.
- Behavior-preservation risks: preserve the single in-flight task and do not timestamp an old observation as new. Account for future timestamps/clock changes conservatively.
- Evidence type: executed fixture on the built source's cached branch plus source reachability analysis; no microphone/device access.
- Verification plan: fake scan that consumes a substantial portion of the one-second interval, then read the cache after observation expiry but before collection-cache expiry; source and doctor must yield Unknown, followed by fresh recovery.
- Relationship: none.

### ANALYSIS-005: Speech guard is not rechecked immediately before native Speak

- Severity: warning.
- Related task / requirement: 2.3; call-start race and remaining-speech cancellation.
- Location: `src/BellTower/Tray/WindowsAudioSink.cs:36`, COM setup at 44-65 and Speak at 67.
- Observation: the final guard runs before creating the speech COM object and enumerating/selecting voices. Those native operations occur before Speak with no further guard. If microphone activity, Mute or stop changes during initialization, Speak still starts; cancellation is checked only after WaitUntilDone. A short phrase can finish before that loop executes. The code therefore lacks the planned immediate-before-speech boundary.
- Proposed improvement and benefit: check both cancellation callbacks directly before Speak, after all voice initialization; retain the polling cancellation for speech already in progress.
- Behavior-preservation risks: preserve tones and dispose the voice even when this final check cancels. Suppressed phrases must not be requeued.
- Evidence type: source observation; duration of native initialization and leaked speech were not measured or physically exercised.
- Verification plan: silent injectable speech-init/voice boundary fixture that changes the microphone/mute state during initialization and records zero Speak calls. Also verify cancellation after speech begins, without changing tone behavior.
- Relationship: none.

### ANALYSIS-006: Callback-session storage bypasses the stated session bound

- Severity: warning.
- Related task / requirement: 1.3; count/time bounds and safe incomplete observations.
- Locations: `src/BellTower/Tray/WindowsMicrophoneActivitySource.cs:374`, Add at 377-388, DrainAndMap at 391-418; count check after ReadStates at 182-186.
- Observation: every callback control is retained in an unbounded Queue. The 256-session check runs only after draining/mapping and allocating an array for all callbacks. A busy or stalled scan can retain arbitrarily many COM controls despite the declared session limit. An executed generic-buffer fixture retained 1024 items. This demonstrates absent admission control, not a measurement of real workload memory or latency.
- Proposed improvement and benefit: impose an admission cap at enqueue, release overflow controls immediately, and mark the observation incomplete/Unknown. Keep cleanup safe for callbacks racing disposal and for release failures.
- Behavior-preservation risks: dropping callbacks silently would create false Inactive results; overflow must remain visible to the reducer and diagnostic reason. Do not leak controls during overflow/error cleanup.
- Evidence type: executed bounded generic-buffer fixture and source observation. Production performance impact is a hypothesis, not a benchmark result.
- Verification plan: enqueue more than the cap with fake disposable controls, verify bounded retained count, exactly-once release of accepted/overflow controls, Unknown on overflow, and safe concurrent dispose. Re-run existing partial/exception/time-bound fixtures.
- Relationship: none.

## Re-verification

Not applicable: first review; no corrections have been reviewed.

## Assessment and limitations

Three critical native-contract defects and three warnings affect agreed acceptance. The shared integration, persisted controls and separate tone/speech callbacks are appropriately centralized for both providers. The accepted best-effort exclusive-mode limitation is documented; it does not justify the ABI, freshness, call-start or bound defects above.

Existing fake-result tests do not establish native observation correctness. Require executable ABI/raw-state checks and safe isolated native metadata enumeration after correction, as well as independent QA regression evidence. Native microphone streams, physical tones/speech, RDP trials and installed acceptance were not performed and are not represented as passing. Full build/test execution remains with QA; this review did not mutate implementation or personal/runtime state.

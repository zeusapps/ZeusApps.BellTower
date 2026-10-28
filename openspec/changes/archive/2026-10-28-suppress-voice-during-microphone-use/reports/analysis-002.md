# Analysis report: suppress-voice-during-microphone-use / 002

## Review target

- Reviewer / role: microphone_analysis / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / delegated default-mode recheck / gpt-6.1-sol, high reasoning (role default).
- Date: 2026-10-28.
- Change root / assignment: `openspec/changes/suppress-voice-during-microphone-use` / ANALYSIS-RECHECK-002.
- Task and requirement references: [tasks](../tasks.md) 1.2, 1.3, 2.2, 2.3, 4.1, 4.2; [design](../design.md); shared microphone and attention requirements.
- Scope: recheck only ANALYSIS-001 through ANALYSIS-006 from [analysis-001.md](analysis-001.md), plus affected regression. No new suggestions sought.
- Implementation scope: `src`, `tests`, `README.md`, `docs/compatibility.md`; same 15-file manifest as report 001.
- Baseline / HEAD: `7ddef11527676676b3808fb23adf28a68c9930b4`.
- Identity command before and after: `./scripts/review-identity.ps1 -Baseline 7ddef11 -Paths src tests README.md docs/compatibility.md`.
- Before/after result: identical; no implementation mutation observed.

```text
Summary: head=7ddef11 baseline=7ddef11 files=15 diff=ec32fe6ba6f3 untracked=4/19783d7998c7
Tracked diff SHA-256: ec32fe6ba6f3fbd5b433b6b8472ae159f57fe0926faf94829a3cd0d10a3ee2cd
Untracked SHA-256:
b97658132d08e0a9b598435706d4d49b387e79106826f29cea0f1cd7ca43e892 src/BellTower/Core/MicrophoneActivity.cs
930d067bb712bfc8957f65551a662866e5208e48c3f14489fec1cf80e93dc6b6 src/BellTower/Tray/IMicrophoneActivitySource.cs
872ce2daf1cbb7bc0d421d0154f0628368d202c28596c0bfcd2968983d39fa73 src/BellTower/Tray/WindowsMicrophoneActivitySource.cs
6a6ac9a3b0606c2af347d50519f5cfada4f00c35e86d0b7673a5b4e00e94defe tests/BellTower.Tests/Diagnostics/MicrophoneDiagnosticsTests.cs
```

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Native ABI corrections | Compare corrected declarations and SDK-contract tests against primary contracts already retrieved for report 001; independently inspect compiled metadata by reflection | Collection IID now ends `CC5392387B5E`; manager slots are base methods, enumerator, register, unregister, ducking methods | Passed. No new API assumptions introduced during recheck |
| Raw state conversion | Invoke production `MapNativeSessionState` by reflection with raw 0, 1, 2, 99 | Inactive, Active, Unknown, Unknown respectively; `ReadSessionState` and callback mapping both use this conversion | Passed. No microphone stream needed |
| Cache expiry | Reproduce the prior cached-return fixture: old Inactive observation (1.2 seconds), fresh collection timestamp, injected incomplete task to prevent native probing | GetSnapshot now returns Unknown with bounded-wait reason instead of stale Inactive | Passed. Expanded source test also exercises original-observation expiry and fresh recovery |
| Speech start boundary | Invoke production `WindowsAudioSink.TryBeginSpeech` with recording callbacks; initialization changes microphone state, then a separate run changes general cancellation | Both return false; zero Speak calls | Passed. Physical tone/speech methods never invoked |
| Callback admission and release | Instantiate built `CallbackSessionBuffer<int>` using reflection; enqueue 1024 fake controls, inspect count/overflow and release counts, then dispose | Retained 256; overflow true; 768 released immediately; all 1024 released exactly once after disposal | Passed. No COM controls or hardware involved in this fixture |
| Callback failure and race handling | Inspect expanded tests and production bounded buffer, disposed admission, DrainAndMap and Dispose | Tests cover overflow, late callbacks, concurrent disposal and release failure; native scan checks Overflowed and returns incomplete observation | Passed source review. Independent QA owns executing the expanded suite |
| Native metadata smoke, sandboxed | Instantiate default WindowsMicrophoneActivitySource and call GetSnapshot, retry once after 200 ms if initially Unknown | Unknown: active capture endpoint/session verification unavailable in this sandbox execution | Not proof of a code defect or platform restriction; recorded separately from outside-sandbox result |
| Native metadata smoke, outside sandbox | Same bounded source call, explicitly escalated; inspect only state, timestamp presence and sanitized reason | Inactive; observed timestamp present; no active shared-mode capture stream observed | Passed native read-only execution. This exercises actual endpoint enumeration, session-manager activation, notification register/unregister, state observation and cleanup without opening capture streams |
| Affected shared policy | Inspect separate speech/tone guards, queue discard, new mid-speech and matrix/reminder-like fixtures | Corrections retain shared Claude/Codex playback and no replay behavior | Passed source review; no independent full-suite claim here |
| Diff cleanliness | `git diff --check` | No whitespace errors | Passed |
| Full build / broad tests / formatting | Not repeated | QA owns the single broad regression execution on this stable target; developer reports are separate evidence | Not performed by analyzer to avoid competing runners/output mutation |
| Live microphone/RDP/installation | No physical audio, stream creation, user configuration or installed-app operations | None invoked | Not performed; not required as a substitute for fixture/native-metadata evidence and not claimed passed |

## Findings

No new findings. All six assigned findings are independently verified corrected on this target; the orchestrator owns ledger closure.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | `ec32fe6ba6f3` / `19783d7998c7` | Compiled IMMDeviceCollection GUID matches the official header; actual native enumeration succeeds outside sandbox | Verified |
| ANALYSIS-002 | Same target | Compiled manager methods sorted by metadata token match SDK slot order; source signatures for called operations checked; actual native session enumeration and registration/unregistration succeed | Verified |
| ANALYSIS-003 | Same target | Compiled enum Inactive=0, Active=1, Expired=2; production raw conversion returns expected states for 0/1/2/99; unknown native state propagates to Unknown in reducer | Verified |
| ANALYSIS-004 | Same target | Cache branch now checks original snapshot freshness and rejects future retrieval times; reproduced old cached fixture returns Unknown; expanded expiry/recovery regression inspected | Verified |
| ANALYSIS-005 | Same target | Production Play calls TryBeginSpeech after creating voice; helper initializes voice selection, then immediately checks both guards before Speak; silent initialization-transition fixtures produce zero Speak calls | Verified |
| ANALYSIS-006 | Same target | Callback capacity enforced at admission, overflow released immediately and flagged; 1024-control fixture retains 256 and releases all exactly once; source routes overflow to incomplete/Unknown | Verified |

## Assessment and limitations

The assigned critical native-contract defects and acceptance warnings are corrected. The recheck used current built implementation metadata and executable silent fixtures rather than accepting the developer's assertions. The safe outside-sandbox metadata smoke also demonstrates that the corrected native observer can produce a fresh supported observation in this environment; it does not establish coverage of actual microphone use, exclusive-mode capture or RDP.

The accepted exclusive-mode blind spot remains documented, and RDP/errors remain Unknown. No new provider exception is claimed. Full-suite acceptance and final task reconciliation remain the orchestrator's decision using independent QA evidence. No source, task, ledger or earlier report edits were made by this reviewer.

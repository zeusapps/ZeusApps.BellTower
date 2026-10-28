# Analysis report: suppress-voice-during-microphone-use / 004

## Review target

- Reviewer / role: microphone_analysis / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 / delegated default-mode recheck / gpt-6.1-sol, high reasoning (role default).
- Date: 2026-10-28.
- Change root / assignment: `openspec/changes/suppress-voice-during-microphone-use` / ANALYSIS-RECHECK-004.
- Authorization and scope: the user expressly authorized one additional clock/test correction round. Recheck QA-021 and affected clock semantics only; no new suggestions or broader scope.
- Changed paths since report 003: `src/BellTower/Tray/WindowsMicrophoneActivitySource.cs` and `tests/BellTower.Tests/Tray/AudioQueueTests.cs`. Other product sources remain unchanged in the scoped handoff.
- Task / requirement references: QA-021; task 1.3 freshness verification and task 3.2 required regressions; [design](../design.md) one-second observation/cache contract and Unknown fallback.
- Prior reports: [analysis-003.md](analysis-003.md), [analysis-002.md](analysis-002.md), [qa-002.md](qa-002.md).
- Baseline / HEAD: `7ddef11527676676b3808fb23adf28a68c9930b4`.
- Before/after command: `./scripts/review-identity.ps1 -Baseline 7ddef11 -Paths src tests README.md docs/compatibility.md`.
- Before/after identity: identical; no implementation mutation observed.

```text
Summary: head=7ddef11 baseline=7ddef11 files=15 diff=38fed194f59c untracked=4/a12d62f18a64
Tracked diff SHA-256: 38fed194f59c90359a95fe70855ccc96923b1a11da3ad9a881508472196ef645
Native source SHA-256: 5bd052687cd097c2a78277ee0ef326e1d7fbc9653c910809db8fe251ad17b982
AudioQueueTests.cs SHA-256: 480A40392D4A8A556F5896E0A06A7D8EA48E4833BB7259928CE6602D16EDB8C6
Other untracked SHA-256:
b97658132d08e0a9b598435706d4d49b387e79106826f29cea0f1cd7ca43e892 src/BellTower/Core/MicrophoneActivity.cs
930d067bb712bfc8957f65551a662866e5208e48c3f14489fec1cf80e93dc6b6 src/BellTower/Tray/IMicrophoneActivitySource.cs
6a6ac9a3b0606c2af347d50519f5cfada4f00c35e86d0b7673a5b4e00e94defe tests/BellTower.Tests/Diagnostics/MicrophoneDiagnosticsTests.cs
```

The implementation manifest remains the same 15 paths recorded in analysis-001; only the two paths above differ from the prior approved product/test target. Native source changed narrowly for clock injection, so previous native evidence is mapped below rather than treating the entire file as byte-identical.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Target identity | Before/after review-identity command and SHA-256 of the two changed files | Matches assigned identity and hashes | Passed |
| Runtime clock defaults | Inspect constructors and compatibility Evaluate entry point | Public/default and existing one-argument internal constructors use DateTimeOffset.UtcNow; static one-argument Evaluate retains UTC behavior | Passed; clock injection is internal and opt-in |
| Clock coverage | Inspect every timestamp/cache/freshness reference, using `rg -n 'DateTime|clock|Clock|now\(|ObservedAt|snapshotTime|UtcNow'` on the source | GetSnapshot read time, result evaluation, all collection timestamps and native Query observation start use injected utcNow. Explicit-now Evaluate applies the existing freshness logic | Passed; no real-time reads leak into an injected source's observation/cache path |
| Runtime semantic preservation | Compare changed source to prior reviewed flow | One-second age/cache limit, 150 ms real bounded wait, single in-flight task, future/stale rejection, endpoint/session scan and mapping preserved. Nullable snapshotTime replaces the uninitialized sentinel without changing normal UTC operation | Passed source review; no product-policy change identified |
| Fake-clock thread safety | Inspect ManualUtcClock.Now/Advance and probe event sequencing | Both clock operations lock the same gate; observation time is captured before start signal; test advances only after observing the corresponding event | Passed |
| Four affected compiled tests | Direct reflection invocation of the already-built AudioQueueTests methods listed below, once each | All four returned normally with their assertions active | Passed independently; no new test runner or build started |
| Prior delayed-consumer case | Existing-assembly reflection fixture using the compiled ManualUtcClock, real production source with injected clock, and controlled task completion; initial bounded wait plus 100 ms real consumer delay | Fixed fake age 800 ms remained Inactive; advancing fake time by 250 ms gave Unknown while replacement was held; a fresh completed replacement recovered Inactive | Passed; this exercises the condition that remained scheduling-sensitive in report 003 without hardware or production state |
| Original stale-cache regression discrimination | Inspect test's timestamps and required states | First result observed at fake t=0, collected at t=800; at t=1050 it must be Unknown despite collection age only 250 ms. The old collection-time-only cache would return Inactive and fail this assertion | Passed source reasoning. No mutated implementation was compiled/executed |
| Delayed completion regression | Execute delayed-scan test and inspect fake timeline | A result observed at t=0 and completed at t=1100 is rejected as stale; advancing beyond the Unknown cache starts a fresh replacement and recovers | Passed |
| Diff cleanliness | `git diff --check` | No whitespace errors | Passed |
| Build, full default suite, formatting | Not repeated | QA owns the full suite on this identity; developer build/format claims remain separate evidence | Not performed by analyzer to avoid competing output/runners |
| Native microphone/RDP/installed acceptance | No repeat native probe, stream, physical audio or personal configuration operations | None invoked in this round | Not performed; prior native metadata evidence carried forward only for unchanged native scan behavior |

The independently invoked compiled test methods were:

- `CachedInactiveExpiresFromOriginalObservationTimeAndFreshScanRecovers`
- `DelayedInactiveScanIsRejectedAsStaleThenFreshScanRecovers`
- `BlockingNativeProbeReturnsUnknownAndKeepsOnlyOneInFlightQuery`
- `FutureNativeObservationIsUnknown`

The controlled reflection fixture used private implementation/task state only inside a fresh disposable in-memory instance. It did not modify source, build outputs, data files, the installed app or any device. Its observed sequence was:

```text
controlledConsumerDelay100ms: fakeAgeMs=800; state=Inactive
controlledFakeAdvance: fakeAgeMs=1050; pendingReplacementState=Unknown
controlledReplacementRecovery=Inactive
```

## Findings

No new findings. QA-021 is verified corrected on this target.

The fresh-positive phase now remains at fake t=800 until the assertion succeeds; real scheduling delays cannot consume its remaining observation lifetime. Expiry is introduced explicitly only afterward. Probe-start events make call-count assertions wait for actual delegate execution. Real bounded waits still serve as harness availability limits, but they no longer determine the observation-age assertions.

## Re-verification

| Finding ID | Corrected target / mapping | Independent evidence | Outcome |
| --- | --- | --- | --- |
| QA-021 | `38fed194f59c` / `a12d62f18a64` | Four affected tests pass by direct compiled invocation; prior delayed-consumer case passes with controlled fake time; first/second probe start synchronization retained | Verified |
| ANALYSIS-004, affected freshness regression | Native source changed only for internal clock seam and explicit-now Evaluate | Every observation/collection/evaluation time uses the same injected clock; t=800 fresh and t=1050 stale-cache assertions pass; delayed stale completion and future timestamps covered | Product correction remains verified after affected recheck |
| ANALYSIS-001 through ANALYSIS-003 | Native scan/ABI declarations and raw conversion unchanged from analysis-002; only source clock seam differs | Carry forward analysis-002 compiled ABI/raw-state assertions and successful outside-sandbox metadata enumeration; Query still timestamps scan start, now using its default UTC delegate | Prior corrections remain verified; no new native smoke claimed |
| ANALYSIS-005 | WindowsAudioSink and shared playback integration unchanged through this narrow correction | Carry forward analysis-002 immediate pre-Speak guards and silent initialization-transition zero-Speak evidence | Prior correction remains verified |
| ANALYSIS-006 | Callback admission, overflow handling, mapping and cleanup unchanged through this narrow correction | Carry forward analysis-002 1024-control cap/release fixture and source overflow-to-Unknown evidence | Prior correction remains verified |

## Assessment and limitations

The additional authorized correction removes the concrete timing race recorded in analysis-003 while preserving production UTC defaults, freshness limits and failure behavior. The changed tests still discriminate the original observation-versus-collection cache defect rather than merely testing a relaxed deadline. No further issue was identified within the assigned recheck scope.

Independent QA's exact-target full-suite result remains necessary for overall acceptance. This report does not replace that result or claim live microphone/RDP coverage. No implementation, task, ledger or historical report edits were made by this reviewer.

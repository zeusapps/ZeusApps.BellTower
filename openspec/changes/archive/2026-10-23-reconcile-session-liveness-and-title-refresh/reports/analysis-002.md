# Analysis report: reconcile-session-liveness-and-title-refresh / 002

## Review target

- Reviewer / role: `liveness_analysis` / code-analyzer through the current Codex session. Internal service version/mode unavailable; orchestrator-observed CLI 0.164.0, SDK 10.0.113.
- Date: 2026-10-23; before/after identity checks at 17:33:30 and 17:34:00 UTC.
- Change root: `D:/Code/ZeusApps.BellTower/openspec/changes/reconcile-session-liveness-and-title-refresh`.
- Assignment: recheck ANALYSIS-001 and ANALYSIS-002, affected tasks 2.3–2.4, 3.2, 4.2 and parity coverage, carrying forward unaffected initial analysis explicitly.
- Scope: same 58 paths in [implementation-paths.txt](../evidence/implementation-paths.txt). Exactly six hashes differ from target 001: `docs/attachment-health.md`, change `evidence/implementation.md`, `TerminalTitlePump.cs`, `AttachmentStore.cs`, `PresentationSchedulerTests.cs` and `AttachmentHealthTests.cs`.
- Baseline: `0a7445a506e5306cca515c251e1fadad38fd99de`; supplementary HEAD `64d3e94626b636ca38a3ab0c5b3789686c8fa15b`.
- Identity: [review-target-002.json](review-target-002.json); independently reproduced tracked diff SHA-256 `93e78e0726311a6f3551e6a8f6ce4d8a732a71df469df9ad66274f9739f43eb9`, using Git stdout lines joined with LF plus final LF when nonempty, UTF-8 without BOM. All 58 recorded file hashes, including untracked implementation, independently match before and after analysis. No implementation mutations.
- Prior report: immutable [analysis-001](analysis-001.md); no findings from that round are discarded or overwritten.

## Analysis performed

| Area | Method / evidence | Outcome / limitation |
| --- | --- | --- |
| Partial/uncertain write receipt | Inspected receipt invalidation immediately before `WriteAsync`, success-only reassignment, exception handling and desired-change backoff reset | ANALYSIS-001 corrected; both returned failure and thrown IO invalidate obsolete confirmation |
| Replacement destination generation | Inspected captured-host adoption for replacement, missing-field clearing, removal of former launch-shell fallback and extension merge | ANALYSIS-002 corrected; new destination fields remain one generation; durable opt-ins retained |
| New regression coverage | Read four provider × returned-failure/exception presentation cases and four provider × present/missing-destination replacement cases | Assertions cover A/B/A restoration, failure backoff, subsequent deduplication, independent mixed-provider receipt, episode/palette preservation, real process identities, stale/conflicting rejection, controls and extension preservation |
| Backoff design coherence | Compared current resolver's direct PID/creation-time checks on each tick, desired identity fields and retry reset with design section 5 | Substantive agent/shell/epoch or title/palette changes reset retry state. Timestamp-only recapture supplies no stronger destination evidence than the existing per-tick live probe; no concrete missed required scenario found. Resetting on every unchanged hook would defeat failure backoff. No new finding |
| Unaffected architecture | Compared frozen manifests: remaining 52 files unchanged | Carry forward initial source analysis of shared health/grace, tray/cache isolation, owned-helper cleanup, detach lock, extension-data migration and compatibility guards. Neighboring unchanged console/detach/navigation code also remains the same |
| Verification boundaries | Read developer's correction evidence; coordinated functional checks with QA | Developer reports 47 affected passes and warning-free post-format build/format/diff checks. QA independently reruns functional checks and original probes; analyzer does not duplicate broad builds/tests or present developer assertions as independent execution |

## Findings

**No new findings.** Reserved ANALYSIS-100 through ANALYSIS-199 remain unused.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| ANALYSIS-001 | Target 002 above | Source trace confirms old success is cleared before every attempted mutation and only restored after success. Four new matched-provider regression cases explicitly model the failed mutation and restored A, including thrown IO and mixed-session isolation | Verified by independent source analysis; QA owns executed functional recheck |
| ANALYSIS-002 | Target 002 above | Source trace confirms replacement host fields are copied or cleared together; old launch metadata cannot repopulate a missing replacement shell; incoming and old unknown host extensions survive; title/background opt-ins remain from the persisted attachment. Four matched-provider regression cases cover populated and missing destinations | Verified by independent source analysis; QA owns executed functional recheck |

## Assessment and limitations

The corrected target resolves both analysis findings without duplicating product policy or expanding ordinary launch/resume behavior. No remaining blocking analysis concern was found. The orchestrator owns finding closure and overall acceptance using this report together with current QA evidence.

Only this report was written. Implementation hashes stayed unchanged. This recheck adds no performance measurement, authenticated provider/desktop/RDP acceptance, package installation or historical-release rollback claim. Earlier full-suite results remain historical coverage; targeted functional evidence must be taken from the current QA report. Normal audited build asset refresh is recorded by the developer; no optional package retry or package success is claimed.

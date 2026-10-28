# Analysis report: fix-codex-hook-command-shell / 005

## Review target

- Reviewer / role: analyze_hook_fix / code-analyzer.
- Client / version / mode / model: Codex CLI 0.164.0 local tooling; delegated Default-mode analysis; role-configured `gpt-6.1-sol`, high effort.
- Date: 2026-10-27 UTC.
- Assignment: new user-requested task 2.3 benchmark metric review, not another Core correction cycle. Reserved new IDs ANALYSIS-200 through ANALYSIS-249; none used.
- Requirements: [task 2.3](../tasks.md), [revised design](../design.md), [baseline-adjusted runtime delta](../specs/standalone-setup/spec.md). Compare arithmetic mean(full hook elapsed) minus arithmetic mean(literal `echo 1` elapsed) against 300 ms, retaining raw duration, durable-count and replay guards.
- Scope: `tools/BellTower.Ingress.Bench/Program.cs`, `docs/hook-ingress.md`, `docs/follow-ups.md`, and metric planning/evidence coherence. No product Core review expansion or architecture suggestions.
- Baseline / HEAD: `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`.
- Prior reports: [analysis-004](analysis-004.md) retains the final Core correction review; this metric-only assignment neither reopens nor substitutes for it. Current evidence: [benchmark-mean-002](../evidence/benchmark-mean-002.md); [benchmark-mean-001](../evidence/benchmark-mean-001.md) remains the earlier input-draining baseline variant.

Before/after identity command:

```text
./scripts/review-identity.ps1 -Baseline aadbfb3 -Paths tools/BellTower.Ingress.Bench docs/hook-ingress.md docs/follow-ups.md
```

Manifest:

```text
M docs/follow-ups.md
M docs/hook-ingress.md
M tools/BellTower.Ingress.Bench/Program.cs
```

- Tracked diff SHA-256: `f0da46bb1abff79839650abbd29250a56d8208add178308bba056e1897c072a1`.
- Untracked source files: zero; empty-set hash `e3b0c44298fc` in the identity summary.
- Before/after summary: `head=aadbfb3 baseline=aadbfb3 files=3 diff=f0da46bb1abf untracked=0/e3b0c44298fc`; matched. Assigned report/evidence writes are outside this identity.

## Analysis performed

| Area | Method / command | Evidence | Outcome / limitation |
| --- | --- | --- | --- |
| Metric correctness | Inspect arithmetic and independently average every saved duration | Native mean minus baseline mean matches the reported values within floating-point rounding | Passed; [independent evidence](../evidence/analysis-005-metric-checks.md) |
| Matched sampling | Inspect `Program.cs:55`, `:63`, `:65`, `:113`, `:301`, `:315` | Both legs use literal `echo 1` or native command through common shell construction/environment/stdin/timer plumbing, one serial sample plus 120 at concurrency eight; matched batches alternate first leg | Passed source review; process scheduling and bare echo's lack of stdin consumption remain part of the measured comparison |
| Raw statistics | Recompute all six legs from `Timings` | Sample IDs cover 0 through 120 or 0 through 24; counts, first value, arithmetic mean, nearest-rank median/p95/p99, max, failure and three-second counts agree | Passed |
| Artifact coherence | Compare complete report objects in retained latency/result JSON | Reports identical; final result has 242 native captures, 50 legacy events, eight sessions, replay passed and no comparison failures | Passed artifact verification; the analyzer did not rerun capture/drain/replay |
| Failure closure | Inspect `Program.cs:117` through `:204`, `:335`, `:350`; invoke existing benchmark entry point with missing args and with existing trial directory | Failures invalidate estimated-mean success; raw native max, legacy failures, durable-count mismatch and replay mismatch prevent overall success; invalid invocations exit 1 before provisioning | Passed source checks and executed input rejection; timed-out/failed sample and replay-corruption injection not performed |
| Interpretation and history | Read docs, revised design/delta/tasks and both benchmark evidence records | Unclamped negative estimate is visible; raw distributions and prior failed raw-median trial remain labeled under their original methods; input-draining trial 001 is explicitly superseded for literal baseline | Passed; no universal/pure-processing-cost claim |
| Scope/complexity | Inspect new benchmark flow and shared shell/timing runner | Shared runner avoids divergent preparation; added batching and retained per-sample statistics serve the approved comparison and independent audit | No concrete simplification finding; no style suggestions |
| Hygiene / whitespace / identity | Own output scan, `git diff --check`, before/after identity | Zero targeted personal/secret-pattern hits, no whitespace errors, matching source | Passed |
| Build / product tests / fresh benchmark | Not repeated by analyzer | Independent QA owns current build verification; existing saved run is independently audited here | Not performed; no broader product acceptance claimed |

## Findings

**No findings.** The bounded metric revision is ready from code analysis. No IDs in ANALYSIS-200 through ANALYSIS-249 are assigned.

## Re-verification

| Finding ID | Corrected target | Independent evidence | Outcome |
| --- | --- | --- | --- |
| None; new metric task | Current three-file identity | Fresh trial 002 arrays independently recomputed and source/interpretation reviewed | Passed bounded analysis |
| Prior ANALYSIS-001/002/003/050 | Earlier product target reviewed in analysis 004 | Product correction approval is carried forward from that report; this assignment does not alter those product paths | Not rerun here; no new closure claim |

## Assessment and limitations

The implementation follows the user's revised comparison rather than subtracting medians or relabeling the historical raw-median failure. Claude's independently recomputed arithmetic difference is 86.28282231404964 ms; Codex's is -49.64635371900795 ms. Both meet the approved below-300 ms estimated-added-runtime comparison for this run. Native raw maxima are 254.6142 ms and 587.8654 ms, respectively, so the separate below-3000 ms guard passes. Failed baseline or native calls cannot produce a passing comparison. Fixed sample generation supplies equal counts; exceptions interrupt the benchmark rather than silently omitting a failed launch/write/timeout from a successful run.

The exact baseline command is `echo 1`. It shares event serialization and stdin-write plumbing with native ingress but does not consume stdin itself; an input-write exception aborts rather than being ignored. The evidence records no such exception in this run. Alternating matched batches reduce ordering bias but do not isolate individual processing stages or eliminate machine scheduling/noise. In particular, the negative Codex value means that this measured baseline was slower on average; it does not establish negative or zero helper cost.

The retained artifacts substantiate the reported sample statistics and recorded successful durable/replay outcome. This was an isolated generated-shell trial using synthetic hooks, not a native authenticated or user-operated post-migration trial. The historical failed raw-median observation stays valid under its historical criterion; the current task uses the explicitly revised criterion. No product build, installation, user settings change or new benchmark was performed by the analyzer.

This revision is ready within the assigned scope. Whole-change completion, final task/ledger state and independent QA acceptance remain the orchestrator's responsibility. User-operated packaged-candidate post-migration, mixed-session and trust acceptance remain open; exact host shell fallback branches remain outside this metric review. No extra implementation round or architecture work is requested.

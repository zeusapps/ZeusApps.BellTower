# Adjusted-mean benchmark 002

Developer role model: `gpt-6-luna`.

## Literal `echo 1` correction

This fresh run uses the exact baseline command `echo 1` for both Bash and PowerShell. It supersedes the baseline definition in benchmark evidence 001 for the user's literal command request; evidence 001 and all its numeric results remain unchanged and are retained as the earlier input-draining variant.

The direct baseline still uses the common `ShellStart`, process environment preparation, event serialization/stdin write and timer plumbing used by the full hook. Unlike trial 001, the command itself does not read or discard stdin. A possible broken-pipe/write race was allowed to surface: no input-write error occurred in this run. All baseline calls returned trimmed stdout `1`; all hook calls returned `{}` and persisted captures.

Estimate:

```text
arithmetic mean(full generated hook elapsed) - arithmetic mean(bare `echo 1` elapsed)
```

Each provider used one serial first sample and 120 samples at concurrency 8 for both baseline and hook. Matched eight-sample batches alternated baseline-first and hook-first. The same shell executable/arguments, environment and event input were used for each matching sample index. The 25-sample legacy control remained one serial sample plus 24 at concurrency 8.

## Trial

- Package: 0.1.9015, the previously reviewed NativeAOT candidate. Its ZIP SHA-256 remains `B6F4A1C1184190C4527C3423DF3146E4EA4335AE17CFC25C88971C0601A12488`. No product rebuild or installation occurred.
- Exact command:

  ```text
  dotnet tools/BellTower.Ingress.Bench/bin/Release/net10.0-windows/BellTower.Ingress.Bench.dll artifacts/dev-builds/bell-tower-0.1.9015-win-x64 artifacts/codex-hook-shell/mean-20261027-02
  ```

- The command ran outside the default sandbox, completed successfully, and wrote `results.json`. The isolated trial used generated provider settings and synthetic events; no authenticated provider interaction occurred.
- Every measured duration and success flag is retained in ignored `artifacts/codex-hook-shell/mean-20261027-02/latency-report.json`; durable count and replay status are in ignored `artifacts/codex-hook-shell/mean-20261027-02/results.json`.

| Provider / mode | Samples (first + parallel) | First ms | Mean ms | Median ms | p95 ms | p99 ms | Max ms | Failed | >=3000 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Claude bare `echo 1` | 121 (1 + 120, concurrency 8) | 66.585 | 118.628 | 117.869 | 156.703 | 163.833 | 163.855 | 0 | 0 |
| Claude native ingress | 121 (1 + 120, concurrency 8) | 137.762 | 204.911 | 203.978 | 245.120 | 252.789 | 254.614 | 0 | 0 |
| Codex bare `echo 1` | 121 (1 + 120, concurrency 8) | 389.688 | 530.764 | 525.559 | 593.653 | 605.009 | 605.642 | 0 | 0 |
| Codex native ingress | 121 (1 + 120, concurrency 8) | 334.883 | 481.118 | 479.954 | 547.921 | 580.519 | 587.865 | 0 | 0 |
| Claude legacy control | 25 (1 + 24, concurrency 8) | 1217.166 | 2038.573 | 2077.584 | 2180.533 | 2180.918 | 2180.918 | 0 | 0 |
| Codex legacy control | 25 (1 + 24, concurrency 8) | 1416.689 | 2359.594 | 2380.863 | 2596.583 | 2635.055 | 2635.055 | 0 | 0 |

| Provider | Native mean − bare `echo 1` mean | Estimated mean <300 ms | Native raw max <3000 ms |
| --- | ---: | --- | --- |
| Claude | **86.283 ms** (204.911 − 118.628) | Pass | Pass |
| Codex | **-49.646 ms** (481.118 − 530.764) | Pass; negative retained | Pass |

The report's sample arrays were independently averaged from `Timings[].Milliseconds`; both raw means and differences matched the reported arithmetic values within floating-point rounding. Sample count was 121 on both legs for each provider, with zero baseline/hook failures. The Codex baseline remained slower than the native hook on average in this run, so the estimate is not interpreted as zero or negative helper processing cost.

## Durable and replay guards

- Native capture count: 242/242.
- Legacy event count: 50/50.
- Sessions after drain: 8.
- Committed-capture replay after restoring a retained receipt: passed; state was unchanged.
- No native hook sample reached the three-second raw-duration limit.
- Comparison failures: none; process completed successfully.

## Interpretation

This single matched run meets the revised adjusted-mean and raw-duration thresholds for both providers. The arithmetic difference estimates elapsed runtime above this exact shell baseline and does not isolate per-stage processing cost. The negative Codex difference remains unmodified. The prior raw-latency evidence and benchmark-mean-001 data are preserved under their original methods. No OS file-cache reset is claimed, no product source changed in this correction, and no user installation or provider setting was touched. Independent QA and code-analyzer verification remain pending.

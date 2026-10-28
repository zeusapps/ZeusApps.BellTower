# Adjusted-mean benchmark 001

Developer role model: `gpt-6-luna`.

## Method

This is the user-requested metric revision for task 2.3. The estimate is:

```text
arithmetic mean(full generated hook elapsed) - arithmetic mean(`echo 1` shell elapsed)
```

Both legs use the same provider shell executable and arguments, working directory, environment, and serialized synthetic event input for each sample index. `echo 1` must produce stdout whose trimmed value is `1`; the full hook must produce `{}` and a durable native capture. The shell receives one serial first sample and then 120 samples at concurrency 8. Matched eight-sample batches alternate baseline-first and hook-first order. The legacy control remains 25 samples (one serial plus 24 at concurrency 8).

Raw means, medians, p95, p99, maximum, first sample, sample counts, mode, failures and each sample's duration/success flag are retained for independent recomputation. The estimate is not clamped when negative. It includes run-to-run noise and estimates runtime added over this shell baseline; it does not isolate the ingress helper's pure processing cost.

## Trial

- Package: 0.1.9015, previously reviewed NativeAOT candidate; package ZIP SHA-256 `B6F4A1C1184190C4527C3423DF3146E4EA4335AE17CFC25C88971C0601A12488`. No product rebuild or installation occurred for this benchmark task.
- Exact command:

  ```text
  dotnet tools/BellTower.Ingress.Bench/bin/Release/net10.0-windows/BellTower.Ingress.Bench.dll artifacts/dev-builds/bell-tower-0.1.9015-win-x64 artifacts/codex-hook-shell/mean-20261027-01
  ```

- The process ran outside the default sandbox after the established MSBuild/testhost and Git Bash sandbox IPC denials. It exited 0. The trial used isolated generated provider settings, project and data; no authenticated provider interaction occurred.
- Individual samples and full summaries: ignored `artifacts/codex-hook-shell/mean-20261027-01/latency-report.json`. Count/drain/replay result: ignored `artifacts/codex-hook-shell/mean-20261027-01/results.json`.

| Provider / mode | N (serial + parallel) | First ms | Mean ms | Median ms | p95 ms | p99 ms | Max ms | Failed | >=3000 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Claude `echo 1` | 121 (1 + 120, concurrency 8) | 76.517 | 120.687 | 121.778 | 154.816 | 170.385 | 178.987 | 0 | 0 |
| Claude native ingress | 121 (1 + 120, concurrency 8) | 149.545 | 215.988 | 217.204 | 257.827 | 279.201 | 279.201 | 0 | 0 |
| Codex `echo 1` | 121 (1 + 120, concurrency 8) | 456.412 | 559.333 | 549.900 | 635.648 | 673.362 | 678.196 | 0 | 0 |
| Codex native ingress | 121 (1 + 120, concurrency 8) | 329.270 | 487.239 | 488.104 | 541.592 | 563.990 | 565.984 | 0 | 0 |
| Claude legacy control | 25 (1 + 24, concurrency 8) | 1631.004 | 2187.517 | 2210.113 | 2324.333 | 2333.635 | 2333.635 | 0 | 0 |
| Codex legacy control | 25 (1 + 24, concurrency 8) | 1397.740 | 2240.849 | 2264.935 | 2415.360 | 2570.166 | 2570.166 | 0 | 0 |

| Provider | Native mean − `echo 1` mean | Below 300 ms | Raw max below 3000 ms |
| --- | ---: | --- | --- |
| Claude | **95.301 ms** | Pass | Pass |
| Codex | **-72.094 ms** | Pass; negative retained | Pass |

Both comparisons had equal sample counts and zero failed baseline or hook calls. PowerShell's `echo 1` mean exceeded the Codex hook mean in this trial; therefore the negative estimate is reported as observed, not interpreted as negative helper cost. The benchmark applies no median subtraction and does not relabel the historical raw-median trial.

## Durable and replay guards

- Native capture count: 242/242.
- Legacy event count: 50/50.
- Sessions after drain: 8.
- Committed-capture replay after restoring a retained receipt: passed; replay did not change committed state.
- No hook sample reached the three-second raw-duration limit.

The benchmark stores all these guards in `results.json` and fails nonzero if a baseline or hook call fails, either latency limit fails, output is not neutral, durable counts differ, or replay changes state. Latency summaries are written before the gates are evaluated; the final result includes comparison failures when a latency gate fails.

## Interpretation and limits

This single matched, interleaved run meets the revised `<300 ms` estimated-added-mean threshold for both providers and retains full raw end-to-end latency. The estimate is workload-, shell-, environment- and timing-dependent. In particular, a negative result indicates that the measured baseline was slower on average than the measured hook in this run; it is not proof of zero or negative processing cost. The 2026-10-27 raw-latency measurements and historical evidence remain unchanged under their original definition.

No OS file-cache reset is claimed. A separate user-operated post-migration or mixed-session acceptance trial was not run. The measured task result awaits independent QA and code-analyzer review.

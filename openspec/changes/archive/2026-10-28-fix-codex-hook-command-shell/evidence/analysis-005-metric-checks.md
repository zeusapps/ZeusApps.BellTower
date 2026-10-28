# Analysis 005 metric checks

Reviewed source summary: `head=aadbfb3 baseline=aadbfb3 files=3 diff=f0da46bb1abf untracked=0/e3b0c44298fc`. See [analysis-005](../reports/analysis-005.md) for full identity and scope.

## Independent arithmetic audit

Inputs are ignored retained files `artifacts/codex-hook-shell/mean-20261027-02/latency-report.json` and `results.json`. A read-only PowerShell check recomputed statistics directly from every `Timings[].Milliseconds` value, independently of each stored summary. It checked unique contiguous sample IDs, counts, arithmetic means, first samples, nearest-rank median/p95/p99, maximum, failed-call count and count reaching 3000 ms. Numeric mean comparisons used tolerance 0.00000001 ms. Complete report arrays in the two JSON files were also compared.

| Provider / leg | Samples | Recomputed mean ms | Maximum ms | All checks |
| --- | ---: | ---: | ---: | --- |
| Claude baseline | 121 | 118.62831404958679 | 163.8553 | Passed |
| Claude native ingress | 121 | 204.91113636363627 | 254.6142 | Passed |
| Claude legacy control | 25 | 2038.5728680000002 | 2180.9176 | Passed |
| Codex baseline | 121 | 530.7644347107436 | 605.6422 | Passed |
| Codex native ingress | 121 | 481.1180809917356 | 587.8654 | Passed |
| Codex legacy control | 25 | 2359.594344 | 2635.0554 | Passed |

- Claude independently recomputed difference: 86.28282231404964 ms; reported 86.28282231404948 ms. The tiny difference is floating-point addition order.
- Codex independently recomputed and reported difference: -49.64635371900795 ms. No clamping is applied.
- Baseline/native indices cover 0 through 120; legacy indices cover 0 through 24. Every saved sample succeeded. Both native and baseline legs report zero samples at or above 3000 ms.
- Both providers report valid comparison, estimate below 300 ms and native maximum below 3000 ms, consistent with the arrays and source rules.
- `results.json` report objects equal those in `latency-report.json`; native count 242, legacy count 50, drained sessions eight, crash replay `passed`, comparison failures empty.

This independently verifies saved statistics and result coherence. It does not independently rerun actual command delivery, count durability or crash replay.

## Entry-point rejection checks

The existing built benchmark entry point was invoked without launching a measurement trial:

```text
dotnet tools/BellTower.Ingress.Bench/bin/Release/net10.0-windows/BellTower.Ingress.Bench.dll
```

Observed exit 1, ArgumentException, instruction to supply isolated package root and a new artifact directory, and explicit statement that no performance pass is claimed.

```text
dotnet tools/BellTower.Ingress.Bench/bin/Release/net10.0-windows/BellTower.Ingress.Bench.dll artifacts/dev-builds/bell-tower-0.1.9015-win-x64 artifacts/codex-hook-shell/mean-20261027-02
```

Observed exit 1, IOException, instruction to use a new isolated artifact directory, and explicit statement that no performance pass is claimed. The existing trial was rejected before provisioning; no fresh benchmark was run. These checks exercise built entry-point input rejection; independent current-source build verification belongs to QA.

## Source guard review and boundaries

`Program.cs:117` rejects failed baseline/native calls from a valid comparison; `:118` compares the unclamped arithmetic-mean difference with 300; `:119` preserves raw native maximum below 3000. The final nonzero gate retains failures from either provider and legacy controls. Durable count mismatch and changed committed replay state throw before any successful completion. Launch/write/timeout exceptions propagate to the nonzero entry-point handler, so they do not silently reduce the successful sample denominator.

`RunNoop` passes the literal command `echo 1`; `RunStarted` validates trimmed output `1`, zero exit and empty stderr for the baseline, and `{}` plus quiet success for native ingress. Common shell arguments, environment, synthetic event construction and timer placement are used for both legs. Trial 001's input-draining baseline is not substituted for the literal trial 002.

No failed-sample, timeout or replay-corruption injection was performed. No new build, full product suite, benchmark, user installation/configuration update or native user trial was performed by the analyzer. Prior product correlation review remains recorded in analysis 004. The source identity matched before/after, `git diff --check` passed, and the assigned-output hygiene scan found no targeted personal or secret patterns.

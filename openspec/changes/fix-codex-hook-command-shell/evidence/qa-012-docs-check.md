# QA-012 documentation check: ORCH-001

## Result

**Passed.** The stale adjusted-mean review-status clause reported as QA-500 / canonical ORCH-001 is gone from `docs/hook-ingress.md`. The replacement says trial 002's arithmetic and guards were independently verified and links [QA 005](../reports/qa-005.md) and [Analysis 005](../reports/analysis-005.md). Those reviews confirm the formula, both estimates, sampling/guard checks, and raw-data calculations.

The paragraph retains the separate boundary: the synthetic benchmark is not authenticated native-provider or user acceptance. Trial 002's values, raw distributions, capture counts, replay result, and distinction from the superseded input-draining trial are unchanged. No other paragraph or source behavior was in scope for this light check.

## Checks

- Compared the revised paragraph in `docs/hook-ingress.md` with completion record [012](completion-docs-012.md), [QA 005](../reports/qa-005.md), and [Analysis 005](../reports/analysis-005.md): passed.
- Rechecked source identity against `aadbfb366bd24906a8e477d09957d08c5ac6bf3d`: before/after `head=aadbfb3 baseline=aadbfb3 files=22 diff=659f1706b2bc untracked=3/cce5a39616ce`, unchanged.
- No tests, build, formatter, or native probes were run; this round changes one documentation sentence and its completion record only.


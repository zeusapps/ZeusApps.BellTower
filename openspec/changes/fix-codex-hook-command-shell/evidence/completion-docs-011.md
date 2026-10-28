# Completion documentation 011

Corrects two factual claims in [completion documentation 010](completion-docs-010.md) and the corresponding current product documentation:

- The literal `echo 1` matched-mean trial 002 was independently verified by [QA 005](../reports/qa-005.md) and [analysis 005](../reports/analysis-005.md). The documentation no longer describes that benchmark as awaiting independent review. The review validates the revised baseline-adjusted metric and its raw-data guards; it does not relabel the separate historical raw-median result.
- The post-migration 0.1.9018 user trial passed the stated task 5.2 checks for both providers. The installed Claude CLI version probe reported 2.1.311, but the screenshot does not identify the Claude process version. The trial did not include a separate trust-dialog interaction; this is recorded as an observation boundary rather than an open acceptance gate.

Updated files: `docs/follow-ups.md` and `docs/compatibility.md`. No source or test files changed.

Checks: `git diff --check` passed for the two updated docs and this report. Review identity is recorded in the task handoff; no build or tests were needed for these documentation-only corrections.

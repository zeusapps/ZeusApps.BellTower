# Implementation 007: fix cycle 1 for ANALYSIS-160 to -164 (DEV-007)

Baseline `02d4f56`. Client / model: Claude Code, `claude-sonnet-5-5`. Nothing here is closed or approved by the developer; each fix awaits independent verification. Suggestions ANALYSIS-165 to -171 and QA-163 to -166 were not touched.

## Per finding

- **ANALYSIS-160 (gate = ingress contract).** `packaged-hook-benchmark` gates `added-mean-below-300ms`: mean full hook minus mean `echo 1` through the same shell, session, input path, sample count and concurrency, for round trip and in-agent, below 300 ms; negative estimates pass and stay visible; a failed baseline fails `hooks-accepted`. `raw-max-below-3s` (three-second raw guard), `durable-within-3s` and `event-counts-exact` stay gates. The raw median, p95, p99 and max are reported in `raw-distribution-informational` (labeled INFORMATIONAL, not a gate, never relabeled). The mixed run now has both control legs in the same batches, each through its own provider's shell. The old `median-below-300ms` check is gone.
- **ANALYSIS-161 (neutral leg).** `echo 1` stays the contract control. A third rotating leg (`HookTiming.Rotate`, new in the shared source) runs a native no-op: `/usr/bin/printf 1` through Git Bash, `[Console]::Out.Write('1')` through PowerShell (`ProviderModel.NeutralCommand`). `neutral-control-informational` reports hook minus neutral, labeled informational and not the contract baseline. The Codex PowerShell argument vector (`-NoProfile -NonInteractive`) is recorded as modeled and unverified for Codex 0.164.0 in the run notes and in `harness-2.3.md`, with the closing evidence still open (the Codex source line that builds the PowerShell command line, or a probe of a real hook runner).
- **ANALYSIS-162 (surface checks).** Badge stays the installed output (`badges-own-and-no-foreign-ticket`). Tab, chip details and speech are labeled "product function over installed state, evaluated in-process with the harness build (not an installed output)" and replaced by cross-conversation checks `tab-distinct`, `chip-distinct`, `speech-distinct` (`Harness/SurfaceDistinctness.cs`). The copied subject-word rule and the self-comparisons are removed. The speech-distinctness check passed in all three groups on today's policy (no four-word-cap collision occurred with these fixtures); a unit test proves two conversations alike in the first four words fail it. Finding to note for the orchestrator: the product's four-word cap can make two such conversations sound identical, and no field fixture exercises that today.
- **ANALYSIS-163 / task 2.8 (product fix).** `CheckoutPolicy.Choose` takes an optional retained checkout used only in place of the launch fallback (`CheckoutPolicy.Retained`, "retained: branch changed in the verified checkout"). `IdentityResolver` marks a checkout retained when a same-checkout branch change cleared the work that a unique work mapping had verified, and passes it back while the source stays `Retained` and it is still a worktree of the launch repository (`CheckoutPolicy.RetainedCheckout`). Pins, provider evidence, ambiguity, a missing pin and new work evidence still win; the subject is cleared exactly as before and `AutomaticSubject` re-resolves from the new branch (unit test: it is the new branch, not the old change). Shared policy; no provider branch. The 2.2 distinctness check no longer exempts fallback conversations, and a new check `branch-change-keeps-working-checkout` proves each switched conversation keeps its own worktree with the refreshed branch (`DEP-n-hotfix`) and worktree. Installed run: Claude 5/5, Codex 5/5 and mixed 10/10 distinct names, tabs, chips and speech after the branch change.
- **ANALYSIS-164 (Codex native names).** `RunContext.Unfinished` / `CheckResult.Unfinished` is a separate outcome: never a pass, not a failure, counted in the summary, JSON (`unfinishedItems`) and console. `codex/native-title-tier` and `mixed/native-title-tier` report it; the old assertion that `NativeTitle == ""` is removed from `native-and-reset`. Ledger F7 now says no fixture covers Codex explicit native names and names the open gap (user decision 2026-11-25, open for 0.1.11) and its closing evidence (a supported read with name provenance, or a version-scoped probe). `CapabilityMatrix` is unchanged (its text is correct).

## Spec gap for the orchestrator

`session-identity` "Git branch and worktree context" says a uniquely verified working checkout supplies the displayed context and that on a branch change the displayed context refreshes. It does not say the verified checkout stays the working checkout after the work that verified it is cleared by that branch change. The retained-checkout basis (and its `retained: ...` source label) is behavior without precise spec text.

## Runs (installed harness, disposable roots; fixture-version 0.1.11 candidate rebuilt from this tree)

- `five-conversations`: 84 of 86 passed, 0 failures, 2 unfinished (the Codex and mixed native-title tier) in [harness-2.2](harness-2.2.md).
- `packaged-hook-benchmark` twice, 38 of 38 each; numbers in [harness-2.3](harness-2.3.md). Contract estimate / raw median / hook-minus-neutral (run 1, run 2): Claude 60.7, 52.9 ms / 210.4, 173.3 ms / 30.5, 30.6 ms; Codex -166.8, -167.5 ms / 465.1, 421.5 ms / 56.3, 18.4 ms; mixed -43.2, -34.2 ms / 244.3, 316.7 ms / 40.7, 48.9 ms.
- `--scenario all`: 159 of 161 passed, 0 harness failures, 1 expected failure observed as predicted (from the earlier scenario), 2 unfinished. Its contract estimates: Claude 51.3 ms, Codex -154.6 ms, mixed -49.4 ms.
- All runs: production isolation held (no production file, task or process changed).

## Checks

Release build 0 warnings, 0 errors. FieldAcceptance tests 83 passed. New or changed unit tests: `CheckoutResolutionTests` (both providers), `SurfaceAndGateTests`. Identity and checkout groups (`CheckoutResolutionTests`, `IdentityRecovery*`) 84 passed. `dotnet format --verify-no-changes` on the 13 changed C# files: exit 0 (a mutating format of those files ran after the harness runs and changed whitespace only). Full suite: 2194 of 2195 passed; `CodexSharedRuntimeServiceTests.ForkedOrDelegatedThreadIsRefusedByPolicyWithAnUnverifiedStatusAndNothingIsWritten` failed once in the full run and passed alone (18 of 18 in its class); it does not touch checkout policy.

## Process note

An orphaned unit-test tray (disposable `artifacts/unit-tests` data directory, parent process gone) held `bell-tower.exe` in `src/BellTower/bin` and blocked the build; that one process was stopped. The user's installed tray and agents were not touched.

## Docs

`docs/distribution-validation.md` and `docs/development.md` describe the new gate, the legs, the unfinished outcome and the retained checkout.

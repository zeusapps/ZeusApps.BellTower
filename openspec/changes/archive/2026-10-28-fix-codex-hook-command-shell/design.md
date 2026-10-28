# Design

## Context

`HookCommandBuilder` (introduced in `0a7445a`, change `2026-10-23-speed-up-claude-hook-ingestion`) emits Bash quoting for Claude and Windows command-shell quoting for Codex. That change recorded the Codex shell as a "verified native runner contract", and its latency trial ran the generated commands through locally chosen shells, not through Codex's hook runner. The 2026-10-27 harness found Codex 0.164.0 running hooks through PowerShell 7 (`%COMSPEC%` unexpanded, `$PSHOME` expanded), and the generated command failed there.

The [version-pinned source investigation](evidence/native-investigation-002.md) now establishes that Codex hooks inherit the selected turn environment's shell program and arguments. The Windows default is PowerShell, with an ultimate cmd fallback; COMSPEC is used by the command runner only when no shell program was supplied. Two native environment probes observed PowerShell 7. [Additional native evidence](evidence/native-investigation-001.md) proves the installed parser rejects the tested command-array form and actual Claude Code 2.1.310 delivers the generated Bash command. Exact host executable resolution and every fallback/alternate handler branch are not claimed verified.

The user's real Codex configuration has not been inspected. Its hook entries reference two older ingress images (harness note), so a real-environment check must be read-only and must not run or rewrite those hooks.

H-2 was seen once, with asynchronous registration, in the managed-runtime harness scenario.

## Goals / Non-Goals

**Goals:**
- Codex hook commands that run in the shell Codex actually uses, proven by a probe through Codex itself.
- Setup verification that runs through each provider's real hook shell, so this class of defect cannot be installed silently again.
- Honest delivery diagnostics for both providers, and an explicit migration for existing owned entries.
- A deterministic H-2 reproduction and, if confirmed, a shared-policy fix.

**Non-Goals:**
- Changing Claude's hook form unless its real-shell verification fails.
- Changing human-wait confirmation policy (archived `confirm-claude-human-permission-waits`).
- Editing the user's provider settings outside the previewed migration, or recovering events lost before the fix.

## Decisions

1. **Evidence before form.** First, determine Codex 0.164.0's Windows hook-shell rule from a reproducible probe through the installed Codex. Run it with isolated `CODEX_HOME`, the fake model, and at least two shell environments: default, and a different default shell or `COMSPEC`. Read Codex's open-source hook runner for the same version where available. Also check whether Codex supports an argument-vector (exec) hook form or a shell field. *Alternative rejected:* switching to the PowerShell call form now. That would break any setup where Codex still uses the command shell.
2. **Prefer a shell-independent form.** If Codex supports an argument-vector form, use it, which removes quoting entirely. Otherwise generate for the verified shell rule. If the rule depends on the environment, emit a single command that parses identically in both the command shell and PowerShell, if one is proven, or select the form per detected shell at setup, re-checked by doctor. Keep the direct ingress so the latency goal is not regressed. *Alternative rejected:* the old `powershell.exe -EncodedCommand` chain, which violates the latency requirement.
3. **Verify through the actual provider shell.** Stage the immutable helper at its retained target and verify a durable capture before writing provider settings. Codex's inherited shell is checked through an isolated native SessionStart invocation, temporary CODEX_HOME/data and a fake loopback endpoint. Claude's explicit Bash adapter is checked through configured Git Bash, with separate native Claude CLI evidence establishing that rule for the tested version. Native results, direct-shell results and user-operated acceptance remain distinct. An unverified or failing command blocks settings application; it is never treated as a successful installation.

   **Selected-shell continuation:** Resolve Codex's command adapter during planning with the validated packaged source helper and isolated native probe. This may launch a disposable native Codex process during preview; it does not write production provider configuration or make a real model request. Generate the concrete retained-target command using that proven adapter. Apply stages the immutable target and re-verifies that exact form before settings writes, refusing shell drift or delivery failure. Doctor rechecks the selected-shell compatibility. A nested cmd dispatch wrapper was investigated and not selected because expansion/quoting across outer shells was not established safe. This is the existing environment-aware selection option from decision 2.
4. **Migration reuses existing ownership.** Broken owned Codex entries are recognized by their recorded ownership metadata and command shape. Replacement goes through the existing previewed migration (`Edited provider hook migration`), so user edits such as timeout or async are surfaced, never silently erased.
5. **Delivery diagnostics are provider-neutral.** A doctor field reports per-provider `configured`, `delivered` (last capture time) and `unverified` when hooks are configured but nothing was ever delivered. Claude and Codex use the same rule.
6. **H-2 in shared correlation.** Reproduce it deterministically by injecting a fixture with request-before-start source ordering for both providers. If it fails, fix it in shared reducer correlation by matching operation identity and fingerprint, not by provider-specific timing.

## Risks / Trade-offs

- [Codex's shell choice depends on the environment] → Decision 2's environment-aware selection, plus a doctor re-check after shell or Codex upgrades.
- [Probing through the real runner needs a session] → Use SessionStart in an isolated `CODEX_HOME`; no model call or approval is involved.
- [Migration touches user settings] → Previewed and explicit only. Unrelated handlers and edits are preserved, and applying it twice is idempotent (existing scenarios).
- [H-2 does not reproduce] → Record the attempt and keep the regression fixture; no speculative change.

## Migration Plan

Ship the corrected builder and diagnostics. After upgrading, doctor flags broken owned Codex entries, and the user applies the previewed migration. Rollback: the previous build keeps its old command form, and the migration's preserved preview allows restoring the prior entries.

## Open Questions

- Whether older Codex versions in the field used the command shell remains unverified. Preserve existing entries until an explicit setup/migration verifies the proposed replacement against the current native runner.
- Windows command selection now has native evidence for PowerShell and the genuine cmd fallback at Codex 0.164.0. The [isolated native cmd probe](evidence/native-investigation-009.md) used unchanged Codex and the packaged helper under a process-local MXC policy denying both PowerShell directories, with no host ACL or installation changes. Its protocol-valid capture identified the actual cmd parent and preserved the special-character paths. The [source review](evidence/analysis-008-source-checks.md) establishes that normal local Windows 0.164.0 selects PowerShell or cmd and has no default Bash branch; that particular Bash scenario is not applicable. Other versions, external-host Bash and untested path-priority combinations remain unverified; no universal provider restriction is inferred.
- The historical Codex raw median exceeds 300 ms. The user subsequently requested a baseline-adjusted arithmetic mean: mean(full hook elapsed) minus mean(echo 1 elapsed) using the same provider shell. Apply the 300 ms Bell Tower overhead comparison to this explicitly revised metric; preserve raw distributions, capture counts, and the three-second raw-duration guard. The difference estimates added runtime rather than isolating individual processing stages. No alternate transport or release completion is accepted here.

## User-requested measurement revision

The user requested the subtraction of the mean runtime of `echo 1` from the mean full-hook runtime. The benchmark must measure the actual baseline, validate its expected output `1`, and use arithmetic means for both terms. Use matched shell executable/arguments, environment, sample counts and concurrency for the baseline and native ingress. Keep first-sample and raw end-to-end statistics visible. Report negative differences without clamping and describe the value as an estimate; independent runs can contain noise or drift. Any failed command, missing durable capture, or replay failure still prevents a successful complete benchmark result. Historical reports remain unchanged.

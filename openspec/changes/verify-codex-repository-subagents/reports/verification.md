# Verification: Codex agent setup after adding Claude Code

- Date: 2026-10-24 UTC
- Orchestrator: Codex CLI 0.164.0, Default mode, workspace-write
- Session: `01a16cf4-e9ce-7a43-b56b-e747d8b2406b`
- Scope: verification only; no OpenSpec apply, implementation edits, personal settings changes, commits, or live user-session manipulation
- Change root: resolved by `openspec status` and `openspec instructions apply` to `D:/Code/ZeusApps.BellTower/openspec/changes/verify-codex-repository-subagents`

## Scorecard

| Dimension | Result |
| --- | --- |
| Completeness | All three Codex roles discovered in the active native tool catalog. QA and analyzer launched. Developer execution and three mixed-client scenarios remain unverified. Six task checkboxes were initially unchecked; this verification does not claim the whole change is complete. |
| Correctness | QA ran under its configured gpt-6-luna role, read shared policy, quoted the current repository instruction, wrote its own report, and exercised review identity success and invalid-path behavior. Per-assignment model override instructions conflict with current Codex precedence. |
| Coherence | Both clients use thin adapters over shared roles, facts, and templates. No scoped implementation mutation was observed. This change has no design artifact or delta specs; existing `repository-agent-workflow` requirements were used for acceptance. |

## Verified behavior and limits

- Codex exposes `developer` and `qa` with configured `gpt-6-luna`, and `code-analyzer` with configured `gpt-6.1-sol` and high reasoning effort. Discovery is native tool-catalog evidence, not a developer execution trial.
- QA startup, shared instructions, native report writing, and the read-only identity script's success and rejection paths passed. See [QA report](qa-001.md).
- The analyzer launched successfully; its independent report and final identity comparison are recorded in [analysis report](analysis-001.md). See the [ledger](findings.md) for report acceptance and final triage.
- Both adapter sets point to shared role instructions; both OpenSpec skill directories contain 12 skills. Inventory and source inspection do not establish execution of every skill or Claude role.
- Installed Claude reports version 2.1.310. No Claude native role was launched in this audit. Historical restriction evidence for 2.1.309 does not prove the behavior of 2.1.310.

## Findings and required next work

The canonical model-override finding and duplicate reviewer evidence are tracked in [findings.md](findings.md). All three custom Codex role files set `model`, and the analyzer also sets `model_reasoning_effort`. Current official [Codex subagent documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents) says values in the custom role file take precedence over explicit spawn settings. The active custom-role tool metadata likewise says those model settings cannot be changed. Consequently the shared workflow's per-invocation override procedure cannot be relied on for these roles. This is documentation/catalog evidence; no rejected override invocation or successful alternative was simulated.

Task 1.1 has native discovery and current-instruction evidence in the QA report. Task 1.2 remains incomplete because developer execution and its resolved runtime model were not tested. The developer adapter permits use for authorized apply tasks or approved fixes, so a verification-only request did not launch an implementation specialist. Task 1.3 requires developer-owned configuration/documentation edits, including correcting the historical Claude reviewer restriction scope; this audit made none. The existing "not yet natively verified" notes should eventually be narrowed to match the actual per-role evidence rather than removed wholesale.

Tasks 2.1–2.3 remain unverified: real cross-client hand-off with report/finding continuity, simultaneous Claude/Codex orchestrators with Bell Tower identity and attention observed, and a second main orchestrator asking before taking over an active change. Native reviewer concurrency in this audit is not a substitute for those main-session scenarios. No live trials were started on the user's behalf.

## Target integrity

Baseline and HEAD: `d307c344e22999e48b3c62bc664f0e552fdef3b9`.

Scoped paths: `AGENTS.md .codex/agents .claude/agents .agents/roles .agents/templates .agents/workflows docs/agent-workflow.md scripts/review-identity.ps1`.

Parent before/after identity:

```text
Summary: head=d307c34 baseline=d307c34 files=0 diff=e3b0c44298fc untracked=0/e3b0c44298fc
```

Independent reviewer identities must match this target; acceptance is recorded in the ledger. Reports and the ledger are outside the implementation identity. Unrelated product work was already dirty at the start and was not edited by this audit.

The bounded Codex audit is complete when both reports are accepted. The full OpenSpec change is not ready for completion or archive: the model-override issue and the listed execution gaps remain open. No archival or implementation is authorized by this verification result.

# Design

## Context

HookAdapter currently maps both PermissionRequest and Notification/permission_prompt to the same Permission event. SessionState applies 15 s unless Codex native verification supplies its five-second confirmation. The report and current Claude docs distinguish approval-flow start from a human prompt: permission_prompt arrives after approximately six seconds without typing. Notification lacks tool_use_id; PermissionRequest also may omit it. Main custom-agent metadata still needs an exact fixture.

All changes in this plan are required for the same next release. This artifact is self-contained with its proposal, specs and tasks; the ordered program handoff is docs/handoffs/next-release-plan.md. Test only isolated --data-dir/configuration/transcripts and disposable hosts; never write user production data or settings, send production messages, play physical sound, or stop user agents/terminals. Deliver the same user-facing feature for Codex and Claude Code by default, with shared policy and provider-specific adapters. Preserve data and safe runtime compatibility; an exception requires a verified version/mode-specific hard restriction and an honest fallback. New shared JSON writers must preserve unknown fields and declare compatibility before old/new coexistence or rollback is claimed.

**Required predecessors:** `speed-up-claude-hook-ingestion`, `reconcile-session-liveness-and-title-refresh`.

**Primary source boundaries:** Providers/HookAdapter; Core/Observation, WaitState, SessionState and SessionReducer; tray eligibility/diagnostics; provider and permission regression fixtures.

## Goals / Non-Goals

**Goals:** Eliminate timing-only false human-permission alerts in both clients using shared state/grace policy and verified provider signal adapters.

**Non-Goals:** Do not answer approvals, use an extra arbitrary Claude debounce, infer a tool ID from message prose, or mistake timing-only hook fallback for proven human wait.

## Decisions

### 1. Provider-specific provisional and confirmed state

Keep one provisional/confirmed/resolved/unverified approval model for both clients. Claude notification and Codex verified owning-runtime approval status supply normalized evidence; neither raw approval-flow start proves human wait. Use a shared five-second human-wait grace and count the provider's documented elapsed notification delay so Claude's delayed signal receives no extra fixed delay. Preserve safe exact-thread runtime verification and snapshot migration. Timing-only hook fallback is unverified, not a default exception allowing false confirmed alerts; investigate equivalent supported signals in each mode and record proven restrictions/fallbacks explicitly.

### 2. Correlation without inventing IDs

Use supported operation/turn/attachment IDs and fingerprints from either provider, preserving ambiguity when a source lacks IDs. Keep completion and source-order receipts to reject late notification or runtime confirmation after resolution. Test same-fingerprint parallel approvals, result-before-confirmation and unrelated completion in both clients. Do not clear an arbitrary parallel request or infer IDs from message prose. Map source observations to common state before eligibility/reminder/webhook policy.

### 3. Confirmation and restart

Persist source/provenance/freshness and common confirmation state for both clients. Restore due waits silently and retain independent episodes, viewed/acknowledged state and original wait timing. Clear on supported correlated resolution, prompt/end or replacement attachment. Migrate old timing-only snapshots as unverified unless fresh supporting evidence establishes a human wait; do not turn old fallback episodes into newly confirmed alerts.

### 4. Coverage diagnostics instead of silent fallback

Report configured/observed/current confirmation capability by provider/version/mode. Preserve provider trust and user settings. If no supported signal can be obtained, require current contract/probe evidence before documenting a hard restriction, retain available monitoring with honest status and never assert confirmation from elapsed time alone. Missing API integration or captures remains release work, not an accepted limitation.

### 5. Question-tool approval flows

Isolated 2026-10-26 captures (`evidence/claude-askuserquestion-hooks.json`, `evidence/codex-request-user-input-hooks.json`) show the question dialog itself runs through Claude's approval flow. Claude 2.1.310 (interactive, `auto` mode) emits PreToolUse(AskUserQuestion) with tool_use_id, PermissionRequest(AskUserQuestion) at +127 ms without tool_use_id but with the same tool_input and prompt_id, permission_prompt at +6.3 s with prompt_id and no tool fields, then PostToolUse with the original tool_use_id. HookAdapter currently maps the latter two to Permission, producing the field-observed Question→Permission flip. Codex 0.164.0 emits no approval hook for request_user_input in Plan mode (PreToolUse, then PostToolUse ~17 s later); in Default mode it rejects the call without a dialog, emitting PreToolUse then Stop with no PostToolUse.

Treat question-tool approval observations as provisional/confirming evidence for the pending question wait, correlated by tool name plus input fingerprint or prompt scope, never as a new permission wait. An unattributed permission_prompt with only a question pending confirms the question; with a question and an uncorrelated permission pending it is ambiguous and assigns nothing. A rejected Codex question clears at Stop. Open: whether a Codex owning-runtime user-input request signal can confirm Plan-mode questions before any chip appears, and whether Claude permission modes other than `auto` differ.

### 6. User decisions after review (2026-10-26)

- Sessions with no supported confirmation signal (for example Codex without a verified owning runtime, or when its status read fails) show a visibly unverified "possibly waiting for approval" chip after 15 s: no sound, no reminder or escalation eligibility, never labelled confirmed. This keeps a visible cue without reintroducing false audible alerts.
- ~~An ambiguous prompt held while a question and a permission are pending is re-evaluated when the question resolves.~~ Withdrawn after review (ANALYSIS-100) and an isolated capture (`evidence/claude-question-plus-bash-hooks.json`): Claude runs a question and a tool requested in the same message sequentially, so the held prompt is the question's own and must not be credited to a later permission by elimination. A permission relies on its own later prompt. A prompt cannot confirm a request younger than Claude's documented ~6 s notification delay.
- A confirmation arriving after the user dismissed the unverified chip starts a fresh attention episode (QA-100).
- Claude sessions where no permission_prompt has been observed get the same silent unverified chip after 15 s, and diagnostics name `CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS` as a known way the signal becomes unavailable.

## Risks / Trade-offs

- [Uncorrelated late prompt recreates resolved attention] -> Keep causal receipt/turn boundaries and explicit ambiguous coverage.
- [Provider signal is disabled] -> Expose unavailable confirmation; do not claim all provisional requests are human waits.
- [Signal differences fork the policy] -> Normalize provider evidence into shared state and matched conformance fixtures while preserving native ownership.

## Migration Plan

Accept old permission snapshots conservatively with provenance unknown; do not replay old pending requests as newly confirmed alerts. Preserve episode/viewed/dismissed receipts and snapshot compatibility, and document unsupported rollback contracts if old writers cannot preserve added state.

## Verification and fresh-context notes

Use the reliable ingress contract from dependency one. Cover auto-approval longer than fifteen seconds without notification, real confirmation, no additional debounce, missing precursor, same-fingerprint parallel tools, stale notification after resolution/turn/end, restart, denied/interrupted flows and main/subagent distinction, question-tool approval flows for both clients (including question plus permission pending together and a rejected Codex question). Verify Codex's supported native-confirmed grace and conservative migration of former fifteen-second timing-only snapshots against the new shared policy. Current documentation: https://code.claude.com/docs/en/hooks#notification. No real approvals or sound in tests.

Do not infer implementation completion from planning status. Start by reading this change's proposal, delta specs and tasks, inspecting predecessor implementation/evidence, then rechecking the named source files. Fetch current library/API documentation with the project's Context7 CLI workflow before using platform-specific APIs. Update actual verification evidence as tasks complete; preserve reported-versus-reproduced distinctions. Coverage: Field report 10; prerequisite for dependable reminders and escalation.


## Shared provider architecture and parity gate

Use a shared feature model, resolver/scheduler/policy and persistence contract, with thin Claude and Codex adapters translating their supported observations and presentation capabilities. Do not fork product flows, configuration meaning or feature defaults by client. Preserve existing data, native-name ownership and active sessions while extending both integrations; compatibility does not mean freezing one client's feature set.

For every behavior in this change, acceptance covers Claude and Codex, including mixed simultaneous sessions. A version/mode-scoped exception requires a current primary contract or reproducible probe, a precise unavailable operation, the common fallback retained in Bell Tower, and a regression fixture. Unknown/unimplemented/unverified are not proven restrictions. Record the resulting capability matrix in the release handoff and doctor; missing required parity remains a release gate until implemented or a hard restriction is demonstrated.

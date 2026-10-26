namespace BellTower.Diagnostics;

// Honest per-surface Claude Code / Codex capability status for doctor and docs/compatibility.md.
// Status is one of:
//   supported   implemented with automated coverage for this provider (live trials may still be listed as open)
//   restricted  a proven, version-scoped provider restriction with Proof, Fallback and Regression filled in (AGENTS.md rules)
//   unverified  unfinished or not yet proven either way; never a claim of restriction
public sealed record ProviderCapability(string Status, string Detail);

public sealed record CapabilityRow(string Surface, ProviderCapability Claude, ProviderCapability Codex, string? Proof = null, string? Fallback = null, string? Regression = null);

public static class CapabilityMatrix
{
    public const string Supported = "supported";
    public const string Restricted = "restricted";
    public const string Unverified = "unverified";

    public const string ClaudeVersion = "Claude Code 2.1.306 (Agent SDK 0.3.309)";
    public const string CodexVersion = "Codex 0.164.0";

    // Version and mode evidence for the human-wait confirmation signals (doctor ConfirmationCoverage).
    public const string ClaudeConfirmationEvidence = "Claude Code 2.1.310: Notification permission_prompt follows PermissionRequest by about 6.2 s in auto and default permission modes (isolated fixture captures); other permission modes unverified; live auto-versus-human acceptance on real sessions not performed";
    public const string CodexConfirmationEvidence = "Codex 0.164.0: owning-runtime waitingOnApproval status for managed local sessions; waitingOnUserInput status probe-verified for question dialogs in Plan mode and in Default mode with default_mode_request_user_input; no hook-only human signal; live auto-versus-human acceptance not performed";

    private static ProviderCapability Yes(string detail) => new(Supported, detail);
    private static ProviderCapability Open(string detail) => new(Unverified, detail);

    public static IReadOnlyList<CapabilityRow> Rows { get; } =
    [
        new("Hook capture and lifecycle events",
            Yes("shared NativeAOT capture protocol; documented-contract and native-probe fixtures; authenticated live capture not performed"),
            Yes("shared NativeAOT capture protocol; documented-contract and native-probe fixtures; authenticated live capture not performed")),
        new("Work evidence from successful edits",
            Yes("Write, Edit, MultiEdit and NotebookEdit hooks through the shared classifier"),
            Yes("apply_patch hook (tool_input.command) through the same classifier; a failed patch emits no PostToolUse at 0.164.0 (source-verified)")),
        new("Work evidence from literal shell create commands",
            Yes("Bash and PowerShell hooks through the bounded shell grammar"),
            Yes("exec_command hook through the same grammar; other shell tools are not registered at 0.164.0 (source-verified), unverified for other versions")),
        new("Work evidence from workflow-skill invocation",
            Yes("Skill tool hook with literal change argument"),
            Open("unverified: rust-v0.164.0 has no dedicated skill hook event, but UserPromptSubmit carries the prompt text (an explicit skill mention) and an implicit invocation reads the skill file through the shell tool that PreToolUse/PostToolUse expose; neither surface was probed. Closing evidence: a reproducible probe (explicit and implicit skill invocation, hooks enabled, 0.164.0) showing no usable signal, or an adapter fed by those payloads. Until then the same association comes from the successful patch, edit and literal shell-create evidence that the skill causes; inspection never selects")),
        new("History recovery of work (bounded, incremental)",
            Yes("JSONL tool_use/tool_result pairs; fixtures derived from the 2.1.306 envelope; no live tool transcript captured"),
            Yes("exec_command and freeform apply_patch call/result pairs; shapes verified in rust-v0.164.0 sources and tests; no live capture of the installed binary")),
        new("History recovery of the native title",
            Yes("custom-title record (SDK probe against a disposable 2.1.306 transcript)"),
            Open("not persisted in the rollout at 0.164.0 (kept in session_index.jsonl, an internal file); read through the thread name service for managed runtimes only, where an externally set name pauses writes but is not promoted to a user title because whether it was typed by the user or generated is unverified. Closing evidence: a documented or supported read with name provenance")),
        new("Native chat-name write (managed ownership)",
            Open("unfinished: not implemented in this release; deferred to the sync-native-session-titles change (not a provider restriction). Claude's own surfaces (status line, managed tab, chip, speech) show the shared identity"),
            Yes("dedicated authenticated runtime for new managed local sessions; verified in an isolated native TUI trial; explicit --no-daemon and remote sessions stay unavailable (runtime missing, not a proven restriction)")),
        new("Native-name read, ownership and pause",
            Open("no native write, so no ownership or pause state; only custom-title records that are neither Bell Tower's, the SDK fork title nor released count as user titles (generated ai-title records are ignored); Use Bell Tower name returns to the automatic name. Native sync switches refuse Claude"),
            Yes("shipped pause-only policy: a name set outside Bell Tower pauses writes until the user takes ownership with Use Bell Tower name; Bell Tower never overwrites it")),
        new("Fork, clear and resume",
            Open("SDK forkSession (probe): the parent is proven by the transcript's own forkedFrom records, the fork inherits the parent's pins and work evidence once and keeps its own palette, and the generated \"(fork)\" title is ignored; interactive --fork-session and /branch are unverified and fall back to automatic resolution with the \"(fork)\" title ignored; clear restarts derived state; resume keeps pins and palette"),
            Yes("fork: a thread created by codex fork records its parent in its own first rollout line (session_meta.forked_from_id; openai/codex rust-v0.164.0 protocol, rollout recorder and thread manager sources), so the shared inheritance copies the parent's pins and work evidence once and the fork keeps its own palette; parent_thread_id (spawned sub-agents) is not fork evidence; no live codex fork capture yet. clear and resume use the same shared derived-state reset; no Codex-specific clear or resume transcript captured")),
        new("Presentation: badge, tab title, chip, speech",
            Yes("bundled ANSI status line with a process-free Git fallback; tab, chip and speech from the shared derived record; user-confirmed badge"),
            Yes("native footer thread-name and git-branch fields set reversibly; tab, chip and speech from the same record; native styling is retained")),
        new("Checkout resolution and controls",
            Yes("user pin > verified hook cwd > unique work mapping > launch directory; attach, clear, color and reset controls"),
            Yes("identical policy and controls")),
        new("Color, reset and legacy badge import",
            Yes("shared allocation, rename --color/--reset and explicit OSC rgb import"),
            Yes("identical; Codex footer styling stays native")),
        new("Human-wait confirmation (permission versus automatic approval)",
            Yes("PermissionRequest is tracked as provisional and is never attention alone; Notification permission_prompt confirms it and is eligible on arrival because the shared five-second grace credits the provider's own delay (about 6.2 s observed; Claude documents about 6 s); timing-only evidence is unverified and never alerts, except that a session in which no permission_prompt has been observed (the provider can turn these notifications off, for example with CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS) shows the same silent, visibly unverified \"possibly waiting for approval\" chip after 15 seconds; fixture-verified at 2.1.310 in auto and default modes; live auto-versus-human acceptance on real sessions not performed"),
            Yes("PermissionRequest is provisional; the owning runtime's waitingOnApproval status confirms it with the same shared policy and a five-second grace from the first runtime report; a fresh negative status suppresses a stale request; fixture and fake-runtime verified; live auto-versus-human acceptance not performed")),
        new("Human-wait confirmation without an owning runtime",
            Yes("the confirming signal is a hook, so no runtime is needed"),
            Open("unverified: Codex sessions without a verified owning runtime (explicit --no-daemon, existing or remote sessions) have no human-wait signal in the 0.164.0 hooks (PermissionRequest runs before guardian or user review: openai/codex codex-rs hooks permission_request event), so their approval requests are never confirmed. Supported unverified fallback (shared Core policy, applied to any session without a signal): a request pending 15 seconds shows a visibly unverified \"possibly waiting for approval\" chip with no sound and no reminder or escalation eligibility, cleared by the correlated resolution, stop or end; doctor reports these sessions as unavailable. Closing evidence for a confirmed wait: a supported hook or notification payload that distinguishes a human prompt, or a verified attach path to the owning runtime")),
        new("Question-tool approval flows",
            Yes("AskUserQuestion runs through the approval flow (PermissionRequest without tool_use_id, then permission_prompt about 6.2 s later) in auto and default modes at 2.1.310; both stay confirming evidence for the one question wait, never a permission wait; a question plus an uncorrelated permission makes an unattributed prompt ambiguous and it is never credited to the permission later by elimination (an isolated capture showed the question's own dialog emits that prompt and a following permission dialog is sequential and relies on its own prompt); a prompt cannot confirm a request younger than the documented notification delay (about 6 seconds, floor 5)"),
            Yes("request_user_input emits PreToolUse and a correlated PostToolUse with no approval hook in Plan mode and in Default mode with default_mode_request_user_input; a Default-mode rejection clears at Stop; the owning runtime's waitingOnUserInput status (probe-verified) is read as confirmation evidence and never gates a question; request_user_input_async is not offered by 0.164.0 (hook shape unverified)")),
        new("Setup, configuration, diagnostics and tray controls",
            Yes("setup --clients, --status-line and --desktop; doctor; list; Sessions controls"),
            Yes("identical commands and controls; reversible config.toml footer fields")),
    ];
}

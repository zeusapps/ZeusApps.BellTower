namespace BellTower.Diagnostics;

// Honest per-surface Claude Code / Codex capability status for doctor and docs/compatibility.md.
// Status is one of:
//   supported   implemented with automated coverage for this provider (live trials may still be listed as open)
//   restricted  a proven, version-scoped provider restriction with Proof, Fallback and Regression filled in (AGENTS.md rules)
//   unverified  unfinished or not yet proven either way; never a claim of restriction
//   not applicable  the provider has no such mode or surface (for example Claude Code has no shared backend), stated with the reason
public sealed record ProviderCapability(string Status, string Detail);

public sealed record CapabilityRow(string Surface, ProviderCapability Claude, ProviderCapability Codex, string? Proof = null, string? Fallback = null, string? Regression = null);

public static class CapabilityMatrix
{
    public const string Supported = "supported";
    public const string Restricted = "restricted";
    public const string Unverified = "unverified";
    public const string NotApplicable = "not applicable";

    public const string ClaudeVersion = "Claude Code 2.1.306 (Agent SDK 0.3.309)";
    public const string CodexVersion = "Codex 0.164.0";

    // Version and mode evidence for the human-wait confirmation signals (doctor ConfirmationCoverage).
    public const string ClaudeConfirmationEvidence = "Claude Code 2.1.310: Notification permission_prompt follows PermissionRequest by about 6.2 s in auto and default permission modes (isolated fixture captures); user-operated 2026-10-27 native trials passed Claude human and automatic approval scenarios, including a mixed-provider session; other permission modes remain open";
    public const string CodexConfirmationEvidence = "Codex 0.164.0: owning-runtime waitingOnApproval status for managed local sessions; waitingOnUserInput status probe-verified for question dialogs in Plan mode and Default mode with default_mode_request_user_input; user-operated 2026-10-27 native trials passed managed human/automatic, --no-daemon fallback and mixed-provider scenarios; other modes remain open";

    private static ProviderCapability Yes(string detail) => new(Supported, detail);
    private static ProviderCapability Open(string detail) => new(Unverified, detail);
    private static ProviderCapability Never(string detail) => new(NotApplicable, detail);

    // The one text for the per-mode Codex native-name and runtime-status states. Doctor's Codex.NameSynchronization reports it, so
    // the matrix, doctor and docs/compatibility.md cannot drift apart. The refusal labels are the constants the name status uses.
    public static readonly string CodexNameModes =
        "owned runtime (new managed local sessions): supported, name read and write and work status over authenticated loopback; " +
        "shared backend (existing native backend of the resolved Codex home): supported as a non-owning observer, name read and write and work status, never resume or a turn; " +
        "direct/embedded client (--no-daemon, remote, no runtime): unverified, no access (" + Sessions.CodexAccessStatus.NoRuntime + "), not a proven restriction; " +
        "shared conversations adopted after the launcher exited: unverified, no shared access (unfinished by user scope decision 2026-11-13, not a provider restriction); " +
        "a shared conversation of another Codex home: still read through the tray-home backend, which does not host it, so the answer is unverified and expected to surface as a shared-backend refusal; no name is written unless the read succeeds for the exact thread id, and per-conversation home routing is not built (same user scope decision 2026-11-13); " +
        "fork or delegated threads: unverified, refused by a conservative policy (" + Sessions.CodexAccessStatus.Unverified + ") pending the task 6.1 probe; " +
        "elevated shared backend: refused by scope (Bell Tower observes only a non-elevated backend); native /rename pauses writes";

    public static IReadOnlyList<CapabilityRow> Rows { get; } =
    [
        new("Hook capture and lifecycle events",
            Yes("shared NativeAOT capture protocol; setup executes the generated command through configured Git Bash (CLAUDE_CODE_GIT_BASH_PATH) or standard Git for Windows Bash with a synthetic SessionStart payload, and requires a durable capture before settings changes"),
            Yes("shared NativeAOT capture protocol; Codex 0.164.0 inherits the turn shell; the generated Windows PowerShell call form delivered through PowerShell 7 in two tested environments. With PATH limited to System32 and both PowerShell directories denied in process-local MXC, the generated Cmd form produced a durable protocol-1 SessionStart capture whose HookShell matched its cmd.exe ancestor (including special-character helper/data paths; evidence 009). The pinned local Windows resolver selects PowerShell or ultimate cmd and has no Bash selection branch. External-host Bash and other versions/path-priority combinations remain unverified. Command-array form was rejected")),
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
            Open("not persisted in the rollout at 0.164.0 (kept in session_index.jsonl, an internal file); read through the thread name service for owned runtimes and for a shared backend of the resolved Codex home, where an externally set name pauses writes but is not promoted to a user title because whether it was typed by the user or generated is unverified. Closing evidence: a documented or supported read with name provenance")),
        new("Native chat-name write (managed ownership)",
            Open("unfinished: not implemented in this release; deferred to the sync-native-session-titles change (not a provider restriction). Claude's own surfaces (status line, managed tab, chip, speech) show the shared identity"),
            Yes("dedicated authenticated runtime for new managed local sessions (isolated native TUI trial) and, as a non-owning observer, an existing shared backend of the resolved Codex home (stand-in and isolated native backend reads); explicit --no-daemon and remote sessions stay unavailable (no runtime access, not a proven restriction); the per-mode states are in the rows below")),
        new("Native-name read, ownership and pause",
            Open("no native write, so no ownership or pause state; only custom-title records that are neither Bell Tower's, the SDK fork title nor released count as user titles (generated ai-title records are ignored); Use Bell Tower name returns to the automatic name. Native sync switches refuse Claude"),
            Yes("shipped pause-only policy: a name set outside Bell Tower pauses writes until the user takes ownership with Use Bell Tower name; Bell Tower never overwrites it")),
        new("Native name and runtime status: Codex owned runtime",
            Never("Claude Code has no Bell Tower runtime: its native-title write is the unfinished row above (sync-native-session-titles) and its wait signal is a hook"),
            Yes("authenticated loopback runtime Bell Tower started: exact-conversation thread/read (name, waitingOnApproval, waitingOnUserInput) and thread/name/set through the shared pause and ownership policy; a read sends no resume or turn and discards previews, turns and tokens (CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens); only the owned manager ends the runtime")),
        new("Native name and runtime status: Codex shared backend",
            Never("Claude Code has no known shared-backend mode (design decision 4); retained evidence covers --print captures and launch-shape fixtures, and the interactive shape is a stated assumption"),
            Yes("existing native backend of the resolved Codex home as a non-owning observer: one peer-verified connection, thread/read with includeTurns false for name and work status, thread/name/set under the same pause policy, never resume, turn or approval; the connection is the only thing Bell Tower closes (CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens)")),
        new("Native name and runtime status: direct clients",
            Open("Claude Code is treated as direct (no known shared-backend mode; the interactive shape is a stated assumption, see the shared backend row): the native title is read from custom-title records and its write is unfinished (Native chat-name write row, owned by sync-native-session-titles, not a provider restriction); its wait signal is a hook and needs no runtime"),
            Open("unverified: a standalone or embedded Codex client (--no-daemon, remote) exposes no runtime Bell Tower reaches, so native-name read and write and runtime work status report " + Sessions.CodexAccessStatus.NoRuntime + " and tracking continues from hooks; absence of an access path is not a proven restriction. Closing evidence: a supported external endpoint for that mode, or a documented statement that none exists")),
        new("Native name and runtime status: shared conversations not yet reached",
            Never("Claude Code has no shared backend, so there is no such conversation"),
            Open("unverified (unfinished, user scope decision 2026-11-13, not a provider restriction): a shared conversation adopted after its launching client exited has an unknown mode and gets no shared access, and a conversation of another Codex home is still read through the tray-home backend, which does not host it: the backend's answer is unverified and expected to surface as a shared-backend refusal, and no name is written unless the read succeeds for the exact thread id (CodexSharedRuntimeServiceTests.AConversationTheTrayHomeBackendDoesNotHostIsRefusedAndNeverWritten pins a rejecting stand-in only); the adopted-late conversation reports no access; both keep hook tracking. Closing evidence: a separate change that adds a client-endpoint binder and a per-conversation home record")),
        new("Native name and runtime status: refused shared threads and backends",
            Never("Claude Code has no shared backend or runtime thread to refuse"),
            Open("unverified: fork, parent or delegated threads are refused by a conservative policy (" + Sessions.CodexAccessStatus.Unverified + ") because which session id their hooks carry is unobserved (task 6.1 probe); an elevated shared backend is refused with its own reason by the non-elevated scope decision, not by a provider restriction. Closing evidence: the 6.1 fork and delegated-thread hook probe, or an explicit scope decision for elevated backends")),
        new("Observer read content handling",
            Never("Claude Code has no observer runtime read; transcript history recovery is the separate history rows"),
            Yes("an observer read keeps only the thread id check, the name and the work status flags; previews, turn and message contents and tokens in a response are discarded and never reach the local log, stored identity, attachment or session state, or status text (CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens)")),
        new("Transcript read content handling",
            Yes("the history reader (native custom title and work association) keeps only the verified title, work association and call ids; prompt, assistant and tool-result text never reach the data directory, serialized identity or status text (IdentityHistoryTests.TranscriptReadsKeepNoPromptAssistantOrToolContent, disposable transcript canaries)"),
            Yes("the same history reader over a disposable rollout keeps only the verified work association and call ids; prompt, assistant and tool output text never reach the data directory, serialized identity or status text (IdentityHistoryTests.TranscriptReadsKeepNoPromptAssistantOrToolContent, disposable rollout canaries)")),
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
        new("Terminal navigation: adopted direct sessions (plain claude, codex --no-daemon)",
            Yes("an adopted direct client (verified agent process and shell, no wrapper) is bound from process ancestry and host registration. Windows Terminal: window only while the tray setting \"Title and color native sessions\" is off, the session's tab (selected through its managed title) while it is on; VS Code: the registered terminal through the bridge, partial states reported as terminal only. A click that ends below an exact tab or terminal also shows a balloon with the reason; fixture-verified, live trial 6.1 open. doctor nativeSessions reports the setting state and both precisions"),
            Yes("the same shared policy for a standalone client (--no-daemon, embedded runtime): window only with the setting off, the tab with it on in Windows Terminal; the registered terminal in VS Code; the same click balloon for a partial result; fixture-verified, live trial 6.1 open")),
        new("Title and color: adopted direct sessions (plain claude, codex --no-daemon)",
            Yes("supported behind the off-by-default tray setting \"Title and color native sessions\": the session title and its identity palette background are written through the isolated helper after the first accepted hook (not before startup, so no pre-launch coloring for native sessions); exit, switch-off or loss of the verified agent or shell resets the colors to the terminal default with OSC 110/111 and gives back the prior title, only on the recorded shell (process identifier plus creation time); the prior color is never read. The reset behavior in Windows Terminal and the VS Code terminal is assumed until the 6.1 trial, not verified. Fixture-verified (NativePresentationTests)"),
            Yes("identical for a --no-daemon or embedded Codex client, with the same single setting and the same reset; fixture-verified (NativePresentationTests) with the reset assumed for both hosts until the 6.1 trial")),
        new("Plain default codex (shared backend): tracking, alerts, name sync and permission status",
            Never("Claude Code has no shared backend, so there is no plain default shared-backend session"),
            Yes("hook tracking and completion, question and permission alerts; native name read and write and the owning-or-shared backend's waitingOnApproval status as a non-owning observer of the resolved Codex home (CodexSharedRuntimeServiceTests.ObserverReadsDiscardPreviewsTurnsAndTokens); the shared backend confirms a human wait alongside the owning runtime")),
        new("Plain default codex (shared backend): navigation, title and color",
            Never("Claude Code has no shared backend, so there is no plain default shared-backend session"),
            Open("unfinished: user scope decision 2026-11-13, not a provider restriction. A shared-backend conversation has no verified agent process or shell of its own, so a click reports terminal-unverified, the tab title and background are never written, and the native-presentation setting does not apply. Supported routes today: the Codex profile (Start-Codex, a dedicated runtime) and codex --no-daemon. Closing evidence: a separate change with a documented or reproducibly probed conversation-to-client binder")),
        new("Human-wait confirmation (permission versus automatic approval)",
            Yes("PermissionRequest is tracked as provisional and is never attention alone; Notification permission_prompt confirms it and is eligible on arrival because the shared five-second grace credits the provider's own delay (about 6.2 s observed; Claude documents about 6 s); timing-only evidence is unverified and never alerts, except that a session in which no permission_prompt has been observed (the provider can turn these notifications off, for example with CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS) shows the same silent, visibly unverified \"possibly waiting for approval\" chip after 15 seconds; fixture-verified at 2.1.310 in auto and default modes; user-operated native trials passed Claude's human and automatic approval scenarios on 2026-10-27"),
            Yes("PermissionRequest is provisional; the owning runtime's waitingOnApproval status, or for a shared-backend conversation the shared backend's status read on the observer path, confirms it with the same shared policy and a five-second grace from the first runtime report; a fresh negative status suppresses a stale request; fixture and fake-runtime verified; user-operated native trials passed managed human/automatic, --no-daemon fallback and mixed-provider scenarios on 2026-10-27")),
        new("Human-wait confirmation without an owning runtime",
            Yes("the confirming signal is a hook, so no runtime is needed"),
            Open("unverified: Codex sessions with neither a verified owning runtime nor a verified shared backend (explicit --no-daemon, remote sessions, shared conversations not yet reached, by the 2026-11-13 user scope decision) have no human-wait signal in the 0.164.0 hooks (PermissionRequest runs before guardian or user review: openai/codex codex-rs hooks permission_request event), so their approval requests are never confirmed. A shared-backend conversation reaches the runtime status over the observer path. Supported unverified fallback (shared Core policy, applied to any session without a signal): a request pending 15 seconds shows a visibly unverified \"possibly waiting for approval\" chip with no sound and no reminder or escalation eligibility, cleared by the correlated resolution, stop or end; doctor reports these sessions as unavailable. Closing evidence for a confirmed wait: a supported hook or notification payload that distinguishes a human prompt, or a verified attach path to the owning runtime")),
        new("Question-tool approval flows",
            Yes("AskUserQuestion runs through the approval flow (PermissionRequest without tool_use_id, then permission_prompt about 6.2 s later) in auto and default modes at 2.1.310; both stay confirming evidence for the one question wait, never a permission wait; a question plus an uncorrelated permission makes an unattributed prompt ambiguous and it is never credited to the permission later by elimination (an isolated capture showed the question's own dialog emits that prompt and a following permission dialog is sequential and relies on its own prompt); a prompt cannot confirm a request younger than the documented notification delay (about 6 seconds, floor 5)"),
            Yes("request_user_input emits PreToolUse and a correlated PostToolUse with no approval hook in Plan mode and in Default mode with default_mode_request_user_input; a Default-mode rejection clears at Stop; the owning runtime's waitingOnUserInput status (probe-verified) is read as confirmation evidence and never gates a question; request_user_input_async is not offered by 0.164.0 (hook shape unverified)")),
        new("Setup, configuration, diagnostics and tray controls",
            Yes("setup --clients, --status-line and --desktop; doctor; list; Sessions controls"),
            Yes("identical commands and controls; reversible config.toml footer fields")),
    ];
}

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
        new("Setup, configuration, diagnostics and tray controls",
            Yes("setup --clients, --status-line and --desktop; doctor; list; Sessions controls"),
            Yes("identical commands and controls; reversible config.toml footer fields")),
    ];
}

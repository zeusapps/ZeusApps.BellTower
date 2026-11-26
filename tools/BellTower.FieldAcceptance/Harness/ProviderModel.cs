using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.FieldAcceptance.Isolation;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// How one provider behaves as the harness models it: where its hook settings live, which owned hook command they hold, which
/// shell runs a hook, what event payload it sends and what counts as an accepted capture. Scenarios ask the model and keep
/// provider-name branches to a minimum, so adding a scenario does not fork the provider policy.
/// </summary>
/// <param name="Name">managed, plain or shared.</param>
/// <param name="HooksLostAcrossUpgrade">Documented limitation: the conversation 0.1.9 registered is not followed once its hooks run the candidate ingress.</param>
/// <param name="HooksLostAcrossRollback">Documented limitation: a rolled-back 0.1.9 does not follow the candidate-image hooks of the conversation (ANALYSIS-240).</param>
/// <param name="OwnedRuntime">The hook runs under a Bell Tower owned <c>codex app-server</c> runtime whose manager and shell a runtime record names.</param>
public sealed record LaunchKind(string Name, IReadOnlyList<string> Arguments, bool ManagedAttachment, bool Dedicated, string Meaning,
    bool HooksLostAcrossUpgrade = false, bool HooksLostAcrossRollback = false, bool OwnedRuntime = false);

public abstract class ProviderModel
{
    public static ProviderModel Claude { get; } = new ClaudeModel();
    public static ProviderModel Codex { get; } = new CodexModel();
    public static IReadOnlyList<ProviderModel> All { get; } = [Claude, Codex];

    /// <summary>The captured native payloads the payload builder starts from (copied next to the harness).</summary>
    public static string DefaultFixtureDirectory => Path.Combine(AppContext.BaseDirectory, "FieldAcceptanceFixtures", "native");

    public abstract string Name { get; }
    public abstract string SettingsPath(InstanceLayout layout);

    /// <summary>The arguments the fake provider process is started with (a plain native Codex start is <c>--no-daemon</c>).</summary>
    public abstract IReadOnlyList<string> AgentArguments { get; }

    /// <summary>True when a session started with these arguments uses a backend shared with the user's real installation (a plain Codex without <c>--no-daemon</c>).</summary>
    public abstract bool UsesSharedBackend(IReadOnlyList<string> arguments);

    /// <summary>
    /// True when a startup whose hook captured no agent process (so its mode cannot be classified) fails closed to the shared runtime, because the
    /// provider may share a backend (<c>AgentModeClassifier.MayShareBackend</c>, <c>NativeAdoption.Apply</c>): a later hook from the identifiable
    /// process then cannot repair it and the conversation reads unavailable after the grace. Otherwise the later hook repairs it (Claude Code, which
    /// has no shared backend, treats an unclassified startup as direct).
    /// </summary>
    public abstract bool UnclassifiedStartupFailsClosed { get; }

    /// <summary>
    /// True when a plain launch whose hook finds no agent process (and carries no attachment id) gets a fresh random attachment id for every hook
    /// (<c>Hosts/AttachmentCapture.cs:110-111</c>; the same in 0.1.9): Claude Code keeps it, so its later hooks never match the session. Codex's unclassified
    /// capture fails closed to the stable shared id (<c>NativeAdoption.Shared</c>).
    /// </summary>
    public bool NullProcessPlainStartupGetsFreshIds => !UnclassifiedStartupFailsClosed;

    /// <summary>
    /// The real ways a user reaches this provider, as the hook sees them: the process arguments and whether Bell Tower's own launch set the
    /// attachment id (<c>BELLTOWER_ATTACHMENT</c>) and the dedicated-runtime marker (<c>BELLTOWER_DEDICATED=1</c>). Sources: <c>BellTower.psm1:40,52-58,97-106</c>
    /// and <c>AgentLauncher.cs:32-33</c>. A Bell Tower Codex launch sets the attachment id and the marker; with a user <c>--no-daemon</c> it runs standalone,
    /// without it (the default Codex profile) the launch prepares an owned <c>app-server</c> runtime and the hooks run under that backend with the same
    /// variables (the <c>owned-runtime</c> kind). <c>AgentModeClassifier</c>: a hook run by a <c>codex app-server</c> backend is the shared backend.
    /// The first kind is the Bell Tower managed launch.
    /// </summary>
    /// <remarks>
    /// <see cref="LaunchKind.HooksLostAcrossUpgrade"/> is the documented 0.1.11 limitation (user decision 2026-11-26): a plain <c>codex --no-daemon</c>
    /// conversation that 0.1.9 registered under its shared id is not followed once its hooks run the candidate ingress (which emits the process id) until it
    /// registers again, in the candidate and in a rolled-back 0.1.9 alike. <see cref="LaunchKind.HooksLostAcrossRollback"/> (owned runtime, ANALYSIS-240) is
    /// a rollback-only loss pending a user decision. Recovery: rerun setup, then resume or restart that conversation.
    /// </remarks>
    public abstract IReadOnlyList<LaunchKind> LaunchKinds { get; }

    /// <summary>
    /// A neutral native no-op through the provider's shell: the shell starts a native no-op and prints 1 (informational only, never the contract
    /// baseline, which is <c>echo 1</c>). PowerShell resolves <c>echo</c> to <c>Write-Output</c>, which auto-loads a module, so the contract
    /// control is not neutral under PowerShell; this command is.
    /// </summary>
    public abstract string NeutralCommand { get; }

    /// <summary>The shell the provider's hook runner uses for the command currently installed.</summary>
    public abstract ResolvedShell RuntimeShell(Instance instance);

    /// <summary>The shell used to run a hook command in the 0.1.9 command form where that form needs a different shell than the runtime one (a modeled fallback).</summary>
    public abstract ResolvedShell? ModeledFallbackShell(Instance instance);

    public static ProviderModel For(string name) => All.Single(model => model.Name == name);

    /// <summary>The first owned (ingress) hook command the provider's settings file holds for an event, or null.</summary>
    public string? OwnedCommand(InstanceLayout layout, string hookEvent)
    {
        var path = SettingsPath(layout);
        if (!File.Exists(path))
        {
            return null;
        }
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("hooks", out var hooks) || !hooks.TryGetProperty(hookEvent, out var groups))
        {
            return null;
        }
        foreach (var group in groups.EnumerateArray())
        {
            foreach (var hook in group.GetProperty("hooks").EnumerateArray())
            {
                if (hook.TryGetProperty("command", out var command) && command.GetString() is { } text &&
                    text.Contains("bell-tower-ingress", StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The event payload: the field set of the provider's native capture (<c>tests/BellTower.Tests/Fixtures/native</c>) with the
    /// session id, working directory and transcript path replaced by sandbox values. No field the native capture lacks is added.
    /// </summary>
    public string Payload(string hookEvent, string sessionId, string directory, InstanceLayout layout, string? fixtureDirectory = null, string? source = null)
    {
        var pattern = Name + "-*-" + hookEvent.ToLowerInvariant() + ".json";
        var file = Directory.EnumerateFiles(fixtureDirectory ?? DefaultFixtureDirectory, pattern).OrderBy(path => path, StringComparer.Ordinal).LastOrDefault()
            ?? throw new FileNotFoundException("No native fixture matches " + pattern + ".");
        var payload = JsonNode.Parse(File.ReadAllText(file))!["payload"]!.AsObject();
        payload["session_id"] = sessionId;
        payload["cwd"] = directory;
        payload["transcript_path"] = TranscriptPath(layout, sessionId);
        // Only a field the native capture has can be changed (a SessionStart carries its start source, for example "resume").
        if (source is not null && payload.ContainsKey("source"))
        {
            payload["source"] = source;
        }
        return payload.ToJsonString();
    }

    public static string TranscriptPath(InstanceLayout layout, string sessionId) => Path.Combine(layout.Root, "transcripts", sessionId + ".jsonl");

    /// <summary>
    /// A PostToolUse event for a successful shell command. A MODEL: the repository holds no native PostToolUse capture for either client
    /// (only the SessionStart and UserPromptSubmit captures above), so this follows the documented hook schema (session, transcript, cwd,
    /// event, tool name, tool use id, tool input and response) and is never described as a native capture.
    /// </summary>
    public string ShellToolPayload(string sessionId, string directory, InstanceLayout layout, string toolUseId, string command)
    {
        var payload = new JsonObject
        {
            ["session_id"] = sessionId,
            ["transcript_path"] = TranscriptPath(layout, sessionId),
            ["cwd"] = directory,
            ["hook_event_name"] = "PostToolUse",
            ["tool_name"] = "Bash",
            ["tool_use_id"] = toolUseId,
            ["tool_input"] = new JsonObject { ["command"] = command },
            ["tool_response"] = new JsonObject { ["stdout"] = "{\"changeName\":\"inspected\"}", ["stderr"] = "", ["interrupted"] = false }
        };
        return payload.ToJsonString();
    }

    /// <summary>
    /// A tool hook of the approval flow: <c>PreToolUse</c> and <c>PostToolUse</c> carry the tool use id, while a <c>PermissionRequest</c> carries none
    /// (both providers' documented and captured shape: it correlates with its operation by tool name and command). A MODEL, like
    /// <see cref="ShellToolPayload"/>: the repository holds a native <c>PermissionRequest</c> capture per provider, but no native tool-flow sequence for
    /// the harness's shell commands, so nothing here is described as a native capture. No <c>turn_id</c> is set, so the event is never filtered by turn.
    /// </summary>
    public string ToolPayload(string hookEvent, string sessionId, string directory, InstanceLayout layout, string command, string? toolUseId)
    {
        var payload = new JsonObject
        {
            ["session_id"] = sessionId,
            ["transcript_path"] = TranscriptPath(layout, sessionId),
            ["cwd"] = directory,
            ["hook_event_name"] = hookEvent,
            ["permission_mode"] = "default",
            ["tool_name"] = "Bash"
        };
        if (hookEvent != "PermissionRequest")
        {
            payload["tool_use_id"] = toolUseId ?? throw new ArgumentException("A " + hookEvent + " event carries a tool use id.");
        }
        payload["tool_input"] = new JsonObject { ["command"] = command };
        if (hookEvent == "PostToolUse")
        {
            payload["tool_response"] = new JsonObject { ["stdout"] = "", ["stderr"] = "", ["interrupted"] = false };
        }
        return payload.ToJsonString();
    }

    /// <summary>
    /// The modeled transcript records of one successful historical write into a change folder of <paramref name="worktree"/>, in the provider's
    /// own record shapes (see <see cref="TranscriptModel"/>). <paramref name="nativeTitle"/> adds the provider's native custom-title record where
    /// the provider has one the product reads (Claude Code); otherwise it is ignored.
    /// </summary>
    public abstract IEnumerable<string> HistoryRecords(string sessionId, string toolKey, string worktree, string change, DateTimeOffset at, string? nativeTitle);

    /// <summary>A capture is accepted when the hook exits 0 with <c>{}</c> on standard output and nothing on standard error.</summary>
    public bool Accepted(ProcessResult result) => result.Succeeded && result.Output.Trim() == "{}" && result.Error.Length == 0;

    private sealed class ClaudeModel : ProviderModel
    {
        public override string Name => "claude";
        public override string SettingsPath(InstanceLayout layout) => layout.ClaudeSettings;
        public override IReadOnlyList<string> AgentArguments => [];
        public override bool UsesSharedBackend(IReadOnlyList<string> arguments) => false;
        public override bool UnclassifiedStartupFailsClosed => false;
        public override IReadOnlyList<LaunchKind> LaunchKinds { get; } =
        [
            new("managed", [], true, false, "claude started by a Bell Tower launch (the launch exports the attachment id)"),
            new("plain", [], false, false, "plain claude started in a terminal (no Bell Tower environment)")
        ];

        // Git Bash starts a native (MSYS) printf image instead of its builtin.
        public override string NeutralCommand => "/usr/bin/printf 1";

        // Claude Code runs hooks through Git Bash on Windows (archived native evidence for 2.1.310).
        public override ResolvedShell RuntimeShell(Instance instance) => new(HookShell.GitBash, instance.Tools.GitBash);
        public override ResolvedShell? ModeledFallbackShell(Instance instance) => null;

        public override IEnumerable<string> HistoryRecords(string sessionId, string toolKey, string worktree, string change, DateTimeOffset at, string? nativeTitle)
        {
            yield return TranscriptModel.ClaudeWriteUse(sessionId, "toolu_" + toolKey, worktree, change, TranscriptModel.Timestamp(at));
            yield return TranscriptModel.ClaudeResult(sessionId, "toolu_" + toolKey, TranscriptModel.Timestamp(at.AddSeconds(5)));
            if (nativeTitle is not null)
            {
                yield return TranscriptModel.ClaudeCustomTitle(sessionId, nativeTitle);
            }
        }
    }

    private sealed class CodexModel : ProviderModel
    {
        public override string Name => "codex";
        public override string SettingsPath(InstanceLayout layout) => layout.CodexHooks;
        public override IReadOnlyList<string> AgentArguments => ["--no-daemon"];
        public override bool UsesSharedBackend(IReadOnlyList<string> arguments) => !arguments.Contains("--no-daemon");
        public override bool UnclassifiedStartupFailsClosed => true;
        public override IReadOnlyList<LaunchKind> LaunchKinds { get; } =
        [
            new("managed", ["--no-daemon"], true, true, "codex started by a Bell Tower launch with a user --no-daemon (standalone: --no-daemon, the attachment id and BELLTOWER_DEDICATED=1)"),
            new("owned-runtime", ["app-server", "--listen", "ws://127.0.0.1:1"], true, true,
                "codex started by the default Bell Tower launch (an owned app-server runtime under a manager; the hook runs under that backend with the attachment id and BELLTOWER_DEDICATED=1)", HooksLostAcrossRollback: true, OwnedRuntime: true),
            new("plain", ["--no-daemon"], false, false, "plain codex --no-daemon (no Bell Tower environment)", HooksLostAcrossUpgrade: true),
            new("shared", ["app-server"], false, false, "plain shared-backend codex (the hook runs under the shared codex app-server backend; no Bell Tower environment)")
        ];

        // PowerShell calls one .NET method: no cmdlet resolution, no module auto-load.
        public override string NeutralCommand => "[Console]::Out.Write('1')";

        // Codex 0.164.0 discovery order (see CodexShell): this is the shell it really uses, whatever command form is installed.
        public override ResolvedShell RuntimeShell(Instance instance) =>
            CodexShell.Resolve(instance.Environment.GetValueOrDefault("PATH"), instance.Environment.GetValueOrDefault("ProgramFiles") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.SystemDirectory);

        // The 0.1.9 command only parses in the command shell: running it there models a Codex whose PowerShell discovery failed.
        public override ResolvedShell? ModeledFallbackShell(Instance instance) => new(HookShell.Cmd, instance.Tools.Cmd);

        public override IEnumerable<string> HistoryRecords(string sessionId, string toolKey, string worktree, string change, DateTimeOffset at, string? nativeTitle)
        {
            yield return TranscriptModel.CodexPatchUse("call_" + toolKey, worktree, change, TranscriptModel.Timestamp(at));
            yield return TranscriptModel.CodexPatchResult("call_" + toolKey, TranscriptModel.Timestamp(at.AddSeconds(5)));
        }
    }
}

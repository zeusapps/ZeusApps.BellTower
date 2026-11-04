using System.Diagnostics;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// Native adoption classifies the hook's agent process with one shared policy for Claude Code and Codex. Fixtures only:
// ancestry and command-line shapes come from the retained native probes, with no provider launch.
public sealed class NativeAdoptionTests
{
    public static TheoryData<string, string[]?, string> Modes => new()
    {
        { "claude", [], AgentModes.InteractiveClient },
        { "claude", ["--resume", "abc"], AgentModes.InteractiveClient },
        { "claude", ["-p", "prompt"], AgentModes.Headless },
        { "claude", ["--print", "--output-format", "json"], AgentModes.Headless },
        { "claude", null, AgentModes.Unknown },
        { "codex", [], AgentModes.EmbeddedRuntime },
        { "codex", ["--no-daemon"], AgentModes.EmbeddedRuntime },
        { "codex", ["resume", "id"], AgentModes.EmbeddedRuntime },
        { "codex", ["-m", "exec"], AgentModes.EmbeddedRuntime },
        { "codex", ["app-server", "--managed-daemon"], AgentModes.SharedBackend },
        { "codex", ["-c", "x=y", "app-server"], AgentModes.SharedBackend },
        { "codex", ["exec", "--json", "--sandbox", "read-only"], AgentModes.Headless },
        { "codex", ["review"], AgentModes.Unknown },
        { "codex", null, AgentModes.Unknown },
        // Fail-closed drift rows: a new option before app-server, an unknown option, and every non-session subcommand.
        { "codex", ["--future-option", "value", "app-server"], AgentModes.SharedBackend },
        { "codex", ["--future-option", "app-server", "daemon"], AgentModes.SharedBackend },
        { "codex", ["--future-option", "value"], AgentModes.Unknown },
        { "codex", ["--future-option=1", "resume"], AgentModes.Unknown },
        { "codex", ["--no-daemon", "--search", "do the thing"], AgentModes.EmbeddedRuntime },
        { "codex", ["--model=gpt", "fork"], AgentModes.EmbeddedRuntime },
        { "codex", ["do", "--", "app-server"], AgentModes.EmbeddedRuntime },
        { "codex", ["mcp-server"], AgentModes.Unknown },
        { "codex", ["exec-server"], AgentModes.Unknown },
        { "codex", ["execpolicy"], AgentModes.Unknown },
        { "codex", ["login"], AgentModes.Unknown },
        { "codex", ["logout"], AgentModes.Unknown },
        { "codex", ["apply"], AgentModes.Unknown },
        { "codex", ["cloud"], AgentModes.Unknown },
        { "codex", ["sandbox", "windows"], AgentModes.Unknown },
        { "claude", ["--future-option", "value"], AgentModes.InteractiveClient },
        { "claude", ["--", "-p"], AgentModes.InteractiveClient }
    };

    [Theory]
    [MemberData(nameof(Modes))]
    public void ModeIsClassifiedFromVerifiedProcessEvidence(string provider, string[]? arguments, string expected) =>
        Assert.Equal(expected, AgentModeClassifier.Classify(provider, true, arguments));

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AMissingAgentProcessIsNeverClassified(string provider) =>
        Assert.Equal(AgentModes.Unknown, AgentModeClassifier.Classify(provider, false, []));

    [Fact]
    public void CommandLinesSplitWithWindowsQuotingAndDropTheExecutable()
    {
        Assert.Equal(["app-server", "--managed-daemon"], AgentModeClassifier.Arguments("\"C:\\Program Files\\codex\\codex.exe\" app-server --managed-daemon"));
        Assert.Equal(["exec", "two words", "a\"b", "c\\d"], AgentModeClassifier.Arguments("codex.exe exec \"two words\" a\\\"b c\\d"));
        Assert.Empty(AgentModeClassifier.Arguments("codex.exe"));
    }

    [Fact]
    public void ACurrentProcessCommandLineIsReadableAndAnInvalidOneIsNot()
    {
        var text = ProcessProbe.CommandLine(Environment.ProcessId);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Null(ProcessProbe.CommandLine(int.MaxValue));
    }

    internal static Attachment Captured(string provider, ProcessIdentity? agent, string id = "launch-attachment", string ownership = "observed",
        ProcessIdentity? shell = null, string? shellSource = null) => new(id, provider, agent,
        new("windows-terminal", shell, null, 0, "terminal-session") { ShellSource = shellSource }, "D:/example", DateTimeOffset.UtcNow,
        ManagedTitle: true, ManagedBackground: true)
        {
            Ownership = ownership
        };

    [Theory]
    [InlineData("claude", AgentModes.InteractiveClient)]
    [InlineData("codex", AgentModes.EmbeddedRuntime)]
    [InlineData("claude", AgentModes.Unknown)]
    public void DirectClientsKeepTheirProcessAndHostWithoutAnyWrapperFlag(string provider, string mode)
    {
        var agent = new ProcessIdentity(100, 200);
        var outcome = NativeAdoption.Apply(Captured(provider, agent, "provider-100-200"), "session", mode, AgentModeClassifier.MayShareBackend(provider));
        Assert.True(outcome.Dedicated);
        Assert.Equal(agent, outcome.Attachment.Process);
        Assert.Equal("windows-terminal", outcome.Attachment.Host.Kind);
        Assert.Equal("provider-100-200", outcome.Attachment.Id);
        var evidence = outcome.Attachment.Evidence!;
        Assert.Equal(mode, evidence.Mode);
        Assert.Equal(LifecycleOwners.AgentProcess, evidence.LifecycleOwner);
        Assert.Equal(EvidenceStatus.Unverified, evidence.TerminalBinding.Binding);
        Assert.Equal(EvidenceStatus.None, evidence.RuntimeAccess.Access);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void HeadlessRunsAreObservedWithoutClaimingATerminal(string provider)
    {
        var outcome = NativeAdoption.Apply(Captured(provider, new(100, 200)), "session", AgentModes.Headless, AgentModeClassifier.MayShareBackend(provider));
        Assert.True(outcome.Dedicated);
        Assert.Equal(new ProcessIdentity(100, 200), outcome.Attachment.Process);
        Assert.Equal("unknown", outcome.Attachment.Host.Kind);
        Assert.Null(outcome.Attachment.Host.Shell);
        Assert.False(outcome.Attachment.ManagedTitle);
        Assert.False(outcome.Attachment.ManagedBackground);
        Assert.Equal(EvidenceStatus.None, outcome.Attachment.Evidence!.TerminalBinding.Binding);
    }

    [Theory]
    [InlineData("claude", AgentModes.SharedBackend)]
    [InlineData("codex", AgentModes.SharedBackend)]
    [InlineData("codex", AgentModes.Unknown)]
    public void AnythingNotProvenDirectFailsClosedToTheSharedRuntime(string provider, string mode)
    {
        var shell = new ProcessIdentity(101, 201);
        var outcome = NativeAdoption.Apply(Captured(provider, new(100, 200), "inherited-launch", mode == AgentModes.Unknown ? "observed" : "managed", shell), "session", mode, AgentModeClassifier.MayShareBackend(provider));
        Assert.False(outcome.Dedicated);
        Assert.Equal(provider + "-session-" + BellTower.IO.ContentHash.Compute("session")[..24], outcome.Attachment.Id);
        Assert.Null(outcome.Attachment.Process);
        Assert.Equal("unknown", outcome.Attachment.Host.Kind);
        Assert.Null(outcome.Attachment.Host.Shell);
        Assert.Equal("shared-runtime", outcome.Attachment.Ownership);
        Assert.False(outcome.Attachment.ManagedTitle);
        Assert.Equal(LifecycleOwners.SharedBackend, outcome.Attachment.Evidence!.LifecycleOwner);
        Assert.Throws<InvalidDataException>(() => NativeAdoption.Apply(Captured(provider, null), null, mode, AgentModeClassifier.MayShareBackend(provider)));
    }

    internal static Process Sleeper() => Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 60 127.0.0.1 >nul")
    {
        CreateNoWindow = true,
        UseShellExecute = false
    })!;

    internal const string Start = """{"session_id":"nested-session","cwd":"D:/example","hook_event_name":"SessionStart","source":"startup"}""";
    internal const string Prompt = """{"session_id":"nested-session","cwd":"D:/example","hook_event_name":"UserPromptSubmit","prompt":"private"}""";

    internal static void Capture(TestWorkspace workspace, string provider, string json, NativeAdoption.Outcome outcome, string? name = null, string? change = null)
    {
        using var document = JsonDocument.Parse(json);
        var envelope = CaptureFactory.Create(provider, document.RootElement, outcome.Attachment, DateTimeOffset.UtcNow, null, null, outcome.Dedicated, name, change)!;
        CaptureSpool.Write(workspace.Paths.Root, envelope);
    }

    // ANALYSIS-500: the shared backend's ancestry and environment belong to its first launcher. A BELLTOWER_* flag or
    // attachment identifier inherited by a nested client must not move the conversation onto that launcher's terminal.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ASharedBackendHookWithInheritedLaunchMetadataNeverTakesTheLaunchersTerminal(string provider)
    {
        using var workspace = new TestWorkspace();
        using var launcherAgent = Sleeper();
        var launcher = ProcessProbe.Identify(launcherAgent.Id)!;
        var shell = ProcessProbe.Identify(Environment.ProcessId)!;
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(Captured(provider, launcher, "launch-attachment", "managed", shell) with { Evidence = AttachmentEvidence.ForManagedLaunch() });
        var launchFile = Path.Combine(workspace.Paths.Attachments, BellTower.IO.ContentHash.Compute("launch-attachment") + ".json");
        var before = File.ReadAllBytes(launchFile);

        // The backend hook sees the launcher's identifier, shell and flag.
        var inherited = NativeAdoption.Apply(Captured(provider, launcher, "launch-attachment", "managed", shell), "nested-session", AgentModes.SharedBackend, AgentModeClassifier.MayShareBackend(provider));
        Capture(workspace, provider, Start, inherited);
        var state = new StateStore(workspace.Paths).Drain();

        var session = state.Sessions[provider + ":nested-session"];
        Assert.StartsWith(provider + "-session-", session.AttachmentId);
        var shared = attachments.Get(session.AttachmentId)!;
        Assert.Null(shared.Process);
        Assert.Null(shared.Host.Shell);
        Assert.Equal("shared-runtime", shared.Ownership);
        Assert.Equal(LifecycleOwners.SharedBackend, shared.Evidence!.LifecycleOwner);
        Assert.Equal(EvidenceStatus.HookObserved, shared.Evidence.ConversationHealth);
        Assert.Equal(before, File.ReadAllBytes(launchFile));
        Assert.Equal(launcher, attachments.Get("launch-attachment")!.Process);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void TheLegacyIngestionPathAppliesTheSamePolicyWithoutAWrapperFlag(string provider)
    {
        using var workspace = new TestWorkspace();
        using var agent = Sleeper();
        var identity = ProcessProbe.Identify(agent.Id)!;
        var direct = $"{provider}-{identity.Pid}-{identity.StartedUtcTicks}";
        new HookIngestor(workspace.Paths, (name, directory, session) =>
            NativeAdoption.Apply(Captured(name, identity, direct), session, AgentModes.EmbeddedRuntime, AgentModeClassifier.MayShareBackend(provider))).Ingest(provider, new StringReader(Start));
        var attached = new AttachmentStore(workspace.Paths).Get(direct)!;
        Assert.Equal(identity, attached.Process);
        Assert.Equal(AgentModes.EmbeddedRuntime, attached.Evidence!.Mode);
        Assert.Equal(EvidenceStatus.HookObserved, attached.Evidence.ConversationHealth);

        new HookIngestor(workspace.Paths, (name, directory, session) =>
            NativeAdoption.Apply(Captured(name, identity, "ignored-managed-id", "managed"), session, AgentModes.SharedBackend, AgentModeClassifier.MayShareBackend(provider))).Ingest(provider, new StringReader(Prompt));
        var shared = new AttachmentStore(workspace.Paths).Get(NativeAdoption.SharedId(provider, "nested-session"))!;
        Assert.Null(shared.Process);
        Assert.Null(shared.Host.Shell);
        Assert.Equal("shared-runtime", shared.Ownership);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ALaterEventRecoversAMissedSessionStartWithoutInventingAttention(string provider)
    {
        using var workspace = new TestWorkspace();
        var outcome = NativeAdoption.Apply(Captured(provider, null, "recovered", "observed"), "nested-session", AgentModes.SharedBackend, AgentModeClassifier.MayShareBackend(provider));
        Capture(workspace, provider, Prompt, outcome);
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":nested-session"];
        Assert.False(session.Ended);
        Assert.Empty(session.Waits);
        Assert.Equal(EvidenceStatus.HookObserved, new AttachmentStore(workspace.Paths).Get(session.AttachmentId)!.Evidence!.ConversationHealth);
    }
}

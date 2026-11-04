using System.Diagnostics;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// Attachment evidence separates conversation health, runtime access, terminal binding and lifecycle ownership.
// Each scenario runs for Claude Code and Codex because the records and merge rules are shared.
public sealed class AttachmentEvidenceTests
{
    private static Attachment Capture(string provider, string id, ProcessIdentity? process = null, AttachmentEvidence? evidence = null, string ownership = "observed") =>
        new(id, provider, process, new("windows-terminal", null, null, 0, "session"), "D:/example", DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            Ownership = ownership,
            Evidence = evidence?.Stamped(process)
        };

    private static string FilePath(TestWorkspace workspace, string id) => Path.Combine(workspace.Paths.Attachments, ContentHash.Compute(id) + ".json");

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void RecordsWrittenBeforeEvidenceExistedReadWithConservativeDerivedFacets(string provider)
    {
        using var workspace = new TestWorkspace();
        const string legacy = """
            {"id":"legacy","provider":"PROVIDER","process":{"pid":12,"startedUtcTicks":34},"host":{"kind":"vscode"},"directory":"D:/example",
             "registeredAt":"2026-10-01T00:00:00+00:00","health":"verified-live","ownership":"OWNER","futureField":{"kept":true}}
            """;
        foreach (var (owner, expected) in new[] { ("managed", LifecycleOwners.BellTower), ("shared-runtime", LifecycleOwners.SharedBackend),
            ("observed", LifecycleOwners.AgentProcess), ("unverified", LifecycleOwners.Unknown) })
        {
            var attachment = Json.Decode<Attachment>(legacy.Replace("PROVIDER", provider).Replace("OWNER", owner));
            Assert.Null(attachment.Evidence);
            var evidence = AttachmentEvidence.Of(attachment);
            Assert.Equal(expected, evidence.LifecycleOwner);
            Assert.Equal(AgentModes.Unknown, evidence.Mode);
            Assert.Equal(EvidenceStatus.Unverified, evidence.ConversationHealth);
            Assert.Equal(EvidenceStatus.None, evidence.RuntimeAccess.Access);
            Assert.Equal(EvidenceStatus.Unverified, evidence.TerminalBinding.Binding);
            Assert.True(attachment.AdditionalData!.ContainsKey("futureField"));
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void UnknownFieldsAtEveryLevelSurviveAdoptionAndTheEvidenceRoundTrips(string provider)
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow;
        var store = new AttachmentStore(workspace.Paths);
        var stored = Json.Decode<Attachment>("""
            {"id":"kept","provider":"PROVIDER","host":{"kind":"vscode","hostFuture":1},"directory":"D:/example","registeredAt":"REGISTERED",
             "ownership":"observed","topFuture":"x",
             "evidence":{"mode":"future-mode","conversationHealth":"hook-observed","runtime":{"access":"observer","source":"future","runtimeFuture":true},
             "terminal":{"binding":"verified","source":"host-bridge","terminalFuture":2},"lifecycleOwner":"agent-process","evidenceFuture":[1]}}
            """.Replace("PROVIDER", provider).Replace("REGISTERED", at.AddMinutes(-5).ToString("O")));
        store.Save(stored);

        var adopted = store.Adopt(Capture(provider, "kept", evidence: new() { Mode = AgentModes.EmbeddedRuntime, LifecycleOwner = LifecycleOwners.SharedBackend }), at);

        var text = File.ReadAllText(FilePath(workspace, "kept"));
        foreach (var future in new[] { "topFuture", "hostFuture", "runtimeFuture", "terminalFuture", "evidenceFuture" })
        {
            Assert.Contains(future, text);
        }
        var evidence = adopted.Evidence!;
        // An established mode, binding, access and owner are not rewritten by a later hook.
        Assert.Equal("future-mode", evidence.Mode);
        Assert.Equal("verified", evidence.TerminalBinding.Binding);
        Assert.Equal("observer", evidence.RuntimeAccess.Access);
        Assert.Equal(LifecycleOwners.AgentProcess, evidence.LifecycleOwner);
        Assert.Equal(at, evidence.ConversationObservedAt);
        Assert.Equal(evidence.Mode, store.Get("kept")!.Evidence!.Mode);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ALegacyManagedRecordKeepsItsOwnerAndLearnsTheModeFromTheFirstHook(string provider)
    {
        using var workspace = new TestWorkspace();
        var store = new AttachmentStore(workspace.Paths);
        store.Save(Capture(provider, "managed", ownership: "managed"));
        var adopted = store.Adopt(Capture(provider, "managed", evidence: new() { Mode = AgentModes.InteractiveClient, LifecycleOwner = LifecycleOwners.AgentProcess }, ownership: "managed"), DateTimeOffset.UtcNow);
        Assert.Equal(LifecycleOwners.BellTower, adopted.Evidence!.LifecycleOwner);
        Assert.Equal(AgentModes.InteractiveClient, adopted.Evidence.Mode);
        Assert.Equal(EvidenceStatus.HookObserved, adopted.Evidence.ConversationHealth);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void OnlyAnExplicitResumeReplacementAdoptsTheNewGenerationsFacets(string provider)
    {
        using var workspace = new TestWorkspace();
        using var first = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 60 127.0.0.1 >nul") { CreateNoWindow = true, UseShellExecute = false })!;
        using var second = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 60 127.0.0.1 >nul") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            var store = new AttachmentStore(workspace.Paths);
            var oldAgent = ProcessProbe.Identify(first.Id)!;
            var newAgent = ProcessProbe.Identify(second.Id)!;
            var at = DateTimeOffset.UtcNow;
            store.Adopt(Capture(provider, "gen", oldAgent, new() { Mode = AgentModes.InteractiveClient, LifecycleOwner = LifecycleOwners.AgentProcess }), at.AddSeconds(-30));
            var capture = Capture(provider, "gen", newAgent, new() { Mode = AgentModes.EmbeddedRuntime, LifecycleOwner = LifecycleOwners.AgentProcess });

            // A late non-resume hook from another process cannot transfer the attachment or its facets.
            var late = store.Adopt(capture, at.AddSeconds(-10));
            Assert.Equal(oldAgent, late.Process);
            Assert.Equal(AgentModes.InteractiveClient, late.Evidence!.Mode);

            var replaced = store.Adopt(capture, at, resume: true);
            Assert.Equal(newAgent, replaced.Process);
            Assert.Equal(AgentModes.EmbeddedRuntime, replaced.Evidence!.Mode);
        }
        finally
        {
            first.Kill();
            second.Kill();
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AProcessWithoutAMatchingCreationTimeIsNeitherAdoptedNorHealthy(string provider)
    {
        using var workspace = new TestWorkspace();
        var live = ProcessProbe.Identify(Environment.ProcessId)!;
        var stale = live with { StartedUtcTicks = live.StartedUtcTicks + 1 };
        var store = new AttachmentStore(workspace.Paths);
        // A first registration keeps what ingress observed; a later hook never attaches a process that fails the creation-time check.
        store.Save(Capture(provider, "stale"));
        var adopted = store.Adopt(Capture(provider, "stale", stale), DateTimeOffset.UtcNow);
        Assert.Null(adopted.Process);
        var evaluated = new AttachmentHealth().Evaluate(Capture(provider, "stale", stale), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        Assert.Equal("confirmed-ended", evaluated.Health);
        Assert.Equal("verified-live", new AttachmentHealth().Evaluate(Capture(provider, "live", live), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)).Health);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void RecordingEvidenceDoesNotResetIdentityPaletteOrAttentionState(string provider)
    {
        using var workspace = new TestWorkspace();
        var attachment = Capture(provider, "steady");
        var store = new AttachmentStore(workspace.Paths);
        store.Adopt(attachment, DateTimeOffset.UtcNow.AddMinutes(-2));
        var at = DateTimeOffset.UtcNow.AddMinutes(-1);
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission(at) with { Provider = provider, AttachmentId = "steady", SessionId = "steady-session" });
        var key = provider + ":steady-session";
        var identity = new IdentityStore(workspace.Paths).Get(key, "D:/example");
        var palette = identity.Palette;
        var state = new StateStore(workspace.Paths).Drain();
        var waits = state.Sessions[key].Waits.Count;
        Assert.NotEqual(0, waits);
        var identityBytes = File.ReadAllBytes(Directory.GetFiles(workspace.Paths.Identities, "*.json").Single());
        var stateBytes = File.ReadAllBytes(workspace.Paths.State);

        store.Adopt(attachment with { Evidence = new() { Mode = AgentModes.EmbeddedRuntime } }, DateTimeOffset.UtcNow);
        store.Adopt(attachment with { Evidence = new() { Mode = AgentModes.Headless } }, DateTimeOffset.UtcNow, resume: true);

        Assert.Equal(identityBytes, File.ReadAllBytes(Directory.GetFiles(workspace.Paths.Identities, "*.json").Single()));
        Assert.Equal(stateBytes, File.ReadAllBytes(workspace.Paths.State));
        Assert.Equal(palette, new IdentityStore(workspace.Paths).Get(key, "D:/example").Palette);
        Assert.Equal(waits, new StateStore(workspace.Paths).Read().Sessions[key].Waits.Count);
    }

    [Fact]
    public void TheEnvelopeCarriesEvidenceAndAnOlderReaderIgnoresIt()
    {
        var evidence = new AttachmentEvidence { Mode = AgentModes.Headless, LifecycleOwner = LifecycleOwners.AgentProcess };
        using var input = JsonDocument.Parse("""{"session_id":"s","cwd":"D:/example","hook_event_name":"SessionStart","source":"startup"}""");
        var envelope = CaptureFactory.Create("claude", input.RootElement, Capture("claude", "envelope", evidence: evidence), DateTimeOffset.UtcNow)!;
        using var stream = new MemoryStream(CaptureProtocol.Encode(envelope));
        var decoded = CaptureProtocol.Decode(stream);
        Assert.Equal(AgentModes.Headless, decoded.Attachment.Evidence!.Mode);
        // An older reader's attachment record has no evidence property; it keeps the value as extension data.
        var json = JsonSerializer.Serialize(decoded.Attachment, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"evidence\"", json);
        Assert.DoesNotContain("runtimeAccess", json);
        Assert.DoesNotContain("terminalBinding", json);
    }
}

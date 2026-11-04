using System.Text.Json;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;
using static BellTower.Tests.Providers.NativeAdoptionTests;

namespace BellTower.Tests.Providers;

// Inherited launch metadata never transfers presentation, shell or identity text, for Claude Code and Codex alike.
public sealed class NativeAdoptionReconcileTests
{
    [Theory]
    [InlineData("claude", AgentModes.InteractiveClient, "launch-env")]
    [InlineData("codex", AgentModes.EmbeddedRuntime, "launch-env")]
    [InlineData("claude", AgentModes.Headless, "launch-env")]
    [InlineData("codex", AgentModes.Headless, "launch-env")]
    [InlineData("claude", AgentModes.InteractiveClient, "ancestry")]
    [InlineData("codex", AgentModes.EmbeddedRuntime, "ancestry")]
    public void ANestedClientThatInheritedAManagedIdentifierKeepsItsOwnIdentityAndNoLaunchPresentation(string provider, string mode, string shellSource)
    {
        using var workspace = new TestWorkspace();
        using var launcherAgent = DisposableSleeper.Start();
        using var nestedAgent = DisposableSleeper.Start();
        var launcher = ProcessProbe.Identify(launcherAgent.Id)!;
        var nested = ProcessProbe.Identify(nestedAgent.Id)!;
        var shell = ProcessProbe.Identify(Environment.ProcessId)!;
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(Captured(provider, launcher, "launch-attachment", "managed", shell, "launch-env") with { Evidence = AttachmentEvidence.ForManagedLaunch(launcher) });

        var outcome = NativeAdoption.Apply(Captured(provider, nested, "launch-attachment", "managed", shell, shellSource), "nested-session", mode, AgentModeClassifier.MayShareBackend(provider));
        Capture(workspace, provider, Start, outcome, "Parent task", "parent-change");
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":nested-session"];

        var own = attachments.Get(session.AttachmentId)!;
        Assert.Equal(NativeAdoption.OwnId(provider, nested), own.Id);
        Assert.Equal(nested, own.Process);
        Assert.Equal("observed", own.Ownership);
        Assert.Equal(LifecycleOwners.AgentProcess, own.Evidence!.LifecycleOwner);
        Assert.False(own.ManagedTitle);
        Assert.False(own.ManagedBackground);
        // Only a shell proven by this client's own ancestry survives; the inherited variable's shell never does.
        Assert.Equal(shellSource == "ancestry" && mode != AgentModes.Headless ? shell : null, own.Host.Shell);
        var identity = new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root);
        Assert.NotEqual("Parent task", identity.Name);
        Assert.NotEqual("parent-change", identity.Change);
        // The presentation writer is never offered the nested session's title or background.
        Assert.Null(new TerminalPresentationResolver(workspace.Paths).Resolve(session, new GitContext()));

        var original = attachments.Get("launch-attachment")!;
        Assert.Equal(launcher, original.Process);
        Assert.Equal("managed", original.Ownership);
        Assert.True(original.ManagedTitle);
        Assert.Equal(LifecycleOwners.BellTower, original.Evidence!.LifecycleOwner);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ALegitimateManagedLaunchStillResolvesItsPresentationAndIdentity(string provider)
    {
        using var workspace = new TestWorkspace();
        using var agent = DisposableSleeper.Start();
        var launcher = ProcessProbe.Identify(agent.Id)!;
        var shell = ProcessProbe.Identify(Environment.ProcessId)!;
        var record = Captured(provider, launcher, "launch-attachment", "managed", shell, "launch-env");
        new AttachmentStore(workspace.Paths).Save(record with { Host = record.Host with { Kind = "vscode" }, Evidence = AttachmentEvidence.ForManagedLaunch(launcher) });
        var outcome = NativeAdoption.Apply(record, "nested-session", AgentModes.EmbeddedRuntime, AgentModeClassifier.MayShareBackend(provider));
        Capture(workspace, provider, Start, outcome, "Parent task");
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":nested-session"];
        Assert.Equal("launch-attachment", session.AttachmentId);
        Assert.Equal("Parent task", new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).Name);
        Assert.NotNull(new TerminalPresentationResolver(workspace.Paths).Resolve(session, new GitContext()));
    }

    // An unproven mode may rely on the launch record only while that record's process is live and is the hooked agent.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ManagedPlusUnknownModeRelyOnTheVerifiedLaunchRecordOtherwiseFailsClosed(string provider)
    {
        using var workspace = new TestWorkspace();
        using var launcherAgent = DisposableSleeper.Start();
        using var other = DisposableSleeper.Start();
        var launcher = ProcessProbe.Identify(launcherAgent.Id)!;
        var different = ProcessProbe.Identify(other.Id)!;
        var shell = ProcessProbe.Identify(Environment.ProcessId)!;
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(Captured(provider, launcher, "launch-attachment", "managed", shell, "launch-env") with { Evidence = AttachmentEvidence.ForManagedLaunch(launcher) });
        var shares = AgentModeClassifier.MayShareBackend(provider);

        var matching = NativeAdoption.Apply(Captured(provider, launcher, "launch-attachment", "managed", shell, "launch-env"), "session-a", AgentModes.Unknown, shares);
        // Codex puts the unproven launch on the wire in the fail-closed shape; Claude keeps its direct classification.
        Assert.Equal(!shares, matching.Dedicated);
        Assert.Equal(shares, matching.Attachment.Evidence!.Provisional);
        Capture(workspace, provider, Start.Replace("nested-session", "session-a"), matching);
        var kept = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-a"];
        Assert.Equal("launch-attachment", kept.AttachmentId);
        Assert.Equal(shell, attachments.Get("launch-attachment")!.Host.Shell);
        Assert.True(attachments.Get("launch-attachment")!.ManagedTitle);
        Assert.False(attachments.Get("launch-attachment")!.Evidence!.Provisional);

        // A different hooked agent carrying the same identifier is not the recorded launch generation.
        var foreign = NativeAdoption.Apply(Captured(provider, different, "launch-attachment", "managed", shell, "launch-env"), "session-b", AgentModes.Unknown, shares);
        Capture(workspace, provider, Start.Replace("nested-session", "session-b"), foreign);
        var own = attachments.Get(new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-b"].AttachmentId)!;
        Assert.NotEqual("launch-attachment", own.Id);
        Assert.False(own.ManagedTitle);
        Assert.Equal(launcher, attachments.Get("launch-attachment")!.Process);

        // A capture with no recorded launch fails closed for a provider that can share a backend; Claude keeps its direct classification.
        var orphan = NativeAdoption.Apply(Captured(provider, different, "no-record", "managed", shell, "launch-env"), "session-c", AgentModes.Unknown, shares);
        Capture(workspace, provider, Start.Replace("nested-session", "session-c"), orphan);
        var third = attachments.Get(new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-c"].AttachmentId)!;
        if (shares)
        {
            Assert.Equal("shared-runtime", third.Ownership);
            Assert.Null(third.Process);
            Assert.False(third.ManagedTitle);
        }
        else
        {
            Assert.Equal(different, third.Process);
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void EvidenceStampedForAnotherGenerationOrRevisionIsTreatedAsAbsent(string provider)
    {
        var agent = new ProcessIdentity(10, 20);
        var attachment = Captured(provider, agent, "stamp", "observed");
        var good = attachment with { Evidence = new() { Mode = AgentModes.EmbeddedRuntime, Generation = agent, LifecycleOwner = LifecycleOwners.AgentProcess } };
        Assert.Equal(AgentModes.EmbeddedRuntime, AttachmentEvidence.Of(good).Mode);
        var otherGeneration = good with { Process = new ProcessIdentity(10, 21) };
        Assert.Equal(AgentModes.Unknown, AttachmentEvidence.Of(otherGeneration).Mode);
        Assert.Equal(LifecycleOwners.AgentProcess, AttachmentEvidence.Of(otherGeneration).LifecycleOwner);
        var futureRevision = good with { Evidence = good.Evidence! with { Revision = AttachmentEvidence.CurrentRevision + 1 } };
        Assert.Equal(AgentModes.Unknown, AttachmentEvidence.Of(futureRevision).Mode);
    }

    // The record an older tray round-trips has no evidence property; every evidence field must come back unchanged.
    private sealed record OlderAttachment(string Id, string Provider, string Ownership)
    {
        public string Health { get; init; } = "unverified";
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnOlderTrayRoundTripKeepsEvidenceIncludingReservedBinderFields(string provider)
    {
        var agent = new ProcessIdentity(10, 20);
        var evidence = new AttachmentEvidence
        {
            Mode = AgentModes.EmbeddedRuntime,
            Generation = agent,
            LifecycleOwner = LifecycleOwners.AgentProcess,
            Terminal = new() { Binding = "verified", Client = agent, Shell = new(11, 21), OrderingKey = "01a1", ObservedAt = DateTimeOffset.UnixEpoch }
        };
        var text = Json.Encode(Captured(provider, agent, "round-trip", "observed") with { Evidence = evidence });
        var older = Json.Decode<OlderAttachment>(text);
        Assert.True(older.AdditionalData!.ContainsKey("evidence"));
        var restored = Json.Decode<Attachment>(Json.Encode(older with { Health = "unavailable" }));
        Assert.Equal("unavailable", restored.Health);
        Assert.Equal(evidence.Mode, restored.Evidence!.Mode);
        Assert.Equal(agent, restored.Evidence.Generation);
        Assert.Equal(evidence.Terminal.Shell, restored.Evidence.TerminalBinding.Shell);
        Assert.Equal("01a1", restored.Evidence.TerminalBinding.OrderingKey);
        Assert.Equal(DateTimeOffset.UnixEpoch, restored.Evidence.TerminalBinding.ObservedAt);
        Assert.Equal(restored.Host, Captured(provider, agent, "round-trip", "observed").Host);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ReplacementKeepsUnknownFieldsNestedInsideStoredEvidenceAndLearnsAnUnknownOwner(string provider)
    {
        using var workspace = new TestWorkspace();
        var stored = Json.Decode<Attachment>("""
            {"id":"nested","provider":"PROVIDER","host":{"kind":"vscode"},"directory":"D:/example","registeredAt":"2026-10-01T00:00:00+00:00","ownership":"observed",
             "endedAt":"2026-10-01T01:00:00+00:00",
             "evidence":{"mode":"embedded-runtime","lifecycleOwner":"unknown","runtime":{"access":"none","runtimeFuture":1},"terminal":{"binding":"unverified","terminalFuture":2}}}
            """.Replace("PROVIDER", provider));
        var store = new AttachmentStore(workspace.Paths);
        store.Save(stored);
        var captured = Captured(provider, null, "nested", "observed") with { Evidence = new() { Mode = AgentModes.Headless, LifecycleOwner = LifecycleOwners.AgentProcess } };
        var adopted = store.Adopt(captured, DateTimeOffset.UtcNow, resume: true);
        var text = File.ReadAllText(Path.Combine(workspace.Paths.Attachments, ContentHash.Compute("nested") + ".json"));
        Assert.Contains("runtimeFuture", text);
        Assert.Contains("terminalFuture", text);
        Assert.Equal(AgentModes.Headless, adopted.Evidence!.Mode);

        // Without replacement an unknown stored owner is learned from the capture, like the mode.
        store.Save(stored with { Id = "learn", EndedAt = null });
        var learned = store.Adopt(captured with { Id = "learn" }, DateTimeOffset.UtcNow);
        Assert.Equal(LifecycleOwners.AgentProcess, learned.Evidence!.LifecycleOwner);
    }

    // A pre-change capture-v1 consumer reads the wire fields only: a provisional Codex capture must look like a shared runtime.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AProvisionalCaptureIsFailClosedOnTheWireForAPreChangeConsumer(string provider)
    {
        var launcher = new ProcessIdentity(100, 200);
        var shell = new ProcessIdentity(101, 201);
        var outcome = NativeAdoption.Apply(Captured(provider, launcher, "launch-attachment", "managed", shell, "launch-env"), "wire-session", AgentModes.Unknown,
            AgentModeClassifier.MayShareBackend(provider), new ProcessIdentity(102, 202));
        using var input = JsonDocument.Parse(Start.Replace("nested-session", "wire-session"));
        var envelope = CaptureFactory.Create(provider, input.RootElement, outcome.Attachment, DateTimeOffset.UtcNow, null, null, outcome.Dedicated, "Parent task", "parent-change")!;
        using var stream = new MemoryStream(CaptureProtocol.Encode(envelope));
        using var wire = JsonDocument.Parse(stream.ToArray());
        var root = wire.RootElement;
        var attachment = root.GetProperty("attachment");
        if (AgentModeClassifier.MayShareBackend(provider))
        {
            Assert.False(root.GetProperty("dedicated").GetBoolean());
            Assert.Equal(JsonValueKind.Null, attachment.GetProperty("process").ValueKind);
            Assert.Equal("unknown", attachment.GetProperty("host").GetProperty("kind").GetString());
            Assert.False(attachment.GetProperty("managedTitle").GetBoolean());
            Assert.False(attachment.GetProperty("managedBackground").GetBoolean());
            Assert.Equal("shared-runtime", attachment.GetProperty("ownership").GetString());
            Assert.StartsWith(provider + "-session-", attachment.GetProperty("id").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("name").ValueKind);
            Assert.Equal(JsonValueKind.Null, root.GetProperty("change").ValueKind);
            // The candidate is inert evidence for a consumer that does not know it.
            Assert.Equal("launch-attachment", attachment.GetProperty("evidence").GetProperty("candidate").GetProperty("id").GetString());
        }
        else
        {
            Assert.True(root.GetProperty("dedicated").GetBoolean());
            Assert.Equal(100, attachment.GetProperty("process").GetProperty("pid").GetInt32());
            Assert.False(attachment.GetProperty("evidence").GetProperty("provisional").GetBoolean());
        }
    }

    // Profile (module) launches have no launch record before the first hook; the prepared launch record names the launching shell.
    [Theory]
    [InlineData("claude", "child")]
    [InlineData("claude", "nested")]
    [InlineData("claude", "dead")]
    [InlineData("claude", "reused")]
    [InlineData("codex", "child")]
    [InlineData("codex", "nested")]
    [InlineData("codex", "dead")]
    [InlineData("codex", "reused")]
    public void AProfileLaunchIsPromotedOnlyFromItsLiveRecordedShell(string provider, string scenario)
    {
        using var workspace = new TestWorkspace();
        using var shellProcess = DisposableSleeper.Start();
        using var toolShell = DisposableSleeper.Start();
        using var agentProcess = DisposableSleeper.Start();
        var launchShell = ProcessProbe.Identify(shellProcess.Id)!;
        var tool = ProcessProbe.Identify(toolShell.Id)!;
        var agent = ProcessProbe.Identify(agentProcess.Id)!;
        var recorded = launchShell;
        var parent = launchShell;
        switch (scenario)
        {
            case "nested":
                parent = tool;
                break;
            case "dead":
                shellProcess.Kill();
                shellProcess.WaitForExit();
                break;
            case "reused":
                parent = launchShell with { StartedUtcTicks = launchShell.StartedUtcTicks + 1 };
                break;
        }
        new LaunchPresentationStore(workspace.Paths).Prepare("profile-launch", recorded);
        var shares = AgentModeClassifier.MayShareBackend(provider);
        var outcome = NativeAdoption.Apply(Captured(provider, agent, "profile-launch", "managed", launchShell, "launch-env"), "profile-session", AgentModes.Unknown, shares, parent);
        if (outcome.Attachment.Evidence?.Candidate is { } candidate)
        {
            // AttachmentCapture.Resolve attaches the launch identity text to the candidate.
            outcome = outcome with { Attachment = outcome.Attachment with { Evidence = outcome.Attachment.Evidence with { Candidate = candidate with { Name = "Profile task", Change = "profile-change" } } } };
        }
        Capture(workspace, provider, Start.Replace("nested-session", "profile-session"), outcome, "Profile task", "profile-change");
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":profile-session"];
        var adopted = new AttachmentStore(workspace.Paths).Get(session.AttachmentId)!;

        if (!shares || scenario == "child")
        {
            // Claude keeps its existing direct classification; a Codex agent whose direct parent is the live recorded shell is promoted.
            Assert.Equal("profile-launch", adopted.Id);
            Assert.Equal(agent, adopted.Process);
            Assert.True(adopted.ManagedTitle);
            Assert.Equal("managed", adopted.Ownership);
            Assert.False(adopted.Evidence!.Provisional);
            Assert.Equal("Profile task", new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).Name);
        }
        else
        {
            Assert.StartsWith(provider + "-session-", adopted.Id);
            Assert.Null(adopted.Process);
            Assert.Null(adopted.Host.Shell);
            Assert.False(adopted.ManagedTitle);
            Assert.False(adopted.ManagedBackground);
            Assert.Equal("shared-runtime", adopted.Ownership);
            Assert.NotEqual("Profile task", new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).Name);
        }
    }
}

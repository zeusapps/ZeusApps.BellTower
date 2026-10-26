using System.Diagnostics;
using System.Text.Json;
using BellTower.Core;
using BellTower.Diagnostics;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class AttachmentHealthTests
{
    [Theory]
    [InlineData("claude", false)]
    [InlineData("claude", true)]
    [InlineData("codex", false)]
    [InlineData("codex", true)]
    public void ExplicitReplacementUsesOnlyNewGenerationDestinationsAndRetainsOptInsAndExtensions(string provider, bool capturedDestination)
    {
        using var workspace = new TestWorkspace();
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30" })
        {
            start.ArgumentList.Add(argument);
        }
        using var child = Process.Start(start)!;
        try
        {
            var former = ProcessProbe.Identify(Environment.ProcessId)!;
            var current = ProcessProbe.Identify(child.Id)!;
            var at = DateTimeOffset.UtcNow.AddSeconds(-10);
            using var extra = JsonDocument.Parse("{\"old\":1,\"new\":2}");
            var previousHost = new HostTarget("windows-terminal", former, former, 111, "old-terminal")
            {
                AdditionalData = new() { ["futureOldHost"] = extra.RootElement.GetProperty("old").Clone() }
            };
            var previous = new Attachment("same-id", provider, former, previousHost, workspace.Root, at,
                EndedAt: at.AddSeconds(1), ManagedTitle: true, ManagedBackground: true)
            {
                AdditionalData = new() { ["futureAttachment"] = extra.RootElement.Clone() }
            };
            var attachments = new AttachmentStore(workspace.Paths);
            attachments.Save(previous);
            new LaunchPresentationStore(workspace.Paths).Prepare(previous.Id, former);
            var capturedHost = capturedDestination ? new HostTarget("vscode", current, current, 222, "new-terminal") : new HostTarget();
            capturedHost.AdditionalData = new() { ["futureNewHost"] = extra.RootElement.GetProperty("new").Clone() };
            var captured = new Attachment(previous.Id, provider, current, capturedHost, workspace.Root, at.AddSeconds(2));
            var resumed = attachments.Adopt(captured, at.AddSeconds(3), resume: true);
            Assert.Equal(current, resumed.Process);
            Assert.Null(resumed.EndedAt);
            Assert.Equal(at.AddSeconds(3), resumed.RegisteredAt);
            Assert.Equal(capturedDestination ? "vscode" : "unknown", resumed.Host.Kind);
            Assert.Equal(capturedDestination ? current : null, resumed.Host.Shell);
            Assert.Equal(capturedDestination ? current : null, resumed.Host.WindowProcess);
            Assert.Equal(capturedDestination ? 222 : 0, resumed.Host.WindowHandle);
            Assert.Equal(capturedDestination ? "new-terminal" : null, resumed.Host.TerminalSession);
            Assert.True(resumed.ManagedTitle);
            Assert.True(resumed.ManagedBackground);
            var encoded = Json.Encode(resumed);
            Assert.Contains("futureAttachment", encoded);
            Assert.Contains("futureOldHost", encoded);
            Assert.Contains("futureNewHost", encoded);
            Assert.Equal(encoded, Json.Encode(attachments.Adopt(captured with { Process = former, Host = previousHost }, at.AddSeconds(4))));
            Assert.Equal(encoded, Json.Encode(attachments.Adopt(captured, at.AddSeconds(2), resume: true)));
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: false);
            }
            Assert.True(child.WaitForExit(5000));
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void CaptureConsumerRepairsMissingAgentAndShellWithoutCreatingAnotherWait(string provider)
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddSeconds(-10);
        var attachment = new Attachment("attachment-1", provider, null, new("windows-terminal"), workspace.Root, at);
        CaptureEnvelope Capture(EventKind kind, DateTimeOffset when, Attachment target)
        {
            var observation = TestWorkspace.Event(kind, when) with
            {
                Provider = provider,
                Directory = workspace.Root,
                IngressAt = when,
                TurnId = null,
                HumanPrompt = kind == EventKind.Permission,
                ProviderDelay = kind == EventKind.Permission ? TimeSpan.FromSeconds(5) : null
            };
            return new() { Id = observation.Id, IngressAt = when, Observation = observation, Attachment = target, Dedicated = true };
        }
        CaptureSpool.Write(workspace.Paths.Root, Capture(EventKind.Permission, at, attachment));
        var first = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-1"];
        var identity = new IdentityStore(workspace.Paths).Get(first.Key, workspace.Root);
        var live = ProcessProbe.Identify(Environment.ProcessId)!;
        CaptureSpool.Write(workspace.Paths.Root, Capture(EventKind.Register, at.AddSeconds(1), attachment with
        {
            Process = live,
            Host = new("windows-terminal", live)
        }));
        var repaired = new StateStore(workspace.Paths).Drain().Sessions[first.Key];
        Assert.Equal("verified-live", repaired.AttachmentHealth);
        Assert.Equal(first.Episode, repaired.Episode);
        Assert.Equal(first.Since, repaired.Since);
        Assert.Single(repaired.Waits);
        Assert.Equal(identity.Palette, new IdentityStore(workspace.Paths).Get(first.Key, workspace.Root).Palette);
        Assert.Equal(live, new AttachmentStore(workspace.Paths).Get(attachment.Id)!.Host.Shell);
        Assert.Equal(live, new AttachmentStore(workspace.Paths).Get(attachment.Id)!.Process);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ConsumedPaletteRetainsDurableShellAndRepairPreservesWaitAndIdentity(string provider)
    {
        using var workspace = new TestWorkspace();
        var shell = ProcessProbe.Identify(Environment.ProcessId)!;
        var launches = new LaunchPresentationStore(workspace.Paths);
        var prepared = launches.Prepare("attachment-1", shell);
        Assert.Equal(prepared.Palette, launches.Consume("attachment-1", shell));
        Assert.Null(launches.Consume("attachment-1", shell));
        Assert.Equal(shell, new LaunchPresentationStore(workspace.Paths).Shell("attachment-1"));
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Adopt(new("attachment-1", provider, null, new(), workspace.Root, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow);
        Assert.Equal(shell, attachments.Get("attachment-1")!.Host.Shell);
        new IdentityStore(workspace.Paths).Get(provider + ":session-1", workspace.Root, initialPalette: prepared.Palette);
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with { Provider = provider, Directory = workspace.Root });
        var original = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-1"];
        attachments.Adopt(attachments.Get("attachment-1")! with { Process = shell }, DateTimeOffset.UtcNow);
        var repaired = new StateStore(workspace.Paths).Drain().Sessions[original.Key];
        Assert.Equal("verified-live", repaired.AttachmentHealth);
        Assert.Equal(original.Episode, repaired.Episode);
        Assert.Equal(original.Since, repaired.Since);
        Assert.Equal(prepared.Palette, new IdentityStore(workspace.Paths).Get(original.Key, workspace.Root).Palette);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void StaleUnknownSurvivesRestartAndOnlyVerifiedRepairRestoresAttention(string provider)
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddHours(-1);
        var attachments = new AttachmentStore(workspace.Paths);
        attachments.Save(new("attachment-1", provider, null, new("future-host"), workspace.Root, at));
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission(at) with { Provider = provider });
        var first = new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-1"];
        Assert.False(first.Ended);
        Assert.Equal("unavailable", first.AttachmentHealth);
        Assert.Equal("unavailable", first.Activity);
        Assert.False(first.RequiresAttention(DateTimeOffset.UtcNow));
        Assert.Single(first.Waits);
        var restarted = new StateStore(workspace.Paths).Drain().Sessions[first.Key];
        Assert.Equal(first.AttachmentUnverifiedSince, restarted.AttachmentUnverifiedSince);
        Assert.Equal("unavailable", restarted.AttachmentHealth);
        var live = ProcessProbe.Identify(Environment.ProcessId)!;
        var current = attachments.Get("attachment-1")!;
        // Invalid/reused PID evidence and a newer hook timestamp alone do not revive it.
        attachments.Adopt(current with { Process = live with { StartedUtcTicks = 1 } }, DateTimeOffset.UtcNow);
        Assert.Equal("unavailable", new StateStore(workspace.Paths).Drain().Sessions[first.Key].AttachmentHealth);
        attachments.Adopt(current with { Process = live }, DateTimeOffset.UtcNow);
        var repaired = new StateStore(workspace.Paths).Drain().Sessions[first.Key];
        Assert.Equal("verified-live", repaired.AttachmentHealth);
        Assert.Equal(first.Episode, repaired.Episode);
        Assert.Equal(first.Since, repaired.Since);
        Assert.True(repaired.RequiresAttention(DateTimeOffset.UtcNow));
        Assert.Equal("future-host", attachments.Get("attachment-1")!.Host.Kind);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ShellOnlyLiveIsUnverifiedButShellExitOrPidReuseConfirmsEnd(string provider)
    {
        var at = DateTimeOffset.UtcNow;
        var agent = new ProcessIdentity(10, 10);
        var shell = new ProcessIdentity(20, 20);
        var attachment = new Attachment("fixture", provider, null, new("unknown", shell), "", at);
        var liveShell = new AttachmentHealth(identity => identity == shell ? ProcessLiveness.Live : ProcessLiveness.Unverified);
        Assert.Equal("unverified", liveShell.Evaluate(attachment, at.AddMinutes(5), TimeSpan.FromMinutes(10)).Health);
        Assert.Equal("unavailable", liveShell.Evaluate(attachment, at.AddMinutes(11), TimeSpan.FromMinutes(10)).Health);
        Assert.Equal("confirmed-ended", new AttachmentHealth(_ => ProcessLiveness.Ended).Evaluate(attachment, at, TimeSpan.FromMinutes(10)).Health);
        var unknown = new AttachmentHealth(_ => ProcessLiveness.Unverified);
        Assert.Equal("unverified", unknown.Evaluate(attachment with { Process = agent, Health = "verified-live" }, at, TimeSpan.FromMinutes(10)).Health);
        var live = new AttachmentHealth(_ => ProcessLiveness.Live);
        Assert.Equal("verified-live", live.Evaluate(attachment with { Process = agent }, at.AddDays(7), TimeSpan.FromMinutes(1)).Health);
        var actual = ProcessProbe.Identify(Environment.ProcessId)!;
        Assert.Equal(ProcessLiveness.Ended, ProcessProbe.Inspect(actual with { StartedUtcTicks = 1 }));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void LaterCaptureRepairsShellButStaleOrConflictingEvidenceCannotStealOwnership(string provider)
    {
        using var workspace = new TestWorkspace();
        var current = ProcessProbe.Identify(Environment.ProcessId)!;
        var at = DateTimeOffset.UtcNow;
        var attachments = new AttachmentStore(workspace.Paths);
        var original = new Attachment("fixture", provider, current, new("future-host"), workspace.Root, at);
        attachments.Save(original);
        attachments.Adopt(original with { Host = new("windows-terminal", current) }, at.AddSeconds(-1));
        Assert.Null(attachments.Get("fixture")!.Host.Shell);
        attachments.Adopt(original with { Host = new("windows-terminal", current) }, at.AddSeconds(1));
        var repaired = attachments.Get("fixture")!;
        Assert.Equal(current, repaired.Host.Shell);
        Assert.Equal("future-host", repaired.Host.Kind);
        var ended = repaired with { EndedAt = at.AddSeconds(2) };
        attachments.Save(ended);
        Assert.NotNull(attachments.Adopt(original, at.AddSeconds(3)).EndedAt);
        var resumed = attachments.Adopt(original with { Host = repaired.Host }, at.AddSeconds(4), resume: true);
        Assert.Null(resumed.EndedAt);
        Assert.Equal(at.AddSeconds(4), resumed.RegisteredAt);
        Assert.Equal(provider, attachments.Adopt(original with { Provider = "other" }, at.AddSeconds(5)).Provider);
    }

    [Fact]
    public void MixedProvidersHealthControlsAndUnknownFieldsRemainIndependent()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddMinutes(-5);
        new ConfigStore(workspace.Paths).Update(config => config.AttachmentEvidenceGraceMinutes = 1);
        foreach (var provider in new[] { "claude", "codex" })
        {
            using var extra = JsonDocument.Parse("{\"retained\":true}");
            var attachment = new Attachment(provider, provider, provider == "claude" ? ProcessProbe.Identify(Environment.ProcessId) : null,
                new("future-host") { AdditionalData = new() { ["futureHost"] = extra.RootElement.Clone() } }, workspace.Root, at)
            {
                Ownership = "shared-runtime",
                AdditionalData = new() { ["futureAttachment"] = extra.RootElement.Clone() }
            };
            new AttachmentStore(workspace.Paths).Save(attachment);
            new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission(at) with { Provider = provider, AttachmentId = provider });
        }
        var state = new StateStore(workspace.Paths).Drain();
        Assert.Equal("verified-live", state.Sessions["claude:session-1"].AttachmentHealth);
        Assert.Equal("unavailable", state.Sessions["codex:session-1"].AttachmentHealth);
        foreach (var provider in new[] { "claude", "codex" })
        {
            var encoded = Json.Encode(new AttachmentStore(workspace.Paths).Get(provider));
            Assert.Contains("futureHost", encoded);
            Assert.Contains("futureAttachment", encoded);
            Assert.Contains("future-host", encoded);
        }
        var report = Json.Encode(new DiagnosticsReporter(workspace.Paths).Report());
        Assert.Contains("verified-live", report);
        Assert.Contains("unavailable", report);
        Assert.Contains("shared-runtime", report);
        Assert.Equal("unavailable", new Navigator([]).Precision(new AttachmentStore(workspace.Paths).Get("codex")));
        Assert.False(new Navigator([]).Focus(new AttachmentStore(workspace.Paths).Get("codex")).Success);
        Assert.Equal("unavailable", new SessionCatalog(workspace.Paths).List().Single(session => session.Key == "codex:session-1").AttachmentHealth);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(10, 10)]
    [InlineData(1441, 1440)]
    public void GraceConfigurationIsBoundedAndPreservesUnrelatedSettings(int configured, int expected)
    {
        using var workspace = new TestWorkspace();
        File.WriteAllText(workspace.Paths.Config, "{\"voice\":false,\"future\":42,\"attachmentEvidenceGraceMinutes\":" + configured + "}");
        var config = new ConfigStore(workspace.Paths);
        Assert.Equal(expected, config.Read().AttachmentEvidenceGraceMinutes);
        config.Update(value => value.Muted = true);
        Assert.False(config.Read().Voice);
        Assert.Contains("future", File.ReadAllText(workspace.Paths.Config));
    }
}

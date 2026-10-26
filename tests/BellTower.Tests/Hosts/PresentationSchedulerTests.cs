using System.Diagnostics;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class PresentationSchedulerTests
{
    private sealed class RecordingWriter : ITerminalPresentationWriter
    {
        public List<DesiredTerminalPresentation> Requests { get; } = [];
        public bool Success { get; set; } = true;
        public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation)
        {
            Requests.Add(desired);
            return Task.FromResult(Success);
        }
    }

    private sealed class PartialWriter(bool throws) : ITerminalPresentationWriter
    {
        public List<DesiredTerminalPresentation> Requests { get; } = [];
        public Dictionary<string, string> VisibleTitles { get; } = [];
        public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation)
        {
            Requests.Add(desired);
            VisibleTitles[desired.AttachmentId] = desired.Title;
            if (desired.Title == "B")
            {
                if (throws)
                {
                    throw new IOException("partial-write-result-unknown");
                }
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }
    }

    private static SessionState Register(TestWorkspace workspace, string provider)
    {
        var current = ProcessProbe.Identify(Environment.ProcessId)!;
        new AttachmentStore(workspace.Paths).Save(new(provider, provider, current, new("windows-terminal", current), workspace.Root,
            DateTimeOffset.UtcNow, ManagedTitle: true));
        new EventStore(workspace.Paths).Publish(TestWorkspace.ConfirmedPermission() with
        {
            Provider = provider,
            AttachmentId = provider,
            Directory = workspace.Root
        });
        return new StateStore(workspace.Paths).Drain().Sessions[provider + ":session-1"];
    }

    [Theory]
    [InlineData("claude", false)]
    [InlineData("claude", true)]
    [InlineData("codex", false)]
    [InlineData("codex", true)]
    public async Task PartialFailedWriteInvalidatesOldReceiptAndRestoresFormerDesiredTitle(string provider, bool throws)
    {
        using var workspace = new TestWorkspace();
        var session = Register(workspace, provider);
        var independent = Register(workspace, provider == "claude" ? "codex" : "claude");
        var identities = new IdentityStore(workspace.Paths);
        var identity = identities.Get(session.Key, workspace.Root);
        identity.ManualName = "A";
        identities.Save(identity);
        var other = identities.Get(independent.Key, workspace.Root);
        other.ManualName = "Independent";
        identities.Save(other);
        var episode = session.Episode;
        var palette = identity.Palette;
        var now = DateTimeOffset.UtcNow;
        var writer = new PartialWriter(throws);
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), writer, () => now);
        await pump.RefreshAsync([session, independent]);
        Assert.Equal("A", writer.VisibleTitles[session.AttachmentId]);
        identity.ManualName = "B";
        identities.Save(identity);
        await pump.RefreshAsync([session, independent]);
        Assert.Equal("B", writer.VisibleTitles[session.AttachmentId]);
        await pump.RefreshAsync([session, independent]);
        Assert.Equal(2, writer.Requests.Count(request => request.AttachmentId == session.AttachmentId));
        identity.ManualName = "A";
        identities.Save(identity);
        await pump.RefreshAsync([session, independent]);
        Assert.Equal("A", writer.VisibleTitles[session.AttachmentId]);
        Assert.Equal(new[] { "A", "B", "A" }, writer.Requests.Where(request => request.AttachmentId == session.AttachmentId).Select(request => request.Title));
        await pump.RefreshAsync([session, independent]);
        Assert.Equal(3, writer.Requests.Count(request => request.AttachmentId == session.AttachmentId));
        Assert.Single(writer.Requests, request => request.AttachmentId == independent.AttachmentId);
        Assert.Equal("Independent", writer.VisibleTitles[independent.AttachmentId]);
        Assert.Equal(episode, new StateStore(workspace.Paths).Read().Sessions[session.Key].Episode);
        Assert.Equal(palette, identities.Get(session.Key, workspace.Root).Palette);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task FreshUnchangedTicksLaunchNoWriterOrGitAndChangedIdentityWritesOnce(string provider)
    {
        using var workspace = new TestWorkspace();
        var session = Register(workspace, provider);
        var identity = new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root);
        var now = DateTimeOffset.UtcNow;
        var reads = 0;
        var cache = new CheckoutCache(_ => { reads++; return new("feature"); }, () => now);
        var writer = new RecordingWriter();
        using var pump = new TerminalTitlePump(workspace.Paths, cache, writer, () => now);
        await pump.RefreshAsync([session]);
        for (var tick = 0; tick < 100; tick++)
        {
            await pump.RefreshAsync([session]);
            // Overview and title scheduling share the same cache.
            Assert.Equal("feature", (await cache.ReadAsync(workspace.Root)).Branch);
        }
        Assert.Single(writer.Requests);
        Assert.Equal(1, reads);
        var palette = identity.Palette;
        var episode = session.Episode;
        identity.ManualName = "Changed";
        new IdentityStore(workspace.Paths).Save(identity);
        await pump.RefreshAsync([session]);
        await pump.RefreshAsync([session]);
        Assert.Equal(2, writer.Requests.Count);
        Assert.Equal("Changed | feature", writer.Requests[1].Title);
        Assert.Equal(1, reads);
        Assert.Equal(episode, new StateStore(workspace.Paths).Read().Sessions[session.Key].Episode);
        Assert.Equal(palette, new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root).Palette);
    }

    [Fact]
    public async Task CacheFreshnessAndExplicitInvalidationObserveBranchChangeWithoutParallelReaders()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        var branch = "first";
        var reads = 0;
        var active = 0;
        var peak = 0;
        var cache = new CheckoutCache(_ =>
        {
            var concurrent = Interlocked.Increment(ref active);
            peak = Math.Max(peak, concurrent);
            Interlocked.Increment(ref reads);
            Interlocked.Decrement(ref active);
            return new(branch);
        }, () => now);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.ReadAsync(workspace.Root)));
        Assert.Equal(1, reads);
        Assert.Equal(1, peak);
        branch = "second";
        Assert.Equal("first", (await cache.ReadAsync(workspace.Root)).Branch);
        now = now.AddSeconds(5);
        Assert.Equal("second", (await cache.ReadAsync(workspace.Root)).Branch);
        branch = "third";
        await cache.InvalidateAsync(workspace.Root);
        Assert.Equal("third", (await cache.ReadAsync(workspace.Root)).Branch);
        Assert.Equal(3, reads);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task FailedTargetBacksOffAndLogsAtMostOnceAMinuteAndChangesResetRetry(string provider)
    {
        using var workspace = new TestWorkspace();
        var session = Register(workspace, provider);
        var now = DateTimeOffset.UtcNow;
        var origin = now;
        var writer = new RecordingWriter { Success = false };
        var logs = new List<string>();
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => new()), writer, () => now, logs.Add);
        var attempts = new List<int>();
        for (var second = 0; second <= 180; second++)
        {
            var before = writer.Requests.Count;
            now = origin.AddSeconds(second);
            await pump.RefreshAsync([session]);
            if (writer.Requests.Count != before)
            {
                attempts.Add(second);
            }
        }
        Assert.Equal(new[] { 0, 2, 7, 22, 52, 112, 172 }, attempts);
        Assert.Equal(3, logs.Count);
        var identity = new IdentityStore(workspace.Paths).Get(session.Key, workspace.Root);
        identity.ManualName = "Retry changed";
        new IdentityStore(workspace.Paths).Save(identity);
        writer.Success = true;
        await pump.RefreshAsync([session]);
        Assert.Equal(8, writer.Requests.Count);
        await pump.RefreshAsync([session]);
        Assert.Equal(8, writer.Requests.Count);
    }

    [Fact]
    public async Task MixedProvidersSkipDeadAndUnavailableTargetsAndDoNotCrossReusePresentation()
    {
        using var workspace = new TestWorkspace();
        var claude = Register(workspace, "claude");
        var codex = Register(workspace, "codex");
        var writer = new RecordingWriter();
        var reads = 0;
        using var pump = new TerminalTitlePump(workspace.Paths, new CheckoutCache(_ => { reads++; return new(); }), writer);
        await pump.RefreshAsync([claude, codex]);
        Assert.Equal(2, writer.Requests.Count);
        Assert.Equal(2, writer.Requests.Select(request => request.AttachmentId).Distinct().Count());
        Assert.Equal(1, reads);
        new BellTower.Sessions.AttachmentLifecycle(workspace.Paths).Detach("claude");
        codex.AttachmentHealth = "unavailable";
        await pump.RefreshAsync([claude, codex]);
        Assert.Equal(2, writer.Requests.Count);
        Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutAndCancellationReapOnlyOwnedHelperAndAllowTheNextLaunch(bool cancel)
    {
        using var workspace = new TestWorkspace();
        var pidFile = Path.Combine(workspace.Root, "owned-pid.txt");
        var script = Path.Combine(workspace.Root, "sleep.ps1");
        File.WriteAllText(script, "[IO.File]::WriteAllText('" + pidFile.Replace("'", "''") + "', [string]$PID)\nStart-Sleep -Seconds 30");
        var start = new ProcessStartInfo("powershell.exe") { WorkingDirectory = workspace.Root };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script })
        {
            start.ArgumentList.Add(argument);
        }
        using var runner = new OwnedProcessRunner();
        using var cancellation = new CancellationTokenSource();
        var run = runner.RunAsync(start, "", TimeSpan.FromSeconds(8), cancellation.Token);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(pidFile) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
        Assert.True(File.Exists(pidFile));
        var pid = int.Parse(File.ReadAllText(pidFile));
        var helper = ProcessProbe.Identify(pid)!;
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        }
        else
        {
            Assert.False(await run);
        }
        Assert.Null(runner.ActiveProcessId);
        Assert.Equal(ProcessLiveness.Ended, ProcessProbe.Inspect(helper));
        Assert.Equal(ProcessLiveness.Live, ProcessProbe.Inspect(ProcessProbe.Identify(Environment.ProcessId)!));
        var next = new ProcessStartInfo("powershell.exe") { WorkingDirectory = workspace.Root };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "exit 0" })
        {
            next.ArgumentList.Add(argument);
        }
        Assert.True(await runner.RunAsync(next, "", TimeSpan.FromSeconds(5), CancellationToken.None));
        Assert.Equal(2, runner.LaunchCount);
        Assert.Null(runner.ActiveProcessId);
    }
}

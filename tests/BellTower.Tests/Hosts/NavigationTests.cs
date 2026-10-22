using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class NavigationTests
{
    [Fact]
    public void StaleWindowsTerminalHandleNeverFocusesOrCreatesAWindow()
    {
        using var w = new TestWorkspace();
        var host = new HostTarget("windows-terminal", null, new(int.MaxValue, 1), 123);
        Assert.False(WindowsTerminalNavigator.IsAvailable(host));
        var attachment = new Attachment("a", "codex", ProcessProbe.Identify(Environment.ProcessId), host, w.Root, DateTimeOffset.UtcNow);
        var result = new Navigator(w.Paths).Focus(attachment);
        Assert.False(result.Success);
        Assert.Empty(Directory.GetFiles(w.Paths.Requests));
    }

    [Fact]
    public void AmbiguousVsCodeRegistrationIsUnavailable()
    {
        using var w = new TestWorkspace();
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        foreach (var id in new[]
        {
            "window-one",
            "window-two"
        })
        {
            AtomicFile.Write(Path.Combine(w.Paths.Hosts, id + ".json"), BellTower.IO.Json.Encode(new HostRegistration(id, process, DateTimeOffset.UtcNow, [process.Pid])));
        }

        var attachment = new Attachment("a", "claude", process, new("vscode", process), w.Root, DateTimeOffset.UtcNow);
        Assert.Equal("unavailable", new Navigator(w.Paths).Precision(attachment));
        Assert.False(new Navigator(w.Paths).Focus(attachment).Success);
    }

    [Fact]
    public void BothAgentsUseTheSameHostRouting()
    {
        using var w = new TestWorkspace();
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        AtomicFile.Write(Path.Combine(w.Paths.Hosts, "window.json"), BellTower.IO.Json.Encode(new HostRegistration("window", process, DateTimeOffset.UtcNow, [process.Pid])));
        foreach (var provider in new[]
        {
            "claude",
            "codex"
        })
        {
            var attachment = new Attachment(provider, provider, process, new("vscode", process), w.Root, DateTimeOffset.UtcNow);
            Assert.Equal("exact terminal", new Navigator(w.Paths).Precision(attachment));
        }
    }

    [Fact]
    public async Task AcknowledgedHostFocusDoesNotAcknowledgeAttention()
    {
        using var w = new TestWorkspace();
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var store = new StateStore(w.Paths);
        var attachment = new Attachment("attachment-1", "claude", process, new("vscode", process), w.Root, DateTimeOffset.UtcNow);
        new AttachmentStore(w.Paths).Save(attachment);
        new EventStore(w.Paths).Publish(TestWorkspace.Event(EventKind.Permission, DateTimeOffset.UtcNow.AddSeconds(-15)));
        store.Drain();
        AtomicFile.Write(Path.Combine(w.Paths.Hosts, "window.json"), BellTower.IO.Json.Encode(new HostRegistration("window", process, DateTimeOffset.UtcNow, [process.Pid])));
        var focus = new Navigator(w.Paths).FocusAsync(attachment);
        var request = AtomicFile.Read<FocusRequest>(Directory.GetFiles(w.Paths.Requests, "*.json").Single())!;
        AtomicFile.Write(Path.Combine(w.Paths.Requests, request.Id + ".result"), BellTower.IO.Json.Encode(new FocusResult(true, "exact terminal", "revealed")));
        Assert.True((await focus).Success);
        var state = store.Read().Sessions["claude:session-1"];
        Assert.Null(state.AcknowledgedEpisode);
        Assert.True(state.RequiresAttention(DateTimeOffset.UtcNow));
    }
}

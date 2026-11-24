using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

public sealed class NavigationOutcomeTests
{
    private static (StateStore Store, TestWorkspace Workspace, Observation Wait) Arrange()
    {
        var workspace = new TestWorkspace();
        var store = new StateStore(workspace.Paths);
        var wait = TestWorkspace.ConfirmedPermission(DateTimeOffset.UtcNow.AddMinutes(-1), "blocked");
        new EventStore(workspace.Paths).Publish(wait);
        store.Drain();
        return (store, workspace, wait);
    }

    [Theory]
    [InlineData("Windows did not bring the owning VS Code window to the foreground.", "exact terminal")]
    [InlineData("This VS Code window runs an older Bell Tower bridge.", "unavailable")]
    [InlineData("The VS Code window lost the foreground before navigation completed.", "exact terminal")]
    public void FailedOrDeniedNavigationLeavesTheWaitAndChipTimerUnchanged(string message, string precision)
    {
        var (store, workspace, wait) = Arrange();
        using (workspace)
        {
            var before = store.Read().Sessions[wait.Key];
            Assert.False(NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, new FocusResult(false, precision, message), hideTimer: true));

            var after = store.Read().Sessions[wait.Key];
            Assert.Null(after.ViewedEpisode);
            Assert.Equal(before.Episode, after.Episode);
            Assert.Equal(before.Since, after.Since);
            Assert.True(after.RequiresAttention(DateTimeOffset.UtcNow));
            Assert.Null(after.AcknowledgedEpisode);
        }
    }

    [Fact]
    public void SuccessHidesOnlyTheCurrentEpisodeTimerAndOnlyForChipClicks()
    {
        var (store, workspace, wait) = Arrange();
        using (workspace)
        {
            var success = new FocusResult(true, "exact terminal", "ok");
            Assert.False(NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, success, hideTimer: false));
            Assert.Null(store.Read().Sessions[wait.Key].ViewedEpisode);
            Assert.False(NavigationOutcome.ApplyViewed(store, wait.Key, null, success, hideTimer: true));
            Assert.True(NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, success, hideTimer: true));
            Assert.Equal(wait.Id, store.Read().Sessions[wait.Key].ViewedEpisode);
        }
    }

    [Theory]
    [InlineData(true, "exact tab", false)]
    [InlineData(true, "exact terminal", false)]
    [InlineData(true, "window only", true)]
    [InlineData(true, "terminal only", true)]
    [InlineData(true, "tab or window", true)]
    [InlineData(false, "window only", true)]
    [InlineData(false, "unavailable", true)]
    [InlineData(false, "exact tab", true)]
    public void OnlyAnExactSuccessIsAllowedToSkipTheVisibleNotice(bool success, string precision, bool notice) =>
        Assert.Equal(notice, NavigationOutcome.NeedsNotice(new FocusResult(success, precision, "message")));

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AWindowTerminalProcessWithSeveralWindowsStatesItsReasonAndNeedsANotice(string provider)
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var host = new HostTarget("windows-terminal", process, process, 1234, "terminal-session");
        var several = BellTower.Hosts.WindowsTerminalNavigator.UnavailableReason(host, _ => 2);
        var single = BellTower.Hosts.WindowsTerminalNavigator.UnavailableReason(host, _ => 1);
        Assert.Contains("several windows", several, StringComparison.Ordinal);
        Assert.DoesNotContain("several windows", single, StringComparison.Ordinal);
        // Whatever host reports the failure, the click ends in a visible message for either client.
        using var workspace = new TestWorkspace();
        var attachment = new Attachment(provider, provider, process, host, workspace.Root, DateTimeOffset.UtcNow);
        var result = new Navigator(workspace.Paths).Focus(attachment);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.True(NavigationOutcome.NeedsNotice(result));
    }

    private static async Task<FocusResult> SharedClick(string provider, string mode)
    {
        var process = ProcessProbe.Identify(Environment.ProcessId)!;
        var host = new HostTarget("windows-terminal", process, process, 1234, "terminal-session") { ShellSource = "ancestry" };
        var attachment = new Attachment("a", provider, null, host, "D:/example", DateTimeOffset.UtcNow) { Ownership = "observed" };
        var shared = NativeAdoption.Shared(attachment, "session-1", mode);
        return await new Navigator([]).FocusAsync(shared);
    }

    [Theory]
    [InlineData("codex")]
    [InlineData("claude")]
    public async Task AStatedPermanentLimitHidesTheCurrentEpisodeTimerForEitherProvider(string provider)
    {
        var (store, workspace, wait) = Arrange();
        using (workspace)
        {
            var result = await SharedClick(provider, AgentModes.SharedBackend);
            Assert.Equal(NavigationReasons.SharedBackend, result.Message);
            Assert.False(NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, result, hideTimer: false));
            Assert.Null(store.Read().Sessions[wait.Key].ViewedEpisode);
            Assert.True(NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, result, hideTimer: true));

            var session = store.Read().Sessions[wait.Key];
            Assert.Equal(wait.Id, session.ViewedEpisode);
            Assert.True(session.RequiresAttention(DateTimeOffset.UtcNow));
            Assert.Null(session.AcknowledgedEpisode);
        }
    }

    public static IEnumerable<object[]> TemporaryFailures()
    {
        foreach (var provider in new[] { "claude", "codex" })
        {
            yield return [provider, NavigationReasons.ProcessUnverified];
            yield return [provider, NavigationReasons.TerminalUnverified];
            yield return [provider, "Windows declined foreground activation."];
            yield return [provider, NavigationNotice.FailedMessage];
            yield return [provider, NavigationNotice.Missing().Result.Message];
            yield return [provider, "No verified terminal destination is registered."];
        }
    }

    [Theory]
    [MemberData(nameof(TemporaryFailures))]
    public void ATemporaryFailureKeepsTheTimerForEitherProvider(string provider, string message)
    {
        _ = provider;
        var (store, workspace, wait) = Arrange();
        using (workspace)
        {
            Assert.False(NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, new FocusResult(false, "unavailable", message), hideTimer: true));
            Assert.Null(store.Read().Sessions[wait.Key].ViewedEpisode);
        }
    }

    [Fact]
    public void ASuccessSaveFailureStatesTheFocusAndTheSaveFailure()
    {
        var status = NavigationOutcome.SaveFailedStatus(new FocusResult(true, "exact terminal", "ok"));
        Assert.StartsWith("Session focused; ", status, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ASaveFailureKeepsTheStatedRefusalForEitherProvider(string provider)
    {
        var result = await SharedClick(provider, AgentModes.SharedBackend);
        var status = NavigationOutcome.SaveFailedStatus(result);
        Assert.StartsWith(NavigationReasons.SharedBackend, status, StringComparison.Ordinal);
        Assert.EndsWith("The timer preference could not be saved.", status, StringComparison.Ordinal);
        Assert.DoesNotContain("Session focused", status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnrecognizedModeConversationKeepsTheTimer()
    {
        var (store, workspace, wait) = Arrange();
        using (workspace)
        {
            var result = await SharedClick("codex", AgentModes.Unknown);
            Assert.Equal(NavigationReasons.TerminalUnverified, result.Message);
            Assert.False(NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, result, hideTimer: true));
            Assert.Null(store.Read().Sessions[wait.Key].ViewedEpisode);
        }
    }

    [Fact]
    public async Task ALateLimitForAnOlderEpisodeCannotHideANewerTimer()
    {
        var (store, workspace, wait) = Arrange();
        using (workspace)
        {
            new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Prompt, wait.At.AddSeconds(1)));
            var newer = TestWorkspace.ConfirmedPermission(wait.At.AddSeconds(2), "newer");
            new EventStore(workspace.Paths).Publish(newer);
            store.Drain();

            NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, await SharedClick("codex", AgentModes.SharedBackend), hideTimer: true);

            var session = store.Read().Sessions[wait.Key];
            Assert.Equal(newer.Id, session.Episode);
            Assert.Null(session.ViewedEpisode);
        }
    }

    [Fact]
    public void ALateSuccessForAnOlderEpisodeCannotHideANewerTimer()
    {
        var (store, workspace, wait) = Arrange();
        using (workspace)
        {
            new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Prompt, wait.At.AddSeconds(1)));
            var newer = TestWorkspace.ConfirmedPermission(wait.At.AddSeconds(2), "newer");
            new EventStore(workspace.Paths).Publish(newer);
            store.Drain();

            NavigationOutcome.ApplyViewed(store, wait.Key, wait.Id, new FocusResult(true, "exact terminal", "late"), hideTimer: true);

            var session = store.Read().Sessions[wait.Key];
            Assert.Equal(newer.Id, session.Episode);
            Assert.Null(session.ViewedEpisode);
        }
    }
}

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

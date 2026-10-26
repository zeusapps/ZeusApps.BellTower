using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

public sealed class ViewedTimerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoringAnOlderWaitPreservesItsViewedAndDismissedState(bool dismiss)
    {
        using var workspace = new TestWorkspace();
        var events = new EventStore(workspace.Paths);
        var store = new StateStore(workspace.Paths);
        var first = TestWorkspace.ConfirmedPermission(DateTimeOffset.UtcNow.AddMinutes(-1), "first");
        events.Publish(first);
        store.Drain();
        store.MarkViewed(first.Key, first.Id);
        if (dismiss)
        {
            store.Acknowledge(first.Key);
        }
        var second = TestWorkspace.ConfirmedPermission(first.At.AddSeconds(1), "second");
        events.Publish(second);
        store.Drain();
        store.MarkViewed(second.Key, second.Id);
        store.Acknowledge(second.Key);
        events.Publish(second with { Id = Guid.NewGuid().ToString("N"), Kind = EventKind.ToolEnd, At = second.At.AddSeconds(1) });

        var restored = new StateStore(workspace.Paths).Drain();
        var session = restored.Sessions[first.Key];
        Assert.Equal(first.Id, session.Episode);
        Assert.Equal(first.Id, session.ViewedEpisode);
        Assert.Equal(dismiss ? first.Id : null, session.AcknowledgedEpisode);
        if (dismiss)
        {
            Assert.Empty(Chips(workspace, restored));
        }
        else
        {
            Assert.Empty(Assert.Single(Chips(workspace, restored)).Elapsed);
        }
    }

    [Fact]
    public void ViewedWaitKeepsItsChipAndProviderStateButHidesItsTimerAcrossRestart()
    {
        using var workspace = new TestWorkspace();
        var events = new EventStore(workspace.Paths);
        var store = new StateStore(workspace.Paths);
        var wait = TestWorkspace.ConfirmedPermission(DateTimeOffset.UtcNow.AddMinutes(-1), "blocked");
        events.Publish(wait);
        events.Publish(wait with { Id = Guid.NewGuid().ToString("N"), Provider = "codex" });
        store.Drain();
        store.MarkViewed(wait.Key, wait.Id);

        var restored = new StateStore(workspace.Paths).Drain();
        var session = restored.Sessions[wait.Key];
        Assert.True(session.RequiresAttention(DateTimeOffset.UtcNow));
        Assert.Equal(AttentionReason.Permission, session.Reason);
        Assert.Equal(wait.At, session.Since);
        Assert.Null(session.AcknowledgedEpisode);
        var chips = Chips(workspace, restored);
        Assert.Equal(2, chips.Count);
        Assert.Empty(chips.Single(chip => chip.Key == wait.Key).Elapsed);
        Assert.NotEmpty(chips.Single(chip => chip.Key != wait.Key).Elapsed);
    }

    [Fact]
    public void DelayedNavigationCannotHideTheTimerOfANewerWait()
    {
        using var workspace = new TestWorkspace();
        var events = new EventStore(workspace.Paths);
        var store = new StateStore(workspace.Paths);
        var started = DateTimeOffset.UtcNow.AddMinutes(-1);
        var first = TestWorkspace.ConfirmedPermission(started, "first");
        events.Publish(first);
        store.Drain();
        store.MarkViewed(first.Key, first.Id);
        events.Publish(TestWorkspace.Event(EventKind.Prompt, started.AddSeconds(1)));
        var second = TestWorkspace.ConfirmedPermission(started.AddSeconds(2), "second");
        events.Publish(second);
        store.Drain();

        // The previous focus request finishes after the new episode has already arrived.
        store.MarkViewed(first.Key, first.Id);
        var restored = store.Read();
        var chip = Assert.Single(Chips(workspace, restored));
        Assert.Equal(second.Id, chip.Episode);
        Assert.NotEmpty(chip.Elapsed);
        Assert.True(restored.Sessions[first.Key].RequiresAttention(DateTimeOffset.UtcNow));
    }

    private static IReadOnlyList<ChipView> Chips(TestWorkspace workspace, AppState state) =>
        new SessionViewFactory(new IdentityStore(workspace.Paths), new AttachmentStore(workspace.Paths), new Navigator(workspace.Paths))
            .Chips(state.Sessions.Values, DateTimeOffset.UtcNow, new Dictionary<string, GitContext>());
}

using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

// The visibly unverified "possibly waiting for approval" chip for sessions without a supported confirmation signal.
public sealed class UnverifiedChipTests
{
    private static IReadOnlyList<ChipView> Chips(TestWorkspace workspace, SessionState session, DateTimeOffset now) =>
        new SessionViewFactory(new IdentityStore(workspace.Paths), new AttachmentStore(workspace.Paths), new Navigator(workspace.Paths))
            .Chips([session], now, new Dictionary<string, GitContext>());

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AnUnverifiedRequestShowsOnlyAfterFifteenSecondsAndNeverAsAConfirmedWait(string provider)
    {
        using var workspace = new TestWorkspace();
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start, "tool") with { Provider = provider, Directory = workspace.Root });
        var session = state.Sessions.Values.Single();
        session.ConfirmationSignalUnavailable = true;
        Assert.Empty(Chips(workspace, session, start.AddSeconds(14)));
        var chip = Assert.Single(Chips(workspace, session, start.AddSeconds(20)));
        Assert.Equal(AlertPolicy.UnverifiedText, chip.Reason);
        Assert.Equal("possibly waiting for approval", chip.Reason);
        Assert.NotEqual(AlertPolicy.ReasonText(AttentionReason.Permission), chip.Reason);
        Assert.True(chip.Unverified);
        Assert.Equal("00:00:20", chip.Elapsed);
        Assert.Contains("silent", chip.ToolTip);
        Assert.Empty(new AlertPolicy(start.AddSeconds(-1)).Collect([session], start.AddSeconds(20), false));
        // A confirmation turns it into the ordinary confirmed chip.
        SessionReducer.Apply(state, TestWorkspace.ConfirmedPermission(start.AddSeconds(21)) with { Provider = provider });
        var confirmed = Assert.Single(Chips(workspace, session, start.AddSeconds(30)));
        Assert.False(confirmed.Unverified);
        Assert.Equal(AlertPolicy.ReasonText(AttentionReason.Permission), confirmed.Reason);
    }

    [Fact]
    public void ADismissedOrResolvedUnverifiedChipDisappears()
    {
        using var workspace = new TestWorkspace();
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start, "tool") with { Provider = "codex", Directory = workspace.Root });
        var session = state.Sessions.Values.Single();
        session.ConfirmationSignalUnavailable = true;
        Assert.Single(Chips(workspace, session, start.AddSeconds(20)));
        session.AcknowledgedEpisode = session.Episode;
        Assert.Empty(Chips(workspace, session, start.AddSeconds(21)));
        session.AcknowledgedEpisode = null;
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(22), "tool") with { Provider = "codex" });
        Assert.Empty(Chips(workspace, session, start.AddSeconds(23)));
    }
}

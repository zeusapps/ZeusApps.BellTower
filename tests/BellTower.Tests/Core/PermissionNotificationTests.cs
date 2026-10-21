using System.Text.Json;
using BellTower.Core;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class PermissionNotificationTests
{
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void SequentialApprovedCommandsReceiveIndependentNotifications(string provider)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start);
        var attachment = new Attachment("attachment", provider, null, new(), "D:/example", start);
        var offset = 0;
        foreach (var command in new[] { "git add openspec", "git commit -m example" })
        {
            var operation = "tool-" + offset;
            Apply("PreToolUse", new { command }, operation);
            Apply("PermissionRequest", new { command, description = "Approval for " + command }, null);
            var session = state.Sessions.Values.Single();
            Assert.Single(session.Waits);
            Assert.Single(policy.Collect([session], start.AddSeconds(offset), false));
            session.ViewedEpisode = session.Episode;
            Apply("PostToolUse", new { command }, operation);
            Assert.Empty(session.Waits);
        }

        void Apply(string hook, object input, string? operation)
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                session_id = "test",
                hook_event_name = hook,
                tool_name = "Bash",
                tool_input = input,
                tool_use_id = operation
            }));
            var observation = HookAdapter.Parse(provider, json.RootElement, attachment, start.AddSeconds(++offset))!;
            SessionReducer.Apply(state, observation);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DistinctPermissionNotifiesEvenWhenAnotherWaitIsViewedOrDismissed(bool dismissed)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start);
        var first = TestWorkspace.Event(EventKind.Permission, start.AddSeconds(1), "first");
        SessionReducer.Apply(state, first);
        var session = state.Sessions[first.Key];
        Assert.Single(policy.Collect([session], start.AddSeconds(1), false));
        session.ViewedEpisode = first.Id;
        session.AcknowledgedEpisode = dismissed ? first.Id : null;

        var second = TestWorkspace.Event(EventKind.Permission, start.AddSeconds(2), "second");
        SessionReducer.Apply(state, second);
        Assert.Equal(2, session.Waits.Count);
        Assert.Equal(second.Id, session.Episode);
        Assert.NotEqual(session.ViewedEpisode, session.Episode);
        Assert.True(session.RequiresAttention(start.AddSeconds(2)));
        Assert.Single(policy.Collect([session], start.AddSeconds(2), false));
        SessionReducer.Apply(state, second with { Id = Guid.NewGuid().ToString("N"), At = start.AddSeconds(3) });
        Assert.Equal(second.Id, session.Episode);
        Assert.Empty(policy.Collect([session], start.AddSeconds(3), false));
    }

    [Fact]
    public void ANewPermissionAfterRestartNotifiesEvenWithAnOlderUnresolvedWait()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddMinutes(-1), "old"));
        var restored = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state))!;
        var session = restored.Sessions.Values.Single();
        var policy = new AlertPolicy(start);
        Assert.Empty(policy.Collect([session], start, false));
        SessionReducer.Apply(restored, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(1), "new"));
        Assert.Single(policy.Collect([session], start.AddSeconds(1), false));
    }
}

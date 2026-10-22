using System.Text.Json;
using BellTower.Core;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class PermissionNotificationTests
{
    [Theory]
    [InlineData("claude", "PreToolUse", 5)]
    [InlineData("claude", "PostToolUse", 12)]
    [InlineData("codex", "PreToolUse", 5)]
    [InlineData("codex", "PostToolUse", 12)]
    public void PermissionResolvedDuringGraceNeverCreatesAChipOrSound(string provider, string resolution, int resolvedAt)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var attachment = new Attachment("attachment", provider, null, new(), "D:/example", start);
        Apply("PreToolUse", 0, "tool");
        Apply("PermissionRequest", 1, null);
        var session = state.Sessions.Values.Single();
        Assert.False(session.RequiresAttention(start.AddSeconds(5)));
        Assert.Empty(policy.Collect([session], start.AddSeconds(5), false));
        Assert.False(session.RequiresAttention(start.AddSeconds(resolvedAt - 1)));
        Assert.Empty(policy.Collect([session], start.AddSeconds(resolvedAt - 1), false));
        Apply(resolution, resolvedAt, "tool");
        Assert.Empty(session.Waits);
        Assert.False(session.RequiresAttention(start.AddSeconds(60)));
        Assert.Empty(policy.Collect([session], start.AddSeconds(60), false));

        void Apply(string hook, int offset, string? operation)
        {
            using var input = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                session_id = "test",
                hook_event_name = hook,
                tool_name = "Bash",
                tool_input = new { command = "example command" },
                tool_use_id = operation
            }));
            SessionReducer.Apply(state, HookAdapter.Parse(provider, input.RootElement, attachment, start.AddSeconds(offset))!);
        }
    }

    [Fact]
    public void DuplicateObservationsDoNotExtendPermissionGrace()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var permission = TestWorkspace.Event(EventKind.Permission, start, "tool");
        SessionReducer.Apply(state, permission);
        var session = state.Sessions[permission.Key];
        SessionReducer.Apply(state, permission with { Id = "duplicate", At = start.AddSeconds(4) });
        Assert.Empty(policy.Collect([session], start.AddSeconds(14), false));
        Assert.Equal(start.AddSeconds(15), session.NotificationDue);
        Assert.Single(policy.Collect([session], start.AddSeconds(15), false));
        Assert.Empty(policy.Collect([session], start.AddSeconds(60), false));
        Assert.Equal(start, session.Since);
    }

    [Fact]
    public void AReplacementRequestGetsItsOwnFullGrace()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var first = TestWorkspace.Event(EventKind.Permission, start, "first");
        SessionReducer.Apply(state, first);
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(4), "first"));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(4), "second"));
        var session = state.Sessions[first.Key];
        Assert.False(session.RequiresAttention(start.AddSeconds(5)));
        Assert.Empty(policy.Collect([session], start.AddSeconds(18), false));
        Assert.Single(policy.Collect([session], start.AddSeconds(19), false));
    }

    [Fact]
    public void AutoApprovedNewRequestCannotReannounceAnOlderWait()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start);
        var first = TestWorkspace.Event(EventKind.Permission, start.AddMinutes(-1), "old");
        SessionReducer.Apply(state, first);
        var session = state.Sessions[first.Key];
        Assert.Empty(policy.Collect([session], start, false));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(1), "new"));
        Assert.Empty(policy.Collect([session], start.AddSeconds(2), false));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(3), "new"));
        Assert.True(session.RequiresAttention(start.AddSeconds(10)));
        Assert.Single(session.Waits);
        Assert.Equal(first.Id, session.Episode);
        Assert.Empty(policy.Collect([session], start.AddSeconds(10), false));
    }

    [Fact]
    public void AutoApprovedParallelRequestDoesNotSilenceAnUnannouncedPermission()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var first = TestWorkspace.Event(EventKind.Permission, start, "first");
        SessionReducer.Apply(state, first);
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(1), "second"));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(2), "second"));
        var session = state.Sessions[first.Key];
        Assert.Equal(first.Id, session.Episode);
        Assert.Equal(start.AddSeconds(15), session.NotificationDue);
        Assert.Empty(policy.Collect([session], start.AddSeconds(14), false));
        Assert.Single(policy.Collect([session], start.AddSeconds(15), false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestartRetainsRemainingPermissionGraceIncludingLegacySnapshots(bool legacy)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var first = TestWorkspace.Event(EventKind.Permission, start, "first");
        SessionReducer.Apply(state, first);
        if (legacy)
        {
            var session = state.Sessions[first.Key];
            session.Waits[0] = session.Waits[0] with { Episode = null };
        }
        var restored = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state))!;
        var wait = restored.Sessions[first.Key];
        var policy = new AlertPolicy(start.AddSeconds(2));
        Assert.False(wait.RequiresAttention(start.AddSeconds(14)));
        Assert.Empty(policy.Collect([wait], start.AddSeconds(14), false));
        Assert.Single(policy.Collect([wait], start.AddSeconds(15), false));
        Assert.Empty(new AlertPolicy(start.AddSeconds(16)).Collect([wait], start.AddSeconds(16), false));
    }

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
            Assert.Empty(policy.Collect([session], start.AddSeconds(offset + 14), false));
            offset += 15;
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
        Assert.Single(policy.Collect([session], start.AddSeconds(16), false));
        session.ViewedEpisode = first.Id;
        session.AcknowledgedEpisode = dismissed ? first.Id : null;

        var second = TestWorkspace.Event(EventKind.Permission, start.AddSeconds(20), "second");
        SessionReducer.Apply(state, second);
        Assert.Equal(2, session.Waits.Count);
        Assert.Equal(second.Id, session.Episode);
        Assert.NotEqual(session.ViewedEpisode, session.Episode);
        Assert.True(session.RequiresAttention(start.AddSeconds(20)));
        Assert.Empty(policy.Collect([session], start.AddSeconds(34), false));
        Assert.Single(policy.Collect([session], start.AddSeconds(35), false));
        SessionReducer.Apply(state, second with { Id = Guid.NewGuid().ToString("N"), At = start.AddSeconds(36) });
        Assert.Equal(second.Id, session.Episode);
        Assert.Empty(policy.Collect([session], start.AddSeconds(36), false));
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
        Assert.Empty(policy.Collect([session], start.AddSeconds(1), false));
        Assert.Single(policy.Collect([session], start.AddSeconds(16), false));
    }
}

using System.Text.Json;
using BellTower.Core;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

// Human-wait confirmation policy (confirm-claude-human-permission-waits). Approval-flow starts are provisional; only a
// supported human-wait signal (Claude permission_prompt, Codex owning-runtime status) makes a permission wait attention.
// All inputs are fixtures, not live provider sessions.
public sealed class PermissionNotificationTests
{
    private static readonly TimeSpan ClaudeDelay = TimeSpan.FromSeconds(5);

    private static Observation Request(string provider, DateTimeOffset at, string? operation, string fingerprint = "command") =>
        TestWorkspace.Event(EventKind.Permission, at, operation) with { Provider = provider, OperationFingerprint = fingerprint };

    private static Observation Prompt(string provider, DateTimeOffset at, TimeSpan? delay = null) =>
        TestWorkspace.Event(EventKind.Permission, at) with { Provider = provider, HumanPrompt = true, ProviderDelay = delay ?? ClaudeDelay };

    private static Observation Resolve(string provider, DateTimeOffset at, string? operation, string fingerprint = "command") =>
        TestWorkspace.Event(EventKind.ToolEnd, at, operation) with { Provider = provider, OperationFingerprint = fingerprint };

    // Provider adapter stand-in: Claude confirms through its notification; Codex through the owning-runtime status the pump applies.
    private static void Confirm(AppState state, string provider, DateTimeOffset at, SessionState session)
    {
        if (provider == "claude")
        {
            SessionReducer.Apply(state, Prompt(provider, at));
        }
        else
        {
            session.NativePermissionPending = true;
            session.NativePermissionSince = at;
        }
    }

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

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AutomaticReviewLongerThanFifteenSecondsWithoutConfirmationNeverBecomesPermissionAttention(string provider)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Request(provider, start, "tool"));
        var session = state.Sessions.Values.Single();
        foreach (var second in new[] { 5, 15, 16, 40, 119 })
        {
            Assert.Null(session.Reason);
            Assert.False(session.RequiresAttention(start.AddSeconds(second)));
            Assert.Empty(policy.Collect([session], start.AddSeconds(second), false));
            Assert.Null(session.NotificationDue);
        }
        var wait = Assert.Single(session.Waits);
        Assert.Equal(HumanConfirmation.Provisional, wait.Confirmation);
        SessionReducer.Apply(state, Resolve(provider, start.AddSeconds(120), "tool"));
        Assert.Empty(session.Waits);
        Assert.Empty(policy.Collect([session], start.AddSeconds(121), false));
    }

    [Fact]
    public void ConfirmedClaudePromptIsEligibleOnArrivalWithoutAnExtraDebounceAndKeepsHonestElapsedTime()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Request("claude", start, null));
        var arrival = start.AddMilliseconds(6274);
        var prompt = Prompt("claude", arrival);
        Assert.Empty(policy.Collect([state.Sessions.Values.Single()], arrival.AddMilliseconds(-1), false));
        SessionReducer.Apply(state, prompt);
        var session = state.Sessions.Values.Single();
        Assert.Single(session.Waits);
        Assert.Equal(start, session.Since);
        Assert.Equal(arrival, session.Due);
        Assert.True(session.RequiresAttention(arrival));
        Assert.Equal("00:00:06", AlertPolicy.Elapsed(session.Since!.Value, arrival));
        Assert.Single(policy.Collect([session], arrival, false));
        SessionReducer.Apply(state, prompt with { Id = "duplicate", At = arrival.AddSeconds(3) });
        Assert.Equal(arrival, session.Due);
        Assert.Empty(policy.Collect([session], arrival.AddSeconds(4), false));
    }

    [Fact]
    public void ConfirmationOnlyWaitStartsAtTheNotificationAndTheSharedGraceCountsAnyElapsedProviderDelay()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Prompt("claude", start));
        var session = state.Sessions.Values.Single();
        Assert.Equal(start, session.Since);
        Assert.Equal(HumanConfirmation.Confirmed, Assert.Single(session.Waits).Confirmation);
        Assert.True(session.RequiresAttention(start));

        // A provider signal with no credited delay gets the shared five-second grace, reduced by the elapsed time already seen.
        var other = new AppState();
        SessionReducer.Apply(other, Request("claude", start, null));
        SessionReducer.Apply(other, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(2)) with { HumanPrompt = true });
        var credited = other.Sessions.Values.Single();
        Assert.Equal(start.AddSeconds(5), credited.Due);
        Assert.False(credited.RequiresAttention(start.AddSeconds(4)));
        Assert.True(credited.RequiresAttention(start.AddSeconds(5)));
    }

    [Fact]
    public void CodexNativeConfirmationKeepsItsFiveSecondGraceFromTheRuntimeReport()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Request("codex", start, "tool"));
        var session = state.Sessions.Values.Single();
        session.NativePermissionPending = true;
        session.NativePermissionSince = start.AddSeconds(2);
        Assert.Equal(start.AddSeconds(7), session.Due);
        Assert.Empty(policy.Collect([session], start.AddSeconds(6), false));
        Assert.Single(policy.Collect([session], start.AddSeconds(7), false));
        // The runtime later reports no human approval: the provisional request stays quiet without being answered or cleared.
        session.NativePermissionPending = false;
        Assert.False(session.RequiresAttention(start.AddSeconds(30)));
        Assert.Single(session.Waits);
    }

    [Fact]
    public void DuplicateObservationsDoNotExtendPermissionGrace()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var permission = TestWorkspace.ConfirmedPermission(start, "tool");
        SessionReducer.Apply(state, permission);
        var session = state.Sessions[permission.Key];
        SessionReducer.Apply(state, permission with { Id = "duplicate", At = start.AddSeconds(4) });
        Assert.Equal(start, session.NotificationDue);
        Assert.Single(policy.Collect([session], start, false));
        Assert.Empty(policy.Collect([session], start.AddSeconds(60), false));
        Assert.Equal(start, session.Since);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AReplacementRequestNeedsItsOwnConfirmation(string provider)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Request(provider, start, "first"));
        var session = state.Sessions.Values.Single();
        Confirm(state, provider, start.AddSeconds(6), session);
        Assert.Single(policy.Collect([session], start.AddSeconds(11), false));
        SessionReducer.Apply(state, Resolve(provider, start.AddSeconds(12), "first"));
        session.NativePermissionPending = null;
        session.NativePermissionSince = null;
        SessionReducer.Apply(state, Request(provider, start.AddSeconds(13), "second", "other"));
        Assert.False(session.RequiresAttention(start.AddSeconds(60)));
        Assert.Empty(policy.Collect([session], start.AddSeconds(60), false));
        Confirm(state, provider, start.AddSeconds(61), session);
        Assert.Single(policy.Collect([session], start.AddSeconds(67), false));
    }

    [Fact]
    public void AutoApprovedNewRequestCannotReannounceAnOlderWait()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start);
        var first = TestWorkspace.ConfirmedPermission(start.AddMinutes(-1), "old");
        SessionReducer.Apply(state, first);
        var session = state.Sessions[first.Key];
        Assert.Empty(policy.Collect([session], start, false));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(1), "new"));
        Assert.Empty(policy.Collect([session], start.AddSeconds(30), false));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(31), "new"));
        Assert.True(session.RequiresAttention(start.AddSeconds(32)));
        Assert.Single(session.Waits);
        Assert.Equal(first.Id, session.Episode);
        Assert.Empty(policy.Collect([session], start.AddSeconds(32), false));
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
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(7)) with { HumanPrompt = true, ProviderDelay = ClaudeDelay });
        var session = state.Sessions[first.Key];
        Assert.Equal(first.Id, session.Episode);
        Assert.Equal(start.AddSeconds(7), session.NotificationDue);
        Assert.Empty(policy.Collect([session], start.AddSeconds(6), false));
        Assert.Single(policy.Collect([session], start.AddSeconds(7), false));
    }

    [Fact]
    public void RestartRestoresConfirmedAttentionSilentlyAndLegacyTimingOnlyWaitsAsUnverified()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var first = Request("claude", start.AddMinutes(-2), "first");
        SessionReducer.Apply(state, first);
        SessionReducer.Apply(state, Prompt("claude", start.AddMinutes(-2).AddSeconds(6)));
        var restored = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state))!.Sessions[first.Key];
        Assert.True(restored.RequiresAttention(start));
        Assert.Empty(new AlertPolicy(start).Collect([restored], start, false));
        Assert.Equal(first.Id, restored.Episode);

        // A pre-change snapshot has no confirmation field: it is unverified, never newly confirmed or announced.
        var legacy = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state))!;
        var session = legacy.Sessions[first.Key];
        session.Waits[0] = session.Waits[0] with { Confirmation = null, ConfirmedAt = null, ConfirmationCredit = null };
        var reloaded = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(legacy))!.Sessions[first.Key];
        Assert.Null(reloaded.Waits[0].Confirmation);
        Assert.False(reloaded.RequiresAttention(start));
        Assert.Null(reloaded.Reason);
        Assert.Empty(new AlertPolicy(start.AddMinutes(-3)).Collect([reloaded], start, false));
        Assert.Single(reloaded.Waits);
    }

    [Fact]
    public void WaitStateRoundTripPreservesUnknownFieldsAndOldSnapshotsReadAsUnverified()
    {
        var current = JsonSerializer.Serialize(new WaitState(AttentionReason.Permission, DateTimeOffset.UtcNow, "op", null,
            Confirmation: HumanConfirmation.Confirmed), BellTower.IO.Json.Options);
        var future = current.TrimEnd('}') + ",\"futureField\":{\"nested\":[1,2]}}";
        var read = JsonSerializer.Deserialize<WaitState>(future, BellTower.IO.Json.Options)!;
        Assert.Equal(HumanConfirmation.Confirmed, read.Confirmation);
        Assert.Contains("futureField", JsonSerializer.Serialize(read, BellTower.IO.Json.Options));
        var old = JsonSerializer.Deserialize<WaitState>("{\"reason\":\"Permission\",\"since\":\"2026-10-26T00:00:00+00:00\",\"operationId\":\"op\"}", BellTower.IO.Json.Options)!;
        Assert.Null(old.Confirmation);
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
            if (provider == "claude")
            {
                ApplyNotification();
            }
            else
            {
                session.NativePermissionPending = true;
                session.NativePermissionSince = start.AddSeconds(offset);
                offset += 5;
            }
            Assert.Single(policy.Collect([session], start.AddSeconds(offset), false));
            session.ViewedEpisode = session.Episode;
            session.NativePermissionPending = null;
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

        void ApplyNotification()
        {
            using var json = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"Notification","notification_type":"permission_prompt","message":"Claude needs your permission"}""");
            SessionReducer.Apply(state, HookAdapter.Parse(provider, json.RootElement, attachment, start.AddSeconds(offset))!);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DistinctConfirmedPermissionNotifiesEvenWhenAnotherWaitIsViewedOrDismissed(bool dismissed)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start);
        var first = TestWorkspace.ConfirmedPermission(start.AddSeconds(1), "first");
        SessionReducer.Apply(state, first);
        var session = state.Sessions[first.Key];
        Assert.Single(policy.Collect([session], start.AddSeconds(2), false));
        session.ViewedEpisode = first.Id;
        session.AcknowledgedEpisode = dismissed ? first.Id : null;

        var second = TestWorkspace.Event(EventKind.Permission, start.AddSeconds(20), "second");
        SessionReducer.Apply(state, second);
        Assert.Equal(2, session.Waits.Count);
        Assert.Equal(first.Id, session.Episode);
        Assert.Empty(policy.Collect([session], start.AddSeconds(60), false));
        // The second request is confirmed by its own prompt (the first is already confirmed, so the prompt is parallel-ambiguous
        // only to the unconfirmed candidate set); it then owns a new episode and notifies exactly once.
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(26)) with { HumanPrompt = true, ProviderDelay = ClaudeDelay });
        Assert.Equal(second.Id, session.Episode);
        Assert.NotEqual(session.ViewedEpisode, session.Episode);
        Assert.True(session.RequiresAttention(start.AddSeconds(26)));
        Assert.Single(policy.Collect([session], start.AddSeconds(26), false));
        SessionReducer.Apply(state, second with { Id = Guid.NewGuid().ToString("N"), At = start.AddSeconds(36) });
        Assert.Equal(second.Id, session.Episode);
        Assert.Empty(policy.Collect([session], start.AddSeconds(36), false));
    }

    [Fact]
    public void ANewConfirmedPermissionAfterRestartNotifiesEvenWithAnOlderUnresolvedWait()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.ConfirmedPermission(start.AddMinutes(-1), "old"));
        var restored = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state))!;
        var session = restored.Sessions.Values.Single();
        var policy = new AlertPolicy(start);
        Assert.Empty(policy.Collect([session], start, false));
        SessionReducer.Apply(restored, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(1), "new"));
        Assert.Empty(policy.Collect([session], start.AddSeconds(20), false));
        SessionReducer.Apply(restored, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(21)) with { HumanPrompt = true, ProviderDelay = ClaudeDelay });
        Assert.Single(policy.Collect([session], start.AddSeconds(21), false));
    }

    [Theory]
    [InlineData("unattributed", "resolved")]
    [InlineData("unattributed", "stop")]
    [InlineData("unattributed", "interrupt")]
    [InlineData("unattributed", "end")]
    [InlineData("unattributed", "prompt")]
    [InlineData("attributed", "resolved")]
    public void LateConfirmationNeverResurrectsAResolvedWait(string attribution, string boundary)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolStart, start, "tool") with { OperationFingerprint = "command" });
        SessionReducer.Apply(state, Request("claude", start.AddSeconds(1), null));
        var session = state.Sessions.Values.Single();
        switch (boundary)
        {
            case "resolved":
                SessionReducer.Apply(state, Resolve("claude", start.AddSeconds(3), "tool"));
                break;
            case "stop":
                SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Stop, start.AddSeconds(3)));
                break;
            case "interrupt":
                SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Interrupt, start.AddSeconds(3)));
                break;
            case "end":
                SessionReducer.Apply(state, TestWorkspace.Event(EventKind.End, start.AddSeconds(3)));
                break;
            default:
                SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Prompt, start.AddSeconds(3), turn: "turn-2"));
                break;
        }
        // A late notification that precedes the new prompt in time cannot belong to the new turn.
        var late = Prompt("claude", start.AddSeconds(boundary == "prompt" ? 2 : 7)) with
        {
            OperationId = attribution == "attributed" ? "tool" : null,
            OperationFingerprint = attribution == "attributed" ? "command" : null
        };
        SessionReducer.Apply(state, late);
        SessionReducer.Apply(state, late with { Id = "duplicate" });
        Assert.DoesNotContain(session.Waits, wait => wait.Reason == AttentionReason.Permission);
        Assert.False(session.RequiresAttention(start.AddSeconds(30)) && session.Reason == AttentionReason.Permission);
        Assert.DoesNotContain(policy.Collect([session], start.AddSeconds(30), false), item => item.Reason == AttentionReason.Permission);
    }

    [Fact]
    public void ReplacementAttachmentRejectsAConfirmationForTheOldAttachment()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Request("claude", start, "tool"));
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Register, start.AddSeconds(1)) with { AttachmentId = "replacement" });
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(7)));
        var session = state.Sessions.Values.Single();
        Assert.Equal("replacement", session.AttachmentId);
        Assert.Empty(session.Waits);
    }

    [Fact]
    public void ShuffledAndDuplicatedDeliveryNeverResurrectsACompletedWait()
    {
        var start = DateTimeOffset.UtcNow;
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var events = new[]
        {
            TestWorkspace.Event(EventKind.ToolStart, start, "tool") with { OperationFingerprint = "command", IngressAt = start },
            Request("claude", start.AddSeconds(1), null) with { IngressAt = start.AddSeconds(1) },
            Prompt("claude", start.AddSeconds(6)) with { IngressAt = start.AddSeconds(6) },
            Resolve("claude", start.AddSeconds(8), "tool") with { IngressAt = start.AddSeconds(8) }
        };
        foreach (var order in new[] { new[] { 3, 2, 1, 0 }, new[] { 2, 3, 0, 1 }, new[] { 1, 3, 2, 0, 3, 2 } })
        {
            var state = new AppState();
            foreach (var index in order)
            {
                SessionReducer.Apply(state, events[index] with { Id = events[index].Id + (index == order[^1] ? "x" : "") });
            }
            var session = state.Sessions.Values.Single();
            Assert.Empty(session.Waits);
            Assert.Empty(policy.Collect([session], start.AddSeconds(60), false));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameFingerprintParallelRequestsAreNeverArbitrarilyAssignedOrResolved(bool withToolIds)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        if (withToolIds)
        {
            SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolStart, start, "one") with { OperationFingerprint = "same" });
            SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(1), "two") with { OperationFingerprint = "same" });
        }
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(2)) with { OperationFingerprint = "same" });
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, start.AddSeconds(3)) with { OperationFingerprint = "same" });
        var session = state.Sessions.Values.Single();
        Assert.Null(session.Reason);
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(4), "unrelated"));
        Assert.NotEmpty(session.Waits);
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(8)));
        Assert.Equal(AttentionReason.Permission, session.Reason);
        Assert.All(session.Waits, wait => Assert.Equal(HumanConfirmation.Confirmed, wait.Confirmation));
        Assert.Single(policy.Collect([session], start.AddSeconds(8), false));
        if (withToolIds)
        {
            SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(9), "one") with { OperationFingerprint = "same" });
            Assert.Equal(AttentionReason.Permission, session.Reason);
            SessionReducer.Apply(state, TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(10), "two") with { OperationFingerprint = "same" });
            Assert.Null(session.Reason);
        }
    }

    [Fact]
    public void PartialParallelResolutionKeepsOnlyTheOtherRequestWithItsOwnDeadlineAndReceipts()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Request("claude", start, "one", "alpha"));
        SessionReducer.Apply(state, Request("claude", start.AddSeconds(1), "two", "beta"));
        SessionReducer.Apply(state, Resolve("claude", start.AddSeconds(2), "one", "alpha"));
        var session = state.Sessions.Values.Single();
        Assert.Single(session.Waits);
        // The late prompt confirms the only remaining request; the resolved one stays resolved.
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(7)));
        Assert.Equal("two", Assert.Single(session.Waits).OperationId);
        Assert.Equal(start.AddSeconds(7), session.Due);
        Assert.Single(policy.Collect([session], start.AddSeconds(7), false));
        session.ViewedEpisode = session.Episode;
        SessionReducer.Apply(state, Resolve("claude", start.AddSeconds(9), "one", "alpha"));
        Assert.Single(session.Waits);
        Assert.Equal(session.Episode, session.ViewedEpisode);
    }

    [Fact]
    public void DeniedOrInterruptedFlowsClearAndRejectTheirLateNotification()
    {
        var start = DateTimeOffset.UtcNow;
        var attachment = new Attachment("attachment", "claude", null, new(), "D:/example", start);
        var state = new AppState();
        using (var request = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"PermissionRequest","tool_name":"Bash","tool_input":{"command":"example"}}"""))
        {
            SessionReducer.Apply(state, HookAdapter.Parse("claude", request.RootElement, attachment, start)!);
        }
        using (var denied = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"PermissionDenied","tool_name":"Bash","tool_input":{"command":"example"}}"""))
        {
            SessionReducer.Apply(state, HookAdapter.Parse("claude", denied.RootElement, attachment, start.AddSeconds(2))!);
        }
        using (var notification = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"Notification","notification_type":"permission_prompt"}"""))
        {
            SessionReducer.Apply(state, HookAdapter.Parse("claude", notification.RootElement, attachment, start.AddSeconds(6))!);
        }
        Assert.Empty(state.Sessions.Values.Single().Waits);
    }

    [Fact]
    public void SubagentStopDoesNotClearAMainThreadHumanWait()
    {
        var start = DateTimeOffset.UtcNow;
        var attachment = new Attachment("attachment", "claude", null, new(), "D:/example", start);
        var state = new AppState();
        SessionReducer.Apply(state, Request("claude", start, "main"));
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(6)));
        using var stop = JsonDocument.Parse("""{"session_id":"session-1","hook_event_name":"Stop","agent_id":"sub-1"}""");
        var subagentStop = HookAdapter.Parse("claude", stop.RootElement, attachment, start.AddSeconds(7))! with { AttachmentId = "attachment-1" };
        Assert.False(subagentStop.IsMainThread);
        SessionReducer.Apply(state, subagentStop);
        Assert.Equal(AttentionReason.Permission, state.Sessions.Values.Single().Reason);
    }

    [Fact]
    public void MixedClaudeAndCodexSessionsKeepIndependentConfirmationAndEpisodes()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Request("claude", start, "c-tool"));
        SessionReducer.Apply(state, Request("codex", start.AddSeconds(1), "x-tool"));
        var claude = state.Sessions["claude:session-1"];
        var codex = state.Sessions["codex:session-1"];
        Assert.Empty(policy.Collect([claude, codex], start.AddSeconds(30), false));
        // Only the Claude session is confirmed; the Codex request is still provisional.
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(6)));
        Assert.Equal(AttentionReason.Permission, claude.Reason);
        Assert.Null(codex.Reason);
        Assert.Equal([claude], policy.Collect([claude, codex], start.AddSeconds(31), false));
        codex.NativePermissionPending = true;
        codex.NativePermissionSince = start.AddSeconds(32);
        Assert.Equal([codex], policy.Collect([claude, codex], start.AddSeconds(37), false));
        Assert.NotEqual(claude.Episode, codex.Episode);
    }
}

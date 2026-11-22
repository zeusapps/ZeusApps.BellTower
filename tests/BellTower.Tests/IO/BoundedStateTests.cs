using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

// Bounded monitoring state: settled ended sessions are compacted, ended sessions are capped, a save over the budget removes
// the oldest ended sessions, and an existing state at the budget recovers on one drain. Both providers share every policy,
// so each scenario runs for Claude Code and Codex and the mixed ones run both together. Fixtures are synthetic.
public sealed class BoundedStateTests
{
    private static readonly string[] Providers = ["claude", "codex"];

    private static Observation Observe(string provider, string session, EventKind kind, DateTimeOffset at, string? operation = null) => new()
    {
        Provider = provider,
        SessionId = session,
        AttachmentId = "attachment-" + session,
        Kind = kind,
        At = at,
        IngressAt = at,
        TurnId = "turn-1",
        OperationId = operation,
        Directory = "D:/synthetic/" + session,
        AdapterRevision = Observation.HumanWaitRevision
    };

    // An ended session with a full replay journal, as the earlier version left it. The last event is at endedAt.
    private static void EndedSession(AppState state, string provider, string session, DateTimeOffset endedAt, int events = CausalSessionReducer.Window + 4)
    {
        var at = endedAt.AddMilliseconds(-events);
        SessionReducer.Apply(state, Observe(provider, session, EventKind.Register, at));
        for (var i = 1; i < events - 2; i += 2)
        {
            SessionReducer.Apply(state, Observe(provider, session, EventKind.ToolStart, at.AddMilliseconds(i), "op-" + i));
            SessionReducer.Apply(state, Observe(provider, session, EventKind.ToolEnd, at.AddMilliseconds(i + 1), "op-" + i));
        }
        SessionReducer.Apply(state, Observe(provider, session, EventKind.Stop, endedAt.AddMilliseconds(-1)));
        SessionReducer.Apply(state, Observe(provider, session, EventKind.End, endedAt));
    }

    private static void LiveSession(AppState state, string provider, string session, DateTimeOffset at, int events = 10)
    {
        SessionReducer.Apply(state, Observe(provider, session, EventKind.Register, at));
        for (var i = 1; i < events; i++)
        {
            SessionReducer.Apply(state, Observe(provider, session, EventKind.Prompt, at.AddMilliseconds(i)));
        }
    }

    private static SessionState Plain(string provider, string session, DateTimeOffset lastObserved, bool ended) => new()
    {
        Key = provider + ":" + session,
        Provider = provider,
        SessionId = session,
        AttachmentId = "attachment-" + session,
        LastObserved = lastObserved,
        Ended = ended
    };

    // The displayed state: everything except the replay history that compaction is allowed to drop.
    private static string Displayed(SessionState session)
    {
        var copy = Json.Decode<SessionState>(Json.Encode(session));
        copy.CausalEvents = [];
        copy.CausalCheckpoint = null;
        copy.CausalFloor = null;
        copy.CompletedOperations = [];
        copy.Operations = [];
        return Json.Encode(copy);
    }

    private static void Persist(TestWorkspace workspace, AppState state) => new StateSnapshotStore(workspace.Paths, long.MaxValue).Save(state);

    private static string QueueCapture(TestWorkspace workspace, string provider, string session, DateTimeOffset at)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = session,
            cwd = "D:/synthetic/" + session,
            hook_event_name = "UserPromptSubmit",
            prompt = "synthetic"
        }));
        var envelope = CaptureFactory.Create(provider, document.RootElement,
            new("capture-attachment-" + session, provider, null, new(), "D:/synthetic/" + session, DateTimeOffset.UtcNow), at)!;
        return CaptureSpool.Write(workspace.Paths.Root, envelope);
    }

    private static string[] Queued(TestWorkspace workspace) =>
        Directory.GetFiles(Path.Combine(workspace.Paths.Root, CaptureProtocol.InboxName), "*.json");

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void SettledEndedSessionKeepsItsDisplayedStateButNoReplayHistory(string provider)
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var endedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        EndedSession(state, provider, "settled", endedAt);
        var before = state.Sessions[provider + ":settled"];
        Assert.Equal(CausalSessionReducer.Window, before.CausalEvents.Count);
        Assert.NotNull(before.CausalCheckpoint);
        // The drain reconciles ended sessions to this health; it is not part of compaction.
        before.AttachmentHealth = "confirmed-ended";
        var shown = Displayed(before);
        Persist(workspace, state);

        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":settled"];

        Assert.True(session.Ended);
        Assert.Empty(session.CausalEvents);
        Assert.Null(session.CausalCheckpoint);
        Assert.Empty(session.CompletedOperations);
        Assert.Empty(session.Operations);
        Assert.Equal(endedAt, session.CausalFloor!.At);
        Assert.Equal(shown, Displayed(session));
        // The compacted state is what was persisted, and it is valid for the shared reader.
        Assert.Empty(new StateStore(workspace.Paths).Read().Sessions[provider + ":settled"].CausalEvents);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void SessionWithinTheSettleIntervalAndLiveSessionsAreNotCompacted(string provider)
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        EndedSession(state, provider, "recent", DateTimeOffset.UtcNow.AddMinutes(-2));
        LiveSession(state, provider, "live", DateTimeOffset.UtcNow.AddHours(-3));
        Persist(workspace, state);

        var drained = new StateStore(workspace.Paths).Drain();

        Assert.NotEmpty(drained.Sessions[provider + ":recent"].CausalEvents);
        Assert.NotNull(drained.Sessions[provider + ":recent"].CausalCheckpoint);
        Assert.NotEmpty(drained.Sessions[provider + ":live"].CausalEvents);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void DelayedEarlierEventAfterCompactionLeavesTheSessionEndedWithoutAttention(string provider)
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var endedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        EndedSession(state, provider, "settled", endedAt);
        Persist(workspace, state);
        var drained = new StateStore(workspace.Paths).Drain();

        var earlier = Observe(provider, "settled", EventKind.Permission, endedAt.AddSeconds(-1), "late") with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) };
        SessionReducer.Apply(drained, earlier);
        // The same event delivered without an ingress stamp takes the direct path and must also be ignored.
        SessionReducer.Apply(drained, earlier with { Id = Guid.NewGuid().ToString("N"), IngressAt = null });

        var session = drained.Sessions[provider + ":settled"];
        Assert.True(session.Ended);
        Assert.Empty(session.Waits);
        Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow));
        Assert.Null(session.Reason);
    }

    // The resumed client may keep its attachment or arrive with a new one; both must be tracked after compaction.
    [Theory]
    [InlineData("claude", false)]
    [InlineData("codex", false)]
    [InlineData("claude", true)]
    [InlineData("codex", true)]
    public void ResumedSessionAfterCompactionIsLiveAndItsApprovalIsOrderedAndTracked(string provider, bool newAttachment)
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var endedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        EndedSession(state, provider, "settled", endedAt);
        Persist(workspace, state);
        var drained = new StateStore(workspace.Paths).Drain();
        var resumed = DateTimeOffset.UtcNow.AddMinutes(-5);
        Observation After(EventKind kind, DateTimeOffset at, string? operation = null) =>
            Observe(provider, "settled", kind, at, operation) with { AttachmentId = newAttachment ? "attachment-resumed" : "attachment-settled" };

        SessionReducer.Apply(drained, After(EventKind.Register, resumed) with { RegistrationSource = "resume" });
        Assert.False(drained.Sessions[provider + ":settled"].Ended);
        Assert.Equal(newAttachment ? "attachment-resumed" : "attachment-settled", drained.Sessions[provider + ":settled"].AttachmentId);

        // Delivered out of order: the approval resolution arrives before the approval and the prompt.
        SessionReducer.Apply(drained, After(EventKind.ToolEnd, resumed.AddSeconds(30), "approved-tool"));
        SessionReducer.Apply(drained, After(EventKind.Permission, resumed.AddSeconds(10), "approved-tool") with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) });
        SessionReducer.Apply(drained, After(EventKind.Prompt, resumed.AddSeconds(1)));
        var session = drained.Sessions[provider + ":settled"];
        Assert.False(session.Ended);
        Assert.Empty(session.Waits);
        Assert.NotEmpty(session.CausalEvents);

        // A second approval that is still outstanding is a tracked wait, and its resolution clears it.
        SessionReducer.Apply(drained, After(EventKind.Permission, resumed.AddSeconds(60), "pending-tool") with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) });
        session = drained.Sessions[provider + ":settled"];
        Assert.Equal(AttentionReason.Permission, session.Reason);
        Assert.Single(session.Waits);
        SessionReducer.Apply(drained, After(EventKind.ToolEnd, resumed.AddSeconds(90), "pending-tool"));
        Assert.Empty(drained.Sessions[provider + ":settled"].Waits);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void CompactionFloorsAtTheEndSoALateResumeBeforeAStrayPostEndEventStillRevivesTheSession(string provider)
    {
        var state = new AppState();
        var at = DateTimeOffset.UtcNow.AddMinutes(-60);
        SessionReducer.Apply(state, Observe(provider, "late", EventKind.Register, at));
        SessionReducer.Apply(state, Observe(provider, "late", EventKind.End, at.AddSeconds(10)));
        // Ignored by the replay: the session is already ended.
        SessionReducer.Apply(state, Observe(provider, "late", EventKind.ToolStart, at.AddSeconds(40), "stray"));
        var session = state.Sessions[provider + ":late"];
        Assert.True(session.Ended);
        Assert.Equal(3, session.CausalEvents.Count);

        Assert.True(CausalSessionReducer.Compact(session));
        Assert.Equal(EventKind.End, session.CausalFloor!.Kind);

        SessionReducer.Apply(state, Observe(provider, "late", EventKind.Register, at.AddSeconds(25)) with { RegistrationSource = "resume" });
        Assert.False(state.Sessions[provider + ":late"].Ended);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void CompactionWithoutAnEndObservationKeepsTheLatestJournalObservationAsTheFloor(string provider)
    {
        var state = new AppState();
        var at = DateTimeOffset.UtcNow.AddMinutes(-60);
        SessionReducer.Apply(state, Observe(provider, "plain", EventKind.Register, at));
        SessionReducer.Apply(state, Observe(provider, "plain", EventKind.Prompt, at.AddSeconds(5)));
        var session = state.Sessions[provider + ":plain"];
        // Ended by attachment evidence, not by an End observation.
        session.Ended = true;

        CausalSessionReducer.Compact(session);

        Assert.Equal(at.AddSeconds(5), session.CausalFloor!.At);
    }

    // Captures are drained in ingress order, 128 at a time. The ended session's events are in the first batch; its resume
    // registration (a hook time earlier than a stray post-end event) and a later prompt are in the second.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ABacklogLargerThanOneBatchIsAppliedBeforeCompactionSoALateResumeAfterTheEndIsTracked(string provider)
    {
        using var workspace = new TestWorkspace();
        var other = provider == "claude" ? "codex" : "claude";
        var start = DateTimeOffset.UtcNow.AddMinutes(-50);
        var index = 0;
        void Queue(string hook, string target, string session, DateTimeOffset at, Action<Dictionary<string, object?>>? extra = null)
        {
            var fields = new Dictionary<string, object?>
            {
                ["session_id"] = session,
                ["cwd"] = "D:/synthetic/" + session,
                ["hook_event_name"] = hook
            };
            extra?.Invoke(fields);
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(fields));
            var ingress = start.AddSeconds(index++);
            var envelope = CaptureFactory.Create(target, document.RootElement,
                new("capture-attachment-" + session, target, null, new(), "D:/synthetic/" + session, DateTimeOffset.UtcNow), ingress)!;
            CaptureSpool.Write(workspace.Paths.Root, envelope with { Observation = envelope.Observation with { At = at } });
        }
        var born = start.AddMinutes(-2);
        Queue("SessionStart", provider, "ended", born);
        Queue("SessionEnd", provider, "ended", born.AddSeconds(10));
        Queue("PreToolUse", provider, "ended", born.AddSeconds(40), f => { f["tool_name"] = "Bash"; f["tool_use_id"] = "stray"; });
        for (var i = 0; index < CaptureConsumer.BatchSize; i++)
        {
            Queue("UserPromptSubmit", i % 2 == 0 ? provider : other, "filler-" + i, start.AddSeconds(index));
        }
        Assert.Equal(CaptureConsumer.BatchSize, Queued(workspace).Length);
        Queue("SessionStart", provider, "ended", born.AddSeconds(25), f => f["source"] = "resume");
        Queue("UserPromptSubmit", provider, "ended", born.AddSeconds(60), f => f["turn_id"] = "turn-after");
        Assert.True(Queued(workspace).Length > CaptureConsumer.BatchSize);
        var store = new StateStore(workspace.Paths);

        var first = store.Drain();
        // The first batch ended the session, but compaction waits for the rest of the backlog.
        Assert.True(first.Sessions[provider + ":ended"].Ended);
        Assert.NotEmpty(first.Sessions[provider + ":ended"].CausalEvents);
        Assert.NotEmpty(Queued(workspace));

        var second = store.Drain();

        Assert.Empty(Queued(workspace));
        var session = second.Sessions[provider + ":ended"];
        Assert.False(session.Ended);
        Assert.Equal("turn-after", session.TurnId);
        Assert.Contains(other + ":filler-1", second.Sessions.Keys);
        Assert.Contains(provider + ":filler-0", second.Sessions.Keys);
    }

    // Queues hook captures for one provider in ingress order, with the hook's own time set independently of the ingress time.
    private sealed class Feed(TestWorkspace workspace, string provider)
    {
        private int index;

        public void Queue(string hook, string session, DateTimeOffset at, Action<Dictionary<string, object?>>? extra = null)
        {
            var fields = new Dictionary<string, object?>
            {
                ["session_id"] = session,
                ["cwd"] = "D:/synthetic/" + session,
                ["hook_event_name"] = hook
            };
            extra?.Invoke(fields);
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(fields));
            var envelope = CaptureFactory.Create(provider, document.RootElement,
                new("capture-attachment-" + session, provider, null, new(), "D:/synthetic/" + session, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow.AddSeconds(index++ - 1000))!;
            CaptureSpool.Write(workspace.Paths.Root, envelope with { Observation = envelope.Observation with { At = at } });
        }
    }

    // ANALYSIS-101: a stray event of the resumed client (its prompt) is drained before its resume registration, which has an earlier hook time.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AResumeDrainedAfterAStrayPromptWithAnEarlierHookTimeStillRevivesTheCompactedSessionAndTracksTheTurn(string provider)
    {
        using var workspace = new TestWorkspace();
        var feed = new Feed(workspace, provider);
        var store = new StateStore(workspace.Paths);
        var born = DateTimeOffset.UtcNow.AddMinutes(-60);
        feed.Queue("SessionStart", "resumed", born);
        feed.Queue("SessionEnd", "resumed", born.AddSeconds(10));
        var session = store.Drain().Sessions[provider + ":resumed"];
        Assert.True(session.Ended);
        Assert.Empty(session.CausalEvents);
        Assert.Equal(EventKind.End, session.CausalFloor!.Kind);

        // The resumed client's first event lands one drain before its registration.
        feed.Queue("UserPromptSubmit", "resumed", born.AddSeconds(60), f => f["turn_id"] = "turn-stray");
        session = store.Drain().Sessions[provider + ":resumed"];
        Assert.True(session.Ended);
        Assert.Single(session.CausalEvents);
        Assert.Equal(EventKind.End, session.CausalFloor!.Kind);
        Assert.NotNull(session.CausalCheckpoint);

        feed.Queue("SessionStart", "resumed", born.AddSeconds(25), f => f["source"] = "resume");
        session = store.Drain().Sessions[provider + ":resumed"];
        Assert.False(session.Ended);
        Assert.Equal("turn-stray", session.TurnId);

        feed.Queue("UserPromptSubmit", "resumed", born.AddSeconds(120), f => f["turn_id"] = "turn-later");
        session = store.Drain().Sessions[provider + ":resumed"];
        Assert.False(session.Ended);
        Assert.Equal("turn-later", session.TurnId);
        Assert.False(new StateStore(workspace.Paths).Read().Sessions[provider + ":resumed"].Ended);
    }

    // ANALYSIS-101 (c): a delayed earlier event stays ignored after a compaction that kept a stray, and after a further one.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ADelayedEarlierEventStaysIgnoredAfterCompactionAndRecompactionWithAStrayInTheJournal(string provider)
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var endedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        EndedSession(state, provider, "settled", endedAt);
        Persist(workspace, state);
        var drained = new StateStore(workspace.Paths).Drain();
        SessionReducer.Apply(drained, Observe(provider, "settled", EventKind.Prompt, endedAt.AddSeconds(60)));
        Persist(workspace, drained);

        for (var round = 0; round < 2; round++)
        {
            drained = new StateStore(workspace.Paths).Drain();
            var session = drained.Sessions[provider + ":settled"];
            Assert.Single(session.CausalEvents);
            Assert.Equal(endedAt, session.CausalFloor!.At);
            var earlier = Observe(provider, "settled", EventKind.Permission, endedAt.AddSeconds(-1), "late-" + round) with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) };
            SessionReducer.Apply(drained, earlier);
            session = drained.Sessions[provider + ":settled"];
            Assert.True(session.Ended);
            Assert.Empty(session.Waits);
            Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow));
            Assert.Equal(endedAt, session.CausalFloor!.At);
            Persist(workspace, drained);
        }
    }

    // ANALYSIS-101 (d): repeated strays on a dead session are bounded by the window, and a quiet drain afterwards writes nothing.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void RepeatedStraysOnACompactedSessionStayWithinTheWindowAndCauseNoSaveLoop(string provider)
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var endedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        EndedSession(state, provider, "settled", endedAt);
        Persist(workspace, state);
        var drained = new StateStore(workspace.Paths).Drain();
        for (var i = 1; i <= CausalSessionReducer.Window + 44; i++)
        {
            SessionReducer.Apply(drained, Observe(provider, "settled", EventKind.Prompt, endedAt.AddSeconds(1).AddMilliseconds(i)));
        }
        Persist(workspace, drained);
        var store = new StateStore(workspace.Paths);
        store.Drain();
        store.Drain();
        var bytes = File.ReadAllBytes(workspace.Paths.State);
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(workspace.Paths.State, stamp);

        var session = store.Drain().Sessions[provider + ":settled"];

        Assert.True(session.Ended);
        Assert.InRange(session.CausalEvents.Count, 1, CausalSessionReducer.Window);
        Assert.Equal(bytes, File.ReadAllBytes(workspace.Paths.State));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(workspace.Paths.State));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void CompactionFoldsOnlyWhatTheSessionAppliedKeepsLaterIgnoredObservationsAndNeverMovesTheFloorBack(string provider)
    {
        var state = new AppState();
        var at = DateTimeOffset.UtcNow.AddMinutes(-60);
        SessionReducer.Apply(state, Observe(provider, "kept", EventKind.Register, at));
        SessionReducer.Apply(state, Observe(provider, "kept", EventKind.End, at.AddSeconds(10)));
        SessionReducer.Apply(state, Observe(provider, "kept", EventKind.Prompt, at.AddSeconds(40)));
        SessionReducer.Apply(state, Observe(provider, "kept", EventKind.Prompt, at.AddSeconds(50)));
        var session = state.Sessions[provider + ":kept"];
        var before = Displayed(session);

        Assert.True(CausalSessionReducer.Compact(session));

        Assert.Equal(at.AddSeconds(10), session.CausalFloor!.At);
        Assert.Equal([at.AddSeconds(40), at.AddSeconds(50)], session.CausalEvents.Select(e => e.At));
        Assert.NotNull(session.CausalCheckpoint);
        Assert.True(session.CausalCheckpoint!.Ended);
        Assert.Equal(before, Displayed(session));
        // Nothing new to fold: the second call changes nothing and the floor stays.
        var floor = session.CausalFloor;
        Assert.False(CausalSessionReducer.Compact(session));
        Assert.Same(floor, session.CausalFloor);
        // A floor that is already later is never moved back.
        session.CausalFloor = Observe(provider, "kept", EventKind.Prompt, at.AddSeconds(30));
        session.CausalEvents = [Observe(provider, "kept", EventKind.Prompt, at.AddSeconds(5))];
        session.CausalCheckpoint = null;
        CausalSessionReducer.Compact(session);
        Assert.Equal(at.AddSeconds(30), session.CausalFloor!.At);
        Assert.Empty(session.CausalEvents);
        Assert.Null(session.CausalCheckpoint);
    }

    // ANALYSIS-103: more captures queued than one batch, at a budget that compaction alone satisfies.
    [Fact]
    public void ABacklogDrainOverTheBudgetCompactsSettledSessionsBeforeItRemovesAnyOfThem()
    {
        using var workspace = new TestWorkspace();
        using var reference = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 12; i++)
        {
            EndedSession(state, Providers[i % 2], "settled-" + i, now.AddHours(-2).AddMinutes(-i));
        }
        LiveSession(state, "claude", "live-claude", now.AddMinutes(-3));
        LiveSession(state, "codex", "live-codex", now.AddMinutes(-3));
        foreach (var target in new[] { workspace, reference })
        {
            Persist(target, Json.Decode<AppState>(Json.Encode(state)));
            for (var i = 0; i < CaptureConsumer.BatchSize + 2; i++)
            {
                QueueCapture(target, Providers[i % 2], "filler-" + i, now.AddSeconds(i));
            }
        }
        // The state a drain would write without a budget, and what it would be after compaction.
        var uncompacted = new StateStore(reference.Paths).Drain();
        var compacted = Json.Decode<AppState>(Json.Encode(uncompacted));
        foreach (var session in compacted.Sessions.Values.Where(s => s.Ended))
        {
            CausalSessionReducer.Compact(session);
        }
        var budget = System.Text.Encoding.UTF8.GetByteCount(Json.Encode(compacted)) + 8 * 1024;
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(Json.Encode(uncompacted)) > budget + 8 * 1024);
        Assert.Equal(CaptureConsumer.BatchSize + 2, Queued(workspace).Length);

        var drained = new StateStore(workspace.Paths, budgetBytes: budget).Drain();

        Assert.True(new FileInfo(workspace.Paths.State).Length <= budget);
        Assert.Equal(2, Queued(workspace).Length);
        Assert.Equal(12, drained.Sessions.Values.Count(s => s.Ended));
        Assert.All(drained.Sessions.Values.Where(s => s.Ended), session => Assert.Empty(session.CausalEvents));
        Assert.Contains("claude:live-claude", drained.Sessions.Keys);
        Assert.Contains("codex:live-codex", drained.Sessions.Keys);
        Assert.Contains("claude:filler-0", drained.Sessions.Keys);
        Assert.Contains("codex:filler-1", drained.Sessions.Keys);
    }

    // The inbox half of the backlog predicate: more than 2000 pending replay files defer compaction, 2000 do not.
    [Theory]
    [InlineData(2001, false)]
    [InlineData(2000, true)]
    public void MoreThanTwoThousandInboxFilesDeferCompactionForTheDrain(int files, bool compacted)
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        EndedSession(state, "claude", "settled", DateTimeOffset.UtcNow.AddMinutes(-30));
        EndedSession(state, "codex", "settled", DateTimeOffset.UtcNow.AddMinutes(-30));
        Persist(workspace, state);
        Directory.CreateDirectory(workspace.Paths.Inbox);
        for (var i = 0; i < files; i++)
        {
            // Invalid replay files: each is rejected and deleted, up to the batch of 2000.
            File.WriteAllText(Path.Combine(workspace.Paths.Inbox, $"junk-{i:D5}.json"), "{}");
        }

        var first = new StateStore(workspace.Paths).Drain();

        foreach (var provider in Providers)
        {
            Assert.Equal(compacted, first.Sessions[provider + ":settled"].CausalEvents.Count == 0);
        }
        var second = new StateStore(workspace.Paths).Drain();
        foreach (var provider in Providers)
        {
            Assert.Empty(second.Sessions[provider + ":settled"].CausalEvents);
        }
    }

    [Fact]
    public void AQuietSecondDrainLeavesStateJsonUntouchedForBothProviders()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        foreach (var provider in Providers)
        {
            EndedSession(state, provider, "settled", now.AddMinutes(-30));
            LiveSession(state, provider, "live", now.AddMinutes(-1));
        }
        Persist(workspace, state);
        var store = new StateStore(workspace.Paths);
        // The first drains compact and reconcile; the next one has nothing to change.
        store.Drain();
        store.Drain();
        var bytes = File.ReadAllBytes(workspace.Paths.State);
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(workspace.Paths.State, stamp);

        store.Drain();

        Assert.Equal(bytes, File.ReadAllBytes(workspace.Paths.State));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(workspace.Paths.State));
    }

    [Fact]
    public void MoreThanTheRetainedEndedSessionsRemovesTheOldestAcrossBothProvidersAndKeepsEveryLiveSession()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        const int total = StateStore.MaxEndedSessions + 30;
        for (var i = 0; i < total; i++)
        {
            var session = Plain(Providers[i % 2], "ended-" + i, now.AddHours(-1).AddMinutes(-i), ended: true);
            state.Sessions[session.Key] = session;
        }
        for (var i = 0; i < 6; i++)
        {
            // Live sessions older than every ended one are still never removed.
            var session = Plain(Providers[i % 2], "live-" + i, now.AddDays(-20), ended: false);
            state.Sessions[session.Key] = session;
        }
        Persist(workspace, state);

        var drained = new StateStore(workspace.Paths).Drain();

        Assert.Equal(StateStore.MaxEndedSessions, drained.Sessions.Values.Count(s => s.Ended));
        Assert.Equal(6, drained.Sessions.Values.Count(s => !s.Ended));
        for (var i = 0; i < total; i++)
        {
            // Index i is older as i grows, so exactly the 30 highest indexes are removed.
            Assert.Equal(i < StateStore.MaxEndedSessions, drained.Sessions.ContainsKey(Providers[i % 2] + ":ended-" + i));
        }
    }

    [Fact]
    public void ThirtyDayAgeRuleStillRemovesEndedSessionsBelowTheCount()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        foreach (var provider in Providers)
        {
            state.Sessions[provider + ":old"] = Plain(provider, "old", DateTimeOffset.UtcNow.AddDays(-31), ended: true);
            state.Sessions[provider + ":recent"] = Plain(provider, "recent", DateTimeOffset.UtcNow.AddDays(-29), ended: true);
        }
        Persist(workspace, state);

        var drained = new StateStore(workspace.Paths).Drain();

        Assert.Equal(["claude:recent", "codex:recent"], drained.Sessions.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void OverBudgetSaveRemovesOnlyTheOldestEndedSessionsInABoundedNumberOfEncodes()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 24; i++)
        {
            // Within the settle interval, so only the save budget acts. Higher index is more recent.
            EndedSession(state, Providers[i % 2], "ended-" + i, now.AddMinutes(-9).AddSeconds(i));
        }
        LiveSession(state, "claude", "live-claude", now.AddHours(-1));
        LiveSession(state, "codex", "live-codex", now.AddHours(-1));
        var full = System.Text.Encoding.UTF8.GetByteCount(Json.Encode(state));
        var budget = full / 3;
        var snapshots = new StateSnapshotStore(workspace.Paths, budget);

        snapshots.Save(state);

        Assert.True(new FileInfo(workspace.Paths.State).Length <= budget);
        Assert.InRange(snapshots.LastSaveEncodes, 2, StateSnapshotStore.FallbackPasses + 1);
        var saved = snapshots.Read();
        Assert.Contains("claude:live-claude", saved.Sessions.Keys);
        Assert.Contains("codex:live-codex", saved.Sessions.Keys);
        var keptEnded = Enumerable.Range(0, 24).Where(i => saved.Sessions.ContainsKey(Providers[i % 2] + ":ended-" + i)).ToArray();
        Assert.NotEmpty(keptEnded);
        Assert.True(keptEnded.Length < 24);
        // What remains is a contiguous run of the newest sessions: the oldest were removed first.
        Assert.Equal(Enumerable.Range(24 - keptEnded.Length, keptEnded.Length), keptEnded);
    }

    [Fact]
    public void LiveSessionsAloneOverTheBudgetRefuseTheSaveAndKeepCapturesQueued()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        EndedSession(state, "claude", "ended-claude", now.AddMinutes(-9));
        EndedSession(state, "codex", "ended-codex", now.AddMinutes(-9));
        LiveSession(state, "claude", "live-claude", now.AddHours(-1), events: 200);
        LiveSession(state, "codex", "live-codex", now.AddHours(-1), events: 200);
        Persist(workspace, state);
        var before = File.ReadAllBytes(workspace.Paths.State);
        var live = System.Text.Encoding.UTF8.GetByteCount(Json.Encode(state.Sessions.Values.Where(s => !s.Ended).ToArray()));
        QueueCapture(workspace, "claude", "queued-claude", now);
        QueueCapture(workspace, "codex", "queued-codex", now);

        var error = Assert.Throws<InvalidDataException>(() => new StateStore(workspace.Paths, budgetBytes: live / 2).Drain());

        Assert.Contains("budget", error.Message);
        Assert.Equal(2, Queued(workspace).Length);
        Assert.Equal(before, File.ReadAllBytes(workspace.Paths.State));
    }

    [Fact]
    public void StateAtTheBudgetWithQueuedCapturesForBothProvidersRecoversInOneDrain()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 40; i++)
        {
            // Sessions left by the earlier version: full journals, a checkpoint and operation memory for each.
            EndedSession(state, Providers[i % 2], "ended-" + i, now.AddHours(-2).AddMinutes(-i));
        }
        LiveSession(state, "claude", "live-claude", now.AddMinutes(-3));
        LiveSession(state, "codex", "live-codex", now.AddMinutes(-3));
        Persist(workspace, state);
        var written = new FileInfo(workspace.Paths.State).Length;
        for (var i = 0; i < 6; i++)
        {
            QueueCapture(workspace, Providers[i % 2], "queued-" + i, now.AddSeconds(i));
        }
        Assert.Equal(6, Queued(workspace).Length);

        // The budget is the size at which the earlier version stopped: growth without retention would be refused.
        var drained = new StateStore(workspace.Paths, budgetBytes: written + 512).Drain();

        Assert.True(new FileInfo(workspace.Paths.State).Length <= written + 512);
        Assert.True(new FileInfo(workspace.Paths.State).Length < written / 4);
        Assert.Empty(Queued(workspace));
        for (var i = 0; i < 6; i++)
        {
            Assert.Contains(Providers[i % 2] + ":queued-" + i, drained.Sessions.Keys);
        }
        Assert.Equal(40, drained.Sessions.Values.Count(s => s.Ended));
        Assert.All(drained.Sessions.Values.Where(s => s.Ended), session =>
        {
            Assert.Empty(session.CausalEvents);
            Assert.Null(session.CausalCheckpoint);
        });
        Assert.Contains("claude:live-claude", drained.Sessions.Keys);
        Assert.Contains("codex:live-codex", drained.Sessions.Keys);
        Assert.Equal(drained.Sessions.Count, new StateStore(workspace.Paths).Read().Sessions.Count);
    }

    [Fact]
    public void TheLastFallbackPassRemovesEveryEndedSessionSoTheEncodesAreBounded()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 24; i++)
        {
            EndedSession(state, Providers[i % 2], "ended-" + i, now.AddMinutes(-9).AddSeconds(i));
        }
        LiveSession(state, "claude", "live-claude", now.AddHours(-1));
        LiveSession(state, "codex", "live-codex", now.AddHours(-1));
        var budget = System.Text.Encoding.UTF8.GetByteCount(Json.Encode(state)) / 3;
        // Removing the oldest sessions that cover the excess is enough with the normal number of passes (see the test above) ...
        var normal = new StateSnapshotStore(workspace.Paths, budget);
        normal.Save(Json.Decode<AppState>(Json.Encode(state)));
        Assert.Contains(normal.Read().Sessions.Values, session => session.Ended);

        // ... but when the only pass allowed is the last one, it removes every ended session in a single re-encode.
        var single = new StateSnapshotStore(workspace.Paths, budget, fallbackPasses: 1);
        single.Save(state);

        Assert.Equal(2, single.LastSaveEncodes);
        var saved = single.Read();
        Assert.DoesNotContain(saved.Sessions.Values, session => session.Ended);
        Assert.Equal(["claude:live-claude", "codex:live-codex"], saved.Sessions.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AStateThatNeedsBothCompactionAndTheBudgetFallbackRecoversInOneDrainWithQueuedCapturesForBothProviders()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 16; i++)
        {
            // Settled sessions are compacted; the others are inside the settle interval and keep their full journals.
            EndedSession(state, Providers[i % 2], "settled-" + i, now.AddHours(-2).AddMinutes(-i));
            EndedSession(state, Providers[i % 2], "recent-" + i, now.AddMinutes(-9).AddSeconds(-i));
        }
        LiveSession(state, "claude", "live-claude", now.AddMinutes(-3));
        LiveSession(state, "codex", "live-codex", now.AddMinutes(-3));
        Persist(workspace, state);
        var written = new FileInfo(workspace.Paths.State).Length;
        var compacted = Json.Decode<AppState>(Json.Encode(state));
        foreach (var session in compacted.Sessions.Values.Where(session => now - session.LastObserved > StateStore.SettleInterval))
        {
            CausalSessionReducer.Compact(session);
        }
        var compactedSize = System.Text.Encoding.UTF8.GetByteCount(Json.Encode(compacted));
        var budget = compactedSize / 2;
        // Neither step alone is enough: compaction leaves the state over the reduced budget.
        Assert.True(compactedSize < written);
        Assert.True(compactedSize > budget);
        for (var i = 0; i < 6; i++)
        {
            QueueCapture(workspace, Providers[i % 2], "queued-" + i, now.AddSeconds(i));
        }

        var drained = new StateStore(workspace.Paths, budgetBytes: budget).Drain();

        Assert.True(new FileInfo(workspace.Paths.State).Length <= budget);
        Assert.Empty(Queued(workspace));
        for (var i = 0; i < 6; i++)
        {
            Assert.Contains(Providers[i % 2] + ":queued-" + i, drained.Sessions.Keys);
        }
        Assert.Contains("claude:live-claude", drained.Sessions.Keys);
        Assert.Contains("codex:live-codex", drained.Sessions.Keys);
        var ended = drained.Sessions.Values.Where(session => session.Ended).ToArray();
        Assert.True(ended.Length < 32);
        Assert.Equal(drained.Sessions.Count, new StateStore(workspace.Paths).Read().Sessions.Count);
    }

    [Fact]
    public void SaveWithinTheBudgetIsUnchangedAndTheProductionBudgetIsSixteenMebibytes()
    {
        Assert.Equal(16L * 1024 * 1024, StateSnapshotStore.DefaultBudgetBytes);
        using var workspace = new TestWorkspace();
        var state = new AppState();
        state.Sessions["claude:one"] = Plain("claude", "one", DateTimeOffset.UtcNow, ended: true);
        var snapshots = new StateSnapshotStore(workspace.Paths);
        snapshots.Save(state);
        Assert.Equal(1, snapshots.LastSaveEncodes);
        Assert.Single(snapshots.Read().Sessions);
    }
}

using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class ReminderPolicyTests
{
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ConfirmedProviderWaitRemindsAfterBothThresholdsAndCaps(string provider)
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting(provider, now);
        var state = new AppState();
        var settings = new ReminderSettings();

        Assert.Empty(ReminderPolicy.Advance(state, [session], now, TimeSpan.FromMinutes(5), settings, "run-1").Scheduled);
        var due = ReminderPolicy.Advance(state, [session], now.AddMinutes(5), TimeSpan.FromMinutes(4).Add(TimeSpan.FromSeconds(59)), settings, "run-1");
        Assert.Empty(due.Scheduled);

        var first = ReminderPolicy.Advance(state, [session], now.AddMinutes(5), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled.Single();
        Assert.Equal(1, first.Ordinal);
        Assert.NotNull(state.ReminderReceipts.Values.Single().Escalation);

        Assert.Empty(ReminderPolicy.Advance(state, [session], now.AddMinutes(15).Add(TimeSpan.FromMilliseconds(-1)), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled);
        Assert.Equal(2, ReminderPolicy.Advance(state, [session], now.AddMinutes(15), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled.Single().Ordinal);
        Assert.Equal(3, ReminderPolicy.Advance(state, [session], now.AddMinutes(25), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled.Single().Ordinal);
        Assert.Empty(ReminderPolicy.Advance(state, [session], now.AddMinutes(35), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled);
    }

    [Fact]
    public void MaximumIdleThresholdStillQualifiesWhenCurrentInactivityExceedsTwentyFourHours()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("claude", now);
        var settings = new ReminderSettings { InitialDelayMinutes = 1, IdleThresholdMinutes = 1440 };
        var state = new AppState();
        ReminderPolicy.Advance(state, [session], now, null, settings, "run-1");
        var due = ReminderPolicy.Advance(state, [session], now.AddMinutes(1), TimeSpan.FromHours(25), settings, "run-1");
        Assert.Equal(1, Assert.Single(due.Scheduled).Ordinal);
    }

    [Fact]
    public void MixedSessionsKeepIndependentReceiptsAndViewedWaitStillQualifies()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var claude = Copy(Waiting("claude", now), session => session.ViewedEpisode = "episode-claude");
        var codex = Waiting("codex", now);
        var state = new AppState();
        var settings = new ReminderSettings();
        ReminderPolicy.Advance(state, [claude, codex], now, null, settings, "run-1");

        var due = ReminderPolicy.Advance(state, [claude, codex], now.AddMinutes(5), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled;
        Assert.Equal(new[] { "claude:session", "codex:session" }, due.Select(item => item.Key).Order(StringComparer.Ordinal));
        Assert.Equal(2, state.ReminderReceipts.Count);
        Assert.Equal(2, state.ReminderReceipts.Values.Select(receipt => receipt.ReceiptId).Distinct().Count());
    }

    [Fact]
    public void AcknowledgedResolvedAndUnverifiedEpisodesNeverRemind()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("claude", now);
        var state = new AppState();
        var settings = new ReminderSettings();
        ReminderPolicy.Advance(state, [session], now, null, settings, "run-1");
        Assert.Empty(ReminderPolicy.Advance(state, [Copy(session, value => value.AcknowledgedEpisode = value.Episode)], now.AddMinutes(6), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled);
        Assert.Empty(state.ReminderReceipts);
        Assert.Empty(ReminderPolicy.Advance(state, [Copy(session, value => value.AttachmentHealth = "unverified")], now.AddMinutes(7), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled);
        Assert.Empty(ReminderPolicy.Advance(state, [Copy(session, value => value.Ended = true)], now.AddMinutes(8), TimeSpan.FromMinutes(5), settings, "run-1").Scheduled);
    }

    [Fact]
    public void RestartAndReenableMoveExpiredDeadlinesIntoFutureWithoutCatchUp()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("codex", now);
        var state = new AppState();
        var enabled = new ReminderSettings();
        ReminderPolicy.Advance(state, [session], now, null, enabled, "run-1");
        ReminderPolicy.Advance(state, [session], now.AddMinutes(5), TimeSpan.FromMinutes(20), enabled, "run-1");

        var priorCandidate = state.ReminderReceipts.Values.Single().Escalation;
        Assert.NotNull(priorCandidate);
        Assert.Empty(ReminderPolicy.Advance(state, [session], now.AddMinutes(16), TimeSpan.FromMinutes(20), enabled, "run-2").Scheduled);
        Assert.Equal(now.AddMinutes(26), state.ReminderReceipts.Values.Single().NextReminderAt);
        Assert.Equal(priorCandidate, state.ReminderReceipts.Values.Single().Escalation);

        var disabled = new ReminderSettings { Enabled = false };
        Assert.Empty(ReminderPolicy.Advance(state, [session], now.AddMinutes(17), TimeSpan.FromMinutes(20), disabled, "run-2").Scheduled);
        Assert.Empty(ReminderPolicy.Advance(state, [session], now.AddMinutes(18), TimeSpan.FromMinutes(20), enabled, "run-2").Scheduled);
        Assert.Equal(now.AddMinutes(23), state.ReminderReceipts.Values.Single().NextReminderAt);
    }

    [Fact]
    public void EscalationIsPersistedBeforeScheduledAudioAndRevokedByAcknowledgment()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("claude", now);
        var snapshots = new StateSnapshotStore(workspace.Paths);
        snapshots.Save(new AppState { Sessions = new() { [session.Key] = session } });
        var store = new StateStore(workspace.Paths);
        store.AdvanceReminders([session], now, null, new(), "run-1");

        var scheduled = store.AdvanceReminders([session], now.AddMinutes(5), TimeSpan.FromMinutes(5), new(), "run-1");
        Assert.Single(scheduled);
        var receipt = store.Read().ReminderReceipts.Values.Single();
        Assert.Equal(1, receipt.Ordinal);
        var candidate = Assert.Single(store.Escalations([session], now.AddMinutes(5)));
        Assert.Equal(1, candidate.ProtocolVersion);
        Assert.Equal(session.Key, candidate.ProviderSessionKey);
        Assert.Equal(session.Episode, candidate.Episode);
        Assert.Equal(1, candidate.ReminderOrdinal);
        Assert.Equal(receipt.ReceiptId, candidate.ReceiptId);
        Assert.Equal(receipt.Escalation, candidate);

        store.Acknowledge(session.Key);
        Assert.Empty(store.Escalations([session], now.AddMinutes(5)));
        Assert.Empty(store.AdvanceReminders([session], now.AddMinutes(15), TimeSpan.FromMinutes(5), new(), "run-1"));
    }

    [Fact]
    public void TemporaryVerificationGapRetainsReceiptAndCandidateButSuspendsExposure()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("codex", now);
        var snapshots = new StateSnapshotStore(workspace.Paths);
        snapshots.Save(new AppState { Sessions = new() { [session.Key] = session } });
        var store = new StateStore(workspace.Paths);
        store.AdvanceReminders([session], now, null, new(), "run-1");
        store.AdvanceReminders([session], now.AddMinutes(5), TimeSpan.FromMinutes(5), new(), "run-1");
        var before = store.Read().ReminderReceipts.Values.Single();
        Assert.Single(store.Escalations([session], now.AddMinutes(5)));

        var unverified = Copy(session, value => value.AttachmentHealth = "unverified");
        snapshots.Save(new AppState
        {
            Sessions = new() { [unverified.Key] = unverified },
            ReminderReceipts = store.Read().ReminderReceipts,
            ReminderRunId = "run-1",
            ReminderSettingsEnabled = true
        });
        Assert.Empty(store.AdvanceReminders([], now.AddMinutes(6), TimeSpan.FromMinutes(5), new(), "run-1"));
        Assert.Empty(store.Escalations([unverified], now.AddMinutes(6)));

        snapshots.Save(new AppState
        {
            Sessions = new() { [session.Key] = session },
            ReminderReceipts = store.Read().ReminderReceipts,
            ReminderRunId = "run-1",
            ReminderSettingsEnabled = true
        });
        var recovered = Copy(session, value => value.NativePermissionPending = true);
        Assert.Empty(store.AdvanceReminders([recovered], now.AddMinutes(7), TimeSpan.FromMinutes(5), new(), "run-1"));
        var after = store.Read().ReminderReceipts.Values.Single();
        Assert.Equal(before.ReceiptId, after.ReceiptId);
        Assert.Equal(before.Ordinal, after.Ordinal);
        Assert.Equal(before.NextReminderAt, after.NextReminderAt);
        Assert.Equal(before.Escalation, after.Escalation);
        Assert.Single(store.Escalations([recovered], now.AddMinutes(7)));
    }

    [Fact]
    public void SerializedRestartDoesNotReplayDueReminderAndSchedulesOnlyNextOrdinal()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("claude", now);
        var snapshots = new StateSnapshotStore(workspace.Paths);
        snapshots.Save(new AppState { Sessions = new() { [session.Key] = session } });
        var firstRun = new StateStore(workspace.Paths);
        firstRun.AdvanceReminders([session], now, null, new(), "run-1");
        var emitted = firstRun.AdvanceReminders([session], now.AddMinutes(5), TimeSpan.FromMinutes(5), new(), "run-1");
        Assert.Equal(1, Assert.Single(emitted).Ordinal);
        var durable = firstRun.Read().ReminderReceipts.Values.Single();
        Assert.Equal(1, durable.Ordinal); // committed before the scheduled item can be enqueued

        var restarted = new StateStore(workspace.Paths);
        var absentForLongTime = now.AddDays(90);
        Assert.Empty(restarted.AdvanceReminders([session], absentForLongTime, TimeSpan.FromMinutes(5), new(), "run-2"));
        var afterRestart = restarted.Read().ReminderReceipts.Values.Single();
        Assert.Equal(durable.ReceiptId, afterRestart.ReceiptId);
        Assert.Equal(1, afterRestart.Ordinal);
        Assert.Equal(absentForLongTime.AddMinutes(10), afterRestart.NextReminderAt);
        Assert.Empty(new StateStore(workspace.Paths).AdvanceReminders([session], absentForLongTime, TimeSpan.FromMinutes(5), new(), "run-2"));
        var second = restarted.AdvanceReminders([session], afterRestart.NextReminderAt, TimeSpan.FromMinutes(5), new(), "run-2");
        Assert.Equal(2, Assert.Single(second).Ordinal);
        Assert.Equal(durable.ReceiptId, restarted.Read().ReminderReceipts.Values.Single().ReceiptId);
    }

    [Fact]
    public void EscalationExportRequiresFreshCurrentCodexConfirmationAndEnabledConfig()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("codex", now);
        session.Waits = [new(AttentionReason.Permission, now, null, null, Episode: session.Episode, Confirmation: HumanConfirmation.Provisional)];
        var store = new StateStore(workspace.Paths);
        new StateSnapshotStore(workspace.Paths).Save(new AppState { Sessions = new() { [session.Key] = session } });
        var confirmed = Copy(session, value =>
        {
            value.NativePermissionPending = true;
            value.NativePermissionSince = now;
        });
        store.AdvanceReminders([confirmed], now.AddSeconds(5), null, new(), "run-1");
        Assert.Single(store.AdvanceReminders([confirmed], now.AddMinutes(5).AddSeconds(5), TimeSpan.FromMinutes(5), new(), "run-1"));
        Assert.Single(store.Escalations([confirmed], now.AddMinutes(5).AddSeconds(5)));
        Assert.Empty(store.Escalations([session], now.AddMinutes(5).AddSeconds(5)));

        var receiptBeforeGap = store.Read().ReminderReceipts.Values.Single();
        Assert.Empty(store.AdvanceReminders([session], now.AddMinutes(15).AddSeconds(5), TimeSpan.FromMinutes(5), new(), "run-1"));
        var resumed = Assert.Single(store.AdvanceReminders([confirmed], now.AddMinutes(15).AddSeconds(5), TimeSpan.FromMinutes(5), new(), "run-1"));
        Assert.Equal(2, resumed.Ordinal);
        var receiptAfterGap = store.Read().ReminderReceipts.Values.Single();
        Assert.Equal(receiptBeforeGap.ReceiptId, receiptAfterGap.ReceiptId);
        Assert.Equal(2, receiptAfterGap.Ordinal);

        new ConfigStore(workspace.Paths).Update(value => value.Reminders.Enabled = false);
        Assert.Empty(store.Escalations([confirmed], now.AddMinutes(15).AddSeconds(5)));
    }

    [Fact]
    public void ReceiptPruningKeepsActiveEpisodeHistoryAndPrunesInactiveRecords()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var active = Waiting("claude", now);
        var activeKey = ReminderPolicy.Key(active);
        var receipts = Enumerable.Range(0, 10000).ToDictionary(index => $"inactive:{index:D5}\nepisode",
            index => new ReminderReceipt($"inactive:{index:D5}", "episode", now, now.AddMinutes(5), null, index % 3, $"id-{index}"), StringComparer.Ordinal);
        var retained = new HashSet<string>([activeKey], StringComparer.Ordinal);
        receipts[activeKey] = new(active.Key, active.Episode!, now, now.AddMinutes(5), now, 3, "active-stable-id");
        var state = new AppState { ReminderReceipts = receipts, Sessions = new() { [active.Key] = active }, ReminderRunId = "run-1", ReminderSettingsEnabled = true };
        var result = ReminderPolicy.Advance(state, [active], now.AddMinutes(6), TimeSpan.FromMinutes(5), new(), "run-1", retained);

        Assert.Single(state.ReminderReceipts);
        var kept = state.ReminderReceipts[activeKey];
        Assert.Equal("active-stable-id", kept.ReceiptId);
        Assert.Equal(3, kept.Ordinal);
        Assert.Equal(now.AddMinutes(5), kept.NextReminderAt);
        Assert.Empty(result.Scheduled);
        Assert.False(state.ReminderReceipts.ContainsKey("inactive:00000\nepisode"));
    }

    [Fact]
    public void StoreRejectsStaleAttachmentIdentityAndPreservesUnknownSnapshotFields()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        using var extension = System.Text.Json.JsonDocument.Parse("{\"future\":{\"keep\":true}}");
        var session = Copy(Waiting("codex", now), value => value.AdditionalData = new() { ["futureSession"] = extension.RootElement.Clone() });
        new StateSnapshotStore(workspace.Paths).Save(new AppState { Sessions = new() { [session.Key] = session } });
        var stale = Copy(session, value => value.AttachmentId = "other-attachment");
        Assert.Empty(new StateStore(workspace.Paths).AdvanceReminders([stale], now, TimeSpan.FromMinutes(5), new(), "run-1"));
        Assert.Empty(new StateStore(workspace.Paths).Read().ReminderReceipts);
        Assert.Contains("futureSession", File.ReadAllText(workspace.Paths.State));
    }

    [Fact]
    public void UnknownReceiptFieldsSurviveReminderUpdateAndSnapshotReload()
    {
        using var workspace = new TestWorkspace();
        using var extension = System.Text.Json.JsonDocument.Parse("{\"keep\":true}");
        var started = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var session = Waiting("codex", started);
        var receiptKey = ReminderPolicy.Key(session);
        var receipt = new ReminderReceipt(session.Key, session.Episode!, started, started.AddMinutes(5), null, 0, Guid.NewGuid().ToString("N"))
        {
            AdditionalData = new() { ["futureReceipt"] = extension.RootElement.Clone() }
        };
        Assert.True(Json.Decode<ReminderReceipt>(Json.Encode(receipt)).AdditionalData!["futureReceipt"].GetProperty("keep").GetBoolean());
        new StateSnapshotStore(workspace.Paths).Save(new AppState
        {
            Sessions = new() { [session.Key] = session },
            ReminderReceipts = new() { [receiptKey] = receipt },
            ReminderRunId = "run-1",
            ReminderSettingsEnabled = true
        });

        new StateStore(workspace.Paths).AdvanceReminders([session], started.AddMinutes(5), TimeSpan.FromMinutes(5), new(), "run-1");
        var reloaded = new StateStore(workspace.Paths).Read().ReminderReceipts[receiptKey];
        Assert.True(reloaded.AdditionalData!["futureReceipt"].GetProperty("keep").GetBoolean());
    }

    [Fact]
    public void WindowsTickElapsedUsesUnsignedWrapAndUnknownProbeStaysUnknown()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(21), WindowsUserIdleSource.Elapsed((ulong)uint.MaxValue + 11, uint.MaxValue - 10));
        Assert.Null(WindowsUserIdleSource.Elapsed(10_000, 11_000));
        var source = new WindowsUserIdleSource(() => throw new InvalidOperationException("fixture"));
        Assert.Null(source.GetSnapshot().IdleFor);
    }

    [Fact]
    public void OlderSnapshotWithoutReminderFieldsLoadsWithEmptyReceiptDefaults()
    {
        var oldSnapshot = Json.Decode<AppState>("{\"schemaVersion\":1,\"sessions\":{},\"appliedEvents\":[],\"captureReceipts\":{}}");
        Assert.NotNull(oldSnapshot);
        Assert.Empty(oldSnapshot.ReminderReceipts);
        Assert.Null(oldSnapshot.ReminderRunId);
    }

    private static SessionState Waiting(string provider, DateTimeOffset started)
    {
        var episode = "episode-" + provider;
        return new()
        {
            Key = provider + ":session",
            Provider = provider,
            SessionId = "session",
            AttachmentId = "attachment-" + provider,
            AttachmentHealth = "verified-live",
            Episode = episode,
            EpisodeStarted = started,
            Waits = [new(AttentionReason.Question, started, null, null, Episode: episode, Confirmation: HumanConfirmation.Confirmed)]
        };
    }

    private static SessionState Copy(SessionState session, Action<SessionState> update)
    {
        var copy = Json.Decode<SessionState>(Json.Encode(session))!;
        update(copy);
        return copy;
    }
}

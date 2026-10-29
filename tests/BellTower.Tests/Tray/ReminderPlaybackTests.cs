using System.Reflection;
using System.Runtime.CompilerServices;
using BellTower.Core;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

public sealed class ReminderPlaybackTests
{
    private static readonly TimeSpan HealthyProbeTestBudget = TimeSpan.FromSeconds(5);

    private sealed class ManualClock(DateTimeOffset value)
    {
        private readonly object gate = new();
        private DateTimeOffset now = value;
        public DateTimeOffset Now() { lock (gate) { return now; } }
        public void Advance(TimeSpan by) { lock (gate) { now += by; } }
    }

    private sealed class SilentSink : IAudioSink
    {
        private int calls;
        public string? Phrase { get; private set; }
        public bool ToneCancelled { get; private set; }
        public bool SpeechCancelled { get; private set; }
        public bool HoldFirst { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public TaskCompletionSource Next { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action? DuringTone { get; set; }

        public void Play(string? phrase, AttentionReason reason, Func<bool> cancelled, Func<bool> speechCancelled)
        {
            var call = Interlocked.Increment(ref calls);
            if (HoldFirst && call == 1)
            {
                Started.TrySetResult();
                Release.Wait(TimeSpan.FromSeconds(3));
                return;
            }

            Phrase = phrase;
            DuringTone?.Invoke();
            ToneCancelled = cancelled();
            SpeechCancelled = speechCancelled();
            if (call == 1)
            {
                Started.TrySetResult();
            }
            else
            {
                Next.TrySetResult();
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class BlockingSink : IAudioSink
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();

        public void Play(string? phrase, AttentionReason reason, Func<bool> cancelled, Func<bool> speechCancelled)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                Started.TrySetResult();
                Release.Wait(TimeSpan.FromSeconds(3));
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class MutableIdleSource(UserIdleSnapshot snapshot) : IUserIdleSource
    {
        public UserIdleSnapshot Snapshot = snapshot;
        public int Reads;
        public UserIdleSnapshot GetSnapshot()
        {
            Interlocked.Increment(ref Reads);
            return Snapshot;
        }
    }

    [Fact]
    public void PlaybackReadsLatestIdleSourceAndRejectsFutureObservationInsteadOfYoungTrayCache()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var now = clock.Now();
        var waiting = Waiting(now.AddMinutes(-10));
        var source = new MutableIdleSource(new(TimeSpan.Zero, now, "recent input"));
        var application = UninitializedApplication(waiting, source, new(TimeSpan.FromMinutes(20), now, "cached idle"), clock.Now);

        Assert.False(IsCurrent(application, new(waiting.Key, waiting.Episode!, "phrase", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1)));
        Assert.Equal(1, source.Reads);

        source.Snapshot = new(TimeSpan.FromMinutes(20), now.AddMinutes(1), "future fixture");
        Assert.False(IsCurrent(application, new(waiting.Key, waiting.Episode!, "phrase", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1)));
        Assert.Equal(2, source.Reads);
    }

    [Fact]
    public void ActualWindowsSourceRefreshesBeforePlaybackAndHealthyIdleRemainsEligible()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var now = clock.Now();
        var waiting = Waiting(now.AddMinutes(-10));
        var idleFor = TimeSpan.FromMinutes(20);
        var reads = 0;
        var source = new WindowsUserIdleSource(() =>
        {
            Interlocked.Increment(ref reads);
            return new(idleFor, clock.Now(), "fixture");
        }, clock.Now, HealthyProbeTestBudget);
        var application = UninitializedApplication(waiting, source, new(TimeSpan.FromMinutes(20), now, "old cache"), clock.Now);
        var reminder = new AudioItem(waiting.Key, waiting.Episode!, "Still waiting", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1);

        Assert.True(IsCurrent(application, reminder));
        Assert.Equal(1, Volatile.Read(ref reads));
        idleFor = TimeSpan.Zero;
        Assert.False(IsCurrent(application, reminder));
        Assert.Equal(2, Volatile.Read(ref reads));
    }

    [Fact]
    public void ActualWindowsSourceRejectsAgingAndFutureObservationsWithSharedManualClock()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var now = clock.Now();
        var waiting = Waiting(now.AddMinutes(-10));
        var observationMode = 0;
        var source = new WindowsUserIdleSource(() =>
        {
            var observedAt = clock.Now();
            var mode = Interlocked.Exchange(ref observationMode, 0);
            if (mode == 1)
            {
                clock.Advance(TimeSpan.FromMilliseconds(501));
            }
            else if (mode == 2)
            {
                observedAt = observedAt.AddTicks(1);
            }
            return new(TimeSpan.FromMinutes(20), observedAt, "fixture");
        }, clock.Now, HealthyProbeTestBudget);
        var application = UninitializedApplication(waiting, source,
            new(TimeSpan.FromMinutes(20), now, "old cache"), clock.Now);
        var reminder = new AudioItem(waiting.Key, waiting.Episode!, "Still waiting", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1);

        Assert.True(IsCurrent(application, reminder));
        Interlocked.Exchange(ref observationMode, 1);
        Assert.False(IsCurrent(application, reminder));
        Interlocked.Exchange(ref observationMode, 2);
        Assert.False(IsCurrent(application, reminder));
    }

    [Fact]
    public async Task ActualWindowsSourceBlocksStaleReminderQueuedBeforeRecentInput()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var now = clock.Now();
        var waiting = Waiting(now.AddMinutes(-10));
        var idleFor = TimeSpan.FromMinutes(20);
        var source = new WindowsUserIdleSource(() => new(idleFor, clock.Now(), "fixture"), clock.Now, HealthyProbeTestBudget);
        Assert.Equal(TimeSpan.FromMinutes(20), source.GetFreshSnapshot(HealthyProbeTestBudget).IdleFor);
        var application = UninitializedApplication(waiting, source, new(TimeSpan.FromMinutes(20), now, "old cache"), clock.Now);
        var sink = new BlockingSink();
        var checkedReminder = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool Valid(AudioItem item)
        {
            if (item.Test)
            {
                return true;
            }
            var valid = IsCurrent(application, item);
            checkedReminder.TrySetResult();
            return valid;
        }
        using var queue = new AudioQueue(sink, Valid, () => new() { MicrophoneAwareVoice = false });
        queue.Enqueue(new("occupy", "test", "occupy", Test: true));
        await sink.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        queue.Enqueue(new(waiting.Key, waiting.Episode!, "Still waiting", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1));
        idleFor = TimeSpan.Zero;
        sink.Release.Set();

        await checkedReminder.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, sink.Calls);
    }

    [Fact]
    public async Task ActualWindowsSourceAllowsHealthyReminderThroughQueue()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var now = clock.Now();
        var waiting = Waiting(now.AddMinutes(-10));
        var codexWaiting = Waiting(now.AddMinutes(-10), "codex");
        var secondCodexWaiting = Waiting(now.AddMinutes(-10), "codex", "session-2");
        var source = new WindowsUserIdleSource(() => new(TimeSpan.FromMinutes(20), clock.Now(), "fixture"), clock.Now, HealthyProbeTestBudget);
        Assert.Equal(TimeSpan.FromMinutes(20), source.GetFreshSnapshot(HealthyProbeTestBudget).IdleFor);
        var application = UninitializedApplication(waiting, source, new(TimeSpan.Zero, now, "unknown cache"), clock.Now,
            codexWaiting, secondCodexWaiting);
        var sink = new SilentSink { HoldFirst = true };
        using var queue = new AudioQueue(sink, item => IsCurrent(application, item),
            () => new() { Voice = true, MicrophoneAwareVoice = false });
        queue.Enqueue(new("occupy", "test", "occupy", Test: true));
        await sink.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        queue.Enqueue(new(waiting.Key, waiting.Episode!, "Still waiting: Claude session has a question", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1));
        queue.Enqueue(new(codexWaiting.Key, codexWaiting.Episode!, "Still waiting: Codex session has a question", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1));
        queue.Enqueue(new(secondCodexWaiting.Key, secondCodexWaiting.Episode!, "Still waiting: Codex session has a question", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1));
        sink.Release.Set();

        await sink.Next.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(sink.ToneCancelled);
        Assert.False(sink.SpeechCancelled);
        Assert.Equal("3 sessions still waiting", sink.Phrase);
    }

    [Fact]
    public async Task ActualWindowsSourceDuringMixedToneCancelsStaleReminderSpeechAndKeepsOrdinaryTone()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var now = clock.Now();
        var waiting = Waiting(now.AddMinutes(-10));
        var idleFor = TimeSpan.FromMinutes(20);
        var source = new WindowsUserIdleSource(() => new(idleFor, clock.Now(), "fixture"), clock.Now, HealthyProbeTestBudget);
        var application = UninitializedApplication(waiting, source, new(TimeSpan.FromMinutes(20), now, "old cache"), clock.Now);
        var sink = new SilentSink { HoldFirst = true, DuringTone = () => idleFor = TimeSpan.Zero };
        using var queue = new AudioQueue(sink, item => item.Kind == AudioNotificationKind.Reminder ? IsCurrent(application, item) : true,
            () => new() { Voice = true, MicrophoneAwareVoice = false });
        queue.Enqueue(new("occupy", "test", "occupy", Test: true));
        await sink.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        queue.Enqueue(new(waiting.Key, waiting.Episode!, "Still waiting", Reason: AttentionReason.Question,
            Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1));
        queue.Enqueue(new("claude:ordinary-one", "ordinary-one", "ordinary one", Reason: AttentionReason.Permission));
        queue.Enqueue(new("codex:ordinary-two", "ordinary-two", "ordinary two", Reason: AttentionReason.Error));
        sink.Release.Set();

        await sink.Next.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("3 sessions waiting", sink.Phrase);
        Assert.False(sink.ToneCancelled);
        Assert.True(sink.SpeechCancelled);
    }

    [Fact]
    public async Task TimedOutFreshReadCannotAuthorizeAnOlderPositiveObservation()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var now = clock.Now();
        var waiting = Waiting(now.AddMinutes(-10));
        var reads = 0;
        var block = false;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var source = new WindowsUserIdleSource(() =>
        {
            Interlocked.Increment(ref reads);
            if (block)
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(3));
            }
            return new(TimeSpan.FromMinutes(20), clock.Now(), "fixture");
        }, clock.Now);
        await ReminderFixtureDriver.RunAsync(() =>
        {
            Assert.Equal(TimeSpan.FromMinutes(20), source.GetFreshSnapshot(HealthyProbeTestBudget).IdleFor);
            block = true;
            Assert.Null(source.GetFreshSnapshot(TimeSpan.Zero).IdleFor);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            var application = UninitializedApplication(waiting, source, new(TimeSpan.FromMinutes(20), now, "cached idle"), clock.Now);
            Assert.False(IsCurrent(application, new(waiting.Key, waiting.Episode!, "Still waiting", Reason: AttentionReason.Question,
                Kind: AudioNotificationKind.Reminder, ReminderOrdinal: 1)));
            Assert.Equal(2, Volatile.Read(ref reads));
            return true;
        }, release.Set, TimeSpan.FromSeconds(4));
    }

    private static TrayApplication UninitializedApplication(SessionState waiting, IUserIdleSource source, UserIdleSnapshot cached,
        Func<DateTimeOffset>? validityClock = null, params SessionState[] additionalWaiting)
    {
        var application = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        var sessions = new Dictionary<string, SessionState> { [waiting.Key] = waiting };
        foreach (var additional in additionalWaiting)
        {
            sessions.Add(additional.Key, additional);
        }
        Set(application, "state", new AppState { Sessions = sessions });
        Set(application, "config", new AppConfig());
        Set(application, "userIdle", source);
        Set(application, "idleSnapshot", cached);
        Set(application, "idleGate", new object());
        application.ReminderValidityUtcNow = validityClock ?? (() => DateTimeOffset.UtcNow);
        return application;
    }

    private static bool IsCurrent(TrayApplication application, AudioItem item) =>
        (bool)typeof(TrayApplication).GetMethod("IsCurrentAlert", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(application, [item])!;

    private static void Set(object instance, string name, object value) =>
        typeof(TrayApplication).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);

    private static SessionState Waiting(DateTimeOffset now, string provider = "claude", string sessionId = "session") => new()
    {
        Key = $"{provider}:{sessionId}",
        Provider = provider,
        SessionId = sessionId,
        AttachmentId = "attachment",
        AttachmentHealth = "verified-live",
        Episode = "episode",
        EpisodeStarted = now,
        Waits = [new(AttentionReason.Question, now, null, null, Episode: "episode", Confirmation: HumanConfirmation.Confirmed)]
    };
}

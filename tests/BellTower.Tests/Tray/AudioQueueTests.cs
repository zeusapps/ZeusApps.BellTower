using System.Collections.Concurrent;
using BellTower.Core;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

public sealed class AudioQueueTests
{
    private sealed class Sink : IAudioSink
    {
        public readonly ConcurrentQueue<string?> Phrases = new();
        public readonly ConcurrentQueue<AttentionReason> Reasons = new();
        public readonly ConcurrentQueue<bool> SpeechCancelled = new();
        public readonly ConcurrentQueue<bool> ToneCancelled = new();
        public readonly ManualResetEventSlim Started = new();
        public readonly ManualResetEventSlim Release = new();
        public readonly ManualResetEventSlim Next = new();
        private int calls;
        public Action? DuringTone { get; set; }
        public Action? DuringSpeech { get; set; }
        public void Play(string? phrase, AttentionReason reason, Func<bool> cancelled, Func<bool> speechCancelled)
        {
            Phrases.Enqueue(phrase);
            Reasons.Enqueue(reason);
            DuringTone?.Invoke();
            if (phrase is not null)
            {
                DuringSpeech?.Invoke();
            }
            SpeechCancelled.Enqueue(speechCancelled());
            ToneCancelled.Enqueue(cancelled());
            if (Interlocked.Increment(ref calls) == 1)
            {
                Started.Set();
                Release.Wait(TimeSpan.FromSeconds(2));
            }
            else
            {
                Next.Set();
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class Microphone(MicrophoneActivitySnapshot snapshot) : IMicrophoneActivitySource
    {
        public MicrophoneActivitySnapshot Snapshot = snapshot;
        public MicrophoneActivitySnapshot GetSnapshot() => Snapshot;
    }

    private sealed class FailedMicrophone : IMicrophoneActivitySource
    {
        public MicrophoneActivitySnapshot GetSnapshot() => throw new InvalidOperationException("fixture failure");
    }

    private sealed class FakeSession(MicrophoneUsageState state) : IDisposable
    {
        public MicrophoneUsageState State => state;
        public bool Disposed { get; private set; }
        public int ReleaseCount { get; private set; }
        public void Dispose()
        {
            ReleaseCount++;
            Disposed = true;
        }
    }

    private sealed class ManualUtcClock(DateTimeOffset current)
    {
        private readonly object gate = new();
        private DateTimeOffset current = current;

        public DateTimeOffset Now()
        {
            lock (gate)
            {
                return current;
            }
        }

        public void Advance(TimeSpan amount)
        {
            lock (gate)
            {
                current += amount;
            }
        }
    }

    [Fact]
    public void ThreePendingPhrasesAggregateOnTheRealWorkerWithoutAudio()
    {
        var sink = new Sink();
        var config = new AppConfig();
        using var queue = new AudioQueue(sink, _ => true, () => config, Inactive());
        queue.Enqueue(new("occupy", "a", "first", true));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        foreach (var i in Enumerable.Range(1, 3))
        {
            queue.Enqueue(new(i.ToString(), "e", "subject", Reason: (AttentionReason)i));
        }

        sink.Release.Set();
        Assert.True(sink.Next.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(new[] { "first", "3 sessions waiting" }, sink.Phrases.ToArray());
        Assert.Equal(new[] { AttentionReason.Done, AttentionReason.Error }, sink.Reasons.ToArray());
    }

    [Fact]
    public void ReminderAudioUsesSameReasonQueueAndNamesReminderAggregate()
    {
        var items = Enumerable.Range(0, 3).Select(index => new AudioItem(index.ToString(), "episode", "Still waiting",
            Reason: AttentionReason.Question, Kind: AudioNotificationKind.Reminder)).ToArray();
        Assert.Equal("3 sessions still waiting", AudioQueue.SelectPhrase(items, true));
        Assert.Equal(AttentionReason.Question, items.Max(item => item.Reason));
        Assert.Null(AudioQueue.SelectPhrase(items, false));
    }

    [Fact]
    public void ResolvedQueuedWaitIsExcludedBeforePlayback()
    {
        var sink = new Sink();
        var current = new ConcurrentDictionary<string, bool>();
        current["wait"] = true;
        using var queue = new AudioQueue(sink, item => current.ContainsKey(item.Key), () => new() { MicrophoneAwareVoice = false });
        queue.Enqueue(new("occupy", "a", "first", true));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        queue.Enqueue(new("wait", "e", "stale"));
        current.TryRemove("wait", out _);
        queue.Enqueue(new("sentinel", "e", "sentinel", true));
        sink.Release.Set();
        Assert.True(sink.Next.Wait(TimeSpan.FromSeconds(2)));
        Assert.DoesNotContain("stale", sink.Phrases);
    }

    [Fact]
    public void ResolvedReminderDuringAggregateToneCancelsAggregateSpeech()
    {
        var current = new ConcurrentDictionary<string, bool>();
        current["reminder"] = true;
        current["alert-one"] = true;
        current["alert-two"] = true;
        var sink = new Sink();
        var calls = 0;
        sink.Release.Set();
        sink.DuringTone = () =>
        {
            if (Interlocked.Increment(ref calls) == 2)
            {
                current.TryRemove("reminder", out _);
            }
        };
        using var queue = new AudioQueue(sink, item => item.Test || current.ContainsKey(item.Key), () => new() { MicrophoneAwareVoice = false });
        queue.Enqueue(new("occupy", "a", "first", true));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        queue.Enqueue(new("reminder", "r", "Still waiting", Reason: AttentionReason.Question, Kind: AudioNotificationKind.Reminder));
        queue.Enqueue(new("alert-one", "a", "first alert", Reason: AttentionReason.Permission));
        queue.Enqueue(new("alert-two", "b", "second alert", Reason: AttentionReason.Error));
        sink.Release.Set();

        Assert.True(sink.Next.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal("3 sessions waiting", sink.Phrases.ElementAt(1));
        Assert.Contains(true, sink.SpeechCancelled);
        Assert.DoesNotContain("Still waiting", sink.Phrases);
    }

    [Fact]
    public void RecentInputDuringMixedAggregateToneCancelsReminderAndAggregateSpeechButKeepsOrdinaryTone()
    {
        var now = DateTimeOffset.UtcNow;
        var idle = new MutableIdleSource(new(TimeSpan.FromMinutes(20), now, "idle fixture"));
        var sink = new Sink
        {
            DuringTone = () => idle.Snapshot = new(TimeSpan.Zero, DateTimeOffset.UtcNow, "recent input")
        };
        sink.Release.Set();
        bool Valid(AudioItem item) => item.Kind != AudioNotificationKind.Reminder ||
            idle.GetSnapshot().IsFresh(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(500)) && idle.Snapshot.IdleFor >= TimeSpan.FromMinutes(5);
        using var queue = new AudioQueue(sink, Valid, () => new() { MicrophoneAwareVoice = false });
        queue.Enqueue(new("reminder", "r", "Still waiting", Reason: AttentionReason.Question, Kind: AudioNotificationKind.Reminder));
        queue.Enqueue(new("alert-one", "a", "ordinary one", Reason: AttentionReason.Permission));
        queue.Enqueue(new("alert-two", "b", "ordinary two", Reason: AttentionReason.Error));

        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(sink.SpeechCancelled.Single());
        Assert.False(sink.ToneCancelled.Single());
        Assert.Equal("3 sessions waiting", sink.Phrases.Single()); // passed to the silent sink, but cancellation forbids speaking this stale count
    }

    private sealed class MutableIdleSource(UserIdleSnapshot snapshot) : IUserIdleSource
    {
        public UserIdleSnapshot Snapshot = snapshot;
        public UserIdleSnapshot GetSnapshot() => Snapshot;
    }

    [Fact]
    public void VoiceOffStillRequestsAChimeThroughTheSink()
    {
        var sink = new Sink();
        sink.Release.Set();
        var config = new AppConfig
        {
            Voice = false
        };
        using var queue = new AudioQueue(sink, _ => true, () => config, Inactive());
        queue.Enqueue(new("wait", "e", "not spoken", Reason: AttentionReason.Permission));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Single());
        Assert.Equal(AttentionReason.Permission, sink.Reasons.Single());
    }

    [Theory]
    [InlineData(MicrophoneUsageState.Active, "claude:session")]
    [InlineData(MicrophoneUsageState.Active, "codex:session")]
    [InlineData(MicrophoneUsageState.Unknown, "claude:session")]
    [InlineData(MicrophoneUsageState.Unknown, "codex:session")]
    public void ActiveOrUnknownKeepsToneAndSuppressesVoice(MicrophoneUsageState state, string key)
    {
        var sink = new Sink();
        sink.Release.Set();
        using var queue = new AudioQueue(sink, _ => true, () => new(), new Microphone(new(state, DateTimeOffset.UtcNow, "fixture")));
        queue.Enqueue(new(key, "episode-a", "private phrase", Reason: AttentionReason.Question));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Single());
        Assert.Equal(AttentionReason.Question, sink.Reasons.Single());
    }

    [Fact]
    public void FreshInactiveAllowsSpeech()
    {
        var sink = new Sink();
        sink.Release.Set();
        using var queue = new AudioQueue(sink, _ => true, () => new(), Inactive());
        queue.Enqueue(new("provider-session", "episode-a", "spoken phrase"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal("spoken phrase", sink.Phrases.Single());
    }

    [Fact]
    public void MicrophoneStartingAfterEnqueueSuppressesTheQueuedPhrase()
    {
        var microphone = Inactive();
        var sink = new Sink();
        using var queue = new AudioQueue(sink, _ => true, () => new(), microphone);
        queue.Enqueue(new("occupy", "episode-a", "first", true));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        queue.Enqueue(new("claude:session", "episode-b", "queued before microphone starts"));
        microphone.Snapshot = new(MicrophoneUsageState.Active, DateTimeOffset.UtcNow, "fixture");
        sink.Release.Set();
        Assert.True(sink.Next.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Last());
    }

    [Fact]
    public void MicrophoneActivationAfterToneCancelsSpeechOnly()
    {
        var microphone = Inactive();
        var sink = new Sink { DuringTone = () => microphone.Snapshot = new(MicrophoneUsageState.Active, DateTimeOffset.UtcNow, "fixture") };
        sink.Release.Set();
        using var queue = new AudioQueue(sink, _ => true, () => new(), microphone);
        queue.Enqueue(new("provider-session", "episode-a", "discarded phrase"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(sink.SpeechCancelled.Single());
        Assert.False(sink.ToneCancelled.Single());
    }

    [Fact]
    public void MicrophoneActivationAfterSpeechBeginsCancelsRemainingSpeechWithoutReplay()
    {
        var microphone = Inactive();
        var sink = new Sink
        {
            DuringSpeech = () => microphone.Snapshot = new(MicrophoneUsageState.Active, DateTimeOffset.UtcNow, "fixture")
        };
        sink.Release.Set();
        using var queue = new AudioQueue(sink, _ => true, () => new(), microphone);
        queue.Enqueue(new("claude:session", "episode-a", "cancel remaining speech"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(sink.SpeechCancelled.Single());
        Assert.False(sink.ToneCancelled.Single());
        Thread.Sleep(250);
        Assert.Single(sink.Phrases);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void VoiceMuteAndMicrophoneOptOutMatrix(bool voice, bool muted, bool optedOut)
    {
        foreach (var state in new[] { MicrophoneUsageState.Inactive, MicrophoneUsageState.Active, MicrophoneUsageState.Unknown })
        {
            var sink = new Sink();
            sink.Release.Set();
            var config = new AppConfig { Voice = voice, Muted = muted, MicrophoneAwareVoice = !optedOut };
            using var queue = new AudioQueue(sink, _ => true, () => config,
                new Microphone(new(state, DateTimeOffset.UtcNow, "fixture")));
            queue.Enqueue(new("codex:session", "episode-a", "matrix phrase"));
            if (muted)
            {
                Thread.Sleep(100);
                Assert.Empty(sink.Phrases);
            }
            else
            {
                Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
                Assert.Single(sink.Phrases);
                Assert.Equal(voice && (optedOut || state == MicrophoneUsageState.Inactive) ? "matrix phrase" : null, sink.Phrases.Single());
                Assert.False(sink.ToneCancelled.Single());
            }

            Assert.Equal(voice, config.Voice);
            Assert.Equal(muted, config.Muted);
            Assert.Equal(!optedOut, config.MicrophoneAwareVoice);
        }
    }

    [Fact]
    public void StaleInactiveEvidenceDoesNotPermitSpeech()
    {
        var sink = new Sink();
        sink.Release.Set();
        var stale = new Microphone(new(MicrophoneUsageState.Inactive, DateTimeOffset.UtcNow.AddSeconds(-10), "stale fixture"));
        using var queue = new AudioQueue(sink, _ => true, () => new(), stale);
        queue.Enqueue(new("provider-session", "episode-a", "must remain private"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Single());
    }

    [Fact]
    public void SuppressedPhraseIsNotReplayedWhenMicrophoneBecomesInactive()
    {
        var microphone = new Microphone(new(MicrophoneUsageState.Active, DateTimeOffset.UtcNow, "fixture"));
        var sink = new Sink();
        using var queue = new AudioQueue(sink, _ => true, () => new(), microphone);
        queue.Enqueue(new("codex:session", "episode-a", "discard permanently"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        microphone.Snapshot = new(MicrophoneUsageState.Inactive, DateTimeOffset.UtcNow, "fixture");
        sink.Release.Set();
        Thread.Sleep(250);
        Assert.Single(sink.Phrases);
        Assert.Null(sink.Phrases.Single());
    }

    [Fact]
    public void MutedQueueDoesNotCallSinkAndUnmuteDoesNotReplayItsOldPhrase()
    {
        var sink = new Sink();
        var config = new AppConfig { Muted = true, MicrophoneAwareVoice = false };
        using var queue = new AudioQueue(sink, _ => true, () => config);
        queue.Enqueue(new("claude:session", "episode-a", "muted phrase"));
        Thread.Sleep(250);
        config.Muted = false;
        queue.Enqueue(new("codex:sentinel", "episode-b", "new phrase", true));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(new[] { "new phrase" }, sink.Phrases.ToArray());
    }

    [Fact]
    public void OptOutRestoresVoiceWithoutChangingMuteOrVoicePreferences()
    {
        var sink = new Sink();
        sink.Release.Set();
        var config = new AppConfig { MicrophoneAwareVoice = false, Voice = true, Muted = false };
        using var queue = new AudioQueue(sink, _ => true, () => config, new Microphone(MicrophoneActivitySnapshot.Unknown("fixture")));
        queue.Enqueue(new("provider-session", "episode-a", "opt-out phrase"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal("opt-out phrase", sink.Phrases.Single());
        Assert.True(config.Voice);
        Assert.False(config.Muted);
    }

    [Fact]
    public void MixedClaudeAndCodexAggregateUsesTheSameMicrophonePolicy()
    {
        var sink = new Sink();
        var config = new AppConfig();
        using var queue = new AudioQueue(sink, _ => true, () => config,
            new Microphone(new(MicrophoneUsageState.Active, DateTimeOffset.UtcNow, "fixture")));
        queue.Enqueue(new("claude:one", "episode-a", "one"));
        queue.Enqueue(new("codex:two", "episode-b", "two"));
        queue.Enqueue(new("claude:three", "episode-c", "three"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Single());
        Assert.Single(sink.Reasons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalAndReminderLikeMixedProviderAggregatesUseSharedPolicy(bool reminderLike)
    {
        var sink = new Sink();
        sink.Release.Set();
        using var queue = new AudioQueue(sink, _ => true, () => new(),
            new Microphone(new(MicrophoneUsageState.Active, DateTimeOffset.UtcNow, "fixture")));
        var prefix = reminderLike ? "reminder:" : string.Empty;
        queue.Enqueue(new(prefix + "claude:one", "episode-a", "one"));
        queue.Enqueue(new(prefix + "codex:two", "episode-b", "two"));
        queue.Enqueue(new(prefix + "claude:three", "episode-c", "three"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Single());
        Assert.Single(sink.Reasons);
    }

    [Fact]
    public void ProbeExceptionFailsClosedToToneOnly()
    {
        var sink = new Sink();
        sink.Release.Set();
        using var queue = new AudioQueue(sink, _ => true, () => new(), new FailedMicrophone());
        queue.Enqueue(new("codex:session", "episode-a", "private phrase"));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Single());
    }

    [Fact]
    public void BlockingNativeProbeReturnsUnknownAndKeepsOnlyOneInFlightQuery()
    {
        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        var calls = 0;
        var probe = new WindowsMicrophoneActivitySource(() =>
        {
            Interlocked.Increment(ref calls);
            started.Set();
            release.Wait(TimeSpan.FromSeconds(3));
            return CompleteInactiveProbe();
        });

        var first = probe.GetSnapshot();
        Assert.Equal(MicrophoneUsageState.Unknown, first.State);
        Assert.Contains("bounded wait", first.Reason);
        Assert.True(started.Wait(TimeSpan.FromSeconds(2)), "The blocked probe did not start.");
        Assert.Equal(MicrophoneUsageState.Unknown, probe.GetSnapshot().State);
        Assert.Equal(1, Volatile.Read(ref calls));

        release.Set();
        Assert.True(SpinWait.SpinUntil(() => probe.GetSnapshot().State == MicrophoneUsageState.Inactive, TimeSpan.FromSeconds(2)));
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public void NativeProbeExceptionReturnsUnknownInsteadOfEscapingDoctor()
    {
        var probe = new WindowsMicrophoneActivitySource(() => throw new InvalidOperationException("fixture"));
        var snapshot = probe.GetSnapshot();
        Assert.Equal(MicrophoneUsageState.Unknown, snapshot.State);
        Assert.Contains("failed", snapshot.Reason);
    }

    [Fact]
    public void SessionNotificationBufferMapsAndReleasesCallbackSessions()
    {
        var sessions = new List<FakeSession>();
        var buffer = new CallbackSessionBuffer<FakeSession>(session => session.Dispose());
        using (buffer)
        {
            sessions.Add(new(MicrophoneUsageState.Active));
            sessions.Add(new(MicrophoneUsageState.Inactive));
            foreach (var session in sessions)
            { buffer.Add(session); }
            Assert.Equal(new[] { MicrophoneUsageState.Active, MicrophoneUsageState.Inactive }, buffer.DrainAndMap(session => session.State));
            var pending = new FakeSession(MicrophoneUsageState.Active);
            sessions.Add(pending);
            buffer.Add(pending);
        }

        var late = new FakeSession(MicrophoneUsageState.Active);
        sessions.Add(late);
        buffer.Add(late);
        Assert.All(sessions, session => Assert.True(session.Disposed));
    }

    [Fact]
    public void SessionNotificationBufferReleasesAllControlsWhenStateReadFails()
    {
        var first = new FakeSession(MicrophoneUsageState.Active);
        var second = new FakeSession(MicrophoneUsageState.Inactive);
        using var buffer = new CallbackSessionBuffer<FakeSession>(session => session.Dispose());
        buffer.Add(first);
        buffer.Add(second);

        Assert.Throws<InvalidOperationException>(() => buffer.DrainAndMap<FakeSession>(session =>
            session == first ? throw new InvalidOperationException("fixture") : session));
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    [Fact]
    public void SessionNotificationBufferRejectsOverflowAndReleasesEveryControlExactlyOnce()
    {
        var sessions = Enumerable.Range(0, CallbackSessionBuffer<FakeSession>.Capacity + 1)
            .Select(_ => new FakeSession(MicrophoneUsageState.Inactive)).ToArray();
        using var buffer = new CallbackSessionBuffer<FakeSession>(session => session.Dispose());
        foreach (var session in sessions)
        {
            buffer.Add(session);
        }

        Assert.Equal(CallbackSessionBuffer<FakeSession>.Capacity, buffer.Count);
        Assert.True(buffer.Overflowed);
        Assert.True(sessions[^1].Disposed);
        buffer.Dispose();
        Assert.All(sessions, session => Assert.True(session.Disposed));
        Assert.All(sessions, session => Assert.Equal(1, session.ReleaseCount));
    }

    [Fact]
    public void SessionNotificationBufferDisposesConcurrentlyAndContinuesAfterReleaseFailure()
    {
        var sessions = Enumerable.Range(0, 16).Select(_ => new FakeSession(MicrophoneUsageState.Inactive)).ToArray();
        var calls = 0;
        var buffer = new CallbackSessionBuffer<FakeSession>(session =>
        {
            Interlocked.Increment(ref calls);
            session.Dispose();
            if (session == sessions[0])
            {
                throw new InvalidOperationException("release fixture");
            }
        });
        foreach (var session in sessions)
        {
            buffer.Add(session);
        }

        Assert.Throws<AggregateException>(() => Parallel.Invoke(buffer.Dispose, buffer.Dispose));
        Assert.Equal(sessions.Length, calls);
        Assert.All(sessions, session => Assert.True(session.Disposed));
        Assert.All(sessions, session => Assert.Equal(1, session.ReleaseCount));
        var late = new FakeSession(MicrophoneUsageState.Inactive);
        buffer.Add(late);
        Assert.True(late.Disposed);
        Assert.Equal(1, late.ReleaseCount);
        Assert.Equal(sessions.Length + 1, calls);
    }

    [Fact]
    public void NativeAudioContractsMatchWindowsSdkMetadataAndRawStateValues()
    {
        Type nested(string name) => typeof(WindowsMicrophoneActivitySource).GetNestedType(name,
            System.Reflection.BindingFlags.NonPublic)!;
        var expectedIids = new Dictionary<string, string>
        {
            ["IMMDeviceEnumerator"] = "A95664D2-9614-4F35-A746-DE8DB63617E6",
            ["IMMDeviceCollection"] = "0BD7A1BE-7A1A-44DB-8397-CC5392387B5E",
            ["IMMDevice"] = "D666063F-1587-4E43-81F1-B948E807363F",
            ["IAudioSessionManager2"] = "77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F",
            ["IAudioSessionEnumerator"] = "E2F5BB11-0570-40CA-ACDD-3AA01277DEE8",
            ["IAudioSessionControl"] = "F4B1A599-7266-4319-A8CA-E70ACB11E8CD",
            ["IAudioSessionNotification"] = "641DD20B-4D41-49CC-ABA3-174B9477BB08",
            ["IAudioVolumeDuckNotification"] = "C3B284D4-6D39-4359-B3CF-B56DDB3BB39C",
            ["IAudioSessionEvents"] = "24918ACC-64B3-37C1-8CA9-74A66E9957A8",
            ["IPropertyStore"] = "886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99",
            ["IMMNotificationClient"] = "7991EEC9-7E89-4D85-8390-6C703CEC60C0"
        };
        foreach (var (name, iid) in expectedIids)
        {
            Assert.Equal(iid, nested(name).GUID.ToString().ToUpperInvariant());
        }
        Assert.Equal(new[] { "GetAudioSessionControl", "GetSimpleAudioVolume", "GetSessionEnumerator", "RegisterSessionNotification", "UnregisterSessionNotification", "RegisterDuckNotification", "UnregisterDuckNotification" },
            nested("IAudioSessionManager2").GetMethods().OrderBy(method => method.MetadataToken).Select(method => method.Name));
        Assert.Equal(typeof(WindowsMicrophoneActivitySource).GetNestedType("IAudioSessionEnumerator", System.Reflection.BindingFlags.NonPublic),
            nested("IAudioSessionManager2").GetMethod("GetSessionEnumerator")!.GetParameters()[0].ParameterType.GetElementType());
        Assert.Equal(typeof(WindowsMicrophoneActivitySource).GetNestedType("IAudioSessionNotification", System.Reflection.BindingFlags.NonPublic),
            nested("IAudioSessionManager2").GetMethod("RegisterSessionNotification")!.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(WindowsMicrophoneActivitySource.AudioSessionState).MakeByRefType(),
            nested("IAudioSessionControl").GetMethod("GetState")!.GetParameters()[0].ParameterType);
        Assert.Equal(0, (int)WindowsMicrophoneActivitySource.AudioSessionState.Inactive);
        Assert.Equal(1, (int)WindowsMicrophoneActivitySource.AudioSessionState.Active);
        Assert.Equal(2, (int)WindowsMicrophoneActivitySource.AudioSessionState.Expired);
        Assert.Equal(MicrophoneUsageState.Inactive, WindowsMicrophoneActivitySource.MapNativeSessionState(0));
        Assert.Equal(MicrophoneUsageState.Active, WindowsMicrophoneActivitySource.MapNativeSessionState(1));
        Assert.Equal(MicrophoneUsageState.Unknown, WindowsMicrophoneActivitySource.MapNativeSessionState(2));
        Assert.Equal(MicrophoneUsageState.Unknown, WindowsMicrophoneActivitySource.MapNativeSessionState(99));
        Assert.Equal(MicrophoneUsageState.Unknown, WindowsMicrophoneActivitySource.Evaluate(new(true, false, true, 1,
            [new(true, 1, [WindowsMicrophoneActivitySource.MapNativeSessionState(2)])], ObservedAt: DateTimeOffset.UtcNow)).State);
    }

    [Fact]
    public void SpeechInitializationRechecksGeneralAndSpeechCancellationBeforeSpeak()
    {
        var spoke = false;
        var initialize = 0;
        Assert.False(WindowsAudioSink.TryBeginSpeech("private", () => false, () => true,
            () => initialize++, _ => spoke = true));
        Assert.Equal(1, initialize);
        Assert.False(spoke);

        Assert.False(WindowsAudioSink.TryBeginSpeech("private", () => true, () => false,
            () => initialize++, _ => spoke = true));
        Assert.Equal(2, initialize);
        Assert.False(spoke);
    }

    [Fact]
    public void PartialEndpointOrSessionObservationCannotBecomeInactive()
    {
        var partialEndpoint = new MicrophoneProbeResult(true, false, true, 2,
        [
            new(true, 0, []),
            new(false, 0, [])
        ], ObservedAt: DateTimeOffset.UtcNow);
        var incompleteSessions = new MicrophoneProbeResult(true, false, true, 1,
        [
            new(true, 2, [MicrophoneUsageState.Inactive])
        ], ObservedAt: DateTimeOffset.UtcNow);
        Assert.Equal(MicrophoneUsageState.Unknown, WindowsMicrophoneActivitySource.Evaluate(partialEndpoint).State);
        Assert.Equal(MicrophoneUsageState.Unknown, WindowsMicrophoneActivitySource.Evaluate(incompleteSessions).State);
    }

    [Fact]
    public void ActiveObservationWinsOverOtherEndpointFailures()
    {
        var result = new MicrophoneProbeResult(true, false, false, 2,
        [
            new(true, 1, [MicrophoneUsageState.Active]),
            new(false, 0, [])
        ], ObservedAt: DateTimeOffset.UtcNow);
        Assert.Equal(MicrophoneUsageState.Active, WindowsMicrophoneActivitySource.Evaluate(result).State);
    }

    [Fact]
    public void RemoteSessionAndEmptyEndpointInventoryRemainUnknown()
    {
        Assert.Equal(MicrophoneUsageState.Unknown,
            WindowsMicrophoneActivitySource.Evaluate(new(true, true, false, 0, [], "fixture")).State);
        Assert.Equal(MicrophoneUsageState.Unknown,
            WindowsMicrophoneActivitySource.Evaluate(new(true, false, true, 0, [], ObservedAt: DateTimeOffset.UtcNow)).State);
    }

    [Fact]
    public void DelayedInactiveScanIsRejectedAsStaleThenFreshScanRecovers()
    {
        using var release = new ManualResetEventSlim();
        using var firstStarted = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var clock = new ManualUtcClock(new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var calls = 0;
        var source = new WindowsMicrophoneActivitySource(() =>
        {
            var call = Interlocked.Increment(ref calls);
            var started = clock.Now();
            if (call == 1)
            {
                firstStarted.Set();
                release.Wait(TimeSpan.FromSeconds(3));
                return CompleteInactiveProbe(started);
            }

            secondStarted.Set();
            return CompleteInactiveProbe(started);
        }, clock.Now);

        Assert.Equal(MicrophoneUsageState.Unknown, source.GetSnapshot().State);
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(2)), "The initial delayed scan did not start.");
        clock.Advance(TimeSpan.FromMilliseconds(1100));
        release.Set();
        Assert.True(SpinWait.SpinUntil(() => source.GetSnapshot().Reason.Contains("stale", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(2)));
        clock.Advance(TimeSpan.FromMilliseconds(1100));
        source.GetSnapshot();
        Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(2)), "A fresh replacement scan did not start.");
        Assert.True(SpinWait.SpinUntil(() => source.GetSnapshot().State == MicrophoneUsageState.Inactive, TimeSpan.FromSeconds(3)));
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    [Fact]
    public void CachedInactiveExpiresFromOriginalObservationTimeAndFreshScanRecovers()
    {
        using var firstRelease = new ManualResetEventSlim();
        using var secondRelease = new ManualResetEventSlim();
        using var firstStarted = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var clock = new ManualUtcClock(new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var calls = 0;
        var source = new WindowsMicrophoneActivitySource(() =>
        {
            var call = Interlocked.Increment(ref calls);
            var observedAt = clock.Now();
            if (call == 1)
            {
                firstStarted.Set();
                firstRelease.Wait(TimeSpan.FromSeconds(3));
            }
            else
            {
                secondStarted.Set();
                secondRelease.Wait(TimeSpan.FromSeconds(3));
            }
            return CompleteInactiveProbe(observedAt);
        }, clock.Now);

        Assert.Equal(MicrophoneUsageState.Unknown, source.GetSnapshot().State);
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(2)), "The initial probe did not start.");
        clock.Advance(TimeSpan.FromMilliseconds(800));
        firstRelease.Set();
        Assert.True(SpinWait.SpinUntil(() => source.GetSnapshot().State == MicrophoneUsageState.Inactive, TimeSpan.FromSeconds(2)));
        clock.Advance(TimeSpan.FromMilliseconds(250));
        Assert.Equal(MicrophoneUsageState.Unknown, source.GetSnapshot().State);
        Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(2)), "The expired cache did not start a replacement probe.");
        Assert.Equal(2, Volatile.Read(ref calls));
        secondRelease.Set();
        Assert.True(SpinWait.SpinUntil(() => source.GetSnapshot().State == MicrophoneUsageState.Inactive, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void FutureNativeObservationIsUnknown()
    {
        Assert.Equal(MicrophoneUsageState.Unknown,
            WindowsMicrophoneActivitySource.Evaluate(CompleteInactiveProbe(DateTimeOffset.UtcNow.AddSeconds(1)), DateTimeOffset.UtcNow).State);
    }

    private static Microphone Inactive() => new(new(MicrophoneUsageState.Inactive, DateTimeOffset.UtcNow, "fixture"));

    private static MicrophoneProbeResult CompleteInactiveProbe(DateTimeOffset? observedAt = null) =>
        new(true, false, true, 1, [new(true, 0, [])], ObservedAt: observedAt ?? DateTimeOffset.UtcNow);
}

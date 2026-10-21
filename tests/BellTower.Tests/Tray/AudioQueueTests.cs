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
        public readonly ManualResetEventSlim Started = new();
        public readonly ManualResetEventSlim Release = new();
        public readonly ManualResetEventSlim Next = new();
        private int calls;
        public void Play(string? phrase, AttentionReason reason, Func<bool> cancelled)
        {
            Phrases.Enqueue(phrase);
            Reasons.Enqueue(reason);
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

    [Fact]
    public void ThreePendingPhrasesAggregateOnTheRealWorkerWithoutAudio()
    {
        var sink = new Sink();
        var config = new AppConfig();
        using var queue = new AudioQueue(sink, _ => true, () => config);
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
    public void ResolvedQueuedWaitIsExcludedBeforePlayback()
    {
        var sink = new Sink();
        var current = new ConcurrentDictionary<string, bool>();
        current["wait"] = true;
        using var queue = new AudioQueue(sink, item => current.ContainsKey(item.Key), () => new());
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
    public void VoiceOffStillRequestsAChimeThroughTheSink()
    {
        var sink = new Sink();
        sink.Release.Set();
        var config = new AppConfig
        {
            Voice = false
        };
        using var queue = new AudioQueue(sink, _ => true, () => config);
        queue.Enqueue(new("wait", "e", "not spoken", Reason: AttentionReason.Permission));
        Assert.True(sink.Started.Wait(TimeSpan.FromSeconds(2)));
        Assert.Null(sink.Phrases.Single());
        Assert.Equal(AttentionReason.Permission, sink.Reasons.Single());
    }
}

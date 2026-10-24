namespace BellTower.Core;

// Provider-neutral change-driven schedule for native name synchronization: a provider call is due when the
// desired name changed, when the steady-state verification interval has elapsed, or when the failure
// backoff has expired. Owned by one long-lived pump; never persisted.
public sealed class NativeNameSchedule(TimeSpan verifyEvery, Func<DateTimeOffset>? clock = null)
{
    public static readonly int[] BackoffSeconds = [2, 5, 15, 30, 60];

    private sealed record Entry(string Desired, DateTimeOffset Next, int Failures);

    private readonly Func<DateTimeOffset> clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public bool Due(string key, string desired, bool force = false)
    {
        lock (gate)
        {
            return force || !entries.TryGetValue(key, out var entry) || entry.Desired != desired || clock() >= entry.Next;
        }
    }

    public void Succeeded(string key, string desired)
    {
        lock (gate)
        { entries[key] = new(desired, clock() + verifyEvery, 0); }
    }

    public void Failed(string key, string desired)
    {
        lock (gate)
        {
            var failures = entries.TryGetValue(key, out var entry) && entry.Desired == desired ? entry.Failures : 0;
            entries[key] = new(desired, clock().AddSeconds(BackoffSeconds[Math.Min(failures, BackoffSeconds.Length - 1)]), failures + 1);
        }
    }

    public int Failures(string key)
    {
        lock (gate)
        { return entries.TryGetValue(key, out var entry) ? entry.Failures : 0; }
    }

    public void Retain(IEnumerable<string> live)
    {
        lock (gate)
        {
            var keep = live.ToHashSet(StringComparer.Ordinal);
            foreach (var key in entries.Keys.Where(key => !keep.Contains(key)).ToArray())
            { entries.Remove(key); }
        }
    }
}

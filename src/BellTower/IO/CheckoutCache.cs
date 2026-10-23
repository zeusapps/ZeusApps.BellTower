using BellTower.Core;

namespace BellTower.IO;

// One tray-owned, bounded, serialized discovery boundary. Callers share immutable values.
public sealed class CheckoutCache(Func<string, GitContext>? read = null, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<string, GitContext> read = read ?? new GitReader().ReadUncached;
    private readonly Func<DateTimeOffset> clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset At, GitContext Value)> cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<GitContext> ReadAsync(string directory, CancellationToken cancellation = default)
    {
        var key = string.IsNullOrWhiteSpace(directory) ? "" : Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            if (cache.TryGetValue(key, out var hit) && clock() - hit.At < TimeSpan.FromSeconds(5))
            {
                return hit.Value;
            }
            var value = await Task.Run(() => read(directory), cancellation).ConfigureAwait(false);
            if (cache.Count >= 256 && !cache.ContainsKey(key))
            {
                cache.Remove(cache.MinBy(pair => pair.Value.At).Key);
            }
            cache[key] = (clock(), value);
            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task InvalidateAsync(string directory, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            var key = string.IsNullOrWhiteSpace(directory) ? "" : Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            cache.Remove(key);
        }
        finally
        {
            gate.Release();
        }
    }
}

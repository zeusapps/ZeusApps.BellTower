using System.Collections.Concurrent;
using BellTower.Protocol;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>One durable capture file seen in the inbox: the envelope id and ingress time from its name, when it became visible and who wrote it.</summary>
public sealed record ObservedCapture(string Id, long IngressTicks, DateTime ObservedUtc, DateTime? LastWriteUtc, string? Provider, string? SessionId);

/// <summary>
/// Watches a capture inbox while a running tray consumes it. A file name is <c>&lt;ingress ticks&gt;-&lt;envelope id&gt;.json</c> and appears
/// atomically (a temporary file is flushed and then moved), so the moment its name is first seen is the moment the capture became
/// durable (a few milliseconds later at most). The file is read at once for its provider and session, because the tray deletes it within
/// one tick (250 ms) after committing it. A directory poll runs beside the file system watcher so a dropped notification cannot lose one.
/// Safe to read from other threads.
/// </summary>
public sealed class CaptureObserver : IDisposable
{
    private readonly string inbox;
    private readonly ConcurrentDictionary<string, ObservedCapture> seen = new(StringComparer.Ordinal);
    private readonly FileSystemWatcher watcher;
    private readonly CancellationTokenSource stop = new();
    private readonly Thread poller;
    private int watcherErrors;

    public CaptureObserver(string inbox)
    {
        this.inbox = inbox;
        Directory.CreateDirectory(inbox);
        watcher = new FileSystemWatcher(inbox) { InternalBufferSize = 64 * 1024, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
        watcher.Created += (_, e) => Handle(e.FullPath);
        watcher.Renamed += (_, e) => Handle(e.FullPath);
        watcher.Error += (_, _) => Interlocked.Increment(ref watcherErrors);
        watcher.EnableRaisingEvents = true;
        poller = new Thread(Poll) { IsBackground = true, Name = "capture-observer" };
        poller.Start();
    }

    public IReadOnlyCollection<ObservedCapture> Captures => seen.Values.ToArray();

    public int WatcherErrors => Volatile.Read(ref watcherErrors);

    private void Poll()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(inbox, "*.json"))
                {
                    Handle(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The directory is being replaced or a file vanished mid-listing; the next pass sees the rest.
            }
            stop.Token.WaitHandle.WaitOne(8);
        }
    }

    /// <summary>Parses a capture file name into its envelope id and ingress ticks, or null for anything else (a temporary file, a rejection).</summary>
    public static (string Id, long Ticks)? ParseName(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Length != 19 + 1 + 32 + 5 || !name.EndsWith(".json", StringComparison.Ordinal) || name[19] != '-' ||
            !long.TryParse(name.AsSpan(0, 19), out var ticks) || !Guid.TryParseExact(name.AsSpan(20, 32), "N", out _))
        {
            return null;
        }
        return (name.Substring(20, 32), ticks);
    }

    private void Handle(string path)
    {
        if (ParseName(path) is not { } parsed || seen.ContainsKey(parsed.Id))
        {
            return;
        }
        var observed = DateTime.UtcNow;
        DateTime? lastWrite = null;
        string? provider = null, session = null;
        try
        {
            lastWrite = File.GetLastWriteTimeUtc(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var envelope = CaptureProtocol.Decode(stream);
            provider = envelope.Observation.Provider;
            session = envelope.Observation.SessionId;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            // The tray consumed it first: the name alone still counts as one durable capture, but who wrote it stays unknown.
        }
        // A missing file reads as a time in 1601; keep only a real modification time.
        seen.TryAdd(parsed.Id, new ObservedCapture(parsed.Id, parsed.Ticks, observed, lastWrite is { Year: > 1700 } ? lastWrite : null, provider, session));
    }

    /// <summary>
    /// Fills in the writer of captures the tray consumed before they could be read, from what the tray itself applied (the envelope id names the
    /// capture in each session's causal journal, with its provider and session). The matching of captures to hooks then no longer depends on winning
    /// that race; a capture the tray never applied stays unresolved, so the exact counts stay strict. Returns how many were resolved.
    /// </summary>
    public int ResolveFrom(IReadOnlyDictionary<string, (string Provider, string Session)> applied)
    {
        var resolved = 0;
        foreach (var capture in seen.Values.Where(capture => capture.Provider is null))
        {
            if (applied.TryGetValue(capture.Id, out var writer) && seen.TryUpdate(capture.Id, capture with { Provider = writer.Provider, SessionId = writer.Session }, capture))
            {
                resolved++;
            }
        }
        return resolved;
    }

    public void Dispose()
    {
        stop.Cancel();
        watcher.Dispose();
        poller.Join(2000);
        stop.Dispose();
    }
}

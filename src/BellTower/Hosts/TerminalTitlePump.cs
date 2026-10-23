using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalTitlePump : IDisposable
{
    private readonly TerminalPresentationResolver resolver;
    private readonly CheckoutCache checkout;
    private readonly ITerminalPresentationWriter writer;
    private readonly Func<DateTimeOffset> clock;
    private readonly Action<string> log;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Dictionary<string, Destination> destinations = [];
    private Task? running;
    private bool disposed;
    private DateTimeOffset? lastRefreshFailure;

    private sealed class Destination
    {
        public DesiredTerminalPresentation? Desired { get; set; }
        public DesiredTerminalPresentation? Successful { get; set; }
        public int Failures { get; set; }
        public DateTimeOffset Next { get; set; }
        public DateTimeOffset LastLog { get; set; }
    }

    public TerminalTitlePump(DataPaths paths, CheckoutCache? checkout = null, ITerminalPresentationWriter? writer = null,
        Func<DateTimeOffset>? clock = null, Action<string>? log = null)
    {
        resolver = new(paths);
        this.checkout = checkout ?? new();
        this.writer = writer ?? new IsolatedTerminalPresentationWriter(paths);
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.log = log ?? new LocalLog(paths).Write;
    }

    public void Update(IReadOnlyList<SessionState> sessions)
    {
        if (!disposed && (running is null || running.IsCompleted))
        {
            running = Task.Run(() => RefreshAsync(sessions));
        }
    }

    public async Task RefreshAsync(IReadOnlyList<SessionState> sessions)
    {
        if (disposed || !await gate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }
        try
        {
            var live = sessions.Where(session => !session.Ended).GroupBy(session => session.AttachmentId)
                .Select(group => group.MaxBy(session => session.LastObserved)!).ToArray();
            var ids = live.Select(session => session.AttachmentId).ToHashSet(StringComparer.Ordinal);
            foreach (var id in destinations.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                destinations.Remove(id);
            }
            foreach (var session in live)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var basis = resolver.Resolve(session, new());
                if (basis is null)
                {
                    continue;
                }
                var git = await checkout.ReadAsync(session.Directory, cancellation.Token).ConfigureAwait(false);
                var desired = basis with { Title = SessionPresentation.Title(basis.Title, git) };
                if (!destinations.TryGetValue(desired.AttachmentId, out var target))
                {
                    target = new();
                    destinations.Add(desired.AttachmentId, target);
                }
                if (target.Desired != desired)
                {
                    target.Desired = desired;
                    target.Failures = 0;
                    target.Next = default;
                }
                if (target.Successful == desired || clock() < target.Next)
                {
                    continue;
                }
                var success = false;
                // A failed/unknown result may follow a partial title or palette mutation.
                // The prior receipt no longer establishes the destination's current state.
                target.Successful = null;
                try
                {
                    success = await writer.WriteAsync(desired, cancellation.Token).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException or
                    UnauthorizedAccessException or System.ComponentModel.Win32Exception)
                {
                    // Failures belong to this destination; continue other conversations.
                }
                if (success)
                {
                    target.Successful = desired;
                    if (target.Failures > 0)
                    {
                        log("title-recovered");
                    }
                    target.Failures = 0;
                    target.Next = default;
                }
                else
                {
                    var delays = new[] { 2, 5, 15, 30, 60 };
                    target.Next = clock().AddSeconds(delays[Math.Min(target.Failures, delays.Length - 1)]);
                    if (target.Failures == 0 || clock() - target.LastLog >= TimeSpan.FromMinutes(1))
                    {
                        log("title-unavailable");
                        target.LastLog = clock();
                    }
                    target.Failures = Math.Min(target.Failures + 1, delays.Length);
                }
            }
            lastRefreshFailure = null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or System.Text.Json.JsonException)
        {
            if (lastRefreshFailure is null || clock() - lastRefreshFailure >= TimeSpan.FromMinutes(1))
            {
                log("title-refresh-unavailable");
                lastRefreshFailure = clock();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        disposed = true;
        cancellation.Cancel();
        running?.GetAwaiter().GetResult();
        if (writer is IDisposable disposable)
        {
            disposable.Dispose();
        }
        cancellation.Dispose();
    }
}

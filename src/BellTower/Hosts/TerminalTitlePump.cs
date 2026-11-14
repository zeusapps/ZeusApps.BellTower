using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalTitlePump : IDisposable
{
    private readonly DataPaths paths;
    private readonly TerminalPresentationResolver resolver;
    private readonly IdentityStore identities;
    private readonly CheckoutCache checkout;
    private readonly ITerminalPresentationWriter writer;
    private readonly Func<DateTimeOffset> clock;
    private readonly Action<string> log;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Dictionary<string, Destination> destinations = [];
    private readonly Dictionary<string, DateTimeOffset> restoreRetry = [];
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
        this.paths = paths;
        resolver = new(paths);
        identities = new(paths);
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
            var presented = new HashSet<string>(StringComparer.Ordinal);
            foreach (var session in live)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var basis = resolver.Resolve(session, new());
                if (basis is null)
                {
                    continue;
                }
                presented.Add(basis.AttachmentId);
                var identity = identities.Get(session.Key, session.Directory);
                var git = await checkout.ReadAsync(identity.Checkout(session.Directory), cancellation.Token).ConfigureAwait(false);
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
            await RestoreUnpresentedAsync(presented).ConfigureAwait(false);
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

    // A native title Bell Tower recorded is given back once its session is no longer presented: it ended, its agent or shell
    // identity changed, or the native-presentation setting was turned off. The helper checks the recorded shell identity itself.
    private async Task RestoreUnpresentedAsync(HashSet<string> presented)
    {
        var records = new TitleRestoreStore(paths).List();
        // One request per owning attachment: after a resume it can own records in more than one shell, and the helper restores each.
        foreach (var owner in records.Where(record => !presented.Contains(record.AttachmentId)).GroupBy(record => record.AttachmentId))
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (restoreRetry.TryGetValue(owner.Key, out var next) && clock() < next)
            {
                continue;
            }
            destinations.Remove(owner.Key);
            bool done;
            try
            {
                done = await writer.RestoreAsync(owner.Key, cancellation.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or
                UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                done = false;
            }
            // The helper restores each record on its own, so a partial failure still gave back the shells whose record is gone.
            // A session still presented in such a shell lost its title; forgetting its receipt makes it write again.
            var kept = new TitleRestoreStore(paths).List();
            var restored = owner.Where(record => !kept.Any(item => item.Shell == record.Shell)).ToArray();
            foreach (var shared in destinations.Where(item => restored.Any(record => item.Value.Desired?.Shell == record.Shell)).Select(item => item.Key).ToArray())
            {
                destinations.Remove(shared);
            }
            if (done)
            {
                restoreRetry.Remove(owner.Key);
            }
            else
            {
                restoreRetry[owner.Key] = clock().AddSeconds(30);
                log("title-restore-unavailable");
            }
        }
        foreach (var id in restoreRetry.Keys.Where(id => records.All(record => record.AttachmentId != id)).ToArray())
        {
            restoreRetry.Remove(id);
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

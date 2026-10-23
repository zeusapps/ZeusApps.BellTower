using BellTower.Core;
using BellTower.IO;

namespace BellTower.Sessions;

public sealed class CodexNamePump(DataPaths paths, CheckoutCache? checkout = null) : IDisposable
{
    private readonly CheckoutCache checkout = checkout ?? new();
    private int running;
    private volatile bool disposed;
    private DateTimeOffset next;

    public void Update(IReadOnlyList<SessionState> sessions)
    {
        if (disposed || DateTimeOffset.UtcNow < next || Interlocked.CompareExchange(ref running, 1, 0) != 0)
        {
            return;
        }
        next = DateTimeOffset.UtcNow.AddSeconds(2);
        _ = Task.Run(async () =>
        {
            try
            {
                var service = new CodexNameService(paths);
                foreach (var session in sessions.Where(value => value.Provider == "codex" && !value.Ended && value.AttachmentHealth != "unavailable"))
                {
                    if (disposed)
                    {
                        break;
                    }
                    service.Sync(session, await checkout.ReadAsync(session.Directory).ConfigureAwait(false));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                new LocalLog(paths).Write("native-name-refresh-failed");
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        });
    }

    public void Dispose() => disposed = true;
}

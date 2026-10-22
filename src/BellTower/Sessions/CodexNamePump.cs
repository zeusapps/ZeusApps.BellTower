using BellTower.Core;
using BellTower.IO;

namespace BellTower.Sessions;

public sealed class CodexNamePump(DataPaths paths) : IDisposable
{
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
        _ = Task.Run(() =>
        {
            try
            {
                var service = new CodexNameService(paths);
                foreach (var session in sessions.Where(value => value.Provider == "codex" && !value.Ended))
                {
                    if (disposed)
                    {
                        break;
                    }
                    service.Sync(session);
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

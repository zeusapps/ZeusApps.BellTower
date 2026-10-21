using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalTitlePump(DataPaths paths) : IDisposable
{
    private int running;
    private volatile bool disposed;

    public void Update(IReadOnlyList<SessionState> sessions)
    {
        if (disposed || Interlocked.CompareExchange(ref running, 1, 0) != 0)
        {
            return;
        }
        _ = RefreshAsync(sessions);
    }

    private async Task RefreshAsync(IReadOnlyList<SessionState> sessions)
    {
        try
        {
            foreach (var session in sessions.Where(session => !session.Ended).GroupBy(session => session.AttachmentId)
                .Select(group => group.MaxBy(session => session.LastObserved)!))
            {
                if (disposed)
                {
                    break;
                }
                var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
                if (attachment is not { ManagedTitle: true, EndedAt: null })
                {
                    continue;
                }
                var start = new ProcessStartInfo(Environment.ProcessPath!)
                {
                    UseShellExecute = false,
                    WorkingDirectory = AppContext.BaseDirectory,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in new[] { "sync-title", "--session", session.Key, "--data-dir", paths.Root })
                {
                    start.ArgumentList.Add(argument);
                }
                using var child = Process.Start(start);
                if (child is null)
                {
                    continue;
                }
                var output = child.StandardOutput.ReadToEndAsync();
                var error = child.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await child.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await Task.WhenAll(output, error).ConfigureAwait(false);
                if (child.ExitCode != 0)
                {
                    new LocalLog(paths).Write("title-unavailable");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
            System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            new LocalLog(paths).Write("title-refresh-failed");
        }
        finally
        {
            Interlocked.Exchange(ref running, 0);
        }
    }

    public void Dispose() => disposed = true;
}

using System.Diagnostics;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class IsolatedTerminalPresentationWriter(DataPaths paths, string? executable = null) : ITerminalPresentationWriter, IDisposable
{
    private readonly OwnedProcessRunner runner = new();
    public int LaunchCount => runner.LaunchCount;
    public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation)
    {
        var start = Start("--desired");
        return runner.RunAsync(start, Json.Encode(desired), TimeSpan.FromSeconds(3), cancellation);
    }

    public Task<bool> RestoreAsync(string attachmentId, CancellationToken cancellation) =>
        runner.RunAsync(Start("--restore", attachmentId), "", TimeSpan.FromSeconds(3), cancellation);

    private ProcessStartInfo Start(params string[] mode)
    {
        var start = new ProcessStartInfo(executable ?? Environment.ProcessPath!) { WorkingDirectory = AppContext.BaseDirectory };
        start.ArgumentList.Add("sync-title");
        foreach (var argument in mode.Concat(["--data-dir", paths.Root]))
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    public void Dispose() => runner.Dispose();
}

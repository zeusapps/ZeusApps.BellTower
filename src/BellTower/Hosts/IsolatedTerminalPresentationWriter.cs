using System.Diagnostics;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class IsolatedTerminalPresentationWriter(DataPaths paths, string? executable = null) : ITerminalPresentationWriter, IDisposable
{
    private readonly OwnedProcessRunner runner = new();
    public int LaunchCount => runner.LaunchCount;
    public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo(executable ?? Environment.ProcessPath!) { WorkingDirectory = AppContext.BaseDirectory };
        foreach (var argument in new[] { "sync-title", "--desired", "--data-dir", paths.Root })
        {
            start.ArgumentList.Add(argument);
        }
        return runner.RunAsync(start, Json.Encode(desired), TimeSpan.FromSeconds(3), cancellation);
    }

    public void Dispose() => runner.Dispose();
}

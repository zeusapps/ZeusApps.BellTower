using System.Diagnostics;
using BellTower.IO;

namespace BellTower.Tray;

public sealed class TrayController(DataPaths paths)
{
    public bool IsRunning()
    {
        if (!Mutex.TryOpenExisting(paths.LockName("tray-instance"), out var mutex))
        {
            return false;
        }

        mutex.Dispose();
        return true;
    }

    public void Start(bool silent)
    {
        if (BellTower.Updates.UpdateTransaction.RestartPending(paths))
        { return; }
        if (IsRunning())
        {
            return;
        }

        var start = new ProcessStartInfo(BellTower.Distribution.Installation.StableExecutable(Environment.ProcessPath!))
        {
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            "tray",
            "--data-dir",
            paths.Root
        })
        {
            start.ArgumentList.Add(argument);
        }

        if (silent)
        {
            start.ArgumentList.Add("--silent");
        }

        Process.Start(start)?.Dispose();
    }

    public void RequestExit() => Request("exit.request", "exit");
    public void RequestTestSound() => Request("sound.request", "test");
    private void Request(string fileName, string command)
    {
        if (!IsRunning())
        {
            throw new InvalidOperationException("Bell Tower is not running for this data directory.");
        }

        AtomicFile.Write(Path.Combine(paths.Root, fileName), command);
    }
}

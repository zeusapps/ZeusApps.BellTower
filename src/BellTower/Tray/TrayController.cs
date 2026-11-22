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
    /// <summary>The user's own stop (the exit command): it also keeps hooks from restarting the tray for the rest of this sign-in. Update, switch and recovery stops use <see cref="RequestExit"/>.</summary>
    public void RequestUserExit() => Request("exit.request", "exit", () => BellTower.Protocol.HookTrayStart.RecordUserStop(paths.Root));
    public void RequestTestSound() => Request("sound.request", "test");
    private void Request(string fileName, string command, Action? beforeRequest = null)
    {
        if (!IsRunning())
        {
            throw new InvalidOperationException("Bell Tower is not running for this data directory.");
        }

        beforeRequest?.Invoke();
        AtomicFile.Write(Path.Combine(paths.Root, fileName), command);
    }
}

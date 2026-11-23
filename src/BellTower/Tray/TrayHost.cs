using BellTower.IO;

namespace BellTower.Tray;

public sealed class TrayHost(DataPaths paths)
{
    public int Run(bool silent, int durationMilliseconds = 0, bool hookStarted = false)
    {
        if (!BellTower.Updates.UpdateTransaction.CanStartTray(paths))
        { return 0; }
        if (TrayJobGuard.Refuses(paths, hookStarted))
        { return 0; }
        using var instance = new Mutex(true, paths.LockName("tray-instance"), out var created);
        if (!created)
        {
            return 0;
        }
        // This tray holds the instance now, so an earlier job refusal no longer describes the state of the tray.
        BellTower.Protocol.HookTrayStart.ClearJobRefusal(paths.Root);
        BellTower.Distribution.DistributionJson.Write(BellTower.Updates.UpdateTransaction.Presence(paths) + ".starting",
            new BellTower.Updates.TrayPresence(BellTower.IO.ProcessProbe.Identify(Environment.ProcessId)!, BellTower.Cli.ApplicationInfo.Version,
                paths.Root, Environment.GetEnvironmentVariable("BELLTOWER_UPDATE_ACTIVATION"), Capture: 1));

        File.Delete(Path.Combine(paths.Root, "exit.request"));
        File.Delete(Path.Combine(paths.Root, "sound.request"));
        RegisterStart(Environment.ProcessPath);
        ApplicationConfiguration.Initialize();
        using var app = new TrayApplication(paths, silent);
        using var stop = new System.Windows.Forms.Timer();
        if (durationMilliseconds > 0)
        {
            stop.Interval = durationMilliseconds;
            stop.Tick += (_, _) =>
            {
                stop.Stop();
                Application.ExitThread();
            };
            stop.Start();
        }

        Application.Run(app);
        return 0;
    }

    /// <summary>
    /// A tray that started (by hand, from a launch, after an update or from a hook) ends any user stop, and a tray run from the
    /// installation records its stable launcher so a later hook can start it again.
    /// </summary>
    internal void RegisterStart(string? executable)
    {
        BellTower.Protocol.HookTrayStart.ClearUserStop(paths.Root);
        try
        {
            if (executable is not null && BellTower.Distribution.Installation.ForPayload(executable) is { } installation)
            {
                BellTower.Protocol.HookTrayStart.RecordLauncher(paths.Root, installation.Launcher);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException) { }
    }
}

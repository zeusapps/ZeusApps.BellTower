using BellTower.IO;

namespace BellTower.Tray;

public sealed class TrayHost(DataPaths paths)
{
    public int Run(bool silent, int durationMilliseconds = 0)
    {
        if (!BellTower.Updates.UpdateTransaction.CanStartTray(paths))
        { return 0; }
        using var instance = new Mutex(true, paths.LockName("tray-instance"), out var created);
        if (!created)
        {
            return 0;
        }
        BellTower.Distribution.DistributionJson.Write(BellTower.Updates.UpdateTransaction.Presence(paths) + ".starting",
            new BellTower.Updates.TrayPresence(BellTower.IO.ProcessProbe.Identify(Environment.ProcessId)!, BellTower.Cli.ApplicationInfo.Version,
                paths.Root, Environment.GetEnvironmentVariable("BELLTOWER_UPDATE_ACTIVATION"), Capture: 1));

        File.Delete(Path.Combine(paths.Root, "exit.request"));
        File.Delete(Path.Combine(paths.Root, "sound.request"));
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
}

using System.Diagnostics;
using BellTower.Distribution;

namespace BellTower.Launcher;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var installation = new Installation(Path.GetDirectoryName(Environment.ProcessPath!)!);
            FileStream lease;
            Process child;
            using (var gate = new InstallationGate(installation.Root))
            {
                var selected = installation.Selection();
                var contract = DistributionJson.Read<ReleaseContract>(Path.Combine(installation.Payload(selected.Version), "release.json"));
                CaptureCompatibility.EnsureTrayInvocation(args, contract.Capture);
                lease = installation.Lease(selected.Version);
                try
                {
                    var start = new ProcessStartInfo(Path.Combine(installation.Payload(selected.Version), "bell-tower.exe")) { UseShellExecute = false };
                    foreach (var argument in args)
                    { start.ArgumentList.Add(argument); }
                    child = Process.Start(start) ?? throw new IOException("Application could not start.");
                }
                catch { lease.Dispose(); throw; }
            }
            using (lease)
            using (child)
            {
                child.WaitForExit();
                return child.ExitCode;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine("bell-tower launcher: " + error.Message);
            return 1;
        }
    }
}

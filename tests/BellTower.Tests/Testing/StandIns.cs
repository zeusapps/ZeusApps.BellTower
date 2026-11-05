using System.ComponentModel;
using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Tests.Testing;

// Helpers for stand-in processes: copies of cmd.exe under a workspace, found by their exact image path.
internal static class StandIns
{
    public static List<ProcessIdentity> Find(string image)
    {
        var result = new List<ProcessIdentity>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(image)))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(process.MainModule?.FileName, image, StringComparison.OrdinalIgnoreCase) &&
                        ProcessProbe.Identify(process.Id) is { } identity)
                    {
                        result.Add(identity);
                    }
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                {
                    // Not inspectable: not ours.
                }
            }
        }
        return result;
    }

    // Kills every process started from an image under the root, including one whose parent already exited.
    public static void KillUnder(string root)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } path && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit();
                    }
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                {
                    // Not inspectable, or already gone.
                }
            }
        }
    }
}

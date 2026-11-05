using System.ComponentModel;
using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Hosts;

namespace BellTower.Tests.Testing;

// Helpers for stand-in processes: copies of cmd.exe under a workspace, found by their exact image path. The conventions live
// here: the cmd.exe quoting rule, the environment that must not leak into a stand-in and the wait deadline.
internal static class StandIns
{
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(25);

    // A copy of cmd.exe named like a provider (or any) image, in its own folder of the workspace.
    public static string CopyImage(TestWorkspace workspace, string provider, string folder = "image", string? imageName = null)
    {
        var directory = Directory.CreateDirectory(Path.Combine(workspace.Root, folder)).FullName;
        var image = Path.Combine(directory, imageName ?? provider + ".exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), image);
        return image;
    }

    public static string Hook(string entry, string provider, TestWorkspace workspace) => entry == NativeAgentResolveProcessTests.Ingress
        ? $"\"{BuiltApplication.Ingress}\" --provider {provider} --data-dir \"{workspace.Paths.Root}\""
        : $"\"{BuiltApplication.Executable}\" event --provider {provider} --data-dir \"{workspace.Paths.Root}\"";

    // cmd.exe strips the outer quotes of a /s /c command, so the whole command is wrapped in one more pair.
    public static string Under(string arguments, string command) => $"{arguments} /s /c \"{command}\"";

    // Removes the Bell Tower and terminal variables a stand-in must not inherit from the test host.
    public static void ScrubEnvironment(ProcessStartInfo start)
    {
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("BELLTOWER_", StringComparison.Ordinal) || key is "WT_SESSION" or "TERM_PROGRAM").ToArray())
        {
            start.Environment.Remove(key);
        }
    }

    // Waits for a flag file (or for the optional process to exit) within the deadline; true when the flag exists.
    public static async Task<bool> WaitFor(string path, Process? process = null)
    {
        var deadline = DateTime.UtcNow + Deadline;
        while (!File.Exists(path) && (process is null || !process.HasExited) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
        return File.Exists(path);
    }

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

using System.Runtime.InteropServices;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

// Only the dedicated runtime-host process uses this. Closing inherited pipe
// handles prevents it from keeping prepare-codex-runtime's JSON pipeline open.
public static class CodexRuntimeConsole
{
    public static bool Join(ProcessIdentity shell)
    {
        foreach (var identifier in new[] { -10, -11, -12 })
        {
            var handle = GetStdHandle(identifier);
            if (handle != IntPtr.Zero && handle != new IntPtr(-1) && GetFileType(handle) == 3)
            {
                CloseHandle(handle);
            }
        }
        FreeConsole();
        return ProcessProbe.Identify(shell.Pid) == shell && AttachConsole((uint)shell.Pid);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int identifier);
    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);
}

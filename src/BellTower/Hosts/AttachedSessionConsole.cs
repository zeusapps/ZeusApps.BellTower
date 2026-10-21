using System.Runtime.InteropServices;
using System.Text;
using BellTower.Core;
using BellTower.IO;
using Microsoft.Win32.SafeHandles;

namespace BellTower.Hosts;

// Console attachment is process-wide. Use this only in a disposable helper process.
public sealed class AttachedSessionConsole : ISessionConsole
{
    private AttachedSessionConsole() { }

    public static ISessionConsole? Open(ProcessIdentity shell, ProcessIdentity agent)
    {
        FreeConsole();
        if (ProcessProbe.Identify(shell.Pid) != shell || ProcessProbe.Identify(agent.Pid) != agent || !AttachConsole((uint)shell.Pid))
        {
            return null;
        }
        var processes = new uint[256];
        var count = GetConsoleProcessList(processes, (uint)processes.Length);
        if (count == 0 || count > processes.Length || !processes.Contains((uint)shell.Pid) ||
            !processes.Contains((uint)agent.Pid) || ProcessProbe.Identify(shell.Pid) != shell || ProcessProbe.Identify(agent.Pid) != agent)
        {
            FreeConsole();
            return null;
        }
        return new AttachedSessionConsole();
    }

    public string ReadTitle()
    {
        var title = new StringBuilder(32768);
        GetConsoleTitle(title, (uint)title.Capacity);
        return title.ToString();
    }

    public bool SetTitle(string title) => SetConsoleTitle(title);

    public bool Write(string text)
    {
        using var output = CreateFile("CONOUT$", 0xc0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (output.IsInvalid || !GetConsoleMode(output, out var mode) || !SetConsoleMode(output, mode | 4))
        {
            return false;
        }
        try
        {
            return WriteConsole(output, text, (uint)text.Length, out var written, IntPtr.Zero) && written == text.Length;
        }
        finally
        {
            SetConsoleMode(output, mode);
        }
    }

    public void Dispose() => FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList([Out] uint[] processes, uint count);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetConsoleTitle(StringBuilder title, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetConsoleTitle(string title);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(SafeFileHandle handle, out uint mode);
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(SafeFileHandle handle, uint mode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool WriteConsole(SafeFileHandle handle, string text, uint count, out uint written, IntPtr reserved);
}

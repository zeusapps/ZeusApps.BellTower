using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BellTower.Core;

namespace BellTower.IO;

public static class ProcessProbe
{
    public static ProcessLiveness Inspect(ProcessIdentity identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.Pid);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == identity.StartedUtcTicks
                ? ProcessLiveness.Live : ProcessLiveness.Ended;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return ProcessLiveness.Ended;
        }
        catch (Win32Exception)
        {
            return ProcessLiveness.Unverified;
        }
    }

    public static ProcessIdentity? Identify(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return new(pid, process.StartTime.ToUniversalTime().Ticks);
        }
        catch (Exception e) when (e is ArgumentException or Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    public static bool IsAlive(ProcessIdentity identity)
    {
        try
        {
            using var p = Process.GetProcessById(identity.Pid);
            return !p.HasExited && p.StartTime.ToUniversalTime().Ticks == identity.StartedUtcTicks;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return true;
        }
    }

    public static List<(int Pid, string Name)> Ancestors(int pid)
    {
        var result = new List<(int, string)>();
        var parents = new Dictionary<int, (int Parent, string Name)>();
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1))
        {
            return result;
        }

        try
        {
            var entry = new ProcessEntry
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry>()
            };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    parents[(int)entry.ProcessId] = ((int)entry.ParentProcessId, entry.ExeFile);
                }
                while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        var seen = new HashSet<int>();
        while (parents.TryGetValue(pid, out var entry) && seen.Add(pid) && result.Count < 32)
        {
            pid = entry.Parent;
            if (parents.TryGetValue(pid, out var p))
            {
                result.Add((pid, p.Name));
            }
        }

        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr Heap;
        public uint Module;
        public uint Threads;
        public uint ParentProcessId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32First(IntPtr handle, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32Next(IntPtr handle, ref ProcessEntry entry);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}

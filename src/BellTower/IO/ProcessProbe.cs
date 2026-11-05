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

    /// <summary>
    /// Reads one process's command line through a single bounded query, or null when it is not readable.
    /// Callers keep only a classification, never the text.
    /// </summary>
    public static string? CommandLine(int pid)
    {
        const int ProcessCommandLine = 60;
        var handle = OpenProcess(0x1000, false, pid);
        if (handle == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            var size = 2048;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    var status = NtQueryInformationProcess(handle, ProcessCommandLine, buffer, (uint)size, out var needed);
                    if (status == 0)
                    {
                        var length = (ushort)Marshal.ReadInt16(buffer);
                        var text = Marshal.ReadIntPtr(buffer, IntPtr.Size);
                        return text == IntPtr.Zero ? null : Marshal.PtrToStringUni(text, length / 2);
                    }
                    if (needed <= size || needed > 65536)
                    {
                        return null;
                    }
                    size = (int)needed;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            return null;
        }
        finally
        {
            CloseHandle(handle);
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

    /// <summary>The ancestors of a process from a fresh snapshot. A caller that also needs relatives takes one <see cref="ProcessTable"/> instead.</summary>
    public static List<(int Pid, string Name)> Ancestors(int pid) => ProcessTable.Take().Ancestors(pid);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int information, IntPtr buffer, uint length, out uint needed);
}

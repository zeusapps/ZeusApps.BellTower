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

    /// <summary>
    /// Whether a process runs with an elevated token or at high integrity or above, read with limited query rights. Null when the
    /// process or its token cannot be read; a caller that must not cross a privilege boundary treats null as unreadable.
    /// </summary>
    public static bool? IsElevated(int pid)
    {
        const uint QueryLimited = 0x1000;
        const uint TokenQuery = 0x0008;
        const int TokenElevationClass = 20;
        const int TokenIntegrityLevelClass = 25;
        const uint HighIntegrity = 0x3000;
        var process = OpenProcess(QueryLimited, false, pid);
        if (process == IntPtr.Zero)
        {
            return null;
        }
        var token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(process, TokenQuery, out token))
            {
                return null;
            }
            var elevation = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                if (!GetTokenInformation(token, TokenElevationClass, elevation, sizeof(int), out _))
                {
                    return null;
                }
                if (Marshal.ReadInt32(elevation) != 0)
                {
                    return true;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(elevation);
            }
            GetTokenInformation(token, TokenIntegrityLevelClass, IntPtr.Zero, 0, out var needed);
            if (needed is 0 or > 1024)
            {
                return null;
            }
            var label = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!GetTokenInformation(token, TokenIntegrityLevelClass, label, needed, out _))
                {
                    return null;
                }
                var sid = Marshal.ReadIntPtr(label);
                var count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                return count > 0 && (uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1))) >= HighIntegrity;
            }
            finally
            {
                Marshal.FreeHGlobal(label);
            }
        }
        finally
        {
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
            CloseHandle(process);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int information, IntPtr buffer, uint length, out uint needed);
    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int information, IntPtr buffer, uint length, out uint needed);
}

using System.Globalization;
using System.Runtime.InteropServices;
using BellTower.IO;
using BellTower.Protocol;

namespace BellTower.Tray;

/// <summary>
/// The tray's own check of the job it was started in (design decision 10). A tray started by a hook that finds itself in a job
/// that ends it when the job closes (a provider's kill-on-close job) records why and exits instead of dying silently later.
/// Only a hook-started tray checks: one started by hand, a launch or the scheduled task is never refused. The check reads only
/// the immediate job; a kill-on-close job further up cannot be seen from here.
/// </summary>
public static class TrayJobGuard
{
    public const uint KillOnJobClose = 0x00002000;
    public const string LogCode = "tray-start-refused-kill-on-close-job";
    private const int JobObjectExtendedLimitInformation = 9;

    public static string RecordPath(string root) => HookTrayStart.JobRefusalRecord(root);

    /// <summary>True when a hook started this tray (the marker variable is set). The variable is removed so nothing the tray launches inherits it.</summary>
    public static bool TakeMarker()
    {
        var marked = Environment.GetEnvironmentVariable(HookTrayStart.HookStartVariable) == "1";
        Environment.SetEnvironmentVariable(HookTrayStart.HookStartVariable, null);
        return marked;
    }

    /// <summary>The limit flags of this process's immediate job, or null when it is in no job or they cannot be read.</summary>
    public static uint? ImmediateJobFlags()
    {
        // JOBOBJECT_EXTENDED_LIMIT_INFORMATION: the basic limits come first, the flags are at offset 16.
        var buffer = Marshal.AllocHGlobal(144);
        try
        {
            return QueryInformationJobObject(IntPtr.Zero, JobObjectExtendedLimitInformation, buffer, 144, out _) ? (uint)Marshal.ReadInt32(buffer, 16) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>True when the tray must not run: it was started by a hook and its immediate job kills it on close. The refusal is recorded for doctor and the log.</summary>
    public static bool Refuses(DataPaths paths, bool hookStarted, Func<uint?>? flags = null)
    {
        if (!hookStarted || ((flags ?? ImmediateJobFlags)() ?? 0) is var value && (value & KillOnJobClose) == 0)
        {
            return false;
        }
        try
        {
            Directory.CreateDirectory(HookTrayStart.StateDirectory(paths.Root));
            File.WriteAllText(RecordPath(paths.Root), DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\tkill-on-close-job");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        new LocalLog(paths).Write(LogCode);
        return true;
    }

    public static (DateTimeOffset At, string Reason)? LastRefusal(string root)
    {
        var parts = HookTrayStart.ReadRecord(RecordPath(root))?.Split('\t');
        return parts is { Length: 2 } && HookTrayStart.JobRefusalAt(root) is { } at ? (at, parts[1]) : null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(IntPtr job, int informationClass, IntPtr information, int length, out int returned);
}

using System.Globalization;
using System.Runtime.InteropServices;
using BellTower.IO;

namespace BellTower.Protocol;

/// <summary>
/// The on-demand per-user scheduled task that is the fallback tray start (design decision 10). It has no trigger, so it never
/// runs on its own: the hook only asks the Task Scheduler to run it when the shell start failed. Setup creates and removes the
/// task and records its path here; the hook runs only a recorded task. The task and the record share one name computation.
/// </summary>
public static class HookStartTask
{
    public const string DefaultFolder = "ZeusApps.BellTower";
    /// <summary>The run was accepted by the Task Scheduler (<c>schtasks /run</c> exited 0 within the bound).</summary>
    public const string RouteTask = "task-run-requested";
    /// <summary><c>schtasks /run</c> had not exited within the bound; the hook did not wait for it. Not a failure.</summary>
    public const string RoutePending = "task-run-pending";
    // The hook never waits for the tray, and the request is not the tray: this only lets a fast refusal be recorded as one.
    private static readonly TimeSpan RunWait = TimeSpan.FromMilliseconds(250);

    /// <summary>The record that setup wrote after it created the task; absent when no task exists for this data directory.</summary>
    public static string RecordPath(string root) => Path.Combine(HookTrayStart.StateDirectory(root), "task.txt");

    private static string TaskName(string root) =>
        "HookStart-" + ContentHash.Compute(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)).ToUpperInvariant() + "hook-start-task")[..16];

    /// <summary>The task path for a data directory: unique per data directory, under a folder (a test uses its own folder).</summary>
    public static string TaskPath(string root, string folder = DefaultFolder) => "\\" + folder + "\\" + TaskName(root);

    /// <summary>
    /// The exact shape of a task Bell Tower creates for this data directory: one folder of letters, digits, dot, dash and underscore, then
    /// this data directory's own task name. Anything else is not a record Bell Tower wrote and is never put on a command line.
    /// </summary>
    public static bool IsBellTowerTask(string root, string path) =>
        path.Length is > 0 and <= 128 && path.StartsWith('\\') && path.LastIndexOf('\\') is > 1 and var slash &&
        path[1..slash].All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_') && path[1..slash].Length <= 64 &&
        string.Equals(path[(slash + 1)..], TaskName(root), StringComparison.Ordinal);

    /// <summary>The recorded task path when it has the exact Bell Tower shape for this data directory; null for no record or an invalid one.</summary>
    public static string? Recorded(string root) => HookTrayStart.ReadRecord(RecordPath(root))?.Trim() is { } path && IsBellTowerTask(root, path) ? path : null;

    /// <summary>For doctor and removal: none, a valid record, or a record that is not a Bell Tower task path.</summary>
    public static string RecordState(string root) => !File.Exists(RecordPath(root)) ? "none" : Recorded(root) is not null ? "valid" : "invalid";

    public static void Record(string root, string taskPath)
    {
        Directory.CreateDirectory(HookTrayStart.StateDirectory(root));
        File.WriteAllText(RecordPath(root), taskPath);
    }

    public static void ClearRecord(string root)
    {
        try
        {
            File.Delete(RecordPath(root));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Asks the Task Scheduler to run the recorded task, then returns: the tray is the task's action and nothing here waits for it. A
    /// refusal that arrives within a short bound is a failure; an exit that has not arrived is recorded as pending, not as a failure.
    /// An invalid or missing record never starts <c>schtasks</c>.
    /// </summary>
    public static (HookStartOutcome Outcome, string Detail) Run(string root) => Run(root, null);

    internal static (HookStartOutcome Outcome, string Detail) Run(string root, string? schtasks)
    {
        if (Recorded(root) is not { } task)
        {
            return (HookStartOutcome.Failed, "no-task-recorded");
        }
        var executable = schtasks ?? Path.Combine(Environment.SystemDirectory, "schtasks.exe");
        var command = Marshal.StringToHGlobalUni("\"" + executable + "\" /run /tn \"" + task + "\"");
        var info = new StartupInfo { Size = (uint)Marshal.SizeOf<StartupInfo>() };
        try
        {
            if (!CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, false, CreateNoWindow, IntPtr.Zero, Environment.SystemDirectory, ref info, out var process))
            {
                return (HookStartOutcome.Failed, "task-run-create:" + Marshal.GetLastPInvokeError());
            }
            try
            {
                if (WaitForSingleObject(process.Process, (uint)RunWait.TotalMilliseconds) != 0)
                {
                    return (HookStartOutcome.Started, RoutePending);
                }
                return GetExitCodeProcess(process.Process, out var code) && code == 0
                    ? (HookStartOutcome.Started, RouteTask)
                    : (HookStartOutcome.Failed, "task-run-exit:" + code.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                CloseHandle(process.Process);
                CloseHandle(process.Thread);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(command);
        }
    }

    private const uint CreateNoWindow = 0x08000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public uint Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, Reserved2Size;
        public IntPtr Reserved2, StandardInput, StandardOutput, StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public uint ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string applicationName, IntPtr commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory,
        ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

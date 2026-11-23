using System.Diagnostics;
using System.Runtime.InteropServices;
using BellTower.Protocol;

// A stand-in for the stable launcher. It never starts a tray: it records how it was started (arguments, working directory,
// environment variable names, the type of each standard handle), holds the tray-instance mutex like a tray would, and
// stays alive until the test writes the release file, so a test can show that it outlives the hook that started it.
namespace BellTower.Tests.FakeLauncher;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedJobLimitInformation
    {
        public JobBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr job, int informationClass, ref ExtendedJobLimitInformation information, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll")]
    private static extern uint GetFileType(IntPtr handle);

    private static int Main(string[] args)
    {
        if (args.Length == 6 && args[0] == "job-host")
        {
            return RunJobHost(args);
        }
        if (args.Length >= 1 && args[0] == "/run")
        {
            return SchtasksStandIn(args);
        }
        // Exactly the arguments every payload has always accepted: a payload that rejects unknown options still starts (ANALYSIS-4702).
        var valid = args is ["tray", "--data-dir", _];
        if (!valid)
        {
            return 2;
        }
        var root = args[2];
        var folder = Path.Combine(root, "fake-launcher");
        Directory.CreateDirectory(folder);
        var lines = new List<string>
        {
            "args=" + string.Join("|", args),
            "cwd=" + Environment.CurrentDirectory,
            "pid=" + Environment.ProcessId,
            "stdin=" + GetFileType(GetStdHandle(-10)),
            "stdout=" + GetFileType(GetStdHandle(-11)),
            "stderr=" + GetFileType(GetStdHandle(-12))
        };
        lines.AddRange(Environment.GetEnvironmentVariables().Keys.Cast<string>().OrderBy(key => key, StringComparer.OrdinalIgnoreCase).Select(key => "env=" + key));
        using var tray = new Mutex(true, HookTrayStart.MutexName(root, HookTrayStart.TrayArea), out var created);
        lines.Add("mutex-created=" + created);
        var record = Path.Combine(folder, Environment.ProcessId + ".txt");
        File.WriteAllLines(record + ".tmp", lines);
        File.Move(record + ".tmp", record);
        var release = Path.Combine(folder, "release");
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (!File.Exists(release) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }
        return 0;
    }

    // Stands in for schtasks.exe /run: records the call and, per the mode file next to it, exits at once (ok, fail) or only after a while (slow).
    private static int SchtasksStandIn(string[] args)
    {
        var folder = AppContext.BaseDirectory;
        File.AppendAllText(Path.Combine(folder, "schtasks-calls.txt"), string.Join("|", args) + Environment.NewLine);
        var mode = File.Exists(Path.Combine(folder, "schtasks-mode.txt")) ? File.ReadAllText(Path.Combine(folder, "schtasks-mode.txt")).Trim() : "ok";
        if (mode == "slow")
        {
            Thread.Sleep(1500);
        }
        return mode == "fail" ? 1 : 0;
    }

    private static int RunJobHost(string[] args)
    {
        var outerJob = CreateJobObjectW(IntPtr.Zero, null);
        if (outerJob == IntPtr.Zero)
        {
            return Marshal.GetLastPInvokeError();
        }
        var innerJob = IntPtr.Zero;
        try
        {
            if (!ConfigureAndAssignJob(outerJob, 0x00002000))
            {
                return Marshal.GetLastPInvokeError();
            }
            if (args[1] == "nested")
            {
                innerJob = CreateJobObjectW(IntPtr.Zero, null);
                if (innerJob == IntPtr.Zero || !ConfigureAndAssignJob(innerJob, 0x00000800))
                {
                    return Marshal.GetLastPInvokeError();
                }
            }

            var start = new ProcessStartInfo(args[2]) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--provider");
            start.ArgumentList.Add(args[3]);
            start.ArgumentList.Add("--data-dir");
            start.ArgumentList.Add(args[4]);
            using var process = Process.Start(start);
            if (process is null)
            {
                return 1;
            }
            process.StandardInput.Write(File.ReadAllText(args[5]));
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(20000))
            {
                process.Kill();
                return 2;
            }
            Console.Write(output);
            Console.Error.Write(error);
            return process.ExitCode;
        }
        finally
        {
            // The inner job (when present) permits breakaway; the outer kill-on-close job does not.
            if (innerJob != IntPtr.Zero)
            {
                CloseHandle(innerJob);
            }
            CloseHandle(outerJob);
        }
    }

    private static bool ConfigureAndAssignJob(IntPtr job, uint limitFlags)
    {
        var limits = new ExtendedJobLimitInformation
        {
            BasicLimitInformation = new JobBasicLimitInformation { LimitFlags = limitFlags }
        };
        return SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedJobLimitInformation>()) &&
            AssignProcessToJobObject(job, GetCurrentProcess());
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

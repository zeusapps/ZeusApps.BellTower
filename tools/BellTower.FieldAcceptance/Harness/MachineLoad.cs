using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The machine load around a measurement, without naming any process: the share of processor time that was not idle over a short
/// window, the number of logical processors and the number of running processes. Timing numbers are machine-specific, and this is
/// what lets a reader judge whether the machine was busy or idle while they were taken.
/// </summary>
public sealed record MachineLoadSample(DateTimeOffset At, double CpuBusyPercent, int LogicalProcessors, int Processes, TimeSpan Window)
{
    public string Describe() => CpuBusyPercent.ToString("F0") + "% of " + LogicalProcessors + " logical processors busy over " + Window.TotalSeconds.ToString("F0") + " s, " + Processes + " processes running";
}

public static class MachineLoad
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    public static MachineLoadSample Sample(TimeSpan window)
    {
        GetSystemTimes(out var idle1, out var kernel1, out var user1);
        Thread.Sleep(window);
        GetSystemTimes(out var idle2, out var kernel2, out var user2);
        // Kernel time includes idle time.
        var total = (kernel2 - kernel1) + (user2 - user1);
        var busy = total <= 0 ? 0 : 100d * (1 - (double)(idle2 - idle1) / total);
        var processes = Process.GetProcesses();
        var count = processes.Length;
        foreach (var process in processes)
        {
            process.Dispose();
        }
        return new MachineLoadSample(DateTimeOffset.UtcNow, Math.Clamp(busy, 0, 100), Environment.ProcessorCount, count, window);
    }

    /// <summary>"idle" when every sample was below 20% busy, otherwise "busy".</summary>
    public static string Classify(params MachineLoadSample[] samples) => samples.Max(sample => sample.CpuBusyPercent) < 20 ? "idle" : "busy";
}

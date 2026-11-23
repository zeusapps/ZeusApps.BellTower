using System.Diagnostics;
using System.Text;
using BellTower.Protocol;

namespace BellTower.Setup;

/// <summary>
/// Creates, removes and reports the on-demand per-user scheduled task that is the fallback tray start (design decision 10). The
/// task has no trigger and the limited interactive principal, never ends by time, ignores battery state and starts one instance
/// at most. It is not a sign-in entry: nothing runs it except a hook whose shell start failed.
/// </summary>
public static class HookStartTaskSetup
{
    private static string PowerShell => Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    private const char Separator = (char)92;

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>The script that registers the task for a launcher and data directory. Exposed so setup preview and tests show the exact definition.</summary>
    public static string RegisterScript(string taskPath, string launcher, string root)
    {
        var slash = taskPath.LastIndexOf(Separator);
        var folder = taskPath[..(slash + 1)];
        var name = taskPath[(slash + 1)..];
        var arguments = "tray --data-dir \"" + root + "\"";
        return string.Join("\n",
            "$ErrorActionPreference = 'Stop'",
            "$action = New-ScheduledTaskAction -Execute " + Quote(launcher) + " -Argument " + Quote(arguments) + " -WorkingDirectory " + Quote(Path.GetDirectoryName(launcher)!),
            "$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType Interactive -RunLevel Limited",
            "$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew",
            "Register-ScheduledTask -TaskName " + Quote(name) + " -TaskPath " + Quote(folder) +
            " -Action $action -Principal $principal -Settings $settings -Description 'Starts the Bell Tower tray on demand for a hook. It has no trigger and never runs by itself.' -Force | Out-Null");
    }

    /// <summary>
    /// Removes the task and, when it is then empty, its folder. A task that is not there counts as removed; any other failure stops the script
    /// with a non-zero exit, so the caller keeps its record and ownership and the removal can be retried.
    /// </summary>
    public static string UnregisterScript(string taskPath)
    {
        var slash = taskPath.LastIndexOf(Separator);
        var folder = taskPath[..(slash + 1)];
        var name = taskPath[(slash + 1)..];
        return string.Join("\n",
            "$ErrorActionPreference = 'Stop'",
            // Only a genuine "not found" counts as already removed; any other lookup failure stops the script with a non-zero exit, so the
            // action fails and the record and ownership are kept for a retry.
            "$task = $null",
            "try { $task = Get-ScheduledTask -TaskName " + Quote(name) + " -TaskPath " + Quote(folder) + " -ErrorAction Stop }",
            "catch { if ($_.CategoryInfo.Category -ne 'ObjectNotFound') { throw } }",
            "if ($task) { Unregister-ScheduledTask -TaskName " + Quote(name) + " -TaskPath " + Quote(folder) + " -Confirm:$false -ErrorAction Stop }",
            "if (-not (Get-ScheduledTask -TaskPath " + Quote(folder) + " -ErrorAction SilentlyContinue)) {",
            "  try { $service = New-Object -ComObject 'Schedule.Service'; $service.Connect(); $service.GetFolder('\\').DeleteFolder(" + Quote(folder.Trim(Separator)) + ", 0) } catch { }",
            "}",
            // Every failure above stopped the script (ErrorActionPreference Stop); reaching here means the task is gone, whatever the folder step left in $?.
            "exit 0");
    }

    /// <summary>
    /// The removal as a setup action with its own failure wording. The record that lets doctor and a retry find the task is deleted only
    /// after the task is gone.
    /// </summary>
    public static SetupAction RemoveAction(string taskPath, string root, string? executable = null) =>
        new("Remove the on-demand tray start task " + taskPath, executable ?? PowerShell, Arguments(UnregisterScript(taskPath)))
        {
            Failure = "The on-demand tray start task could not be removed. Its record and ownership were kept; rerun setup --remove.",
            NodeEnvironment = false,
            OnSuccess = () => HookStartTask.ClearRecord(root)
        };

    private static string[] Arguments(string script) =>
        ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script))];

    /// <summary>Registers the task and records it for the hook. Returns false (and records nothing) when it cannot be created; the shell start still works.</summary>
    public static bool Register(string root, string launcher, string folder = HookStartTask.DefaultFolder)
    {
        var task = HookStartTask.TaskPath(root, folder);
        if (!Run(RegisterScript(task, launcher, root)))
        {
            return false;
        }
        HookStartTask.Record(root, task);
        return true;
    }

    public static bool Unregister(string root, string folder = HookStartTask.DefaultFolder)
    {
        var done = Run(UnregisterScript(HookStartTask.TaskPath(root, folder)));
        if (done)
        {
            HookStartTask.ClearRecord(root);
        }
        return done;
    }

    /// <summary>True when the Task Scheduler knows the recorded task. Used by doctor and tests; the hook never calls it.</summary>
    public static bool Exists(string taskPath)
    {
        try
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("/query");
            start.ArgumentList.Add("/tn");
            start.ArgumentList.Add(taskPath);
            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            return process.WaitForExit(10000) && process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool Run(string script)
    {
        try
        {
            var start = new ProcessStartInfo(PowerShell) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in Arguments(script))
            {
                start.ArgumentList.Add(argument);
            }
            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000))
            {
                process.Kill();
                return false;
            }
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}

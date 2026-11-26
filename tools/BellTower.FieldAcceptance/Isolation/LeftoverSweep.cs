using System.Diagnostics;
using System.Xml.Linq;

namespace BellTower.FieldAcceptance.Isolation;

/// <summary>
/// The preflight sweep: removes only what an earlier, interrupted harness run can have left behind. A task is the harness's only when
/// its action runs <c>tray --data-dir</c> for a data directory under <c>artifacts\field-acceptance\</c>; a process is the harness's only
/// when its image lies under <c>artifacts\field-acceptance\runs\</c>. The user's own production task and tray never match either rule.
/// Run it while no other harness run is active: a concurrent run's tasks and processes match the same rules.
/// </summary>
public static class LeftoverSweep
{
    private const string TaskFolderPrefix = @"\ZeusApps.BellTower\HookStart-";

    /// <summary>True when the exported task definition starts <c>tray --data-dir "&lt;dir&gt;"</c> with a directory under <paramref name="harnessBase"/>.</summary>
    public static bool IsHarnessTask(string taskXml, string harnessBase)
    {
        try
        {
            var document = XDocument.Parse(taskXml);
            foreach (var exec in document.Descendants().Where(element => element.Name.LocalName == "Exec"))
            {
                var arguments = exec.Elements().FirstOrDefault(element => element.Name.LocalName == "Arguments")?.Value.Trim();
                if (arguments is null || !arguments.StartsWith("tray --data-dir", StringComparison.Ordinal))
                {
                    continue;
                }
                var directory = arguments["tray --data-dir".Length..].Trim().Trim('"');
                if (directory.Length > 0 && Path.IsPathRooted(directory) && directory.IndexOfAny(Path.GetInvalidPathChars()) < 0 && PathRelation.IsUnderOrEqual(directory, harnessBase))
                {
                    return true;
                }
            }
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException or NotSupportedException)
        {
            // Not a definition this harness wrote.
        }
        return false;
    }

    /// <summary>True when a process image lies under the harness run directories.</summary>
    public static bool IsHarnessImage(string image, string runsBase) => image.Length > 0 && PathRelation.IsUnderOrEqual(image, runsBase);

    /// <summary>
    /// Removes the harness-owned leftover tasks and processes and returns one line per removal. <paramref name="runSchtasks"/> runs
    /// <c>schtasks</c> with the given arguments; <paramref name="harnessBase"/> is <c>artifacts\field-acceptance</c>.
    /// </summary>
    public static IReadOnlyList<string> Run(string harnessBase, Func<string[], (int ExitCode, string Output)> runSchtasks, int excludePid)
    {
        var removed = new List<string>();
        var runs = Path.Combine(harnessBase, "runs");
        foreach (var process in KillProcessesUnder(runs, excludePid))
        {
            removed.Add("process " + process);
        }
        var (listExit, listing) = runSchtasks(["/query", "/fo", "csv", "/nh"]);
        if (listExit == 0)
        {
            foreach (var name in listing.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim().Trim('"').Split("\",\"")[0])
                         .Where(name => name.StartsWith(TaskFolderPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                var (xmlExit, xml) = runSchtasks(["/query", "/tn", name, "/xml"]);
                if (xmlExit == 0 && IsHarnessTask(xml, harnessBase))
                {
                    var (deleteExit, _) = runSchtasks(["/delete", "/tn", name, "/f"]);
                    removed.Add("scheduled task " + name + (deleteExit == 0 ? " (deleted)" : " (delete failed, exit " + deleteExit + ")"));
                }
            }
        }
        return removed;
    }

    /// <summary>Ends every process whose image is under <paramref name="root"/> (never the given pid) and describes each one.</summary>
    public static IReadOnlyList<string> KillProcessesUnder(string root, int excludePid)
    {
        var killed = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != excludePid && process.MainModule?.FileName is { } image && PathRelation.IsUnderOrEqual(image, root))
                    {
                        process.Kill(true);
                        killed.Add(Path.GetFileName(image) + " pid " + process.Id + " (killed)");
                    }
                }
                catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // The process ended or is not ours to inspect.
                }
            }
        }
        return killed;
    }
}

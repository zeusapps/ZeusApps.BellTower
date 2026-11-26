using System.Diagnostics;
using System.Security.Cryptography;

namespace BellTower.FieldAcceptance.Isolation;

public sealed record FileFingerprint(string Path, bool Exists, long Length, long LastWriteTicks, string? Sha256);

public sealed record ProcessFingerprint(int Pid, long StartTicks, string Image);

public sealed record SnapshotChange(string Kind, string Subject, string Detail, bool Gating);

/// <summary>
/// Metadata of the production locations before and after a run. Gating entries are files only setup, an update or a user action
/// changes (provider and VS Code settings, the setup manifest, hook-start records, the whole installation tree, the user's scheduled
/// tray-start tasks, the running production Bell Tower processes). Directory totals are informational: the user's live tray and agents
/// write there while a run is in progress.
/// </summary>
public sealed record ProductionSnapshot(
    IReadOnlyList<FileFingerprint> Gated, IReadOnlyList<string> Tasks, IReadOnlyList<ProcessFingerprint> Processes,
    IReadOnlyDictionary<string, (int Files, long Bytes)> DirectoryTotals)
{
    private const long HashLimit = 4 * 1024 * 1024;
    private const int TotalsEntryCap = 200_000;

    public static ProductionSnapshot Take(ProductionLocations production, Func<IEnumerable<string>> tasks, Func<IEnumerable<ProcessFingerprint>> processes)
    {
        var gated = new List<FileFingerprint>();
        foreach (var file in production.WatchedFiles)
        {
            gated.Add(Fingerprint(file, hash: true));
        }
        if (Directory.Exists(production.InstallationRoot))
        {
            foreach (var file in EnumerateFiles(production.InstallationRoot).Order(StringComparer.OrdinalIgnoreCase))
            {
                gated.Add(Fingerprint(file, hash: false));
            }
        }
        var totals = new Dictionary<string, (int, long)>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in production.Roots.Where(Directory.Exists))
        {
            var files = 0;
            long bytes = 0;
            foreach (var file in EnumerateFiles(root).Take(TotalsEntryCap))
            {
                files++;
                bytes += SafeLength(file);
            }
            totals[root] = (files, bytes);
        }
        return new(gated, tasks().Order(StringComparer.OrdinalIgnoreCase).ToList(), processes().OrderBy(process => process.Pid).ToList(), totals);
    }

    /// <summary>Differences that matter. <paramref name="ownTasks"/> are the sandbox's own scheduled tasks, which are expected to come and go.</summary>
    public static IReadOnlyList<SnapshotChange> Diff(ProductionSnapshot before, ProductionSnapshot after, IReadOnlySet<string> ownTasks)
    {
        var changes = new List<SnapshotChange>();
        var left = before.Gated.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        var right = after.Gated.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var path in left.Keys.Union(right.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            left.TryGetValue(path, out var was);
            right.TryGetValue(path, out var now);
            if (was is null || now is null)
            {
                changes.Add(new("file", path, was is null ? "appeared" : "disappeared", true));
            }
            else if (was != now)
            {
                changes.Add(new("file", path, was.Exists != now.Exists ? (now.Exists ? "created" : "removed") : "length, timestamp or hash changed", true));
            }
        }
        var tasksBefore = before.Tasks.Except(ownTasks, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tasksAfter = after.Tasks.Except(ownTasks, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var task in tasksBefore.Except(tasksAfter, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new("scheduled-task", task, "removed", true));
        }
        foreach (var task in tasksAfter.Except(tasksBefore, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new("scheduled-task", task, "added", true));
        }
        foreach (var task in after.Tasks.Intersect(ownTasks, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new("scheduled-task", task, "a sandbox task remains registered", true));
        }
        foreach (var process in before.Processes.Where(process => !after.Processes.Contains(process)))
        {
            changes.Add(new("process", process.Image + " pid " + process.Pid, "a production Bell Tower process was not running after the run", true));
        }
        // The other direction: a production process that appears during the run is the most likely visible sign of a leak (a hook or a start
        // that resolved to the user's launcher). Compared by pid plus start time, so a reused pid is still a new process.
        foreach (var process in after.Processes.Where(process => !before.Processes.Contains(process)))
        {
            changes.Add(new("process", process.Image + " pid " + process.Pid, "a production Bell Tower process started during the run", true));
        }
        foreach (var (root, was) in before.DirectoryTotals)
        {
            if (after.DirectoryTotals.TryGetValue(root, out var now) && now != was)
            {
                changes.Add(new("directory-total", root, $"files {was.Files}->{now.Files}, bytes {was.Bytes}->{now.Bytes} (informational: live tray and agents write here)", false));
            }
        }
        return changes;
    }

    public static FileFingerprint Fingerprint(string path, bool hash)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return new(path, false, 0, 0, null);
            }
            return new(path, true, info.Length, info.LastWriteTimeUtc.Ticks, hash && info.Length <= HashLimit ? Hex(path) : null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(path, false, -1, 0, "unreadable");
        }
    }

    private static string Hex(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static long SafeLength(string path)
    {
        try
        { return new FileInfo(path).Length; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return 0; }
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        return Directory.EnumerateFiles(root, "*", options);
    }

    /// <summary>The scheduled tasks under the Bell Tower task folder, by name only (their run state legitimately changes).</summary>
    public static IEnumerable<string> BellTowerTasks(Func<string[], (int ExitCode, string Output)> runSchtasks)
    {
        var (exit, output) = runSchtasks(["/query", "/fo", "csv", "/nh"]);
        if (exit != 0)
        {
            return [];
        }
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Trim('"').Split("\",\"")[0])
            .Where(name => name.StartsWith(@"\ZeusApps.BellTower\", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Running processes whose image lies inside the production installation (the user's tray and launcher).</summary>
    public static IEnumerable<ProcessFingerprint> ProductionProcesses(ProductionLocations production)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string? image = null;
                long start = 0;
                try
                {
                    image = process.MainModule?.FileName;
                    start = process.StartTime.ToUniversalTime().Ticks;
                }
                catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    continue;
                }
                if (image is not null && PathRelation.IsUnderOrEqual(image, production.InstallationRoot))
                {
                    yield return new(process.Id, start, System.IO.Path.GetFileName(image));
                }
            }
        }
    }
}

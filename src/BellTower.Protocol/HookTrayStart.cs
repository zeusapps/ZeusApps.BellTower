using System.Globalization;
using System.Text;
using BellTower.IO;

namespace BellTower.Protocol;

public enum HookStartOutcome
{
    TrayRunning,
    UserStopped,
    UpdatePending,
    NoLauncher,
    Throttled,
    Started,
    Failed
}

/// <summary>
/// Starts a stopped tray from the hook ingress (design decision 9). Every step is bounded and none waits for the tray:
/// the mutex open is the only cost while a tray runs, and a failure leaves the capture queued and the hook unchanged.
/// The same code is linked into the tray (records, user stop) and the diagnostics, so the names and records cannot diverge.
/// </summary>
public static class HookTrayStart
{
    public const string TrayArea = "tray-instance";
    public const string LauncherName = "bell-tower.exe";
    /// <summary>
    /// The marker a hook puts in the environment of the tray it starts, so the tray checks its own job. It is an environment variable
    /// and not an argument: a tray from before this contract ignores it, so a rollback or switch to an older payload still starts. The
    /// tray removes it from its own environment at once, so nothing it launches inherits it.
    /// </summary>
    public const string HookStartVariable = "BELLTOWER_HOOK_START";

    public static string JobRefusalRecord(string root) => Path.Combine(StateDirectory(root), "tray-job.txt");

    /// <summary>The time a hook-started tray refused its job, or null. Cleared when a tray takes the instance mutex.</summary>
    public static DateTimeOffset? JobRefusalAt(string root) => ReadRecord(JobRefusalRecord(root))?.Split('\t') is { Length: 2 } parts &&
        DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;

    public static void ClearJobRefusal(string root)
    {
        try
        {
            File.Delete(JobRefusalRecord(root));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { }
    }
    // Long enough that a slow tray start (it consumes the queue only after it shows) is not retried by every hook of a busy
    // session, short enough that a start that failed is retried within a minute of the next hook.
    public static readonly TimeSpan AttemptInterval = TimeSpan.FromSeconds(30);
    // The update marker is honored for as long as the update transaction itself honors it (UpdateTransaction.RestartPending).
    private static readonly TimeSpan UpdateWindow = TimeSpan.FromMinutes(10);
    private const int RecordLimit = 2048;
    private static readonly TimeSpan RefusalBinding = TimeSpan.FromMinutes(10);

    public static string StateDirectory(string root) => Path.Combine(root, "hook-start");
    public static string LauncherRecord(string root) => Path.Combine(StateDirectory(root), "launcher.txt");
    public static string UserStopRecord(string root) => Path.Combine(StateDirectory(root), "user-stop.txt");
    public static string LastAttemptRecord(string root) => Path.Combine(StateDirectory(root), "last-attempt.txt");
    private static string AttemptRecord(string root, long bucket) => Path.Combine(StateDirectory(root), "attempt-" + bucket.ToString(CultureInfo.InvariantCulture) + ".lock");

    /// <summary>The one named-object computation for a data directory, shared by the tray, the CLI and this ingress.</summary>
    public static string MutexName(string root, string area) =>
        "Local\\BellTower-" + ContentHash.Compute(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)).ToUpperInvariant() + area)[..24];

    public static bool TrayRunning(string root)
    {
        try
        {
            if (!Mutex.TryOpenExisting(MutexName(root, TrayArea), out var mutex))
            {
                return false;
            }
            mutex.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true; // The object exists but belongs to someone else: starting another tray cannot help.
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    /// <summary>Null when a desktop shell can be verified now (for doctor); otherwise the bounded reason. Opens and releases the shell handle.</summary>
    public static string? ShellProblem()
    {
        var shell = DesktopShell.Open();
        shell.Close();
        return shell.Problem;
    }

    public static HookStartOutcome TryStart(string root, DateTimeOffset? at = null) => TryStart(root, at, null, null);

    /// <summary>The routes are injectable only so a test can prove the fallback is chosen solely when the shell start fails.</summary>
    internal static HookStartOutcome TryStart(string root, DateTimeOffset? at, Func<string, string, (HookStartOutcome Outcome, string Detail)>? shellStart,
        Func<string, (HookStartOutcome Outcome, string Detail)>? taskStart)
    {
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (TrayRunning(root))
            {
                return HookStartOutcome.TrayRunning;
            }
            if (UserStopActive(root))
            {
                return HookStartOutcome.UserStopped;
            }
            if (UpdatePending(root))
            {
                return HookStartOutcome.UpdatePending;
            }
            // No valid record: nothing is searched for and nothing starts. The launcher is validated before an attempt is claimed.
            if (ValidLauncher(root, out _) is not { } launcher)
            {
                return HookStartOutcome.NoLauncher;
            }
            var now = at ?? DateTimeOffset.UtcNow;
            if (!TryClaimAttempt(root, now))
            {
                return HookStartOutcome.Throttled;
            }
            // A tray started by an earlier hook that refused its job (after the last attempt) is not retried through the shell: the next
            // start goes to the recorded task, or fails with a stated reason, so a false trigger cannot keep the tray down.
            // The refusal binds until a tray takes the instance (which clears it) or, for a stale record nobody cleared, for ten minutes.
            var refused = JobRefusalAt(root) is { } refusedAt && (LastAttempt(root) is not { } previousAttempt || refusedAt >= previousAttempt.At || now - refusedAt < RefusalBinding);
            var (outcome, detail) = refused ? (HookStartOutcome.Failed, "shell-route-refused") : (shellStart ?? TrayProcess.Start)(launcher, root);
            // Only a failed shell start falls back, and only to the task setup recorded; a started tray never takes a second route.
            if (outcome == HookStartOutcome.Failed && HookStartTask.Recorded(root) is not null)
            {
                var (taskOutcome, taskDetail) = (taskStart ?? HookStartTask.Run)(root);
                (outcome, detail) = taskOutcome == HookStartOutcome.Started ? (taskOutcome, taskDetail) : (HookStartOutcome.Failed, detail + ";" + taskDetail);
            }
            try
            {
                WriteRecord(LastAttemptRecord(root), now.ToString("O", CultureInfo.InvariantCulture) + "\t" + outcome + "\t" + detail);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return outcome;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return HookStartOutcome.Failed;
        }
    }

    // ---- Launcher record -------------------------------------------------------------------------------------------

    /// <summary>Records the stable launcher when it passes the same checks the ingress applies. A path that fails them is not recorded.</summary>
    public static void RecordLauncher(string root, string launcher)
    {
        try
        {
            if (LauncherProblem(launcher) is not null)
            {
                return;
            }
            var record = LauncherRecord(root);
            if (ReadRecord(record) != launcher)
            {
                WriteRecord(record, launcher);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }

    /// <summary>The recorded launcher, or null with the reason it cannot be used.</summary>
    public static string? ValidLauncher(string root, out string reason)
    {
        var recorded = ReadRecord(LauncherRecord(root))?.Trim();
        if (string.IsNullOrEmpty(recorded))
        {
            reason = "no launcher recorded";
            return null;
        }
        reason = LauncherProblem(recorded) ?? "";
        return reason.Length == 0 ? recorded : null;
    }

    /// <summary>Null when the path is an absolute local path to an existing regular file named bell-tower.exe with no reparse point on the way.</summary>
    public static string? LauncherProblem(string path)
    {
        try
        {
            if (path.Length is 0 or > 1024 || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
                !string.Equals(Path.GetFullPath(path), path, StringComparison.Ordinal))
            {
                return "recorded path is not a plain absolute local path";
            }
            if (!string.Equals(Path.GetFileName(path), LauncherName, StringComparison.OrdinalIgnoreCase))
            {
                return "recorded file is not " + LauncherName;
            }
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
            {
                return "recorded path is not a regular file";
            }
            for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return "recorded path crosses a reparse point";
                }
            }
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return "recorded launcher does not exist or cannot be read";
        }
    }

    // ---- User stop ---------------------------------------------------------------------------------------------------

    /// <summary>The identity of the current Windows sign-in: the token's logon session id plus that session's logon time, so a reused id after a restart does not match.</summary>
    public static string? SignInToken() => TrayProcess.SignInToken();

    public static void RecordUserStop(string root)
    {
        try
        {
            if (SignInToken() is { } token)
            {
                WriteRecord(UserStopRecord(root), token);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }

    public static void ClearUserStop(string root)
    {
        try
        {
            File.Delete(UserStopRecord(root));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }

    /// <summary>True only while the record belongs to this sign-in; a record from another sign-in or boot is stale and ignored.</summary>
    public static bool UserStopActive(string root) => ReadRecord(UserStopRecord(root)) is { } recorded && SignInToken() is { } token &&
        string.Equals(recorded.Trim(), token, StringComparison.Ordinal);

    // ---- Update or switch transaction ---------------------------------------------------------------------------------

    /// <summary>A cheap reading of the update marker. The tray's own start gate stays authoritative.</summary>
    public static bool UpdatePending(string root)
    {
        try
        {
            var marker = new FileInfo(Path.Combine(root, "updates", "restart.json"));
            return marker.Exists && DateTime.UtcNow - marker.LastWriteTimeUtc < UpdateWindow;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // ---- Burst control -------------------------------------------------------------------------------------------------

    /// <summary>
    /// At most one attempt per fixed interval: the winner of an exclusive create of the interval's record. A hook also yields
    /// when the previous interval's record is younger than the interval, so a burst across a boundary does not start twice.
    /// The tray-instance mutex still guarantees one tray.
    /// </summary>
    public static bool TryClaimAttempt(string root, DateTimeOffset now)
    {
        try
        {
            Directory.CreateDirectory(StateDirectory(root));
            var bucket = now.UtcTicks / AttemptInterval.Ticks;
            if (ReadRecord(AttemptRecord(root, bucket - 1)) is { } earlier && long.TryParse(earlier, NumberStyles.None, CultureInfo.InvariantCulture, out var at) &&
                now.UtcTicks - at < AttemptInterval.Ticks)
            {
                return false;
            }
            try
            {
                using var stream = new FileStream(AttemptRecord(root, bucket), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(Encoding.ASCII.GetBytes(now.UtcTicks.ToString(CultureInfo.InvariantCulture)));
            }
            catch (IOException)
            {
                return false;
            }
            Prune(root, bucket);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static void Prune(string root, long bucket)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(StateDirectory(root), "attempt-*.lock").Take(64))
            {
                var name = Path.GetFileNameWithoutExtension(file)["attempt-".Length..];
                if (long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number < bucket - 1)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    // ---- Diagnostics ----------------------------------------------------------------------------------------------------

    public static (DateTimeOffset At, string Outcome, string Detail)? LastAttempt(string root)
    {
        var parts = ReadRecord(LastAttemptRecord(root))?.Split('\t');
        return parts is { Length: 3 } && DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? (at, parts[1], parts[2]) : null;
    }

    public static string? ReadRecord(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= RecordLimit ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static void WriteRecord(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

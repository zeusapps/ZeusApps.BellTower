using System.Text.RegularExpressions;

namespace BellTower.IO;

/// <summary>The persisted record of a failing tray refresh: written on transitions only, read by doctor.</summary>
public sealed record RefreshHealthRecord(DateTimeOffset? FailingSince, string? Reason, DateTimeOffset? LastSuccess);

/// <summary>
/// Reports tray refresh failures once per reason change or interval, never once per tick, and records the failure window for doctor.
/// Shared by both providers: the refresh is the shared state drain.
/// </summary>
public sealed partial class RefreshHealth(DataPaths paths, TimeProvider? time = null)
{
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromMinutes(10);
    public const int MaxReasonLength = 200;

    /// <summary>How long a failure window lasts before it is recorded for doctor even when its log line is suppressed.</summary>
    public static readonly TimeSpan RecordAfter = TimeSpan.FromSeconds(30);

    // Distinct reasons remembered for the rate limit; alternating reasons cannot grow it or bypass it.
    private const int MaxRememberedReasons = 8;
    private const int MaxMessageLength = 2000;

    // Total budget about 100 ms: five rules of at most 20 ms each. A rule that runs out reduces the reason to the exception type.
    private const int SanitizerMilliseconds = 20;
    private static readonly TimeSpan SanitizerTimeout = TimeSpan.FromMilliseconds(SanitizerMilliseconds);

    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private readonly Dictionary<string, DateTimeOffset> logged = new(StringComparer.Ordinal);
    private DateTimeOffset? failingSince;
    private DateTimeOffset? lastSuccess;
    private string? persistedReason;
    private DateTimeOffset? lastPersisted;
    private DateTimeOffset? lastSuppressedLogged;
    private bool recordChecked;
    private bool windowLogged;
    private bool recordShowsFailure;

    public static RefreshHealthRecord? Read(DataPaths paths)
    {
        try
        {
            return File.Exists(paths.RefreshHealth) ? AtomicFile.Read<RefreshHealthRecord>(paths.RefreshHealth) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            return null;
        }
    }

    public void Failed(Exception exception)
    {
        var now = clock.GetUtcNow();
        var text = Describe(exception, paths.Root);
        var started = failingSince is null;
        if (started)
        {
            // Carry the last success known from before this process, once.
            lastSuccess ??= Read(paths)?.LastSuccess;
            failingSince = now;
            windowLogged = false;
        }
        recordChecked = true;
        // Each reason is limited on its own: a new reason logs at once, a known one at most once per interval.
        var isLogged = false;
        if (!(logged.TryGetValue(text, out var last) && now - last < RepeatInterval))
        {
            if (Remember(text, now))
            {
                new LocalLog(paths).Write("tray-refresh-failed", text);
                windowLogged = isLogged = true;
            }
            else if (lastSuppressedLogged is null || now - lastSuppressedLogged >= RepeatInterval)
            {
                // The map holds the maximum of fresh reasons: a further one waits, and one line per interval says so.
                lastSuppressedLogged = now;
                new LocalLog(paths).Write("tray-refresh-failed", "further reasons suppressed");
            }
        }
        // The record is written on a transition only: for a logged failure when it shows none yet or the reason differs from the
        // recorded one (at most once per interval), and for any window that has lasted RecordAfter even when its line is suppressed,
        // so doctor sees a persistent failure. A recovery does not reset the limiter, so a flap cannot write per tick.
        var persistent = !recordShowsFailure && now - failingSince >= RecordAfter;
        if (persistent || isLogged && (!recordShowsFailure || text != persistedReason && now - lastPersisted >= RepeatInterval))
        {
            persistedReason = text;
            lastPersisted = now;
            recordShowsFailure = true;
            Persist(new(failingSince, text, lastSuccess));
        }
    }

    public void Succeeded()
    {
        var now = clock.GetUtcNow();
        var wasFailing = failingSince is not null;
        if (!wasFailing && !recordChecked)
        {
            // A failure recorded by an earlier run is recovered by this first success.
            recordChecked = true;
            wasFailing = Read(paths)?.FailingSince is not null;
            // A recorded failure is treated as a logged one. A window recorded after RecordAfter whose own line was suppressed gets one
            // extra recovery line after a restart: bounded (one per restart) and harmless, so no logged-window state is persisted.
            recordShowsFailure = windowLogged = wasFailing;
        }
        lastSuccess = now;
        if (!wasFailing)
        {
            return;
        }
        failingSince = null;
        // The per-reason limiter survives a recovery: failures that flap stay within the bound of a continuous failure.
        if (windowLogged)
        {
            new LocalLog(paths).Write("tray-refresh-recovered");
        }
        windowLogged = false;
        if (recordShowsFailure)
        {
            // Doctor must never show an ended failure as persistent.
            recordShowsFailure = false;
            persistedReason = null;
            lastPersisted = null;
            Persist(new(null, null, now));
        }
    }

    // Only an entry older than the repeat interval is evicted, so the log is bounded to the map size plus one notice per interval
    // whatever the order or number of reasons. Returns false when the reason is new and the map holds only fresh entries.
    private bool Remember(string text, DateTimeOffset now)
    {
        if (!logged.ContainsKey(text) && logged.Count >= MaxRememberedReasons)
        {
            foreach (var key in logged.Where(pair => now - pair.Value >= RepeatInterval).Select(pair => pair.Key).ToArray())
            {
                logged.Remove(key);
            }
            if (logged.Count >= MaxRememberedReasons)
            {
                return false;
            }
        }
        logged[text] = now;
        return true;
    }

    /// <summary>
    /// The exception type and a bounded message with paths replaced: the data directory, the user-profile directory, drive, UNC and Unix
    /// paths (a segment may contain spaces and apostrophes), the current user name as a whole token, and any single-quoted segment that
    /// starts at a path root, which is masked through the last closing quote on its line (text between two quoted paths may go too).
    /// </summary>
    /// <param name="profile">The user-profile directory to mask; the current user's by default.</param>
    /// <param name="userName">The user name to mask; the current user's by default.</param>
    public static string Describe(Exception exception, string dataRoot, string? profile = null, string? userName = null)
    {
        try
        {
            return DescribeCore(exception, dataRoot, profile, userName);
        }
        catch (RegexMatchTimeoutException)
        {
            // The message could not be masked within the budget, so none of it is kept.
            return exception.GetType().Name;
        }
    }

    private static string DescribeCore(Exception exception, string dataRoot, string? profile, string? userName)
    {
        var message = exception.Message ?? "";
        if (message.Length > MaxMessageLength)
        {
            message = message[..MaxMessageLength];
        }
        if (dataRoot.Length > 0)
        {
            message = message.Replace(dataRoot, "<data>", StringComparison.OrdinalIgnoreCase)
                .Replace(dataRoot.Replace('\\', '/'), "<data>", StringComparison.OrdinalIgnoreCase);
        }
        message = DataTail().Replace(message, "<data>");
        // The profile prefix becomes a private-use root marker, so the profile counts as a path root for the quoted and unquoted rules below.
        profile ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var parts = profile.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            var root = profile.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\" : profile.StartsWith('/') ? "/" : "";
            var pattern = root.Replace(@"\", @"\\") + string.Join(@"[\\/]+", parts.Select(Regex.Escape)) + @"(?![\w.-])";
            message = Regex.Replace(message, pattern, RootMarker, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, SanitizerTimeout);
        }
        // The exact profile and user name go before the path rules: a name with an apostrophe would otherwise end a segment early.
        // Both are private-use markers until the end so the path rules below still see one unbroken segment.
        userName ??= Environment.UserName;
        if (userName.Length > 0)
        {
            message = Regex.Replace(message, @"(?<![\p{L}\p{N}_])" + Regex.Escape(userName) + @"(?![\p{L}\p{N}_])", UserMarker,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, SanitizerTimeout);
        }
        message = Quoted().Replace(message, match => match.Value.StartsWith("'<data>", StringComparison.Ordinal) ? "'<data>'" : "'<path>'");
        message = PathPattern().Replace(message, "<path>").Replace(UserMarker, "<user>").Replace(RootMarker, "<path>");
        message = new string(message.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        var text = exception.GetType().Name + ": " + message;
        return text.Length <= MaxReasonLength ? text : text[..MaxReasonLength];
    }

    // Private-use markers standing for the current user name and the profile prefix while the path rules run.
    private const string UserMarker = "\uE000";
    private const string RootMarker = "\uE001";

    private void Persist(RefreshHealthRecord record)
    {
        try
        {
            Directory.CreateDirectory(paths.Root);
            AtomicFile.Write(paths.RefreshHealth, Json.Encode(record));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    // What follows the replaced data directory, such as a file name beneath it.
    [GeneratedRegex("""<data>[\\/][^\s'"<>|:]*""", RegexOptions.None, SanitizerMilliseconds)]
    private static partial Regex DataTail();

    // .NET IO exceptions quote the path: 'C:\Users\A B\x'. A quoted segment that starts at a path root (drive, UNC, Unix root, the data
    // directory or the profile marker) is masked through the LAST closing quote on its line: an apostrophe not followed by a letter or digit
    // (so whatever punctuation follows, or the end). Apostrophes inside the path never end it. Text between two quoted paths on one line may be masked too:
    // over-masking loses only detail, a partial match would leak names.
    [GeneratedRegex("""(?<![\p{L}\p{N}])'(?:<data>|\uE001|[A-Za-z]:[\\/]|\\\\|/)[^\r\n]*'(?![\p{L}\p{N}])""", RegexOptions.None, SanitizerMilliseconds)]
    private static partial Regex Quoted();

    // Drive, UNC, Unix and profile-marker paths. A segment before a separator may hold spaces and any apostrophe, since a separator still follows;
    // the last one ends at whitespace.
    [GeneratedRegex("""(?:[A-Za-z]:[\\/]|\\\\)(?:[^\\/"<>|\r\n]*[\\/])*(?:'(?=[\p{L}\p{N}])|[^\s'"<>|\\/])*|(?<![\w<>/\\.:-])(?:/(?:root|home|Users|tmp|var|mnt|opt|etc|usr|srv|media|private|Volumes|proc|run|dev|snap|Library)(?![\w.-])|/[^\s'"<>|/]+(?=/))(?:/(?:[^/"<>|\r\n]*/)*(?:'(?=[\p{L}\p{N}])|[^\s'"<>|/])*)?|\uE001(?:[\\/](?:[^\\/"<>|\r\n]*[\\/])*(?:'(?=[\p{L}\p{N}])|[^\s'"<>|\\/])*)?""", RegexOptions.None, SanitizerMilliseconds)]
    private static partial Regex PathPattern();
}

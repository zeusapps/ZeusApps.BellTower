using System.Globalization;
using System.Text.RegularExpressions;

namespace BellTower.Distribution;

/// <summary>One display string for the running or selected version, shared by every surface that reports it.</summary>
public static class VersionDisplay
{
    /// <summary>Stable: the plain version. Dev: version, "dev", commit (and "dirty"), build time. Dev range without metadata: "dev (unmarked)".</summary>
    public static string Format(string version, ReleaseBuild? build)
    {
        if (!ReleaseContract.IsDevVersion(version))
        { return version; }
        if (build is null)
        { return version + " dev (unmarked)"; }
        var time = build.BuiltAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        return $"{version} dev {build.Commit}{(build.Dirty ? " dirty" : "")} ({time})";
    }
    /// <summary>Fallback for an executable without a payload contract: reads the informational version, for example 0.1.9001-dev+b74a12b.dirty.</summary>
    public static string FromInformational(string informational)
    {
        var plus = informational.IndexOf('+');
        var head = plus < 0 ? informational : informational[..plus];
        var version = head.Split('-')[0];
        if (!Regex.IsMatch(version, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") || !ReleaseContract.IsDevVersion(version))
        { return version; }
        var marked = head.EndsWith("-dev", StringComparison.Ordinal);
        var metadata = plus < 0 ? "" : informational[(plus + 1)..];
        var parts = metadata.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (!marked || parts.Length == 0 || !parts[0].All(Uri.IsHexDigit))
        { return version + " dev (unmarked)"; }
        return $"{version} dev {parts[0]}{(parts.Contains("dirty") ? " dirty" : "")}";
    }
    /// <summary>True when the display text names a dev build (any dev-range version).</summary>
    public static bool IsDev(string display) => display.Contains(" dev", StringComparison.Ordinal);
}

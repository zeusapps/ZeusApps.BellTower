using System.Text.RegularExpressions;
using BellTower.Distribution;
using BellTower.IO;

namespace BellTower.Diagnostics;

/// <summary>
/// Read-only doctor view of whether the owned provider hooks call the ingress image the selected installation provides. The owned hook
/// commands present in the provider settings (the manifest only identifies which are ours), whose path carries the image's content hash (<c>provider-ingress/1/&lt;hash&gt;/bell-tower-ingress.exe</c>); an update or
/// development switch selects a payload with another image but leaves those commands alone until setup is rerun. Nothing is written.
/// </summary>
public sealed class HookIngressImageReporter(DataPaths paths, Func<string?>? selectedHash = null, string? claudeConfigDirectory = null, string? codexHome = null)
{
    public const string Current = "current";
    public const string Stale = "stale";
    public const string Unknown = "unknown";
    public const string StaleAdvice = "rerun setup --host-bridge --apply; Codex may ask to trust the changed hooks";

    public sealed record ProviderImage(string Status, string? RecordedImage, string? Advice);

    public object Report() => new { Claude = Provider("claude"), Codex = Provider("codex") };

    public ProviderImage Provider(string provider)
    {
        var commands = new HookDeliveryReporter(paths, claudeConfigDirectory, codexHome).LiveOwnedCommands(provider);
        var (status, recorded) = Evaluate(commands, (selectedHash ?? SelectedHash)());
        return new(status, recorded, status == Stale ? StaleAdvice : null);
    }

    /// <summary>Current when every live owned ingress command names the selected image, stale when one names another (never the manifest history, which keeps superseded commands), unknown when nothing can be compared (never reported as stale).</summary>
    public static (string Status, string? Recorded) Evaluate(IEnumerable<string> commands, string? selected)
    {
        var hashes = commands.Select(RecordedHash).Where(hash => hash is not null).Select(hash => hash!).Distinct(StringComparer.Ordinal).ToList();
        if (hashes.Count == 0 || selected is null || !HashPattern.IsMatch(selected))
        {
            return (Unknown, null);
        }
        var differing = hashes.FirstOrDefault(hash => !hash.Equals(selected, StringComparison.OrdinalIgnoreCase));
        return differing is null ? (Current, hashes[0][..12]) : (Stale, differing[..12]);
    }

    /// <summary>The image hash in an owned ingress command, for the bash literal, PowerShell and cmd forms; null when the command is not parseable.</summary>
    public static string? RecordedHash(string command)
    {
        var match = CommandPattern.Match(command);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    private static string? SelectedHash()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return null;
            }
            var directory = Path.GetDirectoryName(Path.GetFullPath(processPath))!;
            var installation = File.Exists(Path.Combine(directory, "installation.json")) ? new Installation(directory) : Installation.ForPayload(processPath);
            return installation?.Contract(installation.Selection().Version).Files?.GetValueOrDefault("bell-tower-ingress.exe");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static readonly Regex CommandPattern = new(@"[/\\]provider-ingress[/\\]1[/\\]([0-9a-fA-F]{64})[/\\]bell-tower-ingress\.exe", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    private static readonly Regex HashPattern = new("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
}

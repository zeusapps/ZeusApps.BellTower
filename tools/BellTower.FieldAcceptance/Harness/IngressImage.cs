using System.Text.RegularExpressions;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The content-addressed ingress image an owned hook command names (<c>provider-ingress/1/&lt;sha256&gt;/bell-tower-ingress.exe</c> under the data
/// directory). An update selects a payload with another image but leaves the commands alone until setup is rerun, so a scenario compares the
/// hash a command names with the hash a payload ships.
/// </summary>
public static partial class IngressImage
{
    [GeneratedRegex(@"provider-ingress[/\\]1[/\\]([0-9a-fA-F]{64})[/\\]bell-tower-ingress\.exe", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static string? HashOf(string? command) => command is not null && Pattern().Match(command) is { Success: true } match ? match.Groups[1].Value.ToLowerInvariant() : null;

    public static string Short(string? hash) => hash is { Length: >= 12 } ? hash[..12] : hash ?? "none";
}

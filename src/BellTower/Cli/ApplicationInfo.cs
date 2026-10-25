using System.Reflection;
using BellTower.Distribution;

namespace BellTower.Cli;

internal static class ApplicationInfo
{
    private const int NotifyIconTextLimit = 127;
    private static readonly string Informational = typeof(ApplicationInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    /// <summary>Plain MAJOR.MINOR.PATCH, also for dev builds (their informational version carries a "-dev+commit" suffix).</summary>
    public static string Version { get; } = Informational.Split('+')[0].Split('-')[0];
    /// <summary>Version text for people: the plain version for stable builds, or version, "dev", commit and build time for dev builds.</summary>
    public static string Display { get; } = ResolveDisplay();
    public static string TrayText => VersionDisplay.IsDev(Display) ? Truncate("Bell Tower " + Display, NotifyIconTextLimit) : "Bell Tower";
    private static string Truncate(string text, int limit) => text.Length <= limit ? text : text[..limit];
    private static string ResolveDisplay()
    {
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "", "release.json");
            if (File.Exists(path) && DistributionJson.Read<ReleaseContract>(path) is { } contract && contract.Version == Version)
            { return VersionDisplay.Format(contract.Version, contract.Build); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException) { }
        return VersionDisplay.FromInformational(Informational);
    }
}

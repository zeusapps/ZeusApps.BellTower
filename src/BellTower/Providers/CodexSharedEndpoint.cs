using System.Text;

namespace BellTower.Providers;

/// <summary>
/// Locates the local control socket of a shared Codex backend from a resolved Codex home and nothing else. The path is derived,
/// never enumerated and never accepted from a hook or tool payload (native evidence 009 and 010).
/// </summary>
public static class CodexSharedEndpoint
{
    public const string ControlDirectory = "app-server-control";
    public const string SocketName = "app-server-control.sock";

    // Windows sockaddr_un.sun_path holds 108 bytes including the terminator.
    public const int MaxPathBytes = 107;

    /// <summary>
    /// The one normalized spelling of a Codex home, used as the key for backoff and replacement and as the base of the socket path:
    /// absolute, fully resolved and without a trailing separator, compared case-insensitively on Windows.
    /// </summary>
    public static string HomeKey(string codexHome)
    {
        if (string.IsNullOrWhiteSpace(codexHome) || !Path.IsPathFullyQualified(codexHome))
        {
            throw new CodexTransportException(CodexTransportFailure.EndpointMissing, "A resolved absolute Codex home is required.");
        }
        var home = Normalize(codexHome);
        return OperatingSystem.IsWindows() ? home.ToUpperInvariant() : home;
    }

    /// <summary>The Codex home Bell Tower resolves for itself: <c>CODEX_HOME</c> when set, otherwise <c>.codex</c> in the user profile.</summary>
    public static string DefaultHome() =>
        Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } configured
            ? configured : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

    public static string SocketPath(string codexHome)
    {
        _ = HomeKey(codexHome);
        var path = Path.Combine(Normalize(codexHome), ControlDirectory, SocketName);
        if (Encoding.UTF8.GetByteCount(path) > MaxPathBytes)
        {
            throw new CodexTransportException(CodexTransportFailure.PathTooLong,
                "The control socket path exceeds the local socket path limit, so the shared backend cannot be reached.");
        }
        return path;
    }

    public static bool SameHome(string? reported, string expected)
    {
        if (string.IsNullOrWhiteSpace(reported))
        {
            return false;
        }
        var text = reported.StartsWith(@"\\?\", StringComparison.Ordinal) ? reported[4..] : reported;
        return Path.IsPathFullyQualified(text) && string.Equals(Normalize(text), Normalize(expected), PathComparison);
    }

    private static string Normalize(string value) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

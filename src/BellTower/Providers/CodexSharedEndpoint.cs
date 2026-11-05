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

    public static string SocketPath(string codexHome)
    {
        if (string.IsNullOrWhiteSpace(codexHome) || !Path.IsPathFullyQualified(codexHome))
        {
            throw new CodexTransportException(CodexTransportFailure.EndpointMissing, "A resolved absolute Codex home is required.");
        }
        var home = Path.TrimEndingDirectorySeparator(Path.GetFullPath(codexHome));
        var path = Path.Combine(home, ControlDirectory, SocketName);
        if (!Path.GetFullPath(path).StartsWith(home + Path.DirectorySeparatorChar, PathComparison))
        {
            throw new CodexTransportException(CodexTransportFailure.HomeMismatch, "The control socket is not under the Codex home.");
        }
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

using BellTower.Providers;

namespace BellTower.Sessions;

/// <summary>Honest, content-free status text for a failed Codex runtime read or write. It names the reason and never carries a response.</summary>
public static class CodexAccessStatus
{
    public const string NoRuntime = "unavailable: no verified dedicated runtime";
    public const string ConnectionFailed = "unavailable: native metadata connection failed";

    public const string Mismatch = "unavailable: runtime thread does not match the conversation";
    public const string Unverified = "unavailable: fork or delegated thread mapping unverified";

    /// <summary>
    /// The status for a failure, or null when the failure is only a backoff in progress (nothing new was attempted, so the
    /// previous status stays).
    /// </summary>
    public static string? For(CodexAccess access, Exception exception)
    {
        if (exception is not CodexTransportException transport)
        {
            return ConnectionFailed;
        }
        if (transport.Failure == CodexTransportFailure.BackedOff)
        {
            return null;
        }
        if (transport.Failure == CodexTransportFailure.ThreadUnmapped)
        {
            return Mismatch;
        }
        if (transport.Failure == CodexTransportFailure.ThreadUnverified)
        {
            return Unverified;
        }
        if (access is not CodexAccess.Shared)
        {
            return ConnectionFailed;
        }
        return "unavailable: shared backend " + transport.Failure switch
        {
            CodexTransportFailure.EndpointMissing => "is not running",
            CodexTransportFailure.PathTooLong => "socket path is too long",
            CodexTransportFailure.Refused => "refused the connection",
            CodexTransportFailure.Timeout => "did not answer in time",
            CodexTransportFailure.Malformed => "sent an unexpected response",
            CodexTransportFailure.HomeMismatch => "uses another Codex home",
            CodexTransportFailure.BackendNotVerified => "is not verified",
            CodexTransportFailure.PeerUnsupported => "process lookup is unsupported",
            CodexTransportFailure.PeerUnreadable => "process cannot be read",
            CodexTransportFailure.ImageMismatch => "is not a codex process",
            CodexTransportFailure.CommandLineMismatch => "is not a shared backend",
            CodexTransportFailure.ElevatedPeer => "runs elevated",
            CodexTransportFailure.EndpointReplaced => "was replaced",
            CodexTransportFailure.Rejected => "rejected the request",
            _ => "is unavailable"
        };
    }
}

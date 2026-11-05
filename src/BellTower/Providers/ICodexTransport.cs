using System.Text.Json;

namespace BellTower.Providers;

/// <summary>
/// Message transport beneath the Codex name and attention clients. Implementations differ in how they reach a runtime and
/// who owns it (an owned authenticated loopback runtime, or a non-owning local shared backend); the JSON-RPC layer is shared.
/// Disposing a transport closes only Bell Tower's own connection and never stops or reconfigures the runtime.
/// </summary>
public interface ICodexTransport : IDisposable
{
    /// <summary>Per-phase time limits applied on top of the caller's token, or null when the caller's token is the only bound.</summary>
    CodexTransportBudget? Budget => null;

    /// <summary>True when transport failures must be reported as <see cref="CodexTransportException"/> with a diagnosable reason.</summary>
    bool Strict => false;

    Task ConnectAsync(CancellationToken cancellation);

    /// <summary>Checks the initialize result before the connection is used. Throws when the runtime is not the one expected.</summary>
    void VerifyInitialized(JsonElement initializeResult)
    {
    }

    /// <summary>Re-checks before a request that the verified runtime is still the one serving this connection. Throws when it ended.</summary>
    void EnsureCurrent()
    {
    }

    /// <summary>
    /// True when the runtime is not owned by Bell Tower, so the returned thread must carry exactly the requested id (the proof,
    /// on a peer-verified backend). Refusing a fork or delegated thread is a conservative policy, not a proof; see
    /// <see cref="CodexThreadIdentity"/>.
    /// </summary>
    bool RequiresProvenThreads => false;

    Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellation);

    /// <summary>Receives one complete text message of at most <see cref="CodexLimits.MessageBytes"/>.</summary>
    Task<byte[]> ReceiveAsync(CancellationToken cancellation);
}

public static class CodexLimits
{
    public const int MessageBytes = 1024 * 1024;

    /// <summary>Unrelated notifications skipped while waiting for one response before the exchange is abandoned.</summary>
    public const int SkippedMessages = 1000;
}

public sealed record CodexTransportBudget(TimeSpan Connect, TimeSpan Handshake, TimeSpan Read)
{
    public static CodexTransportBudget Default { get; } = new(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

    /// <summary>
    /// The shared backend's open budgets: together clearly shorter than the services' 3 s operation timeout, so a stalled backend
    /// ends the open as a recorded <see cref="CodexTransportFailure.Timeout"/> before the caller's own token can fire.
    /// </summary>
    public static CodexTransportBudget Shared { get; } = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3));
}

public enum CodexTransportFailure
{
    EndpointMissing,
    PathTooLong,
    Refused,
    Timeout,
    Malformed,
    HomeMismatch,
    /// <summary>The process serving the socket is not live, or is not the expected one.</summary>
    BackendNotVerified,
    /// <summary>The platform cannot report the process serving a local socket.</summary>
    PeerUnsupported,
    /// <summary>The serving process is known but cannot be read (identity, command line or token).</summary>
    PeerUnreadable,
    /// <summary>The serving process is not a codex image.</summary>
    ImageMismatch,
    /// <summary>The serving process is a codex image whose command line is not a shared backend.</summary>
    CommandLineMismatch,
    /// <summary>The shared backend runs elevated; Bell Tower observes only a non-elevated backend.</summary>
    ElevatedPeer,
    EndpointReplaced,
    BackedOff,
    /// <summary>The runtime answered the request with an error.</summary>
    Rejected,
    /// <summary>The runtime returned no thread, no identifier, or a different thread than the one requested (a true mismatch).</summary>
    ThreadUnmapped,
    /// <summary>A fork or delegated thread on an unowned runtime, refused as a conservative policy: the hook-to-thread mapping is unverified, not disproven.</summary>
    ThreadUnverified
}

/// <summary>A fail-closed transport refusal. The message names the reason only; it never carries response or credential content.</summary>
public sealed class CodexTransportException(CodexTransportFailure failure, string message, Exception? inner = null) : IOException(message, inner)
{
    public CodexTransportFailure Failure { get; } = failure;
}

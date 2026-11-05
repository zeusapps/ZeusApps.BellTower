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

    /// <summary>Re-checks that the endpoint is still the verified one. Throws when it was replaced or went away.</summary>
    void EnsureCurrent()
    {
    }

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
}

public enum CodexTransportFailure
{
    EndpointMissing,
    PathTooLong,
    Refused,
    Timeout,
    Malformed,
    HomeMismatch,
    BackendNotVerified,
    PeerUnavailable,
    EndpointReplaced,
    BackedOff
}

/// <summary>A fail-closed transport refusal. The message names the reason only; it never carries response or credential content.</summary>
public sealed class CodexTransportException(CodexTransportFailure failure, string message, Exception? inner = null) : IOException(message, inner)
{
    public CodexTransportFailure Failure { get; } = failure;
}

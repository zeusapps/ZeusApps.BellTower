using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BellTower.Cli;

namespace BellTower.Providers;

/// <summary>
/// JSON-RPC over a <see cref="ICodexTransport"/>. A connection is either connected by the owned-runtime path
/// (<see cref="ConnectAsync(string, string, CancellationToken)"/>) or built over a transport and connected with
/// <see cref="ConnectAsync(CancellationToken)"/>; the name and attention services receive an already connected connection from
/// the one runtime opener and never connect it themselves.
/// </summary>
public sealed class CodexConnection : ICodexNameClient, ICodexAttentionClient
{
    private ICodexTransport? transport;
    private int nextId;

    /// <summary>An owned authenticated-loopback connection; <see cref="ConnectAsync(string, string, CancellationToken)"/> supplies the endpoint.</summary>
    public CodexConnection()
    {
    }

    /// <summary>A connection over an already chosen transport, for example the non-owning shared-backend transport.</summary>
    public CodexConnection(ICodexTransport transport) => this.transport = transport;

    public async Task<string?> ReadNameAsync(string thread, CancellationToken cancellation) =>
        HookInput.Text(await ReadThreadAsync(thread, cancellation).ConfigureAwait(false), "name");

    public async Task SetNameAsync(string thread, string name, CancellationToken cancellation) =>
        _ = await CallAsync("thread/name/set", new { threadId = thread, name }, cancellation).ConfigureAwait(false);

    public async Task<bool?> ReadPermissionPendingAsync(string thread, CancellationToken cancellation) =>
        PermissionPending(await ReadThreadAsync(thread, cancellation).ConfigureAwait(false));

    public async Task<bool?> ReadUserInputPendingAsync(string thread, CancellationToken cancellation) =>
        UserInputPending(await ReadThreadAsync(thread, cancellation).ConfigureAwait(false));

    // Exact-conversation metadata only: never thread/resume, never a turn, and no turn history (includeTurns is false, so no
    // transcript content is requested). The caller extracts the few fields it needs; the response is not retained or logged.
    private async Task<JsonElement> ReadThreadAsync(string threadId, CancellationToken cancellation)
    {
        var active = transport ?? throw new InvalidOperationException("The Codex connection has no transport.");
        try
        {
            var result = await CallAsync("thread/read", new { threadId, includeTurns = false }, cancellation).ConfigureAwait(false);
            var thread = result.GetProperty("thread");
            CodexThreadIdentity.Require(thread, threadId, active.RequiresProvenThreads);
            return thread;
        }
        catch (Exception exception) when (active.Strict && Reason(exception, cancellation) is { } reasoned)
        {
            throw reasoned;
        }
    }

    public static bool? PermissionPending(JsonElement thread) => Pending(thread, "waitingOnApproval");

    public static bool? UserInputPending(JsonElement thread) => Pending(thread, "waitingOnUserInput");

    // Probe-verified at Codex 0.164.0: both flags come from the owning runtime's thread status.
    private static bool? Pending(JsonElement thread, string flag)
    {
        if (!thread.TryGetProperty("status", out var status))
        {
            return null;
        }
        switch (HookInput.Text(status, "type"))
        {
            case "idle":
            case "systemError":
                return false;
            case "active" when status.TryGetProperty("activeFlags", out var flags) && flags.ValueKind == JsonValueKind.Array:
                return flags.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == flag);
            default:
                return null;
        }
    }

    public async Task ConnectAsync(string endpoint, string token, CancellationToken cancellation)
    {
        if (transport is not null)
        {
            throw new InvalidOperationException("The Codex connection already has a transport.");
        }
        transport = new CodexLoopbackTransport(endpoint, token);
        await ConnectAsync(cancellation).ConfigureAwait(false);
    }

    /// <summary>Connects the transport given at construction, verifies it with the initialize response, then completes the handshake.</summary>
    public async Task ConnectAsync(CancellationToken cancellation)
    {
        var active = transport ?? throw new InvalidOperationException("The Codex connection has no transport.");
        var budget = active.Budget;
        try
        {
            using (var connect = Limited(budget?.Connect, cancellation))
            {
                await active.ConnectAsync(connect.Token).ConfigureAwait(false);
            }
            using var handshake = Limited(budget?.Handshake, cancellation);
            var result = await CallAsync("initialize", new { clientInfo = new { name = "bell_tower", version = ApplicationInfo.Version } }, handshake.Token).ConfigureAwait(false);
            active.VerifyInitialized(result);
            await active.SendAsync(Encoding.UTF8.GetBytes("{\"method\":\"initialized\",\"params\":{}}"), handshake.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (active.Strict && Reason(exception, cancellation) is { } reasoned)
        {
            throw reasoned;
        }
    }

    /// <summary>
    /// The one mapping from a raw transport, protocol or parse failure to a reasoned <see cref="CodexTransportException"/> for a
    /// strict transport, used by the handshake and by every request. The caller's own cancellation and a failure that already
    /// carries a reason pass through unchanged (null). Messages name the reason only; they never carry response content.
    /// </summary>
    private static CodexTransportException? Reason(Exception exception, CancellationToken caller) => exception switch
    {
        CodexTransportException => null,
        OperationCanceledException when caller.IsCancellationRequested => null,
        OperationCanceledException => new(CodexTransportFailure.Timeout, "The Codex runtime did not answer within its time budget.", exception),
        WebSocketException or IOException => new(CodexTransportFailure.Refused, "The Codex runtime refused or dropped the connection.", exception),
        JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException =>
            new(CodexTransportFailure.Malformed, "The Codex runtime sent a malformed response.", exception),
        _ => null
    };

    private static CancellationTokenSource Limited(TimeSpan? limit, CancellationToken cancellation)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        if (limit is { } value)
        {
            source.CancelAfter(value);
        }
        return source;
    }

    public async Task<JsonElement> CallAsync(string method, object parameters, CancellationToken cancellation)
    {
        var active = transport ?? throw new InvalidOperationException("The Codex connection has no transport.");
        try
        {
            return await ExchangeAsync(active, method, parameters, cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (active.Strict && Reason(exception, cancellation) is { } reasoned)
        {
            throw reasoned;
        }
    }

    private async Task<JsonElement> ExchangeAsync(ICodexTransport active, string method, object parameters, CancellationToken cancellation)
    {
        var id = Interlocked.Increment(ref nextId);
        var request = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, method, @params = parameters }));
        active.EnsureCurrent();
        using var read = Limited(active.Budget?.Read, cancellation);
        await active.SendAsync(request, read.Token).ConfigureAwait(false);
        for (var skipped = 0; skipped <= CodexLimits.SkippedMessages; skipped++)
        {
            var message = await active.ReceiveAsync(read.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(message);
            var data = document.RootElement;
            if (!data.TryGetProperty("id", out var responseId) || responseId.ValueKind != JsonValueKind.Number ||
                !responseId.TryGetInt32(out var number) || number != id)
            {
                continue;
            }
            if (data.TryGetProperty("error", out _))
            {
                throw active.Strict
                    ? new CodexTransportException(CodexTransportFailure.Rejected, "The Codex runtime rejected the request.")
                    : new IOException("Codex rejected the native metadata request.");
            }
            return data.GetProperty("result").Clone();
        }
        throw new InvalidDataException("Invalid Codex response.");
    }

    public void Dispose() => transport?.Dispose();
}

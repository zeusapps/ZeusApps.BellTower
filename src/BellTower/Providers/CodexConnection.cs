using System.Text;
using System.Text.Json;
using BellTower.Cli;

namespace BellTower.Providers;

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

    public async Task<string?> ReadNameAsync(string thread, CancellationToken cancellation)
    {
        var result = await CallAsync("thread/read", new { threadId = thread }, cancellation).ConfigureAwait(false);
        return HookInput.Text(result.GetProperty("thread"), "name");
    }

    public async Task SetNameAsync(string thread, string name, CancellationToken cancellation) =>
        _ = await CallAsync("thread/name/set", new { threadId = thread, name }, cancellation).ConfigureAwait(false);

    public async Task<bool?> ReadPermissionPendingAsync(string thread, CancellationToken cancellation)
    {
        var result = await CallAsync("thread/read", new { threadId = thread, includeTurns = false }, cancellation).ConfigureAwait(false);
        return PermissionPending(result.GetProperty("thread"));
    }

    public async Task<bool?> ReadUserInputPendingAsync(string thread, CancellationToken cancellation)
    {
        var result = await CallAsync("thread/read", new { threadId = thread, includeTurns = false }, cancellation).ConfigureAwait(false);
        return Pending(result.GetProperty("thread"), "waitingOnUserInput");
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
            active.EnsureCurrent();
        }
        catch (OperationCanceledException) when (active.Strict && !cancellation.IsCancellationRequested)
        {
            throw new CodexTransportException(CodexTransportFailure.Timeout, "The Codex runtime did not answer within its time budget.");
        }
        catch (Exception exception) when (active.Strict && exception is JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException)
        {
            throw new CodexTransportException(CodexTransportFailure.Malformed, "The Codex runtime sent a malformed handshake response.", exception);
        }
    }

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
        var id = ++nextId;
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
                throw new IOException("Codex rejected the native metadata request.");
            }
            return data.GetProperty("result").Clone();
        }
        throw new InvalidDataException("Invalid Codex response.");
    }

    public void Dispose() => transport?.Dispose();
}

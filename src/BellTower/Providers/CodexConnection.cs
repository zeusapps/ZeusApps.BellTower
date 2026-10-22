using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BellTower.Cli;

namespace BellTower.Providers;

public sealed class CodexConnection : ICodexNameClient, ICodexAttentionClient
{
    private readonly ClientWebSocket socket = new();
    private int nextId;

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

    public static bool? PermissionPending(JsonElement thread)
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
                return flags.EnumerateArray().Any(flag => flag.ValueKind == JsonValueKind.String && flag.GetString() == "waitingOnApproval");
            default:
                return null;
        }
    }

    public async Task ConnectAsync(string endpoint, string token, CancellationToken cancellation)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var address) ||
            address.Scheme != "ws" || address.Host != "127.0.0.1")
        {
            throw new InvalidDataException("A dedicated loopback Codex endpoint is required.");
        }
        socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
        await socket.ConnectAsync(address, cancellation).ConfigureAwait(false);
        await CallAsync("initialize", new { clientInfo = new { name = "bell_tower", version = ApplicationInfo.Version } }, cancellation).ConfigureAwait(false);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"method\":\"initialized\",\"params\":{}}").AsMemory(),
            WebSocketMessageType.Text, true, cancellation).ConfigureAwait(false);
    }

    public async Task<JsonElement> CallAsync(string method, object parameters, CancellationToken cancellation)
    {
        var id = ++nextId;
        var request = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id, method, @params = parameters }));
        await socket.SendAsync(request.AsMemory(), WebSocketMessageType.Text, true, cancellation).ConfigureAwait(false);
        var buffer = new byte[8192];
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(buffer, cancellation).ConfigureAwait(false);
                if (received.MessageType != WebSocketMessageType.Text || message.Length + received.Count > 1024 * 1024)
                {
                    throw new InvalidDataException("Invalid Codex response.");
                }
                message.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);
            using var document = JsonDocument.Parse(message.ToArray());
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
    }

    public void Dispose() => socket.Dispose();
}

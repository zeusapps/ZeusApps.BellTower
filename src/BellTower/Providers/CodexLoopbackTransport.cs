using System.Net.WebSockets;

namespace BellTower.Providers;

/// <summary>Owned, authenticated loopback WebSocket to a runtime Bell Tower started. Behavior is the original CodexConnection transport.</summary>
public sealed class CodexLoopbackTransport(string endpoint, string token) : ICodexTransport
{
    // Never through a proxy: the bearer token must only travel the direct loopback connection (as CodexSharedTransport).
    private readonly ClientWebSocket socket = new() { Options = { Proxy = null } };

    public async Task ConnectAsync(CancellationToken cancellation)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var address) ||
            address.Scheme != "ws" || address.Host != "127.0.0.1")
        {
            throw new InvalidDataException("A dedicated loopback Codex endpoint is required.");
        }
        socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
        await socket.ConnectAsync(address, cancellation).ConfigureAwait(false);
    }

    public Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellation) =>
        socket.SendAsync(message, WebSocketMessageType.Text, true, cancellation).AsTask();

    public Task<byte[]> ReceiveAsync(CancellationToken cancellation) => CodexSocket.ReceiveAsync(socket, cancellation);

    public void Dispose() => socket.Dispose();
}

internal static class CodexSocket
{
    public static async Task<byte[]> ReceiveAsync(WebSocket socket, CancellationToken cancellation)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        ValueWebSocketReceiveResult received;
        do
        {
            received = await socket.ReceiveAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                throw new IOException("The Codex runtime closed the connection.");
            }
            if (received.MessageType != WebSocketMessageType.Text || message.Length + received.Count > CodexLimits.MessageBytes)
            {
                throw new InvalidDataException("Invalid Codex response.");
            }
            message.Write(buffer, 0, received.Count);
        }
        while (!received.EndOfMessage);
        return message.ToArray();
    }
}

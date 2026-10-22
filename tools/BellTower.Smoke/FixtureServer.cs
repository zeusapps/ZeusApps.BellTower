using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BellTower.Smoke;

internal static class FixtureServer
{
    public static async Task RunAsync(string[] args)
    {
        var endpoint = new Uri(args[Array.IndexOf(args, "--listen") + 1]);
        var expectedToken = File.ReadAllText(args[Array.IndexOf(args, "--ws-token-file") + 1]);
        var root = Environment.GetEnvironmentVariable("BELLTOWER_FIXTURE_ROOT")!;
        var listener = new TcpListener(IPAddress.Loopback, endpoint.Port);
        listener.Start();
        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = Task.Run(async () =>
            {
                using (client)
                {
                    try
                    {
                        var stream = client.GetStream();
                        var bytes = new List<byte>();
                        var one = new byte[1];
                        while (bytes.Count < 16384)
                        {
                            if (await stream.ReadAsync(one) == 0)
                            { return; }
                            bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.TakeLast(4).ToArray()) == "\r\n\r\n")
                            { break; }
                        }
                        var headers = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
                        if (!headers.Contains("Authorization: Bearer " + expectedToken))
                        { return; }
                        var key = headers.First(h => h.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':', 2)[1].Trim();
                        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"));
                        using var socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30));
                        var buffer = new byte[16384];
                        while (socket.State == WebSocketState.Open)
                        {
                            var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
                            if (received.MessageType != WebSocketMessageType.Text)
                            { return; }
                            using var document = JsonDocument.Parse(buffer.AsMemory(0, received.Count));
                            var request = document.RootElement;
                            if (!request.TryGetProperty("id", out var id))
                            { continue; }
                            var method = request.GetProperty("method").GetString();
                            object result = new { };
                            if (method == "thread/read")
                            {
                                var namePath = Path.Combine(root, "native-name.txt");
                                result = new
                                {
                                    thread = new
                                    {
                                        name = File.Exists(namePath) ? File.ReadAllText(namePath) : (string?)null,
                                        status = new { type = "active", activeFlags = new[] { "waitingOnApproval" } }
                                    }
                                };
                                File.WriteAllText(Path.Combine(root, "read-" + Guid.NewGuid().ToString("N")), Environment.ProcessId.ToString());
                            }
                            if (method == "thread/name/set")
                            { File.WriteAllText(Path.Combine(root, "native-name.txt"), request.GetProperty("params").GetProperty("name").GetString()); }
                            var reply = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { id = id.GetInt32(), result }));
                            await socket.SendAsync(reply, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                    catch (Exception error) when (error is IOException or WebSocketException or JsonException) { }
                }
            });
        }
    }
}

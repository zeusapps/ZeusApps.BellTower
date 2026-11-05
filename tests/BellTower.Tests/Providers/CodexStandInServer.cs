using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BellTower.Tests.Providers;

/// <summary>In-process stand-in for a Codex backend: a WebSocket server over a Unix-domain socket or loopback TCP. It never talks to a real runtime.</summary>
public sealed class CodexStandInServer : IDisposable
{
    public enum Behavior
    {
        Normal,
        Malformed,
        MissingHome,
        Silent
    }

    private readonly Socket listener;
    private readonly CancellationTokenSource stop = new();
    private readonly string? socketPath;
    private readonly List<WebSocket> sockets = [];
    private readonly Lock gate = new();

    public CodexStandInServer(string? socketPath, string reportedHome, Behavior behavior = Behavior.Normal, bool listen = true)
    {
        this.socketPath = socketPath;
        ReportedHome = reportedHome;
        BehaviorMode = behavior;
        if (socketPath is null)
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            Port = ((IPEndPoint)listener.LocalEndPoint!).Port;
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(socketPath)!);
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        }
        if (listen)
        {
            listener.Listen(8);
            _ = Task.Run(AcceptLoop);
        }
    }

    public string ReportedHome { get; set; }
    public Behavior BehaviorMode { get; set; }
    public int Port { get; }
    public string? Authorization { get; private set; }
    public List<string> Methods { get; } = [];
    public int Accepted;

    private async Task AcceptLoop()
    {
        while (!stop.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await listener.AcceptAsync(stop.Token);
            }
            catch (Exception)
            {
                return;
            }
            Interlocked.Increment(ref Accepted);
            _ = Task.Run(() => Serve(client));
        }
    }

    private async Task Serve(Socket client)
    {
        try
        {
            var stream = new NetworkStream(client, true);
            var head = new StringBuilder();
            var one = new byte[1];
            while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                if (await stream.ReadAsync(one, stop.Token) == 0)
                {
                    return;
                }
                head.Append((char)one[0]);
            }
            string? key = null;
            foreach (var line in head.ToString().Split("\r\n"))
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                {
                    key = line[(line.IndexOf(':') + 1)..].Trim();
                }
                else if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    Authorization = line[(line.IndexOf(':') + 1)..].Trim();
                }
            }
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"), stop.Token);
            var socket = WebSocket.CreateFromStream(stream, isServer: true, null, TimeSpan.Zero);
            lock (gate)
            { sockets.Add(socket); }
            var buffer = new byte[65536];
            while (socket.State == WebSocketState.Open)
            {
                var received = await socket.ReceiveAsync(buffer, stop.Token);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
                using var document = JsonDocument.Parse(buffer.AsMemory(0, received.Count));
                var root = document.RootElement;
                var method = root.GetProperty("method").GetString()!;
                lock (gate)
                { Methods.Add(method); }
                if (!root.TryGetProperty("id", out var id) || BehaviorMode == Behavior.Silent)
                {
                    continue;
                }
                string reply;
                if (BehaviorMode == Behavior.Malformed)
                {
                    reply = "{ not json";
                }
                else if (method == "initialize")
                {
                    reply = BehaviorMode == Behavior.MissingHome
                        ? $"{{\"id\":{id.GetRawText()},\"result\":{{\"userAgent\":\"stand-in\"}}}}"
                        : JsonSerializer.Serialize(new { id = id.GetInt32(), result = new { codexHome = ReportedHome, userAgent = "stand-in" } });
                }
                else
                {
                    var thread = root.GetProperty("params").TryGetProperty("threadId", out var value) ? value.GetString() : null;
                    await Task.Delay(thread == "slow" ? 150 : 0);
                    reply = JsonSerializer.Serialize(new { id = id.GetInt32(), result = new { thread = new { id = thread, name = "name-of-" + thread } } });
                }
                await socket.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, stop.Token);
            }
        }
        catch (Exception)
        {
            // A client that disconnects or sends garbage only ends its own stand-in session.
        }
    }

    public bool Serving
    {
        get
        {
            lock (gate)
            { return sockets.Count > 0 && sockets.Any(socket => socket.State == WebSocketState.Open); }
        }
    }

    public string[] RecordedMethods()
    {
        lock (gate)
        { return [.. Methods]; }
    }

    public void Dispose()
    {
        stop.Cancel();
        listener.Dispose();
        if (socketPath is not null)
        {
            try
            { File.Delete(socketPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

/// <summary>
/// A real second process that listens on a Unix-domain socket and accepts connections without speaking any protocol. It is used
/// where the identity of the process serving a socket matters. Optional extra arguments end up on its command line only.
/// </summary>
public sealed class ChildListener : IDisposable
{
    private const string Script = "param($Path, $Extra)\n" +
        "$s = [System.Net.Sockets.Socket]::new('Unix', 'Stream', 'Unspecified')\n" +
        "$s.Bind([System.Net.Sockets.UnixDomainSocketEndPoint]::new($Path))\n" +
        "$s.Listen(8)\n" +
        "$held = @()\n" +
        "while ($true) { $held += $s.Accept() }\n";

    private readonly System.Diagnostics.Process process;
    private readonly string script;
    private readonly string path;

    private ChildListener(System.Diagnostics.Process process, string script, string path, BellTower.Core.ProcessIdentity identity)
    {
        this.process = process;
        this.script = script;
        this.path = path;
        Identity = identity;
    }

    public BellTower.Core.ProcessIdentity Identity { get; }

    public static ChildListener Start(string socketPath, string? extra = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(socketPath)!);
        var script = Path.Combine(Path.GetTempPath(), "btl" + Guid.NewGuid().ToString("N")[..8] + ".ps1");
        File.WriteAllText(script, Script);
        var info = new System.Diagnostics.ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", script, "-Path", socketPath, "-Extra", extra ?? "none" })
        {
            info.ArgumentList.Add(argument);
        }
        var process = System.Diagnostics.Process.Start(info)!;
        var started = new ChildListener(process, script, socketPath, new(process.Id, process.StartTime.ToUniversalTime().Ticks));
        try
        {
            started.WaitUntilListening();
        }
        catch
        {
            started.Dispose();
            throw;
        }
        return started;
    }

    private void WaitUntilListening()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("The child listener ended before listening.");
            }
            try
            {
                using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                probe.Connect(new UnixDomainSocketEndPoint(path));
                return;
            }
            catch (SocketException)
            {
                Thread.Sleep(50);
            }
        }
        throw new TimeoutException("The child listener did not start listening.");
    }

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException) { }
        process.Dispose();
        foreach (var file in new[] { script, path })
        {
            try
            { File.Delete(file); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

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
        Silent,
        // Answers the handshake, then misbehaves on every later request.
        SilentAfterInitialize,
        DropAfterInitialize,
        MalformedAfterInitialize,
        RejectAfterInitialize
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
    /// <summary>Messages read so far across all sessions; counted before the behavior decides whether to answer, so a test can wait until a request has been received.</summary>
    public int Received
    {
        get
        {
            lock (gate)
            { return Methods.Count; }
        }
    }
    public int Accepted;
    /// <summary>WebSocket sessions that have ended (closed by the client or dropped).</summary>
    public int Closed;
    /// <summary>Raw JSON of the thread object returned for a thread id; threads not listed answer with a generated name.</summary>
    public Dictionary<string, string> Threads { get; } = [];
    /// <summary>Names written through thread/name/set by thread id; they also answer later reads.</summary>
    public Dictionary<string, string> Names { get; } = [];
    public List<string> Requests { get; } = [];

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
        var upgraded = false;
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
            upgraded = true;
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
                {
                    Methods.Add(method);
                    Requests.Add(root.GetRawText());
                }
                if (!root.TryGetProperty("id", out var id) || BehaviorMode == Behavior.Silent)
                {
                    continue;
                }
                string reply;
                if (method != "initialize" && BehaviorMode == Behavior.DropAfterInitialize)
                {
                    socket.Abort();
                    return;
                }
                if (method != "initialize" && BehaviorMode == Behavior.SilentAfterInitialize)
                {
                    continue;
                }
                if (BehaviorMode == Behavior.Malformed || (method != "initialize" && BehaviorMode == Behavior.MalformedAfterInitialize))
                {
                    reply = "{ not json";
                }
                else if (method == "initialize")
                {
                    reply = BehaviorMode == Behavior.MissingHome
                        ? $"{{\"id\":{id.GetRawText()},\"result\":{{\"userAgent\":\"stand-in\"}}}}"
                        : JsonSerializer.Serialize(new { id = id.GetInt32(), result = new { codexHome = ReportedHome, userAgent = "stand-in" } });
                }
                else if (BehaviorMode == Behavior.RejectAfterInitialize)
                {
                    reply = $"{{\"id\":{id.GetRawText()},\"error\":{{\"code\":-32600,\"message\":\"rejected\"}}}}";
                }
                else
                {
                    var arguments = root.GetProperty("params");
                    var thread = arguments.TryGetProperty("threadId", out var value) ? value.GetString() : null;
                    await Task.Delay(thread == "slow" ? 150 : 0);
                    if (method == "thread/name/set" && thread is not null)
                    {
                        lock (gate)
                        { Names[thread] = arguments.GetProperty("name").GetString()!; }
                    }
                    string? stored;
                    string? custom;
                    lock (gate)
                    {
                        stored = thread is not null && Names.TryGetValue(thread, out var written) ? written : null;
                        custom = thread is not null && Threads.TryGetValue(thread, out var json) ? json : null;
                    }
                    reply = custom is not null
                        ? $"{{\"id\":{id.GetRawText()},\"result\":{{\"thread\":{custom}}}}}"
                        : JsonSerializer.Serialize(new { id = id.GetInt32(), result = new { thread = new { id = thread, name = stored ?? "name-of-" + thread } } });
                }
                await socket.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, true, stop.Token);
            }
        }
        catch (Exception)
        {
            // A client that disconnects or sends garbage only ends its own stand-in session.
        }
        finally
        {
            if (upgraded)
            {
                Interlocked.Increment(ref Closed);
            }
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

    public string[] RecordedRequests()
    {
        lock (gate)
        { return [.. Requests]; }
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

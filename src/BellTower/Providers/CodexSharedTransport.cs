using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Providers;

/// <summary>
/// Non-owning access to a shared Codex backend through its local control socket. The socket path comes only from the resolved
/// Codex home. The backend identity is derived from the connected socket itself (the server's process ID, then PID plus creation
/// time) before any byte is sent: the process must be live, its image must be codex and its command line must classify as a shared
/// backend. The initialize response must also report the same Codex home. The connected peer is rechecked after the handshake and
/// before every request, so a restarted backend or a different process listening at the path drops the connection instead of
/// being reused. Disposing closes only this connection; the backend is never stopped, reconfigured or resumed from here.
/// </summary>
/// <param name="expected">Optional extra comparison: when given, the derived backend must equal it.</param>
/// <param name="isBackend">Replaces the live/image/command-line check (tests).</param>
/// <param name="peerOf">Replaces the peer process lookup of a connected socket (tests).</param>
public sealed class CodexSharedTransport(string codexHome, ProcessIdentity? expected = null, CodexTransportBudget? budget = null,
    Func<ProcessIdentity, bool>? isBackend = null, Func<Socket, int?>? peerOf = null) : ICodexTransport
{
    private readonly ClientWebSocket socket = new();
    private readonly Func<ProcessIdentity, bool> isBackend = isBackend ?? IsLiveSharedBackend;
    private string? socketPath;
    private CodexTransportException? refusal;

    public CodexTransportBudget? Budget { get; } = budget ?? CodexTransportBudget.Default;

    public bool Strict => true;

    /// <summary>The backend process verified through the connected socket; set once the connection is established.</summary>
    public ProcessIdentity? Backend { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellation)
    {
        var path = CodexSharedEndpoint.SocketPath(codexHome);
        RejectLinkedDirectory(path);
        var info = new FileInfo(path);
        if ((int)info.Attributes == -1 || (info.Attributes & FileAttributes.Directory) != 0)
        {
            throw new CodexTransportException(CodexTransportFailure.EndpointMissing, "The shared Codex control socket does not exist.");
        }
        socketPath = path;
        using var handler = new SocketsHttpHandler { ConnectCallback = ConnectSocketAsync, UseProxy = false };
        using var invoker = new HttpMessageInvoker(handler, false);
        try
        {
            await socket.ConnectAsync(new Uri("ws://localhost/"), invoker, cancellation).ConfigureAwait(false);
        }
        catch (Exception exception) when (refusal is not null)
        {
            throw new CodexTransportException(refusal.Failure, refusal.Message, exception);
        }
        catch (Exception exception) when (exception is WebSocketException or HttpRequestException or SocketException or IOException)
        {
            throw new CodexTransportException(CodexTransportFailure.Refused, "The shared Codex backend refused or dropped the connection.", exception);
        }
        EnsureCurrent();
    }

    private async ValueTask<Stream> ConnectSocketAsync(SocketsHttpConnectionContext context, CancellationToken cancellation)
    {
        var unix = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await unix.ConnectAsync(new UnixDomainSocketEndPoint(socketPath!), cancellation).ConfigureAwait(false);
            Backend = Verify(unix);
            return new NetworkStream(unix, true);
        }
        catch (CodexTransportException exception)
        {
            // The HTTP stack wraps callback failures; keep the reason for the caller. Nothing was sent to the peer.
            refusal = exception;
            unix.Dispose();
            throw;
        }
        catch
        {
            unix.Dispose();
            throw;
        }
    }

    // Binds the process serving this socket, not a caller-held identity, to the Codex shared backend requirements.
    private ProcessIdentity Verify(Socket peer)
    {
        var identity = CodexSharedPeer.Identify(peer, peerOf);
        if (expected is { } wanted && wanted != identity)
        {
            throw new CodexTransportException(CodexTransportFailure.BackendNotVerified, "A different process than the expected one serves the control socket.");
        }
        if (!isBackend(identity))
        {
            throw new CodexTransportException(CodexTransportFailure.BackendNotVerified, "The process serving the control socket is not a live Codex shared backend.");
        }
        return identity;
    }

    // A junction or symbolic link in place of the control directory could redirect the path out of the Codex home.
    private static void RejectLinkedDirectory(string path)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
        if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new CodexTransportException(CodexTransportFailure.HomeMismatch, "The control directory is a link and not part of the Codex home.");
        }
    }

    public void VerifyInitialized(JsonElement initializeResult)
    {
        if (initializeResult.ValueKind != JsonValueKind.Object || !initializeResult.TryGetProperty("codexHome", out var reported) ||
            reported.ValueKind != JsonValueKind.String)
        {
            throw new CodexTransportException(CodexTransportFailure.Malformed, "The initialize response did not report a Codex home.");
        }
        if (!CodexSharedEndpoint.SameHome(reported.GetString(), codexHome))
        {
            throw new CodexTransportException(CodexTransportFailure.HomeMismatch, "The backend reports a different Codex home than the one resolved for this client.");
        }
    }

    /// <summary>
    /// The verified backend must still be live, and the process now listening at the path must be that same backend. The listener is
    /// identified by a short connect to the path that sends nothing (the socket's creation time cannot show a quick re-creation,
    /// because NTFS keeps it across a short delete and create).
    /// </summary>
    public void EnsureCurrent()
    {
        var backend = Backend;
        if (backend is null)
        {
            return;
        }
        if (!isBackend(backend))
        {
            throw Replaced("The shared Codex backend was replaced or ended.");
        }
        var current = ListenerIdentity();
        if (current != backend)
        {
            throw Replaced("A different process is listening at the shared Codex control socket.");
        }
    }

    private ProcessIdentity? ListenerIdentity()
    {
        using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            probe.ConnectAsync(new UnixDomainSocketEndPoint(socketPath!), limit.Token).AsTask().GetAwaiter().GetResult();
            return CodexSharedPeer.Identify(probe, peerOf);
        }
        catch (CodexTransportException)
        {
            return null;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException or IOException)
        {
            return null;
        }
    }

    private static CodexTransportException Replaced(string message) => new(CodexTransportFailure.EndpointReplaced, message);

    public Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellation) =>
        socket.SendAsync(message, WebSocketMessageType.Text, true, cancellation).AsTask();

    public Task<byte[]> ReceiveAsync(CancellationToken cancellation) => CodexSocket.ReceiveAsync(socket, cancellation);

    public void Dispose() => socket.Dispose();

    private static bool IsLiveSharedBackend(ProcessIdentity identity) =>
        ProcessProbe.Inspect(identity) == ProcessLiveness.Live && IsCodexImage(identity.Pid) &&
        ProcessProbe.CommandLine(identity.Pid) is { } text &&
        AgentModeClassifier.Classify("codex", true, AgentModeClassifier.Arguments(text)) == AgentModes.SharedBackend;

    // The image name only: the managed package root differs between installs and shims and is not a requirement.
    private static bool IsCodexImage(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return string.Equals(process.ProcessName, "codex", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

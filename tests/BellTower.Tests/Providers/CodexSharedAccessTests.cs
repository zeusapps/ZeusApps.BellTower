using System.Net.Sockets;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using Xunit;

namespace BellTower.Tests.Providers;

/// <summary>
/// Shared-backend access refinements of task 3.2: distinct refusal reasons, the real peer lookup paths, reasoned failures after the
/// handshake, one normalized home key, and the thread-identity mapping. Servers are stand-ins; nothing reaches a real runtime.
/// </summary>
public sealed class CodexSharedAccessTests : IDisposable
{
    private static readonly CodexTransportBudget Quick = new(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(400));
    private const string BackendLine = "codex app-server --listen unix:// --managed-daemon";
    private readonly List<string> roots = [];
    private readonly List<CodexStandInServer> servers = [];

    private string Home()
    {
        var root = Path.Combine(Path.GetTempPath(), "bt" + Guid.NewGuid().ToString("N")[..6]);
        roots.Add(root);
        Directory.CreateDirectory(root);
        return root;
    }

    private CodexStandInServer Serve(string home, CodexStandInServer.Behavior behavior = CodexStandInServer.Behavior.Normal)
    {
        var server = new CodexStandInServer(CodexSharedEndpoint.SocketPath(home), home, behavior);
        servers.Add(server);
        return server;
    }

    public void Dispose()
    {
        foreach (var server in servers)
        {
            server.Dispose();
        }
        foreach (var root in roots)
        {
            try
            { Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static readonly ProcessIdentity Backend = new(4242, 1);

    private static CodexBackendCheck Check(ProcessLiveness live = ProcessLiveness.Live, bool image = true, string? line = BackendLine, bool? elevated = false) =>
        new(null, _ => live, _ => image, _ => line, _ => elevated);

    [Fact]
    public void EachRequirementOfASharedBackendRefusesWithItsOwnReason()
    {
        Check().Verify(Backend);
        Assert.Equal(CodexTransportFailure.BackendNotVerified, Assert.Throws<CodexTransportException>(() => Check(ProcessLiveness.Ended).Verify(Backend)).Failure);
        Assert.Equal(CodexTransportFailure.ImageMismatch, Assert.Throws<CodexTransportException>(() => Check(image: false).Verify(Backend)).Failure);
        Assert.Equal(CodexTransportFailure.PeerUnreadable, Assert.Throws<CodexTransportException>(() => Check(line: null).Verify(Backend)).Failure);
        Assert.Equal(CodexTransportFailure.CommandLineMismatch, Assert.Throws<CodexTransportException>(() => Check(line: "codex --version").Verify(Backend)).Failure);
        Assert.Equal(CodexTransportFailure.CommandLineMismatch, Assert.Throws<CodexTransportException>(() => Check(line: "codex resume").Verify(Backend)).Failure);
        Assert.Equal(CodexTransportFailure.ElevatedPeer, Assert.Throws<CodexTransportException>(() => Check(elevated: true).Verify(Backend)).Failure);
        Assert.Equal(CodexTransportFailure.PeerUnreadable, Assert.Throws<CodexTransportException>(() => Check(elevated: null).Verify(Backend)).Failure);
    }

    [Fact]
    public void IdentityIsRefusedBeforeElevationSoAReasonNamesTheFirstProblem()
    {
        Assert.Equal(CodexTransportFailure.ImageMismatch, Assert.Throws<CodexTransportException>(() => Check(image: false, elevated: true).Verify(Backend)).Failure);
    }

    [Fact]
    public async Task ElevatedBackendIsRefusedBeforeAnyRequestIsSent()
    {
        var home = Home();
        var server = Serve(home);
        var access = new CodexSharedAccess(null, Quick, Check(elevated: true), _ => Environment.ProcessId);
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.ElevatedPeer, exception.Failure);
        Assert.Empty(server.RecordedMethods());
        Assert.Equal(1, access.Failures(home));
    }

    [Fact]
    public async Task NonElevatedCodexSharedBackendIsAccepted()
    {
        var home = Home();
        Serve(home);
        var (connection, _, backend) = await new CodexSharedAccess(null, Quick, Check(), _ => Environment.ProcessId).OpenAsync(home, CancellationToken.None);
        connection.Dispose();
        Assert.Equal(Environment.ProcessId, backend.Pid);
    }

    [Fact]
    public void ProcessElevationCanBeReadForTheCurrentProcessAndNotForAMissingOne()
    {
        Assert.NotNull(ProcessProbe.IsElevated(Environment.ProcessId));
        Assert.Null(ProcessProbe.IsElevated(int.MaxValue - 7));
    }

    [Fact]
    public void RealPeerLookupIsUnsupportedOnASocketThatIsNotLocal()
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        listener.Listen(1);
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndPoint!);
        Assert.Null(CodexSharedPeer.PeerPid(client));
        Assert.Equal(CodexTransportFailure.PeerUnsupported, Assert.Throws<CodexTransportException>(() => CodexSharedPeer.Identify(client)).Failure);
    }

    [Fact]
    public void RealPeerLookupOnADisposedSocketIsNullInsteadOfARawException()
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Dispose();
        Assert.Null(CodexSharedPeer.PeerPid(socket));
    }

    [Fact]
    public void AnEndedPeerAndAnUnreadablePeerAreDifferentRefusals()
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        Assert.Equal(CodexTransportFailure.BackendNotVerified,
            Assert.Throws<CodexTransportException>(() => CodexSharedPeer.Identify(socket, _ => int.MaxValue - 7)).Failure);
        // A process that exists but whose identity cannot be read.
        Assert.Equal(CodexTransportFailure.PeerUnreadable,
            Assert.Throws<CodexTransportException>(() => CodexSharedPeer.Identify(socket, _ => Environment.ProcessId, _ => null)).Failure);
    }

    [Theory]
    [InlineData(CodexStandInServer.Behavior.SilentAfterInitialize, CodexTransportFailure.Timeout)]
    [InlineData(CodexStandInServer.Behavior.DropAfterInitialize, CodexTransportFailure.Refused)]
    [InlineData(CodexStandInServer.Behavior.MalformedAfterInitialize, CodexTransportFailure.Malformed)]
    [InlineData(CodexStandInServer.Behavior.RejectAfterInitialize, CodexTransportFailure.Rejected)]
    public async Task FailuresAfterTheHandshakeCarryAReason(CodexStandInServer.Behavior behavior, CodexTransportFailure expected)
    {
        var home = Home();
        Serve(home, behavior);
        var (connection, _, _) = await new CodexSharedAccess(null, Quick, CodexBackendCheck.Accepting()).OpenAsync(home, CancellationToken.None);
        using (connection)
        {
            var exception = await Assert.ThrowsAsync<CodexTransportException>(() => connection.ReadNameAsync("one", CancellationToken.None));
            Assert.Equal(expected, exception.Failure);
            Assert.DoesNotContain("rejected\"", exception.Message);
        }
    }

    [Fact]
    public async Task CallerCancellationAfterTheHandshakeIsNotReportedAsATimeout()
    {
        var home = Home();
        Serve(home, CodexStandInServer.Behavior.SilentAfterInitialize);
        var (connection, _, _) = await new CodexSharedAccess(null, new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)), CodexBackendCheck.Accepting())
            .OpenAsync(home, CancellationToken.None);
        using (connection)
        using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.ReadNameAsync("one", cancel.Token));
        }
    }

    [Fact]
    public async Task SpellingsOfOneHomeShareBackoffAndReplacementTracking()
    {
        var home = Home();
        var variants = new[] { home, home.ToUpperInvariant(), home + Path.DirectorySeparatorChar, home.ToUpperInvariant() + Path.DirectorySeparatorChar };
        Assert.Single(variants.Select(CodexSharedEndpoint.HomeKey).Distinct());
        var now = DateTimeOffset.UtcNow;
        var access = new CodexSharedAccess(new(TimeSpan.Zero, () => now), Quick, CodexBackendCheck.Accepting());
        await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(variants[0], CancellationToken.None));
        foreach (var variant in variants.Skip(1))
        {
            Assert.Equal(1, access.Failures(variant));
            var exception = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(variant, CancellationToken.None));
            Assert.Equal(CodexTransportFailure.BackedOff, exception.Failure);
        }
    }

    [Fact]
    public async Task ReplacementIsTrackedAcrossSpellingsOfTheSameHome()
    {
        var home = Home();
        Serve(home);
        using var other = ChildListener.Start(Path.Combine(Home(), "app-server-control", "app-server-control.sock"));
        var peer = Environment.ProcessId;
        var access = new CodexSharedAccess(null, Quick, CodexBackendCheck.Accepting(), _ => peer);
        var (one, first, _) = await access.OpenAsync(home, CancellationToken.None);
        one.Dispose();
        Assert.False(first);
        peer = other.Identity.Pid;
        var (two, second, _) = await access.OpenAsync(home.ToUpperInvariant() + Path.DirectorySeparatorChar, CancellationToken.None);
        two.Dispose();
        Assert.True(second);
    }

    [Fact]
    public void HomeKeyAndSocketPathAcceptAnAbsoluteHomeAndRefuseOthers()
    {
        Assert.Equal(CodexTransportFailure.EndpointMissing, Assert.Throws<CodexTransportException>(() => CodexSharedEndpoint.HomeKey("relative")).Failure);
        var drive = Path.GetPathRoot(Path.GetTempPath())!;
        Assert.Equal(Path.Combine(drive, "app-server-control", "app-server-control.sock"), CodexSharedEndpoint.SocketPath(drive));
    }

    private const string Root = "{\"id\":\"one\",\"sessionId\":\"one\",\"name\":\"Title\",\"parentThreadId\":null,\"forkedFromId\":null,\"status\":{\"type\":\"idle\"}}";

    [Fact]
    public async Task ARootThreadWhoseIdentifiersAgreeIsMappedAndReadWithoutTurnsOrResume()
    {
        var home = Home();
        var server = Serve(home);
        server.Threads["one"] = Root;
        var (connection, _, _) = await new CodexSharedAccess(null, Quick, CodexBackendCheck.Accepting()).OpenAsync(home, CancellationToken.None);
        using (connection)
        {
            Assert.Equal("Title", await connection.ReadNameAsync("one", CancellationToken.None));
            Assert.False(await connection.ReadPermissionPendingAsync("one", CancellationToken.None));
        }
        Assert.DoesNotContain(server.RecordedMethods(), method => method is "thread/resume" || method.StartsWith("turn/", StringComparison.Ordinal));
        var reads = server.RecordedRequests().Where(request => request.Contains("thread/read", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(reads);
        Assert.All(reads, request => Assert.Contains("\"includeTurns\":false", request, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{\"id\":\"other\",\"sessionId\":\"other\",\"name\":\"x\"}", CodexTransportFailure.ThreadUnmapped)]
    [InlineData("{\"name\":\"x\"}", CodexTransportFailure.ThreadUnmapped)]
    [InlineData("{\"id\":\"one\",\"sessionId\":\"root\",\"name\":\"x\"}", CodexTransportFailure.ThreadUnverified)]
    [InlineData("{\"id\":\"one\",\"sessionId\":\"one\",\"parentThreadId\":\"root\",\"name\":\"x\"}", CodexTransportFailure.ThreadUnverified)]
    [InlineData("{\"id\":\"one\",\"sessionId\":\"one\",\"forkedFromId\":\"root\",\"name\":\"x\"}", CodexTransportFailure.ThreadUnverified)]
    [InlineData("{\"id\":\"one\",\"sessionId\":\"one\",\"agentRole\":\"worker\",\"name\":\"x\"}", CodexTransportFailure.ThreadUnverified)]
    public async Task AThreadThatIsNotTheRequestedRootIsRefusedOnASharedBackendWithTheRightKind(string thread, CodexTransportFailure expected)
    {
        var home = Home();
        var server = Serve(home);
        server.Threads["one"] = thread;
        var (connection, _, _) = await new CodexSharedAccess(null, Quick, CodexBackendCheck.Accepting()).OpenAsync(home, CancellationToken.None);
        using (connection)
        {
            var exception = await Assert.ThrowsAsync<CodexTransportException>(() => connection.ReadNameAsync("one", CancellationToken.None));
            Assert.Equal(expected, exception.Failure);
        }
    }

    [Fact]
    public async Task ConcurrentFailedOpensCountOneBackoffFailureAndALaterSuccessResets()
    {
        var home = Home();
        var server = Serve(home, CodexStandInServer.Behavior.Silent);
        var now = DateTimeOffset.UtcNow;
        var access = new CodexSharedAccess(new(TimeSpan.Zero, () => now), Quick, CodexBackendCheck.Accepting());
        var opens = Enumerable.Range(0, 4).Select(_ => Task.Run(() => access.OpenAsync(home, CancellationToken.None))).ToArray();
        foreach (var open in opens)
        {
            Assert.Equal(CodexTransportFailure.Timeout, (await Assert.ThrowsAsync<CodexTransportException>(() => open)).Failure);
        }
        // Every open was a real overlapping attempt, not a late one that was merely backed off.
        Assert.Equal(4, server.Accepted);
        Assert.Equal(1, access.Failures(home));
        // The frozen clock is inside the 2 s window: still backed off, and the count stays one.
        Assert.Equal(CodexTransportFailure.BackedOff, (await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None))).Failure);
        Assert.Equal(1, access.Failures(home));
        // After the window the backend answers: the next open succeeds and the count resets.
        server.BehaviorMode = CodexStandInServer.Behavior.Normal;
        now = now.AddSeconds(3);
        var (connection, _, _) = await access.OpenAsync(home, CancellationToken.None);
        connection.Dispose();
        Assert.Equal(0, access.Failures(home));
    }

    [Fact]
    public async Task SequentialFailuresInLaterWindowsAdvanceTheBackoff()
    {
        var home = Home();
        Serve(home, CodexStandInServer.Behavior.Silent);
        var now = DateTimeOffset.UtcNow;
        var access = new CodexSharedAccess(new(TimeSpan.Zero, () => now), Quick, CodexBackendCheck.Accepting());
        for (var expected = 1; expected <= 3; expected++)
        {
            Assert.Equal(CodexTransportFailure.Timeout, (await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None))).Failure);
            Assert.Equal(expected, access.Failures(home));
            now = now.AddSeconds(60);
        }
    }

    [Fact]
    public async Task AFailureThatSpansASuccessAndANewerFailureIsNotCountedAgain()
    {
        var home = Home();
        var server = Serve(home, CodexStandInServer.Behavior.Silent);
        var now = DateTimeOffset.UtcNow;
        var live = ProcessLiveness.Live;
        var check = new CodexBackendCheck(null, _ => live, _ => true, _ => BackendLine, _ => false);
        var access = new CodexSharedAccess(new(TimeSpan.Zero, () => now), new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)), check, _ => Environment.ProcessId);
        // X fails: count 1.
        await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(1, access.Failures(home));
        now = now.AddSeconds(3);
        // A starts in the expired window and stays silent while the others run.
        var a = Task.Run(() => access.OpenAsync(home, CancellationToken.None));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (server.Accepted < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        Assert.Equal(2, server.Accepted);
        // C succeeds (count 0), then D fails fast (count 1), both while A is still waiting.
        server.BehaviorMode = CodexStandInServer.Behavior.Normal;
        var (connection, _, _) = await access.OpenAsync(home, CancellationToken.None);
        connection.Dispose();
        Assert.Equal(0, access.Failures(home));
        live = ProcessLiveness.Ended;
        Assert.Equal(CodexTransportFailure.BackendNotVerified, (await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None))).Failure);
        Assert.Equal(1, access.Failures(home));
        // A now times out. D and A failed in the same window, so the home counts one failure.
        Assert.Equal(CodexTransportFailure.Timeout, (await Assert.ThrowsAsync<CodexTransportException>(() => a)).Failure);
        Assert.Equal(1, access.Failures(home));
    }

    [Fact]
    public async Task AFailureThatStartedBeforeASuccessDoesNotUndoTheSuccess()
    {
        var home = Home();
        var server = Serve(home, CodexStandInServer.Behavior.Silent);
        var now = DateTimeOffset.UtcNow;
        var access = new CodexSharedAccess(new(TimeSpan.Zero, () => now), new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)), CodexBackendCheck.Accepting());
        var slow = Task.Run(() => access.OpenAsync(home, CancellationToken.None));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (server.Accepted < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        server.BehaviorMode = CodexStandInServer.Behavior.Normal;
        var (connection, _, _) = await access.OpenAsync(home, CancellationToken.None);
        connection.Dispose();
        await Assert.ThrowsAsync<CodexTransportException>(() => slow);
        Assert.Equal(0, access.Failures(home));
    }

    [Fact]
    public async Task OwnedRuntimeStillAcceptsAThreadWithoutIdentityFieldsButNotADifferentThread()
    {
        using var server = new CodexStandInServer(null, "ignored");
        using var connection = new CodexConnection();
        await connection.ConnectAsync("ws://127.0.0.1:" + server.Port, "token", CancellationToken.None);
        server.Threads["bare"] = "{\"name\":\"x\"}";
        server.Threads["moved"] = "{\"id\":\"elsewhere\",\"name\":\"x\"}";
        Assert.Equal("x", await connection.ReadNameAsync("bare", CancellationToken.None));
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => connection.ReadNameAsync("moved", CancellationToken.None));
        Assert.Equal(CodexTransportFailure.ThreadUnmapped, exception.Failure);
    }
}

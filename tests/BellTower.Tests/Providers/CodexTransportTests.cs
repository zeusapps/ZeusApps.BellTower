using BellTower.Core;
using BellTower.Providers;
using Xunit;

namespace BellTower.Tests.Providers;

public sealed class CodexTransportTests : IDisposable
{
    private static readonly CodexTransportBudget Quick = new(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(400));
    private readonly List<string> roots = [];
    private readonly List<CodexStandInServer> servers = [];

    private string Home(string? suffix = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "bt" + Guid.NewGuid().ToString("N")[..6] + suffix);
        roots.Add(root);
        Directory.CreateDirectory(root);
        return root;
    }

    private CodexStandInServer Serve(string home, CodexStandInServer.Behavior behavior = CodexStandInServer.Behavior.Normal, string? reported = null, bool listen = true)
    {
        var server = new CodexStandInServer(CodexSharedEndpoint.SocketPath(home), reported ?? home, behavior, listen);
        servers.Add(server);
        return server;
    }

    private static CodexSharedAccess Access(Func<ProcessIdentity, bool>? isBackend = null, NativeNameSchedule? schedule = null) =>
        new(schedule, Quick, CodexBackendCheck.Accepting(isBackend));

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

    [Fact]
    public async Task SharedTransportInitializesAgainstTheSocketDerivedFromTheCodexHomeOnly()
    {
        var home = Home();
        var other = Home();
        var server = Serve(home);
        var unrelated = Serve(other);
        var (connection, replaced, _) = await Access().OpenAsync(home, CancellationToken.None);
        using (connection)
        {
            Assert.False(replaced);
            var thread = await connection.CallAsync("thread/read", new { threadId = "one" }, CancellationToken.None);
            Assert.Equal("name-of-one", thread.GetProperty("thread").GetProperty("name").GetString());
        }
        Assert.Equal(["initialize", "initialized", "thread/read"], server.RecordedMethods());
        Assert.Equal(0, unrelated.Accepted);
    }

    [Fact]
    public async Task SharedTransportNeverResumesStartsTurnsOrTakesApprovals()
    {
        var home = Home();
        var server = Serve(home);
        var (connection, _, _) = await Access().OpenAsync(home, CancellationToken.None);
        using (connection)
        {
            _ = await connection.ReadPermissionPendingAsync("one", CancellationToken.None);
        }
        Assert.DoesNotContain(server.RecordedMethods(), method => method is "thread/resume" or "turn/start" || method.StartsWith("turn/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HomeReportedByTheBackendMustMatchTheResolvedHome()
    {
        var home = Home();
        Serve(home, reported: Home());
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => Access().OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.HomeMismatch, exception.Failure);
    }

    [Fact]
    public async Task HomeComparisonToleratesCaseAndTrailingSeparators()
    {
        var home = Home();
        Serve(home, reported: home.ToUpperInvariant() + Path.DirectorySeparatorChar);
        var (connection, _, _) = await Access().OpenAsync(home, CancellationToken.None);
        connection.Dispose();
    }

    [Fact]
    public async Task UnverifiedBackendProcessIsRefusedBeforeAnyRequestIsSent()
    {
        var home = Home();
        var server = Serve(home);
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => Access(_ => false).OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.BackendNotVerified, exception.Failure);
        Assert.Empty(server.RecordedMethods());
    }

    [Fact]
    public async Task DefaultProvenanceRefusesAPeerThatIsNotACodexSharedBackend()
    {
        var home = Home();
        var server = Serve(home);
        // The listener is this test host: live, but not a codex image.
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => new CodexSharedAccess(null, Quick).OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.ImageMismatch, exception.Failure);
        Assert.Empty(server.RecordedMethods());
    }

    [Fact]
    public async Task DefaultProvenanceRequiresTheCodexImageEvenWithASharedBackendCommandLine()
    {
        var home = Home();
        // The child's command line carries the app-server token, so only the image name (pwsh, not codex) can refuse it.
        using var child = ChildListener.Start(CodexSharedEndpoint.SocketPath(home), "app-server");
        var line = BellTower.IO.ProcessProbe.CommandLine(child.Identity.Pid)!;
        Assert.Equal(AgentModes.SharedBackend, AgentModeClassifier.Classify("codex", true, AgentModeClassifier.Arguments(line)));
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => new CodexSharedAccess(null, Quick).OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.ImageMismatch, exception.Failure);
    }

    [Fact]
    public async Task UnavailablePeerLookupFailsClosedWithItsOwnReason()
    {
        var home = Home();
        var server = Serve(home);
        var access = new CodexSharedAccess(null, Quick, CodexBackendCheck.Accepting(), _ => null);
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.PeerUnsupported, exception.Failure);
        Assert.Empty(server.RecordedMethods());
    }

    [Fact]
    public void ConnectedSocketReportsTheListeningChildProcess()
    {
        var home = Home();
        using var child = ChildListener.Start(CodexSharedEndpoint.SocketPath(home));
        using var client = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        client.Connect(new System.Net.Sockets.UnixDomainSocketEndPoint(CodexSharedEndpoint.SocketPath(home)));
        Assert.Equal(child.Identity, CodexSharedPeer.Identify(client));
        Assert.NotEqual(Environment.ProcessId, child.Identity.Pid);
    }

    [Fact]
    public async Task ExpectedIdentityThatIsAliveButNotTheListenerIsRefused()
    {
        var home = Home();
        var server = Serve(home);
        using var other = ChildListener.Start(CodexSharedEndpoint.SocketPath(Home()));
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => Access().OpenAsync(home, CancellationToken.None, other.Identity));
        Assert.Equal(CodexTransportFailure.BackendNotVerified, exception.Failure);
        Assert.Empty(server.RecordedMethods());
    }

    [Fact]
    public async Task ExpectedIdentityEqualToTheListenerIsAccepted()
    {
        var home = Home();
        Serve(home);
        var self = BellTower.IO.ProcessProbe.Identify(Environment.ProcessId)!;
        var (connection, _, backend) = await Access().OpenAsync(home, CancellationToken.None, self);
        connection.Dispose();
        Assert.Equal(self, backend);
    }

    [Fact]
    public async Task MissingSocketFailsWithAReasonAndBacksOffWithoutRetrying()
    {
        var home = Home();
        var now = DateTimeOffset.UtcNow;
        var access = Access(schedule: new(TimeSpan.Zero, () => now));
        var first = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.EndpointMissing, first.Failure);
        var second = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.BackedOff, second.Failure);
        Assert.Equal(1, access.Failures(home));
        now = now.AddSeconds(NativeNameSchedule.BackoffSeconds[0] + 1);
        var third = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.EndpointMissing, third.Failure);
        Assert.Equal(2, access.Failures(home));
        now = now.AddSeconds(1);
        Assert.Equal(CodexTransportFailure.BackedOff, (await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None))).Failure);
    }

    [Fact]
    public async Task BackoffRecoversAfterTheBackendAppears()
    {
        var home = Home();
        var now = DateTimeOffset.UtcNow;
        var access = Access(schedule: new(TimeSpan.Zero, () => now));
        await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Serve(home);
        now = now.AddSeconds(3);
        var (connection, _, _) = await access.OpenAsync(home, CancellationToken.None);
        connection.Dispose();
        Assert.Equal(0, access.Failures(home));
        var (again, _, _) = await access.OpenAsync(home, CancellationToken.None);
        again.Dispose();
    }

    [Fact]
    public async Task RefusedConnectionIsReportedAndBacksOff()
    {
        var home = Home();
        var server = Serve(home, listen: false);
        var access = Access();
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.Refused, exception.Failure);
        Assert.Equal(0, server.Accepted);
        Assert.Equal(CodexTransportFailure.BackedOff, (await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None))).Failure);
    }

    [Fact]
    public async Task SilentBackendTimesOutWithinTheHandshakeBudget()
    {
        var home = Home();
        Serve(home, CodexStandInServer.Behavior.Silent);
        var started = DateTime.UtcNow;
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => Access().OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.Timeout, exception.Failure);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(CodexStandInServer.Behavior.Malformed)]
    [InlineData(CodexStandInServer.Behavior.MissingHome)]
    public async Task MalformedHandshakeFailsClosedAndBacksOff(CodexStandInServer.Behavior behavior)
    {
        var home = Home();
        Serve(home, behavior);
        var access = Access();
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.Malformed, exception.Failure);
        Assert.Equal(1, access.Failures(home));
    }

    [Fact]
    public async Task OverlongSocketPathFailsClosedWithoutConnecting()
    {
        var home = Home(new string('x', 90));
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => Access().OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.PathTooLong, exception.Failure);
        Assert.Throws<CodexTransportException>(() => CodexSharedEndpoint.SocketPath(home));
    }

    [Fact]
    public void SocketPathIsDerivedOnlyFromAnAbsoluteResolvedHome()
    {
        var home = Home();
        Assert.Equal(Path.Combine(home, "app-server-control", "app-server-control.sock"), CodexSharedEndpoint.SocketPath(home + Path.DirectorySeparatorChar));
        Assert.Equal(CodexTransportFailure.EndpointMissing, Assert.Throws<CodexTransportException>(() => CodexSharedEndpoint.SocketPath("relative")).Failure);
        Assert.Equal(CodexTransportFailure.EndpointMissing, Assert.Throws<CodexTransportException>(() => CodexSharedEndpoint.SocketPath("")).Failure);
    }

    [Fact]
    public async Task EndedBackendDropsTheOldConnection()
    {
        var home = Home();
        Serve(home);
        var alive = true;
        var access = Access(_ => alive);
        var (connection, replaced, _) = await access.OpenAsync(home, CancellationToken.None);
        Assert.False(replaced);
        using (connection)
        {
            _ = await connection.CallAsync("thread/read", new { threadId = "one" }, CancellationToken.None);
            alive = false;
            var exception = await Assert.ThrowsAsync<CodexTransportException>(() => connection.CallAsync("thread/read", new { threadId = "one" }, CancellationToken.None));
            Assert.Equal(CodexTransportFailure.EndpointReplaced, exception.Failure);
        }
    }

    [Fact]
    public async Task EachOpenMakesOneBackendConnectionAndRequestsMakeNone()
    {
        var home = Home();
        var server = Serve(home);
        var (connection, _, _) = await Access().OpenAsync(home, CancellationToken.None);
        using (connection)
        {
            Assert.Equal(1, server.Accepted);
            for (var request = 0; request < 3; request++)
            {
                _ = await connection.CallAsync("thread/read", new { threadId = "one" }, CancellationToken.None);
            }
            Assert.Equal(1, server.Accepted);
        }
    }

    [Fact]
    public async Task ASupersedingListenerIsSeenByTheNextOpenAndTheVerifiedConnectionKeepsItsBackend()
    {
        var home = Home();
        var path = CodexSharedEndpoint.SocketPath(home);
        var first = Serve(home);
        using var intruder = ChildListener.Start(Path.Combine(Home(), "app-server-control", "app-server-control.sock"));
        var peer = Environment.ProcessId;
        var access = new CodexSharedAccess(null, Quick, CodexBackendCheck.Accepting(), _ => peer);
        var (connection, replaced, backend) = await access.OpenAsync(home, CancellationToken.None);
        Assert.False(replaced);
        using (connection)
        {
            // Another process now answers as the peer of new connections; the established connection still talks to the
            // verified one and needs no probe, so it keeps working and the stand-in sees no extra connection.
            peer = intruder.Identity.Pid;
            var thread = await connection.CallAsync("thread/read", new { threadId = "one" }, CancellationToken.None);
            Assert.Equal("name-of-one", thread.GetProperty("thread").GetProperty("name").GetString());
            Assert.Equal(1, first.Accepted);
            Assert.True(File.Exists(path));
            var (next, nextReplaced, nextBackend) = await access.OpenAsync(home, CancellationToken.None);
            next.Dispose();
            Assert.True(nextReplaced);
            Assert.Equal(intruder.Identity, nextBackend);
            Assert.NotEqual(backend, nextBackend);
        }
    }

    [Fact]
    public async Task RemovedSocketFailsTheNextOpenAsMissing()
    {
        var home = Home();
        var first = Serve(home);
        var access = Access();
        var (connection, _, _) = await access.OpenAsync(home, CancellationToken.None);
        connection.Dispose();
        first.Dispose();
        var exception = await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        Assert.Equal(CodexTransportFailure.EndpointMissing, exception.Failure);
    }

    [Fact]
    public async Task ReplacementIsReportedAfterARestartEvenWhenAFailedAttemptHappenedInBetween()
    {
        var home = Home();
        var now = DateTimeOffset.UtcNow;
        using var restarted = ChildListener.Start(CodexSharedEndpoint.SocketPath(Home()));
        var peer = Environment.ProcessId;
        var access = new CodexSharedAccess(new(TimeSpan.Zero, () => now), Quick, CodexBackendCheck.Accepting(), _ => peer);
        var first = Serve(home);
        var (one, replaced, b1) = await access.OpenAsync(home, CancellationToken.None);
        one.Dispose();
        Assert.False(replaced);
        Assert.Equal(Environment.ProcessId, b1.Pid);

        first.Dispose();
        await Assert.ThrowsAsync<CodexTransportException>(() => access.OpenAsync(home, CancellationToken.None));
        now = now.AddSeconds(NativeNameSchedule.BackoffSeconds[0] + 1);

        peer = restarted.Identity.Pid;
        Serve(home);
        var (two, nowReplaced, b2) = await access.OpenAsync(home, CancellationToken.None);
        two.Dispose();
        Assert.True(nowReplaced);
        Assert.Equal(restarted.Identity, b2);

        var (three, again, _) = await access.OpenAsync(home, CancellationToken.None);
        three.Dispose();
        Assert.False(again);
    }

    [Fact]
    public async Task DisposingTheObserverLeavesTheBackendServing()
    {
        var home = Home();
        var server = Serve(home);
        var access = Access();
        var (keep, _, _) = await access.OpenAsync(home, CancellationToken.None);
        var (connection, _, _) = await access.OpenAsync(home, CancellationToken.None);
        using (keep)
        {
            connection.Dispose();
            await Task.Delay(100);
            Assert.True(server.Serving);
            var thread = await keep.CallAsync("thread/read", new { threadId = "two" }, CancellationToken.None);
            Assert.Equal("name-of-two", thread.GetProperty("thread").GetProperty("name").GetString());
        }
        var (again, _, _) = await access.OpenAsync(home, CancellationToken.None);
        again.Dispose();
    }

    [Fact]
    public async Task ConcurrentConnectionsDoNotShareState()
    {
        var first = Home();
        var second = Home();
        Serve(first);
        Serve(second);
        var access = Access();
        var (one, _, _) = await access.OpenAsync(first, CancellationToken.None);
        var (two, _, _) = await access.OpenAsync(second, CancellationToken.None);
        var (three, _, _) = await access.OpenAsync(first, CancellationToken.None);
        using (one)
        using (two)
        using (three)
        {
            var names = await Task.WhenAll(
                one.ReadNameAsync("slow", CancellationToken.None),
                two.ReadNameAsync("fast", CancellationToken.None),
                three.ReadNameAsync("third", CancellationToken.None));
            Assert.Equal("name-of-slow|name-of-fast|name-of-third", string.Join("|", names));
        }
    }

    [Fact]
    public async Task OwnedLoopbackTransportKeepsAuthenticatedBehavior()
    {
        using var server = new CodexStandInServer(null, "ignored");
        using var connection = new CodexConnection();
        await connection.ConnectAsync("ws://127.0.0.1:" + server.Port, "secret-token", CancellationToken.None);
        Assert.Equal("Bearer secret-token", server.Authorization);
        Assert.Equal("name-of-one", await connection.ReadNameAsync("one", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.ConnectAsync("ws://127.0.0.1:" + server.Port, "x", CancellationToken.None));
    }
}

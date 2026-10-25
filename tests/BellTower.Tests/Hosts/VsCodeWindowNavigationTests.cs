using System.Diagnostics;
using System.Runtime.InteropServices;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class VsCodeWindowNavigationTests
{
    private const long WindowA = 1001;
    private const long WindowB = 1002;

    private sealed class Desktop : IWindowActivationApi, IWindowInspector
    {
        public Dictionary<long, WindowFacts> Windows { get; } = [];
        public HashSet<long> Minimized { get; } = [];
        public List<string> Calls { get; } = [];
        public long Foreground { get; set; }
        public bool AllowForeground { get; set; } = true;
        public bool LoseForegroundAfterActivation { get; set; }

        public bool IsMinimized(IntPtr window) => Minimized.Contains(window.ToInt64());
        public void Restore(IntPtr window)
        {
            Calls.Add("restore:" + window);
            Minimized.Remove(window.ToInt64());
        }

        public bool SetForeground(IntPtr window)
        {
            Calls.Add("foreground:" + window);
            if (AllowForeground)
            {
                Foreground = window.ToInt64();
            }

            return AllowForeground;
        }

        public bool IsForeground(IntPtr window) => Foreground == window.ToInt64();
        public WindowFacts? Inspect(long handle) => Windows.GetValueOrDefault(handle);
        WindowFacts? IWindowInspector.Foreground() => Windows.GetValueOrDefault(Foreground);
    }

    private sealed class Fixture : IDisposable
    {
        public TestWorkspace Workspace { get; } = new();
        public Desktop Desktop { get; } = new();
        public ProcessIdentity Self { get; } = ProcessProbe.Identify(Environment.ProcessId)!;
        public List<FocusRequest> Requests { get; } = [];
        public bool Reveal { get; set; } = true;
        public bool Respond { get; set; } = true;
        private readonly CancellationTokenSource stop = new();
        private readonly HashSet<string> seen = [];
        private readonly Task bridge;
        private readonly List<Process> children = [];

        public Fixture()
        {
            Desktop.Windows[WindowA] = new(WindowA, Self.Pid, VsCodeWindowOwnership.EditorWindowClass, true);
            Desktop.Windows[WindowB] = new(WindowB, Self.Pid, VsCodeWindowOwnership.EditorWindowClass, true);
            bridge = Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    foreach (var file in Directory.EnumerateFiles(Workspace.Paths.Requests, "*.json"))
                    {
                        FocusRequest? request;
                        try
                        {
                            request = AtomicFile.Read<FocusRequest>(file);
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                        {
                            continue; // the tray is removing it
                        }

                        if (request is null || !seen.Add(request.Id))
                        {
                            continue;
                        }

                        Requests.Add(request);
                        if (!Respond)
                        {
                            // A bridge that never answers in time: the request stays until the tray removes it.
                            continue;
                        }

                        AtomicFile.Write(Path.Combine(Workspace.Paths.Requests, request.Id + ".result"),
                            BellTower.IO.Json.Encode(new FocusResult(Reveal, "exact terminal", Reveal ? "revealed" : "not revealed")));
                        File.Delete(file);
                    }

                    await Task.Delay(10);
                }
            });
        }

        public VsCodeNavigator Navigator(bool ancestry = true, Func<DateTimeOffset>? clock = null) => new(Workspace.Paths,
            new(Desktop, ProcessProbe.Identify, _ => ancestry ? [Self.Pid] : []), new(Desktop), _ => Task.CompletedTask, clock);

        public VsCodeNavigator Navigator(IWindowInspector windows, IWindowActivationApi activation, string editorClass) => new(Workspace.Paths,
            new(windows, ProcessProbe.Identify, _ => [Self.Pid], editorClass), new(activation), _ => Task.CompletedTask);

        public Process Shell()
        {
            var process = Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
            children.Add(process);
            return process;
        }

        public void Register(string instance, IEnumerable<int> terminals, long? window = null, int? protocol = 2, ProcessIdentity? owner = null,
            TimeSpan? age = null, string? className = null, string? unavailable = null)
        {
            var evidence = window is { } handle ? new WindowEvidence(handle, owner ?? Self, className ?? VsCodeWindowOwnership.EditorWindowClass, DateTimeOffset.UtcNow) : null;
            AtomicFile.Write(Path.Combine(Workspace.Paths.Hosts, instance + ".json"), BellTower.IO.Json.Encode(
                new HostRegistration(instance, Self, DateTimeOffset.UtcNow - (age ?? TimeSpan.Zero), terminals.ToList(), protocol, evidence, unavailable)));
        }

        public Attachment Attach(string provider, Process shell)
        {
            var identity = ProcessProbe.Identify(shell.Id)!;
            return new(provider + "-" + shell.Id, provider, Self, new("vscode", identity), Workspace.Root, DateTimeOffset.UtcNow);
        }

        public void Dispose()
        {
            stop.Cancel();
            bridge.GetAwaiter().GetResult();
            foreach (var child in children)
            {
                try
                {
                    child.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Already exited.
                }

                child.Dispose();
            }

            Workspace.Dispose();
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task VerifiedWindowIsActivatedThenTheExactTerminalIsRevealed(string provider)
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);
        var attachment = fixture.Attach(provider, shell);
        var navigator = fixture.Navigator();
        Assert.Equal("exact terminal", navigator.Precision(attachment));

        var result = await navigator.FocusAsync(attachment);

        Assert.True(result.Success, result.Message);
        Assert.Equal(["foreground:" + WindowA], fixture.Desktop.Calls);
        var request = Assert.Single(fixture.Requests);
        Assert.Equal("window-a", request.InstanceId);
        Assert.Equal(2, request.Protocol);
        Assert.Equal("reveal", request.Mode);
        Assert.Equal(shell.Id, request.ShellPid);
    }

    [Fact]
    public async Task MixedClaudeAndCodexSessionsInDifferentWindowsNeverCrossRoutes()
    {
        using var fixture = new Fixture();
        var claudeShell = fixture.Shell();
        var codexShell = fixture.Shell();
        fixture.Register("window-a", [claudeShell.Id], WindowA);
        fixture.Register("window-b", [codexShell.Id], WindowB);
        var navigator = fixture.Navigator();

        Assert.True((await navigator.FocusAsync(fixture.Attach("claude", claudeShell))).Success);
        Assert.True((await navigator.FocusAsync(fixture.Attach("codex", codexShell))).Success);
        Assert.True((await navigator.FocusAsync(fixture.Attach("claude", claudeShell))).Success);

        Assert.Equal(["foreground:" + WindowA, "foreground:" + WindowB, "foreground:" + WindowA], fixture.Desktop.Calls);
        Assert.Equal(["window-a", "window-b", "window-a"], fixture.Requests.Select(request => request.InstanceId));
        Assert.Equal([claudeShell.Id, codexShell.Id, claudeShell.Id], fixture.Requests.Select(request => request.ShellPid));
    }

    [Fact]
    public async Task VisibleWindowIsNeverRestoredButAMinimizedOneIsRestoredOnce()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);
        var attachment = fixture.Attach("codex", shell);

        Assert.True((await fixture.Navigator().FocusAsync(attachment)).Success);
        Assert.DoesNotContain(fixture.Desktop.Calls, call => call.StartsWith("restore", StringComparison.Ordinal));

        fixture.Desktop.Foreground = 0;
        fixture.Desktop.Minimized.Add(WindowA);
        Assert.True((await fixture.Navigator().FocusAsync(attachment)).Success);
        Assert.Single(fixture.Desktop.Calls, call => call == "restore:" + WindowA);
    }

    [Fact]
    public async Task DeniedActivationFailsHonestlyButStillRevealsTheTerminal()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);
        fixture.Desktop.AllowForeground = false;

        var result = await fixture.Navigator().FocusAsync(fixture.Attach("claude", shell));

        Assert.False(result.Success);
        Assert.Contains("foreground", result.Message);
        Assert.Single(fixture.Requests);
    }

    [Fact]
    public async Task TerminalRevealFailureIsNotSuccessEvenWhenTheWindowIsForeground()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);
        fixture.Reveal = false;

        var result = await fixture.Navigator().FocusAsync(fixture.Attach("codex", shell));

        Assert.False(result.Success);
        Assert.Equal(WindowA, fixture.Desktop.Foreground);
    }

    [Fact]
    public async Task LegacyBridgeRegistrationIsPartialAndNeverActivatesAWindow()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], protocol: null);
        var attachment = fixture.Attach("claude", shell);
        var navigator = fixture.Navigator();

        Assert.Equal("terminal only", navigator.Precision(attachment));
        var result = await navigator.FocusAsync(attachment);

        Assert.False(result.Success);
        Assert.Contains("older Bell Tower bridge", result.Message);
        Assert.Empty(fixture.Desktop.Calls);
    }

    [Fact]
    public async Task RegistrationWithoutObservedWindowFailsSafely()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], window: null);

        var result = await fixture.Navigator().FocusAsync(fixture.Attach("codex", shell));

        Assert.False(result.Success);
        Assert.Contains("Focus it once", result.Message);
        Assert.Empty(fixture.Desktop.Calls);
    }

    [Fact]
    public async Task TwoBridgesClaimingTheSameWindowAreBothRefused()
    {
        using var fixture = new Fixture();
        var first = fixture.Shell();
        var second = fixture.Shell();
        fixture.Register("window-a", [first.Id], WindowA);
        fixture.Register("window-b", [second.Id], WindowA);
        var navigator = fixture.Navigator();

        foreach (var (provider, shell) in new[] { ("claude", first), ("codex", second) })
        {
            var result = await navigator.FocusAsync(fixture.Attach(provider, shell));
            Assert.False(result.Success);
            Assert.Contains("More than one", result.Message);
        }

        Assert.Empty(fixture.Desktop.Calls);
    }

    [Fact]
    public async Task OwnerProcessReuseOrAWindowOfAnotherProcessIsRefused()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        var attachment = fixture.Attach("claude", shell);

        // The recorded owner start time no longer matches the live process (PID reuse).
        fixture.Register("window-a", [shell.Id], WindowA, owner: fixture.Self with { StartedUtcTicks = fixture.Self.StartedUtcTicks + 1 });
        Assert.False((await fixture.Navigator().FocusAsync(attachment)).Success);

        // The handle now belongs to another process.
        fixture.Register("window-a", [shell.Id], WindowA);
        fixture.Desktop.Windows[WindowA] = new(WindowA, fixture.Self.Pid + 1, VsCodeWindowOwnership.EditorWindowClass, true);
        Assert.False((await fixture.Navigator().FocusAsync(attachment)).Success);

        // The window has been destroyed.
        fixture.Desktop.Windows.Remove(WindowA);
        Assert.False((await fixture.Navigator().FocusAsync(attachment)).Success);
        Assert.Empty(fixture.Desktop.Calls);
    }

    [Fact]
    public async Task WindowOutsideTheBridgeProcessLineageIsRefused()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);

        var result = await fixture.Navigator(ancestry: false).FocusAsync(fixture.Attach("codex", shell));

        Assert.False(result.Success);
        Assert.Empty(fixture.Desktop.Calls);
    }

    [Fact]
    public async Task ExpiredRegistrationIsNeverUsed()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA, age: TimeSpan.FromSeconds(30));
        var attachment = fixture.Attach("claude", shell);
        var navigator = fixture.Navigator();

        Assert.Equal("unavailable", navigator.Precision(attachment));
        Assert.False((await navigator.FocusAsync(attachment)).Success);
        Assert.Empty(fixture.Requests);
        Assert.Empty(fixture.Desktop.Calls);
    }

    [Fact]
    public async Task OlderBridgeRegistrationsStayReadable()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        // The exact JSON an older bridge writes: no protocol or window members.
        File.WriteAllText(Path.Combine(fixture.Workspace.Paths.Hosts, "old.json"), $$"""
            {"instanceId":"old","process":{"pid":{{fixture.Self.Pid}},"startedUtcTicks":"{{fixture.Self.StartedUtcTicks}}"},
             "updatedAt":"{{DateTimeOffset.UtcNow:O}}","terminalPids":[{{shell.Id}}]}
            """);

        var navigator = fixture.Navigator();
        Assert.Equal("terminal only", navigator.Precision(fixture.Attach("claude", shell)));
        Assert.False((await navigator.FocusAsync(fixture.Attach("codex", shell))).Success);
        Assert.Single(fixture.Requests);
    }

    // ---- Implementation 002 ----

    [Fact]
    public async Task RequestNamesTheActivatedWindowOnlyWhenActivationWasConfirmed()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);
        var attachment = fixture.Attach("claude", shell);

        Assert.True((await fixture.Navigator().FocusAsync(attachment)).Success);
        Assert.Equal(WindowA, fixture.Requests[0].WindowHandle);

        fixture.Desktop.Foreground = 0;
        fixture.Desktop.AllowForeground = false;
        Assert.False((await fixture.Navigator().FocusAsync(attachment)).Success);
        Assert.Null(fixture.Requests[1].WindowHandle);

        // An unverified window is never named, so a legacy or unverified request cannot clear a record.
        fixture.Register("window-a", [shell.Id], protocol: null);
        await fixture.Navigator().FocusAsync(attachment);
        Assert.Null(fixture.Requests[2].WindowHandle);
    }

    [Fact]
    public void RegistrationSnapshotIsReusedForOneTickAndNeverPastItsFreshness()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        var attachment = fixture.Attach("codex", shell);
        var clock = DateTimeOffset.UtcNow;
        var navigator = fixture.Navigator(clock: () => clock);
        fixture.Register("window-a", [shell.Id], WindowA);
        Assert.Equal("exact terminal", navigator.Precision(attachment));

        // Within a tick the snapshot is reused: removing the file is not seen yet.
        File.Delete(Path.Combine(fixture.Workspace.Paths.Hosts, "window-a.json"));
        clock = clock.AddMilliseconds(100);
        Assert.Equal("exact terminal", navigator.Precision(attachment));

        // After the snapshot lifetime the files are read again.
        clock = clock.AddMilliseconds(150);
        Assert.Equal("unavailable", navigator.Precision(attachment));

        // A cached registration is re-checked against the 15 second freshness on every use.
        fixture.Register("window-a", [shell.Id], WindowA, age: TimeSpan.FromMilliseconds(14_900));
        clock = DateTimeOffset.UtcNow;
        var other = fixture.Navigator(clock: () => clock);
        Assert.Equal("exact terminal", other.Precision(attachment));
        clock = clock.AddMilliseconds(150);
        Assert.Equal("unavailable", other.Precision(attachment));
    }

    [Fact]
    public void RegistrationReaderCoexistsWithAWriterThatHoldsTheFile()
    {
        using var fixture = new Fixture();
        var file = Path.Combine(fixture.Workspace.Paths.Hosts, "window-a.json");
        fixture.Register("window-a", [1], WindowA);
        // File.ReadAllText (the shared AtomicFile.Read) cannot open a file a writer holds; the scoped reader can.
        using var writer = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        Assert.Throws<IOException>(() => File.ReadAllText(file));
        Assert.Equal("window-a", VsCodeNavigator.ReadRegistration(file)!.InstanceId);
    }

    [Fact]
    public async Task ReceiptAndRequestAreCleanedOnReadAndOnTimeout()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);
        var attachment = fixture.Attach("claude", shell);
        var requests = fixture.Workspace.Paths.Requests;

        // A bridge that answers but fails to remove the request: the tray removes both files on read.
        fixture.Respond = false;
        var focus = fixture.Navigator().FocusAsync(attachment);
        string? pending = null;
        for (var i = 0; i < 200 && pending is null; i++)
        {
            pending = Directory.EnumerateFiles(requests, "*.json").FirstOrDefault();
            await Task.Delay(10);
        }
        Assert.NotNull(pending);
        AtomicFile.Write(Path.ChangeExtension(pending!, ".result"), BellTower.IO.Json.Encode(new FocusResult(true, "exact terminal", "revealed")));
        await focus;
        Assert.True(Directory.GetFiles(requests).Length == 0, string.Join(",", Directory.GetFiles(requests).Select(Path.GetFileName)));

        // A bridge that never answers: the request expires and nothing is left behind.
        var tick = DateTimeOffset.UtcNow;
        var timedOut = await fixture.Navigator(clock: () => tick = tick.AddSeconds(1)).FocusAsync(attachment);
        Assert.False(timedOut.Success);
        Assert.True(Directory.GetFiles(requests).Length == 0, string.Join(",", Directory.GetFiles(requests).Select(Path.GetFileName)));
    }

    [Fact]
    public async Task OldOrphanedReceiptsAreSweptWhileRecentOnesAreKept()
    {
        using var fixture = new Fixture();
        var shell = fixture.Shell();
        fixture.Register("window-a", [shell.Id], WindowA);
        var requests = fixture.Workspace.Paths.Requests;
        var old = Path.Combine(requests, new string('a', 32) + ".result");
        var recent = Path.Combine(requests, new string('b', 32) + ".result");
        File.WriteAllText(old, "{}");
        File.WriteAllText(recent, "{}");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow - VsCodeNavigator.ReceiptSweepAge - TimeSpan.FromSeconds(30));
        Assert.True(VsCodeNavigator.ReceiptSweepAge > TimeSpan.FromSeconds(5));

        Assert.True((await fixture.Navigator().FocusAsync(fixture.Attach("codex", shell))).Success);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public async Task DoctorReportsWindowVerificationEquallyForClaudeAndCodex()
    {
        using var fixture = new Fixture();
        var verifiedA = fixture.Shell();
        var verifiedB = fixture.Shell();
        var partial = fixture.Shell();
        var unverified = fixture.Shell();
        var dead = fixture.Shell();
        var deadClaude = fixture.Attach("claude", dead) with { Id = "claude-dead" };
        var deadCodex = fixture.Attach("codex", dead) with { Id = "codex-dead" };
        dead.Kill();
        dead.WaitForExit();
        fixture.Register("window-a", [verifiedA.Id, verifiedB.Id], WindowA);
        fixture.Register("window-old", [partial.Id], protocol: null);
        fixture.Register("window-new", [unverified.Id], window: null);
        var store = new AttachmentStore(fixture.Workspace.Paths);
        store.Save(deadClaude);
        store.Save(deadCodex);
        foreach (var (provider, shell) in new[] { ("claude", verifiedA), ("codex", verifiedB), ("claude", partial), ("codex", partial), ("claude", unverified), ("codex", unverified) })
        {
            store.Save(fixture.Attach(provider, shell) with { Id = provider + "-" + shell.Id });
        }

        var report = new BellTower.Diagnostics.VsCodeBridgeReporter(fixture.Workspace.Paths, fixture.Navigator());
        using var json = System.Text.Json.JsonDocument.Parse(BellTower.IO.Json.Encode(report.Report()));
        var windows = json.RootElement.GetProperty("windows").EnumerateArray().ToDictionary(w => w.GetProperty("terminals").GetInt32() + ":" + w.GetProperty("state").GetString());
        Assert.Contains("2:verified", windows.Keys);
        Assert.Contains("1:partial", windows.Keys);
        Assert.Contains("1:unverified", windows.Keys);
        Assert.Contains("older Bell Tower bridge", windows["1:partial"].GetProperty("reason").GetString());
        Assert.Contains("seen in the foreground", windows["1:unverified"].GetProperty("reason").GetString());

        var sessions = json.RootElement.GetProperty("sessions").EnumerateArray()
            .Select(s => (Provider: s.GetProperty("provider").GetString()!, Navigation: s.GetProperty("navigation").GetString()!, Reason: s.GetProperty("reason").GetString()!)).ToList();
        foreach (var (claude, codex) in new[] { ("claude", "codex") })
        {
            // Matched providers on the same destination get the same state and reason.
            var a = sessions.Where(s => s.Provider == claude).Select(s => (s.Navigation, s.Reason)).Order().ToList();
            var b = sessions.Where(s => s.Provider == codex).Select(s => (s.Navigation, s.Reason)).Order().ToList();
            Assert.Equal(4, a.Count);
            Assert.Equal(4, b.Count);
            // A dead shell is an ended session, not a registration miss, for both providers.
            Assert.Equal(1, a.Count(x => x.Reason == BellTower.Diagnostics.VsCodeBridgeReporter.EndedReason));
            Assert.Equal(1, b.Count(x => x.Reason == BellTower.Diagnostics.VsCodeBridgeReporter.EndedReason));
            Assert.Equal(a.Where(x => x.Navigation == "terminal only").Select(x => x.Reason), b.Where(x => x.Navigation == "terminal only").Select(x => x.Reason));
            Assert.Equal(1, a.Count(x => x.Navigation == "exact terminal"));
            Assert.Equal(1, b.Count(x => x.Navigation == "exact terminal"));
        }
        // Stable shape: arrays and a null error.
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.GetProperty("windows").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.GetProperty("sessions").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("error").ValueKind);
        await Task.CompletedTask;
    }

    [Fact]
    public void DoctorKeepsArrayTypesAndSetsErrorWhenRegistrationsCannotBeRead()
    {
        using var fixture = new Fixture();
        var navigator = fixture.Navigator();
        Directory.Delete(fixture.Workspace.Paths.Hosts, recursive: true);
        using var json = System.Text.Json.JsonDocument.Parse(BellTower.IO.Json.Encode(
            new BellTower.Diagnostics.VsCodeBridgeReporter(fixture.Workspace.Paths, navigator).Report()));
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.GetProperty("windows").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.GetProperty("sessions").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.String, json.RootElement.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task MultipleWindowsGuardReadsAsReloadRecoveryForClaudeAndCodexInNavigationAndDoctor()
    {
        using var fixture = new Fixture();
        var claudeShell = fixture.Shell();
        var codexShell = fixture.Shell();
        var plain = fixture.Shell();
        fixture.Register("window-multi", [claudeShell.Id, codexShell.Id], window: null, unavailable: "multiple");
        fixture.Register("window-new", [plain.Id], window: null);
        var navigator = fixture.Navigator();
        var store = new AttachmentStore(fixture.Workspace.Paths);

        foreach (var (provider, shell) in new[] { ("claude", claudeShell), ("codex", codexShell) })
        {
            var attachment = fixture.Attach(provider, shell);
            store.Save(attachment);
            Assert.Equal("terminal only", navigator.Precision(attachment));
            var result = await navigator.FocusAsync(attachment);
            Assert.False(result.Success);
            Assert.Contains("Developer: Reload Window", result.Message);
            Assert.Contains("More than one top-level window", result.Message);
            Assert.Empty(fixture.Desktop.Calls);
        }

        // A protocol 2 registration without the field is still just "not yet observed".
        var fresh = await navigator.FocusAsync(fixture.Attach("claude", plain));
        Assert.Contains("seen in the foreground", fresh.Message);
        Assert.DoesNotContain("Reload Window", fresh.Message);

        using var json = System.Text.Json.JsonDocument.Parse(BellTower.IO.Json.Encode(
            new BellTower.Diagnostics.VsCodeBridgeReporter(fixture.Workspace.Paths, navigator).Report()));
        var sessions = json.RootElement.GetProperty("sessions").EnumerateArray().ToList();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, session => Assert.Contains("Developer: Reload Window", session.GetProperty("reason").GetString()));
        Assert.Equal(["claude", "codex"], sessions.Select(session => session.GetProperty("provider").GetString()!).Order());
        var windows = json.RootElement.GetProperty("windows").EnumerateArray().ToList();
        Assert.Contains(windows, w => w.GetProperty("state").GetString() == "unverified" && w.GetProperty("reason").GetString()!.Contains("Reload Window"));
    }

    // ---- Native disposable-window tests (QA-001) ----
    // The windows are created by this test process and are transparent, non-activating and unrelated to any editor.
    // SetForegroundWindow is deliberately NOT called on them: whether Windows grants the foreground depends on the
    // session's foreground lock, so the foreground request is substituted by a recorded denial. Restore, show state,
    // handle/owner mapping and destruction run against the real Win32 APIs. Real foreground grant is live task 4.2.

    private sealed class DisposableWindow : IDisposable
    {
        private sealed class QuietForm : Form
        {
            protected override bool ShowWithoutActivation => true;
            protected override CreateParams CreateParams
            {
                get
                {
                    var parameters = base.CreateParams;
                    parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                    return parameters;
                }
            }
        }

        private readonly QuietForm form = new() { Opacity = 0, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), Size = new(200, 120) };
        private readonly Thread thread;
        public long Handle { get; private set; }

        public DisposableWindow()
        {
            using var ready = new ManualResetEventSlim();
            form.Shown += (_, _) =>
            {
                Handle = form.Handle.ToInt64();
                ready.Set();
            };
            thread = new Thread(() => Application.Run(form)) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "the disposable window did not show");
        }

        public void Do(Action<Form> action) => form.Invoke(() => action(form));
        public T Get<T>(Func<Form, T> read) => (T)form.Invoke(() => read(form))!;
        public bool Exists => thread.IsAlive;

        public void Destroy()
        {
            if (thread.IsAlive)
            {
                form.Invoke(form.Close);
                thread.Join(TimeSpan.FromSeconds(5));
            }
        }

        public void Dispose() => Destroy();
    }

    private sealed class GuardedActivation : IWindowActivationApi
    {
        private readonly NativeWindowActivationApi native = new();
        public int Restores { get; private set; }
        public int ForegroundRequests { get; private set; }
        public bool IsMinimized(IntPtr window) => native.IsMinimized(window);
        public void Restore(IntPtr window)
        {
            Restores++;
            native.Restore(window);
        }

        // Substituted denial: never takes the foreground from the person running the tests.
        public bool SetForeground(IntPtr window)
        {
            ForegroundRequests++;
            return false;
        }

        public bool IsForeground(IntPtr window) => native.IsForeground(window);
    }

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [Fact]
    public void NativeInspectorMapsHandleToOwnerAndClassAndReportsADestroyedHandle()
    {
        using var window = new DisposableWindow();
        var inspector = new NativeWindowInspector();
        var facts = inspector.Inspect(window.Handle);
        Assert.NotNull(facts);
        Assert.Equal(Environment.ProcessId, facts!.OwnerPid);
        Assert.False(string.IsNullOrEmpty(facts.ClassName));
        Assert.True(facts.Visible);

        var shell = GetShellWindow().ToInt64();
        Assert.NotEqual(Environment.ProcessId, inspector.Inspect(shell)?.OwnerPid);

        window.Destroy();
        Assert.Null(inspector.Inspect(window.Handle));
    }

    [Fact]
    public void OwnershipVerifiesARealWindowAndRefusesAForeignOrDestroyedOne()
    {
        using var fixture = new Fixture();
        using var window = new DisposableWindow();
        var self = fixture.Self;
        var cls = new NativeWindowInspector().Inspect(window.Handle)!.ClassName;
        var ownership = new VsCodeWindowOwnership(new NativeWindowInspector(), ProcessProbe.Identify, _ => [self.Pid], cls);
        HostRegistration Registration(long handle, ProcessIdentity owner, string instance = "native") =>
            new(instance, self, DateTimeOffset.UtcNow, [1], 2, new(handle, owner, cls, DateTimeOffset.UtcNow));

        var good = Registration(window.Handle, self);
        Assert.True(ownership.Verify(good, [good], true).Verified);

        // A real window of another process (the shell window) is refused even with the right class claimed.
        var shell = GetShellWindow().ToInt64();
        var shellFacts = new NativeWindowInspector().Inspect(shell)!;
        var foreign = new HostRegistration("native", self, DateTimeOffset.UtcNow, [1], 2, new(shell, self, shellFacts.ClassName, DateTimeOffset.UtcNow));
        Assert.False(new VsCodeWindowOwnership(new NativeWindowInspector(), ProcessProbe.Identify, _ => [self.Pid], shellFacts.ClassName).Verify(foreign, [foreign], true).Verified);

        // The wrong class is refused.
        var wrongClass = new VsCodeWindowOwnership(new NativeWindowInspector(), ProcessProbe.Identify, _ => [self.Pid]);
        Assert.False(wrongClass.Verify(good, [good], true).Verified);

        window.Destroy();
        Assert.False(ownership.Verify(good, [good], true).Verified);
    }

    [Fact]
    public void RealMinimizedWindowIsRestoredAndVisibleMaximizedAndFullscreenWindowsKeepTheirShowState()
    {
        using var window = new DisposableWindow();
        var handle = new IntPtr(window.Handle);

        var minimized = new GuardedActivation();
        window.Do(f => f.WindowState = FormWindowState.Minimized);
        Assert.True(IsIconic(handle));
        new WindowActivator(minimized).Activate(handle);
        Assert.Equal(1, minimized.Restores);
        Assert.False(IsIconic(handle));

        var maximized = new GuardedActivation();
        window.Do(f => f.WindowState = FormWindowState.Maximized);
        Assert.True(IsZoomed(handle));
        new WindowActivator(maximized).Activate(handle);
        Assert.Equal(0, maximized.Restores);
        Assert.True(IsZoomed(handle));
        Assert.False(IsIconic(handle));

        var fullscreen = new GuardedActivation();
        window.Do(f =>
        {
            f.WindowState = FormWindowState.Normal;
            f.FormBorderStyle = FormBorderStyle.None;
            f.Bounds = Screen.PrimaryScreen!.Bounds;
        });
        var before = window.Get(f => f.Bounds);
        new WindowActivator(fullscreen).Activate(handle);
        Assert.Equal(0, fullscreen.Restores);
        Assert.False(IsIconic(handle));
        Assert.Equal(before, window.Get(f => f.Bounds));
    }

    [Fact]
    public async Task RealWindowWithDeniedForegroundGivesAFailureReceiptAndStillRevealsTheTerminal()
    {
        using var fixture = new Fixture();
        using var window = new DisposableWindow();
        var shell = fixture.Shell();
        var inspector = new NativeWindowInspector();
        var cls = inspector.Inspect(window.Handle)!.ClassName;
        fixture.Register("native-window", [shell.Id], window.Handle, className: cls);
        var guarded = new GuardedActivation();
        var navigator = fixture.Navigator(inspector, guarded, cls);
        var attachment = fixture.Attach("codex", shell);

        Assert.Equal("exact terminal", navigator.Precision(attachment));
        var result = await navigator.FocusAsync(attachment);

        Assert.False(result.Success);
        Assert.Contains("foreground", result.Message);
        Assert.Equal(1, guarded.ForegroundRequests);
        var request = Assert.Single(fixture.Requests);
        Assert.Null(request.WindowHandle);

        // Once the window is destroyed the mapping is refused and nothing is activated.
        window.Destroy();
        var again = await navigator.FocusAsync(attachment);
        Assert.False(again.Success);
        Assert.Equal(1, guarded.ForegroundRequests);
        Assert.Equal("terminal only", navigator.Precision(attachment));
    }
}

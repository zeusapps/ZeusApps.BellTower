using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using BellTower.Core;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Providers;
using BellTower.Tests.Testing;
using BellTower.Tray;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Hosts;

// Task 3.3: every Bell Tower management action disposes only its own observer connections and stops only what it provably
// owns. The cohort holds the independent processes a user has (Claude and Codex clients, their shell, a shared and a private
// Codex backend) next to one owned runtime-host manager. Each action runs as the real bell-tower process against an isolated
// data directory and home; the assertion is the identity (PID plus creation time) of every process afterwards.
public sealed class ManagementOwnershipProcessTests : IDisposable
{
    private readonly TestWorkspace workspace = new();
    private readonly ManagementCohort cohort;
    private readonly string codexHome;

    public ManagementOwnershipProcessTests()
    {
        cohort = new ManagementCohort(workspace);
        // The native rows point the isolated tray at the private backend's home, so the real backend is observed.
        codexHome = NativeCodexHome ?? Directory.CreateDirectory(Path.Combine(workspace.Root, "codex-home")).FullName;
    }

    private static string? NativeCodexHome => Environment.GetEnvironmentVariable("BELLTOWER_NATIVE_CODEX_HOME") is { Length: > 0 } home ? home : null;

    public void Dispose()
    {
        cohort.Dispose();
        StandIns.KillUnder(workspace.Root);
        workspace.Dispose();
    }

    private Attachment Register(string id, string provider, ProcessIdentity? process, string mode)
    {
        var host = new HostTarget("unknown", process);
        var attachment = new Attachment(id, provider, process, host, workspace.Root, DateTimeOffset.UtcNow);
        if (mode == "shared")
        {
            attachment = NativeAdoption.Shared(new Attachment(id, provider, null, new("unknown"), workspace.Root, DateTimeOffset.UtcNow), "shared-session", AgentModes.SharedBackend);
        }
        new AttachmentStore(workspace.Paths).Save(attachment);
        return attachment;
    }

    private static (int Code, string Output, string Error) Run(TestWorkspace w, string[] args, string? codexHome = null)
    {
        var start = new ProcessStartInfo(BuiltApplication.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        StandIns.ScrubEnvironment(start);
        if (codexHome is not null)
        {
            start.Environment["CODEX_HOME"] = codexHome;
        }
        start.ArgumentList.Add("--data-dir");
        start.ArgumentList.Add(w.Paths.Root);
        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(60000), "bell-tower did not finish.");
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    // Matched Claude and Codex rows; the mixed rows share one cohort in the same test run.
    [Theory]
    [InlineData("claude", "direct")]
    [InlineData("codex", "direct")]
    [InlineData("codex", "shared")]
    [InlineData("codex", "owned")]
    public void DetachEndsMetadataAndSignalsNoProcess(string provider, string kind)
    {
        var process = provider == "claude" ? cohort.ClaudeClient : kind == "owned" ? cohort.Shell : cohort.CodexClient;
        var id = kind == "owned" ? ManagementCohort.OwnedAttachment : "detach-" + provider + "-" + kind;
        id = Register(id, provider, kind == "shared" ? null : process, kind).Id;

        var result = Run(workspace, ["detach", "--attachment", id]);

        Assert.True(result.Code == 0, result.Error);
        Assert.NotNull(new AttachmentStore(workspace.Paths).Get(id)!.EndedAt);
        cohort.AssertIndependentSurvive("detach");
        // Detach never stops an owned runtime: its shell requests that once the TUI exits, so the marker is absent too.
        Assert.True(ManagementCohort.IsLive(cohort.OwnedManager));
        Assert.False(File.Exists(new CodexRuntimeStore(workspace.Paths).StopPath(ManagementCohort.OwnedAttachment)));
    }

    [Fact]
    public void StoppingTheOwnedRuntimeEndsOnlyItsManager()
    {
        // The wrapper's request after the owned TUI exits: only the manager Bell Tower started is asked to stop.
        var result = Run(workspace, ["stop-codex-runtime", "--attachment", ManagementCohort.OwnedAttachment]);

        Assert.True(result.Code == 0, result.Error);
        Assert.True(cohort.WaitForEnd(cohort.OwnedManager, TimeSpan.FromSeconds(20)), "The owned manager did not stop.");
        cohort.AssertIndependentSurvive("stop-codex-runtime");
        var store = new CodexRuntimeStore(workspace.Paths);
        Assert.False(File.Exists(store.RecordPath(ManagementCohort.OwnedAttachment)));
        Assert.False(File.Exists(store.TokenPath(ManagementCohort.OwnedAttachment)));
    }

    [Theory]
    [InlineData("claude-direct")]
    [InlineData("codex-direct")]
    [InlineData("codex-shared")]
    [InlineData("never-registered")]
    public void StoppingAnAttachmentWithoutAnOwnedRuntimeDoesNothing(string attachment)
    {
        var result = Run(workspace, ["stop-codex-runtime", "--attachment", attachment]);

        Assert.True(result.Code == 0, result.Error);
        Assert.False(File.Exists(new CodexRuntimeStore(workspace.Paths).StopPath(attachment)));
        Assert.True(ManagementCohort.IsLive(cohort.OwnedManager));
        cohort.AssertIndependentSurvive("stop-codex-runtime");
    }

    // Stale and hostile records: a record whose manager is a reused PID, the exact identity of an independent backend or the
    // user's shell must produce a marker file at most, never a signal.
    [Theory]
    [InlineData("backend-reused-pid")]
    [InlineData("backend-exact")]
    [InlineData("shell-exact")]
    [InlineData("client-exact")]
    public void AStaleOrForeignRecordNeverTerminatesTheProcessItNames(string shape)
    {
        var (target, manager) = shape switch
        {
            "backend-reused-pid" => (cohort.SharedBackend, cohort.SharedBackend with { StartedUtcTicks = cohort.SharedBackend.StartedUtcTicks - 1 }),
            "backend-exact" => (cohort.PrivateBackend, cohort.PrivateBackend),
            "shell-exact" => (cohort.Shell, cohort.Shell),
            _ => (cohort.CodexClient, cohort.CodexClient)
        };
        var attachment = "stale-" + shape;
        cohort.WriteRecord(cohort.OwnedRecord(manager, attachment));

        var result = Run(workspace, ["stop-codex-runtime", "--attachment", attachment]);

        Assert.True(result.Code == 0, result.Error);
        Assert.True(ManagementCohort.IsLive(target));
        cohort.AssertIndependentSurvive("stop-codex-runtime with a stale record");
        Assert.True(ManagementCohort.IsLive(cohort.OwnedManager));
        // A reused PID is not the manager: the record is recognized as dead and cleaned up without waiting.
        if (shape == "backend-reused-pid")
        {
            Assert.False(File.Exists(new CodexRuntimeStore(workspace.Paths).RecordPath(attachment)));
        }
    }

    private const string HostAttachment = "real-owned";

    // A live stand-in backend for the production manager: a .cmd (the non-native path, run through PowerShell) that stays up and
    // starts a grandchild from its own image, so the backend's descendants are observable.
    private (string Script, string Grandchild) CreateLiveBackend()
    {
        var grandchild = StandIns.CopyImage(workspace, "grandchild", "grandchild", "grandchild.exe");
        var script = Path.Combine(workspace.Root, "live-backend.cmd");
        File.WriteAllText(script,
            "@echo off\r\n" +
            $"start \"\" /b \"{grandchild}\" /s /c \"ping -n 180 127.0.0.1 >nul\"\r\n" +
            "ping -n 180 127.0.0.1 >nul\r\n");
        return (script, grandchild);
    }

    private Process StartImage(string image, string command)
    {
        var start = new ProcessStartInfo(image)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = StandIns.Under("", command)
        };
        StandIns.ScrubEnvironment(start);
        var process = Process.Start(start)!;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private Process StartHost(string executable, Action<ProcessStartInfo>? configure = null)
    {
        var start = new ProcessStartInfo(BuiltApplication.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        StandIns.ScrubEnvironment(start);
        start.Environment["BELLTOWER_SHELL_PID"] = cohort.Shell.Pid.ToString();
        configure?.Invoke(start);
        foreach (var argument in new[] { "codex-runtime-host", "--attachment", HostAttachment, "--executable", executable, "--data-dir", workspace.Paths.Root })
        {
            start.ArgumentList.Add(argument);
        }
        var manager = Process.Start(start)!;
        manager.OutputDataReceived += (_, _) => { };
        manager.ErrorDataReceived += (_, _) => { };
        manager.BeginOutputReadLine();
        manager.BeginErrorReadLine();
        return manager;
    }

    // Waits until the manager has written its token and the backend's grandchild runs; returns the grandchild processes.
    private List<ProcessIdentity> AwaitLiveBackend(Process manager, string grandchild, ProcessIdentity? decoy)
    {
        var store = new CodexRuntimeStore(workspace.Paths);
        WaitFor(() => File.Exists(store.TokenPath(HostAttachment)) || manager.HasExited, "The runtime host never wrote its token.");
        Assert.False(manager.HasExited, "The runtime host ended before its backend started.");
        List<ProcessIdentity> descendants = [];
        WaitFor(() => (descendants = StandIns.Find(grandchild).Where(process => process != decoy).ToList()).Count > 0 || manager.HasExited,
            "The stand-in backend never started its grandchild.");
        Assert.False(manager.HasExited, "The runtime host ended while its backend was starting.");
        return descendants;
    }

    private string HostLog() => File.Exists(Path.Combine(workspace.Paths.Root, "bell-tower.log")) ? File.ReadAllText(Path.Combine(workspace.Paths.Root, "bell-tower.log")) : "";

    [Fact]
    public void RealRuntimeHostStopsOnlyTheBackendItStartedAndItsDescendants()
    {
        // The production manager (codex-runtime-host) with a live stand-in backend. The stop marker asks the manager to end;
        // the manager ends the backend it started and that backend's descendants and no other process, whatever else runs
        // from the same image.
        var (backend, grandchild) = CreateLiveBackend();
        using var decoyProcess = StartImage(grandchild, "ping -n 180 127.0.0.1 >nul");
        var decoy = ProcessProbe.Identify(decoyProcess.Id)!;
        using var manager = StartHost(backend);
        var managerIdentity = ProcessProbe.Identify(manager.Id)!;
        try
        {
            var descendants = AwaitLiveBackend(manager, grandchild, decoy);
            Assert.All(descendants, process => Assert.True(ManagementCohort.IsLive(process)));

            new CodexRuntimeStore(workspace.Paths).Stop(HostAttachment);

            Assert.True(manager.WaitForExit(30000), "The runtime host did not stop on its marker.");
            foreach (var process in descendants)
            {
                Assert.True(cohort.WaitForEnd(process, TimeSpan.FromSeconds(10)), "A descendant of the backend the manager started was left running.");
            }
            // The marker path, not the readiness timeout or a backend exit, ended the manager.
            Assert.Contains("codex-runtime-stop-requested", HostLog());
            Assert.False(ManagementCohort.IsLive(managerIdentity));
            Assert.True(ManagementCohort.IsLive(decoy), "A process of the same image that the manager did not start was stopped.");
            cohort.AssertIndependentSurvive("the runtime host's shutdown");
            Assert.True(ManagementCohort.IsLive(cohort.OwnedManager));
        }
        finally
        {
            if (!manager.HasExited)
            {
                manager.Kill(entireProcessTree: true);
            }
            if (!decoyProcess.HasExited)
            {
                decoyProcess.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    public void TheOwnedConnectionNeverGoesThroughAProxy()
    {
        // The owned bearer token must only travel the direct loopback connection, whatever the proxy environment says.
        using var proxy = new TcpListener(IPAddress.Loopback, 0);
        proxy.Start();
        var tunnelled = 0;
        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using var client = await proxy.AcceptTcpClientAsync();
                    Interlocked.Increment(ref tunnelled);
                }
            }
            catch (Exception)
            {
                // The listener was stopped.
            }
        });
        var address = "http://127.0.0.1:" + ((IPEndPoint)proxy.LocalEndpoint).Port;
        var (backend, grandchild) = CreateLiveBackend();
        using var manager = StartHost(backend, start =>
        {
            foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy" })
            {
                start.Environment[name] = address;
            }
            start.Environment.Remove("NO_PROXY");
            start.Environment.Remove("no_proxy");
        });
        try
        {
            AwaitLiveBackend(manager, grandchild, null);
            // The readiness probe keeps connecting to the endpoint of the live backend; give it several attempts.
            Thread.Sleep(2500);
            Assert.Equal(0, Volatile.Read(ref tunnelled));

            new CodexRuntimeStore(workspace.Paths).Stop(HostAttachment);
            Assert.True(manager.WaitForExit(30000), "The runtime host did not stop on its marker.");
        }
        finally
        {
            if (!manager.HasExited)
            {
                manager.Kill(entireProcessTree: true);
            }
            proxy.Stop();
        }
    }

    [Fact]
    public void SetupRemovalRestoresIsolatedHomesAndSignalsNoProcess()
    {
        var home = Path.Combine(workspace.Root, "home");
        var settings = Path.Combine(home, ".claude", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, """{"model":"keep"}""");
        new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", home).Plan("both").Apply(workspace.Paths);
        Assert.NotEmpty(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settings))!["hooks"]!.AsObject());

        var result = Run(workspace, ["setup", "--remove", "--apply", "--clients", "both", "--home", home]);

        Assert.True(result.Code == 0, result.Error + result.Output);
        Assert.Empty(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(settings))!["hooks"]!.AsObject());
        Assert.Contains("keep", File.ReadAllText(settings));
        cohort.AssertIndependentSurvive("setup removal");
        Assert.True(ManagementCohort.IsLive(cohort.OwnedManager));
    }

    // Claude direct, Codex direct, Codex shared and Codex owned sessions, all known to the tray at the same time. The owned
    // record names an endpoint (a stand-in Codex server when the test provides one) and the fixture token.
    private void RegisterSessions(string? ownedEndpoint = null)
    {
        Register("exit-claude", "claude", cohort.ClaudeClient, "direct");
        Register("exit-codex", "codex", cohort.CodexClient, "direct");
        var shared = Register("exit-shared", "codex", null, "shared");
        Register(ManagementCohort.OwnedAttachment, "codex", cohort.Shell, "owned");
        if (ownedEndpoint is not null)
        {
            cohort.WriteRecord(cohort.OwnedRecord(endpoint: ownedEndpoint));
        }
        var sessions = new[]
        {
            ("claude", "exit-claude", "session-exit-claude"),
            ("codex", "exit-codex", "session-exit-codex"),
            ("codex", shared.Id, SharedSession),
            ("codex", ManagementCohort.OwnedAttachment, "session-" + ManagementCohort.OwnedAttachment)
        };
        foreach (var (provider, attachment, session) in sessions)
        {
            new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Register) with
            {
                Provider = provider,
                SessionId = session,
                AttachmentId = attachment,
                Directory = workspace.Root
            });
        }
    }

    private const string SharedSession = "shared-session";

    // Native rows only: waits until the tray has actually reached the private backend through the shared session (its native-name
    // status stops being the initial one) and records that status for the evidence. A status that says the backend is missing or
    // unverified fails the row, so the survival of the real backend is not claimed for a backend that was never observed.
    private void AwaitNativeObservation()
    {
        if (NativeCodexHome is null)
        {
            return;
        }
        var identities = new IdentityStore(workspace.Paths);
        string status = "";
        WaitFor(() => (status = identities.Get("codex:" + SharedSession, workspace.Root).NativeSyncStatus) != "unavailable", "The tray never observed the private backend.");
        Assert.DoesNotContain("is not running", status);
        Assert.DoesNotContain("is not verified", status);
        Assert.DoesNotContain("uses another Codex home", status);
        if (Environment.GetEnvironmentVariable("BELLTOWER_NATIVE_STATUS_OUT") is { Length: > 0 } output)
        {
            File.AppendAllText(output, status + Environment.NewLine);
        }
    }

    [Fact]
    public void TrayExitStopsTheTrayAndLeavesEveryOtherProcess()
    {
        // The owned session points at a stand-in Codex server, so the tray opens a real owned observer. The server never answers
        // the read, which holds that observer open when the exit request arrives.
        using var owned = new CodexStandInServer(null, codexHome, CodexStandInServer.Behavior.SilentAfterInitialize);
        RegisterSessions("ws://127.0.0.1:" + owned.Port);
        var tray = StartTray();
        try
        {
            var controller = new TrayController(workspace.Paths);
            WaitFor(controller.IsRunning, "The isolated tray did not start.");
            Assert.True(ManagementCohort.IsLive(ProcessProbe.Identify(tray.Id)!));
            WaitFor(() => owned.RecordedMethods().Contains("initialize"), "The tray did not open its owned observer.");
            AwaitNativeObservation();

            var result = Run(workspace, ["exit"], codexHome);

            Assert.True(result.Code == 0, result.Error);
            Assert.True(tray.WaitForExit(30000), "The tray did not exit.");
            WaitFor(() => owned.Closed >= owned.Accepted && owned.Accepted > 0, "The owned observer connection was not closed.");
            Assert.Equal("Bearer " + ManagementCohort.FixtureToken, owned.Authorization);
            // Observers only read: no resume, start, archive or turn request ever reaches the runtime.
            Assert.DoesNotContain(owned.RecordedMethods(), method => method is "thread/resume" or "thread/start" or "thread/archive" || method.StartsWith("turn/", StringComparison.Ordinal));
            cohort.AssertIndependentSurvive("tray exit");
            Assert.True(ManagementCohort.IsLive(cohort.OwnedManager));
            Assert.False(File.Exists(new CodexRuntimeStore(workspace.Paths).StopPath(ManagementCohort.OwnedAttachment)));
        }
        finally
        {
            if (!tray.HasExited)
            {
                tray.Kill(entireProcessTree: true);
            }
            tray.Dispose();
        }
    }

    private Process StartTray()
    {
        var start = new ProcessStartInfo(BuiltApplication.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        StandIns.ScrubEnvironment(start);
        start.Environment["CODEX_HOME"] = codexHome;
        foreach (var argument in new[] { "tray", "--silent", "--data-dir", workspace.Paths.Root })
        {
            start.ArgumentList.Add(argument);
        }
        var process = Process.Start(start)!;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void WaitFor(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow + StandIns.Deadline;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }
        Assert.True(condition(), message);
    }

    // The packaged update path: the real coordinator runs from a staged payload copy of this build, with the real launcher.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void UpdateReplacesOnlyTheTrayAndLeavesEveryOtherProcess(bool hadTray, bool failBeforeSwitch)
    {
        var install = Path.Combine(workspace.Root, "install");
        var target = BellTower.Cli.ApplicationInfo.Version;
        var installation = CreateInstallation(install, "0.0.1", target);
        var plan = new UpdatePlan(Guid.NewGuid().ToString("N"), Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            installation.Root, workspace.Paths.Root, "0.0.1", target,
            failBeforeSwitch ? new string('a', 64) : Installation.Hash(Path.Combine(installation.Payload(target), "release.json")),
            DateTimeOffset.UtcNow, cohort.Shell, hadTray);
        DistributionJson.Write(UpdateTransaction.PlanPath(installation, plan.Id), plan);
        DistributionJson.Write(Path.Combine(installation.Root, "updates", "pending.json"), plan);
        RegisterSessions();
        var tray = hadTray ? StartTray() : null;
        try
        {
            if (hadTray)
            {
                WaitFor(new TrayController(workspace.Paths).IsRunning, "The isolated tray did not start.");
                AwaitNativeObservation();
            }
            var start = new ProcessStartInfo(Path.Combine(installation.Payload(target), "bell-tower.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            StandIns.ScrubEnvironment(start);
            start.Environment["CODEX_HOME"] = codexHome;
            foreach (var argument in new[] { "__apply-update", UpdateTransaction.PlanPath(installation, plan.Id), plan.Token })
            {
                start.ArgumentList.Add(argument);
            }
            // Not redirected: the restarted tray inherits the coordinator's handles and would hold a pipe open after it exits.
            using var coordinator = Process.Start(start)!;
            Assert.True(coordinator.WaitForExit(90000), "The update coordinator did not finish.");
            var outcome = DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(installation));

            cohort.AssertIndependentSurvive("update");
            Assert.True(ManagementCohort.IsLive(cohort.OwnedManager));
            Assert.False(File.Exists(new CodexRuntimeStore(workspace.Paths).StopPath(ManagementCohort.OwnedAttachment)));
            if (failBeforeSwitch)
            {
                Assert.Equal("failed", outcome.State);
                Assert.Equal("0.0.1", installation.Selection().Version);
                // A failed update that never stopped the tray leaves it running.
                Assert.Equal(hadTray, new TrayController(workspace.Paths).IsRunning());
            }
            else
            {
                Assert.True(coordinator.ExitCode == 0, outcome.Message);
                Assert.Equal(target, installation.Selection().Version);
                Assert.Equal(hadTray ? "monitoring-restored" : "activated", outcome.State);
                Assert.Equal(hadTray, new TrayController(workspace.Paths).IsRunning());
            }
            if (hadTray && !failBeforeSwitch)
            {
                // The replacement tray is the launcher's child; the original tray exited on the request.
                Assert.True(tray!.WaitForExit(10000));
            }
        }
        finally
        {
            if (new TrayController(workspace.Paths).IsRunning())
            {
                Run(workspace, ["exit"], codexHome);
                WaitFor(() => !new TrayController(workspace.Paths).IsRunning(), "The isolated tray did not exit.");
            }
            if (tray is { HasExited: false })
            {
                tray.Kill(entireProcessTree: true);
            }
            tray?.Dispose();
            StandIns.KillUnder(install);
        }
    }

    private static Installation CreateInstallation(string root, string original, string target)
    {
        Directory.CreateDirectory(root);
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var launcher = Path.Combine(Repository(), "src", "BellTower.Launcher", "bin", output.Parent!.Name, output.Name);
        if (!File.Exists(Path.Combine(launcher, "bell-tower.exe")))
        {
            throw new FileNotFoundException("The launcher build output is missing; the test project must build src/BellTower.Launcher for this configuration.", Path.Combine(launcher, "bell-tower.exe"));
        }
        CopyDirectory(launcher, root);
        DistributionJson.Write(Path.Combine(root, "installation.json"), new InstallationManifest());
        AddFakePayload(root, original);
        var payload = Path.Combine(root, "versions", target);
        CopyDirectory(Path.GetDirectoryName(BuiltApplication.Executable)!, payload);
        File.WriteAllText(Path.Combine(payload, $"bell-tower-focus-{target}.vsix"), "fixture");
        // This build's contract declares capture and attachment health, as a real release does, so the update gates that
        // protect live sessions (health consumer, ingress inventory) run with the registered sessions.
        File.Copy(BuiltApplication.Ingress, Path.Combine(payload, "bell-tower-ingress.exe"), true);
        var files = new Dictionary<string, string>
        {
            ["bell-tower.exe"] = Installation.Hash(Path.Combine(payload, "bell-tower.exe")),
            ["bell-tower-ingress.exe"] = Installation.Hash(Path.Combine(payload, "bell-tower-ingress.exe")),
            [$"bell-tower-focus-{target}.vsix"] = Installation.Hash(Path.Combine(payload, $"bell-tower-focus-{target}.vsix"))
        };
        DistributionJson.Write(Path.Combine(payload, "release.json"), new ReleaseContract(target, Capture: 1, AttachmentHealth: 1, Files: files));
        DistributionJson.Write(Path.Combine(root, "current.json"), new VersionSelection(original));
        return new Installation(root);
    }

    private static void AddFakePayload(string root, string version)
    {
        var directory = Directory.CreateDirectory(Path.Combine(root, "versions", version)).FullName;
        var files = new Dictionary<string, string>();
        foreach (var name in new[] { "bell-tower.exe", $"bell-tower-focus-{version}.vsix" })
        {
            File.WriteAllText(Path.Combine(directory, name), name + version);
            files[name] = Installation.Hash(Path.Combine(directory, name));
        }
        DistributionJson.Write(Path.Combine(directory, "release.json"), new ReleaseContract(version, Files: files));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static string Repository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
        {
            directory = directory.Parent;
        }
        return directory!.FullName;
    }
}

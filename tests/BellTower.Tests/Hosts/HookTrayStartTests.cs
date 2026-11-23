using System.Diagnostics;
using BellTower.Cli;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Setup;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Hosts;

// Task 4.5: after the production ingress saves a capture it starts a stopped tray, the same way for Claude Code and Codex.
// The "launcher" is a stand-in executable named bell-tower.exe that only records how it was started and holds the tray-instance
// mutex; no test starts a real tray, uses the real data directory or stops a process it did not start.
public sealed class HookTrayStartTests
{
    private sealed record Fake(int Pid, string Arguments, string WorkingDirectory, string[] Standard, HashSet<string> Environment);

    private sealed record Run(int ExitCode, string Output, string Error, bool StdoutClosed, TimeSpan Elapsed)
    {
        public string Message => $"exit={ExitCode}; stdout={Output}; stderr={Error}; closed={StdoutClosed}";
    }

    private sealed class Harness : IDisposable
    {
        public TestWorkspace Workspace { get; } = new();
        public string Root => Workspace.Paths.Root;
        public string LauncherDirectory => Path.Combine(Workspace.Root, "launcher");
        public string Launcher => Path.Combine(LauncherDirectory, "bell-tower.exe");
        private string FakeFolder => Path.Combine(Root, "fake-launcher");

        public Harness(bool record = true)
        {
            Directory.CreateDirectory(LauncherDirectory);
            foreach (var file in Directory.GetFiles(BuiltApplication.FakeLauncherDirectory))
            {
                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(LauncherDirectory, name == "fake-launcher.exe" ? "bell-tower.exe" : name), true);
            }
            if (record)
            {
                HookTrayStart.RecordLauncher(Root, Launcher);
            }
        }

        public async Task<Run> Hook(string provider, string? payload = null, string[]? arguments = null)
        {
            var start = new ProcessStartInfo(BuiltApplication.Ingress)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments ?? ["--provider", provider, "--data-dir", Root])
            {
                start.ArgumentList.Add(argument);
            }
            StandIns.ScrubEnvironment(start);
            // What a provider's hook environment looks like; none of it may reach the started tray.
            foreach (var name in Leaks)
            {
                start.Environment[name] = "leak";
            }
            start.Environment["HOOK_ONLY_MARKER_4_5"] = "leak";
            var watch = Stopwatch.StartNew();
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(payload ?? NativeFixture.SessionStart(provider).PayloadText);
            process.StandardInput.Close();
            // A provider waits for the output pipe to close, so EOF must arrive while a started tray keeps running.
            var closed = await Task.WhenAny(stdout, Task.Delay(StandIns.Deadline)) == stdout;
            Assert.True(process.WaitForExit(20000), "The hook did not exit.");
            return new Run(process.ExitCode, closed ? (await stdout).Trim() : "", closed && await Task.WhenAny(stderr, Task.Delay(5000)) == stderr ? await stderr : "", closed, watch.Elapsed);
        }

        public Task<Run> HookInKillOnCloseJob(string provider) => HookInProviderJob(provider, "immediate");

        public Task<Run> HookInNestedKillOnCloseJob(string provider) => HookInProviderJob(provider, "nested");

        private async Task<Run> HookInProviderJob(string provider, string mode)
        {
            var payloadPath = Path.Combine(Workspace.Root, "job-hook.json");
            File.WriteAllText(payloadPath, NativeFixture.SessionStart(provider).PayloadText);
            var start = new ProcessStartInfo(Path.Combine(BuiltApplication.FakeLauncherDirectory, "fake-launcher.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "job-host", mode, BuiltApplication.Ingress, provider, Root, payloadPath })
            {
                start.ArgumentList.Add(argument);
            }
            StandIns.ScrubEnvironment(start);
            var watch = Stopwatch.StartNew();
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(25000), "The job harness did not exit.");
            return new Run(process.ExitCode, (await stdout).Trim(), (await stderr).Trim(), true, watch.Elapsed);
        }

        public static readonly string[] Leaks = ["CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CODEX_THREAD_ID", "CODEX_HOME", "BELLTOWER_HOOK_LEAK", "BELLTOWER_DATA_DIR"];

        public IReadOnlyList<Fake> Fakes()
        {
            if (!Directory.Exists(FakeFolder))
            {
                return [];
            }
            var result = new List<Fake>();
            foreach (var file in Directory.GetFiles(FakeFolder, "*.txt"))
            {
                var lines = File.ReadAllLines(file);
                string Value(string key) => lines.First(line => line.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..];
                result.Add(new Fake(int.Parse(Value("pid")), Value("args"), Value("cwd"), [Value("stdin"), Value("stdout"), Value("stderr")],
                    lines.Where(line => line.StartsWith("env=", StringComparison.Ordinal)).Select(line => line[4..]).ToHashSet(StringComparer.OrdinalIgnoreCase)));
            }
            return result;
        }

        public async Task<IReadOnlyList<Fake>> WaitForFakes(int count)
        {
            var deadline = DateTime.UtcNow + StandIns.Deadline;
            while (Fakes().Count < count && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            return Fakes();
        }

        public int Captures() => Directory.Exists(Path.Combine(Root, CaptureProtocol.InboxName)) ? Directory.GetFiles(Path.Combine(Root, CaptureProtocol.InboxName), "*.json").Length : 0;

        public bool Alive(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        // Releases and, if needed, kills only the stand-ins this harness started (matched by pid and image path).
        public void Dispose()
        {
            Directory.CreateDirectory(FakeFolder);
            File.WriteAllText(Path.Combine(FakeFolder, "release"), "");
            foreach (var fake in Fakes())
            {
                try
                {
                    using var process = Process.GetProcessById(fake.Pid);
                    if (string.Equals(process.MainModule?.FileName, Launcher, StringComparison.OrdinalIgnoreCase) && !process.WaitForExit(5000))
                    {
                        process.Kill();
                    }
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
            var junction = Path.Combine(Workspace.Root, "junction");
            if (Directory.Exists(junction))
            {
                Directory.Delete(junction, false); // The link only, never the launcher folder it points to.
            }
            Workspace.Dispose();
        }
    }

    private static async Task<Harness> Settle(Harness harness)
    {
        // A refused start is proven by waiting out a bounded period in which a started stand-in would have recorded itself.
        await Task.Delay(1500);
        return harness;
    }

    // ---- Start, output and process hygiene (both providers) --------------------------------------------------------------

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AHookWithNoTrayStartsExactlyOneTrayThatConsumesTheQueue(string provider)
    {
        using var harness = new Harness();
        var run = await harness.Hook(provider);
        Assert.True(run is { ExitCode: 0, Output: "{}", Error: "", StdoutClosed: true }, run.Message);
        var fake = Assert.Single(await harness.WaitForFakes(1));
        Assert.Equal(1, harness.Captures());
        // The arguments are exactly the ones every payload has always accepted; the hook marker travels in the environment, which an older
        // tray ignores, so a rollback or switch to an older payload still starts (ANALYSIS-4702).
        Assert.Equal($"tray|--data-dir|{harness.Root}", fake.Arguments);
        Assert.Contains(HookTrayStart.HookStartVariable, fake.Environment);
        Assert.Equal(harness.LauncherDirectory, fake.WorkingDirectory, ignoreCase: true);
        // The hook returned and its output reached end-of-file while the started tray is still running.
        Assert.True(harness.Alive(fake.Pid));
        Assert.True(HookTrayStart.TrayRunning(harness.Root));
        var last = HookTrayStart.LastAttempt(harness.Root);
        Assert.Equal(nameof(HookStartOutcome.Started), last?.Outcome);
        // The queue the started tray consumes still holds a usable capture.
        var state = new StateStore(harness.Workspace.Paths).Drain();
        Assert.Contains(state.Sessions.Keys, key => key.StartsWith(provider + ":", StringComparison.Ordinal));
        Assert.Equal(0, harness.Captures());
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task TheStartedTrayHoldsNoHookHandleAndSeesNoProviderEnvironment(string provider)
    {
        using var harness = new Harness();
        var run = await harness.Hook(provider);
        Assert.True(run.StdoutClosed, run.Message);
        var fake = Assert.Single(await harness.WaitForFakes(1));
        // 3 is a pipe: the hook's own standard handles. A fresh hidden console reports character devices.
        Assert.DoesNotContain("3", fake.Standard);
        foreach (var name in Harness.Leaks.Append("HOOK_ONLY_MARKER_4_5"))
        {
            Assert.DoesNotContain(name, fake.Environment);
        }
        // The user's own environment block is present instead of an empty or inherited one.
        Assert.Contains("USERPROFILE", fake.Environment);
        Assert.Contains("LOCALAPPDATA", fake.Environment);
        Assert.Contains("PATH", fake.Environment);
    }

    [Fact]
    public async Task AFailedStartIsRecordedAndNeverChangesTheHook()
    {
        using var harness = new Harness(record: false);
        // Named like the launcher but not an executable: the attempt is made, fails and is recorded.
        File.WriteAllText(harness.Launcher, "not a program");
        HookTrayStart.RecordLauncher(harness.Root, harness.Launcher);
        var run = await harness.Hook("claude");
        Assert.True(run is { ExitCode: 0, Output: "{}", Error: "", StdoutClosed: true }, run.Message);
        Assert.Equal(1, harness.Captures());
        var last = HookTrayStart.LastAttempt(harness.Root);
        Assert.Equal(nameof(HookStartOutcome.Failed), last?.Outcome);
        Assert.StartsWith("create-process:", last?.Detail);
        Assert.Empty(harness.Fakes());
    }

    // ---- Refusals -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ARunningTrayCostsNoStart(string provider)
    {
        using var harness = new Harness();
        using var tray = new Mutex(false, HookTrayStart.MutexName(harness.Root, HookTrayStart.TrayArea));
        var run = await harness.Hook(provider);
        Assert.True(run is { ExitCode: 0, Output: "{}" }, run.Message);
        await Settle(harness);
        Assert.Empty(harness.Fakes());
        Assert.Null(HookTrayStart.LastAttempt(harness.Root));
        Assert.Equal(1, harness.Captures());
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AUserStopInThisSignInKeepsTheTrayStoppedUntilItIsCleared(string provider)
    {
        using var harness = new Harness();
        HookTrayStart.RecordUserStop(harness.Root);
        Assert.True(HookTrayStart.UserStopActive(harness.Root));
        var run = await harness.Hook(provider);
        Assert.True(run is { ExitCode: 0, Output: "{}" }, run.Message);
        await Settle(harness);
        Assert.Empty(harness.Fakes());
        Assert.Equal(1, harness.Captures());
        // Starting the tray by hand clears the record; the next hook may start it again.
        HookTrayStart.ClearUserStop(harness.Root);
        await harness.Hook(provider);
        Assert.Single(await harness.WaitForFakes(1));
    }

    [Fact]
    public async Task AUserStopFromAnotherSignInIsStaleAndIgnored()
    {
        using var harness = new Harness();
        Directory.CreateDirectory(HookTrayStart.StateDirectory(harness.Root));
        File.WriteAllText(HookTrayStart.UserStopRecord(harness.Root), "luid:1/1");
        Assert.False(HookTrayStart.UserStopActive(harness.Root));
        // Same logon session id with another logon time (a reused id after a restart) is also another sign-in.
        var token = HookTrayStart.SignInToken()!;
        File.WriteAllText(HookTrayStart.UserStopRecord(harness.Root), token.Split('/')[0] + "/1");
        if (token.Contains('/'))
        {
            Assert.False(HookTrayStart.UserStopActive(harness.Root));
        }
        await harness.Hook("claude");
        Assert.Single(await harness.WaitForFakes(1));
    }

    [Fact]
    public void OnlyTheUserExitCommandRecordsAStopNotUpdateOrSwitchStops()
    {
        using var harness = new Harness();
        var tray = new Mutex(false, HookTrayStart.MutexName(harness.Root, HookTrayStart.TrayArea));
        var controller = new TrayController(harness.Workspace.Paths);
        controller.RequestExit();
        Assert.False(HookTrayStart.UserStopActive(harness.Root));
        Assert.True(File.Exists(Path.Combine(harness.Root, "exit.request")));
        File.Delete(Path.Combine(harness.Root, "exit.request"));
        controller.RequestUserExit();
        Assert.True(HookTrayStart.UserStopActive(harness.Root));
        // With no tray the exit command fails and records nothing.
        tray.Dispose();
        HookTrayStart.ClearUserStop(harness.Root);
        Assert.Throws<InvalidOperationException>(controller.RequestUserExit);
        Assert.False(HookTrayStart.UserStopActive(harness.Root));
    }

    [Fact]
    public void ExitCliCommandRecordsTheStopThroughItsProductionCommandPath()
    {
        using var harness = new Harness();
        using var tray = new Mutex(false, HookTrayStart.MutexName(harness.Root, HookTrayStart.TrayArea));

        var result = CliApplication.Run(["exit", "--data-dir", harness.Root]);

        Assert.Equal(0, result);
        Assert.True(HookTrayStart.UserStopActive(harness.Root));
        Assert.Equal("exit", File.ReadAllText(Path.Combine(harness.Root, "exit.request")));
    }

    [Fact]
    public void TrayExitMenuRecordsTheStopAndExitsThroughItsProductionHandler()
    {
        using var harness = new Harness();
        var exited = false;
        var item = TrayApplication.CreateExitMenuItem(harness.Workspace.Paths, () => exited = true);

        item.PerformClick();

        Assert.True(exited);
        Assert.True(HookTrayStart.UserStopActive(harness.Root));
    }

    // ---- Design decision 10: the tray is created as a child of the verified desktop shell --------------------------------------

    private static DesktopShell.Facts GoodShell() => new(100, @"C:\Windows\explorer.exe", @"C:\Windows", true, 1, 1, 100);

    [Fact]
    public void AVerifiedShellHasNoProblem() => Assert.Null(DesktopShell.Problem(GoodShell()));

    // The T-12 regression: a hook running inside a provider's kill-on-close job (immediate, or nested under a job that permits
    // breakaway) starts the tray as a child of the shell, so closing the provider's job does not end it.
    [Theory]
    [InlineData("claude", "immediate")]
    [InlineData("claude", "nested")]
    [InlineData("codex", "immediate")]
    [InlineData("codex", "nested")]
    public async Task ATrayStartedFromInsideAKillOnCloseProviderJobSurvivesTheJobClosing(string provider, string shape)
    {
        using var harness = new Harness();
        var run = shape == "immediate" ? await harness.HookInKillOnCloseJob(provider) : await harness.HookInNestedKillOnCloseJob(provider);
        Assert.True(run is { ExitCode: 0, Output: "{}", Error: "" }, run.Message);
        Assert.Equal(1, harness.Captures());
        if (HookTrayStart.ShellProblem() is not null)
        {
            // Headless run with no desktop shell: nothing starts and the capture stays queued.
            Assert.Empty(harness.Fakes());
            return;
        }
        var fake = Assert.Single(await harness.WaitForFakes(1));
        // The host closed its kill-on-close job before returning; the tray it started is still running.
        await Task.Delay(500);
        Assert.True(harness.Alive(fake.Pid));
        Assert.Equal(TrayProcess.RouteShell, HookTrayStart.LastAttempt(harness.Root)!.Value.Detail);
    }

    [Theory]
    [InlineData(0, "shell-none")]
    [InlineData(1, "shell-changed")]
    [InlineData(2, "shell-image-unverified")]
    [InlineData(3, "shell-image-unverified")]
    [InlineData(4, "shell-other-user")]
    [InlineData(5, "shell-other-user")]
    [InlineData(6, "shell-other-session")]
    [InlineData(7, "shell-other-session")]
    public void ShellLookupAndVerificationFailClosed(int variant, string expected)
    {
        var good = GoodShell();
        var facts = variant switch
        {
            0 => good with { ShellPid = 0 },
            1 => good with { OpenedPid = 101 },
            2 => good with { Image = null },
            3 => good with { Image = @"C:\Users\someone\explorer.exe" },
            4 => good with { SameUser = false },
            5 => good with { SameUser = null },
            6 => good with { ShellSession = 2 },
            _ => good with { ShellSession = null }
        };
        Assert.Equal(expected, DesktopShell.Problem(facts));
    }

    [Fact]
    public void AnUnverifiedShellStartsNothingAndTheReasonIsRecorded()
    {
        using var harness = new Harness();
        var (outcome, detail) = TrayProcess.Start(harness.Launcher, harness.Root, () => new DesktopShell.Opened(IntPtr.Zero, "shell-other-session"));
        Assert.Equal(HookStartOutcome.Failed, outcome);
        Assert.Equal("shell-other-session", detail);
        Assert.Empty(harness.Fakes());
    }

    private static (HookStartOutcome Outcome, string Detail) ShellFailure(string launcher, string root) => (HookStartOutcome.Failed, "shell-none");
    private static (HookStartOutcome Outcome, string Detail) ShellSuccess(string launcher, string root) => (HookStartOutcome.Started, TrayProcess.RouteShell);

    [Fact]
    public void TheTaskIsRunOnlyWhenTheShellStartFailsAndOnlyWhenOneIsRecorded()
    {
        using var harness = new Harness();
        var ran = 0;
        (HookStartOutcome Outcome, string Detail) RunTask(string root)
        {
            ran++;
            return (HookStartOutcome.Started, HookStartTask.RouteTask);
        }

        // The shell start works: the task is never consulted.
        HookTrayStart.RecordLauncher(harness.Root, harness.Launcher);
        HookStartTask.Record(harness.Root, HookStartTask.TaskPath(harness.Root, "BellTowerTest-DEV059"));
        Assert.Equal(HookStartOutcome.Started, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow, ShellSuccess, RunTask));
        Assert.Equal(0, ran);
        Assert.Equal(TrayProcess.RouteShell, HookTrayStart.LastAttempt(harness.Root)!.Value.Detail);

        // The shell start fails: the recorded task starts the tray and the route is recorded.
        Assert.Equal(HookStartOutcome.Started, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow.AddMinutes(5), ShellFailure, RunTask));
        Assert.Equal(1, ran);
        Assert.Equal(HookStartTask.RouteTask, HookTrayStart.LastAttempt(harness.Root)!.Value.Detail);

        // No task recorded: a failed shell start stays failed and nothing else runs.
        HookStartTask.ClearRecord(harness.Root);
        Assert.Equal(HookStartOutcome.Failed, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow.AddMinutes(10), ShellFailure, RunTask));
        Assert.Equal(1, ran);

        // Both routes fail: the failure names both reasons.
        HookStartTask.Record(harness.Root, HookStartTask.TaskPath(harness.Root, "BellTowerTest-DEV059"));
        Assert.Equal(HookStartOutcome.Failed, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow.AddMinutes(15), ShellFailure, _ => (HookStartOutcome.Failed, "task-run-exit:1")));
        Assert.Equal("shell-none;task-run-exit:1", HookTrayStart.LastAttempt(harness.Root)!.Value.Detail);
    }

    [Fact]
    public void UserStopBurstControlAndTheUpdateGateStillApplyBeforeAnyRoute()
    {
        using var harness = new Harness();
        var starts = 0;
        (HookStartOutcome Outcome, string Detail) Shell(string launcher, string root)
        {
            starts++;
            return (HookStartOutcome.Started, TrayProcess.RouteShell);
        }
        HookTrayStart.RecordUserStop(harness.Root);
        Assert.Equal(HookStartOutcome.UserStopped, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow, Shell, null));
        HookTrayStart.ClearUserStop(harness.Root);
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(HookStartOutcome.Started, HookTrayStart.TryStart(harness.Root, now, Shell, null));
        Assert.Equal(HookStartOutcome.Throttled, HookTrayStart.TryStart(harness.Root, now.AddSeconds(1), Shell, null));
        Directory.CreateDirectory(Path.Combine(harness.Root, "updates"));
        File.WriteAllText(Path.Combine(harness.Root, "updates", "restart.json"), "{}");
        Assert.Equal(HookStartOutcome.UpdatePending, HookTrayStart.TryStart(harness.Root, now.AddMinutes(5), Shell, null));
        Assert.Equal(1, starts);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0x1800u, false)]
    [InlineData(0x2000u, true)]
    [InlineData(0x2800u, true)]
    public void AHookStartedTrayRefusesAJobThatKillsItOnCloseAndRecordsWhy(uint? flags, bool refuses)
    {
        using var workspace = new TestWorkspace();
        Assert.Equal(refuses, TrayJobGuard.Refuses(workspace.Paths, hookStarted: true, () => flags));
        Assert.Equal(refuses, TrayJobGuard.LastRefusal(workspace.Paths.Root) is not null);
        if (refuses)
        {
            Assert.Contains(TrayJobGuard.LogCode, File.ReadAllText(Path.Combine(workspace.Paths.Root, "bell-tower.log")), StringComparison.Ordinal);
            Assert.True(new HookStartReporter(workspace.Paths).Report().ToString()!.Length > 0);
        }
    }

    [Fact]
    public void ATrayStartedByHandOrByTheTaskIsNeverRefusedForItsJob()
    {
        using var workspace = new TestWorkspace();
        Assert.False(TrayJobGuard.Refuses(workspace.Paths, hookStarted: false, () => 0x2800u));
        Assert.Null(TrayJobGuard.LastRefusal(workspace.Paths.Root));
    }

    [Fact]
    public void AHookStartedTrayIsClassifiedByItsOrdinaryCommandLine()
    {
        var kind = TrayInvocationCommand.Classify("\"D:\\app\\bell-tower.exe\" tray --data-dir D:\\data", "D:\\app\\bell-tower.exe", "D:\\data");
        Assert.Equal(TrayInvocationKind.ThisDataDirectory, kind);
    }

    [Fact]
    public void TheTaskDefinitionHasNoTriggerAndTheLimitedInteractivePrincipal()
    {
        var script = HookStartTaskSetup.RegisterScript(HookStartTask.TaskPath(@"D:\data", "BellTowerTest-DEV059"), @"D:\app\bell-tower.exe", @"D:\data");
        Assert.DoesNotContain("-Trigger", script, StringComparison.Ordinal);
        Assert.Contains("-LogonType Interactive -RunLevel Limited", script, StringComparison.Ordinal);
        Assert.Contains("-ExecutionTimeLimit ([TimeSpan]::Zero)", script, StringComparison.Ordinal);
        Assert.Contains("-AllowStartIfOnBatteries -DontStopIfGoingOnBatteries", script, StringComparison.Ordinal);
        Assert.Contains("-MultipleInstances IgnoreNew", script, StringComparison.Ordinal);
        Assert.Contains("'\\BellTowerTest-DEV059\\'", script, StringComparison.Ordinal);
    }

    // A real per-user task under a disposable folder, run through the same code the hook uses, then deleted. The folder is the test
    // prefix: nothing under the product folder is touched.
    [Fact]
    public async Task ARegisteredOnDemandTaskStartsTheTrayFromTheTaskSchedulerAndIsDeleted()
    {
        const string folder = "BellTowerTest-DEV059";
        using var harness = new Harness();
        var task = HookStartTask.TaskPath(harness.Root, folder);
        try
        {
            Assert.True(HookStartTaskSetup.Register(harness.Root, harness.Launcher, folder));
            Assert.True(HookStartTaskSetup.Exists(task));
            Assert.Equal(task, HookStartTask.Recorded(harness.Root));
            var (outcome, detail) = HookStartTask.Run(harness.Root);
            Assert.True(outcome == HookStartOutcome.Started, detail);
            var fake = Assert.Single(await harness.WaitForFakes(1));
            // The task action is the launcher with the normal arguments and no hook marker.
            Assert.Equal($"tray|--data-dir|{harness.Root}", fake.Arguments);
            // A second run while the instance runs starts no second tray (one instance at most).
            HookStartTask.Run(harness.Root);
            await Task.Delay(1500);
            Assert.Single(harness.Fakes());
        }
        finally
        {
            HookStartTaskSetup.Unregister(harness.Root, folder);
        }
        Assert.False(HookStartTaskSetup.Exists(task));
        Assert.Null(HookStartTask.Recorded(harness.Root));
    }

    [Theory]
    [InlineData(1, false, "ordinary-token", null, "ordinary-token")]
    [InlineData(1, true, "elevated-default-token", null, "elevated-default-token")]
    [InlineData(2, true, "elevated-token", "limited-token", "limited-token")]
    [InlineData(2, false, "unexpected-limited-token", "linked-token", null)]
    [InlineData(3, false, "limited-token", "elevated-token", "limited-token")]
    [InlineData(3, true, "unexpected-elevated-token", null, null)]
    [InlineData(null, false, "unknown-token", null, null)]
    [InlineData(1, null, "unknown-token", null, null)]
    public void SignInIdentityUsesVerifiedLimitedTokenAndFailsClosed(int? elevationType, bool? elevated, string current, string? linked, string? expected)
    {
        Assert.Equal(expected, TrayProcess.SelectSignInIdentity(elevationType, elevated, current, linked));
    }

    [Fact]
    public void AStartedTrayClearsTheStopAndRecordsTheStableLauncher()
    {
        using var harness = new Harness(record: false);
        var installation = Path.Combine(harness.Workspace.Root, "install");
        var payload = Path.Combine(installation, "versions", "0.1.8");
        Directory.CreateDirectory(payload);
        File.Copy(Path.Combine(harness.LauncherDirectory, "bell-tower.exe"), Path.Combine(installation, "bell-tower.exe"));
        File.Copy(Path.Combine(harness.LauncherDirectory, "bell-tower.exe"), Path.Combine(payload, "bell-tower.exe"));
        BellTower.Distribution.DistributionJson.Write(Path.Combine(installation, "installation.json"), new BellTower.Distribution.InstallationManifest());
        BellTower.Distribution.DistributionJson.Write(Path.Combine(installation, "current.json"), new BellTower.Distribution.VersionSelection("0.1.8"));
        BellTower.Distribution.DistributionJson.Write(Path.Combine(payload, "release.json"), new BellTower.Distribution.ReleaseContract("0.1.8"));
        HookTrayStart.RecordUserStop(harness.Root);
        Assert.True(HookTrayStart.UserStopActive(harness.Root));
        new TrayHost(harness.Workspace.Paths).RegisterStart(Path.Combine(payload, "bell-tower.exe"));
        Assert.False(File.Exists(HookTrayStart.UserStopRecord(harness.Root)));
        // The stable launcher is recorded, never the versioned payload.
        Assert.Equal(Path.Combine(installation, "bell-tower.exe"), HookTrayStart.ValidLauncher(harness.Root, out _), ignoreCase: true);
        // A development or test tray (not run from an installation) records nothing.
        File.Delete(HookTrayStart.LauncherRecord(harness.Root));
        new TrayHost(harness.Workspace.Paths).RegisterStart(Path.Combine(harness.LauncherDirectory, "bell-tower.exe"));
        Assert.Null(HookTrayStart.ValidLauncher(harness.Root, out _));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("missing")]
    [InlineData("relative")]
    [InlineData("wrong-name")]
    [InlineData("directory")]
    [InlineData("reparse")]
    public async Task AMissingOrInvalidLauncherRecordStartsNothing(string problem)
    {
        using var harness = new Harness(record: false);
        Directory.CreateDirectory(HookTrayStart.StateDirectory(harness.Root));
        string? recorded = problem switch
        {
            "none" => null,
            "missing" => Path.Combine(harness.Workspace.Root, "elsewhere", "bell-tower.exe"),
            "relative" => Path.Combine("launcher", "bell-tower.exe"),
            "wrong-name" => Path.Combine(harness.LauncherDirectory, "fake-launcher.dll"),
            "directory" => harness.LauncherDirectory,
            _ => LinkedLauncher(harness)
        };
        if (recorded is not null)
        {
            File.WriteAllText(HookTrayStart.LauncherRecord(harness.Root), recorded);
        }
        Assert.Null(HookTrayStart.ValidLauncher(harness.Root, out var reason));
        Assert.NotEmpty(reason);
        var run = await harness.Hook("codex");
        Assert.True(run is { ExitCode: 0, Output: "{}" }, run.Message);
        await Settle(harness);
        Assert.Empty(harness.Fakes());
        Assert.Equal(1, harness.Captures());
        Assert.Null(HookTrayStart.LastAttempt(harness.Root));
        // Doctor says why nothing can start.
        var report = System.Text.Json.JsonSerializer.Serialize(new HookStartReporter(harness.Workspace.Paths).Report());
        Assert.Contains("\"Available\":false", report);
    }

    private static string LinkedLauncher(Harness harness)
    {
        var junction = Path.Combine(harness.Workspace.Root, "junction");
        var start = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{harness.LauncherDirectory}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
        using (var process = Process.Start(start)!)
        {
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
        Assert.True(File.Exists(Path.Combine(junction, "bell-tower.exe")));
        return Path.Combine(junction, "bell-tower.exe");
    }

    [Fact]
    public void AReparsePointAboveTheLauncherIsNotRecordedByTheTrayEither()
    {
        using var harness = new Harness(record: false);
        HookTrayStart.RecordLauncher(harness.Root, LinkedLauncher(harness));
        Assert.False(File.Exists(HookTrayStart.LauncherRecord(harness.Root)));
    }

    [Fact]
    public async Task APendingUpdateOrSwitchStartsNothingAndAStaleMarkerDoesNot()
    {
        using var harness = new Harness();
        var marker = Path.Combine(harness.Root, "updates", "restart.json");
        Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
        File.WriteAllText(marker, "{}");
        var run = await harness.Hook("claude");
        Assert.True(run is { ExitCode: 0, Output: "{}" }, run.Message);
        await Settle(harness);
        Assert.Empty(harness.Fakes());
        Assert.Equal(1, harness.Captures());
        // The same age limit the update transaction applies: an abandoned marker no longer blocks a start.
        File.SetLastWriteTimeUtc(marker, DateTime.UtcNow.AddMinutes(-11));
        await harness.Hook("codex");
        Assert.Single(await harness.WaitForFakes(1));
    }

    [Fact]
    public async Task ARejectedCaptureOrAnArgumentErrorNeverStartsTheTray()
    {
        using var harness = new Harness();
        var invalid = await harness.Hook("claude", payload: "{ not json");
        Assert.True(invalid is { ExitCode: 0, Output: "{}" }, invalid.Message);
        var wrongProvider = await harness.Hook("claude", arguments: ["--provider", "other", "--data-dir", harness.Root]);
        Assert.True(wrongProvider is { ExitCode: 0, Output: "{}" }, wrongProvider.Message);
        await Settle(harness);
        Assert.Empty(harness.Fakes());
        Assert.Equal(0, harness.Captures());
    }

    [Fact]
    public async Task ABenignEventWithNoEnvelopeStillStartsAStoppedTray()
    {
        using var harness = new Harness();
        var run = await harness.Hook("claude", payload: """{"hook_event_name":"SomeFutureEvent","session_id":"s","cwd":"."}""");
        Assert.True(run is { ExitCode: 0, Output: "{}" }, run.Message);
        Assert.Equal(0, harness.Captures());
        Assert.Single(await harness.WaitForFakes(1));
    }

    // ---- Burst control ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentHooksOfBothProvidersStartOneTray()
    {
        using var harness = new Harness();
        var runs = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(() => harness.Hook(index % 2 == 0 ? "claude" : "codex"))));
        foreach (var run in runs)
        {
            Assert.True(run is { ExitCode: 0, Output: "{}", StdoutClosed: true }, run.Message);
        }
        await Task.Delay(1500);
        var fake = Assert.Single(harness.Fakes());
        Assert.True(harness.Alive(fake.Pid));
        Assert.Equal(12, harness.Captures());
    }

    [Fact]
    public void AnAttemptIsClaimedOncePerIntervalAcrossABoundary()
    {
        using var harness = new Harness(record: false);
        var length = HookTrayStart.AttemptInterval;
        var origin = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        origin = new DateTimeOffset(origin.UtcTicks - origin.UtcTicks % length.Ticks, TimeSpan.Zero);
        var late = origin + length - TimeSpan.FromSeconds(5);
        Assert.True(HookTrayStart.TryClaimAttempt(harness.Root, late));
        Assert.False(HookTrayStart.TryClaimAttempt(harness.Root, late + TimeSpan.FromSeconds(1)));
        // The next interval began 5 seconds after the attempt, which is still inside the bound.
        Assert.False(HookTrayStart.TryClaimAttempt(harness.Root, origin + length + TimeSpan.FromSeconds(1)));
        // A full interval after the attempt a hook may try again.
        Assert.True(HookTrayStart.TryClaimAttempt(harness.Root, late + length + TimeSpan.FromSeconds(1)));
        // Old interval records are pruned; the directory stays bounded.
        Assert.True(Directory.GetFiles(HookTrayStart.StateDirectory(harness.Root), "attempt-*.lock").Length <= 3);
    }

    [Fact]
    public async Task ManyConcurrentClaimsInOneIntervalHaveOneWinner()
    {
        using var harness = new Harness(record: false);
        var now = new DateTimeOffset(2031, 1, 1, 0, 0, 3, TimeSpan.Zero);
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => HookTrayStart.TryClaimAttempt(harness.Root, now))));
        Assert.Equal(1, results.Count(won => won));
    }

    // ---- Diagnostics ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task DoctorReportsAvailabilityTheLatestAttemptAndAUserStop()
    {
        using var harness = new Harness();
        string Report() => System.Text.Json.JsonSerializer.Serialize(new HookStartReporter(harness.Workspace.Paths).Report());
        Assert.Contains("\"Available\":true", Report());
        Assert.Contains("\"UserStopSuppressesInThisSignIn\":false", Report());
        await harness.Hook("claude");
        await harness.WaitForFakes(1);
        Assert.Contains("\"Result\":\"Started\"", Report());
        HookTrayStart.RecordUserStop(harness.Root);
        Assert.Contains("\"UserStopSuppressesInThisSignIn\":true", Report());
        Assert.Contains("\"UserStopRecord\":\"this sign-in\"", Report());
        File.WriteAllText(HookTrayStart.UserStopRecord(harness.Root), "luid:1/1");
        Assert.Contains("\"UserStopRecord\":\"from another sign-in (stale, ignored)\"", Report());
    }

    // ---- DEV-060: ANALYSIS-4700, 4701, 4702, 4708, 4709 ---------------------------------------------------------------------

    private const string TestFolder = "BellTowerTest-DEV060";

    private static void Mode(Harness harness, string mode) => File.WriteAllText(Path.Combine(harness.LauncherDirectory, "schtasks-mode.txt"), mode);

    private static string[] SchtasksCalls(Harness harness)
    {
        var path = Path.Combine(harness.LauncherDirectory, "schtasks-calls.txt");
        return File.Exists(path) ? File.ReadAllLines(path) : [];
    }

    // 4700: the hook never waits for schtasks. A request still running after the short bound is pending, not a failure.
    [Fact]
    public void TheTaskRequestNeverBlocksTheHookAndAPendingRequestIsNotAFailure()
    {
        using var harness = new Harness();
        HookStartTask.Record(harness.Root, HookStartTask.TaskPath(harness.Root, TestFolder));
        Mode(harness, "slow");
        var watch = Stopwatch.StartNew();
        var (outcome, detail) = HookStartTask.Run(harness.Root, harness.Launcher);
        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(1500), watch.Elapsed.ToString());
        Assert.Equal(HookStartOutcome.Started, outcome);
        Assert.Equal(HookStartTask.RoutePending, detail);

        Mode(harness, "ok");
        Assert.Equal((HookStartOutcome.Started, HookStartTask.RouteTask), HookStartTask.Run(harness.Root, harness.Launcher));
        // A refusal that arrives inside the bound is a failure with its exit code.
        Mode(harness, "fail");
        Assert.Equal((HookStartOutcome.Failed, "task-run-exit:1"), HookStartTask.Run(harness.Root, harness.Launcher));
        // The stand-in from the first, slow call is still ending; let it finish before the folder is removed.
        Thread.Sleep(2000);
    }

    // 4701: only a record of the exact Bell Tower shape for this data directory is ever used or removed.
    [Theory]
    [InlineData("\\BellTowerTest-DEV060\\HookStart-0123456789abcdef", false)]
    [InlineData("\\X\\HookStart-0123456789abcdef\" /s attacker /u x", false)]
    [InlineData("\\X\" /tn \"\\Microsoft\\Windows\\Defrag\\ScheduledDefrag", false)]
    [InlineData("\\HookStart-0123456789abcdef", false)]
    [InlineData("\\A B\\HookStart-0123456789abcdef", false)]
    [InlineData("\\Microsoft\\Windows\\Defrag\\ScheduledDefrag", false)]
    [InlineData("HookStart-0123456789abcdef", false)]
    [InlineData("", false)]
    public void AnInvalidTaskRecordIsNeverUsed(string record, bool expected)
    {
        using var harness = new Harness();
        Directory.CreateDirectory(HookTrayStart.StateDirectory(harness.Root));
        File.WriteAllText(HookStartTask.RecordPath(harness.Root), record);
        Assert.Equal(expected, HookStartTask.Recorded(harness.Root) is not null);
        Assert.Equal("invalid", HookStartTask.RecordState(harness.Root));
        // The hook never starts schtasks for it, and removal plans no task action for it.
        Assert.Equal((HookStartOutcome.Failed, "no-task-recorded"), HookStartTask.Run(harness.Root, harness.Launcher));
        Assert.Empty(SchtasksCalls(harness));
    }

    [Fact]
    public void ARecordOfTheExactShapeForThisDataDirectoryIsAcceptedAndAnotherDirectorysIsNot()
    {
        using var harness = new Harness();
        using var other = new TestWorkspace();
        var own = HookStartTask.TaskPath(harness.Root, TestFolder);
        HookStartTask.Record(harness.Root, own);
        Assert.Equal(own, HookStartTask.Recorded(harness.Root));
        Assert.Equal("valid", HookStartTask.RecordState(harness.Root));
        HookStartTask.Record(harness.Root, HookStartTask.TaskPath(other.Paths.Root, TestFolder));
        Assert.Null(HookStartTask.Recorded(harness.Root));
        Assert.Equal("invalid", HookStartTask.RecordState(harness.Root));
        Assert.Contains("not a Bell Tower task", BellTower.IO.Json.Encode(new HookStartReporter(harness.Workspace.Paths).Report()), StringComparison.Ordinal);
        Assert.Equal((HookStartOutcome.Failed, "no-task-recorded"), HookStartTask.Run(harness.Root, harness.Launcher));
        Assert.Empty(SchtasksCalls(harness));
    }

    // 4702: the marker is an environment variable, so a payload that rejects unknown options (the stand-in accepts exactly the old
    // arguments) still starts, and the start is recorded as the success it is.
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AHookStartAgainstAPayloadThatRejectsUnknownOptionsStillStartsTheTray(string provider)
    {
        using var harness = new Harness();
        var run = await harness.Hook(provider);
        Assert.True(run is { ExitCode: 0, Output: "{}" }, run.Message);
        if (HookTrayStart.ShellProblem() is not null)
        {
            Assert.Empty(harness.Fakes());
            return;
        }
        var fake = Assert.Single(await harness.WaitForFakes(1));
        Assert.Equal($"tray|--data-dir|{harness.Root}", fake.Arguments);
        Assert.Contains(HookTrayStart.HookStartVariable, fake.Environment);
        Assert.Equal((HookStartOutcome.Started, TrayProcess.RouteShell), (Enum.Parse<HookStartOutcome>(HookTrayStart.LastAttempt(harness.Root)!.Value.Outcome), HookTrayStart.LastAttempt(harness.Root)!.Value.Detail));
    }

    [Fact]
    public void TheEnvironmentBlockCopyAddsTheMarkerOnceKeepsTheOrderAndTheTrayRemovesIt()
    {
        var block = System.Runtime.InteropServices.Marshal.StringToHGlobalUni("Zeta=1\0alpha=2\0Path=C:\\x\0\0");
        var copy = IntPtr.Zero;
        try
        {
            copy = TrayProcess.WithVariable(block, HookTrayStart.HookStartVariable, "1");
            var entries = new List<string>();
            for (var offset = 0; ;)
            {
                var entry = System.Runtime.InteropServices.Marshal.PtrToStringUni(copy + offset * sizeof(char)) ?? "";
                if (entry.Length == 0)
                {
                    break;
                }
                entries.Add(entry);
                offset += entry.Length + 1;
            }
            Assert.Equal(["alpha=2", "BELLTOWER_HOOK_START=1", "Path=C:\\x", "Zeta=1"], entries);
            // An existing value is replaced, never duplicated.
            var again = TrayProcess.WithVariable(copy, HookTrayStart.HookStartVariable, "1");
            var second = new List<string>();
            for (var offset = 0; ;)
            {
                var entry = System.Runtime.InteropServices.Marshal.PtrToStringUni(again + offset * sizeof(char)) ?? "";
                if (entry.Length == 0)
                {
                    break;
                }
                second.Add(entry);
                offset += entry.Length + 1;
            }
            Assert.Equal(entries, second);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(again);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(block);
            if (copy != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(copy);
            }
        }
        Environment.SetEnvironmentVariable(HookTrayStart.HookStartVariable, "1");
        Assert.True(TrayJobGuard.TakeMarker());
        Assert.Null(Environment.GetEnvironmentVariable(HookTrayStart.HookStartVariable));
        Assert.False(TrayJobGuard.TakeMarker());
    }

    // 4708: a hook-started tray that refused its job does not keep the tray down while a fallback exists.
    [Fact]
    public void ARefusalNewerThanTheLastAttemptRoutesTheNextHookToTheTaskOrFailsWithAStatedReason()
    {
        using var harness = new Harness();
        var shellStarts = 0;
        (HookStartOutcome Outcome, string Detail) Shell(string launcher, string root)
        {
            shellStarts++;
            return (HookStartOutcome.Started, TrayProcess.RouteShell);
        }
        var tasks = 0;
        (HookStartOutcome Outcome, string Detail) RunTask(string root)
        {
            tasks++;
            return (HookStartOutcome.Started, HookStartTask.RouteTask);
        }
        var t0 = DateTimeOffset.UtcNow;
        Assert.Equal(HookStartOutcome.Started, HookTrayStart.TryStart(harness.Root, t0, Shell, RunTask));
        Assert.Equal(1, shellStarts);

        // The tray it started refuses its job (a kill-on-close job), recording the refusal after that attempt.
        Assert.True(TrayJobGuard.Refuses(harness.Workspace.Paths, hookStarted: true, () => 0x2000u));

        // No task recorded: nothing starts through the shell again, and the reason is stated.
        Assert.Equal(HookStartOutcome.Failed, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow.AddMinutes(5), Shell, RunTask));
        Assert.Equal("shell-route-refused", HookTrayStart.LastAttempt(harness.Root)!.Value.Detail);
        Assert.Equal(1, shellStarts);

        // A task recorded: the next hook goes straight to it.
        HookStartTask.Record(harness.Root, HookStartTask.TaskPath(harness.Root, TestFolder));
        Assert.Equal(HookStartOutcome.Started, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow.AddMinutes(6), Shell, RunTask));
        Assert.Equal(1, shellStarts);
        Assert.Equal(1, tasks);
        Assert.Equal(HookStartTask.RouteTask, HookTrayStart.LastAttempt(harness.Root)!.Value.Detail);

        // A tray that takes the instance clears the refusal; doctor no longer shows it and the shell route is used again.
        HookTrayStart.ClearJobRefusal(harness.Root);
        Assert.Null(TrayJobGuard.LastRefusal(harness.Root));
        Assert.Equal(HookStartOutcome.Started, HookTrayStart.TryStart(harness.Root, DateTimeOffset.UtcNow.AddMinutes(8), Shell, RunTask));
        Assert.Equal(2, shellStarts);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task AHookAfterARefusalNeverStartsTheShellRouteAndStatesWhy(string provider)
    {
        using var harness = new Harness();
        Directory.CreateDirectory(HookTrayStart.StateDirectory(harness.Root));
        File.WriteAllText(HookTrayStart.JobRefusalRecord(harness.Root), DateTimeOffset.UtcNow.ToString("O") + "\tkill-on-close-job");
        var run = await harness.Hook(provider);
        Assert.True(run is { ExitCode: 0, Output: "{}" }, run.Message);
        await Task.Delay(500);
        Assert.Empty(harness.Fakes());
        Assert.Equal(1, harness.Captures());
        Assert.Equal("shell-route-refused", HookTrayStart.LastAttempt(harness.Root)!.Value.Detail);
    }

    // 4709: an access failure on the shell is reported as such.
    [Fact]
    public void AShellOpenFailureIsReportedWithItsErrorNotAsAnUnverifiedImage()
    {
        var facts = GoodShell() with { OpenError = 5, Image = null };
        Assert.Equal("shell-open:5", DesktopShell.Problem(facts));
        var opened = DesktopShell.Open(() => (IntPtr.Zero, facts));
        Assert.Equal("shell-open:5", opened.Problem);
    }
}

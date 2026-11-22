using System.Diagnostics;
using BellTower.Diagnostics;
using BellTower.Cli;
using BellTower.IO;
using BellTower.Protocol;
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
        Assert.Equal($"tray|--data-dir|{harness.Root}", fake.Arguments);
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

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task PartialBreakawayInsideNestedKillOnCloseProviderJobsKeepsCaptureAndDoesNotStartTray(string provider)
    {
        using var harness = new Harness();

        var run = await harness.HookInNestedKillOnCloseJob(provider);

        Assert.True(run is { ExitCode: 0, Output: "{}", Error: "", StdoutClosed: true }, run.Message);
        Assert.True(run.Elapsed < TimeSpan.FromSeconds(20), run.Message);
        Assert.Equal(1, harness.Captures());
        Assert.Empty(harness.Fakes());
        var attempt = HookTrayStart.LastAttempt(harness.Root);
        Assert.True(attempt.HasValue);
        Assert.Equal(nameof(HookStartOutcome.Failed), attempt.Value.Outcome);
        Assert.Equal("provider-job-breakaway-incomplete", attempt.Value.Detail);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ImmediateBreakawayRefusalInsideKillOnCloseProviderJobKeepsCaptureAndDoesNotStartTray(string provider)
    {
        using var harness = new Harness();

        var run = await harness.HookInKillOnCloseJob(provider);

        Assert.True(run is { ExitCode: 0, Output: "{}", Error: "", StdoutClosed: true }, run.Message);
        Assert.Equal(1, harness.Captures());
        Assert.Empty(harness.Fakes());
        var attempt = HookTrayStart.LastAttempt(harness.Root);
        Assert.True(attempt.HasValue);
        Assert.Equal(nameof(HookStartOutcome.Failed), attempt.Value.Outcome);
        Assert.Equal("provider-job-breakaway-refused", attempt.Value.Detail);
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

    [Theory]
    [InlineData(1, false, 1)]
    [InlineData(1, true, 0)]
    [InlineData(2, true, 2)]
    [InlineData(2, false, 0)]
    [InlineData(3, false, 1)]
    [InlineData(3, true, 0)]
    [InlineData(null, false, 0)]
    [InlineData(1, null, 0)]
    public void LaunchTokenSelectionRequiresKnownNonElevatedOrVerifiedLinkedLimitedToken(int? elevationType, bool? elevated, int expected)
    {
        Assert.Equal((TrayProcess.LaunchTokenChoice)expected, TrayProcess.SelectLaunchToken(elevationType, elevated));
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
}

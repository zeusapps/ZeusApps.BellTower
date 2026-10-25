using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Scripts;

// Fault, retry and synchronization cases for install-dev.ps1 (ANALYSIS-200/201). Interruptions end only the exact
// installer process these tests started; the disposable barriers are opt-in files under the test workspace.
public sealed partial class InstallDevScriptTests
{
    // Resource ownership: the extracted-function harness injects acquisition, validation and disposal faults into the
    // installer's own functions, then proves on the same still-running thread and from this process that nothing is held.
    [Theory]
    [InlineData("commit-lock-throws-state", "injected acquisition failure for state")]
    [InlineData("commit-lock-throws-presentation", "injected acquisition failure for presentation")]
    [InlineData("commit-guard-throws", "injected guard open failure")]
    [InlineData("commit-validation-fails", "record changed bytes")]
    [InlineData("commit-dispose-fails", "record changed bytes")]
    [InlineData("commit-dispose-fails-only", "injected disposal failure for state")]
    [InlineData("commit-busy-retry", null)]
    [InlineData("commit-stale-retry", null)]
    [InlineData("switch-snapshot-lock-throws", "injected acquisition failure for presentation")]
    [InlineData("switch-snapshot-backup-fails", "already exists")]
    [InlineData("pointer-stale-retry", null)]
    [InlineData("pointer-mutation-denied", null)]
    [InlineData("pointer-drift-after-boundary", "record changed bytes")]
    [InlineData("pointer-guard-throws", "injected guard open failure")]
    [InlineData("pointer-dispose-fails", "injected pointer step failure")]
    [InlineData("candidate-stop-activation-changes", "live candidate tray activation differs")]
    [InlineData("candidate-stop-control", null)]
    [InlineData("candidate-live-activation", "live candidate tray activation differs")]
    [InlineData("candidate-receipt-activation", "receipt is bound to a different version or activation")]
    [InlineData("candidate-data-directory", "different data directory")]
    [InlineData("candidate-unrecorded-activation", "activation was not recorded")]
    [InlineData("tray-classification-ended", null)]
    [InlineData("tray-classification-reused", null)]
    [InlineData("tray-classification-unreadable-live", "could not be safely classified")]
    public void InstallerFunctionsReleaseEveryResourceAndKeepThePrimaryError(string scenario, string? expectedError)
    {
        var root = Path.Combine(workspace.Root, "harness-" + scenario);
        Directory.CreateDirectory(root);
        var start = ShellStart("-File", Path.Combine(ProjectRoot(), "tests", "BellTower.Tests", "Scripts", "InstallDevFunctionHarness.ps1"),
            "-Scenario", scenario, "-Root", root, "-AssemblyDirectory", AppContext.BaseDirectory, "-Installer", Path.Combine(ProjectRoot(), "scripts", "install-dev.ps1"));
        using var harness = Process.Start(start)!;
        var output = harness.StandardOutput.ReadToEndAsync();
        var error = harness.StandardError.ReadToEndAsync();
        try
        {
            var resultPath = Path.Combine(root, "result.json");
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!File.Exists(resultPath) && !harness.HasExited && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
            Assert.True(File.Exists(resultPath), "Harness produced no result: " + (harness.HasExited ? Collect(output, error) : "still running"));
            using var result = JsonDocument.Parse(File.ReadAllText(resultPath));
            var text = result.RootElement.ToString();
            var actualError = result.RootElement.GetProperty("error").ValueKind == JsonValueKind.Null ? null : result.RootElement.GetProperty("error").GetString();
            if (expectedError is null)
            {
                Assert.True(actualError is null, text);
            }
            else
            {
                Assert.True(actualError?.Contains(expectedError, StringComparison.Ordinal) == true, text);
            }
            foreach (var check in result.RootElement.GetProperty("checks").EnumerateObject())
            {
                Assert.True(check.Value.GetBoolean(), check.Name + ": " + text);
            }
            Assert.Equal(0, result.RootElement.GetProperty("held").GetArrayLength());
            foreach (var observation in result.RootElement.GetProperty("boundaryObservations").EnumerateArray())
            {
                // Renewal, drain and retry delays never run while a data lock, the launch gate or a record guard is held.
                Assert.True(observation.GetProperty("held").GetArrayLength() == 0, text);
            }

            // The harness host is still alive, so success here is release, not abandonment by process exit.
            Assert.False(harness.HasExited, "The harness exited before the cross-process acquisition check.");
            var names = result.RootElement.GetProperty("lockNames").EnumerateObject().Select(name => name.Value.GetString()!)
                .Append(result.RootElement.GetProperty("launchGate").GetString()!);
            foreach (var name in names)
            {
                using var mutex = new Mutex(false, name);
                Assert.True(mutex.WaitOne(0), "Still held by the harness: " + name);
                mutex.ReleaseMutex();
            }
            foreach (var record in result.RootElement.GetProperty("records").EnumerateArray().Select(item => item.GetString()!).Where(File.Exists))
            {
                using var exclusive = File.Open(record, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        }
        finally
        {
            File.WriteAllText(Path.Combine(root, "exit.release"), "done");
            if (!harness.WaitForExit(30000))
            {
                harness.Kill(true);
                harness.WaitForExit();
            }
        }
    }

    [Theory]
    [InlineData("restart", false)]
    [InlineData("pending", true)]
    public void SwitchRevalidatesExactRecordsAfterWaitingForTheLaunchGate(string marker, bool unknownField)
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        var selection = File.ReadAllBytes(SelectionPath);
        var barriers = Barriers("switch-before-selection.release", "hold-switch-selection-before-gate");
        var drifted = Array.Empty<byte>();
        var target = marker == "restart" ? RestartPath : PendingPath;

        var installer = StartRun("Switch", package, manifest, barriers);
        WaitForBarrier(barriers, "switch-selection-before-gate", installer);
        using (new InstallationGate(installationRoot))
        {
            // The installer has passed its outside boundary and now blocks on the launch gate this test holds.
            ReleaseBarrier(barriers, "switch-selection-before-gate");
            Thread.Sleep(1000);
            drifted = Drift(target, unknownField);
        }
        var result = installer.Finish();

        Assert.False(result.Succeeded, result.Output);
        Assert.Contains("operation coordination record changed", Unwrap(result.Output));
        Assert.Equal(selection, File.ReadAllBytes(SelectionPath));
        Assert.Equal(drifted, File.ReadAllBytes(target));
        Assert.True(File.Exists(PendingPath) && File.Exists(RestartPath));
        Assert.Equal("ready-to-select", State(manifest));
    }

    [Fact]
    public void RollbackRevalidatesRecordsAfterTheGateWaitAndARetryFinishesOnlyThePointer()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var original = File.ReadAllBytes(SelectionPath);
        var originalState = File.ReadAllBytes(Path.Combine(dataDirectory, "state.json"));
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var dev = File.ReadAllBytes(SelectionPath);
        File.WriteAllText(Path.Combine(dataDirectory, "state.json"), "{\"schemaVersion\":1,\"sessions\":{\"dev\":{}}}");
        var barriers = Barriers("rollback-before-selection.release", "hold-rollback-selection-before-gate");
        byte[] restartBytes;

        var installer = StartRun("Rollback", package, manifest, barriers);
        WaitForBarrier(barriers, "rollback-selection-before-gate", installer);
        using (new InstallationGate(installationRoot))
        {
            ReleaseBarrier(barriers, "rollback-selection-before-gate");
            Thread.Sleep(1000);
            restartBytes = File.ReadAllBytes(RestartPath);
            Drift(RestartPath, false);
        }
        var refused = installer.Finish();

        Assert.False(refused.Succeeded, refused.Output);
        Assert.Contains("operation coordination record changed", Unwrap(refused.Output));
        Assert.Equal(dev, File.ReadAllBytes(SelectionPath));
        Assert.Equal(originalState, File.ReadAllBytes(Path.Combine(dataDirectory, "state.json")));
        Assert.Equal("restored-monitoring-candidate-pointer", State(manifest));
        Assert.False(HasIntent(manifest));

        // Put the exact records back: the same manifest must finish the pointer without restoring monitoring again.
        File.WriteAllBytes(RestartPath, restartBytes);
        var monitoring = MonitoringStamp(manifest);
        var retried = Run("Rollback", package, manifest);
        Assert.True(retried.Succeeded, retried.Output);
        Assert.Equal(original, File.ReadAllBytes(SelectionPath));
        Assert.Equal(monitoring, MonitoringStamp(manifest));
        Assert.Equal("running-original", State(manifest));
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
    }

    [Fact]
    public void RollbackResumedAfterRestoredMonitoringSelectsTheOriginalPointerWithoutRestoringMonitoringAgain()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var original = File.ReadAllBytes(SelectionPath);
        var originalState = File.ReadAllBytes(Path.Combine(dataDirectory, "state.json"));
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        File.WriteAllText(Path.Combine(dataDirectory, "state.json"), "{\"schemaVersion\":1,\"sessions\":{\"dev\":{}}}");
        var barriers = Barriers();

        var installer = StartRun("Rollback", package, manifest, barriers);
        WaitForBarrier(barriers, "rollback-before-selection", installer);
        installer.Interrupt();

        Assert.Equal("restored-monitoring-candidate-pointer", State(manifest));
        Assert.NotEqual(original, File.ReadAllBytes(SelectionPath));
        var monitoring = MonitoringStamp(manifest);

        var retried = Run("Rollback", package, manifest);
        Assert.True(retried.Succeeded, retried.Output);
        Assert.Equal(original, File.ReadAllBytes(SelectionPath));
        Assert.Equal(originalState, File.ReadAllBytes(Path.Combine(dataDirectory, "state.json")));
        Assert.Equal(monitoring, MonitoringStamp(manifest));
        Assert.Equal("running-original", State(manifest));
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
    }

    [Theory]
    [InlineData("rollback-pointer-intent-saved", true, true)]
    [InlineData("rollback-pointer-written", false, true)]
    [InlineData("rollback-pointer-phase-saved", false, false)]
    public void RollbackRecoversEveryPointerPublicationInterruption(string barrier, bool devPointer, bool intent)
    {
        var package = BuildPackage("0.1.9001", Marked());
        var original = File.ReadAllBytes(SelectionPath);
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var dev = File.ReadAllBytes(SelectionPath);
        var barriers = Barriers("rollback-before-selection.release", "hold-" + barrier);

        var installer = StartRun("Rollback", package, manifest, barriers);
        WaitForBarrier(barriers, barrier, installer);
        installer.Interrupt();

        Assert.Equal(devPointer ? dev : original, File.ReadAllBytes(SelectionPath));
        Assert.Equal(intent, HasIntent(manifest));
        Assert.Equal(intent ? "restored-monitoring-candidate-pointer" : "restored-monitoring-snapshot", State(manifest));
        var monitoring = MonitoringStamp(manifest);

        var retried = Run("Rollback", package, manifest);
        Assert.True(retried.Succeeded, retried.Output);
        Assert.Equal(original, File.ReadAllBytes(SelectionPath));
        Assert.Equal(monitoring, MonitoringStamp(manifest));
        Assert.False(HasIntent(manifest));
        Assert.Equal("running-original", State(manifest));
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
    }

    [Fact]
    public void RollbackOfASwitchInterruptedBeforeThePointerWriteFinishesWithoutRecordsAndLeavesTheSelection()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        var selection = File.ReadAllBytes(SelectionPath);
        var barriers = Barriers();

        var installer = StartRun("Switch", package, manifest, barriers);
        WaitForBarrier(barriers, "switch-before-selection", installer);
        installer.Interrupt();
        Assert.Equal(selection, File.ReadAllBytes(SelectionPath));
        Assert.True(File.Exists(PendingPath) && File.Exists(RestartPath));

        var result = Run("Rollback", package, manifest);

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal("running-original", State(manifest));
        Assert.Equal(selection, File.ReadAllBytes(SelectionPath));
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
    }

    [Fact]
    public void RollbackStillRefusesWhenItsRecordsDisappearOnAPathThatKeepsThem()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var dev = File.ReadAllBytes(SelectionPath);
        var barriers = Barriers("rollback-before-selection.release", "hold-rollback-selection-before-gate");
        var restartBytes = Array.Empty<byte>();

        var installer = StartRun("Rollback", package, manifest, barriers);
        WaitForBarrier(barriers, "rollback-selection-before-gate", installer);
        using (new InstallationGate(installationRoot))
        {
            ReleaseBarrier(barriers, "rollback-selection-before-gate");
            Thread.Sleep(1000);
            restartBytes = File.ReadAllBytes(RestartPath);
            File.Delete(RestartPath);
        }
        var refused = installer.Finish();

        Assert.False(refused.Succeeded, refused.Output);
        Assert.True(Unwrap(refused.Output).Contains("Could not find file", StringComparison.Ordinal) && refused.Output.Contains("restart.json", StringComparison.Ordinal), refused.Output);
        Assert.NotEqual("running-original", State(manifest));
        Assert.True(restartBytes.Length > 0);
        Assert.True(File.Exists(PendingPath));
        Assert.Equal(dev, File.ReadAllBytes(SelectionPath));
    }

    // After the original pointer was committed but before its phase was saved, only the exact recorded intent may
    // complete the operation; every drifted binding is refused before any record, selection or manifest write.
    [Theory]
    [InlineData("no-intent", "no pointer transition was recorded")]
    [InlineData("source-hash", "failed=source")]
    [InlineData("destination-hash", "failed=destination")]
    [InlineData("snapshot-hash", "failed=snapshot")]
    [InlineData("operation", "failed=operation")]
    [InlineData("phase", "failed=phase")]
    [InlineData("source-activation", "failed=source")]
    [InlineData("destination-previous", "failed=destination")]
    [InlineData("publication", "failed=publication")]
    [InlineData("live-owner", "failed=owner")]
    [InlineData("unrelated-selection", "matches neither side")]
    [InlineData("marker-bytes", "changed bytes")]
    public void RollbackRefusesAPointerIntentThatIsNotExactlyBound(string drift, string expected)
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var barriers = Barriers("rollback-before-selection.release", "hold-rollback-pointer-written");
        var installer = StartRun("Rollback", package, manifest, barriers);
        WaitForBarrier(barriers, "rollback-pointer-written", installer);
        installer.Interrupt();
        Assert.True(HasIntent(manifest));

        var document = JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
        var intent = document["rollbackPointerTransition"]!.AsObject();
        switch (drift)
        {
            case "no-intent":
                document.Remove("rollbackPointerTransition");
                break;
            case "source-hash":
                intent["sourceSelectionSha256"] = new string('f', 64);
                break;
            case "destination-hash":
                intent["destinationSelectionSha256"] = new string('f', 64);
                break;
            case "snapshot-hash":
                intent["snapshotSelectionSha256"] = new string('f', 64);
                break;
            case "operation":
                intent["operationId"] = Guid.NewGuid().ToString("N");
                break;
            case "phase":
                intent["phase"] = "Switch";
                break;
            case "source-activation":
                intent["sourceSelection"]!["activation"] = Guid.NewGuid().ToString("N");
                break;
            case "destination-previous":
                intent["destinationSelection"]!["previous"] = "0.1.7";
                break;
            case "publication":
                intent["pendingHash"] = new string('e', 64);
                break;
            case "live-owner":
                BindRecordsToLiveOwner(document);
                break;
            case "unrelated-selection":
                DistributionJson.Write(SelectionPath, new VersionSelection("0.1.8", null, Guid.NewGuid().ToString("N")));
                break;
            case "marker-bytes":
                Drift(RestartPath, false);
                break;
        }
        File.WriteAllText(manifest, document.ToJsonString());
        var before = new[] { File.ReadAllBytes(manifest), File.ReadAllBytes(SelectionPath), File.ReadAllBytes(PendingPath), File.ReadAllBytes(RestartPath) };

        var result = Run("Rollback", package, manifest);

        Assert.False(result.Succeeded, result.Output);
        Assert.Contains(expected, Unwrap(result.Output));
        var after = new[] { File.ReadAllBytes(manifest), File.ReadAllBytes(SelectionPath), File.ReadAllBytes(PendingPath), File.ReadAllBytes(RestartPath) };
        for (var index = 0; index < before.Length; index++)
        {
            Assert.Equal(before[index], after[index]);
        }
    }

    private static string Collect(Task<string> output, Task<string> error) => output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();

    private string SelectionPath => Path.Combine(installationRoot, "current.json");
    private string PendingPath => Path.Combine(installationRoot, "updates", "pending.json");
    private string RestartPath => UpdateTransaction.Marker(new DataPaths(dataDirectory));

    private void BindRecordsToLiveOwner(JsonObject document)
    {
        // A consistent pair, manifest expectation and intent whose owner is this live test process.
        var plan = JsonNode.Parse(File.ReadAllText(PendingPath))!.AsObject();
        var owner = ProcessProbe.Identify(Environment.ProcessId)!;
        plan["Owner"] = new JsonObject { ["Pid"] = owner.Pid, ["StartedUtcTicks"] = owner.StartedUtcTicks };
        var json = plan.ToJsonString();
        File.WriteAllText(PendingPath, json);
        File.WriteAllText(RestartPath, json);
        var hash = Installation.Hash(PendingPath);
        document["coordinationPlan"] = plan.DeepClone();
        document["coordinationPendingHash"] = hash;
        document["coordinationRestartHash"] = hash;
        var intent = document["rollbackPointerTransition"]!.AsObject();
        intent["plan"] = plan.DeepClone();
        intent["pendingHash"] = hash;
        intent["restartHash"] = hash;
    }

    private static byte[] Drift(string path, bool unknownField)
    {
        if (unknownField)
        {
            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            node["Foreign"] = "added";
            File.WriteAllText(path, node.ToJsonString());
        }
        else
        {
            File.AppendAllText(path, " ");
        }
        return File.ReadAllBytes(path);
    }

    private static string State(string manifest)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        return document.RootElement.GetProperty("state").GetString()!;
    }

    private static bool HasIntent(string manifest)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        return document.RootElement.TryGetProperty("rollbackPointerTransition", out _);
    }

    // Monitoring files and saved dev snapshots with their write times: equal stamps mean nothing was restored or saved again.
    private string MonitoringStamp(string manifest)
    {
        var operation = Path.GetDirectoryName(manifest)!;
        var files = new[] { Path.Combine(dataDirectory, "state.json"), Path.Combine(dataDirectory, "state.json.bak") }
            .Concat(Directory.EnumerateDirectories(operation, "dev-monitoring-aside-*").SelectMany(Directory.EnumerateFiles));
        return string.Join("|", files.Order(StringComparer.OrdinalIgnoreCase)
            .Select(file => file + "=" + Installation.Hash(file) + "@" + File.GetLastWriteTimeUtc(file).Ticks));
    }

    private string Barriers(params string[] files)
    {
        var directory = Path.Combine(workspace.Root, "barriers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var file in files)
        {
            File.WriteAllText(Path.Combine(directory, file), "test");
        }
        return directory;
    }

    private static void ReleaseBarrier(string barriers, string phase) => File.WriteAllText(Path.Combine(barriers, phase + ".release"), "test");

    private static void WaitForBarrier(string barriers, string phase, RunningInstaller installer)
    {
        var entered = Path.Combine(barriers, phase + ".entered");
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (!File.Exists(entered) && !installer.HasExited && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }
        if (!File.Exists(entered))
        {
            var output = installer.Finish();
            Assert.Fail("Installer did not reach barrier " + phase + ": " + output.Output);
        }
    }

    private RunningInstaller StartRun(string action, string package, string manifest, string barriers)
    {
        var start = ShellStart("-File", Path.Combine(ProjectRoot(), "scripts", "install-dev.ps1"),
            "-Action", action, "-Package", package, "-InstallationRoot", installationRoot, "-DataDirectory", dataDirectory, "-Workspace", privateWorkspace,
            "-AssemblyDirectory", AppContext.BaseDirectory, "-Disposable", "-NoTray", "-SkipProbe", "-AllowStaleBuild",
            "-OperationManifest", manifest, "-TestBarrierDirectory", barriers);
        return new RunningInstaller(Process.Start(start)!);
    }

    private static ProcessStartInfo ShellStart(params string[] arguments)
    {
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        var start = new ProcessStartInfo(File.Exists(shell) ? shell : "pwsh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass" }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    private sealed class RunningInstaller
    {
        private readonly Process process;
        private readonly Task<string> output;
        private readonly Task<string> error;

        public RunningInstaller(Process process)
        {
            this.process = process;
            output = process.StandardOutput.ReadToEndAsync();
            error = process.StandardError.ReadToEndAsync();
        }

        public bool HasExited => process.HasExited;

        // Ends only this test-started installer (-NoTray runs start no child process).
        public void Interrupt()
        {
            process.Kill(true);
            process.WaitForExit();
            process.Dispose();
        }

        public ScriptResult Finish()
        {
            Assert.True(process.WaitForExit(180000), "install-dev.ps1 did not finish.");
            var result = new ScriptResult(process.ExitCode, Collect(output, error));
            process.Dispose();
            return result;
        }
    }
}

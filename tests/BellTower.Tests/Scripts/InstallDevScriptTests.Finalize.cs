using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Scripts;

// Finalize and the not-ready diagnostics (recover-unready-dev-operation). Everything runs on disposable installations
// under artifacts/. A rollback whose restarted tray never confirmed readiness leaves the original selection, the restored
// monitoring snapshot and both owned records; the fixture reaches exactly that durable state by ending the installer at
// the point after the original-pointer phase was saved, because no real tray exists to fail to start in a fixture.
public sealed partial class InstallDevScriptTests
{
    private const string TrayNotReadyFacts =
        """{"observedAt":"2026-11-22T10:00:00.0000000Z","launcherAlive":true,"trayProcesses":[4242],"processScanAvailable":true,"presence":"missing","stateBytes":16777216,"stateBudgetBytes":16777216,"refreshFailures":{"logPresent":true,"count":7,"first":"2026-11-22T09:50:00.0000000Z","last":"2026-11-22T09:59:00.0000000Z","lastReason":null}}""";

    [Fact]
    public void FinalizeEndsAnUnconfirmedRollbackRemovingOnlyItsOwnRecordsAndRecordsTheStoredReason()
    {
        var (package, manifest) = StuckRollback();
        SetManifest(manifest, node => node["trayNotReady"] = JsonNode.Parse(TrayNotReadyFacts));
        var before = Snapshot(installationRoot, dataDirectory);
        Assert.True(File.Exists(PendingPath) && File.Exists(RestartPath));
        Assert.Equal("restored-monitoring-snapshot", State(manifest));

        var result = Run("Finalize", package, manifest);

        Assert.True(result.Succeeded, result.Output);
        var after = Snapshot(installationRoot, dataDirectory);
        Assert.Equal(before.Keys.Order().Where(key => !SameFile(key, PendingPath) && !SameFile(key, RestartPath)), after.Keys.Order());
        foreach (var (file, hash) in after)
        {
            Assert.Equal(before[file], hash);
        }
        Assert.Equal("0.1.8", new Installation(installationRoot).Selection().Version);
        Assert.Equal(Installation.Hash(Path.Combine(installationRoot, "current.json")), ManifestValue(manifest, "originalSelectionSha256"));
        Assert.Equal("finalized-without-tray", State(manifest));
        Assert.False(string.IsNullOrEmpty(ManifestValue(manifest, "finalizedAt")));
        var reason = ManifestValue(manifest, "finalizeReason");
        Assert.Contains("tray-not-ready", reason);
        Assert.Contains("presence missing", reason);
        Assert.Contains("16777216 of 16777216", reason);
        Assert.Contains("7 tray-refresh-failed", reason);
        using var output = JsonDocument.Parse(result.Output);
        Assert.Equal("finalized-without-tray", output.RootElement.GetProperty("status").GetString());
        Assert.False(output.RootElement.GetProperty("trayStarted").GetBoolean());
        Assert.DoesNotContain(dataDirectory, result.Output);
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        Assert.False(document.RootElement.TryGetProperty("restartLauncher", out _));
    }

    [Theory]
    [InlineData("restored-monitoring-snapshot", false)]
    [InlineData("running-original", false)]
    [InlineData("restored-monitoring-snapshot", true)]
    public void FinalizeWithoutStoredFactsUsesTheOperatorReasonAndAcceptsBothTerminalRollbackStates(string state, bool withFacts)
    {
        var (package, manifest) = StuckRollback();
        SetManifest(manifest, node =>
        {
            node["state"] = state;
            if (withFacts)
            {
                node["trayNotReady"] = JsonNode.Parse(TrayNotReadyFacts);
            }
        });

        var result = Run("Finalize", package, manifest);

        Assert.True(result.Succeeded, result.Output);
        Assert.Equal("finalized-without-tray", State(manifest));
        Assert.Equal(withFacts ? "tray-not-ready" : "operator-finalize", ManifestValue(manifest, "finalizeReason").Split(':')[0]);
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
    }

    [Theory]
    [InlineData("prepared")]
    [InlineData("interrupted-switch")]
    [InlineData("completed-switch")]
    public void FinalizeRefusesASwitchAtAnyStateAndNamesRollback(string stage)
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        if (stage == "interrupted-switch")
        {
            var barriers = Barriers();
            var installer = StartRun("Switch", package, manifest, barriers);
            WaitForBarrier(barriers, "switch-before-selection", installer);
            installer.Interrupt();
            Assert.True(File.Exists(PendingPath) && File.Exists(RestartPath));
        }
        else if (stage == "completed-switch")
        {
            Assert.True(Run("Switch", package, manifest).Succeeded);
        }

        var text = AssertRefusal(package, manifest, "Switch", "-Action Rollback", "state " + State(manifest));
        Assert.Contains("-Action Rollback", text);
    }

    [Fact]
    public void FinalizeRefusesARollbackWhoseMonitoringOrPointerStepIsIncomplete()
    {
        var (package, manifest) = StuckRollback("rollback-pointer-intent-saved");
        Assert.Equal("restored-monitoring-candidate-pointer", State(manifest));

        AssertRefusal(package, manifest, "incomplete", "-Action Rollback");
    }

    [Fact]
    public void FinalizeRefusesASelectionThatDiffersFromTheRecordedDestination()
    {
        var (package, manifest) = StuckRollback();
        AddPayload(installationRoot, "0.1.7", null);
        new SelectionWriter(installationRoot).Write(new VersionSelection("0.1.7"));

        var text = AssertRefusal(package, manifest, "selection differs", "Expected sha256 " + ManifestValue(manifest, "originalSelectionSha256"));
        Assert.Contains("actual " + Installation.Hash(SelectionPath), text);
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("pending")]
    [InlineData("both")]
    [InlineData("missing-restart")]
    public void FinalizeRefusesCoordinationRecordsThatAreForeignOrAProductUpdates(string drift)
    {
        var (package, manifest) = StuckRollback();
        var foreign = new UpdatePlan(Guid.NewGuid().ToString("N"), new string('c', 64), installationRoot, dataDirectory,
            "0.1.8", "0.1.9", new string('d', 64), DateTimeOffset.UtcNow, ProcessProbe.Identify(Environment.ProcessId)!, true);
        if (drift is "restart" or "both")
        {
            DistributionJson.Write(RestartPath, foreign);
        }
        if (drift is "pending" or "both")
        {
            DistributionJson.Write(PendingPath, foreign);
        }
        if (drift == "missing-restart")
        {
            File.Delete(RestartPath);
        }

        AssertRefusal(package, manifest, "docs/releases.md");
        Assert.Equal("restored-monitoring-snapshot", State(manifest));
    }

    [Fact]
    public void FinalizeRefusesWhileATrayHoldsTheTrayInstanceObjectAndStopsNothing()
    {
        var (package, manifest) = StuckRollback();
        using var fake = FakeTray();

        var text = AssertRefusal(package, manifest, "running or starting", "PID " + Environment.ProcessId, "tray menu", "tray-instance object");

        Assert.DoesNotContain("exit.request", text);
        Assert.False(File.Exists(Path.Combine(dataDirectory, "exit.request")));
        Assert.False(Process.GetProcessById(Environment.ProcessId).HasExited);
    }

    [Fact]
    public void FinalizeRefusesWhileATrayStartIsInProgressUnderTheReservation()
    {
        var (package, manifest) = StuckRollback();
        // A tray start in progress: a process of the installation's payload that runs the tray command for this data directory
        // but holds no tray-instance object and has written no presence yet.
        var payload = Path.Combine(installationRoot, "versions", "0.1.7");
        Directory.CreateDirectory(payload);
        var image = Path.Combine(payload, "bell-tower.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), image);
        var start = new ProcessStartInfo(image)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "tray", "--data-dir", dataDirectory })
        {
            start.ArgumentList.Add(argument);
        }
        using var starting = Process.Start(start)!;
        try
        {
            var before = Snapshot(installationRoot, dataDirectory);

            var text = AssertRefusal(package, manifest, "running or starting", "PID " + starting.Id, "tray menu");

            Assert.False(starting.HasExited);
            Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
        }
        finally
        {
            // Ends only the stand-in process this test started.
            starting.Kill(true);
            starting.WaitForExit();
        }
    }

    [Fact]
    public void FinalizeRefusesChangedOriginalPayloadBytes()
    {
        var (package, manifest) = StuckRollback();
        File.AppendAllText(Path.Combine(installationRoot, "versions", "0.1.8", "README.md"), "x");

        AssertRefusal(package, manifest, "versions/0.1.8/README.md", "Expected sha256");
        Assert.True(File.Exists(PendingPath) && File.Exists(RestartPath));
    }

    [Fact]
    public void FinalizeRetryRemovesRecordsLeftByAnInterruptedFinalizeAndAFinishedOperationChangesNothing()
    {
        var (package, manifest) = StuckRollback();
        var pending = File.ReadAllBytes(PendingPath);
        var restart = File.ReadAllBytes(RestartPath);
        Assert.True(Run("Finalize", package, manifest).Succeeded);
        var finalizedAt = ManifestValue(manifest, "finalizedAt");
        var reason = ManifestValue(manifest, "finalizeReason");

        // The manifest was published first; an interruption left the owned records behind.
        File.WriteAllBytes(PendingPath, pending);
        File.WriteAllBytes(RestartPath, restart);
        var retried = Run("Finalize", package, manifest);

        Assert.True(retried.Succeeded, retried.Output);
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
        Assert.Equal("finalized-without-tray", State(manifest));
        Assert.Equal(finalizedAt, ManifestValue(manifest, "finalizedAt"));
        Assert.Equal(reason, ManifestValue(manifest, "finalizeReason"));

        var manifestBytes = File.ReadAllBytes(manifest);
        var before = Snapshot(installationRoot, dataDirectory);
        var again = Run("Finalize", package, manifest);

        Assert.True(again.Succeeded, again.Output);
        using var document = JsonDocument.Parse(again.Output);
        Assert.Equal("already-finalized", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
        Assert.Equal(manifestBytes, File.ReadAllBytes(manifest));
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("pending")]
    public void FinalizeRetryRemovesTheSingleRecordAnInterruptedRemovalLeftAndANewSwitchFollows(string remaining)
    {
        // QA-001: the removal deletes two files; an interruption between them leaves one record and a finalized manifest.
        var (package, manifest) = StuckRollback();
        var pending = File.ReadAllBytes(PendingPath);
        var restart = File.ReadAllBytes(RestartPath);
        Assert.True(Run("Finalize", package, manifest).Succeeded);
        var finalizedAt = ManifestValue(manifest, "finalizedAt");
        File.WriteAllBytes(remaining == "restart" ? RestartPath : PendingPath, remaining == "restart" ? restart : pending);
        var manifestBytes = File.ReadAllBytes(manifest);

        var retried = Run("Finalize", package, manifest);

        Assert.True(retried.Succeeded, retried.Output);
        using var document = JsonDocument.Parse(retried.Output);
        Assert.Equal(1, document.RootElement.GetProperty("removedRecords").GetInt32());
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
        Assert.Equal(manifestBytes, File.ReadAllBytes(manifest));
        Assert.Equal(finalizedAt, ManifestValue(manifest, "finalizedAt"));
        Assert.Equal("0.1.8", new Installation(installationRoot).Selection().Version);
        var next = BuildPackage("0.1.9002", Marked());
        var preflight = Run("Preflight", next);
        Assert.True(preflight.Succeeded, preflight.Output);
        Assert.True(Run("Switch", next, Manifest(preflight)).Succeeded);
    }

    [Theory]
    [InlineData("restart", "foreign")]
    [InlineData("pending", "foreign")]
    [InlineData("restart", "changed")]
    [InlineData("pending", "changed")]
    public void FinalizeRetryRefusesALoneRecordThatIsForeignOrChanged(string remaining, string kind)
    {
        var (package, manifest) = StuckRollback();
        Assert.True(Run("Finalize", package, manifest).Succeeded);
        var path = remaining == "restart" ? RestartPath : PendingPath;
        if (kind == "foreign")
        {
            DistributionJson.Write(path, new UpdatePlan(Guid.NewGuid().ToString("N"), new string('c', 64), installationRoot, dataDirectory,
                "0.1.8", "0.1.9", new string('d', 64), DateTimeOffset.UtcNow, ProcessProbe.Identify(Environment.ProcessId)!, true));
        }
        else
        {
            File.AppendAllText(path, " ");
        }
        var bytes = File.ReadAllBytes(path);

        AssertRefusal(package, manifest, "docs/releases.md");

        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void FinalizeRefusesWhileTheInstallerThatOwnsTheRecordsIsStillWaitingForItsTray()
    {
        // ANALYSIS-001: a Rollback releases the reservation and gates before it restarts the tray, yet still owns its records.
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var barriers = Barriers("rollback-before-selection.release", "hold-rollback-before-tray-restart");
        var installer = StartRun("Rollback", package, manifest, barriers);
        try
        {
            WaitForBarrier(barriers, "rollback-before-tray-restart", installer);
            Assert.Equal("restored-monitoring-snapshot", State(manifest));
            Assert.True(File.Exists(PendingPath) && File.Exists(RestartPath));

            var text = AssertRefusal(package, manifest, "PID " + installer.Id, "still running", "Wait for it");

            Assert.Contains("owns the coordination records", text);
            Assert.True(File.Exists(PendingPath) && File.Exists(RestartPath));
        }
        finally
        {
            installer.Interrupt();
        }

        // Once the owner has ended, the same record finalizes.
        var finalized = Run("Finalize", package, manifest);
        Assert.True(finalized.Succeeded, finalized.Output);
        Assert.Equal("finalized-without-tray", State(manifest));
    }

    [Fact]
    public void FinalizeDoesNotHoldTheLaunchGateWhileItDrainsTrayStarts()
    {
        // ANALYSIS-003: hooks of either provider wait on this gate; the CIM drain runs under the update gate and reservation only.
        var (package, manifest) = StuckRollback();
        var payload = Path.Combine(installationRoot, "versions", "0.1.7");
        Directory.CreateDirectory(payload);
        var image = Path.Combine(payload, "bell-tower.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), image);
        var start = new ProcessStartInfo(image) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var argument in new[] { "tray", "--data-dir", dataDirectory })
        {
            start.ArgumentList.Add(argument);
        }
        using var starting = Process.Start(start)!;
        try
        {
            var finalize = StartRun("Finalize", package, manifest, Barriers());
            var slowest = TimeSpan.Zero;
            var samples = 0;
            while (!finalize.HasExited)
            {
                var clock = Stopwatch.StartNew();
                using (new InstallationGate(installationRoot, "launch", 3000))
                {
                    samples++;
                }
                slowest = clock.Elapsed > slowest ? clock.Elapsed : slowest;
                Thread.Sleep(50);
            }
            var result = finalize.Finish();

            Assert.False(result.Succeeded, result.Output);
            Assert.Contains("PID " + starting.Id, Unwrap(result.Output));
            Assert.True(samples > 0);
            Assert.True(slowest < TimeSpan.FromSeconds(1.5), "The launch gate was held for " + slowest);
        }
        finally
        {
            starting.Kill(true);
            starting.WaitForExit();
        }
    }

    [Fact]
    public void FinalizeOfATerminalSwitchSaysPreflightAgainAndNeverNamesRollback()
    {
        // ANALYSIS-005: a Switch that failed in ready-to-select and restarted its original tray carries a dev selection hash.
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        SetManifest(manifest, node =>
        {
            node["state"] = "ready-to-select";
            node["trayRestartedAfterFailure"] = true;
            node["devSelectionSha256"] = new string('e', 64);
        });

        var text = AssertRefusal(package, manifest, "run Preflight again", "already ended");

        Assert.DoesNotContain("-Action Rollback", text);
        var rolledBack = Run("Rollback", package, manifest);
        Assert.False(rolledBack.Succeeded);
        Assert.Contains("run Preflight again", Unwrap(rolledBack.Output));
        Assert.DoesNotContain("-Action Rollback", Unwrap(rolledBack.Output));
    }

    [Fact]
    public void ASwitchOrFinalizeOfACompletedRollbackSaysPreflightAgainAndNeverNamesFinalizeOrTheTray()
    {
        // ANALYSIS-100: a finished Rollback left no coordination record, so there is nothing to finalize and no tray to quit.
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var rolledBack = Run("Rollback", package, manifest);
        Assert.True(rolledBack.Succeeded, rolledBack.Output);
        Assert.Equal("running-original", State(manifest));
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));

        var switched = Run("Switch", package, manifest);

        Assert.False(switched.Succeeded);
        var switchText = Unwrap(switched.Output);
        Assert.Contains("Run Preflight again for a new operation", switchText);
        Assert.DoesNotContain("Finalize", switchText);
        Assert.DoesNotContain("tray menu", switchText);
        var finalizeText = AssertRefusal(package, manifest, "Run Preflight again for a new operation");
        Assert.DoesNotContain("coordination record is missing", finalizeText);
        Assert.DoesNotContain("-Action Finalize", finalizeText);
        Assert.DoesNotContain("tray menu", finalizeText);
    }

    [Fact]
    public void PreflightAndSwitchNameTheOperationRecordThatOwnsTheLeftoverRecords()
    {
        // ANALYSIS-010: the maintainer is led from the unknown owner and the interrupted-update refusal to Finalize with the record.
        var (package, manifest) = StuckRollback();
        var next = BuildPackage("0.1.9002", Marked());
        var relative = Path.GetRelativePath(ProjectRoot(), manifest).Replace('\\', '/');
        var command = "-Action Finalize -OperationManifest '" + relative + "'";

        string preflightText;
        using (HoldTrayInstanceObject())
        {
            var unknown = Run("Preflight", next);
            Assert.False(unknown.Succeeded, unknown.Output);
            preflightText = Unwrap(unknown.Output);
        }
        Assert.Contains("owner is unknown", preflightText);
        Assert.Contains(command, preflightText);
        Assert.Contains("Rollback that restored the original selection", preflightText);

        var preflight = Run("Preflight", next);
        Assert.True(preflight.Succeeded, preflight.Output);
        var nextManifest = Manifest(preflight);
        var before = Snapshot(installationRoot, dataDirectory);
        var blocked = Run("Switch", next, nextManifest);

        Assert.False(blocked.Succeeded);
        var text = Unwrap(blocked.Output);
        Assert.Contains("interrupted update needs inspection", text);
        Assert.Contains(command, text);
        Assert.DoesNotContain(Path.GetRelativePath(ProjectRoot(), nextManifest).Replace('\\', '/'), text.Replace(command, string.Empty));
        Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
    }

    [Fact]
    public void AFinalizedOperationIsNotRolledBackOrSwitchedAgain()
    {
        var (package, manifest) = StuckRollback();
        Assert.True(Run("Finalize", package, manifest).Succeeded);
        var before = Snapshot(installationRoot, dataDirectory);

        var rolledBack = Run("Rollback", package, manifest);
        var switched = Run("Switch", package, manifest);

        Assert.False(rolledBack.Succeeded);
        Assert.Contains("finalized without a tray", Unwrap(rolledBack.Output));
        Assert.False(switched.Succeeded);
        Assert.Contains("Run Preflight again", Unwrap(switched.Output));
        Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
    }

    [Fact]
    public void AfterFinalizeANewPreflightAndSwitchSucceedAndReportThatNoTrayWasRestarted()
    {
        var (package, manifest) = StuckRollback();
        var next = BuildPackage("0.1.9002", Marked());
        var nextManifest = Manifest(Run("Preflight", next));
        var blocked = Run("Switch", next, nextManifest);
        Assert.False(blocked.Succeeded);
        Assert.Contains("interrupted update needs inspection", Unwrap(blocked.Output));
        Assert.Equal("0.1.8", new Installation(installationRoot).Selection().Version);

        Assert.True(Run("Finalize", package, manifest).Succeeded);
        var preflight = Run("Preflight", next);
        Assert.True(preflight.Succeeded, preflight.Output);
        var switched = Run("Switch", next, Manifest(preflight));

        Assert.True(switched.Succeeded, switched.Output);
        Assert.Equal("0.1.9002", new Installation(installationRoot).Selection().Version);
        using var document = JsonDocument.Parse(switched.Output);
        Assert.False(document.RootElement.GetProperty("trayRestarted").GetBoolean());
        Assert.Contains("no tray was restarted", document.RootElement.GetProperty("trayRestartNote").GetString());
        Assert.False(File.Exists(PendingPath) || File.Exists(RestartPath));
    }

    // The restart failure and the unknown-owner refusals run in the extracted-function harness, which stands in for the tray
    // process and its launcher; the scripted CLI cannot start a real tray inside a fixture.
    [Fact]
    public void TheSkillAndDocumentationNameTheFinalizeActionAndItsPreconditions()
    {
        var root = ProjectRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "install-dev.ps1"));
        Assert.Contains("'Finalize'", script);
        foreach (var file in new[] { ".agents/skills/install-dev-build/SKILL.md", ".claude/skills/install-dev-build/SKILL.md", "docs/development.md", "docs/releases.md" })
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert.Contains("Finalize", text);
        }
        foreach (var file in new[] { ".agents/skills/install-dev-build/SKILL.md", "docs/development.md" })
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            foreach (var precondition in new[] { "restored-monitoring-snapshot", "running-original", "finalized-without-tray", "trayNotReady" })
            {
                Assert.Contains(precondition, text);
            }
        }
    }

    private (string Package, string Manifest) StuckRollback(string barrier = "rollback-pointer-phase-saved")
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var barriers = Barriers("rollback-before-selection.release", "hold-" + barrier);
        var installer = StartRun("Rollback", package, manifest, barriers);
        WaitForBarrier(barriers, barrier, installer);
        installer.Interrupt();
        return (package, manifest);
    }

    // Every refusal names its reason and next step, prints no data-directory path, and changes nothing.
    private string AssertRefusal(string package, string manifest, string expected, params string[] more)
    {
        var before = Snapshot(installationRoot, dataDirectory);
        var manifestBytes = File.ReadAllBytes(manifest);
        var result = Run("Finalize", package, manifest);

        Assert.False(result.Succeeded, result.Output);
        var text = Unwrap(result.Output);
        Assert.Contains(expected, text);
        foreach (var item in more)
        {
            Assert.Contains(item, text);
        }
        Assert.DoesNotContain(dataDirectory, text);
        Assert.DoesNotContain("dev-data", text);
        Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
        Assert.Equal(manifestBytes, File.ReadAllBytes(manifest));
        return text;
    }

    private static bool SameFile(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static void SetManifest(string manifest, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(File.ReadAllText(manifest))!.AsObject();
        change(node);
        File.WriteAllText(manifest, node.ToJsonString());
    }

    private static string ManifestValue(string manifest, string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        return document.RootElement.GetProperty(name).GetString()!;
    }
}

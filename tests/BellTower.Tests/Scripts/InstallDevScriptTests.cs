using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using BellTower.Core;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Scripts;

// Exercises scripts/install-dev.ps1 against disposable installations under artifacts/. The native tray is not started
// (-NoTray) and the package probe is skipped (-SkipProbe); the real package cycle is recorded in the change evidence.
public sealed partial class InstallDevScriptTests : IDisposable
{
    private static readonly DateTimeOffset BuiltAt = new(2026, 10, 24, 21, 30, 0, TimeSpan.Zero);
    private readonly TestWorkspace workspace = new();
    private readonly string installationRoot;
    private readonly string dataDirectory;
    private readonly string privateWorkspace;

    public InstallDevScriptTests()
    {
        installationRoot = Path.Combine(workspace.Root, "install");
        dataDirectory = Path.Combine(workspace.Root, "dev-data");
        privateWorkspace = Path.Combine(workspace.Root, "private");
        CreateInstallation();
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, "state.json"), "{\"schemaVersion\":1,\"sessions\":{}}");
        File.WriteAllText(Path.Combine(dataDirectory, "state.json.bak"), "{\"schemaVersion\":1,\"sessions\":{}}");
    }

    public void Dispose() => workspace.Dispose();

    [Fact]
    public void PreflightVerifiesTheCandidateAndChangesNothing()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var before = Snapshot(installationRoot, dataDirectory);

        var result = Run("Preflight", package);
        Assert.True(result.Succeeded, result.Output);

        Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("preflight-passed", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("0.1.8", document.RootElement.GetProperty("originalVersion").GetString());
        Assert.Equal("0.1.9001", document.RootElement.GetProperty("candidateVersion").GetString());
        Assert.Contains("dev b74a12b", document.RootElement.GetProperty("display").GetString());
        Assert.True(File.Exists(document.RootElement.GetProperty("operationManifest").GetString()));
        Assert.Equal("0.1.8", new Installation(installationRoot).Selection().Version);
    }

    [Fact]
    public void SwitchInstallsNextToExistingPayloadsAndRollbackRestoresSelectionAndMonitoring()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var selectionBytes = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));
        var launcherBytes = File.ReadAllBytes(Path.Combine(installationRoot, "bell-tower.exe"));
        var stateBytes = File.ReadAllBytes(Path.Combine(dataDirectory, "state.json"));
        var payloadBefore = Snapshot(Path.Combine(installationRoot, "versions", "0.1.8"));
        var manifest = Manifest(Run("Preflight", package));

        var switched = Run("Switch", package, manifest);
        Assert.True(switched.Succeeded, switched.Output);
        var installation = new Installation(installationRoot);
        Assert.Equal("0.1.9001", installation.Selection().Version);
        Assert.Equal("0.1.8", installation.Selection().Previous);
        Assert.Equal("0.1.9001 dev b74a12b (2026-10-24 21:30 UTC)", installation.DisplayVersion("0.1.9001"));
        Assert.Equal(launcherBytes, File.ReadAllBytes(Path.Combine(installationRoot, "bell-tower.exe")));
        Assert.Equal(payloadBefore, Snapshot(Path.Combine(installationRoot, "versions", "0.1.8")));

        // The dev tray writes monitoring state; rollback must bring the pre-switch snapshot back.
        File.WriteAllText(Path.Combine(dataDirectory, "state.json"), "{\"schemaVersion\":1,\"sessions\":{\"dev\":{}}}");

        var rolledBack = Run("Rollback", package, manifest);
        Assert.True(rolledBack.Succeeded, rolledBack.Output);
        Assert.Equal(selectionBytes, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
        Assert.Equal(stateBytes, File.ReadAllBytes(Path.Combine(dataDirectory, "state.json")));
        Assert.Equal(launcherBytes, File.ReadAllBytes(Path.Combine(installationRoot, "bell-tower.exe")));
        Assert.Equal(payloadBefore, Snapshot(Path.Combine(installationRoot, "versions", "0.1.8")));
        Assert.True(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")));
    }

    [Fact]
    public void SwitchRefusesAChangedSelectionWithoutTouchingIt()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        AddPayload(installationRoot, "0.1.7");
        new SelectionWriter(installationRoot).Write(new VersionSelection("0.1.7"));
        var changed = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));

        var result = Run("Switch", package, manifest);
        Assert.False(result.Succeeded);
        Assert.Contains("original selection changed", result.Output);
        Assert.Equal(changed, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
        Assert.False(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")));
    }

    [Fact]
    public void SwitchRefusesForeignRestartMarkerWithoutChangingItsBytes()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        var marker = UpdateTransaction.Marker(new DataPaths(dataDirectory));
        var foreign = new UpdatePlan(Guid.NewGuid().ToString("N"), new string('c', 64), installationRoot, dataDirectory,
            "0.1.8", "0.1.9", new string('d', 64), DateTimeOffset.UtcNow, ProcessProbe.Identify(Environment.ProcessId)!, true);
        DistributionJson.Write(marker, foreign);
        var markerBytes = File.ReadAllBytes(marker);
        var selection = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));

        var result = Run("Switch", package, manifest);

        Assert.False(result.Succeeded);
        Assert.Contains("restart coordination record already exists", result.Output);
        Assert.Equal(markerBytes, File.ReadAllBytes(marker));
        Assert.Equal(selection, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
        Assert.False(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")));
    }

    [Fact]
    public void RollbackRefusesReconstructedCoordinatorRecordsWithChangedTupleAndPreservesBytes()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifestPath = Manifest(Run("Preflight", package));
        var switched = Run("Switch", package, manifestPath);
        Assert.True(switched.Succeeded, switched.Output);
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var pending = new UpdatePlan(root.GetProperty("coordinationId").GetString()!, root.GetProperty("coordinationToken").GetString()!, installationRoot,
            dataDirectory, "0.1.8", "0.1.9001", "", DateTimeOffset.UtcNow.AddMinutes(-11), new ProcessIdentity(999999, 1), false);
        DistributionJson.Write(Path.Combine(installationRoot, "updates", "pending.json"), pending);
        DistributionJson.Write(UpdateTransaction.Marker(new DataPaths(dataDirectory)), pending);
        var pendingBytes = File.ReadAllBytes(Path.Combine(installationRoot, "updates", "pending.json"));
        var restartPath = UpdateTransaction.Marker(new DataPaths(dataDirectory));
        var restartBytes = File.ReadAllBytes(restartPath);

        var rolledBack = Run("Rollback", package, manifestPath);

        Assert.False(rolledBack.Succeeded);
        Assert.Contains("complete pending/restart recovery tuple", rolledBack.Output);
        Assert.Equal(pendingBytes, File.ReadAllBytes(Path.Combine(installationRoot, "updates", "pending.json")));
        Assert.Equal(restartBytes, File.ReadAllBytes(restartPath));
        Assert.Equal("0.1.9001", new Installation(installationRoot).Selection().Version);
    }

    [Fact]
    public void SwitchRefusesATrayThatAppearedAfterPreflight()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        var selection = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));
        using var fake = FakeTray();

        var result = Run("Switch", package, manifest);
        Assert.False(result.Succeeded);
        Assert.Contains("Tray", result.Output);
        Assert.Equal(selection, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
        Assert.False(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")));
    }

    [Fact]
    public void PreflightRefusesAnUnverifiableTrayOwner()
    {
        var package = BuildPackage("0.1.9001", Marked());
        using var fake = FakeTray();
        var before = Snapshot(installationRoot, dataDirectory);

        var result = Run("Preflight", package);
        Assert.False(result.Succeeded);
        Assert.Contains("Tray", result.Output);
        Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
    }

    [Fact]
    public void RollbackRefusesAChangedDevSelection()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        var switched = Run("Switch", package, manifest);
        Assert.True(switched.Succeeded, switched.Output);
        AddPayload(installationRoot, "0.1.9", null);
        new SelectionWriter(installationRoot).Write(new VersionSelection("0.1.9", "0.1.9001"));
        var selection = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));

        var result = Run("Rollback", package, manifest);
        Assert.False(result.Succeeded);
        Assert.Contains("rollback refused", result.Output);
        Assert.Equal(selection, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
    }

    [Theory]
    [InlineData("0.1.9", true)]
    [InlineData("0.1.9", false)]
    [InlineData("0.1.9001", false)]
    public void PreflightRefusesCandidatesThatAreNotMarkedDevBuilds(string version, bool marked)
    {
        var package = BuildPackage(version, marked ? Marked() : null);
        var before = Snapshot(installationRoot, dataDirectory);

        var result = Run("Preflight", package);
        Assert.False(result.Succeeded);
        Assert.Contains(marked ? "Invalid development build metadata" : "not a marked dev build", result.Output);
        Assert.Equal(before, Snapshot(installationRoot, dataDirectory));
    }

    [Fact]
    public void PreflightRefusesAStaleBuildUnlessOverridden()
    {
        var package = BuildPackage("0.1.9001", new ReleaseBuild("dev", "aaaaaaa", false, BuiltAt, "0.1.8"));
        var stale = Run("Preflight", package, allowStale: false);
        Assert.False(stale.Succeeded);
        Assert.Contains("Stale build", stale.Output);
        Assert.True(Run("Preflight", package).Succeeded);
    }

    [Theory]
    [InlineData("Switch", true)]
    [InlineData("Switch", false)]
    [InlineData("Rollback", true)]
    [InlineData("Rollback", false)]
    public void SwitchAndRollbackRefuseActiveOrInterruptedUpdatesBeforeStoppingTray(string action, bool active)
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        var created = active ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddMinutes(-11);
        var pending = new UpdatePlan(Guid.NewGuid().ToString("N"), new string('a', 64), installationRoot, dataDirectory,
            "0.1.8", "0.1.9", new string('b', 64), created, ProcessProbe.Identify(Environment.ProcessId)!, true);
        var pendingPath = Path.Combine(installationRoot, "updates", "pending.json");
        DistributionJson.Write(pendingPath, pending);
        var originalSelection = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));
        using var fake = FakeTray();

        var result = Run(action, package, manifest);

        Assert.False(result.Succeeded);
        Assert.Contains(active ? "staged update is active" : "interrupted update needs inspection", result.Output);
        Assert.Equal(originalSelection, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
        Assert.True(File.Exists(pendingPath));
        Assert.False(File.Exists(Path.Combine(dataDirectory, "exit.request")));
        Assert.False(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")));
    }

    [Fact]
    public void PreflightRefusesDataThatTheOriginalCannotReadAfterRollback()
    {
        var package = BuildPackage("0.1.9001", Marked());
        File.WriteAllText(Path.Combine(dataDirectory, "state.json"), "{\"schemaVersion\":1,\"attachmentHealthVersion\":1}");
        // The installed 0.1.8 payload below declares attachmentHealth 0 and so cannot read health-v1 state.
        var legacy = new ReleaseContract("0.1.8", Capture: 1, AttachmentHealth: 0, Files: Files("0.1.8", Path.Combine(installationRoot, "versions", "0.1.8")));
        DistributionJson.Write(Path.Combine(installationRoot, "versions", "0.1.8", "release.json"), legacy);

        var result = Run("Preflight", package);
        Assert.False(result.Succeeded);
        Assert.Contains("health-v1 consumer", result.Output);
    }

    [Fact]
    public void TestOnlyOptionsAndPathsAreConfined()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var noDisposable = Execute("-Action", "Preflight", "-Package", package, "-InstallationRoot", installationRoot, "-DataDirectory", dataDirectory,
            "-Workspace", privateWorkspace, "-AssemblyDirectory", AppContext.BaseDirectory, "-SkipProbe");
        Assert.False(noDisposable.Succeeded);
        Assert.Contains("only for disposable", noDisposable.Output);

        var outside = Path.Combine(Path.GetTempPath(), "belltower-install-dev-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = Execute("-Action", "Preflight", "-Package", package, "-InstallationRoot", installationRoot, "-DataDirectory", dataDirectory,
                "-Workspace", outside, "-AssemblyDirectory", AppContext.BaseDirectory, "-Disposable");
            Assert.False(result.Succeeded);
            Assert.Contains("under", result.Output);
            Assert.False(Directory.Exists(outside));
        }
        finally
        {
            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, true);
            }
        }
    }

    [Fact]
    public void RetireRemovesAnIdleDevPayloadOnly()
    {
        AddPayload(installationRoot, "0.1.9001", Marked());
        AddPayload(installationRoot, "0.1.9002", Marked());
        new SelectionWriter(installationRoot).Write(new VersionSelection("0.1.9002", "0.1.8"));
        var selection = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));

        var result = Retire("0.1.9001");
        Assert.True(result.Succeeded, result.Output);
        Assert.False(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")));
        Assert.True(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9002")));
        Assert.True(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.8")));
        Assert.Equal(selection, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
    }

    [Fact]
    public void RetireRefusesSelectedPreviousRecoveryRetainedLeasedUnmarkedAndStablePayloads()
    {
        AddPayload(installationRoot, "0.1.9001", Marked());
        AddPayload(installationRoot, "0.1.9002", Marked());
        AddPayload(installationRoot, "0.1.9003", Marked());
        AddPayload(installationRoot, "0.1.10", null);
        AddPayload(installationRoot, "0.1.9", null);
        AddPayload(installationRoot, "0.1.9004", null);
        new SelectionWriter(installationRoot).Write(new VersionSelection("0.1.9002", "0.1.9001"));

        var selected = Retire("0.1.9002");
        Assert.False(selected.Succeeded);
        Assert.Contains("selected version", selected.Output);
        var previous = Retire("0.1.9001");
        Assert.False(previous.Succeeded);
        Assert.Contains("previous version", previous.Output);

        // Unmarked payloads (earlier trial builds, stable releases, even in the dev range) are always refused.
        foreach (var version in new[] { "0.1.10", "0.1.9", "0.1.8", "0.1.9004" })
        {
            var unmarked = Retire(version);
            Assert.False(unmarked.Succeeded);
            Assert.True(Unwrap(unmarked.Output).Contains("no dev marker"), unmarked.Output);
            Assert.True(Unwrap(unmarked.Output).Contains("automatic cleanup"), unmarked.Output);
        }

        // A dev payload that an unfinished (failed) update still needs for recovery stays.
        var plan = new UpdatePlan(Guid.NewGuid().ToString("N"), new string('a', 64), installationRoot, dataDirectory, "0.1.8", "0.1.9003", "", DateTimeOffset.UtcNow,
            ProcessProbe.Identify(Environment.ProcessId)!, false);
        DistributionJson.Write(UpdateTransaction.PlanPath(new Installation(installationRoot), plan.Id), plan);
        DistributionJson.Write(Path.Combine(installationRoot, "updates", plan.Id, "outcome.json"), new UpdateOutcome(plan.Id, "failed", "0.1.9003", "failed", DateTimeOffset.UtcNow));
        var retained = Retire("0.1.9003");
        Assert.False(retained.Succeeded);
        Assert.True(Unwrap(retained.Output).Contains("recovery material"), retained.Output);

        File.Delete(Path.Combine(installationRoot, "updates", plan.Id, "outcome.json"));
        File.Delete(UpdateTransaction.PlanPath(new Installation(installationRoot), plan.Id));
        new SelectionWriter(installationRoot).Write(new VersionSelection("0.1.8"));
        using (new Installation(installationRoot).Lease("0.1.9001"))
        {
            var leased = Retire("0.1.9001");
            Assert.False(leased.Succeeded);
            Assert.Contains("leased", leased.Output);
        }
        foreach (var version in new[] { "0.1.8", "0.1.9", "0.1.10", "0.1.9001", "0.1.9002", "0.1.9003", "0.1.9004" })
        {
            Assert.True(Directory.Exists(Path.Combine(installationRoot, "versions", version)), version);
        }

        // Once nothing retains it, the marked payload goes.
        Assert.True(Retire("0.1.9003").Succeeded);
        Assert.False(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9003")));
    }

    [Fact]
    public void SwitchDriftRefusalsNameTheSelectionHashPayloadFileAndTrayOwner()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        var selectionPath = Path.Combine(installationRoot, "current.json");
        var original = File.ReadAllBytes(selectionPath);
        var expected = Installation.Hash(selectionPath);

        File.AppendAllText(selectionPath, " ");
        var actual = Installation.Hash(selectionPath);
        var selection = Run("Switch", package, manifest);
        Assert.False(selection.Succeeded);
        var text = Unwrap(selection.Output);
        Assert.Contains("Expected sha256 " + expected, text);
        Assert.Contains("actual " + actual, text);
        File.WriteAllBytes(selectionPath, original);

        var readme = Path.Combine(installationRoot, "versions", "0.1.8", "README.md");
        File.AppendAllText(readme, "x");
        var changedPayload = Unwrap(Run("Switch", package, manifest).Output);
        Assert.Contains("versions/0.1.8/README.md", changedPayload);
        Assert.Contains("Expected sha256", changedPayload);
        File.WriteAllText(readme, "README.md0.1.8");

        using var fake = FakeTray();
        var owner = Unwrap(Run("Switch", package, manifest).Output);
        Assert.Contains("Tray executable does not match", owner);

    }

    [Fact]
    public void RollbackDriftRefusalNamesTheSelectionHashes()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifest = Manifest(Run("Preflight", package));
        Assert.True(Run("Switch", package, manifest).Succeeded);
        var selectionPath = Path.Combine(installationRoot, "current.json");
        var expected = Installation.Hash(selectionPath);
        File.AppendAllText(selectionPath, " ");
        var text = Unwrap(Run("Rollback", package, manifest).Output);
        Assert.Contains("Expected sha256 " + expected, text);
        Assert.Contains("actual " + Installation.Hash(selectionPath), text);
    }

    [Fact]
    public void SwitchOwnerDriftReportsPidStartTimeAndExecutableOfBothOwners()
    {
        var package = BuildPackage("0.1.9001", Marked());
        var manifestPath = Manifest(Run("Preflight", package));
        // Pretend preflight saw a different owner than the one now running: the comparison must print both.
        var record = File.ReadAllText(manifestPath);
        var recordedOwner = JsonSerializer.Serialize(new { pid = 4242, ticks = 7, version = "0.1.8", executable = @"C:\expected\bell-tower.exe" });
        var edited = record.Replace("\"originalTray\": null", "\"originalTray\": " + recordedOwner);
        Assert.NotEqual(record, edited);
        File.WriteAllText(manifestPath, edited);
        var selection = File.ReadAllBytes(Path.Combine(installationRoot, "current.json"));

        var result = Unwrap(Run("Switch", package, manifestPath).Output);
        Assert.True(result.Contains("Expected: pid 4242, started (UTC ticks) 7, C:\\expected\\bell-tower.exe", StringComparison.Ordinal), result);
        Assert.Contains("Actual: no tray running", result);
        Assert.Equal(selection, File.ReadAllBytes(Path.Combine(installationRoot, "current.json")));
    }

    private ScriptResult Run(string action, string package, string? manifest = null, bool allowStale = true)
    {
        var arguments = new List<string>
        {
            "-Action", action, "-Package", package, "-InstallationRoot", installationRoot, "-DataDirectory", dataDirectory, "-Workspace", privateWorkspace,
            "-AssemblyDirectory", AppContext.BaseDirectory, "-Disposable", "-NoTray", "-SkipProbe"
        };
        if (allowStale)
        {
            arguments.Add("-AllowStaleBuild");
        }
        if (manifest is not null)
        {
            arguments.AddRange(["-OperationManifest", manifest]);
        }
        return Execute([.. arguments]);
    }

    private ScriptResult Retire(string version, params string[] extra) =>
        Execute(["-Action", "Retire", "-Version", version, "-InstallationRoot", installationRoot, "-DataDirectory", dataDirectory, "-Workspace", privateWorkspace,
            "-AssemblyDirectory", AppContext.BaseDirectory, "-Disposable", .. extra]);

    // PowerShell wraps long error messages across decorated lines; rejoin them before matching text.
    private static string Unwrap(string output) =>
        Regex.Replace(Regex.Replace(output, "\u001b\\[[0-9;]*m", string.Empty), "\\s*\\r?\\n\\s*\\|?\\s*", " ");

    private static string Manifest(ScriptResult preflight)
    {
        Assert.True(preflight.Succeeded, preflight.Output);
        using var document = JsonDocument.Parse(preflight.Output);
        return document.RootElement.GetProperty("operationManifest").GetString()!;
    }

    private static ScriptResult Execute(params string[] arguments)
    {
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        var start = new ProcessStartInfo(File.Exists(shell) ? shell : "pwsh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(ProjectRoot(), "scripts", "install-dev.ps1") })
        {
            start.ArgumentList.Add(argument);
        }
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(180000), "install-dev.ps1 did not finish.");
        return new ScriptResult(process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    private static string ProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Project root missing.");
    }

    private static ReleaseBuild Marked() => new("dev", "b74a12b", false, BuiltAt, "0.1.8");

    private void CreateInstallation()
    {
        Directory.CreateDirectory(installationRoot);
        File.WriteAllText(Path.Combine(installationRoot, "bell-tower.exe"), "launcher fixture");
        DistributionJson.Write(Path.Combine(installationRoot, "installation.json"), new InstallationManifest());
        AddPayload(installationRoot, "0.1.8", null);
        DistributionJson.Write(Path.Combine(installationRoot, "current.json"), new VersionSelection("0.1.8"));
    }

    private static Dictionary<string, string> Files(string version, string? directory = null)
    {
        var files = new Dictionary<string, string>();
        foreach (var name in new[] { "bell-tower.exe", "bell-tower-ingress.exe", $"bell-tower-focus-{version}.vsix", "README.md" })
        {
            files[name] = Installation.Hash(Path.Combine(directory ?? string.Empty, name));
        }
        return files;
    }

    private static void AddPayload(string root, string version, ReleaseBuild? build = null)
    {
        var directory = Path.Combine(root, "versions", version);
        Directory.CreateDirectory(directory);
        var contract = WritePayload(directory, version, build);
        DistributionJson.Write(Path.Combine(directory, "release.json"), contract);
    }

    private static ReleaseContract WritePayload(string directory, string version, ReleaseBuild? build)
    {
        foreach (var name in new[] { "bell-tower.exe", "bell-tower-ingress.exe", $"bell-tower-focus-{version}.vsix", "README.md" })
        {
            File.WriteAllText(Path.Combine(directory, name), name + version);
        }
        return new ReleaseContract(version, Capture: 1, AttachmentHealth: 1, Files: Files(version, directory), Build: build);
    }

    private string BuildPackage(string version, ReleaseBuild? build)
    {
        var packages = Path.Combine(workspace.Root, "packages");
        var stage = Path.Combine(packages, "stage-" + version);
        var payload = Path.Combine(stage, "versions", version);
        Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(stage, "bell-tower.exe"), "launcher fixture");
        DistributionJson.Write(Path.Combine(stage, "installation.json"), new InstallationManifest());
        DistributionJson.Write(Path.Combine(stage, "current.json"), new VersionSelection(version));
        var contract = WritePayload(payload, version, build);
        DistributionJson.Write(Path.Combine(payload, "release.json"), contract);
        var archive = Path.Combine(packages, contract.PackageName);
        ZipFile.CreateFromDirectory(stage, archive);
        var hash = Installation.Hash(archive);
        File.WriteAllText(archive + ".sha256", hash + "  " + contract.PackageName);
        DistributionJson.Write(archive[..^4] + ".release.json", new ReleaseOffer(contract, hash));
        return archive;
    }

    private IDisposable FakeTray()
    {
        var paths = new DataPaths(dataDirectory);
        var mutex = new Mutex(true, paths.LockName("tray-instance"));
        Directory.CreateDirectory(Path.Combine(dataDirectory, "updates"));
        DistributionJson.Write(UpdateTransaction.Presence(paths), new TrayPresence(ProcessProbe.Identify(Environment.ProcessId)!, "0.1.8", dataDirectory));
        return new Release(mutex);
    }

    private static Dictionary<string, string> Snapshot(params string[] roots)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                result[file] = Installation.Hash(file);
            }
        }
        return result;
    }

    private sealed record ScriptResult(int ExitCode, string Output)
    {
        public bool Succeeded => ExitCode == 0;
    }

    private sealed class Release(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    private sealed class SelectionWriter(string root)
    {
        public void Write(VersionSelection selection) => DistributionJson.Write(Path.Combine(root, "current.json"), selection);
    }
}

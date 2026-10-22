using System.IO.Compression;
using System.Text.Json;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Updates;

public sealed class DistributionTests
{
    [Theory]
    [InlineData("../0.1.8")]
    [InlineData("01.1.8")]
    [InlineData("0.1.8-beta")]
    [InlineData("0.1")]
    public void VersionSelectionRejectsUnsafeOrUnsupportedVersions(string version) => Assert.Throws<InvalidDataException>(() => ReleaseContract.Parse(version));

    [Fact]
    public void ContractsAndUnknownSharedFieldsRoundTripWithoutChangingState()
    {
        using var workspace = new TestWorkspace();
        var config = Json.Decode<BellTower.Core.AppConfig>("{\"voice\":false,\"futureSetting\":{\"value\":42},\"updates\":{\"enabled\":false}}");
        AtomicFile.Write(workspace.Paths.Config, Json.Encode(config));
        new ConfigStore(workspace.Paths).Update(c => c.Muted = true);
        using var document = JsonDocument.Parse(File.ReadAllText(workspace.Paths.Config));
        Assert.Equal(42, document.RootElement.GetProperty("futureSetting").GetProperty("value").GetInt32());
        Assert.False(new ConfigStore(workspace.Paths).Read().Updates.Enabled);
        Assert.Throws<InvalidDataException>(() => new ReleaseContract("0.1.8", State: 2).Validate());
        Assert.Empty(Directory.GetFiles(workspace.Paths.Inbox));
    }
    [Fact]
    public void StableResolutionPinsPayloadAndLeasesBlockCleanup()
    {
        using var workspace = new TestWorkspace();
        var installation = CreateInstallation(Path.Combine(workspace.Root, "install"), "0.1.7");
        AddPayload(installation.Root, "0.1.8");
        var pinned = Path.Combine(installation.Payload("0.1.7"), "bell-tower.exe");
        Assert.Equal(installation.Launcher, Installation.StableExecutable(pinned));
        using (installation.Lease("0.1.7"))
        {
            DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.8"));
            Assert.Throws<IOException>(() => installation.Lease("0.1.7", true));
            UpdateTransaction.Cleanup(installation);
            Assert.True(File.Exists(pinned));
        }
        UpdateTransaction.Cleanup(installation);
        Assert.False(Directory.Exists(Path.GetDirectoryName(pinned)));
        Assert.Equal("0.1.8", installation.Selection().Version);
    }
    [Fact]
    public void CleanupPreservesRollbackAndUnownedMaterial()
    {
        using var workspace = new TestWorkspace();
        var installation = CreateInstallation(Path.Combine(workspace.Root, "install"), "0.1.7");
        AddPayload(installation.Root, "0.1.8");
        AddPayload(installation.Root, "0.1.6");
        File.WriteAllText(Path.Combine(installation.Payload("0.1.6"), "keep.txt"), "unowned");
        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.8", "0.1.7"));
        UpdateTransaction.Cleanup(installation);
        Assert.True(Directory.Exists(installation.Payload("0.1.7")));
        Assert.True(File.Exists(Path.Combine(installation.Payload("0.1.6"), "keep.txt")));
    }
    [Fact]
    public void SafeExtractionChecksSidecarAndDoesNotOverwriteInstalledSelection()
    {
        using var workspace = new TestWorkspace();
        var installation = CreateInstallation(Path.Combine(workspace.Root, "installed"), "0.1.7");
        var candidate = CreateInstallation(Path.Combine(workspace.Root, "candidate"), "0.1.8");
        var contract = DistributionJson.Read<ReleaseContract>(Path.Combine(candidate.Payload("0.1.8"), "release.json"));
        var archive = Path.Combine(workspace.Root, contract.PackageName);
        ZipFile.CreateFromDirectory(candidate.Root, archive);
        var offer = new ReleaseOffer(contract, Installation.Hash(archive));
        File.WriteAllText(archive + ".sha256", offer.ArchiveHash + "  " + contract.PackageName);
        var payload = ReleasePackage.Extract(archive, Path.Combine(workspace.Root, "extracted"), offer);
        Assert.True(File.Exists(Path.Combine(payload, "bell-tower.exe")));
        Assert.Equal("0.1.7", installation.Selection().Version);
        Assert.Throws<InvalidDataException>(() => ReleasePackage.Extract(archive, Path.Combine(workspace.Root, "bad"), offer with { ArchiveHash = new string('a', 64) }));
    }
    [Fact]
    public void MutationAndLaunchGatesCoordinateAcrossThreadsAndRecoveryVersionsAreRetained()
    {
        using var workspace = new TestWorkspace();
        var installation = CreateInstallation(Path.Combine(workspace.Root, "install"), "0.1.7");
        AddPayload(installation.Root, "0.1.8");
        using (var gate = new InstallationGate(installation.Root, "update"))
        {
            AssertBusyOnAnotherThread(() => new InstallationGate(installation.Root, "update", 0));
        }
        using (var gate = new InstallationGate(installation.Root))
        {
            AssertBusyOnAnotherThread(() => new InstallationGate(installation.Root, timeoutMilliseconds: 0));
        }
        var plan = new UpdatePlan(Guid.NewGuid().ToString("N"), new string('a', 64), installation.Root, workspace.Paths.Root,
            "0.1.7", "0.1.8", "", DateTimeOffset.UtcNow, ProcessProbe.Identify(Environment.ProcessId)!, false);
        DistributionJson.Write(UpdateTransaction.PlanPath(installation, plan.Id), plan);
        UpdateTransaction.Cleanup(installation);
        Assert.True(Directory.Exists(installation.Payload("0.1.8")));
        File.WriteAllText(workspace.Paths.State, "{\"schemaVersion\":2}");
        Assert.Throws<InvalidDataException>(() => UpdateTransaction.Stage(installation, workspace.Paths, "missing.zip", new ReleaseOffer(new ReleaseContract("0.1.9"), new string('a', 64))));
        Assert.Equal("0.1.7", installation.Selection().Version);
    }
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("versions/0.1.8/../../escape.txt")]
    [InlineData("versions/0.1.8/unowned.txt")]
    public void UnsafeArchivesNeverEscapeStaging(string entry)
    {
        using var workspace = new TestWorkspace();
        var candidate = CreateInstallation(Path.Combine(workspace.Root, "candidate"), "0.1.8");
        var contract = DistributionJson.Read<ReleaseContract>(Path.Combine(candidate.Payload("0.1.8"), "release.json"));
        var archive = Path.Combine(workspace.Root, contract.PackageName);
        ZipFile.CreateFromDirectory(candidate.Root, archive);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(zip.CreateEntry(entry).Open()))
        { writer.Write("bad"); }
        var offer = new ReleaseOffer(contract, Installation.Hash(archive));
        File.WriteAllText(archive + ".sha256", offer.ArchiveHash + "  " + contract.PackageName);
        Assert.Throws<InvalidDataException>(() => ReleasePackage.Extract(archive, Path.Combine(workspace.Root, "extracted"), offer));
        Assert.False(File.Exists(Path.Combine(workspace.Root, "escape.txt")));
    }
    internal static Installation CreateInstallation(string root, string version)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "bell-tower.exe"), "launcher fixture");
        DistributionJson.Write(Path.Combine(root, "installation.json"), new InstallationManifest());
        AddPayload(root, version);
        DistributionJson.Write(Path.Combine(root, "current.json"), new VersionSelection(version));
        return new Installation(root);
    }
    private static void AssertBusyOnAnotherThread(Func<IDisposable> acquire)
    {
        Exception? observed = null;
        var thread = new Thread(() =>
        {
            try
            { using var lease = acquire(); }
            catch (Exception error) { observed = error; }
        })
        { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(2000));
        Assert.IsType<IOException>(observed);
    }
    private static void AddPayload(string root, string version)
    {
        var directory = Path.Combine(root, "versions", version);
        Directory.CreateDirectory(directory);
        var files = new Dictionary<string, string>();
        foreach (var name in new[] { "bell-tower.exe", $"bell-tower-focus-{version}.vsix", "README.md" })
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, name + version);
            files[name] = Installation.Hash(path);
        }
        DistributionJson.Write(Path.Combine(directory, "release.json"), new ReleaseContract(version, Files: files));
    }
}

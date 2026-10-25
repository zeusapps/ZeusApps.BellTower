using System.Text.Json;
using BellTower.Distribution;
using BellTower.Tests.Testing;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Updates;

public sealed class DevBuildTests
{
    private static readonly DateTimeOffset BuiltAt = new(2026, 10, 24, 21, 30, 15, TimeSpan.Zero);

    [Theory]
    [InlineData("0.1.8", false)]
    [InlineData("0.1.10", false)]
    [InlineData("0.1.8999", false)]
    [InlineData("0.1.9000", true)]
    [InlineData("0.1.9001", true)]
    [InlineData("1.0.12345", true)]
    public void ReservedRangeClassifiesVersions(string version, bool dev) => Assert.Equal(dev, ReleaseContract.IsDevVersion(version));

    [Fact]
    public void BuildMetadataRoundTripsAndOlderShapedContractsStillValidate()
    {
        var contract = new ReleaseContract("0.1.9001", Capture: 1, AttachmentHealth: 1, Build: new ReleaseBuild("dev", "b74a12b", true, BuiltAt, "0.1.8"));
        contract.Validate();
        var json = JsonSerializer.Serialize(contract, DistributionJson.Options);
        Assert.Equal(contract.Build, JsonSerializer.Deserialize<ReleaseContract>(json, DistributionJson.Options)!.Build);
        using (var document = JsonDocument.Parse(json))
        {
            var build = document.RootElement.GetProperty("build");
            Assert.Equal("dev", build.GetProperty("channel").GetString());
            Assert.Equal("b74a12b", build.GetProperty("commit").GetString());
            Assert.True(build.GetProperty("dirty").GetBoolean());
            Assert.Equal("0.1.8", build.GetProperty("baseVersion").GetString());
        }

        var legacy = JsonSerializer.Deserialize<ReleaseContract>("{\"version\":\"0.1.8\",\"capture\":1,\"attachmentHealth\":1,\"futureField\":{\"x\":1}}", DistributionJson.Options)!;
        legacy.Validate();
        Assert.Null(legacy.Build);
        using var stable = JsonDocument.Parse(JsonSerializer.Serialize(legacy, DistributionJson.Options));
        Assert.False(stable.RootElement.TryGetProperty("build", out _));

        var future = JsonSerializer.Deserialize<ReleaseContract>("{\"version\":\"0.1.9001\",\"build\":{\"channel\":\"dev\",\"commit\":\"abc123\",\"dirty\":false,\"builtAt\":\"2026-10-24T21:30:15Z\",\"baseVersion\":\"0.1.8\",\"extra\":1}}", DistributionJson.Options)!;
        future.Validate();
    }

    [Theory]
    [InlineData("0.1.8", "dev", "b74a12b", "0.1.7")]
    [InlineData("0.1.9001", "stable", "b74a12b", "0.1.8")]
    [InlineData("0.1.9001", "dev", "", "0.1.8")]
    [InlineData("0.1.9001", "dev", "not-hex", "0.1.8")]
    [InlineData("0.1.9001", "dev", "b74a12b", "0.1.9002")]
    [InlineData("0.1.9001", "dev", "b74a12b", "latest")]
    public void InvalidBuildMetadataIsRejected(string version, string channel, string commit, string baseVersion) =>
        Assert.Throws<InvalidDataException>(() => new ReleaseContract(version, Build: new ReleaseBuild(channel, commit, false, BuiltAt, baseVersion)).Validate());

    [Fact]
    public void EffectiveVersionUsesBaseForDevAndZeroPatchForUnmarkedDev()
    {
        Assert.Equal(new Version(0, 1, 8), new ReleaseContract("0.1.8").EffectiveVersion);
        Assert.Equal(new Version(0, 1, 8), new ReleaseContract("0.1.9001", Build: new ReleaseBuild("dev", "b74a12b", false, BuiltAt, "0.1.8")).EffectiveVersion);
        Assert.Equal(new Version(0, 1, 0), new ReleaseContract("0.1.9001").EffectiveVersion);
        Assert.Equal(new Version(0, 1, 0), ReleaseContract.Effective("0.1.9003", null));
    }

    [Fact]
    public void StableReleaseReplacesSelectedDevBuildAndStableSelectionKeepsSupersededRule()
    {
        using var workspace = new TestWorkspace();
        var installation = DevInstallation(Path.Combine(workspace.Root, "install"), "0.1.8");
        AddPayload(installation.Root, "0.1.9001", new ReleaseBuild("dev", "b74a12b", false, BuiltAt, "0.1.8"));
        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.9001", "0.1.8"));
        Assert.Equal(new Version(0, 1, 8), installation.EffectiveVersion());

        // The superseded check passes for stable 0.1.9, so staging proceeds to the (missing) archive.
        var missing = Path.Combine(workspace.Root, "missing.zip");
        var error = Record.Exception(() => UpdateTransaction.Stage(installation, workspace.Paths, missing, Offer("0.1.9")));
        Assert.IsType<FileNotFoundException>(error);
        Assert.Equal("0.1.9001", installation.Selection().Version);

        // A stable release equal to the dev base is still superseded.
        var superseded = Assert.Throws<IOException>(() => UpdateTransaction.Stage(installation, workspace.Paths, missing, Offer("0.1.8")));
        Assert.Contains("already selected or superseded", superseded.Message);

        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.8"));
        var stable = Assert.Throws<IOException>(() => UpdateTransaction.Stage(installation, workspace.Paths, missing, Offer("0.1.8")));
        Assert.Contains("already selected or superseded", stable.Message);
    }

    [Fact]
    public void PreviousMayNameADevVersionAndCleanupRetainsIt()
    {
        using var workspace = new TestWorkspace();
        var installation = DevInstallation(Path.Combine(workspace.Root, "install"), "0.1.8");
        AddPayload(installation.Root, "0.1.9001", new ReleaseBuild("dev", "b74a12b", false, BuiltAt, "0.1.8"));
        AddPayload(installation.Root, "0.1.9");
        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.9", "0.1.9001", "activation"));
        Assert.Equal("0.1.9001", installation.Selection().Previous);
        Assert.Equal(new Version(0, 1, 9), installation.EffectiveVersion());
        UpdateTransaction.Cleanup(installation);
        Assert.True(Directory.Exists(installation.Payload("0.1.9001")));
        Assert.False(Directory.Exists(Path.Combine(installation.Root, "versions", "0.1.8")));
    }

    [Fact]
    public void PublishedOffersRefuseTheDevRangeButPackagesStayValid()
    {
        var dev = new ReleaseOffer(new ReleaseContract("0.1.9001", Build: new ReleaseBuild("dev", "b74a12b", false, BuiltAt, "0.1.8")), new string('a', 64));
        dev.Validate();
        Assert.Throws<InvalidDataException>(dev.EnsureStable);
        Offer("0.1.9").EnsureStable();
    }

    [Fact]
    public void DisplayShowsDevLabelCommitDirtyAndBuildTimeOnlyForDevBuilds()
    {
        Assert.Equal("0.1.8", VersionDisplay.Format("0.1.8", null));
        Assert.Equal("0.1.8", VersionDisplay.Format("0.1.8", new ReleaseBuild("dev", "b74a12b", false, BuiltAt, "0.1.7")));
        Assert.Equal("0.1.9001 dev b74a12b (2026-10-24 21:30 UTC)", VersionDisplay.Format("0.1.9001", new ReleaseBuild("dev", "b74a12b", false, BuiltAt, "0.1.8")));
        Assert.Equal("0.1.9001 dev b74a12b dirty (2026-10-24 21:30 UTC)", VersionDisplay.Format("0.1.9001", new ReleaseBuild("dev", "b74a12b", true, BuiltAt, "0.1.8")));
        Assert.Equal("0.1.9002 dev (unmarked)", VersionDisplay.Format("0.1.9002", null));
        Assert.False(VersionDisplay.IsDev(VersionDisplay.Format("0.1.8", null)));
        Assert.True(VersionDisplay.IsDev(VersionDisplay.Format("0.1.9001", null)));
    }

    [Theory]
    [InlineData("0.1.8", "0.1.8")]
    [InlineData("0.1.8+abcdef", "0.1.8")]
    [InlineData("0.1.9001-dev+b74a12b", "0.1.9001 dev b74a12b")]
    [InlineData("0.1.9001-dev+b74a12b.dirty", "0.1.9001 dev b74a12b dirty")]
    [InlineData("0.1.9001", "0.1.9001 dev (unmarked)")]
    [InlineData("0.1.9001-dev", "0.1.9001 dev (unmarked)")]
    public void InformationalVersionFallbackFormatsTheSameLabels(string informational, string expected) =>
        Assert.Equal(expected, VersionDisplay.FromInformational(informational));

    [Fact]
    public void InstallationDisplaysSelectedDevAndStablePayloads()
    {
        using var workspace = new TestWorkspace();
        var installation = DevInstallation(Path.Combine(workspace.Root, "install"), "0.1.8");
        AddPayload(installation.Root, "0.1.9001", new ReleaseBuild("dev", "b74a12b", true, BuiltAt, "0.1.8"));
        AddPayload(installation.Root, "0.1.9002");
        Assert.Equal("0.1.8", installation.DisplayVersion("0.1.8"));
        Assert.Equal("0.1.9001 dev b74a12b dirty (2026-10-24 21:30 UTC)", installation.DisplayVersion("0.1.9001"));
        Assert.Equal("0.1.9002 dev (unmarked)", installation.DisplayVersion("0.1.9002"));
    }

    private static ReleaseOffer Offer(string version) => new(new ReleaseContract(version), new string('a', 64));

    private static Installation DevInstallation(string root, string version)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "bell-tower.exe"), "launcher fixture");
        DistributionJson.Write(Path.Combine(root, "installation.json"), new InstallationManifest());
        AddPayload(root, version);
        DistributionJson.Write(Path.Combine(root, "current.json"), new VersionSelection(version));
        return new Installation(root);
    }

    private static void AddPayload(string root, string version, ReleaseBuild? build = null)
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
        DistributionJson.Write(Path.Combine(directory, "release.json"), new ReleaseContract(version, Files: files, Build: build));
    }
}

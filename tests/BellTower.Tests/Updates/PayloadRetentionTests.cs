using BellTower.Distribution;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Updates;

// The retention and removal predicate shared by the tray's Cleanup and the dev Retire action.
public sealed class PayloadRetentionTests
{
    private static readonly ReleaseBuild Dev = new("dev", "b74a12b", false, new DateTimeOffset(2026, 10, 24, 21, 30, 0, TimeSpan.Zero), "0.1.8");

    [Fact]
    public void RetainedVersionsListSelectedPreviousAndUnfinishedUpdateVersionsWithReasons()
    {
        using var workspace = new TestWorkspace();
        var installation = Create(workspace, "0.1.8", "0.1.9001", "0.1.9002", "0.1.9003", "0.1.9004");
        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.9002", "0.1.9001"));
        WritePlan(installation, workspace, "0.1.8", "0.1.9003", "failed");
        WritePlan(installation, workspace, "0.1.8", "0.1.9004", "monitoring-restored");

        var retained = UpdateTransaction.RetainedVersions(installation);
        Assert.Contains("selected", retained["0.1.9002"]);
        Assert.Contains("previous", retained["0.1.9001"]);
        Assert.Contains("recovery", retained["0.1.9003"]);
        Assert.Contains("recovery", retained["0.1.8"]);
        Assert.DoesNotContain("0.1.9004", retained.Keys);
    }

    [Fact]
    public void PredicateReturnsAReasonForEveryKeptPayloadAndNullWhenItRemoves()
    {
        using var workspace = new TestWorkspace();
        var installation = Create(workspace, "0.1.8", "0.1.9001", "0.1.9002", "0.1.9003", "0.1.10");
        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.9002", "0.1.8"));
        var retained = UpdateTransaction.RetainedVersions(installation);

        Assert.Contains("selected version", UpdateTransaction.TryRemovePayload(installation, "0.1.9002", retained, true));
        Assert.Contains("previous version", UpdateTransaction.TryRemovePayload(installation, "0.1.8", retained, true));
        Assert.Contains("no dev marker", UpdateTransaction.TryRemovePayload(installation, "0.1.10", retained, true));
        Assert.True(Directory.Exists(Path.Combine(installation.Root, "versions", "0.1.10")));

        using (installation.Lease("0.1.9001"))
        {
            Assert.Contains("leased", UpdateTransaction.TryRemovePayload(installation, "0.1.9001", retained, true));
        }

        File.WriteAllText(Path.Combine(installation.Root, "versions", "0.1.9003", "notes.txt"), "unowned");
        Assert.Contains("notes.txt", UpdateTransaction.TryRemovePayload(installation, "0.1.9003", retained, true));
        File.Delete(Path.Combine(installation.Root, "versions", "0.1.9003", "notes.txt"));

        Assert.Null(UpdateTransaction.TryRemovePayload(installation, "0.1.9001", retained, true));
        Assert.False(Directory.Exists(Path.Combine(installation.Root, "versions", "0.1.9001")));
        // Without the dev requirement (tray cleanup) an unmarked payload goes under the same rules.
        Assert.Null(UpdateTransaction.TryRemovePayload(installation, "0.1.10", retained, false));
        Assert.False(Directory.Exists(Path.Combine(installation.Root, "versions", "0.1.10")));
    }

    [Fact]
    public void RetireRefusesUnmarkedAndRemovesMarkedPayloadsUnderBothGates()
    {
        using var workspace = new TestWorkspace();
        var installation = Create(workspace, "0.1.8", "0.1.9001", "0.1.9002", "0.1.10");
        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.9002", "0.1.8"));

        Assert.Contains("automatic cleanup", UpdateTransaction.Retire(installation, "0.1.10"));
        Assert.Contains("no dev marker", UpdateTransaction.Retire(installation, "0.1.10") ?? string.Empty, StringComparison.Ordinal);
        Assert.Null(UpdateTransaction.Retire(installation, "0.1.9001"));
        Assert.Equal(["0.1.10", "0.1.8", "0.1.9002"], Directory.GetDirectories(Path.Combine(installation.Root, "versions")).Select(path => new DirectoryInfo(path).Name).Order().ToArray());
    }

    [Fact]
    public void CleanupKeepsRetainedLeasedAndUnownedPayloadsAndRemovesTheRest()
    {
        using var workspace = new TestWorkspace();
        var installation = Create(workspace, "0.1.8", "0.1.9001", "0.1.9002", "0.1.9003", "0.1.9004", "0.1.10");
        DistributionJson.Write(installation.SelectionPath, new VersionSelection("0.1.9002", "0.1.8"));
        File.WriteAllText(Path.Combine(installation.Root, "versions", "0.1.9003", "keep.txt"), "unowned");
        using (installation.Lease("0.1.9004"))
        {
            UpdateTransaction.Cleanup(installation);
        }

        var left = Directory.GetDirectories(Path.Combine(installation.Root, "versions")).Select(path => new DirectoryInfo(path).Name).Order().ToArray();
        Assert.Equal(["0.1.8", "0.1.9002", "0.1.9003", "0.1.9004"], left);
    }

    private static Installation Create(TestWorkspace workspace, string selected, params string[] versions)
    {
        var root = Path.Combine(workspace.Root, "install");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "bell-tower.exe"), "launcher");
        DistributionJson.Write(Path.Combine(root, "installation.json"), new InstallationManifest());
        foreach (var version in new[] { selected }.Concat(versions).Distinct())
        {
            var directory = Path.Combine(root, "versions", version);
            Directory.CreateDirectory(directory);
            var files = new Dictionary<string, string>();
            foreach (var name in new[] { "bell-tower.exe", $"bell-tower-focus-{version}.vsix", "README.md" })
            {
                File.WriteAllText(Path.Combine(directory, name), name + version);
                files[name] = Installation.Hash(Path.Combine(directory, name));
            }
            DistributionJson.Write(Path.Combine(directory, "release.json"), new ReleaseContract(version, Files: files, Build: ReleaseContract.IsDevVersion(version) && version != "0.1.9004" ? Dev : null));
        }
        DistributionJson.Write(Path.Combine(root, "current.json"), new VersionSelection(selected));
        return new Installation(root);
    }

    private static void WritePlan(Installation installation, TestWorkspace workspace, string original, string target, string state)
    {
        var plan = new UpdatePlan(Guid.NewGuid().ToString("N"), new string('a', 64), installation.Root, workspace.Paths.Root, original, target, "", DateTimeOffset.UtcNow,
            ProcessProbe.Identify(Environment.ProcessId)!, false);
        DistributionJson.Write(UpdateTransaction.PlanPath(installation, plan.Id), plan);
        DistributionJson.Write(Path.Combine(installation.Root, "updates", plan.Id, "outcome.json"), new UpdateOutcome(plan.Id, state, target, state, DateTimeOffset.UtcNow));
    }
}

using BellTower.FieldAcceptance.Isolation;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>The before/after production check, run against an invented user profile under a temporary directory.</summary>
public sealed class ProductionSnapshotTests : IDisposable
{
    private readonly TestWorkspace workspace = new();
    private readonly ProductionLocations production;
    private readonly string settings;
    private readonly string installed;

    public ProductionSnapshotTests()
    {
        var profile = Path.Combine(workspace.Root, "profile");
        production = ProductionLocations.For(profile, Path.Combine(profile, "AppData", "Local"), Path.Combine(profile, "AppData", "Roaming"));
        settings = Path.Combine(profile, ".claude", "settings.json");
        installed = Path.Combine(production.InstallationRoot, "current.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        Directory.CreateDirectory(production.InstallationRoot);
        File.WriteAllText(settings, "{\"hooks\":{}}");
        File.WriteAllText(installed, "{\"version\":\"0.1.9\"}");
        Directory.CreateDirectory(production.DataRoot);
        File.WriteAllText(Path.Combine(production.DataRoot, "state.json"), "{}");
    }

    public void Dispose() => workspace.Dispose();

    private ProductionSnapshot Take(IEnumerable<string>? tasks = null, IEnumerable<ProcessFingerprint>? processes = null) =>
        ProductionSnapshot.Take(production, () => tasks ?? [], () => processes ?? []);

    [Fact]
    public void AnUntouchedProductionTreeProducesNoChanges()
    {
        var before = Take();
        var after = Take();
        Assert.Empty(ProductionSnapshot.Diff(before, after, new HashSet<string>()));
    }

    [Fact]
    public void ARewrittenSetupOwnedFileIsAGatingChange()
    {
        var before = Take();
        File.WriteAllText(settings, "{\"hooks\":{\"SessionStart\":[]}}");
        var changes = ProductionSnapshot.Diff(before, Take(), new HashSet<string>());
        var change = Assert.Single(changes, candidate => candidate.Gating);
        Assert.Equal(settings, change.Subject);
    }

    [Fact]
    public void ACreatedWatchedFileAndAnInstallationTreeChangeAreGating()
    {
        var before = Take();
        var codexHooks = Path.Combine(workspace.Root, "profile", ".codex", "hooks.json");
        Directory.CreateDirectory(Path.GetDirectoryName(codexHooks)!);
        File.WriteAllText(codexHooks, "{}");
        File.WriteAllText(installed, "{\"version\":\"0.1.11\"}");
        var subjects = ProductionSnapshot.Diff(before, Take(), new HashSet<string>()).Where(change => change.Gating).Select(change => change.Subject).ToList();
        Assert.Contains(codexHooks, subjects);
        Assert.Contains(installed, subjects);
    }

    [Fact]
    public void LiveWritesToTheDataDirectoryAreInformationalOnly()
    {
        var before = Take();
        File.WriteAllText(Path.Combine(production.DataRoot, "state.json"), "{\"a\":1}");
        File.WriteAllText(Path.Combine(production.DataRoot, "new.json"), "{}");
        var changes = ProductionSnapshot.Diff(before, Take(), new HashSet<string>());
        var change = Assert.Single(changes);
        Assert.False(change.Gating);
        Assert.Equal("directory-total", change.Kind);
    }

    [Fact]
    public void ScheduledTasksAreComparedExceptTheSandboxsOwn()
    {
        const string own = @"\ZeusApps.BellTower\HookStart-aaaa";
        const string users = @"\ZeusApps.BellTower\HookStart-bbbb";
        var before = Take([users]);
        // The sandbox task coming and going is expected; the user's task disappearing or another one appearing is not.
        Assert.Empty(ProductionSnapshot.Diff(before, Take([users]), new HashSet<string> { own }));
        Assert.Contains(ProductionSnapshot.Diff(before, Take([]), new HashSet<string> { own }), change => change is { Kind: "scheduled-task", Gating: true } && change.Detail == "removed");
        Assert.Contains(ProductionSnapshot.Diff(before, Take([users, @"\ZeusApps.BellTower\HookStart-cccc"]), new HashSet<string> { own }), change => change.Detail == "added");
        Assert.Contains(ProductionSnapshot.Diff(before, Take([users, own]), new HashSet<string> { own }), change => change.Detail.Contains("sandbox task remains", StringComparison.Ordinal));
    }

    [Fact]
    public void AProductionProcessThatStoppedRunningIsAGatingChange()
    {
        var tray = new ProcessFingerprint(100, 1234, "bell-tower.exe");
        var before = Take(processes: [tray]);
        Assert.Empty(ProductionSnapshot.Diff(before, Take(processes: [tray]), new HashSet<string>()));
        var change = Assert.Single(ProductionSnapshot.Diff(before, Take(processes: []), new HashSet<string>()));
        Assert.True(change.Gating);
        Assert.Equal("process", change.Kind);
    }

    [Fact]
    public void AProductionProcessThatAppearsDuringTheRunIsAGatingChange()
    {
        var tray = new ProcessFingerprint(100, 1234, "bell-tower.exe");
        var before = Take(processes: [tray]);
        var started = new ProcessFingerprint(200, 5678, "bell-tower.exe");
        var change = Assert.Single(ProductionSnapshot.Diff(before, Take(processes: [tray, started]), new HashSet<string>()));
        Assert.True(change.Gating);
        Assert.Equal("process", change.Kind);
        Assert.Contains("started during the run", change.Detail, StringComparison.Ordinal);
        // The same pid with a different start time is a different process, not the one that was running before.
        var reused = new ProcessFingerprint(100, 9999, "bell-tower.exe");
        Assert.Equal(2, ProductionSnapshot.Diff(before, Take(processes: [reused]), new HashSet<string>()).Count(candidate => candidate.Gating));
    }

    [Fact]
    public void TaskListingKeepsOnlyBellTowerTaskNames()
    {
        var csv = "\"\\ZeusApps.BellTower\\HookStart-aaaa\",\"N/A\",\"Ready\"\r\n\"\\Microsoft\\Windows\\Defrag\",\"1/1/2026\",\"Ready\"\r\n\"\\ZeusApps.BellTower\\HookStart-bbbb\",\"N/A\",\"Running\"\r\n";
        var tasks = ProductionSnapshot.BellTowerTasks(_ => (0, csv)).ToList();
        Assert.Equal([@"\ZeusApps.BellTower\HookStart-aaaa", @"\ZeusApps.BellTower\HookStart-bbbb"], tasks);
        Assert.Empty(ProductionSnapshot.BellTowerTasks(_ => (1, csv)));
    }
}

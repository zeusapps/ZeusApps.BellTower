using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Updates;

public sealed class IntegrationRefreshTests
{
    [Fact]
    public void RefreshPreservesSelectedProvidersProjectsAndUnrelatedSettings()
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        Directory.CreateDirectory(home);
        var settings = Path.Combine(home, "AppData", "Roaming", "Code", "User", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, "{\"editor.fontSize\":17}");
        var executable = Path.Combine(workspace.Root, "bell-tower.exe");
        var setup = new IntegrationSetup(workspace.Paths, executable, home);
        setup.Plan("claude", desktop: true, project: workspace.Root).Apply(workspace.Paths);
        var before = File.ReadAllText(settings);
        var ownershipPath = Path.Combine(workspace.Paths.Root, "integration.json");
        var ownedBefore = Json.Decode<IntegrationOwnership>(File.ReadAllText(ownershipPath));
        var result = new IntegrationRefresh(workspace.Paths, executable, home).Apply();
        var after = Json.Decode<IntegrationOwnership>(File.ReadAllText(ownershipPath));
        Assert.Equal(before, File.ReadAllText(settings));
        Assert.Equal(ownedBefore.Desktop.Projects.Keys, after.Desktop.Projects.Keys);
        Assert.Equal(["claude"], after.Desktop.Providers);
        Assert.False(File.Exists(Path.Combine(home, ".codex", "hooks.json")));
        Assert.Contains("new shell", result);
    }
    [Fact]
    public void EditedOwnedModuleAndUnownedExtensionArePreserved()
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        Directory.CreateDirectory(home);
        var executable = Path.Combine(workspace.Root, "bell-tower.exe");
        new IntegrationSetup(workspace.Paths, executable, home).Plan("codex", desktop: true).Apply(workspace.Paths);
        var module = Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1");
        File.AppendAllText(module, "\n# user change");
        var before = File.ReadAllText(module);
        Assert.Throws<IOException>(() => new IntegrationRefresh(workspace.Paths, executable, home).Apply());
        Assert.Equal(before, File.ReadAllText(module));
    }
}

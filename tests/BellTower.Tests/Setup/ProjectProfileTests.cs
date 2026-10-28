using System.Text.Json.Nodes;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class ProjectProfileTests
{
    [Fact]
    public void ProjectProfilesUseTheProjectDirectoryAndSurviveAdditionalRegistrations()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        var first = Path.Combine(workspace.Root, "ZeusApps.BellTower");
        var second = Path.Combine(workspace.Root, "Other project Ω's");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var setup = new IntegrationSetup(workspace.Paths, "C:/BellTower/bell-tower.exe", fixture.Home);
        var plan = setup.Plan("both", desktop: true, project: first);
        Assert.Equal("{}", File.ReadAllText(fixture.Code));
        plan.Apply(workspace.Paths);
        setup.Plan("both", desktop: true, project: second).Apply(workspace.Paths);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);
        var code = JsonNode.Parse(File.ReadAllText(fixture.Code))!["terminal.integrated.profiles.windows"]!.AsObject();
        Assert.Equal(6, code.Count);
        var command = code["Codex (Other project Ω's)"]!["args"]!.AsArray().Last()!.GetValue<string>();
        Assert.Equal("Start-Codex -Directory " + PowerShellModule.Literal(second), command);
        var terminal = JsonNode.Parse(File.ReadAllText(fixture.Terminal))!["profiles"]!["list"]!.AsArray();
        Assert.Equal(6, terminal.Count);
        Assert.Equal(first, terminal.Single(entry => entry!["name"]!.GetValue<string>() == "Codex (ZeusApps.BellTower)")!["startingDirectory"]!.GetValue<string>());
        Assert.Equal(fixture.Home, terminal.Single(entry => entry!["name"]!.GetValue<string>() == "Codex")!["startingDirectory"]!.GetValue<string>());
        var claude = File.ReadAllText(Path.Combine(fixture.Home, ".claude", "settings.json"));
        var codex = File.ReadAllText(Path.Combine(fixture.Home, ".codex", "hooks.json"));
        setup.Plan("both", remove: true, project: first).Apply(workspace.Paths);
        Assert.Equal(claude, File.ReadAllText(Path.Combine(fixture.Home, ".claude", "settings.json")));
        Assert.Equal(codex, File.ReadAllText(Path.Combine(fixture.Home, ".codex", "hooks.json")));
        Assert.Equal(4, JsonNode.Parse(File.ReadAllText(fixture.Terminal))!["profiles"]!["list"]!.AsArray().Count);
        setup.Plan("both", remove: true).Apply(workspace.Paths);
        Assert.Empty(JsonNode.Parse(File.ReadAllText(fixture.Terminal))!["profiles"]!["list"]!.AsArray());
    }

    [Fact]
    public void DuplicateFolderNamesAndMissingDirectoriesFailBeforeWriting()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/BellTower/bell-tower.exe", fixture.Home);
        Assert.Throws<ArgumentException>(() => setup.Plan("codex", project: Path.Combine(workspace.Root, "missing")));
        var first = Path.Combine(workspace.Root, "one", "Project");
        var second = Path.Combine(workspace.Root, "two", "Project");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        setup.Plan("codex", project: first).Apply(workspace.Paths);
        var original = File.ReadAllText(fixture.Code);
        Assert.Throws<InvalidOperationException>(() => setup.Plan("codex", project: second));
        Assert.Equal(original, File.ReadAllText(fixture.Code));
    }

    [Fact]
    public void ProjectOnlyCodexRemovalPreservesGlobalHookShellMetadata()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        var project = Path.Combine(workspace.Root, "registered-codex-project");
        Directory.CreateDirectory(project);
        var setup = new IntegrationSetup(workspace.Paths, "C:/BellTower/bell-tower.exe", fixture.Home);
        setup.Plan("codex", project: project).Apply(workspace.Paths);

        var manifestPath = Path.Combine(workspace.Paths.Root, "integration.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["selectedCodexHookShell"] = "Cmd";
        manifest["futureShellMetadata"] = "preserve-me";
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        var codexSettings = Path.Combine(fixture.Home, ".codex", "hooks.json");
        var settingsBefore = File.ReadAllBytes(codexSettings);

        var projectRemoval = setup.Plan("codex", remove: true, project: project);
        Assert.DoesNotContain(projectRemoval.Changes, change => change.Path.Equals(codexSettings, StringComparison.OrdinalIgnoreCase));
        projectRemoval.Apply(workspace.Paths);
        var afterProjectRemoval = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        Assert.Equal("Cmd", afterProjectRemoval["selectedCodexHookShell"]!.GetValue<string>());
        Assert.Equal("preserve-me", afterProjectRemoval["futureShellMetadata"]!.GetValue<string>());
        Assert.Equal(settingsBefore, File.ReadAllBytes(codexSettings));

        setup.Plan("claude").Apply(workspace.Paths);
        var afterClaudeSetup = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        Assert.Equal("Cmd", afterClaudeSetup["selectedCodexHookShell"]!.GetValue<string>());
        Assert.Equal("preserve-me", afterClaudeSetup["futureShellMetadata"]!.GetValue<string>());
        Assert.Equal(settingsBefore, File.ReadAllBytes(codexSettings));

        setup.Plan("codex", remove: true).Apply(workspace.Paths);
        var afterCodexRemoval = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        Assert.Null(afterCodexRemoval["selectedCodexHookShell"]);
        Assert.Equal("preserve-me", afterCodexRemoval["futureShellMetadata"]!.GetValue<string>());
    }

    private static (string Home, string Code, string Terminal) Prepare(TestWorkspace workspace)
    {
        var home = Path.Combine(workspace.Root, "home");
        var code = Path.Combine(home, "AppData", "Roaming", "Code", "User", "settings.json");
        var terminal = Path.Combine(home, "AppData", "Local", "Microsoft", "Windows Terminal", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(code)!);
        Directory.CreateDirectory(Path.GetDirectoryName(terminal)!);
        File.WriteAllText(code, "{}");
        File.WriteAllText(terminal, "{}");
        return (home, code, terminal);
    }
}

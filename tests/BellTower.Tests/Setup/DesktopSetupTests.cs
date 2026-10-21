using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class DesktopSetupTests
{
    [Fact]
    public void RemovalPreservesUserEditedProfilesAndTheirHelpers()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture.Home);
        setup.Plan("codex", desktop: true).Apply(workspace.Paths);
        var settings = new JsonSettingsFile(fixture.Code);
        var profiles = settings.Root["terminal.integrated.profiles.windows"]!.DeepClone().AsObject();
        profiles["Codex"]!["icon"] = "my-custom-icon";
        settings.Set("terminal.integrated.profiles.windows", profiles);
        var changes = new List<FileChange>();
        settings.AddTo(changes);
        new SetupPlan(changes).Apply(workspace.Paths);
        var removal = setup.Plan("codex", remove: true);
        Assert.Contains(removal.Notes, note => note.Contains("Kept user-edited terminal profile"));
        removal.Apply(workspace.Paths);
        Assert.Equal("my-custom-icon", Read(fixture.Code)["terminal.integrated.profiles.windows"]!["Codex"]!["icon"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1")));
    }

    [Fact]
    public void FailedSetupActionRetainsRemovalOwnershipForRetry()
    {
        using var workspace = new TestWorkspace();
        var manifest = Path.Combine(workspace.Paths.Root, "integration.json");
        File.WriteAllText(manifest, "original ownership");
        var final = new FileChange(manifest, "original ownership", "removed ownership");
        var settings = Path.Combine(workspace.Root, "owned.json");
        var plan = new SetupPlan([new(settings, null, "configured"), final],
            [new("Failing disposable helper", Path.Combine(workspace.Root, "does-not-exist.exe"), [])], commitAfterActions: final);
        Assert.ThrowsAny<Exception>(() => plan.Apply(workspace.Paths));
        Assert.Equal("original ownership", File.ReadAllText(manifest));
        Assert.Equal("configured", File.ReadAllText(settings));
    }

    private static (string Home, string Code, string Terminal) Prepare(TestWorkspace workspace)
    {
        var home = Path.Combine(workspace.Root, "home");
        var code = Path.Combine(home, "AppData", "Roaming", "Code", "User", "settings.json");
        var terminal = Path.Combine(home, "AppData", "Local", "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(code)!);
        Directory.CreateDirectory(Path.GetDirectoryName(terminal)!);
        File.WriteAllText(code, """
            {
              // Keep unrelated comments and Unicode Ω exactly.
              "editor.fontSize": 15,
              "terminal.integrated.profiles.windows": {
                "Claude PM": {"source":"PowerShell","args":["-NoExit","-Command","Start-ClaudeCodePM"],"overrideName":true,"icon":"organization","color":"terminal.ansiCyan"},
                "My shell": {"path":"custom.exe"},
              },
            }
            """);
        File.WriteAllText(terminal, """
            { // My terminal settings
              "defaultProfile": "custom",
              "profiles": {"defaults":{"font":{"face":"My Font"}},"list":[{"guid":"custom","name":"My shell","commandline":"custom.exe"}]},
              "schemes": [],
            }
            """);
        return (home, code, terminal);
    }

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path), documentOptions:
        new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip })!.AsObject();

    [Fact]
    public void DesktopPlanIsReadOnlyAndPreservesExistingSettingsWhenApplied()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        var original = File.ReadAllText(fixture.Code);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture.Home);
        var plan = setup.Plan("both", desktop: true);
        Assert.Equal(original, File.ReadAllText(fixture.Code));
        Assert.False(File.Exists(Path.Combine(fixture.Home, "Documents", "PowerShell", "profile.ps1")));
        plan.Apply(workspace.Paths);

        var code = Read(fixture.Code);
        Assert.Equal(15, code["editor.fontSize"]!.GetValue<int>());
        Assert.Contains("// Keep unrelated comments and Unicode Ω exactly.", File.ReadAllText(fixture.Code));
        var profiles = code["terminal.integrated.profiles.windows"]!.AsObject();
        Assert.Equal(4, profiles.Count);
        Assert.Null(profiles["Claude"]!["overrideName"]);
        Assert.True(profiles["Claude PM"]!["overrideName"]!.GetValue<bool>());
        Assert.Equal("custom.exe", profiles["My shell"]!["path"]!.GetValue<string>());
        Assert.Equal(workspace.Paths.Root, code["bellTower.dataDirectory"]!.GetValue<string>());
        var terminal = Read(fixture.Terminal);
        Assert.Equal("My Font", terminal["profiles"]!["defaults"]!["font"]!["face"]!.GetValue<string>());
        var entries = terminal["profiles"]!["list"]!.AsArray();
        Assert.Equal(3, entries.Count);
        var pm = entries.Single(entry => entry!["name"]!.GetValue<string>() == "Claude")!;
        Assert.Contains("Start-ClaudeCode", pm["commandline"]!.GetValue<string>());
        Assert.DoesNotContain("bell-tower", pm["commandline"]!.GetValue<string>());
        Assert.False(pm["suppressApplicationTitle"]!.GetValue<bool>());
        foreach (var shell in new[] { "PowerShell", "WindowsPowerShell" })
        {
            Assert.Contains("-NoClobber", File.ReadAllText(Path.Combine(fixture.Home, "Documents", shell, "profile.ps1")));
        }
        Assert.Contains("statusLine", File.ReadAllText(Path.Combine(fixture.Home, ".claude", "settings.json")));
    }

    [Fact]
    public void DesktopReinstallAndRemovalRestoreAdoptedProfilesAndKeepUnrelatedEdits()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture.Home);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);
        var profile = Path.Combine(fixture.Home, "Documents", "PowerShell", "profile.ps1");
        File.AppendAllText(profile, "\r\nfunction My-Helper { 'keep' }\r\n");
        var file = new JsonSettingsFile(fixture.Code);
        file.Set("editor.fontSize", JsonValue.Create(19));
        var edits = new List<FileChange>();
        file.AddTo(edits);
        new SetupPlan(edits).Apply(workspace.Paths);

        setup.Plan("both", remove: true).Apply(workspace.Paths);
        var removed = Read(fixture.Code);
        var remaining = removed["terminal.integrated.profiles.windows"]!.AsObject();
        Assert.Equal(2, remaining.Count);
        Assert.True(remaining["Claude PM"]!["overrideName"]!.GetValue<bool>());
        Assert.Equal(19, removed["editor.fontSize"]!.GetValue<int>());
        Assert.Null(removed["bellTower.executablePath"]);
        Assert.Single(Read(fixture.Terminal)["profiles"]!["list"]!.AsArray());
        Assert.Contains("function My-Helper", File.ReadAllText(profile));
        Assert.DoesNotContain("BELLTOWER:START", File.ReadAllText(profile));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1")));
    }

    [Fact]
    public void PartialRemovalKeepsTheOtherAgentRoleFunctionsAndProfiles()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture.Home);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);
        setup.Plan("claude", remove: true).Apply(workspace.Paths);
        var module = File.ReadAllText(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1"));
        Assert.Contains("function Start-CodexDev", module);
        Assert.DoesNotContain("function Start-ClaudeCodePM", module);
        Assert.Contains("bellTower.executablePath", File.ReadAllText(fixture.Code));
        Assert.Equal(2, Read(fixture.Terminal)["profiles"]!["list"]!.AsArray().Count);
    }

    [Fact]
    public void DesktopSetupRefusesConflictingProfilesBeforeAnyWrite()
    {
        using var workspace = new TestWorkspace();
        var fixture = Prepare(workspace);
        File.WriteAllText(fixture.Code, """{"terminal.integrated.profiles.windows":{"Codex":{"path":"unrelated.exe"}}}""");
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture.Home);
        Assert.Throws<InvalidOperationException>(() => setup.Plan("both", desktop: true));
        Assert.False(File.Exists(Path.Combine(fixture.Home, ".claude", "settings.json")));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"keep\": 1}")]
    [InlineData("{\"keep\": 1, // trailing comment\n}")]
    public void JsonSettingsCanAddReplaceAndRemoveOwnedRootValues(string original)
    {
        using var workspace = new TestWorkspace();
        var path = Path.Combine(workspace.Root, "settings.json");
        File.WriteAllText(path, original);
        var settings = new JsonSettingsFile(path);
        settings.Set("owned", JsonValue.Create("Ω"));
        settings.Set("owned", JsonValue.Create("new"));
        settings.Set("owned", null);
        var changes = new List<FileChange>();
        settings.AddTo(changes);
        new SetupPlan(changes).Apply(workspace.Paths);
        Assert.Null(Read(path)["owned"]);
        if (original.Contains("keep"))
        {
            Assert.Equal(1, Read(path)["keep"]!.GetValue<int>());
        }
        if (original.Contains("comment"))
        {
            Assert.Contains("// trailing comment", File.ReadAllText(path));
        }
    }
}

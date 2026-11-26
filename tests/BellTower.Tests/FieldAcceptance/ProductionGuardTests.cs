using BellTower.FieldAcceptance.Isolation;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>
/// The field acceptance harness must fail a run that could reach the user's installation, data, provider settings or tray.
/// These tests use invented production locations (never the real ones) and only inspect paths, so nothing is created or touched.
/// </summary>
public sealed class ProductionGuardTests
{
    private const string Profile = @"C:\Users\someone";
    private const string Local = @"C:\Users\someone\AppData\Local";
    private const string Roaming = @"C:\Users\someone\AppData\Roaming";
    private const string Base = @"D:\repo\artifacts\field-acceptance";
    private static readonly ProductionLocations Production = ProductionLocations.For(Profile, Local, Roaming, [@"E:\custom-data", null, " "]);
    private static readonly string[] Tools = [@"C:\Program Files\Git\bin\bash.exe", @"C:\Windows\System32\schtasks.exe"];

    private static InstanceLayout Layout(string root = Base + @"\runs\1\sandbox\upgrade") => new("upgrade", root);

    [Fact]
    public void ProductionLocationsNameEveryGuardedRootAndSetupOwnedFile()
    {
        Assert.Contains(Path.Combine(Local, "ZeusApps.BellTower"), Production.Roots);
        Assert.Contains(Path.Combine(Local, "Programs", "ZeusApps.BellTower"), Production.Roots);
        Assert.Contains(Path.Combine(Profile, ".claude"), Production.Roots);
        Assert.Contains(Path.Combine(Profile, ".codex"), Production.Roots);
        Assert.Contains(Path.Combine(Roaming, "Code"), Production.Roots);
        Assert.Contains(@"E:\custom-data", Production.Roots);
        Assert.Equal(7, Production.Roots.Count);
        Assert.Contains(Path.Combine(Profile, ".claude", "settings.json"), Production.WatchedFiles);
        Assert.Contains(Path.Combine(Profile, ".codex", "hooks.json"), Production.WatchedFiles);
        Assert.Contains(Path.Combine(Local, "ZeusApps.BellTower", "integration.json"), Production.WatchedFiles);
    }

    [Fact]
    public void ASandboxInsideTheArtifactsBaseIsAccepted() =>
        Assert.Empty(ProductionGuard.CheckLayout(Layout(), Production, Base));

    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Local\ZeusApps.BellTower\sandbox")]
    [InlineData(@"C:\Users\someone\AppData\Local\Programs\ZeusApps.BellTower")]
    [InlineData(@"C:\Users\someone\.claude\field")]
    [InlineData(@"C:\Users\someone\.codex")]
    [InlineData(@"E:\custom-data\x")]
    public void ASandboxInsideAProductionLocationIsRefused(string root)
    {
        var violations = ProductionGuard.CheckLayout(Layout(root), Production, root);
        Assert.NotEmpty(violations);
        Assert.Contains(violations, violation => violation.Contains("overlaps a production location", StringComparison.Ordinal));
    }

    [Fact]
    public void ASandboxThatContainsAProductionLocationIsRefused()
    {
        // The whole user profile as the "sandbox" would wrap ~/.claude and ~/.codex.
        var violations = ProductionGuard.CheckLayout(Layout(Profile), Production, Profile);
        Assert.Contains(violations, violation => violation.Contains("overlaps a production location", StringComparison.Ordinal));
    }

    [Fact]
    public void ASandboxOutsideTheArtifactsBaseIsRefused()
    {
        var violations = ProductionGuard.CheckLayout(Layout(@"D:\elsewhere\sandbox"), Production, Base);
        Assert.Contains(violations, violation => violation.Contains("outside the harness artifacts base", StringComparison.Ordinal));
    }

    [Fact]
    public void ASiblingWithTheSamePrefixIsNotMistakenForAProductionLocation()
    {
        Assert.False(PathRelation.Overlaps(@"C:\Users\someone\.claude-sandbox", @"C:\Users\someone\.claude"));
        Assert.True(PathRelation.Overlaps(@"C:\Users\someone\.claude\projects", @"C:\Users\someone\.claude"));
        Assert.True(PathRelation.Overlaps(@"c:\users\SOMEONE\.CLAUDE", @"C:\Users\someone\.claude"));
        Assert.True(PathRelation.Mentions("bash -c 'C:/Users/someone/.codex/hooks.json'", @"C:\Users\someone\.codex"));
        Assert.False(PathRelation.Mentions("D:/repo/artifacts/home/.codex/hooks.json", @"C:\Users\someone\.codex"));
    }

    [Fact]
    public void TheChildEnvironmentIsBuiltFromAnAllowListAndSandboxValues()
    {
        var parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = @"C:\Windows",
            ["USERPROFILE"] = Profile,
            ["APPDATA"] = Roaming,
            ["LOCALAPPDATA"] = Local,
            ["BELLTOWER_DATA_DIR"] = Path.Combine(Local, "ZeusApps.BellTower"),
            ["CLAUDE_CONFIG_DIR"] = Path.Combine(Profile, ".claude"),
            ["ANTHROPIC_API_KEY"] = "secret",
            ["PATH"] = string.Join(';', @"C:\Windows\System32", Path.Combine(Local, "Programs", "ZeusApps.BellTower"), Path.Combine(Profile, ".codex", "bin"), @"C:\Program Files\Git\bin")
        };
        var layout = Layout();
        var (environment, removed) = ProductionGuard.BuildEnvironment(parent, layout, Production);

        Assert.Equal(layout.Home, environment["USERPROFILE"]);
        Assert.Equal(layout.Home, environment["HOME"]);
        Assert.Equal(layout.LocalAppData, environment["LOCALAPPDATA"]);
        Assert.Equal(layout.RoamingAppData, environment["APPDATA"]);
        Assert.Equal(layout.Data, environment["BELLTOWER_DATA_DIR"]);
        Assert.Equal(layout.ClaudeDirectory, environment["CLAUDE_CONFIG_DIR"]);
        Assert.Equal(layout.CodexDirectory, environment["CODEX_HOME"]);
        Assert.Equal(layout.Temp, environment["TEMP"]);
        Assert.Equal(@"C:\Windows", environment["SystemRoot"]);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", environment.Keys);
        Assert.Equal(2, removed.Count);
        var path = environment["PATH"].Split(';');
        Assert.Equal(layout.FakeBin, path[0]);
        Assert.Equal([layout.FakeBin, @"C:\Windows\System32", @"C:\Program Files\Git\bin"], path);
        Assert.Empty(ProductionGuard.CheckEnvironment(environment, Production));
    }

    [Fact]
    public void AnEnvironmentValueOrPathEntryNamingProductionIsRefused()
    {
        var violations = ProductionGuard.CheckEnvironment(new Dictionary<string, string>
        {
            ["BELLTOWER_DATA_DIR"] = Path.Combine(Local, "ZeusApps.BellTower"),
            ["PATH"] = @"C:\Windows;" + Path.Combine(Profile, ".codex", "bin")
        }, Production);
        Assert.Contains(violations, violation => violation.Contains("BELLTOWER_DATA_DIR", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("PATH", StringComparison.Ordinal));
    }

    [Fact]
    public void ACommandInsideTheSandboxWithItsOwnDataDirectoryIsAllowed()
    {
        var layout = Layout();
        Assert.Empty(ProductionGuard.CheckCommand(layout.Launcher, ["doctor", "--data-dir", layout.Data], layout, Production, Tools));
        Assert.Empty(ProductionGuard.CheckCommand(layout.Launcher, ["--version"], layout, Production, Tools));
        Assert.Empty(ProductionGuard.CheckCommand(Tools[0], ["-c", "echo ok"], layout, Production, Tools));
    }

    [Fact]
    public void ACommandThatCouldReachProductionIsRefused()
    {
        var layout = Layout();
        var production = Production.InstallationRoot;

        Assert.NotEmpty(ProductionGuard.CheckCommand(Path.Combine(production, "bell-tower.exe"), ["doctor"], layout, Production, Tools));
        Assert.NotEmpty(ProductionGuard.CheckCommand(@"C:\somewhere\else.exe", [], layout, Production, Tools));
        Assert.Contains(ProductionGuard.CheckCommand(layout.Launcher, ["doctor"], layout, Production, Tools), violation => violation.Contains("--data-dir", StringComparison.Ordinal));
        Assert.Contains(ProductionGuard.CheckCommand(layout.Launcher, ["doctor", "--data-dir", Production.DataRoot], layout, Production, Tools),
            violation => violation.Contains("production location", StringComparison.Ordinal));
        Assert.Contains(ProductionGuard.CheckCommand(layout.Launcher, ["setup", "--home", Profile, "--apply", "--data-dir", layout.Data], layout, Production, Tools),
            violation => violation.Contains("user's own profile", StringComparison.Ordinal));
        Assert.Contains(ProductionGuard.CheckEnvironment(new Dictionary<string, string> { ["HOME"] = Profile }, Production),
            violation => violation.Contains("user's own profile", StringComparison.Ordinal));
        Assert.Contains(ProductionGuard.CheckCommand(Tools[0], ["-c", "cat C:/Users/someone/.claude/settings.json"], layout, Production, Tools),
            violation => violation.Contains("names a production location", StringComparison.Ordinal));
        Assert.Contains(ProductionGuard.CheckCommand(layout.Launcher, ["doctor", "--data-dir", @"D:\repo\artifacts\field-acceptance\runs\2\sandbox\other\data"], layout, Production, Tools),
            violation => violation.Contains("outside the sandbox", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSelfTestProvesEveryRefusalAgainstTheGivenProductionLocations()
    {
        var probes = GuardSelfTest.Run(Production, Base, Tools);
        Assert.True(probes.Count >= 9);
        Assert.All(probes, probe => Assert.True(probe.Passed, probe.Id + ": " + probe.Detail));
        Assert.Contains(probes, probe => probe.Id == "sandbox-in-.claude");
        Assert.Contains(probes, probe => probe.Id == "command-production-profile");
    }

    [Fact]
    public void RequireThrowsAnIsolationExceptionListingEveryViolation()
    {
        Assert.Throws<IsolationException>(() => ProductionGuard.Require(["a", "b"]));
        var thrown = Assert.Throws<IsolationException>(() => ProductionGuard.Require(["first", "second"]));
        Assert.Equal(["first", "second"], thrown.Violations);
        ProductionGuard.Require([]);
    }
}

using System.Text.Json.Nodes;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class NativeHostSetupTests
{
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    [InlineData("both")]
    public void NativeHostSetupInstallsOnlyHooksAndOptionalBridge(string clients)
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);

        var preview = setup.Plan(clients, hostBridge: true);

        Assert.Contains(preview.Notes, note => note.Contains("VS Code focus bridge", StringComparison.OrdinalIgnoreCase));
        var codeSettings = Path.Combine(fixture, "AppData", "Roaming", "Code", "User", "settings.json");
        var bridgeSettings = Assert.Single(preview.Changes, change => Path.GetFullPath(change.Path).Equals(codeSettings, StringComparison.OrdinalIgnoreCase));
        var configuredBridge = JsonNode.Parse(bridgeSettings.After!)!;
        Assert.Equal("C:/Programs/Bell Tower/bell-tower.exe", configuredBridge["bellTower.executablePath"]!.GetValue<string>());
        Assert.Equal(workspace.Paths.Root, configuredBridge["bellTower.dataDirectory"]!.GetValue<string>());
        Assert.Equal("powershell.exe", configuredBridge["terminal.integrated.profiles.windows"]!["My Shell"]!["path"]!.GetValue<string>());
        Assert.Null(configuredBridge["terminal.integrated.tabs.title"]);
        Assert.DoesNotContain(preview.Changes, change => change.Path.Contains("profile.ps1", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1")));

        preview.Apply(workspace.Paths);
        setup.Plan(clients, hostBridge: true).Apply(workspace.Paths);

        if (clients is "claude" or "both")
        {
            var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, ".claude", "settings.json")))!;
            Assert.NotNull(settings["hooks"]);
            Assert.Equal("keep", settings["model"]!.GetValue<string>());
        }
        if (clients is "codex" or "both")
        {
            var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, ".codex", "hooks.json")))!;
            Assert.NotNull(settings["hooks"]);
        }

        Assert.Contains("function My-Helper", File.ReadAllText(Path.Combine(fixture, "Documents", "PowerShell", "profile.ps1")));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1")));

        setup.Plan(clients, remove: true, hostBridge: true).Apply(workspace.Paths);

        if (clients is "claude" or "both")
        {
            var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, ".claude", "settings.json")))!;
            Assert.True(settings["hooks"] is null || !settings["hooks"]!.AsObject().Any());
            Assert.Equal("keep", settings["model"]!.GetValue<string>());
        }
        if (clients is "codex" or "both")
        {
            var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, ".codex", "hooks.json")))!;
            Assert.True(settings["hooks"] is null || !settings["hooks"]!.AsObject().Any());
        }

        Assert.Contains("function My-Helper", File.ReadAllText(Path.Combine(fixture, "Documents", "PowerShell", "profile.ps1")));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1")));
    }

    [Fact]
    public void RemovingNativeHooksAndBridgePreservesExistingDesktopProfilesAndImports()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);
        setup.Plan("both", hostBridge: true).Apply(workspace.Paths);

        var module = File.ReadAllText(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1"));
        var powershellProfile = Path.Combine(fixture, "Documents", "PowerShell", "profile.ps1");
        var imports = File.ReadAllText(powershellProfile);
        var codeSettings = Path.Combine(fixture, "AppData", "Roaming", "Code", "User", "settings.json");
        var profiles = JsonNode.Parse(File.ReadAllText(codeSettings))!["terminal.integrated.profiles.windows"]!.ToJsonString();

        setup.Plan("both", remove: true, hostBridge: true).Apply(workspace.Paths);

        Assert.Equal(module, File.ReadAllText(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1")));
        Assert.Equal(imports, File.ReadAllText(powershellProfile));
        Assert.Equal(profiles, JsonNode.Parse(File.ReadAllText(codeSettings))!["terminal.integrated.profiles.windows"]!.ToJsonString());
        Assert.Contains("function My-Helper", File.ReadAllText(powershellProfile));
    }

    [Fact]
    public void HostBridgeCanPrecedeDesktopSetupAndProviderRemovalKeepsOtherProviderAndDesktop()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);
        setup.Plan("both", hostBridge: true).Apply(workspace.Paths);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);

        var module = File.ReadAllText(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1"));
        var profilePath = Path.Combine(fixture, "Documents", "PowerShell", "profile.ps1");
        var profile = File.ReadAllText(profilePath);
        setup.Plan("claude", remove: true, hostBridge: true).Apply(workspace.Paths);

        var claude = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, ".claude", "settings.json")))!;
        var codex = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture, ".codex", "hooks.json")))!;
        Assert.True(claude["hooks"] is null || !claude["hooks"]!.AsObject().Any());
        Assert.NotNull(codex["hooks"]);
        Assert.Equal(module, File.ReadAllText(Path.Combine(workspace.Paths.Root, "shell", "BellTower.psm1")));
        Assert.Equal(profile, File.ReadAllText(profilePath));
    }

    [Fact]
    public void NativeBridgeRemovalReadsLegacyOwnershipWithoutDeletingItsBridge()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var manifest = Path.Combine(workspace.Paths.Root, "integration.json");
        File.WriteAllText(manifest, """{"commands":{},"statusLines":{},"desktop":{"providers":["claude"],"bridgeInstalled":true}}""");
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);

        setup.Plan("both", remove: true, hostBridge: true).Apply(workspace.Paths);

        var ownership = JsonNode.Parse(File.ReadAllText(manifest))!;
        Assert.True(ownership["desktop"]!["bridgeInstalled"]!.GetValue<bool>());
        Assert.Contains("claude", ownership["desktop"]!["providers"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.False(ownership["nativeBridgeProviders"]!.AsArray().Any());
    }

    [Fact]
    public void NativeOnlyProviderRemovalRetainsBridgeSettingsUntilLastProviderLeaves()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);
        setup.Plan("both", hostBridge: true).Apply(workspace.Paths);
        var codeSettings = Path.Combine(fixture, "AppData", "Roaming", "Code", "User", "settings.json");

        setup.Plan("claude", remove: true, hostBridge: true).Apply(workspace.Paths);

        var partial = JsonNode.Parse(File.ReadAllText(codeSettings))!;
        Assert.Equal(workspace.Paths.Root, partial["bellTower.dataDirectory"]!.GetValue<string>());
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace.Paths.Root, "integration.json")))!;
        Assert.Equal(new[] { "codex" }, manifest["nativeBridgeProviders"]!.AsArray().Select(value => value!.GetValue<string>()));

        setup.Plan("codex", remove: true, hostBridge: true).Apply(workspace.Paths);

        var final = JsonNode.Parse(File.ReadAllText(codeSettings))!;
        Assert.Null(final["bellTower.executablePath"]);
        Assert.Null(final["bellTower.dataDirectory"]);
        manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace.Paths.Root, "integration.json")))!;
        Assert.Empty(manifest["nativeBridgeProviders"]!.AsArray());
    }

    [Fact]
    public void GenericProviderRemovalReleasesOnlyItsNativeBridgeOwnership()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);
        setup.Plan("both", hostBridge: true).Apply(workspace.Paths);

        setup.Plan("claude", remove: true).Apply(workspace.Paths);

        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace.Paths.Root, "integration.json")))!;
        Assert.Equal(new[] { "codex" }, manifest["nativeBridgeProviders"]!.AsArray().Select(value => value!.GetValue<string>()));
        var settingsPath = Path.Combine(fixture, ".codex", "hooks.json");
        Assert.NotNull(JsonNode.Parse(File.ReadAllText(settingsPath))!["hooks"]);
        var codeSettings = Path.Combine(fixture, "AppData", "Roaming", "Code", "User", "settings.json");
        Assert.Equal(workspace.Paths.Root, JsonNode.Parse(File.ReadAllText(codeSettings))!["bellTower.dataDirectory"]!.GetValue<string>());
    }

    [Fact]
    public void NativeBridgeSettingsRestorePriorValuesAndKeepLaterUserEdits()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var codeSettings = Path.Combine(fixture, "AppData", "Roaming", "Code", "User", "settings.json");
        File.WriteAllText(codeSettings, """{"bellTower.executablePath":"C:/Personal/bell-tower.exe","bellTower.dataDirectory":"C:/Personal/data","editor.fontSize":15}""");
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);
        setup.Plan("both", hostBridge: true).Apply(workspace.Paths);
        var configured = JsonNode.Parse(File.ReadAllText(codeSettings))!;
        Assert.Equal("C:/Programs/Bell Tower/bell-tower.exe", configured["bellTower.executablePath"]!.GetValue<string>());
        Assert.Equal(workspace.Paths.Root, configured["bellTower.dataDirectory"]!.GetValue<string>());
        Assert.Equal(15, configured["editor.fontSize"]!.GetValue<int>());

        configured["bellTower.dataDirectory"] = "C:/Personal/changed-data";
        File.WriteAllText(codeSettings, configured.ToJsonString());
        setup.Plan("both", remove: true, hostBridge: true).Apply(workspace.Paths);

        var restored = JsonNode.Parse(File.ReadAllText(codeSettings))!;
        Assert.Equal("C:/Personal/bell-tower.exe", restored["bellTower.executablePath"]!.GetValue<string>());
        Assert.Equal("C:/Personal/changed-data", restored["bellTower.dataDirectory"]!.GetValue<string>());
        Assert.Equal(15, restored["editor.fontSize"]!.GetValue<int>());
    }

    [Fact]
    public void FailedBridgeRemovalKeepsOwnershipAndCanRetryWithoutLosingPriorSettings()
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var path = Path.Combine(fixture, "AppData", "Roaming", "Code", "User", "settings.json");
        File.WriteAllText(path, """{"bellTower.executablePath":"C:/Previous/bell-tower.exe","bellTower.dataDirectory":"C:/Previous/data"}""");
        var settings = new JsonSettingsFile(path);
        var ownership = new DesktopOwnership();
        var notes = new List<string>();
        OwnedSettings.Set(settings, path, "bellTower.executablePath", JsonNode.Parse("\"C:/Programs/Bell Tower/bell-tower.exe\""), false,
            ownership.Settings, notes);
        OwnedSettings.Set(settings, path, "bellTower.dataDirectory", JsonNode.Parse("\"D:/disposable/data\""), false,
            ownership.Settings, notes);
        var installChanges = new List<FileChange>();
        settings.AddTo(installChanges);
        new SetupPlan(installChanges).Apply(workspace.Paths);

        var manifestPath = Path.Combine(workspace.Paths.Root, "integration.json");
        var installedOwnership = new IntegrationOwnership([], [])
        {
            NativeBridgeProviders = new HashSet<string>(["claude", "codex"], StringComparer.OrdinalIgnoreCase),
            Desktop = ownership
        };
        AtomicFile.Write(manifestPath, Json.Encode(installedOwnership));
        var manifestBefore = File.ReadAllText(manifestPath);
        var removalOwnership = Json.Decode<IntegrationOwnership>(manifestBefore);
        removalOwnership.NativeBridgeProviders.Clear();
        settings = new JsonSettingsFile(path);
        var removalChanges = new List<FileChange>();
        OwnedSettings.Set(settings, path, "bellTower.executablePath", null, true, removalOwnership.Desktop.Settings, notes);
        OwnedSettings.Set(settings, path, "bellTower.dataDirectory", null, true, removalOwnership.Desktop.Settings, notes);
        settings.AddTo(removalChanges);
        var manifestChange = new FileChange(manifestPath, manifestBefore, Json.Encode(removalOwnership));
        Assert.ThrowsAny<Exception>(() => new SetupPlan(removalChanges,
            [new("Failing disposable bridge removal", Path.Combine(workspace.Root, "does-not-exist.exe"), [])],
            commitAfterActions: manifestChange).Apply(workspace.Paths));
        Assert.Equal(manifestBefore, File.ReadAllText(manifestPath));

        var retryOwnership = Json.Decode<IntegrationOwnership>(File.ReadAllText(manifestPath));
        retryOwnership.NativeBridgeProviders.Clear();
        settings = new JsonSettingsFile(path);
        var retryChanges = new List<FileChange>();
        OwnedSettings.Set(settings, path, "bellTower.executablePath", null, true, retryOwnership.Desktop.Settings, notes);
        OwnedSettings.Set(settings, path, "bellTower.dataDirectory", null, true, retryOwnership.Desktop.Settings, notes);
        settings.AddTo(retryChanges);
        var retryManifest = new FileChange(manifestPath, manifestBefore, Json.Encode(retryOwnership));
        new SetupPlan(retryChanges, commitAfterActions: retryManifest).Apply(workspace.Paths);

        var restored = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal("C:/Previous/bell-tower.exe", restored["bellTower.executablePath"]!.GetValue<string>());
        Assert.Equal("C:/Previous/data", restored["bellTower.dataDirectory"]!.GetValue<string>());
        Assert.All(Json.Decode<IntegrationOwnership>(File.ReadAllText(manifestPath)).Desktop.Settings.Values,
            values => Assert.Empty(values));
    }

    [Theory]
    [InlineData(true, false, "desktop profiles")]
    [InlineData(false, true, "status line")]
    public void HostBridgeSelectionRejectsPresentationAndProfileModes(bool desktop, bool statusLine, string expected)
    {
        using var workspace = new TestWorkspace();
        var fixture = CreateHome(workspace);
        var setup = new IntegrationSetup(workspace.Paths, "C:/Programs/Bell Tower/bell-tower.exe", fixture);

        var error = Assert.Throws<ArgumentException>(() => setup.Plan("both", desktop: desktop, statusLine: statusLine, hostBridge: true));

        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "integration.json")));
    }

    private static string CreateHome(TestWorkspace workspace)
    {
        var home = Path.Combine(workspace.Root, "home");
        Directory.CreateDirectory(Path.Combine(home, ".claude"));
        Directory.CreateDirectory(Path.Combine(home, ".codex"));
        Directory.CreateDirectory(Path.Combine(home, "Documents", "PowerShell"));
        Directory.CreateDirectory(Path.Combine(home, "AppData", "Roaming", "Code", "User"));
        File.WriteAllText(Path.Combine(home, ".claude", "settings.json"), "{\"model\":\"keep\"}");
        File.WriteAllText(Path.Combine(home, ".codex", "hooks.json"), "{\"model\":\"keep\"}");
        File.WriteAllText(Path.Combine(home, "Documents", "PowerShell", "profile.ps1"), "function My-Helper { 'keep' }\n");
        File.WriteAllText(Path.Combine(home, "AppData", "Roaming", "Code", "User", "settings.json"), "{\"editor.fontSize\":15,\"terminal.integrated.profiles.windows\":{\"My Shell\":{\"path\":\"powershell.exe\"}}}");
        return home;
    }
}

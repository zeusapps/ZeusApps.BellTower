using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class ProfileMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpgradeRemovesOwnedRoleMenusAndPreservesOriginalOrEditedProfiles(bool edited)
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        var code = Path.Combine(home, "AppData", "Roaming", "Code", "User", "settings.json");
        var terminal = Path.Combine(home, "AppData", "Local", "Microsoft", "Windows Terminal", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(code)!);
        Directory.CreateDirectory(Path.GetDirectoryName(terminal)!);
        File.WriteAllText(code, "{}");
        File.WriteAllText(terminal, "{}");
        var setup = new IntegrationSetup(workspace.Paths, "C:/BellTower/bell-tower.exe", home);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);
        var manifest = Path.Combine(workspace.Paths.Root, "integration.json");
        var ownership = AtomicFile.Read<IntegrationOwnership>(manifest)!;
        var vs = Read(code);
        var wt = Read(terminal);
        var profiles = vs["terminal.integrated.profiles.windows"]!.AsObject();
        var list = wt["profiles"]!["list"]!.AsArray();
        const string original = """{"source":"PowerShell","args":["-NoExit","-Command","Start-ClaudeCodePM"],"overrideName":true}""";
        foreach (var role in new[] { "claude", "codex" }.SelectMany(RoleProfile.LegacyFor))
        {
            var entry = role.Label == "Claude PM" ? JsonNode.Parse(original)!.AsObject() : new JsonObject
            {
                ["source"] = "PowerShell",
                ["args"] = new JsonArray("-NoExit", "-Command", role.Function)
            };
            entry.Remove("overrideName");
            ownership.Desktop.Profiles.Add(new(code, "vscode", role.Provider, role.Label,
                role.Label == "Claude PM" ? original : null, entry.ToJsonString()));
            if (edited && role.Label == "Codex QA")
            {
                entry["icon"] = "my-custom-icon";
            }
            profiles[role.Label] = entry;
            var guid = "{" + new Guid(Convert.FromHexString(ContentHash.Compute("bell-tower-profile:" + role.Label)[..32])) + "}";
            var wtEntry = new JsonObject { ["guid"] = guid, ["name"] = role.Label, ["commandline"] = role.Function };
            ownership.Desktop.Profiles.Add(new(terminal, "windows-terminal", role.Provider, guid, null, wtEntry.ToJsonString()));
            if (edited && role.Label == "Codex QA")
            {
                wtEntry["icon"] = "my-custom-icon";
            }
            list.Add(wtEntry);
        }
        File.WriteAllText(code, vs.ToJsonString());
        File.WriteAllText(terminal, wt.ToJsonString());
        AtomicFile.Write(manifest, Json.Encode(ownership));
        setup.Plan("both", desktop: true).Apply(workspace.Paths);
        setup.Plan("both", desktop: true).Apply(workspace.Paths);

        profiles = Read(code)["terminal.integrated.profiles.windows"]!.AsObject();
        Assert.Equal(edited ? 4 : 3, profiles.Count);
        Assert.True(profiles["Claude PM"]!["overrideName"]!.GetValue<bool>());
        Assert.NotNull(profiles["Claude"]);
        Assert.NotNull(profiles["Codex"]);
        Assert.Equal(edited ? 3 : 2, Read(terminal)["profiles"]!["list"]!.AsArray().Count);
        if (edited)
        {
            Assert.Equal("my-custom-icon", profiles["Codex QA"]!["icon"]!.GetValue<string>());
        }
        setup.Plan("both", remove: true).Apply(workspace.Paths);
        Assert.Equal(edited ? 2 : 1, Read(code)["terminal.integrated.profiles.windows"]!.AsObject().Count);
    }

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path), documentOptions:
        new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
}

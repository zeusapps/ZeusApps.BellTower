using System.Text.Json.Nodes;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class FastHookSetupTests
{
    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void FastCommandsQuoteUnicodeSpacesAndApostrophesWithoutPowerShell(string provider)
    {
        var paths = new DataPaths("D:/Δ O'Brien/data");
        var command = new HookCommandBuilder("unused", paths, "D:/Δ O'Brien/helper.exe").Build("event", provider);
        Assert.Contains("Δ", command);
        Assert.Contains("--provider " + provider, command);
        Assert.DoesNotContain("powershell", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EncodedCommand", command);
        Assert.DoesNotContain("unused", command);
        Assert.Contains(provider == "claude" ? "'\"'\"'" : "O'Brien", command);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void EditedHandlerRequiresExplicitPreviewedMigrationAndDoesNotDuplicate(string provider)
    {
        using var workspace = new TestWorkspace();
        var path = Path.Combine(workspace.Root, "settings.json");
        var initialOwnership = new IntegrationOwnership([], []);
        var legacy = new ProviderSettingsEditor(new HookCommandBuilder("legacy.exe", workspace.Paths));
        var settings = JsonNode.Parse(legacy.Edit(provider, path, null, new([], []), initialOwnership, false, false))!;
        var edited = settings["hooks"]!["Stop"]![0]!["hooks"]![0]!;
        edited["timeout"] = 3;
        edited["async"] = true;
        settings["keep"] = "unrelated";
        var notes = new List<string>();
        var fast = new ProviderSettingsEditor(new HookCommandBuilder("legacy.exe", workspace.Paths, "D:/fixture/helper.exe"));
        var next = new IntegrationOwnership(new(initialOwnership.Commands), []) { Handlers = new(initialOwnership.Handlers) };
        var before = settings.ToJsonString();
        var ordinary = fast.Edit(provider, path, before, initialOwnership, next, false, false, notes: notes);
        var retained = JsonNode.Parse(ordinary)!["hooks"]!["Stop"]!.AsArray();
        Assert.Single(retained);
        Assert.True(JsonNode.DeepEquals(edited, retained[0]!["hooks"]![0]));
        Assert.Contains(notes, note => note.Contains("--migrate-edited-hooks", StringComparison.Ordinal));
        var migratedOwnership = new IntegrationOwnership(new(next.Commands), []) { Handlers = new(next.Handlers) };
        var migrated = fast.Edit(provider, path, ordinary, next, migratedOwnership, false, false, migrateEdited: true, notes: notes);
        Assert.Contains("helper.exe", migrated);
        Assert.Contains("unrelated", migrated);
        var repeated = fast.Edit(provider, path, migrated, migratedOwnership,
            new(new(migratedOwnership.Commands), []) { Handlers = new(migratedOwnership.Handlers) }, false, false, migrateEdited: true);
        Assert.Single(JsonNode.Parse(repeated)!["hooks"]!["Stop"]!.AsArray());
        Assert.Equal(ordinary, fast.Edit(provider, path, before, initialOwnership,
            new(new(initialOwnership.Commands), []) { Handlers = new(initialOwnership.Handlers) }, false, false));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void FastHandlersAreAsyncExceptForSessionEnd(string provider)
    {
        using var workspace = new TestWorkspace();
        var ownership = new IntegrationOwnership([], []);
        var editor = new ProviderSettingsEditor(new HookCommandBuilder("unused", workspace.Paths, "D:/fixture/helper.exe"));
        var settings = JsonNode.Parse(editor.Edit(provider, "settings.json", null, new([], []), ownership, false, false))!;
        Assert.True(settings["hooks"]!["Stop"]![0]!["hooks"]![0]!["async"]!.GetValue<bool>());
        Assert.Null(settings["hooks"]!["SessionEnd"]![0]!["hooks"]![0]!["async"]);
        Assert.NotEmpty(ownership.Handlers["settings.json"]);
    }

    [Fact]
    public void EnabledCaptureProtocolRefusesIncompatibleConsumerButAllowsLegacyWriters()
    {
        using var workspace = new TestWorkspace();
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "capture-protocol.json"), "{\"protocol\":1}");
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.Ensure(workspace.Paths.Root, 0));
        CaptureCompatibility.Ensure(workspace.Paths.Root, 1);
        CaptureCompatibility.EnsureTrayInvocation(["event", "--data-dir", workspace.Paths.Root], 0);
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.EnsureTrayInvocation(["tray", "--data-dir", workspace.Paths.Root], 0));
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.EnsureTrayInvocation(["--data-dir", workspace.Paths.Root], 0));
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.EnsureTrayInvocation(["--silent", "--data-dir=" + workspace.Paths.Root], 0));
        CaptureCompatibility.EnsureTrayInvocation(["--data-dir", workspace.Paths.Root, "doctor"], 0);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AppliedMigrationBacksUpEditedSettingsAndPreservesUnknownManifestFields(string provider)
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        var setup = new IntegrationSetup(workspace.Paths, "D:/development/bell-tower.exe", home);
        setup.Plan(provider).Apply(workspace.Paths);
        var path = Path.Combine(home, "." + provider, provider == "claude" ? "settings.json" : "hooks.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["hooks"]!["Stop"]![0]!["hooks"]![0]!["async"] = true;
        var edited = node.ToJsonString();
        File.WriteAllText(path, edited);
        var manifest = Path.Combine(workspace.Paths.Root, "integration.json");
        var ownership = JsonNode.Parse(File.ReadAllText(manifest))!;
        ownership["futureOwnership"] = 42;
        File.WriteAllText(manifest, ownership.ToJsonString());
        var preview = setup.Plan(provider, migrateEditedHooks: true);
        Assert.Equal(edited, File.ReadAllText(path));
        Assert.Contains(preview.Notes, note => note.Contains("Explicitly replace edited Stop", StringComparison.Ordinal));
        preview.Apply(workspace.Paths);
        Assert.Contains(Directory.GetFiles(Path.Combine(workspace.Paths.Root, "integration-backups"), "*.json", SearchOption.AllDirectories),
            backup => File.ReadAllText(backup) == edited);
        Assert.Equal(42, JsonNode.Parse(File.ReadAllText(manifest))!["futureOwnership"]!.GetValue<int>());
        setup.Plan(provider, migrateEditedHooks: true).Apply(workspace.Paths);
        Assert.Single(JsonNode.Parse(File.ReadAllText(path))!["hooks"]!["Stop"]!.AsArray());
    }
}

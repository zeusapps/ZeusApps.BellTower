using BellTower.Setup;
using BellTower.Tests.Testing;
using Tomlyn;
using Tomlyn.Model;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class CodexFooterTests
{
    [Theory]
    [InlineData("# keep\nmodel = 'keep'\n[tui]\n# comment\nstatus_line = ['model', 'git-branch'] # footer\nanimations = false\n")]
    [InlineData("# keep\n[tui]\nstatus_line = [\n 'model', # inside\n 'git-branch',\n]\nanimations = false\n")]
    [InlineData("tui.status_line = ['model', 'git-branch']\n[other]\nkey = 'keep'\n")]
    [InlineData("tui = { status_line = ['model', 'git-branch'], animations = false }\n")]
    [InlineData("[tui]\nstatus_line = ['model', '🗼', 'quoted\"value', 'path\\name'] # keep\n")]
    public void ExistingFooterRoundTripsThroughOwnership(string before)
    {
        Dictionary<string, OwnedValue> previous = [];
        Dictionary<string, OwnedValue> installed = [];
        var after = CodexFooterSettings.Edit("config", before, previous, installed, false, []);
        Assert.NotNull(after);
        Assert.Contains("thread-name", after);
        Assert.Contains("git-branch", after);
        var original = (TomlArray)((TomlTable)TomlSerializer.Deserialize<TomlTable>(before)!["tui"]!)["status_line"]!;
        var updated = (TomlArray)((TomlTable)TomlSerializer.Deserialize<TomlTable>(after)!["tui"]!)["status_line"]!;
        Assert.Equal(original, updated.Take(original.Count));
        var repeated = CodexFooterSettings.Edit("config", after, installed, new(installed), false, []);
        Assert.Equal(after, repeated);
        Assert.Equal(before, CodexFooterSettings.Edit("config", after, installed, new(installed), true, []));
    }

    [Theory]
    [InlineData("")]
    [InlineData("model = 'keep'\n")]
    [InlineData("[tui]\nanimations = false\n")]
    [InlineData("tui.animations = false\n[other]\nkey = 'keep'\n")]
    public void MissingFooterCanBeInstalledAndRemoved(string before)
    {
        Dictionary<string, OwnedValue> owned = [];
        var after = CodexFooterSettings.Edit("config", before, [], owned, false, [])!;
        var model = TomlSerializer.Deserialize<TomlTable>(after)!;
        var items = (TomlArray)((TomlTable)model["tui"]!)["status_line"]!;
        Assert.Contains("thread-name", items);
        Assert.Contains("git-branch", items);
        var removed = CodexFooterSettings.Edit("config", after, owned, new(owned), true, [])!;
        Assert.DoesNotContain("status_line", removed);
        Assert.Contains(before.Trim(), removed);
    }

    [Fact]
    public void UserEditsAndUnrelatedSettingsArePreserved()
    {
        Dictionary<string, OwnedValue> owned = [];
        var after = CodexFooterSettings.Edit("config", "[tui]\nstatus_line = ['model']\n", [], owned, false, [])!;
        var unrelatedEdit = after + "\n[other]\nvalue = 'user'\n";
        var removed = CodexFooterSettings.Edit("config", unrelatedEdit, owned, new(owned), true, [])!;
        Assert.Contains("status_line = ['model']", removed);
        Assert.Contains("value = 'user'", removed);
        var userEdit = after.Replace("thread-name", "current-dir");
        var ownership = new Dictionary<string, OwnedValue>(owned);
        List<string> notes = [];
        Assert.Equal(userEdit, CodexFooterSettings.Edit("config", userEdit, owned, ownership, true, notes));
        Assert.Empty(ownership);
        Assert.Single(notes);
    }

    [Fact]
    public void SetupPreviewApplyRerunAndRemovalAreIsolated()
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        var path = Path.Combine(home, ".codex", "config.toml");
        var setup = new IntegrationSetup(workspace.Paths, "C:/Bell Tower/bell-tower.exe", home);
        var preview = setup.Plan("codex", statusLine: true);
        Assert.False(File.Exists(path));
        Assert.Contains(preview.Changes, change => change.Path == path);
        Assert.Contains(preview.Notes, note => note.Contains("dedicated authenticated runtime"));
        preview.Apply(workspace.Paths);
        var after = File.ReadAllText(path);
        setup.Plan("codex", statusLine: true).Apply(workspace.Paths);
        Assert.Equal(after, File.ReadAllText(path));
        File.AppendAllText(path, "\n[other]\nvalue = 'user'\n");
        setup.Plan("codex", remove: true).Apply(workspace.Paths);
        Assert.DoesNotContain("status_line", File.ReadAllText(path));
        Assert.Contains("value = 'user'", File.ReadAllText(path));
    }

    [Fact]
    public void InvalidTomlFailsBeforeAnySetupWrite()
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        var path = Path.Combine(home, ".codex", "config.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "[tui]\nstatus_line = [");
        Assert.Throws<InvalidDataException>(() => new IntegrationSetup(workspace.Paths, "bell-tower.exe", home).Plan("codex", statusLine: true));
        Assert.False(File.Exists(Path.Combine(home, ".codex", "hooks.json")));
    }
}

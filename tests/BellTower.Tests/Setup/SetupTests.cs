using System.Text.Json.Nodes;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class SetupTests
{
    private static IntegrationSetup Setup(TestWorkspace w) => new(w.Paths, "C:/Programs/Bell Tower/bell-tower.exe", Path.Combine(w.Root, "home"));
    [Fact]
    public void DryRunWritesNeitherClientConfigurationNorManifest()
    {
        using var w = new TestWorkspace();
        var plan = Setup(w).Plan("both");
        Assert.Equal(3, plan.Changes.Count);
        Assert.False(Directory.Exists(Path.Combine(w.Root, "home")));
        Assert.False(File.Exists(Path.Combine(w.Paths.Root, "integration.json")));
    }

    [Fact]
    public void InstallAndRemovePreserveUnrelatedHooksAndSettings()
    {
        using var w = new TestWorkspace();
        var file = Path.Combine(w.Root, "home", ".claude", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{"model":"keep","hooks":{"Stop":[{"matcher":"x","hooks":[{"type":"command","command":"echo unrelated"}]}]}}""");
        Setup(w).Plan("both").Apply(w.Paths);
        var installed = JsonNode.Parse(File.ReadAllText(file))!;
        Assert.Equal("keep", installed["model"]!.GetValue<string>());
        Assert.Contains("echo unrelated", File.ReadAllText(file));
        Setup(w).Plan("both", remove: true).Apply(w.Paths);
        var removed = JsonNode.Parse(File.ReadAllText(file))!;
        Assert.Equal("keep", removed["model"]!.GetValue<string>());
        Assert.Single(removed["hooks"]!["Stop"]!.AsArray());
        Assert.Contains("echo unrelated", File.ReadAllText(file));
    }

    [Fact]
    public void RerunDoesNotDuplicateOwnedHooks()
    {
        using var w = new TestWorkspace();
        Setup(w).Plan("codex").Apply(w.Paths);
        Setup(w).Plan("codex").Apply(w.Paths);
        var file = Path.Combine(w.Root, "home", ".codex", "hooks.json");
        var json = JsonNode.Parse(File.ReadAllText(file))!;
        Assert.Single(json["hooks"]!["Stop"]!.AsArray());
    }

    [Fact]
    public void ExistingUnownedStatusLineIsAConflictBeforeAnyWrite()
    {
        using var w = new TestWorkspace();
        var file = Path.Combine(w.Root, "home", ".claude", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        const string original = """{"statusLine":{"type":"command","command":"custom-badge"}}""";
        File.WriteAllText(file, original);
        Assert.Throws<InvalidOperationException>(() => Setup(w).Plan("both", statusLine: true));
        Assert.Equal(original, File.ReadAllText(file));
        Assert.False(File.Exists(Path.Combine(w.Root, "home", ".codex", "hooks.json")));
    }

    [Fact]
    public void OwnedStatusLineCanBeInstalledAndRemoved()
    {
        using var w = new TestWorkspace();
        Setup(w).Plan("claude", statusLine: true).Apply(w.Paths);
        var file = Path.Combine(w.Root, "home", ".claude", "settings.json");
        Assert.Contains("statusLine", File.ReadAllText(file));
        Setup(w).Plan("claude", remove: true).Apply(w.Paths);
        Assert.DoesNotContain("statusLine", File.ReadAllText(file));
    }

    [Fact]
    public void ConcurrentClientEditIsDetectedBeforePlanWrites()
    {
        using var w = new TestWorkspace();
        var plan = Setup(w).Plan("codex");
        var path = Path.Combine(w.Root, "home", ".codex", "hooks.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        Assert.Throws<IOException>(() => plan.Apply(w.Paths));
        Assert.False(File.Exists(Path.Combine(w.Paths.Root, "integration.json")));
    }

    [Fact]
    public void InvalidSettingsPreventWritesForEveryClient()
    {
        using var w = new TestWorkspace();
        var path = Path.Combine(w.Root, "home", ".codex", "hooks.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not-json");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => Setup(w).Plan("both"));
        Assert.False(File.Exists(Path.Combine(w.Root, "home", ".claude", "settings.json")));
    }
}

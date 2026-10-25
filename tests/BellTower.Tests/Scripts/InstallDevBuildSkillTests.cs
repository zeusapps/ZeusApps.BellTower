using Xunit;

namespace BellTower.Tests.Scripts;

// Claude Code reads .claude/skills and Codex reads .agents/skills; the install-dev-build skill must stay byte-identical in both.
public sealed class InstallDevBuildSkillTests
{
    private const string Relative = "install-dev-build/SKILL.md";

    [Fact]
    public void ClientCopiesExistAndAreByteIdentical()
    {
        var claude = Path.Combine(ProjectRoot(), ".claude", "skills", Relative);
        var codex = Path.Combine(ProjectRoot(), ".agents", "skills", Relative);
        Assert.True(File.Exists(claude), claude);
        Assert.True(File.Exists(codex), codex);
        Assert.True(Identical(claude, codex), "The Claude Code and Codex copies of the install-dev-build skill differ.");
    }

    [Fact]
    public void ComparisonDetectsADeliberateOneByteDifference()
    {
        var original = Path.Combine(ProjectRoot(), ".claude", "skills", Relative);
        var directory = Path.Combine(ProjectRoot(), "artifacts", "unit-tests", "skill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var copy = Path.Combine(directory, "SKILL.md");
            File.Copy(original, copy);
            Assert.True(Identical(original, copy));
            var bytes = File.ReadAllBytes(copy);
            bytes[^1] ^= 0x01;
            File.WriteAllBytes(copy, bytes);
            Assert.False(Identical(original, copy));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SkillDeclaresItsNameAndCoversTheWholeProcedureAndSafetyRules()
    {
        var text = File.ReadAllText(Path.Combine(ProjectRoot(), ".agents", "skills", Relative));
        Assert.StartsWith("---", text);
        Assert.Contains("name: install-dev-build", text);
        Assert.Contains("description:", text);
        foreach (var required in new[]
        {
            "package.ps1 -Dev", "-Action Preflight", "-Action Switch", "-Action Rollback", "-Action Retire", "update --status", "doctor",
            "Explicit authorization", "Never stop agent sessions", "--apply", "setup --desktop"
        })
        {
            Assert.Contains(required, text);
        }
    }

    private static bool Identical(string left, string right) => File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));

    private static string ProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Project root missing.");
    }
}

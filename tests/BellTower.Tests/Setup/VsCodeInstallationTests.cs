using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class VsCodeInstallationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FindsBothClassicAndCommitScopedCliResources(bool versioned)
    {
        using var workspace = new TestWorkspace();
        File.WriteAllText(Path.Combine(workspace.Root, "Code.exe"), "discovery fixture only");
        var resourceRoot = versioned ? Path.Combine(workspace.Root, "7baac39ec3") : workspace.Root;
        var cli = Path.Combine(resourceRoot, "resources", "app", "out", "cli.js");
        Directory.CreateDirectory(Path.GetDirectoryName(cli)!);
        File.WriteAllText(cli, "discovery fixture only");
        Directory.CreateDirectory(Path.Combine(workspace.Root, "bin"));
        File.WriteAllText(Path.Combine(workspace.Root, "bin", "code.cmd"), "\"%~dp0..\\Code.exe\" \"%~dp0..\\7baac39ec3\\resources\\app\\out\\cli.js\" %*");
        var installation = VsCodeInstallation.Find([workspace.Root]);
        Assert.NotNull(installation);
        Assert.Equal(cli, installation.CliScript);
    }
}

using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

public sealed class CodexRuntimeProcessTests
{
    [Fact]
    public void HiddenRuntimeHelperReceivesLiteralArgumentsWithoutShellExpansion()
    {
        using var workspace = new TestWorkspace();
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Register) with { Directory = workspace.Root });
        new StateStore(workspace.Paths).Drain();
        var expected = "two words quote\"value literal$() Ω's path trailing\\";
        using var process = CodexRuntimeProcess.Start(BuiltApplication.Executable,
            ["rename", "--session", "claude:session-1", "--name", expected, "--data-dir", workspace.Paths.Root]);
        Assert.True(process.WaitForExit(10000));
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(expected, new IdentityStore(workspace.Paths).Get("claude:session-1", workspace.Root).ManualName);
    }
}

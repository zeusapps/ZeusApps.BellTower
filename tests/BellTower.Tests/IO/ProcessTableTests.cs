using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

// QA-2300: the neighbors of an agent, with the guards that make them trustworthy (creation-time consistency against pid reuse
// and the child bound), driven with real stand-in processes. A parent and its children share one image name (copies of
// cmd.exe in different folders) so they count as same-image relatives.
public sealed class ProcessTableTests : IDisposable
{
    private readonly TestWorkspace workspace = new();
    private readonly string name = "neighbor-" + Guid.NewGuid().ToString("N")[..8] + ".exe";

    public void Dispose()
    {
        StandIns.KillUnder(workspace.Root);
        workspace.Dispose();
    }

    private string Image(string folder, string? imageName = null) => StandIns.CopyImage(workspace, name, folder, imageName ?? name);

    // The parent image runs a batch that starts the given number of children of the given image (detached output and input),
    // writes a flag and stays alive. Returns the parent and its children, found by their exact image paths.
    private async Task<(ProcessIdentity Parent, List<ProcessIdentity> Children)> Start(int children, string? childImageName = null)
    {
        var parentImage = Image("parent");
        var childImage = Image("child", childImageName);
        var flag = Path.Combine(workspace.Root, "ready.flag");
        var script = Path.Combine(workspace.Root, "parent.cmd");
        File.WriteAllText(script,
            "@echo off\r\n" +
            $"for /l %%i in (1,1,{children}) do start \"\" /b \"{childImage}\" /s /c \"ping -n 60 127.0.0.1 >nul\" >nul 2>&1 <nul\r\n" +
            $"echo ready>\"{flag}\"\r\n" +
            "ping -n 60 127.0.0.1 >nul\r\n");
        var process = Process.Start(new ProcessStartInfo(parentImage, $"/s /c \"\"{script}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        Assert.True(await StandIns.WaitFor(flag), "The stand-in parent did not start its children.");
        var parent = ProcessProbe.Identify(process.Id)!;
        var found = new List<ProcessIdentity>();
        for (var attempt = 0; attempt < 100 && (found = StandIns.Find(childImage)).Count < children; attempt++)
        {
            await Task.Delay(50);
        }
        Assert.Equal(children, found.Count);
        return (parent, found);
    }

    [Fact]
    public async Task ARealParentAndChildAreEachOthersNeighbors()
    {
        var (parent, children) = await Start(1);
        var child = Assert.Single(children);
        var table = ProcessTable.Take();
        Assert.Equal(parent, table.Neighbors(child, name)!.Parent);
        var listed = table.Neighbors(parent, name)!;
        Assert.Equal(child, Assert.Single(listed.Children));
        Assert.True(listed.Complete);
        Assert.Null(listed.Parent);
    }

    // A parent that started after the agent cannot be its parent: its identifier was reused.
    [Fact]
    public async Task AParentCreatedAfterTheAgentIsRejected()
    {
        var (parent, children) = await Start(1);
        var older = Assert.Single(children) with { StartedUtcTicks = parent.StartedUtcTicks - 1 };
        Assert.Null(ProcessTable.Take().Neighbors(older, name)!.Parent);
    }

    // A child created before the agent cannot be its child: the identifier of an earlier process was reused.
    [Fact]
    public async Task AChildCreatedBeforeTheAgentIsRejected()
    {
        var (parent, children) = await Start(1);
        var later = parent with { StartedUtcTicks = Assert.Single(children).StartedUtcTicks + 1 };
        var listed = ProcessTable.Take().Neighbors(later, name)!;
        Assert.Empty(listed.Children);
        Assert.True(listed.Complete);
    }

    [Fact]
    public async Task OnlyRelativesOfTheRequestedImageCount()
    {
        var (parent, children) = await Start(1, "other-" + name);
        var table = ProcessTable.Take();
        Assert.Empty(table.Neighbors(parent, name)!.Children);
        Assert.Equal(parent, table.Neighbors(Assert.Single(children), name)!.Parent);
        Assert.Null(table.Neighbors(Assert.Single(children), "other-" + name)!.Parent);
    }

    [Fact]
    public async Task ChildrenAreNotReadWhenNotRequested()
    {
        var (parent, children) = await Start(1);
        var table = ProcessTable.Take();
        var parentOnly = table.Neighbors(Assert.Single(children), name, children: false)!;
        Assert.Equal(parent, parentOnly.Parent);
        var neither = table.Neighbors(parent, name, children: false)!;
        Assert.Empty(neither.Children);
        Assert.True(neither.Complete);
    }

    // The bound is eight: eight children are listed completely, a ninth makes the list incomplete with eight listed.
    [Theory]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public async Task AtMostEightChildrenAreListed(int count, bool complete)
    {
        var (parent, children) = await Start(count);
        var listed = ProcessTable.Take().Neighbors(parent, name)!;
        Assert.Equal(complete, listed.Complete);
        Assert.Equal(Math.Min(count, 8), listed.Children.Count);
        Assert.All(listed.Children, child => Assert.Contains(child, children));
        Assert.Equal(listed.Children.Select(child => child.Pid).Order(), listed.Children.Select(child => child.Pid));
    }
}

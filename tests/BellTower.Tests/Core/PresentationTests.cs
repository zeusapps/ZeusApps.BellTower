using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class PresentationTests
{
    [Fact]
    public void CodexNameAddsOnlyLinkedWorktreeAndReservesSpaceForItsLabel()
    {
        Assert.Equal("Task", SessionPresentation.CodexName("Task", new("branch")));
        Assert.Equal("Task | worktree: checkout-two", SessionPresentation.CodexName("Task", new("branch", "checkout-two")));
        var longName = SessionPresentation.CodexName(new string('n', 100), new("branch", "checkout-two"));
        Assert.Equal(100, longName.Length);
        Assert.EndsWith(" | worktree: checkout-two", longName);
        Assert.DoesNotContain('\u001b', SessionPresentation.CodexName("Task\u001b", new(null, "tree\n")));
    }

    [Fact]
    public void TitleKeepsTaskNameSeparateFromBranchAndWorktree()
    {
        Assert.Equal("Fix navigation | feature/focus | worktree: focus-tree", SessionPresentation.Title("Fix navigation", new("feature/focus", "focus-tree")));
        Assert.Equal("Task", SessionPresentation.Title("Task", new()));
        Assert.DoesNotContain('\u001b', SessionPresentation.Title("Task\u001b", new("branch\n", "tree\r")));
    }

    [Fact]
    public void LaunchPaletteIsConsumedOnceAndExistingConversationPaletteWinsOnResume()
    {
        using var workspace = new TestWorkspace();
        var shell = ProcessProbe.Identify(Environment.ProcessId)!;
        var launches = new LaunchPresentationStore(workspace.Paths);
        var launch = launches.Prepare("launch", shell);
        var palette = launches.Consume("launch", shell);
        Assert.Equal(launch.Palette, palette);
        Assert.Null(launches.Consume("launch", shell));
        var identities = new IdentityStore(workspace.Paths);
        var first = identities.Get("codex:new", workspace.Root, initialPalette: palette);
        Assert.Equal(launch.Palette, first.Palette);
        Assert.Equal(first.Palette, identities.Get(first.Key, workspace.Root, initialPalette: Palette.Create()).Palette);
        var stale = launches.Prepare("stale", shell with { StartedUtcTicks = 1 });
        Assert.Null(launches.Consume(stale.AttachmentId, shell));
    }
}

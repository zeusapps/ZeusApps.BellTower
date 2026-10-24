using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// Status line Git context when the tray snapshot is missing or stale (task 3.4, intake I-9).
public sealed class StatusLineFallbackTests
{
    private static string Render(TestWorkspace workspace, string provider, string id, string cwd)
    {
        var input = JsonSerializer.Serialize(new { session_id = id, cwd });
        return new StatusLineReader(workspace.Paths).Render(new StringReader(input), provider, color: false);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void MissingSnapshotStillShowsThisConversationsBranchAndWorktree(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        var text = Render(workspace, provider, "one", seven);
        Assert.Contains("branch: DEP-7070-login", text);
        Assert.Contains("worktree: DEP-7070", text);
        var main = Render(workspace, provider, "two", family.Main);
        Assert.Contains("branch: main", main);
        Assert.DoesNotContain("worktree:", main);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void StaleOrOutdatedSnapshotsAreRejectedAndAFreshOneIsUsed(string provider)
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        var key = provider + ":one";
        var identity = new IdentityStore(workspace.Paths).Get(key, seven);
        var presentations = new IdentityPresentationStore(workspace.Paths);
        // Older than the staleness window: ignored, the live checkout is read instead.
        presentations.Save(identity, new GitContext("stale-branch", "stale-tree"), DateTimeOffset.UtcNow - IdentityPresentationStore.Staleness - TimeSpan.FromSeconds(1));
        var stale = Render(workspace, provider, "one", seven);
        Assert.DoesNotContain("stale-branch", stale);
        Assert.Contains("branch: DEP-7070-login", stale);
        // A healthy tray's snapshot (published inside the window) is authoritative.
        presentations.Save(identity, new GitContext("tray-branch", "tray-tree"), DateTimeOffset.UtcNow);
        Assert.Contains("branch: tray-branch", Render(workspace, provider, "one", seven));
        // A snapshot for an outdated title is not trusted for this render either.
        new IdentityStore(workspace.Paths).Update(key, seven, value => { value.ManualName = "Renamed"; return true; });
        var renamed = Render(workspace, provider, "one", seven);
        Assert.Contains("Renamed", renamed);
        Assert.DoesNotContain("tray-branch", renamed);
        Assert.Contains("branch: DEP-7070-login", renamed);
    }

    [Fact]
    public void ConversationsNeverBorrowEachOthersSnapshotsOrCheckouts()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        var eight = family.Add("DEP-8080", "docs");
        var store = new IdentityStore(workspace.Paths);
        var claude = store.Get("claude:one", family.Main);
        var codex = store.Get("codex:one", family.Main);
        // Claude's tray snapshot is fresh; Codex has none. Codex resolves to its own pinned checkout.
        new IdentityPresentationStore(workspace.Paths).Save(claude, new GitContext("claude-snapshot", "claude-tree"), DateTimeOffset.UtcNow);
        store.Update("codex:one", family.Main, value => { value.CheckoutPin = eight; return true; });
        store.Update("claude:one", family.Main, value => { value.WorkingCheckout = seven; return true; });
        var claudeText = Render(workspace, "claude", "one", family.Main);
        var codexText = Render(workspace, "codex", "one", family.Main);
        Assert.Contains("branch: claude-snapshot", claudeText);
        Assert.DoesNotContain("claude-snapshot", codexText);
        Assert.Contains("branch: DEP-8080-docs", codexText);
        Assert.Contains("worktree: DEP-8080", codexText);
        Assert.DoesNotContain("DEP-8080", claudeText);
        // Without a snapshot Claude reads its own saved checkout, never Codex's pin.
        new IdentityStore(workspace.Paths).Update("claude:one", family.Main, value => { value.ManualName = "Renamed"; return true; });
        var own = Render(workspace, "claude", "one", family.Main);
        Assert.Contains("branch: DEP-7070-login", own);
        Assert.DoesNotContain("DEP-8080", own);
    }

    [Fact]
    public void TheFallbackReadsGitMetadataFilesWithoutStartingGit()
    {
        using var workspace = new TestWorkspace();
        // Not a valid repository for the git executable; only a process-free reader can understand it.
        var fake = Path.Combine(workspace.Root, "fake");
        Directory.CreateDirectory(Path.Combine(fake, ".git"));
        File.WriteAllText(Path.Combine(fake, ".git", "HEAD"), "ref: refs/heads/process-free\n");
        var nested = Directory.CreateDirectory(Path.Combine(fake, "src", "deep")).FullName;
        Assert.Equal("branch: process-free", Render(workspace, "claude", "one", nested).Split("  ", 2)[1]);
        Assert.Equal(new GitContext(), GitHeadReader.Read(Path.Combine(workspace.Root, "missing")));
        Assert.Equal(new GitContext(), GitHeadReader.Read(null));
    }

    [Fact]
    public void ProcessFreeReaderAgreesWithGitForBranchesWorktreesAndDetachedHeads()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        var detached = Path.Combine(family.Worktrees, "DEP-9090");
        GitHelper.Run(family.Main, "worktree", "add", "--detach", detached);
        var sub = Directory.CreateDirectory(Path.Combine(seven, "src")).FullName;
        var git = new GitReader();
        foreach (var directory in new[] { family.Main, seven, sub })
        { Assert.Equal(git.ReadUncached(directory), GitHeadReader.Read(directory)); }
        var expected = git.ReadUncached(detached);
        var actual = GitHeadReader.Read(detached);
        Assert.Equal(expected.Worktree, actual.Worktree);
        Assert.StartsWith(actual.Branch!, expected.Branch!, StringComparison.Ordinal);
    }

    [Fact]
    public void HeartbeatKeepsAHealthyTraysSnapshotsInsideTheStalenessWindow()
    {
        // Two full heartbeats fit in the window, so a single delayed write never makes a healthy tray look stale.
        Assert.True(IdentityPresentationStore.Staleness >= IdentityPresentationStore.Heartbeat * 2);
        using var workspace = new TestWorkspace();
        var store = new IdentityPresentationStore(workspace.Paths);
        var identity = new IdentityStore(workspace.Paths).Get("claude:one", workspace.Root);
        var git = new GitContext("main");
        var start = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        Assert.True(store.Save(identity, git, start));
        // The tray refreshes every ~2 s; identical content is not rewritten between heartbeats.
        for (var second = 2; second < 15; second += 2)
        {
            Assert.False(store.Save(identity, git, start.AddSeconds(second)));
            Assert.NotNull(store.Read(identity.Key, start.AddSeconds(second)));
        }
        Assert.True(store.Save(identity, git, start.AddSeconds(15)));
        for (var second = 17; second <= 44; second += 2)
        {
            store.Save(identity, git, start.AddSeconds(second));
            Assert.NotNull(store.Read(identity.Key, start.AddSeconds(second)));
        }
        // A stopped tray is noticed after the staleness window.
        Assert.Null(store.Read(identity.Key, start.AddSeconds(44) + IdentityPresentationStore.Staleness));
        // The snapshot only ever answers for its own key.
        Assert.Null(store.Read("codex:other", start.AddSeconds(20)));
    }
}

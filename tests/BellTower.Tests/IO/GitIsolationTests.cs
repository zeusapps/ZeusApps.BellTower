using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

// QA-1700: a test fixture must never act on a repository it did not create. The decoy below is a throwaway repository
// inside the disposable workspace; the real repository is never referenced.
public sealed class GitIsolationTests
{
    private static string Head(string directory) => GitHelper.Run(directory, "rev-parse", "HEAD").Trim();

    private static string Count(string directory) => GitHelper.Run(directory, "rev-list", "--count", "HEAD").Trim();

    private static string Outer(TestWorkspace workspace)
    {
        var outer = Path.Combine(workspace.Root, "outer");
        Directory.CreateDirectory(outer);
        GitHelper.Run(outer, "init", "--initial-branch", "main");
        GitHelper.Run(outer, "-c", "user.name=Decoy", "-c", "user.email=decoy@example.invalid", "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "decoy");
        return outer;
    }

    [Fact]
    public void AnInheritedRepositoryEnvironmentNeverRedirectsTheFixtureToAnotherRepository()
    {
        using var workspace = new TestWorkspace();
        var outer = Outer(workspace);
        var before = Head(outer);
        var ambient = new Dictionary<string, string>
        {
            ["GIT_DIR"] = Path.Combine(outer, ".git"),
            ["GIT_WORK_TREE"] = outer,
            ["GIT_INDEX_FILE"] = Path.Combine(outer, ".git", "index"),
            ["GIT_COMMON_DIR"] = Path.Combine(outer, ".git"),
            ["GIT_OBJECT_DIRECTORY"] = Path.Combine(outer, ".git", "objects"),
            ["GIT_NAMESPACE"] = "decoy"
        };

        // The family lives inside the decoy's work tree, the exact shape of the leak.
        var family = WorktreeFamily.Create(Path.Combine(outer, "nested"), ambient);

        Assert.Equal(before, Head(outer));
        Assert.Equal("1", Count(outer));
        Assert.True(Directory.Exists(Path.Combine(family.Main, ".git")));
        Assert.True(WorktreeFamily.Same(GitHelper.Run(family.Main, "rev-parse", "--show-toplevel").Trim(), family.Main));
        Assert.Equal("1", Count(family.Main));
        Assert.NotEqual(before, Head(family.Main));
    }

    [Fact]
    public void RepositoryDiscoveryStopsAtTheWorkspaceRootParent()
    {
        using var workspace = new TestWorkspace();
        var plain = Path.Combine(workspace.Root, "plain");
        Directory.CreateDirectory(plain);
        // Nothing inside the workspace is a repository, and discovery never climbs above its root into an enclosing one.
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => GitHelper.Run(plain, "rev-parse", "--show-toplevel"));
        Assert.Equal(Path.GetDirectoryName(workspace.Root)!.Replace(Path.DirectorySeparatorChar, '/'), GitHelper.Ceiling(plain));
    }
}

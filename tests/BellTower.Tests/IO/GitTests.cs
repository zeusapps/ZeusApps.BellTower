using System.Diagnostics;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class GitTests
{
    private static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }

    [Fact]
    public void BranchAndNonGitDirectoriesAreRecognized()
    {
        using var w = new TestWorkspace();
        var reader = new GitReader();
        var notRepository = Path.Combine(w.Root, "not-a-checkout");
        Directory.CreateDirectory(notRepository);
        File.WriteAllText(Path.Combine(notRepository, ".git"), "gitdir: missing");
        Assert.Null(reader.Read(notRepository).Branch);
        var repository = Path.Combine(w.Root, "repository");
        Directory.CreateDirectory(repository);
        Git(repository, "init", "--initial-branch", "feature/test");
        var child = Path.Combine(repository, "sub");
        Directory.CreateDirectory(child);
        Assert.Equal("feature/test", reader.Read(child).Branch);
    }

    [Fact]
    public void LinkedWorktreeIsDistinctFromBranchWithoutCommittingAnything()
    {
        using var w = new TestWorkspace();
        var repository = Path.Combine(w.Root, "repository");
        Directory.CreateDirectory(repository);
        Git(repository, "init", "--initial-branch", "main");
        var worktree = Path.Combine(w.Root, "checkout-two");
        Git(repository, "worktree", "add", "--orphan", "-b", "feature/two", worktree);
        var context = new GitReader().Read(worktree);
        Assert.Equal("feature/two", context.Branch);
        Assert.Equal("checkout-two", context.Worktree);
    }
}

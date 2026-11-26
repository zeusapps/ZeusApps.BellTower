using System.Text;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// Real Git in the sandbox, run through the already allowed Git Bash (which finds Git for Windows) so the guard's allow-list does not
/// grow. The child environment is the instance's own (sandbox <c>HOME</c>, no global Git configuration), and every call names its
/// repository with <c>-C</c> and an explicit identity, so it can neither read nor change anything outside the sandbox.
/// </summary>
public static class SandboxGit
{
    public static string Run(Instance instance, string directory, params string[] arguments)
    {
        var command = new StringBuilder("git -C ").Append(Quote(directory))
            .Append(" -c user.name=Fixture -c user.email=fixture@example.invalid -c commit.gpgsign=false -c core.autocrlf=false");
        foreach (var argument in arguments)
        {
            command.Append(' ').Append(Quote(argument));
        }
        var result = instance.Run(instance.Tools.GitBash, ["-c", command.ToString()], timeoutMilliseconds: 60000, record: false);
        if (!result.Succeeded)
        {
            throw new IOException("git " + arguments.FirstOrDefault() + " failed in the sandbox: " + result.Error.Trim());
        }
        return result.Output;
    }

    /// <summary>A repository with one commit on <c>main</c> (creation is refused when the directory is not its own top level).</summary>
    public static void InitRepository(Instance instance, string directory)
    {
        Directory.CreateDirectory(directory);
        Run(instance, directory, "init", "--initial-branch", "main");
        Run(instance, directory, "commit", "--allow-empty", "-m", "initial");
        var top = Run(instance, directory, "rev-parse", "--show-toplevel").Trim();
        if (!string.Equals(Path.GetFullPath(top), Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The sandbox repository resolved outside its directory.");
        }
    }

    /// <summary>A linked worktree on a new branch.</summary>
    public static string AddWorktree(Instance instance, string repository, string path, string branch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Run(instance, repository, "worktree", "add", "-b", branch, path);
        return path;
    }

    public static void SwitchNewBranch(Instance instance, string worktree, string branch) => Run(instance, worktree, "switch", "-c", branch);

    private static string Quote(string value) => "'" + value.Replace('\\', '/').Replace("'", "'\\''") + "'";
}

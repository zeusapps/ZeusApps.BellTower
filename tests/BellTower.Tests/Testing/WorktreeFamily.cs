using BellTower.Core;
using BellTower.IO;

namespace BellTower.Tests.Testing;

// A disposable real Git repository with linked worktrees laid out like the field setup: sessions launch in the main
// checkout (D360) while ticket work lives in D360-worktrees/DEP-XXXX. Everything is created inside the test workspace.
public sealed class WorktreeFamily
{
    public string Main { get; }
    public string Worktrees { get; }

    private WorktreeFamily(string root)
    {
        Main = Path.Combine(root, "D360");
        Worktrees = Path.Combine(root, "D360-worktrees");
    }

    public static WorktreeFamily Create(string root)
    {
        var family = new WorktreeFamily(root);
        Directory.CreateDirectory(family.Main);
        GitHelper.Run(family.Main, "init", "--initial-branch", "main");
        Commit(family.Main);
        return family;
    }

    public static void Commit(string directory) => GitHelper.Run(directory, "-c", "user.name=Test", "-c", "user.email=test@example.invalid",
        "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "initial");

    // Adds a linked worktree named after the ticket on branch "<ticket>-<suffix>" and optionally a change folder inside it.
    public string Add(string ticket, string suffix = "login", params string[] changes)
    {
        var path = Path.Combine(Worktrees, ticket);
        Directory.CreateDirectory(Worktrees);
        GitHelper.Run(Main, "worktree", "add", "-b", ticket + "-" + suffix, path);
        foreach (var change in changes)
        { IdentityFixtures.Change(path, change); }
        return path;
    }

    public string AddNamed(string folder, string branch, params string[] changes)
    {
        var path = Path.Combine(Worktrees, folder);
        Directory.CreateDirectory(Worktrees);
        GitHelper.Run(Main, "worktree", "add", "-b", branch, path);
        foreach (var change in changes)
        { IdentityFixtures.Change(path, change); }
        return path;
    }

    public static bool Same(string? left, string? right) => CheckoutPolicy.SamePath(left, right);

    public static SessionIdentity Identity(DataPaths paths, SessionState session, string launch) => new IdentityStore(paths).Get(session.Key, launch);
}

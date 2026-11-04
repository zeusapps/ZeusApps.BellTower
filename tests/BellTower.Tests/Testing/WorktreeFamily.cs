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

    public static WorktreeFamily Create(string root, IReadOnlyDictionary<string, string>? ambient = null)
    {
        var family = new WorktreeFamily(root);
        Directory.CreateDirectory(family.Main);
        GitHelper.RunWith(family.Main, ambient, "init", "--initial-branch", "main");
        Commit(family.Main, ambient);
        return family;
    }

    // QA-1700: refuses to commit unless the directory is its own repository, so a fixture can never write to an enclosing one.
    private static void Commit(string directory, IReadOnlyDictionary<string, string>? ambient)
    {
        if (!Directory.Exists(Path.Combine(directory, ".git")))
        { throw new InvalidOperationException("The fixture directory did not become its own repository."); }
        var toplevel = GitHelper.RunWith(directory, ambient, "rev-parse", "--show-toplevel").Trim();
        if (!CheckoutPolicy.SamePath(toplevel, directory))
        { throw new InvalidOperationException("The fixture repository resolved outside its directory."); }
        GitHelper.RunWith(directory, ambient, "-c", "user.name=Test", "-c", "user.email=test@example.invalid",
            "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "initial");
    }

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

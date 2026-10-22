namespace BellTower.Core;

public static class SessionPresentation
{
    public static string CodexName(string name, GitContext git)
    {
        var clean = IdentityText.Clean(name);
        var worktree = IdentityText.Optional(git.Worktree);
        if (worktree is null)
        {
            return clean;
        }
        var suffix = " | worktree: " + worktree[..Math.Min(worktree.Length, 40)];
        return clean[..Math.Min(clean.Length, 100 - suffix.Length)].TrimEnd() + suffix;
    }

    public static string Title(string name, GitContext git)
    {
        var parts = new[] { IdentityText.Clean(name), IdentityText.Optional(git.Branch),
            git.Worktree is null ? null : "worktree: " + IdentityText.Clean(git.Worktree) };
        return string.Join(" | ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }
}

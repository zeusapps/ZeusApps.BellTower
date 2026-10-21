namespace BellTower.Core;

public static class SessionPresentation
{
    public static string Title(string name, GitContext git)
    {
        var parts = new[] { IdentityText.Clean(name), IdentityText.Optional(git.Branch),
            git.Worktree is null ? null : "worktree: " + IdentityText.Clean(git.Worktree) };
        return string.Join(" | ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }
}

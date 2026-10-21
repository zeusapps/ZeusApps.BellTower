namespace BellTower.Core;

public sealed record GitContext(string? Branch = null, string? Worktree = null)
{
    public override string ToString() => string.Join("  ", new[]
    {
        Branch is null ? null : "branch: " + Branch,
        Worktree is null ? null : "worktree: " + Worktree
    }.OfType<string>());
}

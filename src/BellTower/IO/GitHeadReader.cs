using BellTower.Core;

namespace BellTower.IO;

// Process-free read of a checkout's branch and linked-worktree name straight from its Git metadata files.
// It backs the status line when no fresh tray snapshot exists: it launches nothing, reads at most one small
// HEAD file, and only ever describes the directory it is given (never another conversation's checkout).
public static class GitHeadReader
{
    private const int MaxDepth = 40;

    public static GitContext Read(string? directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            { return new(); }
            var current = new DirectoryInfo(Path.GetFullPath(directory));
            for (var depth = 0; current is not null && depth < MaxDepth; depth++, current = current.Parent)
            {
                var marker = Path.Combine(current.FullName, ".git");
                if (Directory.Exists(marker))
                { return Head(marker, linked: false, current.FullName); }
                if (File.Exists(marker) && GitDirectory(marker, current.FullName) is { } gitDir)
                {
                    // A linked worktree's git directory is <common>/worktrees/<name>; submodules are not worktrees.
                    var linked = string.Equals(Path.GetFileName(Path.GetDirectoryName(gitDir)), "worktrees", StringComparison.OrdinalIgnoreCase);
                    return Head(gitDir, linked, current.FullName);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        return new();
    }

    private static string? GitDirectory(string file, string root)
    {
        var text = ReadSmall(file);
        if (text is null || !text.StartsWith("gitdir:", StringComparison.Ordinal))
        { return null; }
        var target = text[7..].Trim();
        var full = Path.IsPathRooted(target) ? target : Path.GetFullPath(Path.Combine(root, target));
        return Directory.Exists(full) ? Path.GetFullPath(full) : null;
    }

    private static GitContext Head(string gitDir, bool linked, string root)
    {
        var head = ReadSmall(Path.Combine(gitDir, "HEAD"))?.Trim();
        if (string.IsNullOrEmpty(head))
        { return new(null, linked ? Path.GetFileName(root) : null); }
        const string prefix = "ref: refs/heads/";
        var branch = head.StartsWith(prefix, StringComparison.Ordinal) ? head[prefix.Length..] :
            head.StartsWith("ref:", StringComparison.Ordinal) ? null : head[..Math.Min(7, head.Length)];
        return new(branch, linked ? Path.GetFileName(root) : null);
    }

    private static string? ReadSmall(string path)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length <= 4096 ? File.ReadAllText(path) : null;
    }
}

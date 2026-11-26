namespace BellTower.FieldAcceptance.Isolation;

/// <summary>Case-insensitive, link-resolving path comparison used by every isolation decision.</summary>
public static class PathRelation
{
    /// <summary>The full path without a trailing separator; the deepest existing ancestor is resolved through junctions and symbolic links.</summary>
    public static string Canonical(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var tail = new Stack<string>();
        var current = full;
        while (current is not null && !Exists(current))
        {
            tail.Push(Path.GetFileName(current));
            current = Path.GetDirectoryName(current);
        }
        if (current is null)
        {
            return full;
        }
        try
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.ResolveLinkTarget(true) is { } target)
            {
                current = Path.TrimEndingDirectorySeparator(target.FullName);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable link keeps its literal path; the guard then compares conservatively on the literal form.
        }
        foreach (var part in tail)
        {
            current = Path.Combine(current, part);
        }
        return current;
    }

    public static bool IsUnderOrEqual(string path, string root)
    {
        var child = Canonical(path);
        var parent = Canonical(root);
        return child.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
            child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when one path equals or contains the other: a sandbox may be neither inside production nor around it.</summary>
    public static bool Overlaps(string left, string right) => IsUnderOrEqual(left, right) || IsUnderOrEqual(right, left);

    /// <summary>True when free text (a command line, an environment value) mentions the path in either slash form.</summary>
    public static bool Mentions(string text, string path)
    {
        var literal = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return text.Contains(literal, StringComparison.OrdinalIgnoreCase) ||
            text.Contains(literal.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}

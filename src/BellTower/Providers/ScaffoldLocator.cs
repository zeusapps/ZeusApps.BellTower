namespace BellTower.Providers;

// Independent root evidence for a created change: the nearest openspec folder must contain the change scaffold.
public static class ScaffoldLocator
{
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(10);

    // When fresh is supplied (output was redirected, so no success marker exists) the scaffold must have
    // been created close to the observed event, which ties it to that command.
    public static string? Root(string? directory, string change, DateTimeOffset? fresh = null)
    {
        if (directory is null || !Path.IsPathFullyQualified(directory))
        {
            return null;
        }
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if (!Directory.Exists(Path.Combine(current.FullName, "openspec")))
            {
                continue;
            }
            var marker = Path.Combine(current.FullName, "openspec", "changes", change, ".openspec.yaml");
            if (!File.Exists(marker))
            {
                return null;
            }
            return fresh is { } at && (File.GetCreationTimeUtc(marker) - at).Duration() > Freshness ? null : current.FullName;
        }
        return null;
    }

    // Nearest ancestor of directory containing an existing, non-archive change folder.
    public static string? ChangeRoot(string? directory, string change)
    {
        if (directory is null || !Path.IsPathFullyQualified(directory))
        {
            return null;
        }
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "openspec", "changes", change)))
            {
                return current.FullName;
            }
        }
        return null;
    }
}

namespace BellTower.IO;

public sealed class DataPaths
{
    public string Root { get; }
    public string Inbox => Path.Combine(Root, "inbox");
    public string Attachments => Path.Combine(Root, "attachments");
    public string Identities => Path.Combine(Root, "identities");
    public string Hosts => Path.Combine(Root, "hosts");
    public string Requests => Path.Combine(Root, "focus");
    public string State => Path.Combine(Root, "state.json");
    public string Config => Path.Combine(Root, "config.json");
    public string Position => Path.Combine(Root, "position.json");

    public DataPaths(string? root = null)
    {
        var configured = root ?? Environment.GetEnvironmentVariable("BELLTOWER_DATA_DIR");
        var defaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZeusApps.BellTower");
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured ?? defaultDirectory));
    }
    public void Ensure()
    {
        foreach (var path in new[]
        {
            Root,
            Inbox,
            Attachments,
            Identities,
            Hosts,
            Requests
        })
        {
            Directory.CreateDirectory(path);
        }
    }

    public string LockName(string area) => "Local\\BellTower-" + ContentHash.Compute(Root.ToUpperInvariant() + area)[..24];
}

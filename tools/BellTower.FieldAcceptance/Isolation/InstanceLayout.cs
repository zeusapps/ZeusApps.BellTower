namespace BellTower.FieldAcceptance.Isolation;

/// <summary>
/// The disposable directories of one harness installation: its versioned install, data directory, fake user home (provider
/// settings, VS Code user data), temp and fake provider binaries. Every path lives under <see cref="Root"/>.
/// </summary>
public sealed record InstanceLayout(string Name, string Root)
{
    public string Install => Path.Combine(Root, "install");
    public string Data => Path.Combine(Root, "data");
    public string Home => Path.Combine(Root, "home");
    public string Temp => Path.Combine(Root, "temp");
    public string FakeBin => Path.Combine(Root, "fake-bin");
    public string ClaudeDirectory => Path.Combine(Home, ".claude");
    public string CodexDirectory => Path.Combine(Home, ".codex");
    public string RoamingAppData => Path.Combine(Home, "AppData", "Roaming");
    public string LocalAppData => Path.Combine(Home, "AppData", "Local");
    public string Launcher => Path.Combine(Install, "bell-tower.exe");
    public string ClaudeSettings => Path.Combine(ClaudeDirectory, "settings.json");
    public string CodexHooks => Path.Combine(CodexDirectory, "hooks.json");

    /// <summary>Every directory the harness may write for this instance, with the label used in guard messages.</summary>
    public IEnumerable<KeyValuePair<string, string>> Paths() =>
    [
        new("root", Root), new("install", Install), new("data", Data), new("home", Home),
        new("temp", Temp), new("fake-bin", FakeBin)
    ];

    public void Create()
    {
        foreach (var path in new[] { Data, Home, Temp, FakeBin, ClaudeDirectory, CodexDirectory, RoamingAppData, LocalAppData })
        {
            Directory.CreateDirectory(path);
        }
    }
}

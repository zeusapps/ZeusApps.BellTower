namespace BellTower.FieldAcceptance.Isolation;

/// <summary>
/// The real user's Bell Tower and provider locations the harness must never touch. <see cref="Roots"/> are refused as sandbox
/// locations, process targets and environment values; <see cref="WatchedFiles"/> and the installation tree are compared before and
/// after a run (the user's live tray and agents legitimately write elsewhere in these roots, so only setup-owned files gate).
/// <see cref="Homes"/> are the user's own profile and application-data folders: they are not roots (a repository may live inside
/// them) but no command argument or environment value may be one of them exactly.
/// </summary>
public sealed record ProductionLocations(
    string InstallationRoot, string DataRoot, IReadOnlyList<string> Roots, IReadOnlyList<string> WatchedFiles, IReadOnlyList<string> Homes)
{
    public static ProductionLocations For(string profile, string localAppData, string roamingAppData, IEnumerable<string?>? configuredRoots = null)
    {
        var data = Path.Combine(localAppData, "ZeusApps.BellTower");
        var installation = Path.Combine(localAppData, "Programs", "ZeusApps.BellTower");
        var claude = Path.Combine(profile, ".claude");
        var codex = Path.Combine(profile, ".codex");
        var vscode = Path.Combine(profile, ".vscode");
        var code = Path.Combine(roamingAppData, "Code");
        var roots = new List<string> { data, installation, claude, codex, vscode, code };
        roots.AddRange((configuredRoots ?? []).Where(root => !string.IsNullOrWhiteSpace(root)).Select(root => root!));
        var watched = new List<string>
        {
            Path.Combine(claude, "settings.json"), Path.Combine(claude, "settings.local.json"),
            Path.Combine(codex, "hooks.json"), Path.Combine(codex, "config.toml"),
            Path.Combine(code, "User", "settings.json"), Path.Combine(vscode, "extensions", "extensions.json"),
            Path.Combine(data, "integration.json"), Path.Combine(data, "capture-protocol.json"),
            Path.Combine(data, "hook-start", "launcher.txt"), Path.Combine(data, "hook-start", "task.txt")
        };
        return new(installation, data, roots, watched, [profile, localAppData, roamingAppData]);
    }

    /// <summary>The locations of the user running the harness, including data and provider roots named by the harness's own environment.</summary>
    public static ProductionLocations Current() => For(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        [Environment.GetEnvironmentVariable("BELLTOWER_DATA_DIR"), Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"), Environment.GetEnvironmentVariable("CODEX_HOME")]);
}

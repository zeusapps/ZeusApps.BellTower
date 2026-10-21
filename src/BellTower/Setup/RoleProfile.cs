namespace BellTower.Setup;

public sealed record RoleProfile(string Provider, string Role, string Label, string Function, string Icon, string Color, string? Directory = null)
{
    public string ShellCommand => Function + (Directory is null ? "" : " -Directory " + PowerShellModule.Literal(Directory));

    public static RoleProfile ForProject(string provider, string directory)
    {
        var profile = For(provider)[0];
        return profile with { Label = profile.Label + " (" + new DirectoryInfo(directory).Name + ")", Directory = directory };
    }

    public static IReadOnlyList<RoleProfile> For(string provider)
    {
        var agent = provider == "claude" ? "Claude" : "Codex";
        var function = provider == "claude" ? "Start-ClaudeCode" : "Start-Codex";
        return [new(provider, "", agent, function, "terminal", "terminal.ansiCyan")];
    }

    // Existing shell scripts can keep calling these; new terminal menus use only For().
    public static IReadOnlyList<RoleProfile> LegacyFor(string provider)
    {
        var agent = provider == "claude" ? "Claude" : "Codex";
        var function = provider == "claude" ? "Start-ClaudeCode" : "Start-Codex";
        return [
            new(provider, "PM", agent + " PM", function + "PM", "organization", "terminal.ansiCyan"),
            new(provider, "DEV", agent + " Developer", function + "Dev", "wand", "terminal.ansiGreen"),
            new(provider, "QA", agent + " QA", function + "QA", "search", "terminal.ansiMagenta"),
            new(provider, "APM", agent + " APM", function + "APM", "organization", "terminal.ansiYellow")
        ];
    }
}

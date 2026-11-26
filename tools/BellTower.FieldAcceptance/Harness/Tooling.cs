namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The few system executables a sandboxed run may use outside its own directories, resolved once to absolute paths.
/// <see cref="Pwsh"/> is PowerShell 7 at its fixed installation path (null when it is not installed).
/// </summary>
public sealed record SystemTools(string GitBash, string WindowsPowerShell, string Schtasks, string Cmd, string? Pwsh = null)
{
    public IReadOnlyCollection<string> All => Pwsh is null ? [GitBash, WindowsPowerShell, Schtasks, Cmd] : [GitBash, WindowsPowerShell, Schtasks, Cmd, Pwsh];

    public static SystemTools Resolve()
    {
        var system = Environment.SystemDirectory;
        var powershell = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
        var schtasks = Path.Combine(system, "schtasks.exe");
        var cmd = Path.Combine(system, "cmd.exe");
        var pwsh = CodexShell.FixedPwsh(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        return new(ResolveGitBash(), powershell, schtasks, cmd, File.Exists(pwsh) ? pwsh : null);
    }

    // The same order the product probe uses for Claude's Windows hook runner: the documented override, then Git for Windows, then PATH.
    private static string ResolveGitBash()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CODE_GIT_BASH_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }
        var standard = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe");
        if (File.Exists(standard))
        {
            return standard;
        }
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(entry, "bash.exe");
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }
        throw new FileNotFoundException("Git Bash (bash.exe) is required: Claude Code runs its hooks through it on Windows.");
    }
}

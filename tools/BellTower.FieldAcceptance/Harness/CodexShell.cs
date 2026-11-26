namespace BellTower.FieldAcceptance.Harness;

/// <summary>The shell a provider runs a hook command through.</summary>
public enum HookShell
{
    /// <summary>Git Bash, which Claude Code uses on Windows.</summary>
    GitBash,
    /// <summary>PowerShell (7 or Windows PowerShell), the shell Codex 0.164.0 selects first on Windows (archived native evidence).</summary>
    PowerShell,
    /// <summary>The Windows command interpreter, Codex's documented last fallback.</summary>
    Cmd
}

/// <summary>
/// A shell kind together with the executable that ran it, so each capture records exactly which shell it used.
/// <paramref name="Note"/> is set when the guard skipped a PowerShell Codex would have found, naming the edition Codex would have used.
/// </summary>
public sealed record ResolvedShell(HookShell Kind, string Executable, string? Note = null)
{
    public string Name => Path.GetFileName(Executable);
}

/// <summary>
/// The Windows hook shell discovery of Codex 0.164.0 (<c>shell_detect.rs</c> at <c>rust-v0.164.0</c>), in this order:
/// <c>pwsh.exe</c> anywhere on <c>PATH</c>; PowerShell 7 at its fixed path; <c>powershell.exe</c> anywhere on <c>PATH</c>; Windows PowerShell at its
/// fixed path; <c>cmd.exe</c>. One deliberate difference, the harness guard: a PowerShell found on <c>PATH</c> counts only when it is one
/// of the two system PowerShells the harness may run (the guard allows nothing else). A hit the guard skips is reported in
/// <see cref="ResolvedShell.Note"/> with the edition Codex would have used.
/// </summary>
public static class CodexShell
{
    public static string FixedPwsh(string programFiles) => Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");

    public static ResolvedShell Resolve(string? path, string programFiles, string systemDirectory, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        var pwsh = FixedPwsh(programFiles);
        var windowsPowerShell = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        string? skipped = null;

        bool Allowed(string candidate) => candidate.Equals(pwsh, StringComparison.OrdinalIgnoreCase) || candidate.Equals(windowsPowerShell, StringComparison.OrdinalIgnoreCase);

        IEnumerable<string> PathHits(string name)
        {
            foreach (var entry in (path ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate;
                try
                {
                    candidate = Path.GetFullPath(Path.Combine(entry.Trim().Trim('"'), name));
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (exists(candidate))
                {
                    yield return candidate;
                }
            }
        }

        ResolvedShell PowerShell(string executable) => new(HookShell.PowerShell, executable, Note(skipped));

        // 1. pwsh.exe anywhere on PATH (the first hit the guard allows).
        foreach (var hit in PathHits("pwsh.exe"))
        {
            if (Allowed(hit))
            {
                return PowerShell(hit);
            }
            skipped ??= "PowerShell 7 (pwsh.exe)";
        }
        // 2. PowerShell 7 at its fixed path.
        if (exists(pwsh))
        {
            return PowerShell(pwsh);
        }
        // 3. powershell.exe anywhere on PATH.
        foreach (var hit in PathHits("powershell.exe"))
        {
            if (Allowed(hit))
            {
                return PowerShell(hit);
            }
            skipped ??= "Windows PowerShell (powershell.exe)";
        }
        // 4. Windows PowerShell at its fixed path.
        if (exists(windowsPowerShell))
        {
            return PowerShell(windowsPowerShell);
        }
        // 5. The command shell.
        return new(HookShell.Cmd, Path.Combine(systemDirectory, "cmd.exe"), Note(skipped));
    }

    private static string? Note(string? skipped) => skipped is null ? null :
        "A PowerShell on PATH outside the two system locations the guard allows was skipped; Codex would have used " + skipped + " there.";
}

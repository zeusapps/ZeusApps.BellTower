using System.Text;
using BellTower.IO;

namespace BellTower.Setup;

public enum CodexHookShell
{
    PowerShell,
    Cmd,
    Bash
}

public sealed class HookCommandBuilder(string executable, DataPaths paths, string? ingress = null, CodexHookShell? codexShell = null)
{
    public bool Fast => ingress is not null;

    public string Build(string verb, string provider)
    {
        if (verb == "event" && ingress is not null)
        {
            return provider == "claude"
                ? BashLiteral(ingress.Replace('\\', '/')) + " --provider claude --data-dir " + BashLiteral(paths.Root.Replace('\\', '/'))
                : OperatingSystem.IsWindows()
                    ? BuildWindowsCodex(ingress, codexShell ?? CodexHookShell.PowerShell, paths.Root)
                    : BashLiteral(ingress) + " --provider codex --data-dir " + BashLiteral(paths.Root);
        }
        var script = "$OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "[Console]::InputEncoding = $OutputEncoding; $inputText = [Console]::In.ReadToEnd(); $inputText | & " +
            Literal(executable) + " " + verb + " --provider " + provider + " --data-dir " + Literal(paths.Root);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return "powershell.exe -NoProfile -NonInteractive -EncodedCommand " + encoded;
    }

    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    private static string BashLiteral(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
    private static string PowerShellLiteral(string value) => "'" + value.Replace("'", "''") + "'";

    private static string BuildWindowsCodex(string ingress, CodexHookShell shell, string dataRoot) => shell switch
    {
        CodexHookShell.PowerShell => "& " + PowerShellLiteral(ingress) + " --provider codex --data-dir " + PowerShellLiteral(dataRoot),
        CodexHookShell.Cmd => CmdLiteral(ingress) + " --provider codex --data-dir " + CmdLiteral(dataRoot),
        CodexHookShell.Bash => BashLiteral(ingress.Replace('\\', '/')) + " --provider codex --data-dir " + BashLiteral(dataRoot.Replace('\\', '/')),
        _ => throw new ArgumentOutOfRangeException(nameof(shell))
    };

    private static string CmdLiteral(string value) => "\"" + value.Replace("%", "\"^%\"", StringComparison.Ordinal) + "\"";
}

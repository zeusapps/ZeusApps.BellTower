using System.Text;
using BellTower.IO;

namespace BellTower.Setup;

public sealed class HookCommandBuilder(string executable, DataPaths paths, string? ingress = null)
{
    public bool Fast => ingress is not null;

    public string Build(string verb, string provider)
    {
        if (verb == "event" && ingress is not null)
        {
            return provider == "claude"
                ? BashLiteral(ingress.Replace('\\', '/')) + " --provider claude --data-dir " + BashLiteral(paths.Root.Replace('\\', '/'))
                : CmdLiteral(ingress) + " --provider codex --data-dir " + CmdLiteral(paths.Root);
        }
        var script = "$OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "[Console]::InputEncoding = $OutputEncoding; $inputText = [Console]::In.ReadToEnd(); $inputText | & " +
            Literal(executable) + " " + verb + " --provider " + provider + " --data-dir " + Literal(paths.Root);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return "powershell.exe -NoProfile -NonInteractive -EncodedCommand " + encoded;
    }

    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    private static string BashLiteral(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
    private static string CmdLiteral(string value)
    {
        if (value.Any(c => c is '"' or '%' or '\r' or '\n'))
        {
            throw new ArgumentException("The Codex Windows hook path contains unsupported command-shell expansion characters.");
        }
        var trailing = value.Length - value.TrimEnd('\\').Length;
        return "\"" + value + new string('\\', trailing) + "\"";
    }
}

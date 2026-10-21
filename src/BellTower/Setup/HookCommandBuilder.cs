using System.Text;
using BellTower.IO;

namespace BellTower.Setup;

public sealed class HookCommandBuilder(string executable, DataPaths paths)
{
    public string Build(string verb, string provider)
    {
        var script = "$OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "[Console]::InputEncoding = $OutputEncoding; $inputText = [Console]::In.ReadToEnd(); $inputText | & " +
            Literal(executable) + " " + verb + " --provider " + provider + " --data-dir " + Literal(paths.Root);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return "powershell.exe -NoProfile -NonInteractive -EncodedCommand " + encoded;
    }

    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
}

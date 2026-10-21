using System.Text.RegularExpressions;

namespace BellTower.Setup;

public sealed record VsCodeInstallation(string Executable, string CliScript)
{
    public static VsCodeInstallation? Find() => Find([
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code")
    ]);

    public static VsCodeInstallation? Find(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            var executable = Path.Combine(root, "Code.exe");
            if (!File.Exists(executable))
            { continue; }
            var script = Path.Combine(root, "resources", "app", "out", "cli.js");
            if (File.Exists(script))
            { return new(executable, script); }

            // Newer Windows distributions keep application resources in the active commit directory.
            var wrapper = Path.Combine(root, "bin", "code.cmd");
            if (!File.Exists(wrapper) || new FileInfo(wrapper).Length > 65536)
            { continue; }
            var match = Regex.Match(File.ReadAllText(wrapper), "\"%~dp0(?<relative>[^\"]+cli\\.js)\"", RegexOptions.IgnoreCase);
            if (!match.Success)
            { continue; }
            script = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(wrapper)!, match.Groups["relative"].Value));
            var absoluteRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
            if (script.StartsWith(absoluteRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(script))
            {
                return new(executable, script);
            }
        }
        return null;
    }
}

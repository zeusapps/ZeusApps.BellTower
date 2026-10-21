using System.Text.Json.Nodes;

namespace BellTower.Setup;

public static class VsCodeBridgeSetup
{
    public static void Plan(string executable, string? fixtureHome, DesktopOwnership ownership, bool remove,
        List<SetupAction> actions, List<string> notes)
    {
        var installation = VsCodeInstallation.Find();
        var packageDirectory = Path.GetDirectoryName(executable)!;
        var vsix = Directory.Exists(packageDirectory)
            ? Directory.EnumerateFiles(packageDirectory, "bell-tower-focus-*.vsix").Order(StringComparer.Ordinal).LastOrDefault() : null;
        var extensions = Path.Combine(fixtureHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode", "extensions");
        var registry = Path.Combine(extensions, "extensions.json");
        var present = File.Exists(registry) && JsonNode.Parse(File.ReadAllText(registry))!.AsArray()
            .Any(extension => string.Equals(extension?["identifier"]?["id"]?.GetValue<string>(), "zeusapps.bell-tower", StringComparison.OrdinalIgnoreCase));
        if (remove && (!ownership.BridgeInstalled || !present))
        {
            if (!present)
            {
                ownership.BridgeInstalled = false;
            }
            return;
        }
        if (installation is null || (!remove && vsix is null))
        {
            notes.Add("VS Code focus bridge was not scheduled: use the packaged VSIX with a standard VS Code installation.");
            return;
        }
        if (!remove && present && !ownership.BridgeInstalled)
        {
            notes.Add("Kept the existing VS Code Bell Tower extension; its installation is not owned by this setup.");
            return;
        }
        var arguments = new List<string> { installation.CliScript };
        arguments.AddRange(remove ? ["--uninstall-extension", "zeusapps.bell-tower"] : ["--install-extension", vsix!, "--force"]);
        if (fixtureHome is not null)
        {
            arguments.AddRange(["--user-data-dir", Path.Combine(fixtureHome, "AppData", "Roaming", "Code"), "--extensions-dir", extensions]);
        }
        actions.Add(new(remove ? "Remove the owned VS Code focus bridge" : "Install the bundled VS Code focus bridge",
            installation.Executable, arguments));
        ownership.BridgeInstalled = !remove;
    }
}

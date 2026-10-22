using BellTower.IO;

namespace BellTower.Setup;

public sealed class IntegrationRefresh(DataPaths paths, string executable, string? userHome = null)
{
    public string Apply()
    {
        var manifest = Path.Combine(paths.Root, "integration.json");
        if (!File.Exists(manifest))
        { return "No owned integration refresh required."; }
        using var gate = new DataLock(paths, "setup");
        var before = File.ReadAllText(manifest);
        var ownership = Json.Decode<IntegrationOwnership>(before);
        var changes = new List<FileChange>();
        var actions = new List<SetupAction>();
        var notes = new List<string>();
        var module = Path.Combine(paths.Root, "shell", "BellTower.psm1");
        if (ownership.Desktop.Providers.Count > 0 && ownership.Desktop.Files.TryGetValue(module, out var owned))
        {
            var current = File.Exists(module) ? File.ReadAllText(module).TrimStart('\uFEFF') : null;
            if (current != owned.After)
            { throw new IOException("Owned shell module has user edits; refresh it explicitly."); }
            var after = PowerShellModule.Render(executable, paths.Root, ownership.Desktop.Providers);
            changes.Add(new(module, current, after, Utf8Bom: true));
            ownership.Desktop.Files[module] = new(owned.Before, after);
            notes.Add("Open a new shell when ready to load refreshed functions.");
        }
        if (ownership.Desktop.BridgeInstalled)
        {
            VsCodeBridgeSetup.Plan(executable, userHome, ownership.Desktop, false, actions, notes);
            notes.Add("Reload VS Code when ready to activate the refreshed bridge.");
        }
        // Commit changed ownership only after extension installation succeeds.
        var commit = new FileChange(manifest, before, Json.Encode(ownership));
        changes.Add(commit);
        new SetupPlan(changes, actions, notes, commit).Apply(paths);
        return string.Join(" ", notes);
    }
}

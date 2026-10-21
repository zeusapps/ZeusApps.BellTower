using BellTower.IO;

namespace BellTower.Setup;

public sealed class DesktopSetup(DataPaths paths, string executable, string? userHome)
{
    public void Plan(IReadOnlyList<string> selected, bool remove, DesktopOwnership ownership, List<FileChange> changes,
        List<SetupAction> actions, List<string> notes, string? project = null, bool removeProject = false)
    {
        if (removeProject)
        {
            selected = selected.Where(ownership.Providers.Contains).ToArray();
        }
        if (project is not null)
        {
            if (!ownership.Projects.TryGetValue(project, out var projectProviders))
            {
                projectProviders = [];
                ownership.Projects[project] = projectProviders;
            }
            if (removeProject)
            {
                projectProviders.ExceptWith(selected);
            }
            else
            {
                projectProviders.UnionWith(selected);
            }
        }
        if (remove)
        {
            foreach (var providers in ownership.Projects.Values)
            {
                providers.ExceptWith(selected);
            }
        }
        foreach (var empty in ownership.Projects.Where(pair => pair.Value.Count == 0).Select(pair => pair.Key).ToArray())
        {
            ownership.Projects.Remove(empty);
        }
        foreach (var provider in selected)
        {
            if (remove)
            {
                ownership.Providers.Remove(provider);
            }
            else
            {
                if (!removeProject)
                {
                    ownership.Providers.Add(provider);
                }
            }
        }
        var home = userHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roaming = userHome is null ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : Path.Combine(home, "AppData", "Roaming");
        var local = userHome is null ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : Path.Combine(home, "AppData", "Local");
        var documents = userHome is null ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.Combine(home, "Documents");
        var profiles = new TerminalProfilesEditor(ownership, notes);
        var codeSettings = Path.Combine(roaming, "Code", "User", "settings.json");
        if (File.Exists(codeSettings))
        {
            profiles.VsCode(codeSettings, selected, remove, executable, paths.Root, changes);
            VsCodeBridgeSetup.Plan(executable, userHome, ownership, remove && ownership.Providers.Count == 0, actions, notes);
        }
        foreach (var terminal in new[] { "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe" })
        {
            var terminalSettings = Path.Combine(local, "Packages", terminal, "LocalState", "settings.json");
            if (File.Exists(terminalSettings))
            {
                profiles.WindowsTerminal(terminalSettings, selected, remove, changes, home);
            }
        }
        var unpackaged = Path.Combine(local, "Microsoft", "Windows Terminal", "settings.json");
        if (File.Exists(unpackaged))
        {
            profiles.WindowsTerminal(unpackaged, selected, remove, changes, home);
        }

        // Keep helpers needed by a profile whose user edits deliberately prevented its removal.
        ownership.Providers.UnionWith(ownership.Profiles.Select(profile => profile.Provider));
        var module = Path.Combine(paths.Root, "shell", "BellTower.psm1");
        var moduleBefore = File.Exists(module) ? File.ReadAllText(module) : null;
        var previousModule = ownership.Files.GetValueOrDefault(module);
        if (moduleBefore is not null && (previousModule is null || moduleBefore != previousModule.After))
        {
            throw new InvalidOperationException("The installed Bell Tower PowerShell module was changed. Preserve your edits before rerunning setup.");
        }
        var moduleAfter = ownership.Providers.Count == 0 ? previousModule?.Before : PowerShellModule.Render(executable, paths.Root, ownership.Providers);
        if (moduleAfter != moduleBefore)
        {
            changes.Add(new(module, moduleBefore, moduleAfter, Utf8Bom: true));
        }
        if (ownership.Providers.Count == 0)
        {
            ownership.Files.Remove(module);
        }
        else
        {
            ownership.Files[module] = new(previousModule is null ? moduleBefore : previousModule.Before, moduleAfter!);
        }

        foreach (var shell in new[] { "PowerShell", "WindowsPowerShell" })
        {
            var profile = Path.Combine(documents, shell, "profile.ps1");
            var before = File.Exists(profile) ? File.ReadAllText(profile) : null;
            var previous = ownership.Imports.GetValueOrDefault(profile);
            var block = ownership.Providers.Count == 0 ? null : ShellProfileEditor.Block(module);
            var after = ShellProfileEditor.Update(before ?? "", previous, block);
            if (before != after && (before is not null || block is not null))
            {
                changes.Add(new(profile, before, after, Utf8Bom: true));
            }
            if (block is null)
            {
                ownership.Imports.Remove(profile);
            }
            else
            {
                ownership.Imports[profile] = block;
            }
        }
        notes.Add("New terminal tabs load Start-ClaudeCode and Start-Codex. Agents are invoked directly by PowerShell; bell-tower launch is optional.");
    }
}

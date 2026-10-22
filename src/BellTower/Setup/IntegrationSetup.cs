using BellTower.IO;

namespace BellTower.Setup;

public sealed class IntegrationSetup(DataPaths paths, string executable, string? userHome = null)
{
    private string ManifestPath => Path.Combine(paths.Root, "integration.json");

    public SetupPlan Plan(string clients, bool remove = false, bool statusLine = false, bool desktop = false, string? project = null)
    {
        if (project is not null)
        {
            project = Path.TrimEndingDirectorySeparator(Path.GetFullPath(project));
            if (!remove && !Directory.Exists(project))
            {
                throw new ArgumentException("The project directory does not exist.");
            }
            desktop = true;
        }
        var removeProject = remove && project is not null;
        string[] selected = clients switch
        {
            "both" => ["claude", "codex"],
            "claude" => ["claude"],
            "codex" => ["codex"],
            _ => throw new ArgumentException("Clients must be both, claude, or codex.")
        };
        var previous = AtomicFile.Read<IntegrationOwnership>(ManifestPath) ?? new([], []);
        project = project is null ? null : previous.Desktop.Projects.Keys.FirstOrDefault(path => path.Equals(project, StringComparison.OrdinalIgnoreCase)) ?? project;
        if (removeProject && !previous.Desktop.Projects.ContainsKey(project!))
        {
            throw new ArgumentException("This project has no registered Bell Tower profiles.");
        }
        var ownership = new IntegrationOwnership(new(previous.Commands), new(previous.StatusLines))
        {
            CodexFooters = new(previous.CodexFooters)
        };
        var editor = new ProviderSettingsEditor(new HookCommandBuilder(executable, paths));
        var changes = new List<FileChange>();
        var actions = new List<SetupAction>();
        var notes = new List<string>();
        foreach (var provider in selected)
        {
            if (removeProject)
            {
                continue;
            }
            var path = SettingsPath(provider);
            var before = File.Exists(path) ? File.ReadAllText(path) : null;
            var after = editor.Edit(provider, path, before, previous, ownership, remove && !removeProject, (statusLine || desktop) && !removeProject);
            if (before != after && (!remove || before is not null))
            {
                changes.Add(new(path, before, after));
            }
            if (provider == "codex" && (statusLine || desktop || remove))
            {
                var configPath = Path.Combine(Path.GetDirectoryName(path)!, "config.toml");
                var configBefore = File.Exists(configPath) ? File.ReadAllText(configPath) : null;
                var configAfter = CodexFooterSettings.Edit(configPath, configBefore, previous.CodexFooters,
                    ownership.CodexFooters, remove, notes);
                if (configBefore != configAfter)
                {
                    changes.Add(new(configPath, configBefore, configAfter));
                }
                if (!remove)
                {
                    notes.Add("Codex footer fields: native chat name and branch. New managed local sessions use a dedicated authenticated runtime for live name updates; existing sessions and standalone --no-daemon launches are unchanged.");
                }
            }
        }

        // A fresh copy keeps dry-run planning from mutating the loaded ownership record.
        ownership = ownership with { Desktop = Json.Decode<DesktopOwnership>(Json.Encode(previous.Desktop)) };
        if (desktop || remove && ownership.Desktop.Providers.Overlaps(selected))
        {
            new DesktopSetup(paths, executable, userHome).Plan(selected, remove && !removeProject, ownership.Desktop, changes, actions, notes, project, removeProject);
        }

        var manifestBefore = File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath) : null;
        var manifestChange = new FileChange(ManifestPath, manifestBefore, Json.Encode(ownership));
        // Record ownership before installing hooks, and remove it only after removing hooks.
        if (remove)
        {
            changes.Add(manifestChange);
        }
        else
        {
            changes.Insert(0, manifestChange);
        }

        return new(changes, actions, notes, remove ? manifestChange : null);
    }

    private string SettingsPath(string provider)
    {
        var home = userHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var variable = provider == "claude" ? "CLAUDE_CONFIG_DIR" : "CODEX_HOME";
        var configuredRoot = userHome is null ? Environment.GetEnvironmentVariable(variable) : null;
        var root = configuredRoot ?? Path.Combine(home, "." + provider);
        return Path.Combine(root, provider == "claude" ? "settings.json" : "hooks.json");
    }
}

using BellTower.IO;

namespace BellTower.Setup;

public sealed class IntegrationSetup(DataPaths paths, string executable, string? userHome = null)
{
    /// <summary>A seam for a test to replace the task removal with one that fails; null uses the real Task Scheduler.</summary>
    internal Func<string, SetupAction>? RemoveTaskAction { get; init; }

    private string ManifestPath => Path.Combine(paths.Root, "integration.json");

    public SetupPlan Plan(string clients, bool remove = false, bool statusLine = false, bool desktop = false, string? project = null,
        bool migrateEditedHooks = false, bool hostBridge = false)
    {
        if (hostBridge && (desktop || statusLine || project is not null))
        {
            throw new ArgumentException("--host-bridge cannot be combined with desktop profiles, a status line, or project profiles.");
        }
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
            CodexFooters = new(previous.CodexFooters),
            Handlers = new(previous.Handlers),
            SelectedCodexHookShell = previous.SelectedCodexHookShell,
            NativeBridgeProviders = new(previous.NativeBridgeProviders, StringComparer.OrdinalIgnoreCase),
            ProviderTitles = new(previous.ProviderTitles),
            AdditionalData = previous.AdditionalData
        };
        var ingress = remove || removeProject ? null : IngressProvision.Plan(executable, paths, selected);
        if (ingress?.CodexShell is { } selectedCodexShell)
        {
            ownership = ownership with { SelectedCodexHookShell = selectedCodexShell.ToString() };
        }
        else if (remove && !removeProject && selected.Contains("codex", StringComparer.Ordinal))
        {
            ownership = ownership with { SelectedCodexHookShell = null };
        }
        var editor = new ProviderSettingsEditor(new HookCommandBuilder(executable, paths, ingress?.Target, ingress?.CodexShell));
        var changes = new List<FileChange>();
        var actions = new List<SetupAction>();
        var notes = new List<string>();
        if (ingress is not null)
        {
            notes.Add("Provision verified immutable ingress: " + ingress.Target);
            if (ingress.Launcher is not null)
            {
                notes.Add("Register the on-demand per-user tray start task " + BellTower.Protocol.HookStartTask.TaskPath(paths.Root) +
                    " (no trigger, limited interactive principal, one instance). It never runs by itself: only a hook whose start from the user's shell failed runs it, and removing the last hook deletes it.");
            }
            if (ingress.CodexShell is { } codexShell)
            {
                notes.Add("Codex hook shell selected by isolated native probe: " + codexShell + ". Apply re-verifies this exact shell and target before writing settings.");
            }
        }
        foreach (var provider in selected)
        {
            if (removeProject)
            {
                continue;
            }
            var path = SettingsPath(provider);
            var before = File.Exists(path) ? File.ReadAllText(path) : null;
            var after = editor.Edit(provider, path, before, previous, ownership, remove && !removeProject, (statusLine || desktop) && !removeProject,
                migrateEditedHooks, notes);
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
        if (desktop || remove && !hostBridge && ownership.Desktop.Providers.Overlaps(selected))
        {
            new DesktopSetup(paths, executable, userHome).Plan(selected, remove && !removeProject, ownership.Desktop, changes, actions, notes, project, removeProject);
        }

        if (remove && !removeProject)
        {
            ownership.NativeBridgeProviders.ExceptWith(selected);
        }
        else if (hostBridge)
        {
            ownership.NativeBridgeProviders.UnionWith(selected);
        }

        var desktopBridgeRequested = ownership.Desktop.Providers.Count != 0 ||
            ownership.Desktop.Projects.Values.Any(providers => providers.Count != 0);
        var nativeBridgeRequested = ownership.NativeBridgeProviders.Count != 0;
        var desktopProjectBridgeAffected = previous.Desktop.Projects.Values.Any(providers => providers.Overlaps(selected));
        var bridgeManagementRequested = hostBridge || desktop || remove &&
            (previous.Desktop.Providers.Overlaps(selected) || previous.NativeBridgeProviders.Overlaps(selected) || desktopProjectBridgeAffected);
        if (bridgeManagementRequested)
        {
            var settingsPath = VsCodeBridgeSettings.SettingsPath(userHome);
            VsCodeBridgeSettings.Plan(settingsPath, executable, paths.Root, ownership.Desktop.Settings,
                desktopBridgeRequested || nativeBridgeRequested, changes, notes);
            VsCodeBridgeSetup.Plan(executable, userHome, ownership.Desktop,
                !(desktopBridgeRequested || nativeBridgeRequested), actions, notes);
        }

        if (remove && !removeProject && ownership.Handlers.Count == 0)
        {
            var state = BellTower.Protocol.HookStartTask.RecordState(paths.Root);
            if (state == "valid")
            {
                // The task goes first and its record is deleted only after that succeeded, so a failed removal can be retried.
                var recordedTask = BellTower.Protocol.HookStartTask.Recorded(paths.Root)!;
                actions.Add(RemoveTaskAction?.Invoke(recordedTask) ?? HookStartTaskSetup.RemoveAction(recordedTask, paths.Root));
                notes.Add("Remove the on-demand tray start task " + recordedTask + ".");
            }
            else if (state == "invalid")
            {
                // A record that is not a Bell Tower task path never names a task to remove; only the record itself goes.
                var record = BellTower.Protocol.HookStartTask.RecordPath(paths.Root);
                changes.Add(new(record, File.ReadAllText(record), null));
                notes.Add("Ignored the tray start task record because it is not a Bell Tower task path; the record was removed and no task was touched.");
            }
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

        return new(changes, actions, notes, remove ? manifestChange : null, ingress);
    }

    /// <summary>
    /// The opt-in provider title step (design decision 11): one key per provider, previewed, backed up, idempotent and removable. It is
    /// planned on its own, never together with hooks, profiles or the bridge, so it changes only what it names.
    /// </summary>
    public SetupPlan PlanProviderTitles(string clients, bool remove = false)
    {
        string[] selected = clients switch
        {
            "both" => ["claude", "codex"],
            "claude" => ["claude"],
            "codex" => ["codex"],
            _ => throw new ArgumentException("Clients must be both, claude, or codex.")
        };
        var previous = AtomicFile.Read<IntegrationOwnership>(ManifestPath) ?? new([], []);
        var ownership = new IntegrationOwnership(new(previous.Commands), new(previous.StatusLines))
        {
            Desktop = Json.Decode<DesktopOwnership>(Json.Encode(previous.Desktop)),
            CodexFooters = new(previous.CodexFooters),
            Handlers = new(previous.Handlers),
            SelectedCodexHookShell = previous.SelectedCodexHookShell,
            NativeBridgeProviders = new(previous.NativeBridgeProviders, StringComparer.OrdinalIgnoreCase),
            ProviderTitles = new(previous.ProviderTitles),
            AdditionalData = previous.AdditionalData
        };
        var changes = new List<FileChange>();
        var notes = new List<string>();
        foreach (var provider in selected)
        {
            if (!ProviderTitleSettings.Verified.Contains(provider))
            {
                notes.Add("Provider title step is not offered for " + provider + ": its setting has not been verified to stop the provider's title writes.");
                continue;
            }
            var path = provider == "claude" ? SettingsPath("claude") : Path.Combine(Path.GetDirectoryName(SettingsPath("codex"))!, "config.toml");
            var before = File.Exists(path) ? File.ReadAllText(path) : null;
            var after = provider == "claude"
                ? ProviderTitleSettings.EditClaude(path, before, previous.ProviderTitles, ownership.ProviderTitles, remove, notes)
                : ProviderTitleSettings.EditCodex(path, before, previous.ProviderTitles, ownership.ProviderTitles, remove, notes);
            if (before != after && after is not null)
            {
                changes.Add(new(path, before, after));
            }
            notes.Add((remove ? "Remove" : "Disable") + " " + provider + " terminal title: " + (provider == "claude"
                ? "settings env " + ProviderTitleSettings.ClaudeKey + "=" + ProviderTitleSettings.ClaudeValue
                : "[tui] " + ProviderTitleSettings.CodexKey + " = " + ProviderTitleSettings.CodexValue) + (before == after ? " (no change)" : ""));
        }
        if (!remove)
        {
            notes.Add(ProviderTitleSettings.Consent);
        }
        var manifestBefore = File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath) : null;
        var manifestChange = new FileChange(ManifestPath, manifestBefore, Json.Encode(ownership));
        if (manifestChange.Before == manifestChange.After)
        {
            return new(changes, [], notes);
        }
        // Applying records ownership first. Removing keeps it until the settings were restored, so a failed restore can be retried.
        if (remove)
        {
            changes.Add(manifestChange);
        }
        else
        {
            changes.Insert(0, manifestChange);
        }
        return new(changes, [], notes, remove ? manifestChange : null);
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

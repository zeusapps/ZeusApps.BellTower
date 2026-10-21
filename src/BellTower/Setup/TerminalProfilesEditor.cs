using System.Text.Json.Nodes;
using BellTower.IO;

namespace BellTower.Setup;

public sealed class TerminalProfilesEditor(DesktopOwnership ownership, List<string> notes)
{
    public void VsCode(string path, IReadOnlyList<string> providers, bool remove, string executable, string dataDirectory, List<FileChange> changes)
    {
        var file = new JsonSettingsFile(path);
        var profiles = file.Root["terminal.integrated.profiles.windows"]?.AsObject().DeepClone().AsObject() ?? new JsonObject();
        foreach (var old in ObsoleteProfiles(path, "vscode", providers, remove))
        {
            var restored = Restore(old, profiles[old.Key]);
            if (restored.Restored)
            {
                if (restored.Value is null)
                {
                    profiles.Remove(old.Key);
                }
                else
                {
                    profiles[old.Key] = restored.Value;
                }
            }
        }
        foreach (var provider in providers)
        {
            foreach (var role in ProfilesFor(provider))
            {
                var current = profiles[role.Label];
                var owned = Find(path, "vscode", role.Label);
                if (remove)
                {
                    var restored = Restore(owned, current);
                    if (restored.Restored)
                    {
                        if (restored.Value is null)
                        {
                            profiles.Remove(role.Label);
                        }
                        else
                        {
                            profiles[role.Label] = restored.Value;
                        }
                    }
                    continue;
                }
                if (!MayInstall(owned, current))
                {
                    continue;
                }
                if (owned is null && current is not null && !CallsFunction(current["args"], role.ShellCommand))
                {
                    throw new InvalidOperationException("An unrelated VS Code profile already uses the name " + role.Label + ". Rename it before desktop setup.");
                }
                var desired = current?.DeepClone().AsObject() ?? new JsonObject
                {
                    ["source"] = "PowerShell",
                    ["args"] = new JsonArray("-NoExit", "-Command", role.ShellCommand),
                    ["icon"] = role.Icon,
                    ["color"] = role.Color
                };
                desired.Remove("overrideName");
                profiles[role.Label] = desired;
                Remember(path, "vscode", role, role.Label, current, desired, owned);
            }
        }
        file.Set("terminal.integrated.profiles.windows", profiles.Count == 0 ? null : profiles);
        if (!remove || ownership.Providers.Count == 0)
        {
            Setting(file, path, "bellTower.executablePath", remove ? null : JsonValue.Create(executable), remove);
            Setting(file, path, "bellTower.dataDirectory", remove ? null : JsonValue.Create(dataDirectory), remove);
            Setting(file, path, "terminal.integrated.tabs.title", remove ? null : JsonValue.Create("${sequence}"), remove);
        }
        file.AddTo(changes);
    }

    public void WindowsTerminal(string path, IReadOnlyList<string> providers, bool remove, List<FileChange> changes, string? home = null)
    {
        var file = new JsonSettingsFile(path);
        var container = file.Root["profiles"]?.AsObject().DeepClone().AsObject() ?? new JsonObject();
        var list = container["list"]?.AsArray() ?? new JsonArray();
        if (container["list"] is null)
        {
            container["list"] = list;
        }
        foreach (var old in ObsoleteProfiles(path, "windows-terminal", providers, remove))
        {
            var current = list.FirstOrDefault(node => node?["guid"]?.GetValue<string>() == old.Key);
            var restored = Restore(old, current);
            if (restored.Restored && current is not null)
            {
                var index = list.IndexOf(current);
                if (restored.Value is null)
                {
                    list.RemoveAt(index);
                }
                else
                {
                    list[index] = restored.Value;
                }
            }
        }
        foreach (var provider in providers)
        {
            foreach (var role in ProfilesFor(provider))
            {
                var guid = ProfileGuid(role.Label);
                var current = list.FirstOrDefault(node => node?["guid"]?.GetValue<string>() == guid);
                if (current is null && list.Any(node => node?["name"]?.GetValue<string>() == role.Label))
                {
                    if (remove)
                    {
                        continue;
                    }
                    throw new InvalidOperationException("An unrelated Windows Terminal profile already uses the name " + role.Label + ". Rename it before desktop setup.");
                }
                var owned = Find(path, "windows-terminal", guid);
                if (remove)
                {
                    var restored = Restore(owned, current);
                    if (restored.Restored && current is not null)
                    {
                        var index = list.IndexOf(current);
                        if (restored.Value is null)
                        {
                            list.RemoveAt(index);
                        }
                        else
                        {
                            list[index] = restored.Value;
                        }
                    }
                    continue;
                }
                if (!MayInstall(owned, current))
                {
                    continue;
                }
                if (owned is null && current is not null)
                {
                    throw new InvalidOperationException("A Windows Terminal profile conflicts with Bell Tower's profile ID.");
                }
                var desired = current?.DeepClone().AsObject() ?? new JsonObject();
                desired["guid"] = guid;
                desired["name"] = role.Label;
                desired["commandline"] = "\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe") + "\" -NoExit -Command \"" + role.ShellCommand + "\"";
                desired["startingDirectory"] = role.Directory ?? home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                desired["suppressApplicationTitle"] = false;
                if (current is null)
                {
                    list.Add(desired);
                }
                else
                {
                    list[list.IndexOf(current)] = desired;
                }
                Remember(path, "windows-terminal", role, guid, current, desired, owned);
            }
        }
        file.Set("profiles", container);
        file.AddTo(changes);
    }

    private TerminalProfileOwnership? Find(string path, string host, string key) => ownership.Profiles.SingleOrDefault(profile => profile.File == path && profile.Host == host && profile.Key == key);

    private TerminalProfileOwnership[] ObsoleteProfiles(string path, string host, IReadOnlyList<string> providers, bool remove)
    {
        var active = providers.SelectMany(ProfilesFor)
            .Select(profile => host == "vscode" ? profile.Label : ProfileGuid(profile.Label)).ToHashSet();
        return ownership.Profiles.Where(profile => profile.File == path && profile.Host == host && providers.Contains(profile.Provider) &&
            (remove || !active.Contains(profile.Key))).ToArray();
    }

    private static string ProfileGuid(string label) => "{" + new Guid(Convert.FromHexString(ContentHash.Compute("bell-tower-profile:" + label)[..32])).ToString() + "}";

    private IReadOnlyList<RoleProfile> ProfilesFor(string provider)
    {
        var profiles = RoleProfile.For(provider).Concat(ownership.Projects.Where(pair => pair.Value.Contains(provider))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => RoleProfile.ForProject(provider, pair.Key))).ToArray();
        if (profiles.GroupBy(profile => profile.Label, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Project profile names must be unique. Projects with the same folder name cannot share a terminal profile label.");
        }
        return profiles;
    }

    private bool MayInstall(TerminalProfileOwnership? owned, JsonNode? current)
    {
        if (owned is null || current is null || JsonSettingsFile.Same(current, owned.After))
        {
            return true;
        }
        notes.Add("Kept user-edited terminal profile: " + owned.Key);
        return false;
    }

    private (bool Restored, JsonNode? Value) Restore(TerminalProfileOwnership? owned, JsonNode? current)
    {
        if (owned is null)
        {
            return (false, null);
        }
        if (JsonSettingsFile.Same(current, owned.Before))
        {
            ownership.Profiles.Remove(owned);
            return (false, null);
        }
        if (current is not null && !JsonSettingsFile.Same(current, owned.After))
        {
            notes.Add("Kept user-edited terminal profile: " + owned.Key);
            return (false, null);
        }
        ownership.Profiles.Remove(owned);
        return (true, owned.Before is null ? null : JsonNode.Parse(owned.Before));
    }

    private void Remember(string path, string host, RoleProfile role, string key, JsonNode? before, JsonNode after, TerminalProfileOwnership? previous)
    {
        if (previous is not null)
        {
            ownership.Profiles.Remove(previous);
        }
        ownership.Profiles.Add(new(path, host, role.Provider, key, previous is null ? before?.ToJsonString() : previous.Before, after.ToJsonString()));
    }

    private void Setting(JsonSettingsFile file, string path, string name, JsonNode? value, bool remove)
    {
        if (!ownership.Settings.TryGetValue(path, out var settings))
        {
            settings = [];
            ownership.Settings[path] = settings;
        }
        var previous = settings.GetValueOrDefault(name);
        if (remove && previous is null)
        {
            return;
        }
        if (remove && JsonSettingsFile.Same(file.Root[name], previous!.Before))
        {
            settings.Remove(name);
            return;
        }
        if (previous is not null && !JsonSettingsFile.Same(file.Root[name], previous.After))
        {
            notes.Add("Kept user-edited setting: " + name);
            return;
        }
        if (remove)
        {
            file.Set(name, previous!.Before is null ? null : JsonNode.Parse(previous.Before));
            settings.Remove(name);
        }
        else
        {
            settings[name] = new(previous is null ? file.Root[name]?.ToJsonString() : previous.Before, value!.ToJsonString());
            file.Set(name, value);
        }
    }

    private static bool CallsFunction(JsonNode? arguments, string function) => arguments is JsonArray array &&
        array.LastOrDefault()?.GetValue<string>() == function;
}

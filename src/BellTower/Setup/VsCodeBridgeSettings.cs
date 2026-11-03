using System.Text.Json.Nodes;

namespace BellTower.Setup;

public static class VsCodeBridgeSettings
{
    public static string SettingsPath(string? home)
    {
        var roaming = home is null
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Path.Combine(home, "AppData", "Roaming");
        return Path.Combine(roaming, "Code", "User", "settings.json");
    }

    public static void Plan(string path, string executable, string dataDirectory,
        Dictionary<string, Dictionary<string, OwnedValue>> ownership, bool required, List<FileChange> changes, List<string> notes)
    {
        if (!required && !ownership.ContainsKey(path))
        {
            return;
        }

        var existingChange = changes.FindIndex(change => Path.GetFullPath(change.Path).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        var file = new JsonSettingsFile(path, existingChange >= 0 ? changes[existingChange].After : null);
        OwnedSettings.Set(file, path, "bellTower.executablePath", required ? JsonValue.Create(executable) : null,
            !required, ownership, notes);
        OwnedSettings.Set(file, path, "bellTower.dataDirectory", required ? JsonValue.Create(dataDirectory) : null,
            !required, ownership, notes);
        var combined = new List<FileChange>();
        file.AddTo(combined);
        if (combined.Count == 0)
        {
            return;
        }
        if (existingChange >= 0)
        {
            changes[existingChange] = changes[existingChange] with { After = combined[0].After };
        }
        else
        {
            changes.Add(combined[0]);
        }
    }
}

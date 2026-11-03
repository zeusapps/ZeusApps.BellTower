using System.Text.Json.Nodes;

namespace BellTower.Setup;

internal static class OwnedSettings
{
    public static void Set(JsonSettingsFile file, string path, string name, JsonNode? value, bool remove,
        Dictionary<string, Dictionary<string, OwnedValue>> ownership, List<string> notes)
    {
        if (!ownership.TryGetValue(path, out var settings) && remove)
        {
            return;
        }
        if (settings is null)
        {
            settings = [];
            ownership[path] = settings;
        }
        var previous = settings.GetValueOrDefault(name);
        if (remove && previous is null)
        {
            return;
        }
        if (remove && JsonSettingsFile.Same(file.Root[name], previous!.Before))
        {
            settings.Remove(name);
            if (settings.Count == 0)
            {
                ownership.Remove(path);
            }
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
            if (settings.Count == 0)
            {
                ownership.Remove(path);
            }
        }
        else
        {
            settings[name] = new(previous is null ? file.Root[name]?.ToJsonString() : previous.Before, value!.ToJsonString());
            file.Set(name, value);
        }
    }
}

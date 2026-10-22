using System.Text.Json;

namespace BellTower.Distribution;

public static class DistributionJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true, MaxDepth = 24 };
    public static T Read<T>(string path, int limit = 262144)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > limit)
        { throw new InvalidDataException("Metadata exceeds its size limit."); }
        return JsonSerializer.Deserialize<T>(stream, Options) ?? throw new InvalidDataException("Missing metadata.");
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, Options));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
    }
}

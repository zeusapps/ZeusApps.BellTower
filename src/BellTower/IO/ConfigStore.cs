using System.Text.Json;
using BellTower.Core;

namespace BellTower.IO;

public sealed class ConfigStore(DataPaths paths)
{
    private AppConfig lastGood = new();
    public AppConfig Read()
    {
        try
        {
            lastGood = AtomicFile.Read<AppConfig>(paths.Config) ?? lastGood;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
        {
            new LocalLog(paths).Write("config-invalid");
        }

        return lastGood;
    }

    public void Update(Action<AppConfig> update)
    {
        using var gate = new DataLock(paths, "config");
        var config = Read();
        update(config);
        AtomicFile.Write(paths.Config, Json.Encode(config));
    }
}

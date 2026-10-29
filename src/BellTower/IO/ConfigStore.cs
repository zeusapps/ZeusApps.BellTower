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
            var candidate = AtomicFile.Read<AppConfig>(paths.Config);
            if (candidate is not null)
            {
                candidate.Updates ??= new();
                candidate.Reminders ??= new();
                candidate.Reminders.Validate();
                candidate.AttachmentEvidenceGraceMinutes = Math.Clamp(candidate.AttachmentEvidenceGraceMinutes, 1, 1440);
                lastGood = candidate;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or ArgumentOutOfRangeException)
        {
            new LocalLog(paths).Write("config-invalid");
        }

        return lastGood;
    }

    public void Update(Action<AppConfig> update)
    {
        using var gate = new DataLock(paths, "config");
        var config = Json.Decode<AppConfig>(Json.Encode(Read()))!;
        update(config);
        config.Reminders.Validate();
        AtomicFile.Write(paths.Config, Json.Encode(config));
        lastGood = config;
    }
}

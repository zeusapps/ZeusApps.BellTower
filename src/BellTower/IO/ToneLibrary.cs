using System.Security.Cryptography;
using BellTower.Core;
using BellTower.Tray;

namespace BellTower.IO;

public sealed class ToneLibrary(DataPaths paths)
{
    private static readonly NotificationTone Classic = new(Chime.Wave(), Chime.Milliseconds);
    private string? cachedFile;
    private NotificationTone? cached;

    public NotificationTone Current(AttentionReason reason = AttentionReason.Done)
    {
        var file = new ConfigStore(paths).Read().ToneFile;
        if (file is null)
        {
            return DefaultTones.For(reason);
        }
        if (file == "builtin:chime")
        {
            return Classic;
        }
        if (file == cachedFile && cached is not null)
        {
            return cached;
        }
        try
        {
            if (file.Length != 68 || !file.EndsWith(".wav", StringComparison.Ordinal) || !file[..64].All(Uri.IsHexDigit))
            {
                throw new InvalidDataException("Invalid owned tone name.");
            }
            cached = PcmWave.Load(Path.Combine(paths.Root, "tones", file));
            cachedFile = file;
            return cached;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            new LocalLog(paths).Write("notification-tone-unavailable");
            return DefaultTones.For(reason);
        }
    }

    public NotificationTone Select(string source)
    {
        var tone = PcmWave.Load(source);
        var name = Convert.ToHexString(SHA256.HashData(tone.Wave)).ToLowerInvariant() + ".wav";
        var directory = Path.Combine(paths.Root, "tones");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, name);
        if (!File.Exists(destination))
        {
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, tone.Wave);
                File.Move(temporary, destination, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
        new ConfigStore(paths).Update(config =>
        {
            config.ToneFile = name;
            config.ToneName = Path.GetFileName(source);
        });
        return tone;
    }

    public void Reset() => new ConfigStore(paths).Update(config =>
    {
        config.ToneFile = null;
        config.ToneName = null;
    });

    public void UseClassicChime() => new ConfigStore(paths).Update(config =>
    {
        config.ToneFile = "builtin:chime";
        config.ToneName = "Classic chime";
    });
}
